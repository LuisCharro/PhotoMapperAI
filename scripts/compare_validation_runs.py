#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
from pathlib import Path
from typing import Dict, List


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def list_generated(run_dir: Path, team: str) -> Dict[str, Path]:
    d = run_dir / team / "Generated"
    if not d.exists():
        return {}
    return {p.stem: p for p in d.glob("*.jpg")}


def build_report(base_dir: Path) -> str:
    canonical_order = ["Validation_Run", "Validation_Run_opencv", "Validation_Run_llava"]
    runs = [base_dir / name for name in canonical_order if (base_dir / name).is_dir()]
    if not runs:
        runs = sorted([p for p in base_dir.glob("Validation_Run*") if p.is_dir()], key=lambda p: p.name)
    teams = ["Spain", "Switzerland"]
    lines: List[str] = []
    lines.append("# Validation Runs Comparison Report")
    lines.append("")
    lines.append(f"Base directory: `{base_dir}`")
    lines.append("")
    lines.append("## Runs")
    for run in runs:
        lines.append(f"- `{run.name}`")
    lines.append("")

    for team in teams:
        lines.append(f"## {team}")
        lines.append("")
        counts = []
        generated = {run.name: list_generated(run, team) for run in runs}
        for run in runs:
            counts.append((run.name, len(generated[run.name])))
        lines.append("| Run | Generated Count |")
        lines.append("|---|---:|")
        for run_name, count in counts:
            lines.append(f"| `{run_name}` | {count} |")
        lines.append("")

        lines.append("### Pairwise Hash Comparison")
        lines.append("")
        lines.append("| Run A | Run B | Common IDs | Same Hash | Different Hash |")
        lines.append("|---|---|---:|---:|---:|")
        for i in range(len(runs)):
            for j in range(i + 1, len(runs)):
                a = runs[i].name
                b = runs[j].name
                a_ids = set(generated[a].keys())
                b_ids = set(generated[b].keys())
                common = sorted(a_ids & b_ids)
                same = 0
                for pid in common:
                    if sha256(generated[a][pid]) == sha256(generated[b][pid]):
                        same += 1
                diff = len(common) - same
                lines.append(f"| `{a}` | `{b}` | {len(common)} | {same} | {diff} |")
        lines.append("")

    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--base-dir",
        default="/Users/luis/Repos/PhotoMapperAI_ExternalData/RealDataValidation",
    )
    parser.add_argument(
        "--output",
        default="/Users/luis/Repos/PhotoMapperAI_ExternalData/RealDataValidation/validation_runs_comparison.md",
    )
    args = parser.parse_args()

    base_dir = Path(args.base_dir).expanduser().resolve()
    output = Path(args.output).expanduser().resolve()
    report = build_report(base_dir)
    output.write_text(report, encoding="utf-8")
    print(f"Report written to: {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
