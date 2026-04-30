using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32.SafeHandles;

namespace SwpilotCLIAddin
{
    internal sealed class PSTerminalControl : UserControl
    {
        private static readonly string[] ImageExtensions =
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp",
            ".tif", ".tiff", ".ico", ".heic", ".heif"
        };

        private readonly WebView2 webView;
        private readonly Label statusLabel;
        private readonly object outputLock = new object();
        private readonly Queue<string> pendingOutput = new Queue<string>();

        private PseudoConsoleSession session;
        private bool rendererReady;
        private bool browserInitialized;
        private string pendingInitialCommand;
        private string pendingExitCommand = "exit";
        private string pendingShellCommandLine = "powershell.exe -NoLogo -NoExit";

        public PSTerminalControl()
        {
            Dock = DockStyle.Fill;
            BackColor = System.Drawing.Color.Black;

            webView = new WebView2
            {
                Dock = DockStyle.Fill
            };
            webView.DragEnter += WebView_DragEnter;
            webView.DragDrop += WebView_DragDrop;
            Controls.Add(webView);

            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.Black,
                ForeColor = System.Drawing.Color.LightGray,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Font = new System.Drawing.Font("Consolas", 10f),
                Text = "Starting terminal...",
                Visible = true
            };
            Controls.Add(statusLabel);
            statusLabel.BringToFront();
        }

        private void WebView_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void WebView_DragDrop(object sender, DragEventArgs e)
        {
            if (session == null || e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0)
                return;

            var imageFiles = files
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Where(path => ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .ToArray();
            if (imageFiles.Length == 0)
                return;

            string input = string.Join(" ", imageFiles.Select(QuotePowerShellLiteral)) + " ";
            try { session.WriteInput(input); } catch { }
        }

        private static string QuotePowerShellLiteral(string path)
        {
            return "'" + path.Replace("'", "''") + "'";
        }

        public async void StartSession(
            string workingDirectory,
            string initialCommand = null,
            string exitCommand = "exit",
            string statusText = "Starting terminal...",
            string shellCommandLine = "powershell.exe -NoLogo -NoExit")
        {
            StopSession();

            rendererReady = false;
            pendingInitialCommand = initialCommand;
            pendingExitCommand = string.IsNullOrWhiteSpace(exitCommand) ? "exit" : exitCommand;
            pendingShellCommandLine = string.IsNullOrWhiteSpace(shellCommandLine)
                ? "powershell.exe -NoLogo -NoExit"
                : shellCommandLine;
            SetStatus(string.IsNullOrWhiteSpace(statusText) ? "Starting terminal..." : statusText, true);
            lock (outputLock)
            {
                pendingOutput.Clear();
            }

            try
            {
                await EnsureBrowserReadyAsync();
                if (webView.CoreWebView2 != null)
                    webView.CoreWebView2.NavigateToString(BuildTerminalHtml());
            }
            catch (Exception ex)
            {
                SetStatus("WebView2 init failed: " + ex.Message, true);
                return;
            }

            try
            {
                string launchDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    : workingDirectory;

                session = new PseudoConsoleSession();
                session.OutputReceived += Session_OutputReceived;
                session.Start(
                    pendingShellCommandLine,
                    launchDirectory,
                    120,
                    30);
            }
            catch (Exception ex)
            {
                SetStatus("ConPTY start failed: " + ex.Message, true);
                StopSession();
            }
        }

        public void StopSession()
        {
            if (session == null)
                return;

            try
            {
                string exitCmd = string.IsNullOrWhiteSpace(pendingExitCommand) ? "exit" : pendingExitCommand;
                session.WriteInput(exitCmd + "\r");
            }
            catch { }
            try { session.Dispose(); } catch { }
            session = null;

            lock (outputLock)
            {
                pendingOutput.Clear();
            }
            rendererReady = false;
            SetStatus(string.Empty, false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopSession();
                webView.Dispose();
            }

            base.Dispose(disposing);
        }

        private async System.Threading.Tasks.Task EnsureBrowserReadyAsync()
        {
            if (browserInitialized && webView.CoreWebView2 != null)
                return;

            string userDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SwpilotCLI",
                "WebView2",
                "PSTerminal");
            Directory.CreateDirectory(userDataDir);

            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataDir);
            await webView.EnsureCoreWebView2Async(env);

            string assetRoot = ResolveWebAssetRoot();
            if (string.IsNullOrEmpty(assetRoot))
                throw new DirectoryNotFoundException("Cannot find web assets folder (web/xterm).");

            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "swpilot.local",
                assetRoot,
                CoreWebView2HostResourceAccessKind.Allow);

            webView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            browserInitialized = true;
        }

        private static string ResolveWebAssetRoot()
        {
            string assemblyDir = Path.GetDirectoryName(typeof(PSTerminalControl).Assembly.Location);
            if (!string.IsNullOrWhiteSpace(assemblyDir))
            {
                string candidate = Path.Combine(assemblyDir, "web");
                if (Directory.Exists(candidate))
                    return candidate;
            }

            string appBaseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(appBaseDir))
            {
                string candidate = Path.Combine(appBaseDir, "web");
                if (Directory.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private void Session_OutputReceived(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return;

            string base64 = Convert.ToBase64String(bytes);
            bool shouldQueue;

            lock (outputLock)
            {
                shouldQueue = !rendererReady;
                if (shouldQueue)
                    pendingOutput.Enqueue(base64);
            }

            if (!shouldQueue)
                PushBase64ToRenderer(base64);
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string json = e.WebMessageAsJson ?? string.Empty;

            if (json.Contains("\"type\":\"ready\""))
            {
                rendererReady = true;
                SetStatus(string.Empty, false);
                FlushPendingOutput();
                if (!string.IsNullOrWhiteSpace(pendingInitialCommand))
                {
                    string cmd = pendingInitialCommand;
                    System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
                    {
                        if (session != null)
                            session.WriteInput(cmd + "\r");
                    });
                }
                return;
            }

            if (json.Contains("\"type\":\"error\""))
            {
                string message = DecodeJsonText(ExtractJsonString(json, "data"));
                SetStatus(string.IsNullOrWhiteSpace(message)
                    ? "Terminal renderer load failed."
                    : message, true);
                return;
            }

            if (json.Contains("\"type\":\"input\""))
            {
                string data = ExtractJsonString(json, "data");
                if (!string.IsNullOrEmpty(data) && session != null)
                {
                    string input = DecodeJsonText(data);
                    if (input == "\n")
                        input = "\r";
                    else if (!string.IsNullOrEmpty(input))
                        input = input.Replace("\r\n", "\r");

                    session.WriteInput(input);
                }
                return;
            }

            if (json.Contains("\"type\":\"resize\""))
            {
                int cols = ExtractJsonInt(json, "cols");
                int rows = ExtractJsonInt(json, "rows");
                if (cols > 0 && rows > 0 && session != null)
                    session.Resize(cols, rows);
            }
        }

        private void FlushPendingOutput()
        {
            List<string> chunks = new List<string>();

            lock (outputLock)
            {
                while (pendingOutput.Count > 0)
                    chunks.Add(pendingOutput.Dequeue());
            }

            foreach (string chunk in chunks)
                PushBase64ToRenderer(chunk);
        }

        private void PushBase64ToRenderer(string base64)
        {
            if (string.IsNullOrEmpty(base64))
                return;

            Action run = () =>
            {
                try
                {
                    if (webView.CoreWebView2 == null)
                        return;
                    webView.ExecuteScriptAsync("window.__appendBase64 && window.__appendBase64('" + base64 + "');");
                }
                catch { }
            };

            if (InvokeRequired)
                BeginInvoke(run);
            else
                run();
        }

        private void SetStatus(string message, bool visible)
        {
            Action run = () =>
            {
                statusLabel.Text = message ?? string.Empty;
                statusLabel.Visible = visible;
                if (visible)
                    statusLabel.BringToFront();
            };

            if (InvokeRequired)
                BeginInvoke(run);
            else
                run();
        }

        private static string BuildTerminalHtml()
        {
            return @"<!doctype html>
<html>
<head>
  <meta charset='utf-8' />
  <meta http-equiv='X-UA-Compatible' content='IE=edge' />
  <meta name='viewport' content='width=device-width, initial-scale=1.0' />
  <link rel='stylesheet' href='https://swpilot.local/xterm/xterm.css' />
  <style>
    html, body { margin: 0; padding: 0; width: 100%; height: 100%; background: #0b0b0b; overflow: hidden; }
    #term { width: 100%; height: 100%; padding: 6px; box-sizing: border-box; }
    #loading { position: absolute; left: 12px; top: 10px; color: #b8b8b8; font-family: Consolas, monospace; font-size: 12px; }
  </style>
</head>
<body>
  <div id='loading'>Loading terminal renderer...</div>
  <div id='term'></div>
  <script src='https://swpilot.local/xterm/xterm.js'></script>
  <script>
    (() => {
      if (typeof Terminal === 'undefined') {
        if (window.chrome && window.chrome.webview) {
          window.chrome.webview.postMessage({ type: 'error', data: 'xterm.js load failed from local assets.' });
        }
        return;
      }

      const term = new Terminal({
        cursorBlink: true,
        fontSize: 14,
        lineHeight: 1.2,
        scrollback: 5000,
        fontFamily: 'Consolas, ""Cascadia Mono"", monospace',
        theme: {
          background: '#0b0b0b',
          foreground: '#d7d7d7',
          cursor: '#ffffff'
        }
      });
      term.open(document.getElementById('term'));
      const loading = document.getElementById('loading');
      if (loading) loading.remove();
      term.focus();
      const termHost = document.getElementById('term');

      const baseFontSize = 14;
      const minFontSize = 8;
      const maxFontSize = 40;
      const baseLineHeight = 1.2;
      const minLineHeight = 1.0;
      const maxLineHeight = 1.4;
      let fontSize = baseFontSize;
      let followOutput = true;
      const decoder = new TextDecoder('utf-8');

      function clamp(v, min, max) {
        return Math.max(min, Math.min(max, v));
      }

      function calcLineHeight(size) {
        const ratio = (size - baseFontSize) / (maxFontSize - baseFontSize);
        return clamp(baseLineHeight + ratio * 0.15, minLineHeight, maxLineHeight);
      }

      function syncTerminalLayout() {
        fitTerm();
        sendResize();
        setTimeout(() => { fitTerm(); sendResize(); }, 16);
        setTimeout(() => { fitTerm(); sendResize(); }, 60);
      }

      function isAtBottom() {
        try {
          const b = term && term.buffer && term.buffer.active;
          if (!b) return true;
          return b.viewportY >= b.baseY;
        } catch (_) {
          return true;
        }
      }

      function fitTerm() {
        const holder = document.getElementById('term');
        if (!holder) return;

        let cellW = 8;
        let cellH = 16;
        try {
          const d = term && term._core && term._core._renderService && term._core._renderService.dimensions;
          if (d && d.css && d.css.cell) {
            if (d.css.cell.width > 0) cellW = d.css.cell.width;
            if (d.css.cell.height > 0) cellH = d.css.cell.height;
          }
        } catch (_) {}

        const cols = Math.max(20, Math.floor((holder.clientWidth - 12) / cellW));
        const rows = Math.max(5, Math.floor((holder.clientHeight - 12) / cellH));
        if (cols > 0 && rows > 0 && (cols !== term.cols || rows !== term.rows)) {
          term.resize(cols, rows);
        }
      }

      function sendResize() {
        if (!window.chrome || !window.chrome.webview) return;
        window.chrome.webview.postMessage({
          type: 'resize',
          cols: Math.max(20, term.cols || 80),
          rows: Math.max(5, term.rows || 24)
        });
      }

      window.__appendText = function(text) {
        term.write(text || '');
      };

      window.__appendBase64 = function(base64) {
        if (!base64) return;
        const raw = atob(base64);
        const bytes = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
        term.write(decoder.decode(bytes, { stream: true }));
        if (followOutput) term.scrollToBottom();
      };

      term.onData(data => {
        if (!window.chrome || !window.chrome.webview) return;
        followOutput = true;
        window.chrome.webview.postMessage({ type: 'input', data: data });
      });

      term.onScroll(() => {
        followOutput = isAtBottom();
      });

      if (termHost) {
        termHost.addEventListener('mousedown', (ev) => {
          const target = ev.target;
          if (target && target.closest && target.closest('.xterm-viewport')) {
            followOutput = false;
          }
        }, true);
      }

      function onDragOver(ev) {
        if (!ev || !ev.dataTransfer) return;
        ev.preventDefault();
        ev.stopPropagation();
        ev.dataTransfer.dropEffect = 'copy';
      }

      function quotePowerShellLiteral(path) {
        return '\'' + String(path || '').replace(/'/g, '\'\'') + '\'';
      }

      function parseUriListPaths(dataTransfer) {
        if (!dataTransfer || !dataTransfer.getData) return [];
        const raw = dataTransfer.getData('text/uri-list') || '';
        if (!raw) return [];
        const lines = raw.split(/\r?\n/).map(s => s.trim()).filter(s => s && !s.startsWith('#'));
        const paths = [];
        for (const uri of lines) {
          if (!uri.toLowerCase().startsWith('file://')) continue;
          let p = uri.replace(/^file:\/\/\//i, '');
          p = decodeURIComponent(p).replace(/\//g, '\\');
          if (p) paths.push(p);
        }
        return paths;
      }

      function sendTextToInput(text) {
        if (!text || !window.chrome || !window.chrome.webview) return;
        window.chrome.webview.postMessage({ type: 'input', data: text });
      }

      function onDrop(ev) {
        if (!ev || !ev.dataTransfer) return;
        ev.preventDefault();
        ev.stopPropagation();

        const files = ev.dataTransfer.files;
        if (!files || files.length === 0) return;

        const uriPaths = parseUriListPaths(ev.dataTransfer);
        const args = [];
        for (let i = 0; i < files.length; i++) {
          const f = files[i];
          const p = (uriPaths[i] && uriPaths[i].length > 0)
            ? uriPaths[i]
            : (f && f.path ? f.path : null);
          if (!p) continue;
          args.push(quotePowerShellLiteral(p));
        }

        if (args.length > 0) {
          sendTextToInput(args.join(' ') + ' ');
        }
        term.focus();
      }

      window.addEventListener('resize', () => {
        syncTerminalLayout();
      });

      function onWheel(ev) {
        if (!ev.ctrlKey) {
          if (ev.deltaY < 0) followOutput = false;
          // term.onScroll handles updating followOutput after xterm processes the scroll
          return;
        }

        ev.preventDefault();
        ev.stopImmediatePropagation();
        const delta = ev.deltaY < 0 ? 1 : -1;
        fontSize = clamp(fontSize + delta, minFontSize, maxFontSize);
        term.options.fontSize = fontSize;
        term.options.lineHeight = calcLineHeight(fontSize);
        setTimeout(syncTerminalLayout, 0);
      }

      // Capture phase ensures Ctrl+wheel zoom still works even when TUI consumes wheel events.
      document.addEventListener('wheel', onWheel, { passive: false, capture: true });
      // Prevent WebView default file-open behavior and convert drop to typed path input.
      document.addEventListener('dragover', onDragOver, true);
      document.addEventListener('drop', onDrop, true);

      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: 'ready' });
      }

      setTimeout(() => {
        syncTerminalLayout();
      }, 0);
    })();
  </script>
</body>
</html>";
        }

        private static int ExtractJsonInt(string json, string key)
        {
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(\\d+)");
            if (!m.Success)
                return 0;

            int value;
            return int.TryParse(m.Groups[1].Value, out value) ? value : 0;
        }

        private static string ExtractJsonString(string json, string key)
        {
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (!m.Success)
                return null;

            return m.Groups[1].Value;
        }

        private static string DecodeJsonText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string decoded = text
                .Replace("\\\\", "\\")
                .Replace("\\\"", "\"")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t");

            decoded = Regex.Replace(decoded, @"\\u([0-9a-fA-F]{4})",
                m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

            return decoded;
        }
    }

    internal sealed class PseudoConsoleSession : IDisposable
    {
        private const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
        private const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        private const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        private const int WAIT_TIMEOUT = 0x00000102;

        private IntPtr pseudoConsole = IntPtr.Zero;
        private IntPtr processHandle = IntPtr.Zero;
        private IntPtr threadHandle = IntPtr.Zero;
        private IntPtr attributeList = IntPtr.Zero;

        private FileStream inputStream;
        private FileStream outputStream;
        private Thread readerThread;
        private bool disposed;

        public event Action<byte[]> OutputReceived;

        public void Start(string commandLine, string workingDirectory, short cols, short rows)
        {
            ThrowIfDisposed();

            IntPtr hPipeInRead = IntPtr.Zero;
            IntPtr hPipeInWrite = IntPtr.Zero;
            IntPtr hPipeOutRead = IntPtr.Zero;
            IntPtr hPipeOutWrite = IntPtr.Zero;

            try
            {
                if (!CreatePipe(out hPipeInRead, out hPipeInWrite, IntPtr.Zero, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!CreatePipe(out hPipeOutRead, out hPipeOutWrite, IntPtr.Zero, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                COORD size;
                size.X = cols;
                size.Y = rows;

                int hr = CreatePseudoConsole(size, hPipeInRead, hPipeOutWrite, 0, out pseudoConsole);
                if (hr != 0)
                    throw new Win32Exception(hr, "CreatePseudoConsole failed");

                CloseHandle(hPipeInRead);
                hPipeInRead = IntPtr.Zero;
                CloseHandle(hPipeOutWrite);
                hPipeOutWrite = IntPtr.Zero;

                IntPtr lpSize = IntPtr.Zero;
                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
                attributeList = Marshal.AllocHGlobal(lpSize);
                if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref lpSize))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                IntPtr pConsole = pseudoConsole;
                if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    pConsole,
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                STARTUPINFOEX si = new STARTUPINFOEX();
                si.StartupInfo.cb = Marshal.SizeOf(typeof(STARTUPINFOEX));
                si.lpAttributeList = attributeList;

                PROCESS_INFORMATION pi;
                bool created = CreateProcess(
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT,
                    IntPtr.Zero,
                    workingDirectory,
                    ref si,
                    out pi);

                if (!created)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                processHandle = pi.hProcess;
                threadHandle = pi.hThread;

                inputStream = new FileStream(
                    new SafeFileHandle(hPipeInWrite, true),
                    FileAccess.Write,
                    4096,
                    false);
                hPipeInWrite = IntPtr.Zero;

                outputStream = new FileStream(
                    new SafeFileHandle(hPipeOutRead, true),
                    FileAccess.Read,
                    4096,
                    false);
                hPipeOutRead = IntPtr.Zero;

                readerThread = new Thread(ReadOutputLoop);
                readerThread.IsBackground = true;
                readerThread.Start();
            }
            finally
            {
                if (hPipeInRead != IntPtr.Zero) CloseHandle(hPipeInRead);
                if (hPipeInWrite != IntPtr.Zero) CloseHandle(hPipeInWrite);
                if (hPipeOutRead != IntPtr.Zero) CloseHandle(hPipeOutRead);
                if (hPipeOutWrite != IntPtr.Zero) CloseHandle(hPipeOutWrite);
            }
        }

        public void WriteInput(string text)
        {
            if (disposed || inputStream == null || string.IsNullOrEmpty(text))
                return;

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            try
            {
                inputStream.Write(bytes, 0, bytes.Length);
                inputStream.Flush();
            }
            catch { }
        }

        public void Resize(int cols, int rows)
        {
            if (disposed || pseudoConsole == IntPtr.Zero)
                return;

            cols = Math.Max(cols, 20);
            rows = Math.Max(rows, 5);

            COORD size;
            size.X = (short)Math.Min(cols, short.MaxValue);
            size.Y = (short)Math.Min(rows, short.MaxValue);
            ResizePseudoConsole(pseudoConsole, size);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            try { inputStream?.Dispose(); } catch { }
            try { outputStream?.Dispose(); } catch { }
            inputStream = null;
            outputStream = null;

            if (processHandle != IntPtr.Zero)
            {
                try
                {
                    if (WaitForSingleObject(processHandle, 300) == WAIT_TIMEOUT)
                        TerminateProcess(processHandle, 0);
                }
                catch { }
            }

            if (threadHandle != IntPtr.Zero)
            {
                CloseHandle(threadHandle);
                threadHandle = IntPtr.Zero;
            }

            if (processHandle != IntPtr.Zero)
            {
                CloseHandle(processHandle);
                processHandle = IntPtr.Zero;
            }

            if (attributeList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
                attributeList = IntPtr.Zero;
            }

            if (pseudoConsole != IntPtr.Zero)
            {
                ClosePseudoConsole(pseudoConsole);
                pseudoConsole = IntPtr.Zero;
            }
        }

        private void ReadOutputLoop()
        {
            byte[] buffer = new byte[8192];

            try
            {
                while (!disposed && outputStream != null)
                {
                    int read = outputStream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                        break;

                    byte[] chunk = new byte[read];
                    Buffer.BlockCopy(buffer, 0, chunk, 0, read);
                    OutputReceived?.Invoke(chunk);
                }
            }
            catch { }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(PseudoConsoleSession));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD
        {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreatePipe(
            out IntPtr hReadPipe,
            out IntPtr hWritePipe,
            IntPtr lpPipeAttributes,
            int nSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            int dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll")]
        private static extern int CreatePseudoConsole(
            COORD size,
            IntPtr hInput,
            IntPtr hOutput,
            uint dwFlags,
            out IntPtr phPC);

        [DllImport("kernel32.dll")]
        private static extern void ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll")]
        private static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList,
            int dwAttributeCount,
            int dwFlags,
            ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList,
            uint dwFlags,
            IntPtr attribute,
            IntPtr lpValue,
            IntPtr cbSize,
            IntPtr lpPreviousValue,
            IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
    }
}
