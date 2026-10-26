# Contributing

Thanks for helping the launcher template.

## Before you start

- Windows 10/11, .NET Framework 4.8, PowerShell 5.1.
- Read `AGENTS.md`/`docs/agent-guide.md` and keep the locked visual decisions:
  native DWM frame, simplified inset tab, no trapezoid/distance-field/bridge.
- Do not commit `config.local.json`, `apps/`, `assets/placeholders/reference/`,
  local logs, or built `LauncherHub.exe`.

## Build and test

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\validate-apps.ps1
.\LauncherHub.exe --audit-layout
.\LauncherHub.exe --audit-anim
.\LauncherHub.exe --audit-icons
.\LauncherHub.exe --audit-switch
```

`--audit-switch` needs a real desktop session; CI runs the offscreen audits only.

## Adding an app

Use `apps.example/<id>/app.json` as a template, put local manifests in `apps/`
(git-ignored), then adapt assets and launch commands to your machine. See
`docs/agent-guide.md` and `schemas/app.schema.json`.

## Pull requests

- Keep changes small and scoped.
- Explain the user-visible behavior and which audit ran.
- Do not add new official brand assets; existing ones are documented in THIRD-PARTY-NOTICES.md and are not covered by the code license.
- By contributing, you agree your code is MIT-licensed and your documentation
  may be shared under the same terms.
