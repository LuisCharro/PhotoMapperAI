-- Sample SQL query to extract players by team
-- This is a user-provided query that exports data from their database
-- The column aliases are standardized for PhotoMapperAI

SELECT
    p.PlayerId AS [UserId],
    p.FamilyName AS [FamilyName],
    p.SurName AS [SurName],
    CAST(NULL AS NVARCHAR(50)) AS [Fifa_Player_ID],
    CAST(NULL AS DECIMAL(5,4)) AS [Valid_Mapping]
FROM Players p
INNER JOIN Teams t ON p.TeamId = t.TeamId
WHERE t.TeamId = {teamId}
ORDER BY p.FamilyName, p.SurName;

-- Note: {teamId} is a placeholder that will be replaced by the CLI parameter
-- Alternatively, use parameterized queries: @teamId
