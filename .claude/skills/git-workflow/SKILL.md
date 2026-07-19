---
name: git-workflow
description: Use for any commit, push, or pull-request work in FinFlow. main is protected — no direct pushes for anyone, admins included; every change reaches main via a pull request with passing backend/frontend/smoke checks. Encodes branching, commit style, the pre-push privacy check, and the recovery steps when commits accidentally land on local main.
---

# Git workflow (protected `main`)

Branch protection on `main` (set via `gh api`, applies to admins too):

- Direct pushes are rejected; force pushes and branch deletion are blocked.
- Pull requests are required (0 approvals — solo maintainer), with required
  status checks `backend`, `frontend`, and `smoke`, and the branch must be
  up to date with `main` (`strict: true`).

## Normal flow

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
6. Wait for `backend`, `frontend`, and `smoke` to pass, then merge
   (`gh pr merge --merge`) and delete the branch.

## If commits accidentally landed on local `main`

A push from local `main` will be rejected by the protection. Move the work
to a branch instead of weakening the protection:

```bash
git switch -c feat/<topic>        # takes the local commits along
git branch -f main origin/main    # resets local main to the remote state
```

Then continue with the normal flow (privacy check → push → PR).
