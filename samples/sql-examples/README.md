# SQL Query Examples for PhotoMapperAI

This folder contains example SQL queries for the CLI `extract` command.

## Required `extract` Inputs

The command needs:

- `--inputSqlPath`
- `--connectionStringPath`
- `--outputName`
- `--teamId` for player extraction

For team extraction, use `--extractTeams` and provide a query that returns team rows.

## Expected Player Columns

The CSV reader/writer expects the standard player fields used by the app, including:

- `PlayerId`
- `TeamId`
- `FamilyName`
- `SurName`
- `External_Player_ID`
- `Valid_Mapping`
- `Confidence`
- `FullName`

Additional columns are tolerated when the downstream workflow does not depend on them.

## Example Usage

```bash
dotnet run --project src/PhotoMapperAI -- extract \
  --inputSqlPath samples/sql-examples/sql-server-players.sql \
  --connectionStringPath path/to/connection.txt \
  --teamId 10 \
  --outputName team.csv
```

Team extraction example:

```bash
dotnet run --project src/PhotoMapperAI -- extract \
  --inputSqlPath path/to/get_teams.sql \
  --connectionStringPath path/to/connection.txt \
  --outputName teams.csv \
  --extractTeams
```

## Files

- `sql-server-players.sql`
- `mysql-players.sql`
- `postgresql-players.sql`
- `sqlite-players.sql`

Use them as shape examples and adapt table names, filters, and parameter syntax to your actual schema/database.
