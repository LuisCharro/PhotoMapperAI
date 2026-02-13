#!/usr/bin/env python3
"""
Run anonymized CLI validations for PhotoMapperAI without private/real data.

Covers:
- extract command (synthetic fallback path)
- map command (deterministic pass, no AI required)
- optional AI-required map scenario (forces unresolved rows into AI fallback)
"""

from __future__ import annotations

import csv
import re
import subprocess
import sys
import argparse
from pathlib import Path


def run(cmd: list[str], cwd: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        cmd,
        cwd=str(cwd),
        text=True,
        capture_output=True,
        check=False,
    )


def assert_ok(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def validate_extract(repo_root: Path) -> None:
    print("[extract] running anonymized extract validation...")
    sql_path = repo_root / "tests" / "Data" / "Anonymized" / "Extract" / "query.sql"
    conn_path = repo_root / "tests" / "Data" / "Anonymized" / "Extract" / "connection_string.txt"
    output_name = "anonymized_extract_output.csv"
    output_path = repo_root / output_name

    if output_path.exists():
        output_path.unlink()

    cmd = [
        "dotnet",
        "run",
        "--project",
        str(repo_root / "src" / "PhotoMapperAI" / "PhotoMapperAI.csproj"),
        "--",
        "extract",
        "-i",
        str(sql_path),
        "-c",
        str(conn_path),
        "-t",
        "1",
        "-o",
        output_name,
    ]
    proc = run(cmd, cwd=repo_root)
    assert_ok(proc.returncode == 0, f"extract failed:\n{proc.stdout}\n{proc.stderr}")
    assert_ok(output_path.exists(), "extract output CSV not found")

    with output_path.open("r", encoding="utf-8", newline="") as f:
        rows = list(csv.DictReader(f))

    assert_ok(len(rows) == 3, f"extract expected 3 rows, got {len(rows)}")
    names = {(r.get("FamilyName", "").strip(), r.get("SurName", "").strip()) for r in rows}
    expected = {
        ("Rodríguez", "Sánchez"),
        ("Ramos", "Sergio"),
        ("Iniesta", "Andrés"),
    }
    assert_ok(names == expected, f"extract name set mismatch: got={names}")

    print("[extract] ok")


def validate_map(repo_root: Path) -> None:
    print("[map] running anonymized map validation...")
    input_csv = repo_root / "tests" / "Data" / "Anonymized" / "Map" / "input_unmapped.csv"
    photos_dir = repo_root / "tests" / "Data" / "Anonymized" / "Map" / "photos"
    expected_csv = repo_root / "tests" / "Data" / "Anonymized" / "Map" / "expected_external_ids.csv"
    output_csv = repo_root / "mapped_input_unmapped.csv"

    if output_csv.exists():
        output_csv.unlink()

    cmd = [
        "dotnet",
        "run",
        "--project",
        str(repo_root / "src" / "PhotoMapperAI" / "PhotoMapperAI.csproj"),
        "--",
        "map",
        "-i",
        str(input_csv),
        "-p",
        str(photos_dir),
        "-t",
        "0.8",
    ]
    proc = run(cmd, cwd=repo_root)
    assert_ok(proc.returncode >= 0, f"map execution failed:\n{proc.stdout}\n{proc.stderr}")
    assert_ok(output_csv.exists(), "map output CSV not found")

    with expected_csv.open("r", encoding="utf-8", newline="") as f:
        expected = {row["PlayerId"]: (row.get("ExpectedExternalId", "") or "").strip() for row in csv.DictReader(f)}

    with output_csv.open("r", encoding="utf-8", newline="") as f:
        actual = {row["PlayerId"]: (row.get("ExternalId", "") or "").strip() for row in csv.DictReader(f)}

    missing_ids = sorted(pid for pid in expected if pid not in actual)
    assert_ok(not missing_ids, f"map output missing PlayerId rows: {missing_ids}")

    mismatches: list[str] = []
    for pid, expected_ext in expected.items():
        actual_ext = actual.get(pid, "")
        if actual_ext != expected_ext:
            mismatches.append(f"{pid}: expected='{expected_ext}' actual='{actual_ext}'")

    assert_ok(not mismatches, "map ExternalId mismatches:\n" + "\n".join(mismatches))
    print("[map] ok")


def validate_map_ai_required(repo_root: Path, ai_model: str) -> None:
    print(f"[map-ai] running anonymized AI-required map validation with model '{ai_model}'...")
    input_csv = repo_root / "tests" / "Data" / "Anonymized" / "MapAIRequired" / "input_ai_required.csv"
    photos_dir = repo_root / "tests" / "Data" / "Anonymized" / "MapAIRequired" / "photos"
    output_csv = repo_root / "mapped_input_ai_required.csv"

    if output_csv.exists():
        output_csv.unlink()

    cmd = [
        "dotnet",
        "run",
        "--project",
        str(repo_root / "src" / "PhotoMapperAI" / "PhotoMapperAI.csproj"),
        "--",
        "map",
        "-i",
        str(input_csv),
        "-p",
        str(photos_dir),
        "-n",
        ai_model,
        "-t",
        "0.8",
        "-a",
        "-ap",
    ]
    proc = run(cmd, cwd=repo_root)
    assert_ok(proc.returncode >= 0, f"map-ai execution failed:\n{proc.stdout}\n{proc.stderr}")
    assert_ok(output_csv.exists(), "map-ai output CSV not found")

    output_text = f"{proc.stdout}\n{proc.stderr}"
    ai_eval_match = re.search(r"AI evaluated:\s*(\d+)\s+players", output_text)
    assert_ok(ai_eval_match is not None, f"map-ai output missing AI evaluated summary:\n{output_text}")

    ai_evaluated = int(ai_eval_match.group(1))
    assert_ok(ai_evaluated > 0, f"map-ai expected AI evaluated > 0, got {ai_evaluated}")

    with input_csv.open("r", encoding="utf-8", newline="") as f:
        input_rows = list(csv.DictReader(f))
    with output_csv.open("r", encoding="utf-8", newline="") as f:
        output_rows = list(csv.DictReader(f))

    assert_ok(
        len(output_rows) == len(input_rows),
        f"map-ai row count mismatch: expected {len(input_rows)}, got {len(output_rows)}",
    )
    print(f"[map-ai] ok (AI evaluated: {ai_evaluated} players)")


def main() -> int:
    parser = argparse.ArgumentParser(description="Run anonymized CLI validations.")
    parser.add_argument(
        "--include-ai-map",
        action="store_true",
        help="Run optional AI-required map validation fixture.",
    )
    parser.add_argument(
        "--ai-model",
        default="qwen2.5:7b",
        help="Model identifier for --include-ai-map (default: qwen2.5:7b).",
    )
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parents[1]
    try:
        validate_extract(repo_root)
        validate_map(repo_root)
        if args.include_ai_map:
            validate_map_ai_required(repo_root, args.ai_model)
    except AssertionError as ex:
        print(f"[FAIL] {ex}")
        return 1
    except Exception as ex:  # pragma: no cover
        print(f"[ERROR] {ex}")
        return 2

    print("[PASS] anonymized CLI validations completed successfully.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
