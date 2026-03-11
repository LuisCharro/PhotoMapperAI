# Name Mapping Pipeline

## Goal

Map each CSV player row to at most one photo candidate and write the resolved `External_Player_ID` back to the CSV.

Core rules:

- one player maps to one photo
- one photo maps to one player
- deterministic evidence is preferred first
- AI is reserved for unresolved or ambiguous cases

## Inputs

Required:

- player CSV
- photo directory

Optional:

- filename pattern
- photo manifest JSON
- AI model/provider settings

## Current Pipeline

The logic in [`../../src/PhotoMapperAI/Commands/MapCommand.cs`](../../src/PhotoMapperAI/Commands/MapCommand.cs) currently runs in this order:

1. Load the CSV and enumerate photo candidates.
2. Build candidate metadata from manifest, filename pattern, auto-detection, or raw filename fallback.
3. Apply direct ID matches for rows that already contain `External_Player_ID`.
4. Run deterministic global matching for unresolved rows unless `--aiOnly` is used.
5. Optionally run AI pass 1 for remaining unresolved rows when `--useAI` is enabled.
6. Optionally run AI pass 2 when `--aiSecondPass` is enabled.
7. Persist unmatched rows as no-match results and write the updated CSV.

## Deterministic Matching

The deterministic stage uses normalized name signatures rather than hardcoded nickname dictionaries.

Signals include:

- normalized full-string similarity
- token overlap
- Jaccard-style token similarity
- soft token comparisons for close variants

The algorithm is global rather than purely greedy:

- candidates are ranked per unresolved player
- only proposals with enough score and enough margin are considered safe
- accepted matches remove both the player and the photo from later rounds

## AI Matching

AI is optional.

Relevant CLI switches:

- `--useAI`
- `--aiSecondPass`
- `--aiOnly`
- `--aiTrace`
- `--nameModel`
- provider API key overrides for OpenAI, Anthropic, Z.AI, and MiniMax

AI is only called for rows still unresolved after deterministic matching unless `--aiOnly` forces broader AI use.

Pass behavior:

- pass 1 uses a tighter shortlist
- pass 2 expands recall for harder leftovers

## Threshold Behavior

The CLI enforces a minimum confidence threshold of `0.8`.

Implications:

- UI values below `0.8` are raised during command execution
- lower thresholds can increase recall in concept, but the current CLI floor prevents unsafe values below `0.8`

## Model Identifiers

The mapping pipeline supports both plain and provider-aware identifiers, for example:

- `qwen2.5:7b`
- `ollama:qwen2.5:7b`
- `openai:gpt-5-mini`
- `anthropic:claude-3-5-sonnet`
- `zai:glm-4.5`
- `minimax:MiniMax-M2.5`
- `gemini-3-flash-preview:cloud`

Configured paid model lists can also be supplied through UI configuration.

## Practical Guidance

- Default local model: `qwen2.5:7b`
- Recommended mode for quality/cost balance: deterministic first plus AI fallback
- Use `--aiOnly` only for targeted experiments or when deterministic evidence is known to be weak
- Use `--aiTrace` when you need to inspect per-player accept/reject reasoning during tuning

## Related Docs

- [`FACE_DETECTION_GUIDE.md`](FACE_DETECTION_GUIDE.md)
- [`../testing/ANONYMIZED_VALIDATION.md`](../testing/ANONYMIZED_VALIDATION.md)
- [`../../README.md`](../../README.md)
