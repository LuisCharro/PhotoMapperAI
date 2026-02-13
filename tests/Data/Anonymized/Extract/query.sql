SELECT
    PlayerId,
    TeamId,
    FamilyName,
    SurName,
    ExternalId
FROM Players
WHERE TeamId = @TeamId;
