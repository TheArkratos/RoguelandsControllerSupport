"""Authored ELF-only checks; never edit an installed game/runtime binary."""
from pathlib import Path
import struct
import tempfile
import unittest

from verify_legacy_mono import prepare_x86_host_stack


class LegacyHostTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.host = Path(temporary.name) / "authored-host"
        self.data = bytearray(84)
        self.data[:6] = b"\x7fELF\x01\x01"
        struct.pack_into("<H", self.data, 18, 3)
        struct.pack_into("<I", self.data, 28, 52)
        struct.pack_into("<HH", self.data, 42, 32, 1)
        struct.pack_into("<I", self.data, 52, 0x6474e551)
        struct.pack_into("<I", self.data, 76, 6)

    def reject(self):
        self.host.write_bytes(self.data)
        with self.assertRaises(RuntimeError):
            prepare_x86_host_stack(self.host)
        self.assertEqual(self.host.read_bytes(), self.data)

    def test_only_stack_flag_changes_and_repeated_call_is_idempotent(self):
        self.host.write_bytes(self.data)
        prepare_x86_host_stack(self.host)
        self.data[76] = 7
        self.assertEqual(self.host.read_bytes(), self.data)
        prepare_x86_host_stack(self.host)
        self.assertEqual(self.host.read_bytes(), self.data)

    def test_wrong_class_rejected_without_write(self):
        self.data[4] = 2
        self.reject()

    def test_wrong_machine_rejected_without_write(self):
        struct.pack_into("<H", self.data, 18, 62)
        self.reject()

    def test_truncated_headers_rejected_without_write(self):
        self.data.pop()
        self.reject()

    def test_missing_stack_header_rejected_without_write(self):
        struct.pack_into("<I", self.data, 52, 1)
        self.reject()


if __name__ == "__main__":
    unittest.main()
