SELECT
    PlayerId,
    TeamId,
    FamilyName,
    SurName,
    External_Player_ID
FROM Players
WHERE TeamId = @TeamId;
