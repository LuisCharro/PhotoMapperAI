#!/bin/bash

# PhotoMapperAI End-to-End Test Script
# Tests all core commands in sequence

set -e  # Exit on error

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}===========================================${NC}"
echo -e "${BLUE}PhotoMapperAI - Full Workflow Test${NC}"
echo -e "${BLUE}===========================================${NC}"
echo ""

# Step 1: Extract (synthetic data)
echo -e "${GREEN}Step 1: Extract Player Data${NC}"
echo "------------------------------------------------"
dotnet run --project src/PhotoMapperAI/PhotoMapperAI.csproj -- extract \
  --inputSqlPath get_players.sql \
  --connectionStringPath connection.txt \
  --teamId 1 \
  --outputName test_workflow.csv
echo ""

# Step 2: Map with manifest
echo -e "${GREEN}Step 2: Map Photos to Players${NC}"
echo "------------------------------------------------"
dotnet run --project src/PhotoMapperAI/PhotoMapperAI.csproj -- map \
  --inputCsvPath test_workflow.csv \
  --photosDir ./photos \
  --photoManifest photo_manifest.json
echo ""

# Step 3: Generate portraits
echo -e "${GREEN}Step 3: Generate Portrait Photos${NC}"
echo "------------------------------------------------"
dotnet run --project src/PhotoMapperAI/PhotoMapperAI.csproj -- generatephotos \
  --inputCsvPath mapped_test_workflow.csv \
  --photosDir ./photos \
  --processedPhotosOutputPath ./portraits_workflow \
  --format jpg
echo ""

# Step 4: Benchmark (quick test)
echo -e "${GREEN}Step 4: Benchmark Model Performance${NC}"
echo "------------------------------------------------"
echo "Running quick benchmark on name matching..."
dotnet run --project src/PhotoMapperAI/PhotoMapperAI.csproj -- benchmark \
  --nameModels qwen2.5:7b \
  --testDataPath ./test-data
echo ""

# Summary
echo -e "${GREEN}===========================================${NC}"
echo -e "${GREEN}Workflow Test Complete!${NC}"
echo -e "${GREEN}===========================================${NC}"
echo ""
echo -e "${YELLOW}Generated Files:${NC}"
echo "  - test_workflow.csv (player data)"
echo "  - mapped_test_workflow.csv (photo mappings)"
echo "  - portraits_workflow/*.jpg (generated portraits)"
echo "  - benchmark-results/*.json (performance metrics)"
echo ""
echo -e "${GREEN}✓ All core commands tested successfully!${NC}"
