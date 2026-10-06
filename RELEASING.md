# Releasing

Releases are made by pushing a version tag. The [release workflow](.github/workflows/release.yml) does the rest.

## What a release publishes

For a tag `v<major>.<minor>.<patch>` (optionally `-<prerelease>`, e.g. `v0.2.0-preview.1`), at that commit:

1. Builds and tests the solution; a failure stops the release.
2. Packs every package with the tag's version (`v0.1.0` gives `0.1.0`): `Seamlineage.Contracts`,
   `Seamlineage.Operators`, `Seamlineage.Docs`, `Seamlineage.Testing`, `Seamlineage.Hosting.InProc` and the
   `Seamlineage.Cli` .NET tool.
3. Builds the `seamlineage` CLI as one self-contained file for `win-x64`, `linux-x64` and `osx-arm64`, and checks
   the sample with the Linux one.
4. Pushes the packages to **GitHub Packages**
   (`https://nuget.pkg.github.com/Dan-Price-Photography/index.json`) with the workflow's own `GITHUB_TOKEN`. No
   repository secret is needed. Each package links to this repository.
5. Creates a GitHub Release for the tag, with generated notes and the CLI files and `.nupkg` files attached. A
   version with a `-` suffix is marked as a pre-release.

Every pull request already builds the packages (`ci` workflow), so a release only fails on something new.

## How to release

On an up-to-date `main` whose CI is green:

```bash
git tag v0.1.0
git push origin v0.1.0
```

Then watch the `release` run under Actions. A tag cannot be released twice with different contents: to fix a bad
release, publish a new version. (Pushing packages uses `--skip-duplicate`, so re-running a failed run is safe.)

## Using the packages

GitHub Packages requires authentication **even for public packages**. Create a GitHub personal access token
(classic) with the `read:packages` scope, then add the source once:

```bash
dotnet nuget add source https://nuget.pkg.github.com/Dan-Price-Photography/index.json \
  --name seamlineage \
  --username <your-github-username> \
  --password <PAT> \
  --store-password-in-clear-text
```

(`--store-password-in-clear-text` is needed on Linux and macOS, where the encrypted store is not available.)

Or, to keep the token out of files, a `nuget.config` beside your solution that reads it from environment variables:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="seamlineage" value="https://nuget.pkg.github.com/Dan-Price-Photography/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <seamlineage>
      <add key="Username" value="%GITHUB_USER%" />
      <add key="ClearTextPassword" value="%GITHUB_TOKEN%" />
    </seamlineage>
  </packageSourceCredentials>
</configuration>
```

Then `dotnet add package Seamlineage.Contracts --version 0.1.0`, or for the CLI:
`dotnet tool install --global Seamlineage.Cli --version 0.1.0` (the command is `seamlineage`).

In GitHub Actions in the same organisation, the workflow's `GITHUB_TOKEN` can read the packages: give the job
`permissions: packages: read` and set `GITHUB_USER: ${{ github.actor }}` and `GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}`
for the `nuget.config` above. Repositories outside the organisation need a token with `read:packages`.

Teams without .NET can download the single-file CLI from the GitHub Release instead; no authentication is needed.
The macOS file is not signed, so macOS may ask for confirmation before running it.

## Moving to nuget.org later

Publishing to nuget.org is a one-line change to the push step in
[`release.yml`](.github/workflows/release.yml): set `--source https://api.nuget.org/v3/index.json` and
`--api-key ${{ secrets.NUGET_API_KEY }}`, after creating a nuget.org API key scoped to `Seamlineage.*` and adding it as
the repository secret `NUGET_API_KEY`. Consumers then need no extra source or token.
