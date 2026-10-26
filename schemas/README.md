# Schema

- `app.schema.json` — one application entry under `apps/<id>/app.json`.
- `theme.schema.json` — design tokens (target contract for agent restyling).

## Conventions

- Manifest paths are **repository-relative** (`assets/...`, `scripts/...`) so an agent can copy a directory without rewriting.
- `icon.source=placeholder` means the tile is an original placeholder and may be redistributed with this template.
- `icon.source=official` means the user supplied a local official asset; it must stay out of the public repo.
- `kind` selects a runtime adapter; `generic` uses `launch.command` directly.

## Validate

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validate-apps.ps1
```

The validator checks required fields, icon dimensions, and placeholder disclosure. Runtime support: manifests in `apps/` are loaded into the launcher; `themes/*.json` is still a target contract and is not yet consumed by Renderer.

The validator does not run JSON-Schema; it intentionally has no NuGet dependency.
