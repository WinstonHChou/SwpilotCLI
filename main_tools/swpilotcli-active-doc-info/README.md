# ActiveDocInfo Skill

This directory contains the ActiveDocInfo tool for SwpilotCLI.

## Directory Structure

```
swpilotcli-active-doc-info/
├── SKILL.md                    ← Main skill definition
├── API_REFERENCE.md            ← Detailed output schema
├── README.md                   ← This file
└── scripts/
    └── ActiveDocInfo.csproj    ← C# project (generated)
```

## Setup

1. Ensure the C# project is built:
   ```bash
   cd scripts
   dotnet build
   cd ..
   ```

2. Test the tool:
   ```bash
   dotnet run --project ./scripts/ActiveDocInfo.csproj
   ```

## Developing

When updating the tool:
1. Edit the C# code in `scripts/`
2. Update API_REFERENCE.md if output schema changes
3. Update examples in SKILL.md if behavior changes

## Migration to Agent Skills

This tool is designed to be directly compatible with Claude Agent Skills.

To migrate to `~/.claude/skills/`:
1. Copy the entire `swpilotcli-active-doc-info/` directory
2. Paste into `~/.claude/skills/swpilotcli-active-doc-info/`
3. No changes needed — the structure is identical

The only difference: in Agent Skills, the `dotnet run` command will be executed in the Skills runtime environment rather than locally.
