#!/usr/bin/env python3
"""
Run end-to-end validation using external real-data folders (not committed to repo).

Workflow per team:
1. Prepare team CSV from a source players CSV (or synthesize one from photo filenames).
2. Run `map`.
3. Run `generatephotos`.
4. Compare generated filenames with expected portraits.
5. Write markdown summary report.
"""

from __future__ import annotations

import argparse
import csv
import json
import os
import shutil
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Optional, Tuple


@dataclass
class TeamConfig:
    name: str
    team_id: Optional[int]
    input_photos_dir: Path
    expected_portraits_dir: Path


@dataclass
class TeamResult:
    name: str
    input_count: int
    expected_count: int
    generated_count: int
    missing_expected: List[str]
    unexpected_generated: List[str]
    map_csv: Path
    generated_dir: Path


def _run(cmd: List[str], cwd: Path, allow_nonzero: bool = False) -> int:
    process = subprocess.run(cmd, cwd=str(cwd), check=False)
    if process.returncode != 0 and not allow_nonzero:
        raise RuntimeError(f"Command failed ({process.returncode}): {' '.join(cmd)}")
    return process.returncode


def _read_players_csv(path: Path) -> List[Dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


def _write_players_csv(path: Path, rows: List[Dict[str, str]]) -> None:
    fieldnames = [
        "PlayerId",
        "TeamId",
        "FamilyName",
        "SurName",
        "ExternalId",
        "ValidMapping",
        "Confidence",
        "FullName",
    ]
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            writer.writerow({k: row.get(k, "") for k in fieldnames})


def _is_image(path: Path) -> bool:
    return path.suffix.lower() in {".jpg", ".jpeg", ".png", ".bmp", ".webp"}


def _parse_photo_filename(path: Path) -> Tuple[str, str, str]:
    """
    Expected example: Claudia_Pina_250101503.jpg
    Returns: family_name, sur_name, external_id
    """
    stem = path.stem
    parts = stem.split("_")
    if len(parts) < 3:
        raise ValueError(f"Unsupported filename pattern: {path.name}")

    external_id = parts[-1]
    names = parts[:-1]
    # Keep simple deterministic split:
    # family_name = all but last token, sur_name = last token.
    if len(names) == 1:
        family_name = names[0]
        sur_name = ""
    else:
        family_name = " ".join(names[:-1])
        sur_name = names[-1]

    return family_name, sur_name, external_id


def _synthesize_players_from_photos(team_name: str, team_id: int, photos_dir: Path) -> List[Dict[str, str]]:
    rows: List[Dict[str, str]] = []
    for idx, photo in enumerate(sorted(photos_dir.iterdir()), start=1):
        if not photo.is_file() or not _is_image(photo):
            continue
        try:
            family_name, sur_name, external_id = _parse_photo_filename(photo)
        except ValueError:
            continue
        rows.append(
            {
                "PlayerId": str(900000000 + idx),
                "TeamId": str(team_id),
                "FamilyName": family_name,
                "SurName": sur_name,
                "ExternalId": external_id,
                "ValidMapping": "1",
                "Confidence": "1.0",
                "FullName": f"{family_name} {sur_name}".strip(),
            }
        )

    if not rows:
        raise RuntimeError(f"No parsable photos found for team '{team_name}' in {photos_dir}")
    return rows


def _prepare_team_csv(
    source_csv_path: Optional[Path], team: TeamConfig, workspace_dir: Path
) -> Path:
    team_csv = workspace_dir / f"{team.name.lower()}_players.csv"
    if source_csv_path and source_csv_path.exists() and team.team_id is not None:
        rows = _read_players_csv(source_csv_path)
        filtered = [r for r in rows if r.get("TeamId") == str(team.team_id)]
        if not filtered:
            raise RuntimeError(
                f"No players found in source CSV for team '{team.name}' with TeamId={team.team_id}"
            )
        _write_players_csv(team_csv, filtered)
        return team_csv

    synth_team_id = team.team_id if team.team_id is not None else 0
    rows = _synthesize_players_from_photos(team.name, synth_team_id, team.input_photos_dir)
    _write_players_csv(team_csv, rows)
    return team_csv


def _list_generated_ids(path: Path) -> List[str]:
    if not path.exists():
        return []
    ids: List[str] = []
    for file in sorted(path.iterdir()):
        if file.is_file() and _is_image(file):
            ids.append(file.stem)
    return ids


def _load_config(path: Path) -> Dict[str, object]:
    with path.open("r", encoding="utf-8") as f:
        return json.load(f)


def main() -> int:
    parser = argparse.ArgumentParser(description="Run external real-data validation.")
    parser.add_argument(
        "--config",
        required=True,
        help="Path to validation config JSON",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Only validate config and print planned actions.",
    )
    args = parser.parse_args()

    config_path = Path(args.config).expanduser().resolve()
    config = _load_config(config_path)

    repo_root = Path(config.get("repoRoot", ".")).expanduser().resolve()
    source_csv = config.get("sourcePlayersCsvPath")
    source_csv_path = Path(source_csv).expanduser().resolve() if source_csv else None
    output_root = Path(config.get("outputRoot", str(repo_root / ".external-validation"))).expanduser().resolve()

    map_cfg = config.get("map", {})
    generate_cfg = config.get("generate", {})

    teams: List[TeamConfig] = []
    for raw in config.get("teams", []):
        teams.append(
            TeamConfig(
                name=raw["name"],
                team_id=raw.get("teamId"),
                input_photos_dir=Path(raw["inputPhotosDir"]).expanduser().resolve(),
                expected_portraits_dir=Path(raw["expectedPortraitsDir"]).expanduser().resolve(),
            )
        )

    if not teams:
        raise RuntimeError("Config must include at least one team.")

    if args.dry_run:
        print(f"Repo root: {repo_root}")
        print(f"Source players CSV: {source_csv_path}")
        print(f"Output root: {output_root}")
        for t in teams:
            print(f"- Team {t.name}:")
            print(f"  photos   : {t.input_photos_dir}")
            print(f"  expected : {t.expected_portraits_dir}")
            print(f"  teamId   : {t.team_id}")
        return 0

    output_root.mkdir(parents=True, exist_ok=True)
    results: List[TeamResult] = []

    for team in teams:
        team_workspace = output_root / team.name
        if team_workspace.exists():
            shutil.rmtree(team_workspace)
        team_workspace.mkdir(parents=True, exist_ok=True)

        team_csv = _prepare_team_csv(source_csv_path, team, team_workspace)

        # Run map command from team workspace, so mapped_* output lands there.
        map_cmd = [
            "dotnet",
            "run",
            "--project",
            str(repo_root / "src/PhotoMapperAI/PhotoMapperAI.csproj"),
            "--",
            "map",
            "--inputCsvPath",
            str(team_csv),
            "--photosDir",
            str(team.input_photos_dir),
            "--nameModel",
            str(map_cfg.get("nameModel", "ollama:qwen2.5:7b")),
            "--confidenceThreshold",
            str(map_cfg.get("confidenceThreshold", 0.9)),
        ]
        # map command currently returns matched-count as exit code, so non-zero can still mean success.
        _run(map_cmd, cwd=team_workspace, allow_nonzero=True)

        mapped_csv = team_workspace / f"mapped_{team_csv.name}"
        if not mapped_csv.exists():
            raise RuntimeError(f"Mapped CSV not found: {mapped_csv}")

        generated_out = team_workspace / "Generated"
        generated_out.mkdir(parents=True, exist_ok=True)

        generate_cmd = [
            "dotnet",
            "run",
            "--project",
            str(repo_root / "src/PhotoMapperAI/PhotoMapperAI.csproj"),
            "--",
            "generatephotos",
            "--inputCsvPath",
            str(mapped_csv),
            "--photosDir",
            str(team.input_photos_dir),
            "--processedPhotosOutputPath",
            str(generated_out),
            "--format",
            str(generate_cfg.get("format", "jpg")),
            "--faceDetection",
            str(generate_cfg.get("faceDetectionModel", "llava:7b,qwen3-vl")),
        ]
        if bool(generate_cfg.get("portraitOnly", False)):
            generate_cmd.append("--portraitOnly")
        _run(generate_cmd, cwd=team_workspace)

        expected_ids = _list_generated_ids(team.expected_portraits_dir)
        generated_ids = _list_generated_ids(generated_out)
        expected_set = set(expected_ids)
        generated_set = set(generated_ids)

        results.append(
            TeamResult(
                name=team.name,
                input_count=len([p for p in team.input_photos_dir.iterdir() if p.is_file() and _is_image(p)]),
                expected_count=len(expected_ids),
                generated_count=len(generated_ids),
                missing_expected=sorted(expected_set - generated_set),
                unexpected_generated=sorted(generated_set - expected_set),
                map_csv=mapped_csv,
                generated_dir=generated_out,
            )
        )

    report_path = output_root / "validation_report.md"
    with report_path.open("w", encoding="utf-8") as f:
        f.write("# External Validation Report\n\n")
        for r in results:
            f.write(f"## {r.name}\n\n")
            f.write(f"- Input photos: {r.input_count}\n")
            f.write(f"- Expected portraits: {r.expected_count}\n")
            f.write(f"- Generated portraits: {r.generated_count}\n")
            f.write(f"- Mapped CSV: `{r.map_csv}`\n")
            f.write(f"- Generated dir: `{r.generated_dir}`\n")
            f.write(f"- Missing expected IDs: {len(r.missing_expected)}\n")
            f.write(f"- Unexpected generated IDs: {len(r.unexpected_generated)}\n")

            if r.missing_expected:
                f.write("\nMissing expected IDs:\n")
                for item in r.missing_expected[:50]:
                    f.write(f"- `{item}`\n")
            if r.unexpected_generated:
                f.write("\nUnexpected generated IDs:\n")
                for item in r.unexpected_generated[:50]:
                    f.write(f"- `{item}`\n")
            f.write("\n")

    print(f"Validation report written to: {report_path}")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as ex:
        print(f"ERROR: {ex}", file=sys.stderr)
        sys.exit(1)
