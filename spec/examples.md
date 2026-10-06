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

### Escaping

Every file in these formats (examples, recordings, the manifest) is read in diffs, so writers escape as little as
JSON allows, and readers accept anything JSON allows.

- **Writers must escape** `"`, `\` and the control characters U+0000 to U+001F, preferring the short forms (`\"`,
  `\`, `\n`, `\r`, `\t`, `\b`, `\f`).
- **Writers must not escape** other printable text: `'`, `&`, `<`, `>`, `+`, `` ` `` and printable non-ASCII text such
  as `é` or `→` are written as themselves. (Escaping these is only useful when embedding JSON in HTML, which these
  files never are.)
- **Writers may escape** characters that are invisible or ambiguous in a diff (for example U+007F, U+00A0, U+2028,
  U+2029, U+FEFF) and characters outside the Basic Multilingual Plane (as a `\uXXXX\uXXXX` surrogate pair).
- **Readers must accept** any valid JSON escaping, including `\u0027` for `'`, from older files or other writers.

Escaping never changes meaning: two documents that differ only in escaping are JSON-equal.

## Example view

Examples are the specification, but JSON is slow to review. Each stage's page (see [diagram.md](diagram.md)) shows
every case as a compact table of input → outcome. A stage says how with an optional, declarative **example view** in
the manifest (`exampleView` on the stage); the implementation declares it beside the stage's code.

```json
"exampleView": {
  "rows": "accepted",
  "columns": [
    { "label": "pick", "path": "id" },
    { "label": "picked at", "path": "pickedAt" }
  ],
  "outcome": {
    "from": "groups",
    "members": "picks",
    "match": "id",
    "columns": [ { "label": "basket", "path": "key" }, { "label": "kind", "path": "kind" } ]
  }
}
```

| Field | Meaning |
|---|---|
| `rows` | Path into `input.json` to a list. Each item of it is one row of the table, in order. |
| `columns` | The row's columns, in order: a heading (`label`) and a path into the item (`path`). |
| `outcome` | Optional. How each row's outcome is read from `expected.json`. |
| `outcome.from` | Path into `expected.json` to the list of output records. |
| `outcome.members` | Optional. Path, within one output record, to the list of items it holds. Absent: the record itself is its only member. |
| `outcome.match` | Path applied to a row and to each member. A row's outcome is the first output record with a member whose value there is JSON-equal to the row's. |
| `outcome.columns` | The outcome's columns, read from that output record. Headed `→ label`. A row no record holds shows `–` in each. |

The example above reads: one row per accepted pick, showing its id and time, then the key and kind of the group (from
`groups`) whose `picks` include a pick with the same `id`.

### Paths

A path is field names separated by dots, e.g. `address.city`. A segment of digits picks an item of a list by position
from 0 (`tags.0`). The empty path `""` is the whole document. There are no wildcards, filters or expressions; a field
name that contains a dot cannot be reached. A path that names no field or position resolves to nothing (an empty
cell); a field that is present and `null` resolves to `null`.

### Cells

A value is written in a cell as: text as written; numbers and booleans as JSON; `null` as `null`; a list as its
count (`3 items`, `1 item`, `0 items`); an object as `{…}`. A `|` is escaped and line breaks become spaces.

### Without a view

A stage that declares no view still gets tables, and its page says so. For `input.json` and then `expected.json`:
when the document is an object, each top-level field is shown in turn; otherwise the whole document is shown once.
A list of objects is a table whose columns are the fields of its items, in order of first appearance; an empty list
is `none`; a list of plain values and any other value are written inline, as cells are.

### Checks

`seamlineage check` reports each case a view does not fit: a `rows` path that is not a list in `input.json`, or an
`outcome.from` path that is not a list in `expected.json`.
