# Diagram and stage pages (draft v0)

The reviewable views of a graph, generated from its manifest ([manifest.md](manifest.md)) and its examples folder
([examples.md](examples.md)) only, never from an implementation's code, so one tool serves every language. They come
in two zoom levels:

- **`GRAPH.md`**, the overview: the whole graph as one picture, with a table linking each stage to its page.
- **`graph/<stage>.md`**, one page per stage: what it decides, its box of the picture, *how* it decides (pipeline
  text and judgments) and *what* it decides on each example (a table per case).

`seamlineage graph` writes them; `seamlineage check` fails when a committed copy is stale or a link in it does not
resolve. Both are deterministic: the same manifest and examples give the same bytes, with `\n` line endings. A check
compares text with line endings normalised.

## Files

```
GRAPH.md                 where --out says (default: beside the manifest)
graph/
  <stage>.md             one per stage in the manifest, named by the stage's name
```

`graph/` is always beside `GRAPH.md`. It belongs to the tool: `seamlineage graph` deletes any `.md` file in it that
is not the page of a current stage (e.g. after a rename), and `seamlineage check` reports one. Other files are left
alone.

Every link is relative to the page it is on: to a stage's `code` (relative to the manifest's directory, see
[manifest.md](manifest.md#stage)), to `fixtures/<stage>/` and `fixtures/<stage>/<case>/`, and between `GRAPH.md` and
the stage pages.

## Mermaid rules

Both pages draw with the same rules, as a Mermaid `flowchart LR` that GitHub renders in files and pull request diffs.

- Each **edge type** (a stage's input or output type) is a box: its name in bold, then one `field: type` line per
  field of a record type. `GRAPH.md` draws the graph's input then each new stage output, in stage order; a stage
  page draws only that stage's input and output.
- A **plain stage** is an arrow from its input box to its output box, labelled with its name in bold and its
  description.
- A **composed stage** is a box (`subgraph`) of its steps, top to bottom (`direction TB`), each step its operator in
  bold and one `parameter: value` line per parameter, with an arrow between consecutive steps. The labelled arrow
  leads from the input box into this box, and a plain arrow leads on to the output box.
- Labels are Mermaid *markdown strings* (`"` + backtick + text + backtick + `"`): real line breaks, no HTML and no
  entities, so the raw text reads cleanly in a diff. In label text a `"` or a backtick becomes `'`; nothing else is
  escaped.
- Mermaid ids are the name with every character other than an ASCII letter or digit replaced by `_`; a composed
  stage's steps are `<id>_1`, `<id>_2`, ...

In Markdown tables a `|` in text is escaped as `\|`.

## GRAPH.md

Sections in order (a section with nothing to show is left out):

1. **Title and header**: `# <graph> graph`, which file and command generated it ("Do not edit"), and a note on how
   to read the drawing-only lines of the raw Mermaid text.
2. **Diagram**: the whole graph.
3. **Stages**: one row per stage: its name linked to its page, its description, a link to its code, and a link to
   its examples with their count (`none` without any).
4. **Judgments**: every composed stage's judgments with their meanings.
5. **Operators**: every operator the graph uses with its description.
6. **Other types**: the types not drawn as boxes, one line each (`field: type, ...` or enum values separated by
   `|`).

## Stage pages

`graph/<stage>.md`, sections in order:

1. **Title and header**: `# <stage>`, which file and command generated it, and a link back to `GRAPH.md`; then the
   stage's description; then a small table of its input and output types, a link to its code, and a link to its
   examples with their count.
2. **Diagram**: the stage alone, by the Mermaid rules above. A plain stage is said to be one block of code.
3. **Pipeline** (composed stages only): the pipeline text, in a `text` code block.
4. **Judgments** (composed stages only): each judgment the stage uses, with its meaning.
5. **Examples**: one sentence saying how the tables read (the declared example view, or that the stage declares
   none and the generic view is used), then for each case, in case-name order (ordinal), a `###` heading with the
   case's name linked to its folder, and its table(s) ([examples.md](examples.md#example-view)). `None yet.` when
   the stage has no examples.

### Pipeline text

A composed stage's steps in plain words, one line per step, rendered from the manifest alone:

```text
accepted picks
| group by row
| order each group by picked-at, then id
| start a new session when: more than 5 min since the previous picked-at, or variety-changes
| keep groups of 3 or more whole as basket; split the rest into groups of one, each loose
```

- The first line is the stage's `items`.
- Each further line is `| ` and the step's operator's `phrase` template filled from the step's parameters, by the
  rendering rules in [manifest.md](manifest.md#operators); an operator without a phrase reads as its name and its
  parameters.
- Names in the text that are judgments keep their names; the Judgments section beneath gives their meanings.

A stage's code may do more after its last step (for example, shape or sort the output). That is not an operator, so
the pipeline text cannot show it; the stage's examples do.

## Checks

`seamlineage check` (and the C# test helper `GeneratedFiles.DiagramIsCurrent`) reports:

- `GRAPH.md` or a stage page that is missing or stale;
- a `.md` file in `graph/` that is not the page of a current stage;
- a relative link on any of the pages that resolves to no file or folder (web links and `#anchors` are not checked);
- each example case a stage's example view does not fit ([examples.md](examples.md#checks));
- with `--fixtures`: a stage with no examples, and an examples folder that names no stage.
