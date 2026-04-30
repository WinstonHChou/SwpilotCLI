# Code Development Guide — SwpilotCLI

This document governs ALL SolidWorks automation development and TOOLS.md maintenance.

---

## Step 0 — Tool Discovery (MANDATORY)

Before generating any SolidWorks automation code:

1. You MUST check whether an existing tool in `main_tools/` or `sup_tools/` can solve the task.
2. Review `TOOLS.md` to understand available tools.
3. If a suitable tool exists, you MUST use the tool instead of writing new code.
4. Do NOT generate new automation code if an existing tool can accomplish the task.

Skipping tool discovery is considered an invalid workflow.

---

## TOOLS.md Maintenance

`TOOLS.md` is auto-generated — do NOT edit it manually.

To regenerate after adding or modifying any tool:

```bash
# Double-click (recommended)
scripts/sync-tools.bat

# Or from terminal
powershell -ExecutionPolicy Bypass -File "./scripts/sync-tools.ps1"
```

The script scans `main_tools/` and `sup_tools/`, reads each tool's `SKILL.md` frontmatter, and rebuilds `TOOLS.md`.

**Run this every time you:**
- Add a new tool to `sup_tools/`
- Update a tool's `SKILL.md` description

---

## Step 1 — Framework Loading (MANDATORY)

If and only if no existing tool can solve the task:

- You MUST read and strictly follow `CSharpFramework.md`.
- You MUST apply its workflow in exact order.

---

## Graduating Code from SolidWorksConsole to sup_tools

`SolidWorksConsole/` is the draft area — validate quickly here, no SKILL.md structure required.
Once validated, graduate to `sup_tools/` with full documentation.

```
SolidWorksConsole/ (draft/validate) → decide → sup_tools/ (official)
```

### When to use `SolidWorksConsole/Reference/`

Not every validated draft should graduate to `sup_tools/`.
Use `SolidWorksConsole/Reference/` for code that is worth keeping, but should not become an official tool.

```
SolidWorksConsole/ (draft/validate) -> decide ->
  sup_tools/ (official reusable tool)
  SolidWorksConsole/Reference/ (kept reference / low-frequency draft)
  delete (one-off with no reuse value)
```

Move a project to `SolidWorksConsole/Reference/` when:
- The code helped validate geometry, point tracking, or a one-off workflow and may be useful as future reference
- The code is useful for occasional debugging, verification, or agent testing, but is not part of the normal user workflow
- The code works for a specific session, pose range, or example and would be misleading as a general-purpose `sup_tools` skill

Delete a project instead of archiving it when:
- It is a failed attempt with no reusable logic
- It was only created to test a trivial idea and has no future reference value
- A better draft or official tool already preserves the same method clearly enough

Do NOT graduate archived drafts to `sup_tools/` without first checking whether they are truly reusable and whether they need to be split into smaller tools.

### Should you split into multiple tools?

| Situation | Recommendation |
|-----------|---------------|
| Single purpose, unlikely to be reused | Graduate as one tool, no split |
| Part of the logic has independent value | Extract as atomic tool, let LLM orchestrate |
| Batch logic | Let LLM compose from atomic tools, don't build a batch tool |

A complete version and split versions can coexist in `sup_tools/` — no problem.
But **SKILL.md description must clearly state when to use each**:
- Complete version: `Use when you need to batch export in one shot.`
- Atomic version: `Use as building block for LLM-orchestrated workflows.`

**Description quality is the only signal LLM uses to pick the right tool.**

### After graduating to sup_tools

Run `sync-tools.bat` to update `TOOLS.md` so the new tool is discoverable.

---

## Absolute Prohibitions

Do NOT:
- Write SolidWorks API calls from memory
- Guess enum values
- Skip API search / detail review / enum verification defined in `CSharpFramework.md`
- Create ad-hoc scripts outside the C# console project template
- Generate code outside the `SolidWorksConsole/` folder
- Use Python or VBA unless the user explicitly requests it

Failure to follow this guide invalidates the result.
