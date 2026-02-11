#!/usr/bin/env python3
"""
Generate synthetic player CSV from FIFA photos for PhotoMapperAI testing.
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
    """
    # Remove extension
    name = filename.replace('.jpg', '').replace('.jpeg', '').replace('.png', '')

    # Split by underscores
    parts = name.split('_')

    if len(parts) >= 3:
        # Pattern: FirstName_MiddleName_LastName_ID or FirstName_LastName_ID
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
            'FirstName': first_name,
            'LastName': last_name
        }

    return None

def generate_internal_id():
    """Generate random internal ID (5-7 digits, like real data)"""
    # Real internal IDs from expected outputs: 1039537, 128490, 55041, 63533, 74436
    # Mix of 5-7 digits, not sequential
    length = random.choice([5, 6, 7])
    return str(random.randint(10000, 9999999))[:length]

def main():
    # Photo directories
    spain_dir = Path('/Users/luis/Repos/FakeData_PhotoMapperAI/NewDataExample/Spain')
    switzerland_dir = Path('/Users/luis/Repos/FakeData_PhotoMapperAI/NewDataExample/Switzerland')

    players = []

    # Process Spain team
    for photo_file in spain_dir.glob('*.jpg'):
        player_data = parse_filename(photo_file.name)
        if player_data:
            player_data['Team'] = 'Spain'
            player_data['InternalId'] = generate_internal_id()
            players.append(player_data)

    # Process Switzerland team
    for photo_file in switzerland_dir.glob('*.jpg'):
        player_data = parse_filename(photo_file.name)
        if player_data:
            player_data['Team'] = 'Switzerland'
            player_data['InternalId'] = generate_internal_id()
            players.append(player_data)

    # Sort by team and name
    players.sort(key=lambda p: (p['Team'], p['FullName']))

    # Write CSV (matches extract command output format)
    output_file = Path('/Users/luis/Repos/FakeData_PhotoMapperAI/NewDataExample/players_test.csv')

    with open(output_file, 'w', newline='', encoding='utf-8') as f:
        writer = csv.DictWriter(f, fieldnames=[
            'InternalId', 'ExternalId', 'FullName', 'FirstName', 'LastName', 'Team'
        ])
        writer.writeheader()
        writer.writerows(players)

    print(f"Generated {len(players)} player records")
    print(f"Output: {output_file}")
    print(f"\nBreakdown:")
    print(f"  Spain: {sum(1 for p in players if p['Team'] == 'Spain')}")
    print(f"  Switzerland: {sum(1 for p in players if p['Team'] == 'Switzerland')}")

    # Show sample records
    print(f"\nSample records:")
    for i, player in enumerate(players[:3]):
        print(f"  {i+1}. {player['FullName']} (Ext: {player['ExternalId']}, Int: {player['InternalId']})")

if __name__ == '__main__':
    main()
