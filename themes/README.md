# Themes

`example-light.json` / `example-dark.json` are **target design-token contracts** for agent restyling.
Current Renderer still reads `Look.cs`; wiring these files into `Look` is a separate refactor.

Rules:

- Keep the flat, crisp, no-glass/no-heavy-shadow direction.
- Do not use a single radius for every role; cards, fields, pills keep distinct roles.
- After a theme change, rerun the scale/layout matrix.
