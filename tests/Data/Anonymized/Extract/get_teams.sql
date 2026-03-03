-- Get all teams
-- Output: TeamId, TeamName

SELECT 
    t.TeamId,
    t.TeamName
FROM Teams t
ORDER BY t.TeamId;
