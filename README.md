# Seamlineage

**Seamlineage** (*seam* + *lineage*) is an approach, and a small set of tools, for writing software so that **what the
product does is kept separate from how it's deployed**, and so a person can quickly and confidently review changes,
including changes written by an AI.

> Status: **early and experimental.** The ideas are being tested on a real application before anything here is
> called stable. Formats in [`spec/`](spec/) are drafts (v0) and will change.

## The idea, in plain words

Most code mixes two things:

- **the product**: what it does, and the decisions it makes;
- **the plumbing**: where it runs and how the pieces talk (processes, services, queues, databases, clouds).

Because they're tangled, simple review questions get hard to answer. *What does this change actually do to the
product? Where did this bug come from? Could this part run somewhere else?*

Seamlineage describes the product as **a graph of small, pure stages**. Each stage takes data in and returns data out,
plus a list of side effects it wants performed (described as data), and touches nothing else: no files, no network,
no clock. All plumbing lives in **hosts**, which feed data into stages and carry out their effects. The boundary
between the two is the **seam**.

Because every edge between stages is a named, typed shape, a host can **record** the data on every edge. That gives
the **lineage**: any output can be traced back through every stage that produced it.

From the graph, the tools generate artifacts a reviewer reads *before* the code:

- **a manifest** (`graph.manifest.json`): every stage, what it decides, the shape of every edge, and, for stages
  built from reusable building blocks, their steps and parameters;
- **a diagram** (`GRAPH.md`, Mermaid, rendered by GitHub): the product as a picture, with links from each stage to
  its code and its examples;
- **examples** (`fixtures/<stage>/<case>/input.json` → `expected.json`): each folder is a test, and any real
  recording can be promoted into one with a single command.

A pull request then shows how the *product* changed (diagram, manifest and examples), not only which lines did.

## Why now

None of the pieces are new. Pure-core/imperative-shell design, hexagonal architecture, dataflow systems (Apache
Beam, Flink, Spark), Elm/re-frame/Redux on the front end, dbt's generated lineage and docs, and model-driven
engineering all cover parts of this. Many attempts at the whole thing died because **keeping the model, contracts,
examples and docs in step with the code cost more than it saved.**

The bet behind Seamlineage is that this has changed: **that discipline is cheap when an AI writes and maintains it,
and it is exactly what makes AI-written changes reviewable by a human.** Mechanical checks (generated files must be
current, every link must resolve, every stage must have examples, product code may not reference plumbing) keep it
honest instead of relying on discipline.

## The model

A product is described with six parts:

| Part | What it is |
|---|---|
| **State** | What persists, keyed and versioned |
| **Intents** | What someone (a user, a job, an AI) can ask for |
| **Transforms** | Pure stages: input (+ state) → output (+ effects) |
| **Effects** | What should happen to the outside world, described as data and carried out by a host |
| **Projections** | Read-only views derived from state, including what a user is currently *allowed* to do |
| **Invariants** | Rules that must always hold, checked on every run |

Logic is expressed in **the most readable layer that can express it**:

1. **Flow**: the graph and generic building blocks (group, order, session, split…), shown as a diagram and as a
   pipeline;
2. **Rules**: small decision tables for "combination of conditions → outcome" decisions, machine-checked for gaps and
   overlaps;
3. **Code**: small, named judgments (arithmetic, parsing, algorithms), each with a one-line meaning.

Code is the last resort, not the default. Examples cover all three layers.

## Design goals

- **One spec, several languages.** The formats (manifest, examples, recordings) are language-neutral. Reference
  implementations are planned in **C#**, **Go** and **TypeScript**, held to the same **conformance suite** so a
  stage means the same thing in every language.
- **One shared CLI** generates the diagram, pipeline text, example tables and per-PR change diagrams from any
  implementation's manifest.
- **Deployment is a later choice.** The same stages run in-process for tests and desktop apps, or across services
  over a **Kafka-protocol** event stream (Apache Kafka, or Azure Event Hubs via its Kafka endpoint), with state in
  any store that supports keyed, versioned compare-and-swap (e.g. Postgres or Cosmos DB).
- **Front end as well as back end.** UI state, intents and view-model projections use the same model, examples and
  diagrams as back-end pipelines.
- **Adoptable in existing code.** Wrap an existing module as a few coarse stages, record real traffic, promote
  recordings to examples, and split stages only where it pays.

## Getting started

[`samples/harvest`](samples/harvest/) is a toy product that uses the whole loop: an orchard's pick log is checked,
grouped into baskets (a stage composed from generic operators) and weighed. Start with its
[GRAPH.md](samples/harvest/GRAPH.md), then its [manifest](samples/harvest/graph.manifest.json) and
[examples](samples/harvest/fixtures/).

```bash
dotnet test Seamlineage.slnx                                   # every example is a test

cd samples/harvest
dotnet run --project src/Harvest.Host -- manifest              # the product writes graph.manifest.json
dotnet run --project ../../src/Seamlineage.Cli -- graph \
  --manifest graph.manifest.json --fixtures fixtures --out GRAPH.md   # the shared CLI draws GRAPH.md
dotnet run --project ../../src/Seamlineage.Cli -- check \
  --manifest graph.manifest.json --fixtures fixtures --out GRAPH.md   # exit 1 if stale or a link is broken
```

The product's host also records runs and promotes them to examples:
`run <input.json>` writes `recordings/<run>/NN-<edge>.jsonl`, and `promote <recording> <stage> <case>` turns one
stage's recorded input and output into `fixtures/<stage>/<case>/`.

The CLI reads only the manifest and the examples folder, so it serves a graph written in any language. Install it as
a .NET tool (`dotnet pack src/Seamlineage.Cli`, then `dotnet tool install`), or build one self-contained file:
`dotnet publish src/Seamlineage.Cli -c Release -r linux-x64` (or `win-x64`, `osx-arm64`, ...).

| Package | What it holds |
|---|---|
| `Seamlineage.Contracts` | Stage, effect, envelope, state-store contract, graph builder, composed-stage steps, wire JSON, manifest emitter. BCL only. |
| `Seamlineage.Operators` | The generic building blocks: group-by, order-by, session, split-small-groups. |
| `Seamlineage.Docs` | GRAPH.md from a manifest (Mermaid), and the checks on a committed copy. BCL only. |
| `Seamlineage.Testing` | Test helpers for any test framework: example runner with a JSON diff, "every stage has examples", generated files are current, assembly-reference checks. |
| `Seamlineage.Hosting.InProc` | In-process runner, edge recorder, fixture promoter, and a host command line (`manifest`, `run`, `promote`). |
| `Seamlineage.Cli` | The `seamlineage` command: `graph` and `check`. |

## Roadmap

| Step | What | Status |
|---|---|---|
| 0 | Prove the approach on a real application (photo import: scan, group, detect exposure brackets), including an AI-authored stage reviewed from diagram and examples first | Done, in a private proving-ground repo |
| 1 | Draft spec v0: manifest, examples, recordings, diagram rules | In progress ([`spec/`](spec/)) |
| 2 | Conformance suite, starting with the building blocks (group-by, order-by, session, split-small-groups) | Next |
| 3 | C# reference implementation and the shared CLI, extracted from the proving ground | In progress: contracts, operators, test helpers, in-process host and the `seamlineage` CLI (`graph`, `check`) are done, dogfooded by [`samples/harvest`](samples/harvest/); publishing the packages is next |
| 4 | Pipeline text and example tables in generated docs | Next |
| 5 | Per-PR change diagram: only changed stages and their neighbours, highlighted | Planned |
| 6 | Go implementation passing the conformance suite | Planned |
| 7 | Kafka-protocol distributed host | Planned |
| 8 | TypeScript implementation for front ends | Planned |
| 9 | Decision tables | Planned |

## Licence

[Apache License 2.0](LICENSE). See [NOTICE](NOTICE).
