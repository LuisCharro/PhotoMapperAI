#!/usr/bin/env python3
"""
PhotoMapperAI Validation Script
Validates name matching accuracy across 17 teams using real data.
"""
import csv
import json
import os
import subprocess
import sys
from pathlib import Path
from typing import Dict, List, Tuple
from dataclasses import dataclass, asdict


@dataclass
class ValidationResult:
    """Results for a single team validation."""
    team: str
    total_players: int
    correct_matches: int
    incorrect_matches: int
    unmatched_players: int
    accuracy: float
    results: List[Dict]


class PhotoMapperValidator:
    """Validates PhotoMapperAI mapping accuracy."""

    def __init__(self, base_dir: str, photomapper_ai_dir: str):
        self.base_dir = Path(base_dir)
        self.photomapper_ai_dir = Path(photomapper_ai_dir)
        self.teams = [
            "Belgium", "Denmark", "England", "Finland", "France",
            "Germany", "Iceland", "Italy", "Netherlands", "Norway",
            "Poland", "Portugal", "Spain", "Sweden", "Switzerland", "Wales"
        ]
        self.all_results = []

    def load_reference_mapping(self, team: str) -> Dict[str, str]:
        """
        Load the reference mapping from 03_transformed_with_map_data.
        Returns dict: {ExternalId: FullName}
        """
        ref_file = self.base_dir / "DataPrep/03_transformed_with_map_data" / f"{team}.csv"
        mapping = {}

        with open(ref_file, 'r', encoding='utf-8') as f:
            reader = csv.DictReader(f)
            for row in reader:
                if row['ExternalId'] and row['ExternalId'] != '0':
                    mapping[row['ExternalId']] = row['FullName']

        return mapping

    def load_photo_mapping(self, photos_dir: Path) -> Dict[str, str]:
        """
        Parse photo filenames to extract {ExternalId: FullName}.
        Photo format: "FirstName_LastName_ExternalId.jpg"
        """
        mapping = {}

        for photo_file in sorted(photos_dir.glob("*.jpg")):
            # Remove extension and split by underscore
            parts = photo_file.stem.rsplit('_', 1)

            if len(parts) == 2:
                # Try to parse the last part as ExternalId
                potential_id = parts[1]
                if potential_id.isdigit():
                    full_name = parts[0].replace('_', ' ')
                    mapping[potential_id] = full_name

        return mapping

    def normalize_name(self, name: str) -> str:
        """Normalize name for comparison."""
        if not name:
            return ""

        # Remove accents, convert to lowercase, strip
        import unicodedata
        normalized = unicodedata.normalize('NFKD', name)
        result = ''.join([c for c in normalized if not unicodedata.combining(c)])
        result = result.lower().strip()
        result = ' '.join(result.split())  # Normalize whitespace

        return result

    def compare_names(self, name1: str, name2: str) -> bool:
        """Compare two names after normalization."""
        norm1 = self.normalize_name(name1)
        norm2 = self.normalize_name(name2)
        return norm1 == norm2

    def validate_team(self, team: str) -> ValidationResult:
        """Validate mapping for a single team."""
        print(f"\n{'='*60}")
        print(f"Validating team: {team}")
        print(f"{'='*60}")

        # Load reference mapping (ground truth)
        ref_mapping = self.load_reference_mapping(team)
        print(f"Reference players: {len(ref_mapping)}")

        # Load photo mapping
        photos_dir = self.base_dir / "inputs" / team
        photo_mapping = self.load_photo_mapping(photos_dir)
        print(f"Photo files found: {len(photo_mapping)}")

        # Cross-validate: Check if reference and photo mapping match
        results = []
        correct = 0
        incorrect = 0
        unmatched = 0

        for external_id, ref_name in ref_mapping.items():
            if external_id in photo_mapping:
                photo_name = photo_mapping[external_id]

                if self.compare_names(ref_name, photo_name):
                    status = "✓ CORRECT"
                    correct += 1
                else:
                    status = f"✗ MISMATCH: Ref='{ref_name}' vs Photo='{photo_name}'"
                    incorrect += 1

                results.append({
                    "external_id": external_id,
                    "reference_name": ref_name,
                    "photo_name": photo_mapping.get(external_id, ""),
                    "status": "correct" if self.compare_names(ref_name, photo_name) else "incorrect"
                })
            else:
                status = f"? MISSING: No photo for {ref_name}"
                unmatched += 1
                results.append({
                    "external_id": external_id,
                    "reference_name": ref_name,
                    "photo_name": "",
                    "status": "missing"
                })

            print(f"  {external_id}: {ref_name} -> {status}")

        # Check for extra photos (not in reference)
        ref_ids = set(ref_mapping.keys())
        photo_ids = set(photo_mapping.keys())
        extra_photos = photo_ids - ref_ids

        if extra_photos:
            print(f"\n  Extra photos (not in reference): {len(extra_photos)}")
            for ext_id in sorted(extra_photos):
                print(f"    {ext_id}: {photo_mapping[ext_id]}")
                results.append({
                    "external_id": ext_id,
                    "reference_name": "",
                    "photo_name": photo_mapping[ext_id],
                    "status": "extra"
                })

        total = len(ref_mapping)
        accuracy = (correct / total * 100) if total > 0 else 0

        print(f"\nResults for {team}:")
        print(f"  Total reference players: {total}")
        print(f"  Correct matches: {correct}")
        print(f"  Incorrect matches: {incorrect}")
        print(f"  Unmatched players: {unmatched}")
        print(f"  Extra photos: {len(extra_photos)}")
        print(f"  Accuracy: {accuracy:.2f}%")

        return ValidationResult(
            team=team,
            total_players=total,
            correct_matches=correct,
            incorrect_matches=incorrect,
            unmatched_players=unmatched,
            accuracy=accuracy,
            results=results
        )

    def run_validation(self, output_dir: Path):
        """Run validation for all teams."""
        print("\n" + "="*60)
        print("PhotoMapperAI Validation - Cross-Reference Check")
        print("="*60)
        print(f"\nBase directory: {self.base_dir}")
        print(f"PhotoMapperAI directory: {self.photomapper_ai_dir}")
        print(f"Output directory: {output_dir}")

        output_dir.mkdir(parents=True, exist_ok=True)

        # Validate each team
        for team in self.teams:
            result = self.validate_team(team)
            self.all_results.append(result)

        # Generate summary report
        self.generate_summary_report(output_dir / "validation_summary.md")

        # Generate detailed CSV reports
        for result in self.all_results:
            self.generate_team_csv(result, output_dir)

        # Generate aggregate CSV
        self.generate_aggregate_csv(output_dir / "validation_aggregate.csv")

    def generate_summary_report(self, output_file: Path):
        """Generate markdown summary report."""
        with open(output_file, 'w', encoding='utf-8') as f:
            f.write("# PhotoMapperAI Validation Report\n\n")
            f.write(f"Generated: {self.get_timestamp()}\n\n")

            f.write("## Summary\n\n")
            f.write("| Team | Total | Correct | Incorrect | Unmatched | Accuracy |\n")
            f.write("|------|-------|---------|-----------|-----------|----------|\n")

            total_players = 0
            total_correct = 0
            total_incorrect = 0
            total_unmatched = 0

            for result in self.all_results:
                f.write(f"| {result.team} | {result.total_players} | {result.correct_matches} | "
                       f"{result.incorrect_matches} | {result.unmatched_players} | {result.accuracy:.2f}% |\n")

                total_players += result.total_players
                total_correct += result.correct_matches
                total_incorrect += result.incorrect_matches
                total_unmatched += result.unmatched_players

            overall_accuracy = (total_correct / total_players * 100) if total_players > 0 else 0

            f.write(f"| **TOTAL** | **{total_players}** | **{total_correct}** | "
                   f"**{total_incorrect}** | **{total_unmatched}** | **{overall_accuracy:.2f}%** |\n\n")

            f.write("## Key Findings\n\n")

            if total_incorrect > 0:
                f.write(f"### ⚠️ Issues Found\n\n")
                f.write(f"- {total_incorrect} name mismatches across all teams\n")
                f.write("- These indicate inconsistencies between reference data and photo filenames\n\n")

            teams_with_issues = [r for r in self.all_results if r.incorrect_matches > 0]
            if teams_with_issues:
                f.write("### Teams with Name Mismatches\n\n")
                for result in teams_with_issues:
                    f.write(f"- **{result.team}**: {result.incorrect_matches} mismatches\n")

            f.write("\n## Recommendations\n\n")
            f.write("1. Review and correct name mismatches in reference data or photo filenames\n")
            f.write("2. Ensure consistent name formatting across all data sources\n")
            f.write("3. Verify External_ID accuracy in reference CSV files\n\n")

        print(f"\n✓ Summary report generated: {output_file}")

    def generate_team_csv(self, result: ValidationResult, output_dir: Path):
        """Generate detailed CSV for a single team."""
        output_file = output_dir / f"validation_{result.team}.csv"

        with open(output_file, 'w', encoding='utf-8', newline='') as f:
            writer = csv.writer(f)
            writer.writerow(['External_ID', 'Reference_Name', 'Photo_Name', 'Status'])

            for r in result.results:
                writer.writerow([
                    r['external_id'],
                    r['reference_name'],
                    r['photo_name'],
                    r['status']
                ])

        print(f"✓ Team report generated: {output_file}")

    def generate_aggregate_csv(self, output_file: Path):
        """Generate aggregate CSV with all results."""
        with open(output_file, 'w', encoding='utf-8', newline='') as f:
            writer = csv.writer(f)
            writer.writerow(['Team', 'External_ID', 'Reference_Name', 'Photo_Name', 'Status'])

            for result in self.all_results:
                for r in result.results:
                    writer.writerow([
                        result.team,
                        r['external_id'],
                        r['reference_name'],
                        r['photo_name'],
                        r['status']
                    ])

        print(f"✓ Aggregate report generated: {output_file}")

    @staticmethod
    def get_timestamp() -> str:
        """Get current timestamp as string."""
        from datetime import datetime
        return datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def main():
    """Main entry point."""
    base_dir = os.environ.get("PHOTOMAPPER_VALIDATION_BASE_DIR", "./ValidationData")
    photomapper_ai_dir = os.environ.get("PHOTOMAPPER_REPO_ROOT", ".")
    output_dir = Path(
        os.environ.get(
            "PHOTOMAPPER_VALIDATION_OUTPUT_DIR",
            str(Path(base_dir) / "Validation_Run"),
        )
    )

    print("="*60)
    print("PhotoMapperAI Validation Script")
    print("="*60)

    validator = PhotoMapperValidator(base_dir, photomapper_ai_dir)
    validator.run_validation(output_dir)

    print("\n" + "="*60)
    print("✓ Validation complete!")
    print("="*60)


if __name__ == "__main__":
    main()
