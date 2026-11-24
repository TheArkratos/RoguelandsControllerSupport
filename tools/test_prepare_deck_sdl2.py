"""Network-free source-download/extraction safeguards for the Deck SDL builder."""

import hashlib
import io
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest import mock

import prepare_deck_sdl2 as builder


class PreparationSafetyTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)

    def archive(self, entries):
        path = self.root / "source.tar.gz"
        with tarfile.open(path, "w:gz") as archive:
            for name, kind, value in entries:
                member = tarfile.TarInfo(name)
                member.type = kind
                if kind == tarfile.REGTYPE:
                    data = value.encode()
                    member.size = len(data)
                    archive.addfile(member, io.BytesIO(data))
                else:
                    member.linkname = value
                    archive.addfile(member)
        return path, builder.sha256(path)

    def test_extract_valid_source_and_internal_link(self):
        path, digest = self.archive([
            ("SDL/src.c", tarfile.REGTYPE, "source"),
            ("SDL/link.c", tarfile.SYMTYPE, "src.c"),
        ])
        destination = self.root / "extracted"
        builder.extract(path, digest, destination)
        self.assertEqual((destination / "SDL/link.c").read_text(), "source")

    def test_hash_is_checked_before_tar_is_opened(self):
        path = self.root / "not-a-tar"
        path.write_text("corrupted download")
        with mock.patch.object(tarfile, "open") as opened:
            with self.assertRaisesRegex(ValueError, "SHA-256 mismatch"):
                builder.extract(path, "0" * 64, self.root / "out")
            opened.assert_not_called()
        self.assertFalse((self.root / "out").exists())

    def test_unsafe_archives_rejected_before_extraction(self):
        for name, kind, value in [
            ("../escape", tarfile.REGTYPE, "bad"),
            ("/absolute", tarfile.REGTYPE, "bad"),
            ("SDL/link", tarfile.SYMTYPE, "../../escape"),
            ("SDL/hard", tarfile.LNKTYPE, "../escape"),
            ("SDL/device", tarfile.CHRTYPE, ""),
            ("SDL/fifo", tarfile.FIFOTYPE, ""),
        ]:
            with self.subTest(name=name):
                path, digest = self.archive([(name, kind, value)])
                with self.assertRaises((ValueError, tarfile.FilterError)):
                    builder.extract(path, digest, self.root / "out")
                self.assertFalse((self.root / "out").exists())

    def test_duplicate_entry_rejected(self):
        path, digest = self.archive([
            ("same", tarfile.REGTYPE, "one"),
            ("same", tarfile.REGTYPE, "two"),
        ])
        with self.assertRaisesRegex(ValueError, "Unsafe archive entry"):
            builder.extract(path, digest, self.root / "out")

    def test_existing_extraction_not_reused(self):
        path, digest = self.archive([("a", tarfile.REGTYPE, "b")])
        with self.assertRaisesRegex(ValueError, "must not exist"):
            builder.extract(path, digest, self.root)

    def response(self, data, url="https://official.example/source"):
        response = io.BytesIO(data)
        response.geturl = lambda: url
        return response

    def test_download_checks_hash_then_publishes(self):
        data = b"pinned bytes"
        destination = self.root / "source"
        with mock.patch.object(builder.urllib.request, "urlopen", return_value=self.response(data)):
            builder.download("https://official.example/source", hashlib.sha256(data).hexdigest(), destination)
        self.assertEqual(destination.read_bytes(), data)
        self.assertEqual(list(self.root.iterdir()), [destination])

    def test_bad_download_not_published_and_temporary_removed(self):
        with mock.patch.object(builder.urllib.request, "urlopen", return_value=self.response(b"wrong")):
            with self.assertRaisesRegex(ValueError, "SHA-256 mismatch"):
                builder.download("https://official.example/source", "0" * 64, self.root / "source")
        self.assertEqual(list(self.root.iterdir()), [])

    def test_failed_download_cleans_temporary(self):
        with mock.patch.object(builder.urllib.request, "urlopen", side_effect=OSError("offline")):
            with self.assertRaises(OSError):
                builder.download("https://official.example/source", "0" * 64, self.root / "source")
        self.assertEqual(list(self.root.iterdir()), [])

    def test_cached_download_reverified_without_network(self):
        destination = self.root / "source"
        destination.write_bytes(b"tampered")
        with mock.patch.object(builder.urllib.request, "urlopen") as opened:
            with self.assertRaisesRegex(ValueError, "SHA-256 mismatch"):
                builder.download("https://official.example/source", "0" * 64, destination)
            opened.assert_not_called()

    def test_plaintext_url_rejected(self):
        with self.assertRaisesRegex(ValueError, "HTTPS"):
            builder.download("http://example/source", "0" * 64, self.root / "source")

    def test_plaintext_redirect_rejected(self):
        with mock.patch.object(builder.urllib.request, "urlopen", return_value=self.response(b"x", "http://example/source")):
            with self.assertRaisesRegex(ValueError, "HTTPS"):
                builder.download("https://example/source", "0" * 64, self.root / "source")
        self.assertEqual(list(self.root.iterdir()), [])

    def test_binary_privacy_checks_raw_bytes_including_nontext_sections(self):
        library = self.root / "library.so"
        for marker in (b"/home/" + b"other-person/work", b"/Users/" + b"someone/work",
                       b"private-test-user", b"private-workspace-name"):
            with self.subTest(marker=marker):
                library.write_bytes(b"\x7fELF\x00\xff" + marker + b"\x00")
                with mock.patch.object(builder.getpass, "getuser", return_value="private-test-user"):
                    with self.assertRaisesRegex(ValueError, "private path/identity marker"):
                        builder.verify_binary_privacy(library, ["private-workspace-name"])

    def test_clean_binary_privacy_passes(self):
        library = self.root / "library.so"
        library.write_bytes(b"\x7fELF\x00SDL_GameControllerOpen\x00")
        builder.verify_binary_privacy(library)

    def test_stripping_uses_pinned_zig_before_replacing_library(self):
        library = self.root / "library.so"
        library.write_bytes(b"unstripped")
        stripped = library.with_name("library.so.stripped")

        def objcopy(command, **kwargs):
            self.assertEqual(command, ["/verified-pinned-zig", "objcopy", "--strip-all", str(library), str(stripped)])
            self.assertEqual(library.read_bytes(), b"unstripped")
            stripped.write_bytes(b"\x7fELF\x00clean-stripped-library")

        with mock.patch.object(builder.subprocess, "run", side_effect=objcopy):
            command = builder.strip_library(Path("/verified-pinned-zig"), library, {}, io.StringIO())
        self.assertIn("--strip-all", command)
        self.assertEqual(library.read_bytes(), b"\x7fELF\x00clean-stripped-library")
        self.assertFalse(stripped.exists())

    def test_stripping_refuses_private_result_before_publication(self):
        library = self.root / "library.so"
        library.write_bytes(b"original")

        def objcopy(command, **kwargs):
            Path(command[-1]).write_bytes(b"\x7fELF\x00/home/" + b"private-user/work")

        with mock.patch.object(builder.subprocess, "run", side_effect=objcopy):
            with self.assertRaisesRegex(ValueError, "private path/identity marker"):
                builder.strip_library(Path("/verified-pinned-zig"), library, {}, io.StringIO())
        self.assertEqual(library.read_bytes(), b"original")

    def test_objcopy_failure_cannot_publish_unstripped_library(self):
        library = self.root / "library.so"
        library.write_bytes(b"original")
        with mock.patch.object(builder.subprocess, "run", side_effect=OSError("objcopy failed")):
            with self.assertRaises(OSError):
                builder.strip_library(Path("/verified-pinned-zig"), library, {}, io.StringIO())
        self.assertEqual(library.read_bytes(), b"original")

    def elf_outputs(self, arch="x86_64"):
        return [
            "Class: ELF32\nMachine: Intel 80386\n" if arch == "x86" else "Class: ELF64\nMachine: Advanced Micro Devices X86-64\n",
            "(NEEDED) [libc.so.6]\n(SONAME) [libControllerSupport-SDL2.so]\n(FLAGS) SYMBOLIC\n",
            "GLIBC_2.2.5 GLIBC_2.17",
            "SDL_GameControllerOpen SDL_InitSubSystem SDL_WasInit SDL_PollEvent "
            "SDL_JoystickAttachVirtual SDL_JoystickDetachVirtual SDL_JoystickOpen "
            "SDL_JoystickClose SDL_JoystickSetVirtualAxis SDL_JoystickSetVirtualButton "
            "SDL_JoystickEventState SDL_GameControllerEventState SDL_QuitSubSystem "
            "SDL_GameControllerUpdate SDL_NumJoysticks SDL_IsGameController "
            "SDL_GameControllerGetAttached SDL_GameControllerGetAxis SDL_GameControllerGetButton "
            "SDL_GameControllerClose SDL_GetError",
        ]

    def test_elf_portable_private_library_accepted(self):
        with mock.patch.object(builder.subprocess, "check_output", side_effect=self.elf_outputs()):
            result = builder.inspect_elf(self.root / "library")
        self.assertEqual(result["needed"], ["libc.so.6"])
        self.assertEqual(result["glibc_max"], "2.17")

    def test_elf_x86_accepted(self):
        with mock.patch.object(builder.subprocess, "check_output", side_effect=self.elf_outputs("x86")):
            result = builder.inspect_elf(self.root / "library", arch="x86")
        self.assertEqual(result["arch"], "x86")
        self.assertEqual(result["needed"], ["libc.so.6"])
        self.assertEqual(result["glibc_max"], "2.17")

    def test_elf_rejects_cross_architecture_and_wrong_machine(self):
        for arch in ("x86", "x86_64"):
            for header in (self.elf_outputs("x86_64" if arch == "x86" else "x86")[0],
                           "Class: ELF32\nMachine: PowerPC\n"):
                with self.subTest(arch=arch, header=header):
                    outputs = self.elf_outputs(arch)
                    outputs[0] = header
                    with mock.patch.object(builder.subprocess, "check_output", side_effect=outputs):
                        with self.assertRaises(ValueError):
                            builder.inspect_elf(self.root / "library", arch=arch)

    def test_elf_rejects_foreign_architecture_linker(self):
        for arch, linker in (("x86", "ld-linux-x86-64.so.2"), ("x86_64", "ld-linux.so.2")):
            with self.subTest(arch=arch):
                outputs = self.elf_outputs(arch)
                outputs[1] += "(NEEDED) [" + linker + "]\n"
                with mock.patch.object(builder.subprocess, "check_output", side_effect=outputs):
                    with self.assertRaisesRegex(ValueError, "Unexpected native dependencies"):
                        builder.inspect_elf(self.root / "library", arch=arch)

    def test_unknown_elf_architecture_rejected_without_readelf(self):
        with mock.patch.object(builder.subprocess, "check_output") as readelf:
            with self.assertRaisesRegex(ValueError, "Unsupported ELF architecture"):
                builder.inspect_elf(self.root / "library", arch="arm64")
            readelf.assert_not_called()

    def test_elf_rejects_new_glibc_desktop_deps_and_missing_isolation(self):
        for index, replacement in [
            (0, "Class: ELF32\nMachine: Intel 80386"),
            (1, "(NEEDED) [libX11.so.6]"),
            (1, "(NEEDED) [libc.so.6]\n(SONAME) [libSDL2-2.0.so.0]"),
            (2, "GLIBC_2.17 GLIBC_2.34"),
            (2, "GLIBC_2.17 GLIBC_PRIVATE"),
            (3, "SDL_InitSubSystem"),
        ]:
            with self.subTest(replacement=replacement):
                outputs = self.elf_outputs()
                outputs[index] = replacement
                with mock.patch.object(builder.subprocess, "check_output", side_effect=outputs):
                    with self.assertRaises(ValueError):
                        builder.inspect_elf(self.root / "library")


if __name__ == "__main__":
    unittest.main()
