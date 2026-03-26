#!/usr/bin/env python3
"""Generate statistics for validation results."""
import csv
import json
import os
from pathlib import Path
from collections import defaultdict

# Read validation aggregate
validation_dir = Path(
    os.environ.get("PHOTOMAPPER_VALIDATION_RUN_DIR", "./Validation_Run")
).expanduser().resolve()
aggregate_file = validation_dir / "validation_aggregate.csv"

stats = {
    "total_teams": 0,
    "total_players": 0,
    "total_correct": 0,
    "total_incorrect": 0,
    "total_unmatched": 0,
    "total_extra_photos": 0,
    "teams": {}
}

issue_categories = defaultdict(int)

with open(aggregate_file, 'r', encoding='utf-8') as f:
    reader = csv.DictReader(f)

    current_team = None
    team_stats = None

    for row in reader:
        team = row['Team']

        # New team
        if team != current_team:
            if team_stats:
                stats['teams'][current_team] = team_stats
                stats['total_teams'] += 1

            current_team = team
            team_stats = {
                'total': 0,
                'correct': 0,
                'incorrect': 0,
                'missing': 0,
                'extra': 0,
                'mismatches': []
            }

        # Count status
        status = row['Status']
        team_stats['total'] += 1
        stats['total_players'] += 1

        if status == 'correct':
            team_stats['correct'] += 1
            stats['total_correct'] += 1
        elif status == 'incorrect':
            team_stats['incorrect'] += 1
            stats['total_incorrect'] += 1

            ref = row['Reference_Name']
            photo = row['Photo_Name']

            # Categorize issue
            if ref in photo or photo in ref:
                # Subset/Superset - likely missing/extra name
                issue_categories['missing_middle_names'] += 1
                issue_type = 'missing_middle_name'
            elif 'á' in photo or 'é' in photo or 'í' in photo or 'ó' in photo or 'ú' in photo or 'ñ' in photo:
                # Character encoding issue
                issue_categories['character_encoding'] += 1
                issue_type = 'encoding'
            elif ref.split()[0] == ref.split()[0] and (ref.split()[0] + ' ' + ref.split()[0]) in photo:
                # Duplicate name (Portugal)
                issue_categories['duplicate_names'] += 1
                issue_type = 'duplicate'
            else:
                issue_categories['other'] += 1
                issue_type = 'other'

            team_stats['mismatches'].append({
                'external_id': row['External_ID'],
                'reference': ref,
                'photo': photo,
                'issue_type': issue_type
            })
        elif status == 'missing':
            team_stats['missing'] += 1
            stats['total_unmatched'] += 1
        elif status == 'extra':
            team_stats['extra'] += 1
            stats['total_extra_photos'] += 1

    # Add last team
    if team_stats:
        stats['teams'][current_team] = team_stats

# Calculate overall accuracy
overall_accuracy = (stats['total_correct'] / stats['total_players'] * 100) if stats['total_players'] > 0 else 0

print("\n" + "="*60)
print("VALIDATION STATISTICS")
print("="*60)
print(f"\nTotal Teams: {stats['total_teams']}")
print(f"Total Players: {stats['total_players']}")
print(f"Correct Matches: {stats['total_correct']} ({overall_accuracy:.2f}%)")
print(f"Incorrect Matches: {stats['total_incorrect']}")
print(f"Unmatched Players: {stats['total_unmatched']}")
print(f"Extra Photos: {stats['total_extra_photos']}")

print("\n" + "-"*60)
print("ISSUE CATEGORIES")
print("-"*60)
total_issues = stats['total_incorrect']
for category, count in sorted(issue_categories.items(), key=lambda x: x[1], reverse=True):
    percentage = (count / total_issues * 100) if total_issues > 0 else 0
    print(f"  {category.replace('_', ' ').title()}: {count} ({percentage:.1f}%)")

print("\n" + "-"*60)
print("TEAMS BY ACCURACY (Ranking)")
print("-"*60)
ranked_teams = sorted(
    stats['teams'].items(),
    key=lambda x: (x[1]['correct'] / x[1]['total'] * 100) if x[1]['total'] > 0 else 0,
    reverse=True
)

for rank, (team, team_stats) in enumerate(ranked_teams, 1):
    accuracy = (team_stats['correct'] / team_stats['total'] * 100) if team_stats['total'] > 0 else 0
    medal = "🥇" if rank == 1 else "🥈" if rank == 2 else "🥉" if rank == 3 else "  "
    print(f"  {medal} #{rank:2d} {team:15s} {accuracy:5.1f}% ({team_stats['correct']:2d}/{team_stats['total']:2d})")

print("\n" + "-"*60)
print("TEAMS REQUIRING IMMEDIATE ATTENTION")
print("-"*60)
problem_teams = [
    (team, team_stats)
    for team, team_stats in stats['teams'].items()
    if team_stats['incorrect'] / team_stats['total'] > 0.3  # More than 30% errors
]

for team, team_stats in sorted(problem_teams, key=lambda x: x[1]['incorrect'] / x[1]['total'], reverse=True):
    error_rate = (team_stats['incorrect'] / team_stats['total'] * 100)
    print(f"  ⚠️  {team:15s} {error_rate:5.1f}% error rate ({team_stats['incorrect']}/{team_stats['total']})")

print("\n" + "="*60)

# Save JSON stats
output_file = validation_dir / "validation_stats.json"
with open(output_file, 'w', encoding='utf-8') as f:
    json.dump(stats, f, indent=2, ensure_ascii=False)

print(f"\n✓ Statistics saved to: {output_file}")
print("="*60)
