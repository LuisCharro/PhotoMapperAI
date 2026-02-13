#!/usr/bin/env python3
"""
Compare expected vs generated portrait sets by ID coverage + basic image stats.
No private data paths are hardcoded; caller provides folders.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Dict, List, Optional, Tuple


def _is_image(p: Path) -> bool:
    return p.suffix.lower() in {".jpg", ".jpeg", ".png", ".bmp", ".webp"}


def _jpeg_size(path: Path) -> Optional[Tuple[int, int]]:
    data = path.read_bytes()
    if len(data) < 4 or data[0] != 0xFF or data[1] != 0xD8:
        return None
    i = 2
    while i + 9 < len(data):
        if data[i] != 0xFF:
            i += 1
            continue
        marker = data[i + 1]
        i += 2
        if marker in (0xD8, 0xD9):
            continue
        if i + 1 >= len(data):
            return None
        seg_len = (data[i] << 8) + data[i + 1]
        if seg_len < 2 or i + seg_len > len(data):
            return None
        if marker in (0xC0, 0xC1, 0xC2, 0xC3):
            if i + 7 >= len(data):
                return None
            h = (data[i + 3] << 8) + data[i + 4]
            w = (data[i + 5] << 8) + data[i + 6]
            return (w, h)
        i += seg_len
    return None


def _png_size(path: Path) -> Optional[Tuple[int, int]]:
    data = path.read_bytes()
    sig = b"\x89PNG\r\n\x1a\n"
    if len(data) < 24 or data[:8] != sig:
        return None
    if data[12:16] != b"IHDR":
        return None
    w = int.from_bytes(data[16:20], "big")
    h = int.from_bytes(data[20:24], "big")
    return (w, h)


def _read_dimensions(path: Path) -> Optional[Tuple[int, int]]:
    ext = path.suffix.lower()
    if ext in {".jpg", ".jpeg"}:
        return _jpeg_size(path)
    if ext == ".png":
        return _png_size(path)
    return None


def _collect(dir_path: Path) -> Dict[str, Path]:
    if not dir_path.exists():
        return {}
    return {p.stem: p for p in sorted(dir_path.iterdir()) if p.is_file() and _is_image(p)}


def _avg_file_kb(paths: List[Path]) -> float:
    if not paths:
        return 0.0
    return sum(p.stat().st_size for p in paths) / len(paths) / 1024.0


def _top_dims(paths: List[Path]) -> List[Tuple[str, int]]:
    freq: Dict[str, int] = {}
    for p in paths:
        d = _read_dimensions(p)
        if d is None:
            continue
        key = f"{d[0]}x{d[1]}"
        freq[key] = freq.get(key, 0) + 1
    return sorted(freq.items(), key=lambda kv: kv[1], reverse=True)[:5]


def main() -> int:
    parser = argparse.ArgumentParser(description="Compare expected and generated portrait sets")
    parser.add_argument("--expectedDir", required=True)
    parser.add_argument("--generatedDir", required=True)
    parser.add_argument("--outputJson", required=False)
    args = parser.parse_args()

    expected_dir = Path(args.expectedDir).expanduser().resolve()
    generated_dir = Path(args.generatedDir).expanduser().resolve()

    expected = _collect(expected_dir)
    generated = _collect(generated_dir)

    expected_ids = set(expected.keys())
    generated_ids = set(generated.keys())

    missing = sorted(expected_ids - generated_ids)
    unexpected = sorted(generated_ids - expected_ids)
    common = sorted(expected_ids & generated_ids)

    result = {
        "expectedDir": str(expected_dir),
        "generatedDir": str(generated_dir),
        "expectedCount": len(expected),
        "generatedCount": len(generated),
        "commonCount": len(common),
        "missingExpectedCount": len(missing),
        "unexpectedGeneratedCount": len(unexpected),
        "missingExpectedIds": missing[:200],
        "unexpectedGeneratedIds": unexpected[:200],
        "avgFileKb": {
            "expected": round(_avg_file_kb(list(expected.values())), 2),
            "generated": round(_avg_file_kb(list(generated.values())), 2),
        },
        "topDimensions": {
            "expected": _top_dims(list(expected.values())),
            "generated": _top_dims(list(generated.values())),
        },
    }

    print(json.dumps(result, indent=2))

    if args.outputJson:
        out = Path(args.outputJson).expanduser().resolve()
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps(result, indent=2), encoding="utf-8")
        print(f"\nSaved: {out}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
