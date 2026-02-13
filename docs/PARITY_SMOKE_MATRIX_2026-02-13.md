# Parity Smoke Matrix — 2026-02-13

Quick branch-by-branch pre-merge status snapshot.

## Build checks

| Branch | Check | Result |
|---|---|---|
| `feature/generate-quality-fix` | `dotnet build src/PhotoMapperAI/PhotoMapperAI.csproj` | ✅ Pass |
| `feature/validation-config-parity` | `dotnet build src/PhotoMapperAI/PhotoMapperAI.csproj` | ✅ Pass |
| `feature/cli-size-profiles` | `dotnet build src/PhotoMapperAI/PhotoMapperAI.csproj` | ✅ Pass |
| `feature/ui-size-profile-integration` | `dotnet build src/PhotoMapperAI.UI/PhotoMapperAI.UI.csproj` | ✅ Pass |

## Targeted tests

| Branch | Check | Result |
|---|---|---|
| `feature/generate-quality-fix` | `dotnet test ... --filter FullyQualifiedName~ImageProcessorTests` | ✅ Pass |
| `feature/cli-size-profiles` | `dotnet test ... --filter FullyQualifiedName~SizeProfileLoaderTests\|FullyQualifiedName~OutputProfileResolverTests` | ✅ Pass |
| `feature/ui-size-profile-integration` | UI project build (`dotnet build`) | ✅ Pass |

## Go / No-Go

- **Go** for PR creation and staged merge in recommended order.
- Keep final manual UI validation step via:
  - `docs/UI_GENERATE_SIZE_PROFILE_CHECKLIST.md`

## Remaining risk notes

- External real-data validation still depends on local model availability and private datasets.
- For deterministic CI behavior, continue using the validation scripts in dry-run or controlled local runs.
