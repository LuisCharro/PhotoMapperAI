# Name Mapping Pipeline

This document explains the current name-to-photo mapping strategy at a problem-solving level, not just at code level.

## Goal

Map each player row from an input CSV to at most one photo candidate and write the resolved `ExternalId` back to the output CSV.

Core constraints:
- One player maps to one photo.
- One photo maps to one player.
- Prefer deterministic evidence first.
- Use AI only for unresolved, ambiguous cases.
- Avoid hardcoded name dictionaries that do not scale across leagues/languages.

## Data Inputs

Required:
- Unmapped player CSV (`PlayerId`, `FamilyName`, `SurName`, `FullName`, optional `ExternalId`)
- Photo directory with filenames containing person-name tokens and external ID tokens

Reference test case used during redesign:
- CSV: `/Users/luis/Repos/PhotoMapperAI_ExternalData/RealDataValidation/DataPrep/05_transformed_fully_unmapped_for_map_test/Spain.csv`
- Photos: `/Users/luis/Repos/PhotoMapperAI_ExternalData/RealDataValidation/inputs/Spain`

Optional:
- Filename pattern template
- Photo manifest JSON

## Pipeline Overview

The implementation in `/Users/luis/Repos/PhotoMapperAI/src/PhotoMapperAI/Commands/MapCommand.cs` follows this sequence:

1. Build photo candidates in memory.
2. Extract `DisplayName` and `ExternalId` for each candidate.
3. Run direct-ID matches for players that already have an `ExternalId`.
4. Run deterministic global matching for unresolved players.
5. Run AI fallback pass 1 for still unresolved players.
6. Run AI fallback pass 2 (optional) with wider candidate recall.
7. Write no-match rows for anything still unresolved.
8. Persist mapped CSV.

## Step 1: Candidate Ingestion

Each photo file is parsed into a candidate record with:
- File path
- File name
- Parsed metadata (`ExternalId`, optional parsed full name)
- `DisplayName` used for matching

Metadata precedence:
1. Manifest entry
2. User filename template
3. Auto-detected filename pattern
4. Raw filename fallback

## Step 2: Deterministic Name Signature

Each name is normalized before scoring:
- Remove accents/diacritics
- Lowercase
- Normalize separators (`_`, `-`, punctuation)
- Keep alphanumeric tokens
- Remove numeric-only tokens from token-set scoring

Cached `NameSignature` fields:
- Normalized full string
- Token array
- Token hash set

## Step 3: Deterministic Candidate Score

Score combines multiple generic signals:
- Token overlap ratio
- Jaccard similarity
- Soft token-to-token similarity
- Full normalized string similarity

Safety/boost rules:
- Strong subset/equality token evidence is boosted.
- Zero-overlap is strongly capped.
- Weak single-token-overlap is capped.
- Near-variant case is handled generically (same initial plus strong similarity or shared prefix), without hardcoded nickname maps.

This avoids rigid language-specific dictionaries while still catching common short/long variants.

## Step 4: Global Deterministic Assignment

The algorithm does not finalize row-by-row greedily.

For each unresolved player:
- Rank remaining candidates by deterministic score.
- Build a proposal only when confidence and margin are high enough.

Then resolve conflicts globally:
- Sort proposals by confidence descending.
- Accept a proposal only if both player and photo are still free.
- Remove accepted player/photo and iterate again until no further safe proposals exist.

This reduces early-lock mistakes that happen with simple first-come-first-serve matching.

### First-Round Confidence ("Probability") Behavior

The first round uses deterministic confidence (0.0-1.0), not LLM confidence.

How it works:
- CLI `-t/--confidenceThreshold` sets the minimum deterministic confidence accepted as a safe match.
- A proposal must also pass an ambiguity margin check against the next-best candidate.
- If score is high enough but margin is too small, the row is deferred to AI fallback instead of forcing a risky match.

Practical effect:
- Lower threshold increases automatic mapping recall, but can increase false positives.
- Higher threshold is safer, but sends more rows to AI (slower overall).
- Recommended operating window for most runs is usually around `0.7` to `0.8`, with validation against a trusted reference before locking values.

## Step 5: AI Fallback (Only for Unresolved Rows)

AI is used only after deterministic matching finishes.

For each unresolved player:
- Build a shortlist from deterministic ranking (adaptive thresholds, bounded size).
- Compare player vs shortlist candidates using `INameMatchingService`.
- Accept only high-confidence, non-ambiguous results.

Two-pass behavior:
- Pass 1: tighter shortlist and stricter ambiguity margin.
- Pass 2 (optional): wider shortlist for recall recovery.

Current Ollama prompt logic is in:
- `/Users/luis/Repos/PhotoMapperAI/src/PhotoMapperAI/Services/AI/OllamaNameMatchingService.cs`

Prompt requirements:
- Token-order agnostic (`Family Given` vs `Given Family`)
- Conservative against false positives
- Explicit handling for subset and near-variant evidence

## Why This Design

This structure is aimed at real production data where:
- Token order varies by source.
- Multi-surname names are common.
- Some rows have short/long first-name variants.
- Full pairwise AI (`players x candidates`) is too slow.

Deterministic-first plus targeted AI fallback gives:
- Lower runtime
- Better traceability
- Better precision/recall balance
- Easier tuning without hardcoded name lists

## Tuning Knobs

Main tuning knobs live in `/Users/luis/Repos/PhotoMapperAI/src/PhotoMapperAI/Commands/MapCommand.cs`:
- Deterministic accept threshold (`confidenceThreshold`)
- Proposal ambiguity margin
- AI shortlist size
- AI preselect minimum score
- AI score-gap-from-top
- AI ambiguity margin (pass 1 vs pass 2)

## Ollama Name-Model Benchmark Snapshot

The following is a local benchmark snapshot from the built-in map-reference harness, captured as a model-comparison baseline for the current pipeline design. Values are workload/hardware dependent and should be treated as directional.

| Model | Pass | Diff | Failed (timeout) | Avg time per run | Not found | Wrong ID |
|---|---:|---:|---:|---:|---:|---:|
| `qwen2.5:7b` | 12 | 4 | 0 | 23.96s | 7 | 0 |
| `qwen2.5-coder:7b-instruct-q4_K_M` | 12 | 2 | 2 | 37.36s | 3 | 0 |
| `qwen3:8b` | 10 | 0 | 6 | 68.97s | 0* | 0* |

\*`qwen3:8b` had multiple timeouts on AI-heavy teams, so those quality counters are not fully representative.

### Interpretation

- `qwen2.5:7b`: strongest stability/performance balance in this pipeline.
- `qwen2.5-coder:7b-instruct-q4_K_M`: can recover some difficult rows but currently less stable (timeouts observed).
- `qwen3:8b`: deterministic-only teams complete fast, but AI-fallback teams can stall/timeout in this setup.

### Current Recommendation

- Default: `qwen2.5:7b`
- Alternative for targeted experiments: `qwen2.5-coder:7b-instruct-q4_K_M`
- Avoid for unattended batch runs (current setup): `qwen3:8b` unless timeout/serving behavior is improved

## OpenAI `gpt-4.1` Cost/Quality Snapshot (MAP)

Validated against trusted Denmark reference mapping (`24` players).
Token-cost estimates use OpenAI list pricing for `gpt-4.1` as checked on February 13, 2026 (`$2.00/M` input, `$8.00/M` output).

### Run A: AI-only

- Config: `-n openai:gpt-4.1 -a -ap --aiOnly -t 0.8`
- Console summary: matched `21/24`, `31` AI calls, `19,327` input tokens, `2,082` output tokens
- Reference comparison: `21` correct, `0` wrong ID, `3` unmapped
- Estimated API cost:
  - Input: `19,327 * $2.00 / 1M = $0.0387`
  - Output: `2,082 * $8.00 / 1M = $0.0167`
  - Total: `~$0.0553`

### Run B: Normal deterministic+AI flow

- Config: `-n openai:gpt-4.1 -a -ap -t 0.8` (without `--aiOnly`)
- Console summary: matched `23/24`, `3` AI calls, `1,857` input tokens, `213` output tokens
- Reference comparison: `23` correct, `0` wrong ID, `1` unmapped
- Estimated API cost:
  - Input: `1,857 * $2.00 / 1M = $0.0037`
  - Output: `213 * $8.00 / 1M = $0.0017`
  - Total: `~$0.0054`

### Interpretation

- For this workload, `openai:gpt-4.1` is a practical minimum recommendation when you insist on `--aiOnly`.
- Even with `gpt-4.1`, `--aiOnly` still underperformed normal deterministic+AI flow (`21/24` vs `23/24`) and cost about `10x` more.
- Preferred production mode remains deterministic first + AI fallback (`-a -ap`, no `--aiOnly`).

## Ollama Cloud Candidates (Name Comparison Use Case)

This section summarizes cloud models currently relevant to MAP name-comparison runs. It is intentionally focused on this task (short text-pair matching with strict precision requirements), not general coding-agent benchmarks.

### Cloud Model Fit Snapshot

| Cloud model | Official positioning | Fit for MAP name comparison | Main risk in this pipeline | Recommendation |
|---|---|---|---|---|
| `gemini-3-flash-preview:cloud` | Fast frontier general model | Good first cloud candidate (speed-oriented) | Can still produce variability in confidence formatting/behavior | Priority 1 test |
| `qwen3-coder-next:cloud` | Agentic coding model, non-thinking mode, 256K | Strong reasoning candidate for hard cases | May be slower/costlier than needed for short pairwise comparisons | Priority 2 test |
| `kimi-k2.5:cloud` | Multimodal/agentic model, 256K | Potentially strong reasoning on ambiguous names | Multimodal/agentic overhead can hurt latency consistency | Priority 2 test |
| `glm-4.7:cloud` | Coding/reasoning/tool-using model | Reasonable candidate for difficult edge cases | Thinking-style behavior can increase response latency | Priority 3 test |
| `minimax-m2:cloud` | Coding/agentic efficiency focus | Possible alternative if above are unstable | Tends to be tuned for broader agent loops, not strict name linkage | Priority 3 test |
| `qwen3-coder:480b-cloud` | Very large cloud coding model | Likely high ceiling on difficult disambiguation | Too slow for high-throughput MAP fallback in many environments | Use only for spot checks |

### Suggested Test Order

To minimize retries and compare fairly against local baselines:
1. `gemini-3-flash-preview:cloud`
2. `qwen3-coder-next:cloud`
3. `kimi-k2.5:cloud`
4. `glm-4.7:cloud`
5. `minimax-m2:cloud`

Use `qwen3-coder:480b-cloud` only for very small, hard subsets where quality is more important than throughput.

### Decision Criteria (Same as Local Benchmarks)

When choosing cloud vs local for MAP, rank by:
1. Completion stability (`FAILED`/timeout rate)
2. Quality (`DIFF`, `Not found`, `Wrong ID`)
3. Throughput (average team runtime)
4. AI trace behavior (accepted vs rejected, and reject reasons)

In practice, a cloud model that is slightly more accurate but frequently times out is usually worse for end-to-end team mapping than a stable local model.

### Sources

- Ollama model search (cloud filter): [ollama.com/search?c=cloud](https://ollama.com/search?c=cloud)
- OpenAI pricing: [platform.openai.com/pricing](https://platform.openai.com/pricing)
- Gemini 3 Flash Preview: [ollama.com/library/gemini-3-flash-preview:cloud](https://ollama.com/library/gemini-3-flash-preview%3Acloud)
- Qwen3-Coder-Next: [ollama.com/library/qwen3-coder-next:cloud](https://ollama.com/library/qwen3-coder-next%3Acloud)
- Kimi K2.5: [ollama.com/library/kimi-k2.5:cloud](https://ollama.com/library/kimi-k2.5%3Acloud)
- GLM-4.7: [ollama.com/library/glm-4.7:cloud](https://ollama.com/library/glm-4.7%3Acloud)
- MiniMax M2: [ollama.com/library/minimax-m2:cloud](https://ollama.com/library/minimax-m2%3Acloud)
- Qwen3-Coder 480B Cloud: [ollama.com/library/qwen3-coder:480b-cloud](https://ollama.com/library/qwen3-coder%3A480b-cloud)

## Validation Workflow

Recommended validation loop after changes:

1. Run map command on target dataset.
2. Compare produced mapped CSV against trusted reference mapped CSV.
3. Inspect:
- Total matched
- False positives
- False negatives
- Conflict cases

If quality regresses, discard the branch changes and retune thresholds/rules.
