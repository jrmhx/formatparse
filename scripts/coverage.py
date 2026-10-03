"""Summarize Cobertura coverage and enforce the project's minimum rates."""
import argparse
import json
import os
from pathlib import Path
import xml.etree.ElementTree as ET


def rates(root):
    result = []
    for name in ("lines", "branches"):
        covered = int(root.attrib[f"{name}-covered"])
        total = int(root.attrib[f"{name}-valid"])
        if covered < 0 or total < covered:
            raise ValueError(f"Invalid {name} coverage counts")
        result.append(covered / total if total else 1.0)
    return tuple(result)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--minimum-line", type=float, default=90)
    parser.add_argument("--minimum-branch", type=float, default=85)
    args = parser.parse_args()
    reports = list(args.directory.rglob("coverage.cobertura.xml"))
    if len(reports) != 1:
        raise SystemExit(f"Expected one coverage report, found {len(reports)}")
    line, branch = rates(ET.parse(reports[0]).getroot())
    summary = f"## Coverage\n\n| Lines | Branches |\n| --- | --- |\n| {line:.2%} | {branch:.2%} |\n"
    print(summary)
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as output:
            output.write(summary)
    badge = {"schemaVersion": 1, "label": "coverage", "message": f"{line:.1%}", "color": "brightgreen"}
    (args.directory / "coverage-badge.json").write_text(json.dumps(badge), encoding="utf-8")
    if line * 100 < args.minimum_line or branch * 100 < args.minimum_branch:
        raise SystemExit("Coverage is below the configured minimum")


if __name__ == "__main__":
    main()
