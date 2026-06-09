# Shirt-Number Mapping (2026 World Cup) — Design

- **Date:** 2026-06-09
- **Status:** Approved for planning
- **Scope:** CLI + extract now; Avalonia UI in a later pass.

## Problem

The 2026 World Cup photo set uses a new filename convention that no longer
contains the player name. Example:

```
C:\FIFA_Images\2026_WC\src\Brasilien\BRA-H-01-M1-PPROFILE-308370-MK.png
```

Structure (verified consistent across all three sample teams — Brasilien,
Schottland, Südafrika):

```
{TEAM}-{H}-{CODE}-{VARIANT}-PPROFILE-{FIFAID}-{SUFFIX}.png
```

- `{TEAM}` — 3-letter team code (BRA, SCO, RSA). Not needed for matching
  (photos are processed per team folder).
- `{CODE}` — zero-padded shirt number (`01`..`26`) **or** `HC` for the head
  coach. Shirt numbers are not guaranteed contiguous (Schottland skips `02`).
- `{FIFAID}` — FIFA player id (e.g. `308370`). This is the payload to record,
  not a join key.
- The remaining segments (`H`, `M1`, `PPROFILE`, `MK`) are fixed/irrelevant.

The existing name-matching pipeline cannot work because there is no name in the
filename.

Our per-team CSVs (e.g. `players_8437_Brasilien.csv`) are keyed on the cesim
`compId` (`PlayerId`, e.g. Neymar = `59452`) with an empty `External_Player_ID`
that mapping is meant to fill. The FIFA id from the filename is **not** stored
in cesim, so it cannot be joined directly.

## Key facts (verified against cesim)

Source table: `cesim.dbo.CompetitorContestData`, columns
`Id, CompetitorId, CompetitorMemberId, ContestId, MasterEventId, EventId,
CompetitorContestDataTypeId, Value (varchar)`.

Lookup `cesim.dbo.CompetitorContestDataType`:

| Id | Description   |
|----|---------------|
| 1  | Shirt Number  |
| 2  | Function Type |

For team `8437` (Brasilien), contest `5193` (2026 WC men):

- Type 1 (Shirt Number): 26 rows — players only, values `1`..`26`.
- Type 2 (Function Type): 27 rows — 26 players + 1 coach. Values such as
  `FootballKeeper`, `FootballDefender`, `FootballMidfielder`,
  `FootballForward`, `FootballCoach`.

**The shirt number is the bridge:** filename `{CODE}` (normalized to int) joins
to the cesim shirt number, which identifies the cesim player; the FIFA id is
recorded as the `External_Player_ID` payload. Example:
`BRA-H-01-...-308370` → shirt `1` → Becker Alisson (compId `83725`) →
that player's `External_Player_ID` becomes `308370`.

**Root cause of the missing coach:** the current CSVs were produced by
`CesimPlayersToPhotoMapper.sql`, which filters `c.cmptId in (1, 3)`. The coach
(Ancelotti, compId `29213`) is `cmptId = 4`, so he is excluded. The reliable
signal for "coach" is `Function = 'FootballCoach'`, **not** `cmptId`.

A single query against `CompetitorContestData` (type 2 for the squad incl.
coach, left-joined to type 1 for the shirt number) returns the complete and
correct 27-row squad. This query has been validated.

## Design decisions

1. **Join key = shirt number** (filename code ↔ cesim shirt number). The FIFA id
   is the recorded payload, not a join key.
2. **Architecture = Path A:** enrich the CSV at extract time. `map` stays
   DB-free, the CSV is self-contained and reproducible.
3. **Mode selection:** default behavior (no flag) is name matching and is
   unchanged. Shirt-number matching is opt-in via a new `map` parameter.
4. **Coach handling:** extract must include the coach row (so there is a row to
   map to). Automatic matching covers the numbered shirts only. The `HC` coach
   photo is mapped manually in the UI later (existing manual-repair feature).
   No `HC → coach` logic is built into the CLI. CLI-only runs leave the coach
   unmapped, which is acceptable.
5. **Scope:** CLI + extract in this iteration; UI toggle in a later pass.

## Solution

### 1. Extract (enrich the CSV)

- A new WC SQL template has been created and validated at
  `C:\FIFA_Images\2026_WC\CesimSquadWithShirtFromCompetition_PhotoMapper_2026_WC_men.sql`
  (a copy/equivalent should also live under `samples/` in the repo). It is based
  on `CompetitorContestData`:

  ```sql
  -- Parameters: @TeamId. Contest is the WC men's competition (5193).
  SELECT
    c.compId            AS PlayerId,
    func.CompetitorId   AS TeamId,
    c.compName1         AS FamilyName,
    c.compName2         AS SurName,
    CAST(NULL AS nvarchar(50)) AS External_Player_ID,
    shirt.Value         AS ShirtNumber,
    func.Value          AS [Function]
  FROM cesim.dbo.CompetitorContestData func
  JOIN cesim.dbo.Competitor c
    ON c.compId = func.CompetitorMemberId
  LEFT JOIN cesim.dbo.CompetitorContestData shirt
    ON shirt.CompetitorId    = func.CompetitorId
   AND shirt.CompetitorMemberId = func.CompetitorMemberId
   AND shirt.ContestId       = func.ContestId
   AND shirt.CompetitorContestDataTypeId = 1
  WHERE func.CompetitorId = @TeamId
    AND func.ContestId = 5193
    AND func.CompetitorContestDataTypeId = 2
  ORDER BY
    CASE WHEN shirt.Value IS NULL THEN 999 ELSE CAST(shirt.Value AS INT) END;
  ```

- `DatabaseExtractor` writes two new **optional, nullable** CSV columns:
  - `ShirtNumber` — string; empty when cesim has no shirt data yet.
  - `Function` — cesim function type; used later by the UI to identify the
    coach and roles.
- The CSV DTOs treat both columns as optional so existing CSVs (2024_Euro and
  earlier WC exports) that lack them still load without error.

### 2. CLI map (opt-in shirt matching)

- New parameter on `map`: `--matchBy name|shirt`, default `name`.
  - `name` → current pipeline, unchanged.
  - `shirt` → enables the shirt-match stage below.
- New **WC filename pattern** added to `FilenameParser` auto-detection and as a
  named preset in `FilenamePatternSettings`. It captures the code field and the
  FIFA id from the `...-PPROFILE-...` structure, anchored on the literal
  `PPROFILE` segment for robustness.
- `PhotoMetadata` gains a shirt/code field (e.g. `ShirtCode`) holding the raw
  `{CODE}` value (`01`..`26` or `HC`). `External_Player_ID` continues to hold
  the FIFA id parsed from the filename.
- `PlayerRecord` gains `ShirtNumber` (and optionally `Function`) populated from
  the CSV.
- New deterministic **shirt-match stage** in `MapCommand`, run before name
  matching when `--matchBy shirt` is set:
  - Build an index of CSV players by integer-normalized shirt number
    (`"01"` → `1`, ignoring empty/`HC`).
  - For each photo whose `{CODE}` is numeric, match to the player with the same
    normalized shirt number.
  - On match: set `player.External_Player_ID` = the FIFA id from the filename,
    `ValidMapping = true`, confidence = exact-match value. Method recorded as a
    new `MatchMethod` (e.g. `ShirtNumberMatch`).
  - This is an exact key match — no AI is involved.
- Photos with non-numeric codes (`HC`), numbered photos with no matching CSV
  shirt, and CSV players with no matching photo all fall through to the existing
  unmapped / no-match handling. The coach therefore remains unmapped in CLI.

### 3. Graceful degradation

- If `ShirtNumber` is absent or empty for a team (cesim not released yet), the
  shirt index is empty and no automatic matches occur. Because WC filenames
  carry no name, there is no name fallback; affected rows simply stay unmapped.
  This is reported in the run summary, never a crash.

## Impact / blast radius

`map` remains DB-free. Changes are localized to:

- `src/PhotoMapperAI/Utils/FilenameParser.cs` — new WC pattern + code/id capture.
- `src/PhotoMapperAI/Models/FilenamePatternSettings.cs` — new preset.
- `src/PhotoMapperAI/Models/PhotoMetadata.cs` — shirt/code field.
- `src/PhotoMapperAI/Models/PlayerRecord.cs` + CSV DTOs in
  `src/PhotoMapperAI/Services/Database/DatabaseExtractor.cs` — `ShirtNumber`
  (+ `Function`) optional columns.
- `src/PhotoMapperAI/Commands/MapCommand.cs` — `--matchBy` option and the
  shirt-match stage.
- New WC SQL template under `samples/`.
- Unit tests for: WC filename parsing, shirt normalization, the shirt-match
  stage (including unmatched/empty-shirt cases), and CSV round-trip with the new
  optional columns.

## Out of scope (explicit follow-up)

- Avalonia UI: a `match by shirt number` toggle in the Map step and batch flow,
  and the WC extract surfaced in the UI. The coach is mapped manually there
  using the existing click-to-assign manual-repair feature.
- Automatic `HC → coach` matching.
- Any change to the 2024_Euro / name-based flow beyond keeping it the default.
- UI face-detection default: the CLI `generatephotos` default detector on
  Windows/Linux is now `opencv-dnn,opencv-yunet,center` (opencv-yunet alone
  downscales to a fixed 320x320 and misses ~58% of faces in 1920x1080 photos).
  The Avalonia UI/batch Windows default (currently `opencv-yunet`) should be
  updated to the same chain when the UI work is done.

## Portrait generation notes (added during end-to-end verification)

- `generatephotos` locates each player's photo via `FindPlayerPhotoFiles`, which
  now also matches a hyphen-delimited FIFA id (`*-{id}-*.*`) so WC filenames like
  `BRA-H-01-M1-PPROFILE-308370-MK.png` resolve.
- Verified on Brasilien (26 players): with the default chain, 24 faces detected
  by opencv-dnn, 2 by opencv-yunet, 0 fell back to center, 0 undetected.

## Verification approach

- Unit tests as above (xUnit, following `40-testing.md`).
- Manual CLI validation against `C:\FIFA_Images\2026_WC`:
  - Re-extract Brasilien → CSV has 27 rows incl. coach, shirts `1`..`26`.
  - `map --matchBy shirt` over `src/Brasilien` → 26 players mapped with the
    correct FIFA ids; `HC` photo unmapped.
  - A name-based competition (2024_Euro) with no flag → behavior unchanged.
