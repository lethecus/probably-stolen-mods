# Localizing AutoSorter Plus

Copy `en.csv`, rename the copy to your language's code (for example `de.csv`, `fr.csv`,
`zh.csv`, `ja.csv`, `pt-BR.csv`) and translate the text after the comma.

Rules:

- Keep the key (the part before the comma) unchanged.
- `en.csv` is the source language and must stay complete; other files may be partial —
  missing keys fall back to English.
- Lines starting with `#` are comments.
- `{0}`, `{1}`, … are placeholders; keep them, but reorder them freely for your grammar.
- Use `\n` for a line break and wrap the value in double quotes if it contains a comma.
- Rule syntax (`tag:`, `id:`, `name:`, `mod:`, `AND`, `OR`, `NOT`) is intentionally
  **not** translated — saved rules must work the same for every language.

The file name must match the game's locale code. Restart the game after adding or editing
a file; the loader reads the folder once at startup.

Included languages: `en` (source), `de`, `es`, `fr`, `it`, `ja`, `ko`, `pl`, `ptBR`, `ru`, `zh`.

Translation status (optional, for translators):

```
asp.hotkey.*            hotkey labels in the mod config
asp.log.*               messages written to the log
asp.panel.*             the per-container rule editor
asp.overview.*          the container overview window
asp.slot.*              machine slot names
asp.error.*             rule parse errors
```
