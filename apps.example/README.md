# apps.example

These are **examples**, not the runtime catalog.
Copy one directory to a git-ignored `apps/` and edit `app.json`; the launcher loads `apps/*/app.json` when present.

- `llama` — process + HTTP health + model/ctx params.
- `comfy` — process + HTTP health + checkpoint/VRAM params.
- `dsh` — TCP health + no-stop policy.
- `generic` — add your own; `kind: generic` uses `launch.command` directly.

Icons point at `assets/placeholders/generated/` (L/C CC0, DSH CC BY-SA).
Official assets are intentionally tracked in this repo by user decision; see `THIRD-PARTY-NOTICES.md`.
