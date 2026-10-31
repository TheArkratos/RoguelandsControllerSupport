#!/usr/bin/env python3
"""Discover and run offline fixtures, with explicit local prerequisites and results."""

import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import dataclass
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]


@dataclass(frozen=True)
class Fixture:
    project: Path
    categories: tuple
    required_files: tuple


def paths_for(root):
    game = Path(os.environ.get("ROGUELANDS_DIR") or
                Path.home() / ".local/share/Steam/steamapps/common/Roguelands").resolve()
    reference = Path(os.environ.get("ROGUELANDS_REFERENCE_DIR") or
                     root.parent.parent / "GameReference/Generated").resolve()
    return game / "Roguelands_Data/Managed", reference


def classify(project, root, managed, reference):
    """Read fixture declarations and direct source inputs; do not execute MSBuild."""
    tree = ET.parse(project)
    categories, required = set(), set()
    texts = []
    for compile_item in tree.findall(".//Compile"):
        include = compile_item.get("Include", "")
        if "$(RoguelandsReferenceDir)" in include:
            categories.add("generated-source")
            required.add(Path(include.replace("$(RoguelandsReferenceDir)", str(reference))))
        elif "$(" not in include and "*" not in include:
            source = project.parent / include
            if source.is_file():
                texts.append(source.read_text())
    for hint in tree.findall(".//HintPath"):
        path = hint.text or ""
        if "$(RoguelandsManagedDir)" in path:
            categories.add("installed-assemblies")
            required.add(Path(path.replace("$(RoguelandsManagedDir)", str(managed))))
    if any((node.text or "").lower() == "true"
           for node in tree.findall(".//RequiresRoguelandsReference")):
        categories.add("generated-source")
        # Some research fixtures generate their native input instead of directly
        # listing the original GameScript in Compile items.
        if not any(reference in path.parents for path in required):
            required.add(reference / "Assembly-CSharp/GameScript.cs")
    source = "\n".join(texts)
    if re.search(r'LocalReferences\.Get\(\s*"RoguelandsManagedDir"', source):
        categories.add("installed-assemblies")
        required.update(managed / name for name in (
            "Assembly-CSharp.dll", "UnityEngine.dll", "GadgetCore.dll", "0Harmony.dll",
            "mscorlib.dll", "System.dll", "System.Core.dll"))
    if re.search(r'LocalReferences\.Get\(\s*"RoguelandsReferenceDir"', source):
        categories.add("generated-source")
        required.add(reference / "Assembly-CSharp/GameScript.cs")
        # The native IL audit examines these installed-loader patch references.
        required.update(reference / "GadgetCore/GadgetCore.Patches" / name for name in (
            "Patch_GameScript_PrismItem.cs", "Patch_GameScript_Update.cs"))
    if '"ControllerSupport.dll"' in source and '"build"' in source:
        categories.add("built-mod")
        required.add(root / "build/ControllerSupport.dll")
    return Fixture(project, tuple(sorted(categories)) or ("pure",), tuple(sorted(required)))


def discover(root, managed, reference):
    return [classify(project, root, managed, reference)
            for project in sorted((root / "tests").rglob("*.csproj"))
            if not {"bin", "obj"}.intersection(project.relative_to(root / "tests").parts)]


def run_fixture(fixture, root, output, dotnet):
    relative = fixture.project.relative_to(root)
    log = output / relative.with_suffix(".log")
    log.parent.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    command = [dotnet, "run", "--project", str(fixture.project), "-p:UseSharedCompilation=false"]
    try:
        with log.open("w") as stream:
            process = subprocess.run(command, cwd=root, stdout=stream, stderr=subprocess.STDOUT)
        code, reason = process.returncode, ""
    except OSError as error:
        code, reason = -1, str(error)
        log.write_text(reason + "\n")
    return {"project": str(relative), "categories": list(fixture.categories),
            "status": "PASS" if code == 0 else "FAIL", "returncode": code,
            "seconds": round(time.monotonic() - started, 3), "reason": reason,
            "log": str(log)}


def summarize(results):
    counts = {status.lower(): sum(row["status"] == status for row in results)
              for status in ("PASS", "FAIL", "SKIP")}
    return {"total": len(results), **counts,
            "results": sorted(results, key=lambda row: row["project"])}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--filter", action="append", default=[], metavar="TEXT",
                        help="select paths containing TEXT (repeat to match any)")
    parser.add_argument("--list", action="store_true", help="list fixtures and prerequisite categories")
    parser.add_argument("--jobs", type=int, choices=(1, 2), default=2)
    parser.add_argument("--output-dir", type=Path, help="write logs and summary.json here")
    args = parser.parse_args(argv)
    managed, reference = paths_for(ROOT)
    fixtures = discover(ROOT, managed, reference)
    if args.filter:
        fixtures = [fixture for fixture in fixtures if any(
            token.lower() in str(fixture.project.relative_to(ROOT)).lower() for token in args.filter)]
    if not fixtures:
        parser.error("No fixtures matched.")
    if args.list:
        for fixture in fixtures:
            print(str(fixture.project.relative_to(ROOT)) + ": " + ", ".join(fixture.categories))
        return 0
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S")
    output = (args.output_dir or ROOT / "build/test-results" / (stamp + "-" + str(os.getpid()))).resolve()
    output.mkdir(parents=True, exist_ok=True)
    dotnet = shutil.which("dotnet")
    results, runnable = [], []
    for fixture in fixtures:
        missing = [str(path) for path in fixture.required_files if not path.is_file()]
        reason = "dotnet is not installed" if not dotnet else "Missing prerequisites: " + ", ".join(missing) if missing else ""
        if reason:
            project = str(fixture.project.relative_to(ROOT))
            print("SKIP " + project + ": " + reason, flush=True)
            results.append({"project": project, "categories": list(fixture.categories),
                            "status": "SKIP", "reason": reason})
        else:
            runnable.append(fixture)
    with ThreadPoolExecutor(max_workers=args.jobs) as executor:
        futures = [executor.submit(run_fixture, fixture, ROOT, output, dotnet) for fixture in runnable]
        for future in as_completed(futures):
            result = future.result()
            results.append(result)
            print(result["status"] + " " + result["project"] + " (" + str(result["seconds"]) + "s)", flush=True)
            if result["status"] == "FAIL":
                print("  log: " + result["log"], flush=True)
    summary = summarize(results)
    (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
    print("{pass} passed, {fail} failed, {skip} skipped / {total} selected".format(**summary))
    print("Results: " + str(output / "summary.json"))
    # A partial run is never reported as successful, even if runnable tests pass.
    return 1 if summary["fail"] or summary["skip"] else 0


if __name__ == "__main__":
    sys.exit(main())
