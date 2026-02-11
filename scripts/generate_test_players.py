#!/usr/bin/env python3
"""
Generate synthetic player CSV from FIFA photos for PhotoMapperAI testing.
Matches the SQL schema expected by the extract command.
"""

import os
import re
import random
import csv
from pathlib import Path

def parse_filename(filename):
    """
    Parse filename pattern: FirstName_LastName_PlayerID.jpg
    Example: Adriana_Nanclares_250178426.jpg

    Note: SQL schema uses:
      - SurName = First Name
      - FamilyName = Last Name
    """
    # Remove extension
    name = filename.replace('.jpg', '').replace('.jpeg', '').replace('.png', '')

    # Split by underscores
    parts = name.split('_')

    if len(parts) >= 3:
        # Pattern: FirstName_MiddleName_LastName-ID or FirstName_LastName-ID
        player_id = parts[-1]

        # Join all parts except the last as the name
        name_parts = parts[:-1]
        full_name = ' '.join(name_parts)

        # Try to separate first and last name
        if len(name_parts) >= 2:
            first_name = name_parts[0]
            last_name = ' '.join(name_parts[1:])
        else:
            first_name = name_parts[0]
            last_name = ''

        return {
            'ExternalId': player_id,
            'FullName': full_name,
            'SurName': first_name,  # SQL schema: SurName = First Name
            'FamilyName': last_name  # SQL schema: FamilyName = Last Name
        }

    return None

def generate_internal_id():
    """Generate random internal ID (5-7 digits, like real data)"""
    # Real internal IDs from expected outputs: 1039537, 128490, 55041, 63533, 74436
    # Mix of 5-7 digits, not sequential
    length = random.choice([5, 6, 7])
    return str(random.randint(10000, 9999999))[:length]

def generate_team_id(team_name):
    """Generate team ID for database"""
    # Assign consistent IDs per team
    team_ids = {'Spain': 7535, 'Switzerland': 7536}
    return team_ids.get(team_name, 1)

def main():
    # Photo directories
    spain_dir = Path('/Users/luis/Repos/FakeData_PhotoMapperAI/NewDataExample/Spain')
    switzerland_dir = Path('/Users/luis/Repos/FakeData_PhotoMapperAI/NewDataExample/Switzerland')

    players = []

    # Process Spain team
    for photo_file in spain_dir.glob('*.jpg'):
        player_data = parse_filename(photo_file.name)
        if player_data:
            player_data['TeamId'] = generate_team_id('Spain')
            player_data['PlayerId'] = generate_internal_id()
            players.append(player_data)

    # Process Switzerland team
    for photo_file in switzerland_dir.glob('*.jpg'):
        player_data = parse_filename(photo_file.name)
        if player_data:
            player_data['TeamId'] = generate_team_id('Switzerland')
            player_data['PlayerId'] = generate_internal_id()
            players.append(player_data)

    # Sort by team and name
    players.sort(key=lambda p: (p['TeamId'], p['FamilyName'], p['SurName']))

    # Write CSV (matches extract command output format)
    output_file = Path('/Users/luis/Repos/FakeData_PhotoMapperAI/NewDataExample/players_test.csv')

    with open(output_file, 'w', newline='', encoding='utf-8') as f:
        writer = csv.DictWriter(f, fieldnames=[
            'PlayerId', 'TeamId', 'FamilyName', 'SurName',
            'ExternalId', 'ValidMapping', 'Confidence', 'FullName'
        ])
        writer.writeheader()

        for player in players:
            # Initialize mapping fields (will be updated by map command)
            writer.writerow({
                'PlayerId': player['PlayerId'],
                'TeamId': player['TeamId'],
                'FamilyName': player['FamilyName'],
                'SurName': player['SurName'],
                'ExternalId': '',  # Empty - will be filled by map command
                'ValidMapping': 0,  # False initially
                'Confidence': 0.0,  # 0 initially
                'FullName': player['FullName']
            })

    print(f"Generated {len(players)} player records")
    print(f"Output: {output_file}")
    print(f"\nBreakdown:")
    print(f"  Spain (TeamId 7535): {sum(1 for p in players if p['TeamId'] == 7535)}")
    print(f"  Switzerland (TeamId 7536): {sum(1 for p in players if p['TeamId'] == 7536)}")

    # Show sample records
    print(f"\nSample records:")
    for i, player in enumerate(players[:3]):
        print(f"  {i+1}. {player['FullName']} (Int: {player['PlayerId']})")

if __name__ == '__main__':
    main()
