-- PhotoMapperAI - PostgreSQL Example Query
-- Extracts player data for photo mapping from PostgreSQL database
-- Usage: photomapperai extract -inputSqlPath postgresql-players.sql -teamId 10 -outputName team.csv

-- Parameters: $1 (TeamId)
-- Expected output columns (in order):
--   PlayerId (internal system ID)
--   TeamId
--   FamilyName
--   SurName (first name)
--   ExternalId (empty - will be filled by mapping)
--   ValidMapping (0/False - will be updated by mapping)
--   Confidence (0 - will be updated by mapping)
--   FullName (generated column for display)

SELECT
    p.player_id AS "PlayerId",
    p.team_id AS "TeamId",
    p.family_name AS "FamilyName",
    p.first_name AS "SurName",
    '' AS "ExternalId",
    0 AS "ValidMapping",
    0.0 AS "Confidence",
    p.first_name || ' ' || p.family_name AS "FullName"
FROM players p
WHERE p.team_id = $1
ORDER BY p.family_name, p.first_name;
