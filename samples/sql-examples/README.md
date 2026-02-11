# SQL Query Examples for PhotoMapperAI

This directory contains example SQL queries for extracting player data from different database systems to use with PhotoMapperAI.

## Overview

PhotoMapperAI's `extract` command requires a SQL query that returns player data in a specific format. These examples show how to structure queries for different database types.

## Required Output Format

All queries must return columns in this exact order:

| Column Name | Type | Description |
|-------------|------|-------------|
| PlayerId | int/number | Internal system ID (primary key) |
| TeamId | int/number | Team identifier for filtering |
| FamilyName | string | Player's last name / family name |
| SurName | string | Player's first name / given name |
| ExternalId | string | Photo mapping ID (leave empty: '' or NULL) |
| ValidMapping | int/boolean | Mapping status (use 0 or false) |
| Confidence | number | Match confidence (use 0.0) |
| FullName | string | Full name for display (can concatenate columns) |

## Database-Specific Examples

### 1. SQL Server (`sql-server-players.sql`)

**Usage:**
```bash
photomapperai extract \
  -inputSqlPath samples/sql-examples/sql-server-players.sql \
  -teamId 10 \
  -outputName team.csv
```

**Features:**
- Uses `@parameter` syntax for parameters
- Column aliases with `[Brackets]`
- `CONCAT()` function for name concatenation
- Case-sensitive column names

### 2. MySQL (`mysql-players.sql`)

**Usage:**
```bash
photomapperai extract \
  -inputSqlPath samples/sql-examples/mysql-players.sql \
  -teamId 10 \
  -outputName team.csv
```

**Features:**
- Uses `{parameter}` syntax for parameters
- Column aliases with backticks \`\`
- `CONCAT()` function for name concatenation
- Typically lowercase column names

### 3. PostgreSQL (`postgresql-players.sql`)

**Usage:**
```bash
photomapperai extract \
  -inputSqlPath samples/sql-examples/postgresql-players.sql \
  -teamId 10 \
  -outputName team.csv
```

**Features:**
- Uses `$1`, `$2` syntax for positional parameters
- Column aliases with double quotes `"`
- `||` operator for string concatenation
- Case-sensitive column names

### 4. SQLite (`sqlite-players.sql`)

**Usage:**
```bash
photomapperai extract \
  -inputSqlPath samples/sql-examples/sqlite-players.sql \
  -teamId 10 \
  -outputName team.csv
```

**Features:**
- Uses `@parameter` syntax for named parameters
- No column aliases needed (simple names)
- `||` operator for string concatenation
- Case-sensitive column names

## Adapting to Your Database Schema

### Step 1: Identify Your Tables

Find the table(s) containing player information. Common table names:
- `Players`, `Player`, `tbl_players`
- `TeamPlayers`, `Team_Members`
- `Person`, `People`

### Step 2: Map Columns

Map your existing columns to the required output format:

| Your Column | Required Column | Notes |
|-------------|-----------------|--------|
| player_id | PlayerId | Primary key |
| team_id | TeamId | Foreign key to teams |
| last_name, surname | FamilyName | Family name |
| first_name, given_name | SurName | First name |
| (leave empty) | ExternalId | Will be filled by mapping |
| (use 0/false) | ValidMapping | Will be updated by mapping |
| (use 0.0) | Confidence | Will be updated by mapping |
| (construct) | FullName | Can be SELECT expression |

### Step 3: Add WHERE Clause

Filter by team or other criteria as needed:

```sql
-- Filter by single team
WHERE TeamId = @TeamId

-- Filter by multiple teams
WHERE TeamId IN (@TeamId1, @TeamId2, @TeamId3)

-- Filter by season
WHERE Season = @Season AND TeamId = @TeamId

-- No filtering (all players)
-- (omit WHERE clause entirely)
```

### Step 4: Order Results

Order results for consistency:

```sql
ORDER BY FamilyName, SurName

-- Or by player ID
ORDER BY PlayerId

-- Or custom order
ORDER BY JerseyNumber, FamilyName, SurName
```

## Common Adaptations

### Different Table Names

If your table is named differently:

```sql
-- Instead of:
FROM Players p

-- Use your table name:
FROM MyPlayerTable p
FROM player_data p
FROM team_members p
```

### Different Column Names

If your columns are named differently:

```sql
-- SQL Server
SELECT
    p.player_pk AS [PlayerId],          -- Your primary key
    p.team_fk AS [TeamId],            -- Your team foreign key
    p.last_nm AS [FamilyName],         -- Your last name column
    p.first_nm AS [SurName],           -- Your first name column
    ...

-- MySQL
SELECT
    p.id AS `PlayerId`,
    p.team_id_fk AS `TeamId`,
    p.apellido AS `FamilyName`,         -- Spanish example
    p.nombre AS `SurName`,             -- Spanish example
    ...

-- PostgreSQL
SELECT
    p.id AS "PlayerId",
    p.equipe_id AS "TeamId",           -- French example
    p.nom AS "FamilyName",             -- French example
    p.prenom AS "SurName",           -- French example
    ...
```

### Including Additional Columns

You can include extra columns if needed (PhotoMapperAI will ignore them):

```sql
SELECT
    p.PlayerId,
    p.TeamId,
    p.FamilyName,
    p.SurName,
    '' AS ExternalId,
    0 AS ValidMapping,
    0.0 AS Confidence,
    CONCAT(p.SurName, ' ', p.FamilyName) AS FullName,
    p.JerseyNumber,      -- Extra column (ignored)
    p.Position,          -- Extra column (ignored)
    p.Nationality        -- Extra column (ignored)
FROM Players p
WHERE p.TeamId = @TeamId
```

## Testing Your Query

### 1. Test Directly in Your Database

Run the query in your database tool (SSMS, MySQL Workbench, pgAdmin, etc.) with parameters to verify results match the required format.

### 2. Test with PhotoMapperAI

Test with a small subset first:

```bash
# Test with team ID 10
photomapperai extract \
  -inputSqlPath samples/sql-examples/sql-server-players.sql \
  -teamId 10 \
  -outputName test-team-10.csv

# Verify output
cat test-team-10.csv
```

### 3. Verify CSV Format

The output CSV should look like:

```csv
PlayerId,TeamId,FamilyName,SurName,ExternalId,ValidMapping,Confidence,FullName
1,10,Martínez,Rodriguez,,0,0,Rodríguez Martínez
2,10,Sánchez,Andrés,,0,0,Andrés Sánchez
3,10,Ramos,Sergio,,0,0,Sergio Ramos
```

## Troubleshooting

### "Parameter not recognized" Errors

**Problem:** Database doesn't recognize the parameter syntax.

**Solution:** Use the correct syntax for your database:
- SQL Server: `@parameter`
- MySQL: `{parameter}`
- PostgreSQL: `$1`, `$2` (positional)
- SQLite: `@parameter` (named) or `?` (positional)

### "Column not found" Errors

**Problem:** Column names don't match your schema.

**Solution:**
1. Check your actual column names: `DESCRIBE Players;` (MySQL), `sp_help Players;` (SQL Server)
2. Update the query to use correct column names
3. Use aliases to match required output format

### Empty CSV Output

**Problem:** Query runs but produces no rows.

**Solution:**
1. Verify WHERE clause condition is correct
2. Check if data exists for the given TeamId
3. Test query without WHERE clause first
4. Check database connection string is correct

### Incorrect Column Order

**Problem:** CSV columns are in wrong order.

**Solution:** Ensure SELECT clause lists columns in exact required order:
1. PlayerId
2. TeamId
3. FamilyName
4. SurName
5. ExternalId
6. ValidMapping
7. Confidence
8. FullName

## Advanced Examples

### Multiple Teams at Once

Extract data for multiple teams in a single query:

```sql
-- SQL Server
SELECT
    p.PlayerId,
    p.TeamId,
    p.FamilyName,
    p.SurName,
    '' AS ExternalId,
    0 AS ValidMapping,
    0.0 AS Confidence,
    CONCAT(p.SurName, ' ', p.FamilyName) AS FullName
FROM Players p
WHERE p.TeamId IN (@TeamId1, @TeamId2, @TeamId3)
ORDER BY p.TeamId, p.FamilyName, p.SurName;
```

### Include Additional Metadata

Add jersey numbers, positions, etc. for reference:

```sql
SELECT
    p.PlayerId,
    p.TeamId,
    p.FamilyName,
    p.SurName,
    '' AS ExternalId,
    0 AS ValidMapping,
    0.0 AS Confidence,
    CONCAT(p.SurName, ' ', p.FamilyName) AS FullName,
    p.JerseyNumber,
    p.Position,
    p.Height,
    p.Weight
FROM Players p
WHERE p.TeamId = @TeamId
ORDER BY p.JerseyNumber, p.FamilyName, p.SurName;
```

## Support

For more help:
- Check main README: `../README.md`
- Check architecture docs: `../../docs/ARCHITECTURE_DECISIONS.md`
- Report issues: https://github.com/LuisCharro/PhotoMapperAI/issues

## Contributing

If you create a query for a different database system (Oracle, DB2, etc.), please contribute it to this directory!
