# Recordings (draft v0)

A host records the data on every edge of a run. Recordings give the *lineage* of any output, turn into examples with
one command, and are how a bug is located: find the first edge whose data is wrong.

## Layout

```
recordings/<run-id>/NN-<edge>.jsonl
```

- `NN` is the edge index: `00` is the graph's input; `NN` (1, 2, …) is the output of the NN-th stage.
- `<edge>` is `input` for edge 00, otherwise the stage name.
- Each line is one **envelope** (JSON, no line breaks inside), escaped minimally like every wire file
  ([examples.md](examples.md#escaping)); readers accept any valid JSON escaping.

## Envelope

```json
{ "correlationId": "20261005-101500-ab12cd", "idempotencyKey": "20261005-101500-ab12cd:scan-source", "orderingKey": null, "schemaVersion": 1, "payload": { ... } }
```

| Field | Meaning |
|---|---|
| `correlationId` | Identifies one run; the same on every edge of it. |
| `idempotencyKey` | Stable across retries of the same message. Delivery is assumed at-least-once everywhere, so consumers must use it to ignore duplicates. |
| `orderingKey` | Optional. Messages with the same key must be processed in order (maps to a partition key on Kafka / Event Hubs). |
| `schemaVersion` | The producing stage's `schemaVersion`. |
| `payload` | The edge's data in the wire format (see [examples.md](examples.md)). |

Stages never see envelopes; hosts create and read them. On a message bus, the envelope fields travel as message
headers (or properties) and `payload` as the body.
