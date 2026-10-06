# Contributing

Thanks for helping. Seamlineage is early, so open an issue before a large change.

- **Tests first.** A behaviour change starts with a failing test or example (`fixtures/<stage>/<case>/`), then the
  code. `dotnet test Seamlineage.slnx` must pass with no warnings (warnings are errors).
- **Generated files must be current.** Never hand-edit `graph.manifest.json`, `GRAPH.md` or the stage pages in
  `graph/`. After changing the sample, regenerate them (see [Getting started](README.md#getting-started)); CI fails
  if they are stale or missing and uploads fresh copies as the `generated` artifact.
- **Synthetic data only.** Examples and tests use made-up data. Never commit real files, recordings or names from
  real systems.
- **No Python in the reference tooling.** The C# implementation and the CLI use .NET only; scripts are PowerShell or
  shell. The libraries reference nothing outside .NET's base class library (a test enforces it).
- **Licence.** Contributions are accepted under the [Apache License 2.0](LICENSE) (section 5); no CLA or DCO sign-off
  is needed.
- **Releases** are made by the owner by pushing a version tag; see [RELEASING.md](RELEASING.md).
