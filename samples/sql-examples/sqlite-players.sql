-- PhotoMapperAI - SQLite Example Query
-- Extracts player data for photo mapping from SQLite database
-- Usage: photomapperai extract -inputSqlPath sqlite-players.sql -teamId 10 -outputName team.csv

-- Parameters: @TeamId
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
    p.PlayerId AS PlayerId,
    p.TeamId AS TeamId,
    p.FamilyName AS FamilyName,
    p.FirstName AS SurName,
    '' AS ExternalId,
    0 AS ValidMapping,
    0.0 AS Confidence,
    p.FirstName || ' ' || p.FamilyName AS FullName
FROM Players p
WHERE p.TeamId = @TeamId
ORDER BY p.FamilyName, p.FirstName;
