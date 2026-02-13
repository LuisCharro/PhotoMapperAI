# Parity PR Playbook

This file defines a practical merge path for the active parity branches.

## Recommended PR order

1. `feature/generate-quality-fix`
2. `feature/validation-config-parity`
3. `feature/cli-size-profiles`
4. `feature/ui-size-profile-integration`

## Live PRs (opened)

1. #2 `feature/generate-quality-fix` → `main`
   - https://github.com/LuisCharro/PhotoMapperAI/pull/2
2. #3 `feature/validation-config-parity` → `main`
   - https://github.com/LuisCharro/PhotoMapperAI/pull/3
3. #4 `feature/cli-size-profiles` → `main`
   - https://github.com/LuisCharro/PhotoMapperAI/pull/4
4. #5 `feature/ui-size-profile-integration` → `main`
   - https://github.com/LuisCharro/PhotoMapperAI/pull/5

Why this order:
- 1+2 improve output quality and validation reliability first.
- 3 introduces CLI parity contract.
- 4 builds UI behavior on top of stable CLI behavior.

## Merge automation

Use helper scripts to drive the final gate:

```bash
./scripts/ops/request_parity_reviews.sh <github-user> [more-users...]
./scripts/ops/check_parity_pr_merge_readiness.sh
./scripts/ops/merge_parity_prs.sh         # dry-run (default)
./scripts/ops/merge_parity_prs.sh --apply # perform merges
```

CI snapshot (2026-02-13 late evening): all four PRs reached green status in dry-run verification.

### Merge gate currently blocking completion

Repository rule currently enforces:
- At least **1 approving review** from a reviewer with write access.

Observed behavior:
- Direct merge is blocked by base branch policy.
- Auto-merge is disabled at repository level.
- Admin merge also blocked due to required approval rule.

Action to unblock:
1. Add one approval to each parity PR (#2, #3, #4, #5).
2. Run:
   - `./scripts/ops/merge_parity_prs.sh --apply`

---

## PR checklist template (copy into each PR)

- [ ] Scope is focused to one concern
- [ ] Build passes locally
- [ ] Relevant targeted tests pass
- [ ] Docs/examples updated
- [ ] Backward compatibility preserved
- [ ] No external/private dataset committed

---

## Branch status snapshot (2026-02-13)

### `feature/generate-quality-fix`
- Image resize quality improved (no-stretch oriented settings)
- Save format handling fixed (png/jpg honored)
- Generate flow memory cleanup improved (image disposal)
- Added image regression tests

### `feature/validation-config-parity`
- Team-specific source CSV support for external validation
- Timeout support for map/generate commands
- Continue-on-error mode and per-team status
- Coverage/file-size metrics in report
- Optional summary JSON output
- Portrait set comparison helper script

### `feature/cli-size-profiles`
- Added size profile model + loader
- Added `--sizeProfile` and `--allSizes`
- Added `--outputProfile test|prod`
- Added tests for profile loader and output profile resolver

### `feature/ui-size-profile-integration`
- Generate step UI supports size profile path, all-sizes toggle, output profile selector
- UI execution supports variant runs
- Manual size controls disabled when profile is active
- Session state updated with new generate options
- UI validation checklist added

---

## Suggested follow-up after merges

1. Run one end-to-end external validation pass with parity config.
2. Save summary JSON + markdown report under external validation output.
3. If results look good, tag a milestone release note for "legacy parity phase 1".
