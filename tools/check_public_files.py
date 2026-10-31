#!/usr/bin/env python3
"""Audit public candidates and, optionally, history reachable from HEAD only.

This is a targeted publication check, not an exhaustive secret scanner. Prohibited
paths are reported before their contents are read. Secret values are never shown.
"""
import argparse
from pathlib import Path, PurePosixPath
import re
import subprocess

ARCHIVE_NAMES = """AIM-ASSIST.md ALCHEMY-UI.md COMBAT-CHIPS-UI.md COMBAT-UI.md
ControllerSupportDoc.md EMBLEM-FORGE-UI.md FRONT-END-UI.md GEAR-FORGE-UI.md
GEAR-MODIFICATION-UI.md HISTORY.md INVENTORY-UI.md ITEM-TRASHER-UI.md PORTAL-UI.md
PRISM-FORGE-UI.md QUESTBOT-UI.md SHIP-DROID-UI.md STORY-CHOICE-UI.md TODOs.md
ULTIMATE-FORGE-UI.md""".split()
ARCHIVES = {name.lower() for name in ARCHIVE_NAMES}
PRIVATE_DIRS = {"backup", "backups", "profile", "profiles", "save", "saves", "save backups"}
LOCAL_ROOTS = {"reports", "labs", ".local-research", "gamereference", "references", "build"}
SUFFIXES = {".sav", ".save", ".backup", ".dll", ".exe", ".pdb", ".zip", ".log", ".pyc"}
PATTERNS = {
    "private key": rb"-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----",
    "GitHub token": rb"(?:gh[pousr]_[A-Za-z0-9_]{30,}|github_pat_[A-Za-z0-9_]{30,})",
    "AWS access key": rb"(?:AKIA|ASIA)[A-Z0-9]{16}",
    "OpenAI token": rb"sk-(?:proj-|svcacct-)?[A-Za-z0-9_-]{30,}",
    "Slack token": rb"xox[baprs]-[A-Za-z0-9-]{20,}",
    "personal home path": rb"(?:/(?:home|Users)/[A-Za-z0-9_.-]+/|[A-Za-z]:\\Users\\[A-Za-z0-9_.-]+\\)",
}
PROBES = ["nested/Backups/evidence.txt", "nested/backups/evidence.txt",
          "nested/profile/state.txt", "nested/profiles/state.txt", "nested/save/state.txt",
          "nested/saves/state.txt", "nested/Save Backups/state.txt", "PlayerPrefs.txt",
          "PLAYERPREFS.txt", "sample.sav", "sample.save", "sample.bak", "sample.backup",
          "game.dll", "game.exe", "game.pdb", "package.zip", "session.log", "launch.lock",
          "Reports/receipt.json", "Labs/helper.sh", ".local-research/baseline.txt",
          "GameReference/game.cs", "References/game.cs", "build/result.txt",
          "tests/obj/generated.cs", "tests/bin/output.txt",
          "tests/gear_forge_context_repro/NativeGearRecipe.cs"] + sorted(ARCHIVE_NAMES)


def git(repo, *args, required=True):
    result = subprocess.run(["git", "-C", str(repo), *args], capture_output=True)
    if required and result.returncode:
        raise RuntimeError("Git command failed: " + " ".join(args[:2]))
    return result


def prohibited(path):
    parts = PurePosixPath(path).parts
    lower = [part.lower() for part in parts]
    return bool(parts) and (any(part in PRIVATE_DIRS for part in lower[:-1])
        or lower[0] in LOCAL_ROOTS or (len(parts) == 1 and lower[0] in ARCHIVES)
        or "playerprefs" in lower[-1] or PurePosixPath(lower[-1]).suffix in SUFFIXES
        or re.search(r"\.bak[^/]*$", lower[-1]) is not None
        or lower[-1] == "launch.lock" or any(x in {"bin", "obj", "__pycache__"} for x in lower[:-1])
        or path == "tests/gear_forge_context_repro/NativeGearRecipe.cs")


def content_issues(data, label):
    return {f"{name}: {label}" for name, pattern in PATTERNS.items() if re.search(pattern, data)}


def audit(repo, history=False):
    issues = set()
    paths = set(git(repo, "ls-files", "-z", "--cached", "--others", "--exclude-standard")
                .stdout.decode().strip("\0").split("\0")) - {""}
    for path in sorted(paths):
        if prohibited(path):
            issues.add(f"prohibited candidate path: {path!r}")
            continue
        target = repo / path
        if target.is_symlink():
            issues.add(f"candidate symlink requires review: {path!r}")
            continue
        if target.is_file():
            issues.update(content_issues(target.read_bytes(), f"working file {path!r}"))
        staged = git(repo, "cat-file", "blob", ":" + path, required=False)
        if staged.returncode == 0:
            issues.update(content_issues(staged.stdout, f"index file {path!r}"))
    for path in PROBES:
        if git(repo, "check-ignore", "--no-index", "-q", "--", path, required=False).returncode:
            issues.add(f"missing ignore protection: {path!r}")
    blobs = {}
    if history:
        head = git(repo, "rev-parse", "--verify", "HEAD", required=False)
        commits = git(repo, "rev-list", "HEAD").stdout.splitlines() if head.returncode == 0 else []
        for commit in commits:
            tree = git(repo, "ls-tree", "-rz", commit.decode()).stdout
            for entry in tree.split(b"\0"):
                if not entry:
                    continue
                metadata, raw_path = entry.split(b"\t", 1)
                mode, kind, oid = metadata.split()
                path = raw_path.decode()
                if prohibited(path):
                    issues.add(f"prohibited HEAD history path: {path!r}")
                elif mode == b"120000":
                    issues.add(f"HEAD history symlink requires review: {path!r}")
                elif kind == b"blob":
                    blobs.setdefault(oid.decode(), path)
        for oid, path in blobs.items():
            issues.update(content_issues(git(repo, "cat-file", "blob", oid).stdout,
                                         f"HEAD history {path!r} ({oid[:12]})"))
    return sorted(issues), len(paths), len(blobs)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--history", action="store_true", help="inspect HEAD ancestry only, never other refs")
    args = parser.parse_args(argv)
    try:
        repo = Path(git(args.repo, "rev-parse", "--show-toplevel").stdout.decode().strip())
        issues, paths, blobs = audit(repo, args.history)
    except (OSError, RuntimeError, UnicodeError) as error:
        print("Audit could not complete: " + str(error))
        return 2
    for issue in issues:
        print("FAIL " + issue)
    scope = f"; {blobs} permitted historical blobs from HEAD" if args.history else ""
    print(f"Public audit: {len(issues)} issue(s), {paths} candidate paths{scope}.")
    return 1 if issues else 0


if __name__ == "__main__":
    raise SystemExit(main())
