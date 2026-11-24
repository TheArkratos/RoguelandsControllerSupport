#!/usr/bin/env python3
"""Bounded native-adapter smoke in the game's exact embedded Mono, not Unity.

Usage: python3 -B tools/verify_legacy_mono.py --assembly build/deck/ControllerSupport.dll
All outputs are isolated in ignored build/legacy-mono-smoke/run-*/. No installation,
game launch, profile access, system installation or system Mono fallback occurs.
"""

import argparse
import hashlib
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys
import tempfile


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "build" / "legacy-mono-smoke"


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def prepare_x86_host_stack(host):
    """Set executable-stack flag on ONLY our newly compiled disposable x86 host.

    Shipped legacy x86 Mono requires it; modern glibc cannot add it at dlopen.
    Zig's linker lacks -z execstack, so set the existing ELF program-header flag.
    No installed/runtime/distributed binary is edited.
    """
    data = bytearray(host.read_bytes())
    if len(data) < 52 or data[:6] != b"\x7fELF\x01\x01" or struct.unpack_from("<H", data, 18)[0] != 3:
        raise RuntimeError("Expected authored little-endian x86 ELF host")
    offset = struct.unpack_from("<I", data, 28)[0]
    size, count = struct.unpack_from("<HH", data, 42)
    if size != 32 or offset + size * count > len(data):
        raise RuntimeError("Invalid authored host program headers")
    headers = [offset + index * size for index in range(count)
               if struct.unpack_from("<I", data, offset + index * size)[0] == 0x6474e551]
    if len(headers) != 1:
        raise RuntimeError("Expected one authored host GNU_STACK header")
    flags_offset = headers[0] + 24
    struct.pack_into("<I", data, flags_offset, struct.unpack_from("<I", data, flags_offset)[0] | 1)
    host.write_bytes(data)


def run(command, directory, env, log_name, timeout=60):
    print("COMMAND " + " ".join(str(part) for part in command), flush=True)
    try:
        completed = subprocess.run(
            [str(part) for part in command], cwd=directory, env=env,
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
            errors="replace", timeout=timeout, check=False,
        )
    except subprocess.TimeoutExpired as error:
        partial = error.stdout or b""
        if isinstance(partial, bytes):
            partial = partial.decode(errors="replace")
        (directory / log_name).write_text(partial)
        print(partial, end="", flush=True)
        raise RuntimeError(f"{log_name}: timed out after {timeout}s") from error
    (directory / log_name).write_text(completed.stdout)
    print(completed.stdout, end="", flush=True)
    if completed.returncode:
        raise RuntimeError(f"{log_name}: process exit {completed.returncode}")
    return completed.stdout


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assembly", default="build/deck/ControllerSupport.dll")
    parser.add_argument("--arch", choices=("x86", "x86_64"), default="x86_64")
    parser.add_argument("--export-resources", type=Path,
                        help="Export checksum-verified native resources into a new directory")
    parser.add_argument("--game-dir", default=os.environ.get(
        "ROGUELANDS_DIR", str(Path.home() / ".local/share/Steam/steamapps/common/Roguelands")))
    parser.add_argument("--require-embedded", action="store_true",
                        help="Fail unless the loaded SDL is extracted from an embedded resource")
    args = parser.parse_args()
    assembly = Path(args.assembly).resolve()
    data = Path(args.game_dir).resolve() / "Roguelands_Data"
    managed = data / "Managed"
    mono = data / "Mono" / args.arch / "libmono.so"
    if args.arch == "x86" and not mono.is_file():
        print("UNAVAILABLE x86: installed game has no x86 libmono.so", file=sys.stderr)
        return 77
    inputs = [assembly, mono, managed / "mscorlib.dll", managed / "System.dll"]
    before = {}
    directory = None
    try:
        for path in inputs:
            if not path.is_file():
                raise RuntimeError(f"Missing read-only input: {path}")
            before[path] = sha256(path)
        ignored = subprocess.run(
            ["git", "check-ignore", "--quiet", "build/legacy-mono-smoke/probe"],
            cwd=ROOT, check=False,
        )
        if ignored.returncode:
            raise RuntimeError("Refusing outputs: build/legacy-mono-smoke is not ignored")
        OUTPUT.mkdir(parents=True, exist_ok=True)
        directory = Path(tempfile.mkdtemp(prefix="run-", dir=OUTPUT))
        print(f"OUTPUT {directory}", flush=True)
        env = os.environ.copy()
        # Ignore a developer's native override: this check must exercise selection.
        for key in ("ROGUELANDS_CONTROLLER_SDL2_LIBRARY", "MONO_PATH", "MONO_CFG_DIR",
                    "MONO_ENV_OPTIONS", "LD_PRELOAD", "LD_LIBRARY_PATH"):
            env.pop(key, None)
        for key in ("HOME", "TMPDIR", "XDG_CACHE_HOME", "XDG_CONFIG_HOME", "XDG_DATA_HOME",
                    "DOTNET_CLI_HOME"):
            target = directory / key.lower()
            target.mkdir()
            env[key] = str(target)
        env["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1"
        env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
        sdk_version = run(["dotnet", "--version"], directory, env, "sdk-version.log").strip()
        sdks = run(["dotnet", "--list-sdks"], directory, env, "sdks.log")
        matches = re.findall(r"^" + re.escape(sdk_version) + r" \[(.+)\]$", sdks, re.MULTILINE)
        if not matches:
            raise RuntimeError("Cannot locate active Roslyn SDK")
        csc = Path(matches[0]) / sdk_version / "Roslyn/bincore/csc.dll"
        host = directory / "legacy-mono-host"
        harness = directory / "LegacyMonoSmoke.exe"
        cache = directory / "native-cache"
        cache.mkdir()
        # Load the copy so Mono cannot write beside the caller's artifact.
        copied = directory / assembly.name
        shutil.copyfile(assembly, copied)
        compiler = ["cc"]
        if args.arch == "x86":
            # Reuse only the checksum-pinned cached toolchain; never download/install.
            from prepare_deck_sdl2 import ZIG_SHA256, ZIG_URL, extract
            archive = ROOT / "build/deck-native/downloads" / ZIG_URL.rsplit("/", 1)[1]
            if not archive.is_file():
                print("UNAVAILABLE x86: pinned cached Zig compiler is missing", file=sys.stderr)
                return 77
            toolchain = directory / "toolchain"
            extract(archive, ZIG_SHA256, toolchain)
            zig = toolchain / "zig-linux-x86_64-0.13.0/zig"
            # The shipped 32-bit Mono declares an executable stack. New glibc
            # refuses to enable it during dlopen; declare it on this isolated
            # embedding host instead, matching the old runtime's requirement.
            # No game binary or distributed mod library is modified.
            compiler = [zig, "cc", "-target", "x86-linux-gnu.2.17", "-mcpu=baseline", "-fPIE", "-pie"]
        run([*compiler, "-std=c99", "-Wall", "-Wextra", "-Werror", "-O2",
             ROOT / "tools/legacy_mono_host.c", "-ldl", "-o", host],
            directory, env, "host-build.log")
        if args.arch == "x86":
            prepare_x86_host_stack(host)
            headers = run(["readelf", "-lW", host], directory, env, "host-elf.log")
            interpreter = re.search(r"Requesting program interpreter: ([^\]]+)", headers)
            if not interpreter or not Path(interpreter.group(1)).is_file():
                print("UNAVAILABLE x86: 32-bit host compiled, but its ELF runtime loader is missing",
                      file=sys.stderr)
                return 77
        run(["dotnet", csc, "/nologo", "/target:exe", "/langversion:7.3", "/nostdlib",
             f"/out:{harness}", f"/reference:{managed / 'mscorlib.dll'}",
             f"/reference:{managed / 'System.dll'}", f"/reference:{copied}",
             ROOT / "tools/LegacyMonoSmoke.cs"], directory, env, "harness-build.log")
        resources = directory / "resources"
        output = run([host, mono, managed, harness, copied, cache, resources],
                     directory, env, "smoke.log", timeout=30)
        if "PASS native adapter" not in output or "ADAPTER_DISPOSED" not in output:
            raise RuntimeError("Harness did not report completed adapter lifecycle")
        if "PROCESS_ARCH " + args.arch not in output.splitlines():
            raise RuntimeError("Harness process architecture mismatch")
        mscorlib_lines = [line.removeprefix("MSCORLIB ") for line in output.splitlines()
                          if line.startswith("MSCORLIB ")]
        if len(mscorlib_lines) != 1 or Path(mscorlib_lines[0]).resolve() != inputs[2].resolve():
            raise RuntimeError("Harness did not use the game's exact mscorlib")
        embedded = re.findall(r"^EMBEDDED_SHA256 ([0-9a-f]{64}) (.+)$", output, re.MULTILINE)
        loaded = re.search(r"^LOADED_NATIVE (.+)$", output, re.MULTILINE)
        digest = re.search(r"^LOADED_SHA256 ([0-9a-f]{64})$", output, re.MULTILINE)
        if not loaded or not digest:
            raise RuntimeError("Missing actual native library identity")
        native = Path(loaded.group(1)).resolve()
        if sha256(native) != digest.group(1):
            raise RuntimeError("Loaded native bytes changed after smoke")
        if embedded or args.require_embedded:
            if not native.is_relative_to(cache.resolve()):
                raise RuntimeError("Embedded smoke selected a library outside the isolated cache")
            resources = [name for value, name in embedded if value == digest.group(1)]
            if not resources or "ControllerSupport.Native.linux-" + args.arch + ".SDL2" not in resources:
                raise RuntimeError("Extracted library hash does not match an embedded native resource")
            print("PASS embedded selection/extraction SHA256 " + digest.group(1) + " " + resources[0])
        else:
            print("PASS system SDL only; embedded selection was NOT tested")
        if args.export_resources:
            shutil.copytree(directory / "resources", args.export_resources.resolve())
    except (OSError, RuntimeError, subprocess.TimeoutExpired) as error:
        print(f"FAIL legacy Mono smoke: {error}", file=sys.stderr)
        return 1
    finally:
        for path, expected in before.items():
            if not path.is_file() or sha256(path) != expected:
                print(f"FAIL input bytes changed: {path}", file=sys.stderr)
                return 1
    print("PASS exact game Mono standalone smoke; no Unity/gameplay acceptance claimed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
