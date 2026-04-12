# PhotoMapperAI Repo Agent Guide

Use this file for PhotoMapperAI-specific truth that should stay inside this
repo.

Keep here:
- product identity
- exact commands and preferred workflows
- repo-specific CLI/UI notes
- validation and image-pipeline constraints

Keep reusable heuristics in copied shared skills under `./.agent-local/skills/`.

## Product and stack

PhotoMapperAI maps external player photos to internal player records and
generates portrait crops. The repo contains a CLI for automation and batch
workflows, an Avalonia desktop UI, and supporting validation/docs assets.

Core stack:
- .NET
- C#
- Avalonia desktop UI
- OpenCV and related image-processing providers
- optional AI-assisted name matching and vision backends

## Read this repo in this order

- `README.md`
- `CLAUDE.md`
- `docs/README.md`
- `docs/guides/GUIDE.md`
- `docs/guides/NAME_MAPPING_PIPELINE.md`
- `docs/guides/FACE_DETECTION_GUIDE.md`
- `.kilocode/rules/PhotoMapperAI.md` when it exists for repo-specific workflow notes

## Common commands

- build solution: `dotnet build PhotoMapperAI.sln`
- run tests: `dotnet test tests/PhotoMapperAI.Tests/PhotoMapperAI.Tests.csproj`
- run CLI help: `dotnet run --project src/PhotoMapperAI/PhotoMapperAI.csproj -- --help`
- run desktop UI: `dotnet run --project src/PhotoMapperAI.UI/PhotoMapperAI.UI.csproj`

## Shared-skills router

If `./.agent-local/skills/` is present, start shared-skill routing with:

- `./.agent-local/skills/_shared/repo-bootstrap-check.SKILL.md`
- `./.agent-local/skills/desktop/avalonia/`
- `./.agent-local/skills/backend/computer-vision-pipeline-review.SKILL.md`

Then load only the copied shared skill that matches the task.

## Repo-local constraints

- Keep repo-specific public instructions commit-safe and avoid private machine
  paths in tracked docs.
- Treat mapping and portrait generation as separate workflows with different
  validation needs.
- Keep CLI command surface and desktop UI behavior aligned when both expose the
  same underlying feature.
- Prefer updating the evergreen guides in `docs/guides/` instead of leaving new
  behavior buried in reports or planning notes.

## Docs to keep updated

- `agent/README.md`
- `README.md`
- `docs/`
- `CLAUDE.md`
