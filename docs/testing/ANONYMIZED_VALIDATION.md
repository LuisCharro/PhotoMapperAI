# Anonymized CLI Validation

This repo includes a small anonymized validation pack for exercising core CLI flows without private photos or real customer/team CSVs.

## Coverage

- `extract`
  - uses a fake connection string and SQL file
  - verifies synthetic fallback output without requiring a real database
- `map`
  - uses anonymized player CSV data plus anonymized photo filenames
  - validates expected `External_Player_ID` assignments
- optional AI-required map fixture
  - confirms the map flow actually enters AI fallback when requested

## Fixture Locations

All paths below are relative to the repo root:

- `tests/Data/Anonymized/Extract/query.sql`
- `tests/Data/Anonymized/Extract/connection_string.txt`
- `tests/Data/Anonymized/Map/input_unmapped.csv`
- `tests/Data/Anonymized/Map/photos/`
- `tests/Data/Anonymized/Map/expected_external_ids.csv`
- `tests/Data/Anonymized/MapAIRequired/input_ai_required.csv`
- `tests/Data/Anonymized/MapAIRequired/photos/`
- `scripts/run_anonymized_cli_validation.py`

## Run

From the repo root:

```bash
python3 scripts/run_anonymized_cli_validation.py
```

Optional AI-required validation:

```bash
python3 scripts/run_anonymized_cli_validation.py --include-ai-map --ai-model qwen2.5:7b
```

## Expected Outcome

- extract check passes
- deterministic map check passes
- optional AI-required map check confirms that AI fallback was exercised
- script exits with status `0`
