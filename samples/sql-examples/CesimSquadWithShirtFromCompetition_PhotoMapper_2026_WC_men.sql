-- Cesim squad export for PhotoMapperAI (2026 World Cup, men)
-- Source of truth: cesim.dbo.CompetitorContestData
--   CompetitorContestDataTypeId = 1 -> Shirt Number
--   CompetitorContestDataTypeId = 2 -> Function Type (one row per squad member,
--                                       includes the head coach: Function = 'FootballCoach')
--
-- Unlike CesimPlayersToPhotoMapper.sql (CompetitorRelation, cmptId in (1,3)),
-- this query includes the coach (coach is cmptId = 4; the reliable signal is
-- Function = 'FootballCoach', not cmptId) and the shirt number.
--
-- Parameters expected by PhotoMapperAI: @TeamId
-- Competition (contest) is the 2026 World Cup men's competition: ContestId = 5193
--
-- Output columns map to the PhotoMapperAI CSV schema, plus the two new optional
-- columns ShirtNumber and Function. ShirtNumber is NULL/empty when cesim has no
-- shirt data yet (e.g. before squad release) and for the coach.

select
    c.compId            as PlayerId,
    func.CompetitorId   as TeamId,
    c.compName1         as FamilyName,
    c.compName2         as SurName,
    cast(null as nvarchar(50)) as External_Player_ID,
    shirt.Value         as ShirtNumber,
    func.Value          as [Function]
from
    cesim.dbo.CompetitorContestData func
    join cesim.dbo.Competitor c
        on c.compId = func.CompetitorMemberId
    left join cesim.dbo.CompetitorContestData shirt
        on shirt.CompetitorId            = func.CompetitorId
        and shirt.CompetitorMemberId     = func.CompetitorMemberId
        and shirt.ContestId              = func.ContestId
        and shirt.CompetitorContestDataTypeId = 1
where
    func.ContestId = 5193
    and func.CompetitorContestDataTypeId = 2
    and (isnull(@TeamId, 0) = 0 or func.CompetitorId = @TeamId)
order by
    case when shirt.Value is null then 999 else cast(shirt.Value as int) end,
    c.compName1, c.compName2;
