# Examples (draft v0)

Examples (also called fixtures) are the specification of each stage's behaviour, written as data.

## Layout

```
fixtures/
  <stage-name>/
    <case-name>/
      input.json      the stage's input, in the manifest's wire format
      expected.json   the stage's exact expected output
```

- Each case folder is one test. Adding a folder adds a test; no code is needed.
- `<stage-name>` must match a stage `name` in the manifest. Every stage must have at least one case.
- `<case-name>` is kebab-case and should state the behaviour it pins down, e.g. `three-second-gap-is-not-a-bracket`.

## Rules

- A test passes when the stage's output, serialised in the wire format, is **JSON-equal** to `expected.json`
  (structural equality; key order and whitespace don't matter).
- Stages are pure, so a test runs with a fixed context (a constant correlation id and clock).
- Examples may be written by hand, or **promoted** from a recording: the recorded input and output of one stage
  become `input.json` and `expected.json`. A promoted `expected.json` records what the code *did*; it is only a
  regression guard until a person agrees it is what the code *should* do.
- Behaviour changes start with a new or changed example that fails, then the code.

## Wire format

- Field names are camelCase; enum values are camelCase strings.
- `datetime` values are ISO 8601 with offset (`2026-10-01T21:42:25+00:00`); `localdatetime` values have no offset.
- Optional values that are absent are written as `null`.
