"""Fast package-contract checks; no Mono, SDL, downloads or installed game needed."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
import zipfile
from unittest.mock import patch

import check_deck_package as checker


class DeckPackageTests(unittest.TestCase):
    def package(self, change=None):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        path = Path(temporary.name) / "candidate.zip"
        receipt = {"sdl_version": checker.SDL_VERSION, "source_sha256": checker.SDL_SHA256,
                   "license_sha256": hashlib.sha256(b"authored license").hexdigest(),
                   "library_sha256": hashlib.sha256(b"authored ELF fixture").hexdigest()}
        contents = {name: b"authored fixture" for name in checker.FILES}
        contents["Manifest.ini"] = b"[Metadata]\nAssembly=ControllerSupport.dll\n"
        for arch in checker.ARCHES:
            prefix = "SDL2/linux-" + arch + "/"
            contents[prefix + "LICENSE.txt"] = b"authored license"
            contents[prefix + "provenance.json"] = json.dumps({
                **receipt, "target": arch + "-linux-gnu.2.17"}).encode()
        if change:
            change(contents)
        with zipfile.ZipFile(path, "w") as package:
            package.writestr("SDL2/", b"")
            for name, data in contents.items():
                package.writestr(name, data)
        return path

    def test_expected_contents_accepted(self):
        receipt, dll = checker.inspect_package(self.package())
        for arch in checker.ARCHES:
            self.assertEqual(receipt[arch]["sdl_version"], checker.SDL_VERSION)
        self.assertEqual(dll, b"authored fixture")

    def test_native_libs_scan_entry_rejected(self):
        path = self.package(lambda files: files.update({"Libs/native.so": b"not managed"}))
        with self.assertRaisesRegex(ValueError, "contents"):
            checker.inspect_package(path)

    def test_missing_guide_rejected(self):
        with self.assertRaisesRegex(ValueError, "contents"):
            checker.inspect_package(self.package(lambda files: files.pop("STEAM-DECK-TEST.md")))

    def test_wrong_manifest_assembly_rejected(self):
        path = self.package(lambda files: files.update({"Manifest.ini": b"[Metadata]\nAssembly=Other.dll\n"}))
        with self.assertRaisesRegex(ValueError, "Manifest"):
            checker.inspect_package(path)

    def test_changed_license_rejected(self):
        path = self.package(lambda files: files.update({"SDL2/linux-x86/LICENSE.txt": b"wrong"}))
        with self.assertRaisesRegex(ValueError, "license checksum"):
            checker.inspect_package(path)

    def test_wrong_source_provenance_rejected(self):
        path = self.package(lambda files: files.update({"SDL2/linux-x86_64/provenance.json": json.dumps({
            "sdl_version": "0.0.0", "source_sha256": checker.SDL_SHA256}).encode()}))
        with self.assertRaisesRegex(ValueError, "provenance"):
            checker.inspect_package(path)

    def test_top_level_metadata_rejected(self):
        with self.assertRaisesRegex(ValueError, "contents"):
            checker.inspect_package(self.package(lambda files: files.update({"SDL2/LICENSE.txt": b"old"})))

    def test_each_architecture_metadata_required(self):
        for arch in checker.ARCHES:
            with self.subTest(arch=arch), self.assertRaisesRegex(ValueError, "contents"):
                checker.inspect_package(self.package(lambda files: files.pop("SDL2/linux-" + arch + "/provenance.json")))

    def test_wrong_target_rejected(self):
        def change(files):
            name = "SDL2/linux-x86/provenance.json"
            receipt = json.loads(files[name])
            receipt["target"] = "x86_64-linux-gnu.2.17"
            files[name] = json.dumps(receipt).encode()
        with self.assertRaisesRegex(ValueError, "target"):
            checker.inspect_package(self.package(change))

    def resource_directory(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        directory = Path(temporary.name)
        receipts, _ = checker.inspect_package(self.package())
        for arch in checker.ARCHES:
            (directory / ("linux-" + arch + ".so")).write_bytes(b"authored ELF fixture")
        return directory, receipts

    def test_both_actual_resources_inspected_with_correct_arch(self):
        directory, receipts = self.resource_directory()
        with patch.object(checker, "inspect_elf", return_value={}) as inspect, patch.object(checker, "verify_binary_privacy") as privacy:
            checker.inspect_resources(directory, receipts)
        self.assertEqual([call.kwargs["arch"] for call in inspect.call_args_list], list(checker.ARCHES))
        self.assertEqual(privacy.call_count, 2)

    def test_resource_provenance_hash_mismatch_rejected(self):
        directory, receipts = self.resource_directory()
        (directory / "linux-x86.so").write_bytes(b"wrong bytes")
        with self.assertRaisesRegex(ValueError, "provenance"):
            checker.inspect_resources(directory, receipts)

    def test_missing_actual_resource_rejected(self):
        directory, receipts = self.resource_directory()
        (directory / "linux-x86.so").unlink()
        with self.assertRaisesRegex(ValueError, "Missing actual"):
            checker.inspect_resources(directory, receipts)

    def test_resource_privacy_rejection_propagates(self):
        directory, receipts = self.resource_directory()
        with patch.object(checker, "verify_binary_privacy", side_effect=ValueError("private marker")):
            with self.assertRaisesRegex(ValueError, "private"):
                checker.inspect_resources(directory, receipts)

    def test_resource_architecture_rejection_propagates(self):
        directory, receipts = self.resource_directory()
        with patch.object(checker, "verify_binary_privacy"), patch.object(checker, "inspect_elf", side_effect=ValueError("wrong architecture")):
            with self.assertRaisesRegex(ValueError, "architecture"):
                checker.inspect_resources(directory, receipts)


if __name__ == "__main__":
    unittest.main()
