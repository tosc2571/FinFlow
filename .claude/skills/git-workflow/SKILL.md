---
name: git-workflow
description: Use for any commit, push, pull-request, or GitHub issue work in FinFlow. main is protected — no direct pushes for anyone, admins included; every change reaches main via a pull request with passing backend/frontend/smoke/docker checks. Encodes branching, commit style, the pre-push privacy check, GitHub issue conventions, and the recovery steps when commits accidentally land on local main.
---

# Git workflow (protected `main`)

Branch protection on `main` (set via `gh api`, applies to admins too):

- Direct pushes are rejected; force pushes and branch deletion are blocked.
- Pull requests are required (0 approvals — solo maintainer), with required
  status checks `backend`, `frontend`, `smoke`, and `docker`, and the branch
  must be up to date with `main` (`strict: true`).

## Normal flow

**No code change without a tracking issue and a PR — no exceptions.** Every
piece of work, however small (a one-line bug fix, a doc tweak), starts with
a `gh issue create` and lands via a pull request, never a direct commit
pushed straight through on a branch with no issue behind it. This applies
even when the user reports something informally in chat ("X is broken") —
create the issue first, then branch/implement/PR against it.

1. **Never commit on `main`.** Branch from the current remote state:
   `git fetch origin && git switch -c feat/<topic> origin/main`
2. **Commits:** English, imperative, conventional-commit prefix
   (`feat:`, `fix:`, `docs:`, `chore:`). Claude-authored commits end with the
   Co-Authored-By line from the harness guidelines.
3. **Privacy check before every push** — the repo is public:
   - `git ls-files | grep -i "rules.*json"` → only `rules.example.json`
     (a real `rules.json` must never be tracked).
   - No `bin/`, `obj/`, `node_modules/`, `publish/`, `wwwroot/`, or `*.db`
     files tracked.
   - `git diff origin/main...HEAD` greps clean for personal tokens
     (real vendor/employer/city names from the private rules file).
4. `git push -u origin feat/<topic>`
5. `gh pr create` — English title and body summarizing the change; the body
   ends with the Claude Code attribution line from the harness guidelines.
   - **Use a closing keyword for the tracked issue** (`Closes #N` /
     `Fixes #N`), not just a bare `(#N)` reference — a bare reference
     doesn't auto-close the issue on merge, leaving it stale-open even
     though the work is done (happened with #30/PR #32).
6. Wait for `backend`, `frontend`, `smoke`, and `docker` to pass, then merge
   (`gh pr merge --merge`) and delete the branch.
   - **Sleep at least 60s before the first `gh pr checks`/`gh run list`
     call after pushing or creating a PR.** GitHub Actions can take a while
     to dispatch the workflow run; checking earlier reliably prints "no
     checks reported" even when everything is fine, which reads like a
     stuck/failed trigger it isn't. Don't chain shorter sleeps or retry
     loops to work around this — one 60s wait, then check.

## GitHub issues

- English, per [language-conventions](../language-conventions/SKILL.md) — even
  though the user writes to you in German.
- Match the house style visible in past issues (`gh issue list --repo
  tosc2571/FinFlow --state all`, then `gh issue view <n>`): `## Goal`, then
  `## Proposed behavior` (with sub-sections as needed), `## Design notes`,
  `## Acceptance criteria` as a checklist, ending with the Claude Code
  attribution line from the harness guidelines.
- Check `--state all` (not just open) for related or duplicate issues before
  drafting, and cross-reference them (`#4`, `#5`, ...) where relevant.
- If the body includes implementation details the user didn't explicitly ask
  for (new tables/endpoints, specific acceptance criteria, etc.), show the
  draft in chat and get a go-ahead before `gh issue create` — don't publish
  invented scope straight to a public repo. Skip this step only when the
  user's request already fully specifies the content or says to just create
  it.

## If commits accidentally landed on local `main`

A push from local `main` will be rejected by the protection. Move the work
to a branch instead of weakening the protection:

```bash
git switch -c feat/<topic>        # takes the local commits along
git branch -f main origin/main    # resets local main to the remote state
```

Then continue with the normal flow (privacy check → push → PR).
