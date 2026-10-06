# Conformance suite

Examples every Seamlineage implementation must pass, so a stage means the same thing in C#, Go or TypeScript. The
rules are in [operators.md](../operators.md); where prose and a case disagree, the case is the bug report.

## Layout

```
conformance/
  operators/
    <operator>/          group-by, order-by, session, split-small-groups
      <case>/
        input.json       the parameters and the input
        expected.json    the exact expected output
```

A case's name says the behaviour it pins down.

## input.json

Judgments are code in a real stage. Here they are **plain data**: each item is a JSON object, and a judgment's result
for an item is the field of that item the parameters name. No code is needed to run a case.

```json
{
  "parameters": { "at": "at", "maxGapSeconds": 120, "breakWhen": [ "kindChanges" ] },
  "groups": [
    [
      { "id": "a", "at": "2026-01-01T12:00:00", "kindChanges": false },
      { "id": "b", "at": "2026-01-01T12:01:00", "kindChanges": true }
    ]
  ]
}
```

| Operator | Input | Parameters |
|---|---|---|
| `group-by` | `items`: a list of items | `key`: the field holding the key |
| `order-by` | `groups`: a list of non-empty lists of items | `by`: the fields to sort by, in turn |
| `session` | `groups` | `at`: the field holding the time; `maxGapSeconds`: a number; `breakWhen` (optional): fields holding each break judgment's answer for that item |
| `split-small-groups` | `groups` | `minimum`: a whole number; `keptAs`, `splitAs`: the labels, as text |

Field values:

- A missing field reads as `null`.
- Keys and sort values are text, numbers or booleans. Times are ISO 8601 local date-times with no offset
  (`2026-01-01T12:02:00.001`); `order-by` compares them as text, which in one format is chronological.
- A `breakWhen` field holds what that judgment answers when asked about the item as the next item of the session so
  far; `null` or missing means `false`.

## expected.json

`{ "groups": [ ... ] }`: for `group-by`, `order-by` and `session` a list of lists of items; for `split-small-groups` a
list of `{ "label": ..., "items": [ ... ] }`. Items are written back exactly as they came in.

## Running it

A port writes a small adapter, like the C# one in
[`tests/Seamlineage.Tests/ConformanceTests.cs`](../../tests/Seamlineage.Tests/ConformanceTests.cs):

1. Find every `operators/<operator>/<case>/` folder.
2. Read `input.json`. Turn each named field into a judgment of the port's own operator (for `session`, turn
   `maxGapSeconds` into the port's duration type; the C# adapter rebuilds groups by grouping on each item's group
   index).
3. Run the operator and serialise its output as `{ "groups": ... }`.
4. **Pass** means the output is **JSON-equal** to `expected.json`: the same structure and values, with object key
   order, whitespace and escaping ignored. Numbers compare by value. Anything else is a failure, and every case must
   pass.

A port may skip a case only by saying so in its own README, with the reason (for example, the C# `order-by` takes at
most two keys; no case uses more).
