# PhotoMapperAI

AI-powered command-line tool for mapping sports player photos to database systems. Automatically matches player names using LLMs and generates portrait crops from full-body photos.

## Problem Solved

Sports organizations receive photo sets from external sources (e.g., FIFA) with limited metadata (typically just `PlayerID_FamilyName_Surname.png`). Integrating these into internal database systems requires:
1. Extracting player data from the internal system
2. Mapping external photos to internal player records
3. Cropping full-body photos to portrait format
4. Renaming photos to internal system IDs

This tool automates the entire workflow, making it database-agnostic and system-independent.

## Features

- **Database-agnostic extraction**: Export player data from any database via SQL queries to CSV format
- **AI-powered name matching**: Uses local LLMs (Ollama) to match player names with fuzzy probability matching
- **Automated portrait cropping**: Crops full-body photos to portrait format using AI-based face/eye detection
- **Flexible output**: Generate photos with internal system IDs for seamless integration
- **Rich CLI experience**: Color-coded console output showing mapping progress, unmatched players, and unused photos

## Workflow

### Step 1: Extract Data
```bash
PhotoMapperAI extract -inputSqlPath path/to/playersByTeam.sql -teamId 10 -outputName SpainTeam.csv
```
Runs a user-provided SQL query to export player data from the internal database to CSV format. Includes placeholder columns for `Fifa_Player_ID` and `Valid_Mapping`.

### Step 2: Map Photos to Players
```bash
PhotoMapperAI map -inputCsvPath path/to/SpainTeam.csv -photosDir path/to/photos/SpainTeam
```
- Loads all photo files and extracts metadata from filenames (`PlayerID_FamilyName_Surname.png`)
- Attempts direct ID matching first
- For unmatched players, uses AI (Ollama LLM) for fuzzy name matching
- Validates matches with confidence threshold (default: 0.9)
- Updates CSV with `Fifa_Player_ID` and `Valid_Mapping` columns

### Step 3: Generate Portraits
```bash
PhotoMapperAI generatePhotos -inputCsvPath path/to/SpainTeam.csv -processedPhotosOutputPath path/to/SpainTeamPhotos -format jpg
```
- Reads mapping CSV
- Uses AI vision model (Ollama Qwen Vision) to detect faces and eyes in full-body photos
- Calculates portrait crop area based on eye position
- Outputs portrait photos named with internal system IDs

## Tech Stack

- **.NET 10** (Core) - Command-line application
- **Ollama** - Local LLMs for name matching and vision models for face detection
- **Qwen Vision Model** - AI-based face/eye detection for portrait cropping
- **CSV processing** - Read/write player mappings
- **Command-line parser** - Rich CLI interface with subcommands

## Project Structure

```
PhotoMapperAI/
├── PhotoMapperAI.sln          # Solution file
├── src/
│   └── PhotoMapperAI/         # Main CLI project
└── README.md
```

## Getting Started

### Prerequisites

- .NET 10 SDK
- [Ollama](https://ollama.ai/) installed and running
  - Pull models: `ollama pull qwen2.5:7b` (for name matching)
  - Pull models: `ollama pull qwen2.5-vision` (for face detection)

### Building

```bash
dotnet build
```

### Usage

```bash
# Extract player data
dotnet run -- extract -inputSqlPath data.sql -outputName team.csv

# Map photos
dotnet run -- map -inputCsvPath team.csv -photosDir ./photos

# Generate portraits
dotnet run -- generatePhotos -inputCsvPath team.csv -processedPhotosOutputPath ./portraits -format jpg
```

## Use Cases

- **Football/Soccer**: World Cup, UEFA competitions, league player photos
- **Other Sports**: Basketball, tennis, athletics - any sport with photo requirements
- **Team Management**: Any organization needing to import external photos into internal systems

## Contributing

Contributions are welcome! This is an open-source project for the sports community.

## License

TBD

## Author

[Luis Charro](https://github.com/LuisCharro)
