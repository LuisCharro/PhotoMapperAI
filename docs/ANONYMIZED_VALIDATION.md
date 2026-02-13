# Anonymized CLI Validation

This project includes a small anonymized validation pack to test core CLI flows without private photos or real CSV datasets.

## What It Covers

- `extract` command:
  - Uses a fake connection string and SQL file.
  - Verifies synthetic fallback output (no real database required).
- `map` command:
  - Uses anonymized player CSV + anonymized photo filenames.
  - Runs deterministic mapping (no AI required) and validates expected `ExternalId` assignments.
  - Optional AI-required fixture validates that MAP command actually enters AI fallback (`AI evaluated > 0`).

## Files

- Extract fixtures:
  - `/Users/luis/Repos/PhotoMapperAI/tests/Data/Anonymized/Extract/query.sql`
  - `/Users/luis/Repos/PhotoMapperAI/tests/Data/Anonymized/Extract/connection_string.txt`
- Map fixtures:
  - `/Users/luis/Repos/PhotoMapperAI/tests/Data/Anonymized/Map/input_unmapped.csv`
  - `/Users/luis/Repos/PhotoMapperAI/tests/Data/Anonymized/Map/photos/`
  - `/Users/luis/Repos/PhotoMapperAI/tests/Data/Anonymized/Map/expected_external_ids.csv`
- AI-required map fixtures:
  - `/Users/luis/Repos/PhotoMapperAI/tests/Data/Anonymized/MapAIRequired/input_ai_required.csv`
  - `/Users/luis/Repos/PhotoMapperAI/tests/Data/Anonymized/MapAIRequired/photos/`
- Runner:
  - `/Users/luis/Repos/PhotoMapperAI/scripts/run_anonymized_cli_validation.py`

## Run

From repo root:

```bash
python3 scripts/run_anonymized_cli_validation.py
```

Optional AI-required validation:

```bash
python3 scripts/run_anonymized_cli_validation.py --include-ai-map --ai-model qwen2.5:7b
```

Expected outcome:
- `extract` check passes (3 synthetic rows for team 1)
- `map` check passes (expected external IDs matched)
- Optional `map-ai` check confirms AI fallback is exercised (`AI evaluated > 0`)
- Script exits with status `0`
