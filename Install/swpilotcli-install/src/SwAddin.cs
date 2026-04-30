using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using SolidWorksTools;
using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace SwpilotCLIAddin
{
    [Guid("3d0d23d7-48cb-440d-a8b6-9145cf48cf01"), ComVisible(true)]
    [SwAddin(
        Description = "SwpilotCLI - AI Terminal for SolidWorks",
        Title       = "SwpilotCLI",
        LoadAtStartup = true
        )]
    public class SwAddin : ISwAddin
    {
        #region Local Variables

        ISldWorks        iSwApp     = null;
        ICommandManager  iCmdMgr   = null;
        int              addinID   = 0;
        SwpilotTaskPane  swpilotPane = null;

        #region Event Handler Variables
        Hashtable openDocs = new Hashtable();
        SolidWorks.Interop.sldworks.SldWorks SwEventPtr = null;
        #endregion

        public ISldWorks    SwApp  { get { return iSwApp;  } }
        public ICommandManager CmdMgr { get { return iCmdMgr; } }
        public Hashtable    OpenDocs { get { return openDocs; } }

        #endregion

        #region SolidWorks Registration

        [ComRegisterFunctionAttribute]
        public static void RegisterFunction(Type t)
        {
            SwAddinAttribute SWattr = null;
            foreach (System.Attribute attr in typeof(SwAddin).GetCustomAttributes(false))
                if (attr is SwAddinAttribute) { SWattr = attr as SwAddinAttribute; break; }

            try
            {
                var hklm = Microsoft.Win32.Registry.LocalMachine;
                var hkcu = Microsoft.Win32.Registry.CurrentUser;

                string keyname = "SOFTWARE\\SolidWorks\\Addins\\{" + t.GUID.ToString() + "}";
                var addinkey = hklm.CreateSubKey(keyname);
                addinkey.SetValue(null, 0);
                addinkey.SetValue("Description", SWattr.Description);
                addinkey.SetValue("Title", SWattr.Title);

                keyname = "Software\\SolidWorks\\AddInsStartup\\{" + t.GUID.ToString() + "}";
                addinkey = hkcu.CreateSubKey(keyname);
                addinkey.SetValue(null, Convert.ToInt32(SWattr.LoadAtStartup), Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch (Exception e)
            {
                System.Windows.Forms.MessageBox.Show("Register error: " + e.Message);
            }
        }

        [ComUnregisterFunctionAttribute]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                Microsoft.Win32.Registry.LocalMachine.DeleteSubKey(
                    "SOFTWARE\\SolidWorks\\Addins\\{" + t.GUID.ToString() + "}");
                Microsoft.Win32.Registry.CurrentUser.DeleteSubKey(
                    "Software\\SolidWorks\\AddInsStartup\\{" + t.GUID.ToString() + "}");
            }
            catch (Exception e)
            {
                System.Windows.Forms.MessageBox.Show("Unregister error: " + e.Message);
            }
        }

        #endregion

        #region ISwAddin Implementation

        public SwAddin() { }

        public bool ConnectToSW(object ThisSW, int cookie)
        {
            iSwApp  = (ISldWorks)ThisSW;
            addinID = cookie;
            iSwApp.SetAddinCallbackInfo(0, this, addinID);

            iCmdMgr = iSwApp.GetCommandManager(cookie);

            SwEventPtr = (SolidWorks.Interop.sldworks.SldWorks)iSwApp;
            openDocs   = new Hashtable();
            AttachEventHandlers();

            swpilotPane = new SwpilotTaskPane(iSwApp);
            swpilotPane.Create();

            return true;
        }

        public bool DisconnectFromSW()
        {
            if (swpilotPane != null) { swpilotPane.Remove(); swpilotPane = null; }

            DetachEventHandlers();

            Marshal.ReleaseComObject(iCmdMgr); iCmdMgr = null;
            Marshal.ReleaseComObject(iSwApp);  iSwApp  = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            return true;
        }

        #endregion

        #region Event Methods

        public bool AttachEventHandlers()
        {
            AttachSwEvents();
            AttachEventsToAllDocuments();
            return true;
        }

        private bool AttachSwEvents()
        {
            try
            {
                SwEventPtr.ActiveDocChangeNotify    += new DSldWorksEvents_ActiveDocChangeNotifyEventHandler(OnDocChange);
                SwEventPtr.DocumentLoadNotify2      += new DSldWorksEvents_DocumentLoadNotify2EventHandler(OnDocLoad);
                SwEventPtr.FileNewNotify2           += new DSldWorksEvents_FileNewNotify2EventHandler(OnFileNew);
                SwEventPtr.ActiveModelDocChangeNotify += new DSldWorksEvents_ActiveModelDocChangeNotifyEventHandler(OnModelChange);
                SwEventPtr.FileOpenPostNotify       += new DSldWorksEvents_FileOpenPostNotifyEventHandler(FileOpenPostNotify);
                return true;
            }
            catch (Exception e) { Console.WriteLine(e.Message); return false; }
        }

        private bool DetachSwEvents()
        {
            try
            {
                SwEventPtr.ActiveDocChangeNotify    -= new DSldWorksEvents_ActiveDocChangeNotifyEventHandler(OnDocChange);
                SwEventPtr.DocumentLoadNotify2      -= new DSldWorksEvents_DocumentLoadNotify2EventHandler(OnDocLoad);
                SwEventPtr.FileNewNotify2           -= new DSldWorksEvents_FileNewNotify2EventHandler(OnFileNew);
                SwEventPtr.ActiveModelDocChangeNotify -= new DSldWorksEvents_ActiveModelDocChangeNotifyEventHandler(OnModelChange);
                SwEventPtr.FileOpenPostNotify       -= new DSldWorksEvents_FileOpenPostNotifyEventHandler(FileOpenPostNotify);
                return true;
            }
            catch (Exception e) { Console.WriteLine(e.Message); return false; }
        }

        public void AttachEventsToAllDocuments()
        {
            ModelDoc2 modDoc = (ModelDoc2)iSwApp.GetFirstDocument();
            while (modDoc != null)
            {
                if (!openDocs.Contains(modDoc))
                    AttachModelDocEventHandler(modDoc);
                modDoc = (ModelDoc2)modDoc.GetNext();
            }
        }

        public bool AttachModelDocEventHandler(ModelDoc2 modDoc)
        {
            if (modDoc == null) return false;
            if (openDocs.Contains(modDoc)) return true;

            DocumentEventHandler docHandler = null;
            switch (modDoc.GetType())
            {
                case (int)swDocumentTypes_e.swDocPART:     docHandler = new PartEventHandler(modDoc, this);     break;
                case (int)swDocumentTypes_e.swDocASSEMBLY: docHandler = new AssemblyEventHandler(modDoc, this); break;
                case (int)swDocumentTypes_e.swDocDRAWING:  docHandler = new DrawingEventHandler(modDoc, this);  break;
                default: return false;
            }
            docHandler.AttachEventHandlers();
            openDocs.Add(modDoc, docHandler);
            return true;
        }

        public bool DetachModelEventHandler(ModelDoc2 modDoc)
        {
            var docHandler = (DocumentEventHandler)openDocs[modDoc];
            openDocs.Remove(modDoc);
            modDoc = null;
            docHandler = null;
            return true;
        }

        public bool DetachEventHandlers()
        {
            DetachSwEvents();
            var keys = new object[openDocs.Count];
            openDocs.Keys.CopyTo(keys, 0);
            foreach (ModelDoc2 key in keys)
            {
                var docHandler = (DocumentEventHandler)openDocs[key];
                docHandler.DetachEventHandlers();
                docHandler = null;
            }
            return true;
        }

        #endregion

        #region Event Handlers

        public int OnDocChange() { return 0; }
        public int OnDocLoad(string docTitle, string docPath) { return 0; }
        int FileOpenPostNotify(string FileName) { AttachEventsToAllDocuments(); return 0; }
        public int OnFileNew(object newDoc, int docType, string templateName) { AttachEventsToAllDocuments(); return 0; }
        public int OnModelChange() { return 0; }

        #endregion
    }
}
