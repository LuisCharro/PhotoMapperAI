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
