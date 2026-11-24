#!/usr/bin/env python3
"""Audit the actual Deck ZIP and smoke its DLL in standalone game Mono, not Unity."""
import argparse
import configparser
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import zipfile

from prepare_deck_sdl2 import inspect_elf, verify_binary_privacy, SDL_SHA256, SDL_VERSION

ROOT = Path(__file__).resolve().parents[1]
ARCHES = ("x86", "x86_64")
FILES = {"Manifest.ini", "ModInfo.txt", "LICENSE", "ControllerSupport.dll", "STEAM-DECK-TEST.md"}
FILES.update("SDL2/linux-" + arch + "/" + name
             for arch in ARCHES for name in ("LICENSE.txt", "provenance.json"))


def inspect_package(path):
    with zipfile.ZipFile(path) as package:
        names = package.namelist()
        if len(names) != len(set(names)):
            raise ValueError("Duplicate ZIP entries")
        if {entry.filename for entry in package.infolist() if not entry.is_dir()} != FILES:
            raise ValueError("Unexpected package contents (native files must not be in Libs)")
        directories = {"SDL2/", "SDL2/linux-x86/", "SDL2/linux-x86_64/"}
        if any(entry.is_dir() and entry.filename not in directories for entry in package.infolist()):
            raise ValueError("Unexpected package directory")
        if sum(entry.file_size for entry in package.infolist()) > 64 * 1024 * 1024:
            raise ValueError("Unexpected package size")
        manifest = configparser.ConfigParser()
        manifest.read_string(package.read("Manifest.ini").decode())
        if manifest["Metadata"]["Assembly"] != "ControllerSupport.dll":
            raise ValueError("Manifest assembly mismatch")
        receipts = {}
        for arch in ARCHES:
            prefix = "SDL2/linux-" + arch + "/"
            receipt = json.loads(package.read(prefix + "provenance.json"))
            if receipt["sdl_version"] != SDL_VERSION or receipt["source_sha256"] != SDL_SHA256:
                raise ValueError("SDL provenance does not match pinned upstream source: " + arch)
            if receipt["target"] != arch + "-linux-gnu.2.17":
                raise ValueError("SDL provenance target mismatch: " + arch)
            if not re.fullmatch(r"[0-9a-f]{64}", receipt["library_sha256"]):
                raise ValueError("Invalid library checksum: " + arch)
            if hashlib.sha256(package.read(prefix + "LICENSE.txt")).hexdigest() != receipt["license_sha256"]:
                raise ValueError("SDL license checksum mismatch: " + arch)
            receipts[arch] = receipt
        return receipts, package.read("ControllerSupport.dll")


def inspect_resources(directory, receipts):
    results = {}
    for arch in ARCHES:
        native = directory / ("linux-" + arch + ".so")
        if not native.is_file():
            raise ValueError("Missing actual embedded native resource: " + arch)
        if hashlib.sha256(native.read_bytes()).hexdigest() != receipts[arch]["library_sha256"]:
            raise ValueError("Embedded native bytes do not match packaged provenance: " + arch)
        verify_binary_privacy(native)
        results[arch] = inspect_elf(native, arch=arch)
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", nargs="?", default="build/ControllerSupport-Deck.zip")
    arguments = parser.parse_args()
    package = Path(arguments.package).resolve()
    receipts, assembly = inspect_package(package)
    output = ROOT / "build" / "deck-package-check"
    output.mkdir(parents=True, exist_ok=True)
    directory = Path(tempfile.mkdtemp(prefix="run-", dir=output))
    dll = directory / "ControllerSupport.dll"
    dll.write_bytes(assembly)
    resources = directory / "resources"
    completed = subprocess.run([sys.executable, "-B", str(ROOT / "tools/verify_legacy_mono.py"),
                                 "--assembly", str(dll), "--require-embedded", "--arch", "x86_64",
                                 "--export-resources", str(resources)],
                               cwd=ROOT, capture_output=True, text=True, timeout=180)
    (directory / "mono-smoke.log").write_text(completed.stdout + completed.stderr)
    if completed.returncode:
        raise RuntimeError("Packaged DLL failed Mono smoke: " + str(directory / "mono-smoke.log"))
    match = re.search(r"^LOADED_NATIVE (.+)$", completed.stdout, re.MULTILINE)
    if not match:
        raise ValueError("Missing loaded native identity")
    native = Path(match.group(1))
    if hashlib.sha256(native.read_bytes()).hexdigest() != receipts["x86_64"]["library_sha256"]:
        raise ValueError("Embedded native bytes do not match packaged provenance")
    abi = inspect_resources(resources, receipts)
    for line in completed.stdout.splitlines():
        if line.startswith(("MANAGED_RUNTIME ", "PASS ")):
            print(line)
    x86 = subprocess.run([sys.executable, "-B", str(ROOT / "tools/verify_legacy_mono.py"),
                          "--assembly", str(dll), "--require-embedded", "--arch", "x86"],
                         cwd=ROOT, capture_output=True, text=True, timeout=300)
    (directory / "mono-smoke-x86.log").write_text(x86.stdout + x86.stderr)
    if x86.returncode not in (0, 77):
        raise RuntimeError("Packaged DLL failed x86 Mono smoke: " + str(directory / "mono-smoke-x86.log"))
    x86_status = "passed" if x86.returncode == 0 else "unavailable (not passed)"
    print(("PASS" if x86.returncode == 0 else "UNAVAILABLE") + " x86 exact Mono smoke: " + x86_status)
    if x86.returncode == 77:
        print(x86.stderr.strip())
    digest = hashlib.sha256(package.read_bytes()).hexdigest()
    print("PASS package entries, manifest, licenses, native provenance and privacy")
    for arch in ARCHES:
        print("PASS packaged ELF " + arch + ": GLIBC <= " + abi[arch]["glibc_max"] + "; " + ", ".join(abi[arch]["needed"]))
    print("PACKAGE_SHA256 " + digest)
    report = {"package": package.name, "sha256": digest,
              "architectures": {arch: {"sdl_sha256": receipts[arch]["library_sha256"],
                                       "glibc_max": abi[arch]["glibc_max"],
                                       "exact_mono_smoke": "passed" if arch == "x86_64" else x86_status}
                                for arch in ARCHES},
              "installed": False, "physical_deck_acceptance": "not tested"}
    (ROOT / "build/ControllerSupport-Deck-validation.json").write_text(json.dumps(report, indent=2) + "\n")


if __name__ == "__main__":
    main()
