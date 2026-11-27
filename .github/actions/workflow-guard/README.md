# workflow-guard

A JavaScript action that reads a repository's `.github/workflows/*.yml` and fails when a workflow would let code on a branch reach a
secret, a write token or the self-hosted fleet where it must not. Every repository's caller runs it on pull requests and on pushes to
`main`, and the ruleset makes it a required check, so the guard is what lets agents edit workflows (goal "Protect main, move every secret
behind main-only environments, then let agents edit workflows"; `docs/security/2026-11-github-actions-audit.md`).

It runs `node24` from the committed bundle `dist/index.js` (the YAML parser included), so it needs no install on the fleet or on
`ubuntu-latest`. It writes one annotation per violation and a job summary listing `file:line`, job, rule and detail.

## Rules

The guard assumes the org default `GITHUB_TOKEN` is read-only (it is, since harden-github-settings step 1): a job that declares no
`permissions` is treated as read. A job that calls a reusable workflow in the same repository (`uses: ./.github/workflows/…`) is not
checked by rules 1 and 2 itself; the called workflow's jobs are, where they are defined.

### 1. `secret-without-environment`: secrets and Azure sign-in only in environment jobs

Fails a job with no `environment:` that reads any `secrets.*` other than `GITHUB_TOKEN` (dotted, `secrets['…']`, a bare `if:`, the
workflow-level `env:`, or the whole context as `toJSON(secrets)`), runs `azure/login`, or calls a remote reusable workflow with
`secrets: inherit`.

*Why.* A repository secret is given to any run that asks for it, on any branch. Once the secrets live in main-only environments a job
without an environment receives nothing, so a reference there is either broken or a secret that was never moved. `azure/login` is
flagged even when it reads its ids from `vars`, because an OIDC sign-in without an environment is trusted by subject `pull_request` or
`ref:…`, not by the environment's branch policy. A job that must keep one (the real-Cosmos lanes' test identity, a read-only
pull-request gate key) gets an allow-list entry that says why.

### 2. `write-permission`: write scopes only where allowed

Fails every scope a job's token gets `write` on (its own `permissions`, else the workflow's; `write-all` is one violation) unless it is
built in:

| Scope | Allowed on |
|---|---|
| `id-token: write` | a job with an `environment:` |
| `checks: write` | a job named `verdict` (the runway-ship verdict) |
| `pull-requests: write` | a job named `protected-paths` |

or the repository's allow-list names it.

*Why.* A workflow-level `id-token: write` hands an OIDC token to every job, including the ones on no environment; `contents: write` or
`packages: write` from a branch can push or publish. The workflow's scopes count for every job that does not declare its own, so move a
write down to the one job that needs it.

### 3. `untrusted-checkout`: no pull request code in privileged runs

Fails a `pull_request_target` or `workflow_run` workflow with an `actions/checkout` whose `ref` or `repository` names the pull request's
or the triggering run's code (`github.event.pull_request.head.*`, `github.head_ref`, `workflow_run.head_sha`, `head_branch`,
`head_repository`, `pull_requests`, `refs/pull/…`), or a `gh pr checkout` step.

*Why.* Those triggers run the base branch's workflow with the base repository's secrets and a write-capable token; checking out and
building the head turns that into running the pull request's code with them. A `workflow_run` that checks out the run's `head_sha` only
when the run was a push to `main` in this repository (lib-common-net's `publish.yml`) is safe and is allowed by name, with that reason.

### 4. `environment-runner`: environment jobs on the Linux fleet

Fails a job with an `environment:` whose `runs-on` is not exactly `${{ fromJSON(vars.LINUX_RUNNER) }}` (whitespace and case aside).

*Why.* Environment jobs are the ones holding production credentials, and they belong in one known place: a Windows fleet job runs as the
owner's account, and a hosted job is a second place to secure. A justified exception is an allow-list entry per job with its reason.
Not applied to public repositories, where rule 5 keeps every job off the fleet.

### 5. `public-self-hosted`: no self-hosted runner in a public repository

In a public repository, fails any job whose `runs-on` names `vars.LINUX_RUNNER`, `vars.WINDOWS_RUNNER`, a `self-hosted` label or a
runner `group`.

*Why.* The org runner group Default has `allows_public_repositories=false`, so such a job would wait forever; and fork pull requests
must never reach the fleet, where a Linux job holds the host Docker socket. **Public repositories run everything on GitHub-hosted
runners, the guard itself included (`ubuntu-latest`).** Visibility comes from the input `visibility`, which defaults to
`github.event.repository.visibility`; when it is empty the run fails rather than guess.

### 6. `unpinned-action`: third-party actions pinned to a commit

Fails a step's or a job's `uses:` that is not pinned to a full 40-character commit SHA, and a `docker://` image without an
`@sha256:` digest. Local actions (`./…`) and the org's own (`Dan-Price-Photography/…`) are exempt; GitHub's `actions/*` are not.

*Why.* A tag is mutable: whoever controls the action's repository can move `v4` to new code that then runs with this job's secrets.
Keep the version as a comment: `uses: actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803 # v6.1.0`.

## The allow-list

`.github/workflow-guard.json` in the checked repository (input `config`). Missing means no exemptions beyond the built-in ones in rule 2.

```json
{
  "$comment": "workflow-guard exemptions; each says why",
  "allow": [
    {
      "rule": "secret-without-environment",
      "workflow": "build-and-test.yml",
      "job": "build-and-test",
      "match": "CONTRACT_READER_PRIVATE_KEY",
      "reason": "The pull request gate reads household-platform's contracts; its App is read-only on that one repository."
    },
    {
      "rule": "write-permission",
      "workflow": "publish.yml",
      "job": "publish",
      "match": "packages",
      "reason": "Pushes NuGet packages, only for a push run of main or a dispatch."
    }
  ]
}
```

- `rule`: one of the six names above. `workflow`: the file's name. `job`: the job id, or `*` for every job in that workflow.
  `reason`: required, non-empty; it is shown in the job summary beside what it allows.
- `match` (optional) narrows the entry to one detail, compared case-insensitively: the secret name (or `azure/login`, `inherit`, `*`)
  for rule 1, the scope for rule 2, the `ref`/`repository` value (or `gh pr checkout`) for rule 3, the `runs-on` as written for rules 4
  and 5, the whole `uses:` value for rule 6. Without it the entry allows every detail of that rule in that job.
- Any other key, an unknown rule or a missing reason fails the run. An entry that allowed nothing is a warning and is listed in the
  summary, so stale exemptions are seen.

## The caller workflow

Add one of these as `.github/workflows/workflow-guard.yml`. No `paths` filter, so the check always reports and can be required.

**Private repositories** use the action from household-platform (household-platform's Actions settings already let the org's private
repositories use its actions, as they do runway-ship):

```yaml
name: workflow-guard

on:
  pull_request:
  push:
    branches: [main]

permissions:
  contents: read

jobs:
  workflow-guard:
    runs-on: ${{ fromJSON(vars.LINUX_RUNNER) }}
    steps:
      - uses: actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803 # v6.1.0
      - uses: Dan-Price-Photography/household-platform/.github/actions/workflow-guard@main
```

**Public repositories** cannot read the private household-platform, so they run a vendored copy (below), on `ubuntu-latest`:

```yaml
name: workflow-guard

on:
  pull_request:
  push:
    branches: [main]

permissions:
  contents: read

jobs:
  workflow-guard:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803 # v6.1.0

      - name: The vendored guard comes from household-platform main
        env:
          GH_TOKEN: ${{ github.token }}
        run: |
          set -euo pipefail
          sha="$(tr -d '[:space:]' < .github/actions/workflow-guard/SOURCE_SHA)"
          if ! [[ "$sha" =~ ^[0-9a-f]{40}$ ]]; then
            echo "::error::.github/actions/workflow-guard/SOURCE_SHA must hold one full commit SHA"; exit 1
          fi
          if ! gh api repos/Dan-Price-Photography/household-platform --silent 2>/dev/null; then
            echo "household-platform cannot be read from this repository; skipping the freshness check"; exit 0
          fi
          status="$(gh api "repos/Dan-Price-Photography/household-platform/compare/main...$sha" --jq .status)" || {
            echo "::error::SOURCE_SHA $sha is not a household-platform commit"; exit 1; }
          case "$status" in
            identical|behind) echo "SOURCE_SHA $sha is on household-platform main" ;;
            *) echo "::error::SOURCE_SHA $sha is not an ancestor of household-platform main ($status)"; exit 1 ;;
          esac

      - uses: ./.github/actions/workflow-guard
```

`compare/main...<sha>` reads `behind` (or `identical`) exactly when `<sha>` is an ancestor of `main`. A public repository's
`GITHUB_TOKEN` cannot read the private household-platform, so in practice the check runs where a token that can is supplied and
otherwise skips silently; the copy step below is where its freshness is really kept.

The `visibility` input defaults to the event's repository visibility; set it explicitly only when running on an event without one.

## Making it a required check

In the org ruleset that protects `main` (slice "Protect main in every repo with an org ruleset"), under "Require status checks to
pass", add the check `workflow-guard` (the caller's job name) with the source GitHub Actions. Because the caller has no paths filter, it
reports on every pull request. A repository's own ruleset can require it the same way until the org ruleset covers it.

## Vendored copy (public repos)

A public repository keeps a copy in its own `.github/actions/workflow-guard/`, with its source commit in
`.github/actions/workflow-guard/SOURCE_SHA`: one line, the full household-platform SHA it was copied from. To copy it again:

```bash
sha="$(git -C <household-platform clone> rev-parse origin/main)"
rm -rf <public repo clone>/.github/actions/workflow-guard
git -C <household-platform clone> archive "$sha" .github/actions/workflow-guard | tar -x -C <public repo clone>
echo "$sha" > <public repo clone>/.github/actions/workflow-guard/SOURCE_SHA
```

Commit the copy and `SOURCE_SHA` together. The caller's freshness step above fails when `SOURCE_SHA` is not an ancestor of
household-platform `main` (a copy from a branch that never merged) and skips silently when household-platform cannot be read.

## Running it locally

```bash
node .github/actions/workflow-guard/dist/index.js --visibility private \
  [--path .github/workflows] [--config .github/workflow-guard.json]
```

from the root of the repository to check. It prints `FAIL file:line:job: rule: why` per violation, `ALLOW` per exempted one and
`STALE` per allow entry that matched nothing, and exits 1 when anything fails.

## Developing

Sources are in `src/` (`rules.js` holds the six rules as pure functions; `guard.js` applies them and the allow-list; `main.js` reads
inputs and files). Fixtures are in `test/fixtures/<rule>/`, a good and a bad workflow per rule.

```bash
cd .github/actions/workflow-guard
npm ci
npm test         # the rules, the allow-list, the built action, and that dist/ matches a fresh build
npm run build    # after any change under src/; commit dist/index.js with it
```

`dist/index.js` is generated; never edit it by hand. Its dependencies are pinned exactly in `package.json` and `package-lock.json`.
