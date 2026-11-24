#!/usr/bin/env python3
"""Build the pinned, input-only SDL2 for native Linux x86_64 and x86 (not Proton).

Run from any directory: python3 -B tools/prepare_deck_sdl2.py
Requires Linux x86_64, Python >=3.12, cmake, ninja and readelf; no game access.
Both architectures are built by default: the 64-bit library and the 32-bit
library the Deck's native game process loads; the pinned host toolchain
cross-compiles both. Downloads and work products remain in ignored
build/deck-native. No installation.
"""

import argparse
import getpass
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shlex
import shutil
import subprocess
import tarfile
import tempfile
import urllib.request


SDL_VERSION = "2.32.10"
SDL_URL = "https://github.com/libsdl-org/SDL/releases/download/release-2.32.10/SDL2-2.32.10.tar.gz"
SDL_SHA256 = "5f5993c530f084535c65a6879e9b26ad441169b3e25d789d83287040a9ca5165"
# SDL hash: official GitHub release asset digest, release-2.32.10.
ZIG_URL = "https://ziglang.org/download/0.13.0/zig-linux-x86_64-0.13.0.tar.xz"
ZIG_SHA256 = "d45312e61ebcc48032b77bc4cf7fd6915c11fa16e4aad116b66c9468211230ea"
# Zig hash: https://ziglang.org/download/index.json, 0.13.0/x86_64-linux.
TARGET = "x86_64-linux-gnu.2.17"
# Pinned glibc-2.17 targets. The pinned x86_64 host Zig cross-compiles both;
# each stages under build/deck-native for the dual-architecture Deck ZIP.
ARCHITECTURES = ("x86_64", "x86")
ARCH_TARGETS = {"x86_64": TARGET, "x86": "x86-linux-gnu.2.17"}
# CMake's architecture selection drives SDL's 32/64-bit build configuration.
ARCH_PROCESSORS = {"x86_64": "x86_64", "x86": "i686"}
ARCH_STAGES = {"x86_64": "linux-x86_64", "x86": "linux-x86"}
# Exact readelf identity each packaged library must show; a mismatched ELF
# class, machine or foreign dynamic-loader SONAME is rejected.
ARCH_ELF_SIGNATURES = {
    "x86_64": ("ELF64", "Advanced Micro Devices X86-64"),
    "x86": ("ELF32", "Intel 80386"),
}
ARCH_ELF_LINKERS = {
    "x86_64": "ld-linux-x86-64.so.2",
    "x86": "ld-linux.so.2",
}
ARCH_CPU = {
    "x86_64": "baseline x86_64; SDL runtime-dispatched SIMD enabled",
    "x86": "baseline x86 (i686-class); cross-compiled for the 32-bit game process",
}
UDEV_BASE = "https://raw.githubusercontent.com/systemd/systemd/252ff40a3827b4e8e62e03a5e2f80da3fe02bee6/"
UDEV_HEADER_URL = UDEV_BASE + "src/libudev/libudev.h"
UDEV_HEADER_SHA256 = "fce50b0e7b6c599463287123c153dd15ea6993a5ccbf9076973e336f6e32166e"
UDEV_LICENSE_URL = UDEV_BASE + "LICENSE.LGPL2.1"
UDEV_LICENSE_SHA256 = "dc626520dcd53a22f727af3ee42c770e56c97a64fe3adb063799d8ab032fe551"
ROOT = Path(__file__).resolve().parents[1] / "build" / "deck-native"


def sha256(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def verify(path, expected):
    if sha256(path) != expected:
        raise ValueError(f"SHA-256 mismatch: {Path(path).name}")


def download(url, expected, destination):
    """Only publish a complete, hash-verified HTTPS download; recheck cache too."""
    destination = Path(destination)
    if not url.startswith("https://"):
        raise ValueError("Downloads require HTTPS")
    if destination.exists():
        verify(destination, expected)
        return
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=destination.parent, delete=False) as stream:
        temporary = Path(stream.name)
        try:
            with urllib.request.urlopen(url, timeout=120) as response:
                if not response.geturl().startswith("https://"):
                    raise ValueError("Download redirected away from HTTPS")
                shutil.copyfileobj(response, stream)
            stream.close()
            verify(temporary, expected)
            temporary.replace(destination)
        finally:
            temporary.unlink(missing_ok=True)


def extract(archive, expected, destination):
    """Verify BEFORE opening tar; reject traversal, escaping links and devices.

    Python's data filter validates contained symlink/hardlink targets, including
    existing symlink ancestors. Official SDL includes internal Android symlinks.
    Extraction always uses a new directory; partial extraction is not reused.
    """
    verify(archive, expected)
    destination = Path(destination)
    if destination.exists():
        raise ValueError("Extraction destination must not exist")
    with tarfile.open(archive) as archive_file:
        members = archive_file.getmembers()
        seen = set()
        for member in members:
            parts = Path(member.name).parts
            if (not parts or Path(member.name).is_absolute() or ".." in parts
                    or not (member.isfile() or member.isdir() or member.issym() or member.islnk())
                    or member.name in seen):
                raise ValueError(f"Unsafe archive entry: {member.name}")
            seen.add(member.name)
            tarfile.data_filter(member, str(destination))
        destination.mkdir(parents=True)
        archive_file.extractall(destination, members=members, filter="data")


def inspect_elf(library, arch="x86_64"):
    """Validate architecture identity, isolation and glibc ceiling of a build.

    The default x86_64 keeps the historical signature; pass arch="x86" for the
    32-bit library. A header or dependency belonging to another architecture is
    rejected, not merely warned about.
    """
    if arch not in ARCH_ELF_SIGNATURES:
        raise ValueError(f"Unsupported ELF architecture: {arch}")

    def readelf(*options):
        return subprocess.check_output(["readelf", *options, str(library)], text=True)

    header = readelf("-h")
    dynamic = readelf("-dW")
    versions = readelf("--version-info", "-W")
    symbols = readelf("--dyn-syms", "-W")
    needed = sorted(re.findall(r"\(NEEDED\).*\[(.*?)\]", dynamic))
    allowed = {"libc.so.6", "libm.so.6", "libdl.so.2", "libpthread.so.0", "librt.so.1",
               ARCH_ELF_LINKERS[arch]}
    elf_class, machine = ARCH_ELF_SIGNATURES[arch]
    if elf_class not in header or machine not in header:
        raise ValueError(f"Not an {arch} {elf_class} library")
    if not needed or set(needed) - allowed:
        raise ValueError(f"Unexpected native dependencies: {needed}")
    if "(RPATH)" in dynamic or "(RUNPATH)" in dynamic:
        raise ValueError("Unexpected library search path")
    if "[libControllerSupport-SDL2.so]" not in dynamic or "SYMBOLIC" not in dynamic:
        raise ValueError("Missing private SONAME or local symbol binding")
    glibc = sorted(set(re.findall(r"GLIBC_(\d+(?:\.\d+)+)", versions)),
                   key=lambda version: tuple(map(int, version.split("."))))
    if not glibc or tuple(map(int, glibc[-1].split("."))) > (2, 17):
        raise ValueError(f"glibc floor exceeds 2.17: {glibc}")
    if "GLIBC_PRIVATE" in versions or "GLIBCXX_" in versions:
        raise ValueError("Unexpected private/C++ ABI")
    for name in ("SDL_GameControllerOpen", "SDL_InitSubSystem", "SDL_WasInit", "SDL_PollEvent",
                 "SDL_QuitSubSystem", "SDL_GameControllerUpdate", "SDL_NumJoysticks", "SDL_IsGameController",
                 "SDL_GameControllerGetAttached", "SDL_GameControllerGetAxis", "SDL_GameControllerGetButton",
                 "SDL_GameControllerClose", "SDL_GetError",
                 "SDL_JoystickAttachVirtual", "SDL_JoystickDetachVirtual", "SDL_JoystickOpen",
                 "SDL_JoystickClose", "SDL_JoystickSetVirtualAxis", "SDL_JoystickSetVirtualButton",
                 "SDL_JoystickEventState", "SDL_GameControllerEventState"):
        if not re.search(r"\b" + name + r"\b", symbols):
            raise ValueError(f"Missing SDL export: {name}")
    return {"arch": arch, "needed": needed, "glibc_max": glibc[-1], "readelf_header": header,
            "readelf_dynamic": dynamic, "readelf_versions": versions}


def verify_binary_privacy(library, extra_markers=()):
    """Reject private strings anywhere in ELF bytes, not merely metadata/strings.

    Do not print the matching private value in an error or publish it in receipts.
    The companion-workspace name is private too; the mod name itself is public.
    """
    markers = {b"/home/", b"/Users/", str(Path.home()).encode(),
               str(ROOT.parents[1]).encode(), getpass.getuser().encode()}
    if ROOT.parents[2].name == "Mods":
        markers.add(ROOT.parents[3].name.encode())
    markers.update(marker.encode() if isinstance(marker, str) else marker
                   for marker in extra_markers)
    data = Path(library).read_bytes()
    if any(marker and marker in data for marker in markers):
        raise ValueError("Refusing to publish native library containing a private path/identity marker")


def strip_library(zig, library, environment, log):
    """Use only the verified pinned compiler's objcopy; retain ELF dynsym exports."""
    stripped = library.with_name(library.name + ".stripped")
    command = [str(zig), "objcopy", "--strip-all", str(library), str(stripped)]
    log.write(shlex.join(command) + "\n")
    log.flush()
    subprocess.run(command, env=environment, stdout=log, stderr=subprocess.STDOUT, check=True)
    verify_binary_privacy(stripped)
    stripped.replace(library)
    return command


def prepare(jobs, arch="x86_64"):
    if arch not in ARCHITECTURES:
        raise ValueError(f"Unsupported build architecture: {arch}")
    target = ARCH_TARGETS[arch]
    if platform.system() != "Linux" or platform.machine() != "x86_64":
        raise RuntimeError("Build host must be Linux x86_64")
    for tool in ("cmake", "ninja", "readelf"):
        if not shutil.which(tool):
            raise RuntimeError(f"Required build tool missing: {tool}")
    ROOT.mkdir(parents=True, exist_ok=True)
    sources = []
    for url, digest in ((SDL_URL, SDL_SHA256), (ZIG_URL, ZIG_SHA256)):
        archive = ROOT / "downloads" / url.rsplit("/", 1)[1]
        download(url, digest, archive)
        sources.append((archive, digest))
    # Fresh source/toolchain/build on each run, never trust stale extracted code.
    with tempfile.TemporaryDirectory(prefix="work-", dir=ROOT) as work_name:
        work = Path(work_name)
        for index, (archive, digest) in enumerate(sources):
            extract(archive, digest, work / str(index))
        source = work / "0" / f"SDL2-{SDL_VERSION}"
        udev_include = work / "udev-include"
        udev_include.mkdir()
        for url, digest, filename in ((UDEV_HEADER_URL, UDEV_HEADER_SHA256, "libudev.h"),
                                      (UDEV_LICENSE_URL, UDEV_LICENSE_SHA256, "LICENSE-LIBUDEV.txt")):
            cached = ROOT / "downloads" / filename
            download(url, digest, cached)
            shutil.copyfile(cached, udev_include / filename)
        # SDL's normal probe requires a build-host libudev shared library just to
        # choose a dlopen SONAME. Pin the long-stable runtime SONAME instead.
        udev_customization = ('macro(CheckLibUDev)\n'
                              '  set(HAVE_LIBUDEV_H TRUE)\n'
                              '  set(SDL_UDEV_DYNAMIC "\\\"libudev.so.1\\\"")\n'
                              '  set(HAVE_LIBUDEV TRUE)\nendmacro()')
        checks = source / "cmake/sdlchecks.cmake"
        patched, count = re.subn(r"macro\(CheckLibUDev\).*?endmacro\(\)",
                                lambda match: udev_customization, checks.read_text(), flags=re.S)
        if count != 1:
            raise ValueError("Pinned SDL libudev build probe changed")
        checks.write_text(patched)
        # Local build-only CMake customization, not a patch to SDL implementation.
        # Private SONAME plus local function binding prevents host SDL interposition.
        customization = ('\nset_target_properties(SDL2 PROPERTIES OUTPUT_NAME "ControllerSupport-SDL2")\n'
                         'set_property(TARGET SDL2 PROPERTY VERSION)\n'
                         'set_property(TARGET SDL2 PROPERTY SOVERSION)\n'
                         'target_link_options(SDL2 PRIVATE "-Wl,-Bsymbolic")\n')
        with (source / "CMakeLists.txt").open("a") as cmake_file:
            cmake_file.write(customization)
        zig = work / "1" / "zig-linux-x86_64-0.13.0" / "zig"
        wrappers = []
        for language, subcommand in (("C", "cc"), ("CXX", "c++")):
            wrapper = work / ("zig-" + subcommand.replace("+", "x"))
            wrapper.write_text(f"#!/bin/sh\nexec {shlex.quote(str(zig))} {subcommand} -target {target} -mcpu=baseline \"$@\"\n")
            wrapper.chmod(0o755)
            wrappers.append(f"-DCMAKE_{language}_COMPILER={wrapper}")
        options = {
            "CMAKE_BUILD_TYPE": "Release", "CMAKE_SYSTEM_NAME": "Linux",
            "CMAKE_SYSTEM_PROCESSOR": ARCH_PROCESSORS[arch], "CMAKE_SKIP_RPATH": "ON",
            "CMAKE_C_FLAGS": "-ffile-prefix-map=" + str(work) + "=/sdl2-build -I" + shlex.quote(str(udev_include)),
            "SDL_SHARED": "ON", "SDL_STATIC": "OFF", "SDL_TEST": "OFF",
            "SDL_TESTS": "OFF", "SDL2_DISABLE_INSTALL": "ON", "SDL2_DISABLE_SDL2MAIN": "ON",
            "SDL_AUDIO": "OFF", "SDL_VIDEO": "OFF", "SDL_RENDER": "OFF",
            "SDL_POWER": "OFF", "SDL_SENSOR": "OFF", "SDL_HAPTIC": "OFF",
            "SDL_JOYSTICK": "ON", "SDL_HIDAPI": "ON", "SDL_VIRTUAL_JOYSTICK": "ON",
            "SDL_HIDAPI_LIBUSB": "OFF", "SDL_LIBUDEV": "ON", "SDL_DBUS": "OFF",
            "SDL_IBUS": "OFF", "SDL_FCITX": "OFF", "SDL_ALSA": "OFF",
            "SDL_PULSEAUDIO": "OFF", "SDL_PIPEWIRE": "OFF", "SDL_JACK": "OFF",
            "SDL_WAYLAND": "OFF", "SDL_X11": "OFF", "SDL_KMSDRM": "OFF",
            "SDL_OPENGL": "OFF", "SDL_OPENGLES": "OFF", "SDL_VULKAN": "OFF",
            "PKG_CONFIG_EXECUTABLE": "/bin/false",
        }
        configure = ["cmake", "-S", str(source), "-B", str(work / "build"), "-G", "Ninja",
                     *wrappers, *[f"-D{key}={value}" for key, value in options.items()]]
        environment = {key: value for key, value in os.environ.items()
                       if key not in ("CC", "CXX", "CFLAGS", "CXXFLAGS", "CPPFLAGS", "LDFLAGS", "CPATH", "C_INCLUDE_PATH", "CPLUS_INCLUDE_PATH", "LIBRARY_PATH", "CMAKE_PREFIX_PATH")}
        environment.update(ZIG_GLOBAL_CACHE_DIR=str(ROOT / "zig-cache"),
                           ZIG_LOCAL_CACHE_DIR=str(work / "zig-cache"), SOURCE_DATE_EPOCH="1749168000")
        commands = [configure, ["cmake", "--build", str(work / "build"), "--parallel", str(jobs)]]
        library = work / "build" / "libControllerSupport-SDL2.so"
        with (ROOT / ("build-" + arch + ".log")).open("w") as log:
            for command in commands:
                log.write(shlex.join(command) + "\n")
                log.flush()
                subprocess.run(command, env=environment, stdout=log, stderr=subprocess.STDOUT, check=True)
            commands.append(strip_library(zig, library, environment, log))
        abi = inspect_elf(library, arch=arch)
        stage = ROOT / ARCH_STAGES[arch]
        stage.mkdir(exist_ok=True)
        output = stage / "libControllerSupport-SDL2.so"
        shutil.copyfile(library, output)
        shutil.copyfile(source / "LICENSE.txt", stage / "LICENSE-SDL2.txt")
        shutil.copyfile(source / "src/hidapi/LICENSE-bsd.txt", stage / "LICENSE-HIDAPI-BSD.txt")
        shutil.copyfile(source / "src/video/yuv2rgb/LICENSE", stage / "LICENSE-YUV2RGB.txt")
        shutil.copyfile(udev_include / "LICENSE-LIBUDEV.txt", stage / "LICENSE-LIBUDEV.txt")
        shutil.copyfile(zig.parent / "LICENSE", stage / "LICENSE-ZIG.txt")
        notices = ("LICENSE-SDL2.txt", "LICENSE-HIDAPI-BSD.txt", "LICENSE-YUV2RGB.txt", "LICENSE-LIBUDEV.txt", "LICENSE-ZIG.txt")
        (stage / "LICENSE.txt").write_text("\n\n".join(
            f"{name}\n\n{(stage / name).read_text()}" for name in notices))
        (stage / "libControllerSupport-SDL2.so.sha256").write_text(sha256(output) + "\n")
        receipt = {
            "sdl_version": SDL_VERSION, "source_url": SDL_URL, "source_sha256": SDL_SHA256,
            "source_hash_provenance": "Official libsdl-org/SDL release-2.32.10 GitHub asset digest",
            "compiler_url": ZIG_URL, "compiler_sha256": ZIG_SHA256,
            "compiler_hash_provenance": "https://ziglang.org/download/index.json (0.13.0/x86_64-linux)",
            "compiler_license": "Compiler executable/toolchain not redistributed; Zig root MIT LICENSE included conservatively for any emitted compiler-runtime helpers. SDL uses the target system's dynamically linked glibc, not a redistributed libc binary.",
            "license": "zlib; official SDL implementation, input-only build configuration",
            "cmake_customization": customization,
            "cmake_libudev_customization": udev_customization,
            "libudev_header_url": UDEV_HEADER_URL, "libudev_header_sha256": UDEV_HEADER_SHA256,
            "libudev_license_url": UDEV_LICENSE_URL, "libudev_license_sha256": UDEV_LICENSE_SHA256,
            "libudev_scope": "Official systemd v215 header declarations only; no implementation/binary copied. Runtime libudev.so.1 is optional via SDL's existing dlopen support.",
            "symbol_isolation": "Private SONAME libControllerSupport-SDL2.so; -Wl,-Bsymbolic (bind functions AND data locally; Zig 0.13 does not accept -Bsymbolic-functions)",
            "target": target, "cpu": ARCH_CPU[arch],
            "cmake_options": {key: value.replace(str(work), "/sdl2-build") for key, value in options.items()},
            "commands": [[argument.replace(str(work), "/sdl2-build") for argument in command] for command in commands],
            "library_sha256": sha256(output),
            "stripping": {"tool": "Pinned Zig 0.13.0 objcopy", "options": ["--strip-all"],
                          "scope": "Remove debug sections and static symbol table; dynamic exports checked after stripping",
                          "privacy_validation": "Full ELF bytes scanned before publication; no home paths, local username, absolute repository path or companion-workspace marker"},
            "license_sha256": sha256(stage / "LICENSE.txt"), "abi": abi,
            "additional_notices": {name: sha256(stage / name) for name in
                                   ("LICENSE-HIDAPI-BSD.txt", "LICENSE-YUV2RGB.txt", "LICENSE-LIBUDEV.txt", "LICENSE-ZIG.txt")},
            "steam_filtering": "Upstream defaults unchanged; no Steam hint overrides",
            "optional_runtime_dependencies": "libudev.so.1 dynamically loaded from runtime; libusb disabled; Linux evdev, hidraw and virtual enabled",
            "validation_scope": "ELF inspection only; not game/Deck acceptance or installation",
        }
        receipt_text = json.dumps(receipt, indent=2) + "\n"
        for private_path in (str(ROOT), str(Path.home()), str(work)):
            if private_path in receipt_text:
                raise ValueError("Refusing to publish a local path in provenance")
        (stage / "provenance.json").write_text(receipt_text)
        print(f"Staged {output}\nSHA256 {sha256(output)}\nGLIBC <= {abi['glibc_max']}; NEEDED: {', '.join(abi['needed'])}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--jobs", type=int, default=2)
    arguments = parser.parse_args()
    if arguments.jobs < 1:
        parser.error("--jobs must be positive")
    for arch in ARCHITECTURES:
        prepare(arguments.jobs, arch)


if __name__ == "__main__":
    main()
