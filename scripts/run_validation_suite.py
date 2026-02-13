#!/usr/bin/env python3
"""
Run external validation presets (all or one) with overwrite semantics.

Each selected run:
- uses the external validation config template as base,
- overrides output folder and face-detection model for the preset,
- deletes the target output folder if it already exists,
- executes scripts/run_external_validation.py.

At the end, prints the paths to reports and generated outputs for manual review.
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List


@dataclass(frozen=True)
class RunPreset:
    key: str
    folder: str
    face_detection: str
    description: str


PRESETS: Dict[str, RunPreset] = {
    "run": RunPreset(
        key="run",
        folder="Validation_Run",
        face_detection="llava:7b,qwen3-vl",
        description="Default external validation run (llava with qwen fallback).",
    ),
    "opencv": RunPreset(
        key="opencv",
        folder="Validation_Run_opencv",
        face_detection="opencv-dnn",
        description="OpenCV DNN face detection.",
    ),
    "llava": RunPreset(
        key="llava",
        folder="Validation_Run_llava",
        face_detection="llava:7b,qwen3-vl",
        description="Ollama vision face detection (llava + qwen fallback).",
    ),
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run external validation suite presets.")
    parser.add_argument(
        "--run",
        default="all",
        help="Preset key or comma-separated keys. Available: all, run, opencv, llava",
    )
    parser.add_argument(
        "--config-template",
        default="samples/external_validation.config.template.json",
        help="Base config template path.",
    )
    parser.add_argument(
        "--skip-compare",
        action="store_true",
        help="Do not generate cross-run comparison report after execution.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Show planned actions without running validations.",
    )
    return parser.parse_args()


def resolve_selected_runs(run_arg: str) -> List[RunPreset]:
    if run_arg.strip().lower() == "all":
        return [PRESETS["run"], PRESETS["opencv"], PRESETS["llava"]]

    keys = [part.strip() for part in run_arg.split(",") if part.strip()]
    invalid = [k for k in keys if k not in PRESETS]
    if invalid:
        raise ValueError(f"Invalid run key(s): {', '.join(invalid)}")
    return [PRESETS[k] for k in keys]


def load_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as f:
        return json.load(f)


def run_validation(repo_root: Path, run_external_validation_path: Path, config: dict) -> None:
    with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False, encoding="utf-8") as tmp:
        json.dump(config, tmp, indent=2)
        tmp_path = Path(tmp.name)

    try:
        cmd = [sys.executable, str(run_external_validation_path), "--config", str(tmp_path)]
        completed = subprocess.run(cmd, cwd=str(repo_root), check=False)
        if completed.returncode != 0:
            raise RuntimeError(f"Validation command failed with exit code {completed.returncode}")
    finally:
        if tmp_path.exists():
            tmp_path.unlink()


def main() -> int:
    args = parse_args()
    repo_root = Path(__file__).resolve().parents[1]
    template_path = (repo_root / args.config_template).resolve()
    if not template_path.exists():
        print(f"ERROR: Config template not found: {template_path}", file=sys.stderr)
        return 1

    run_external_validation_path = (repo_root / "scripts" / "run_external_validation.py").resolve()
    compare_script_path = (repo_root / "scripts" / "compare_validation_runs.py").resolve()
    if not run_external_validation_path.exists():
        print(f"ERROR: Missing script: {run_external_validation_path}", file=sys.stderr)
        return 1

    base_config = load_json(template_path)
    selected = resolve_selected_runs(args.run)

    # Determine external validation base dir from template outputRoot parent.
    template_output_root = Path(str(base_config["outputRoot"])).expanduser().resolve()
    realdata_base_dir = template_output_root.parent

    print("Validation Suite")
    print("================")
    print(f"Repo root: {repo_root}")
    print(f"Template: {template_path}")
    print(f"Base external dir: {realdata_base_dir}")
    print("")
    print("Selected runs:")
    for preset in selected:
        print(f"- {preset.key}: {preset.description}")
    print("")

    planned_outputs = []
    for preset in selected:
        output_root = realdata_base_dir / preset.folder
        planned_outputs.append(output_root)
        print(f"[plan] {preset.key}")
        print(f"  outputRoot     : {output_root}")
        print(f"  faceDetection  : {preset.face_detection}")
        print(f"  overwrite      : {'yes' if output_root.exists() else 'n/a (new folder)'}")

    if args.dry_run:
        return 0

    for preset, output_root in zip(selected, planned_outputs):
        if output_root.exists():
            shutil.rmtree(output_root)

        config = json.loads(json.dumps(base_config))
        config["outputRoot"] = str(output_root)
        config.setdefault("generate", {})
        config["generate"]["faceDetectionModel"] = preset.face_detection

        print("")
        print(f"[run] {preset.key} -> {output_root}")
        run_validation(repo_root, run_external_validation_path, config)

    compare_report_path = realdata_base_dir / "validation_runs_comparison.md"
    if not args.skip_compare and compare_script_path.exists():
        print("")
        print("[run] compare-validation-runs")
        completed = subprocess.run([sys.executable, str(compare_script_path)], cwd=str(repo_root), check=False)
        if completed.returncode != 0:
            print(
                f"WARNING: Comparison report generation failed (exit code {completed.returncode})",
                file=sys.stderr,
            )

    print("")
    print("Review Paths")
    print("============")
    for preset, output_root in zip(selected, planned_outputs):
        print(f"- {preset.key}")
        print(f"  root      : {output_root}")
        print(f"  report    : {output_root / 'validation_report.md'}")
        print(f"  Spain     : {output_root / 'Spain' / 'Generated'}")
        print(f"  Switzerland: {output_root / 'Switzerland' / 'Generated'}")
    if compare_report_path.exists():
        print(f"- comparison: {compare_report_path}")

    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ValueError as ex:
        print(f"ERROR: {ex}", file=sys.stderr)
        raise SystemExit(1)
    except Exception as ex:
        print(f"ERROR: {ex}", file=sys.stderr)
        raise SystemExit(1)
