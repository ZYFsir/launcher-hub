# Security policy

This is a local Windows launcher template. It does not collect telemetry and
does not call remote services except the URLs configured by the user.

## Reporting

Please open a private security advisory if available, or contact the maintainer
without posting secrets in a public issue.

## Sensitive data

- `config.local.json`, `apps/`, and `assets/placeholders/reference/` are local
  and git-ignored.
- Launcher logs may contain local URLs and launch tokens. The UI masks
  `token=` values, but logs on disk are not automatically redacted.
- Do not attach screenshots containing terminal tokens, tailnet hostnames, or
  personal paths to public issues.

## Launch commands

Manifests execute local commands chosen by the user. Treat `apps/*/app.json`
as trusted local configuration. The template does not sandbox child processes.
