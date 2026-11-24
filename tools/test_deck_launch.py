"""The public Deck installation must not require changing Steam's executable."""
from pathlib import Path
import unittest


class DeckLaunchTests(unittest.TestCase):
    def setUp(self):
        self.guide = (Path(__file__).resolve().parents[1] / "docs/STEAM-DECK-TEST.md").read_text()

    def test_normal_steam_launch_without_shell_override(self):
        self.assertIn("Leave Steam Launch Options empty", self.guide)
        self.assertNotIn("%command%", self.guide)
        self.assertNotIn("bash -c", self.guide)

    def test_both_native_process_architectures_documented(self):
        self.assertIn("32-bit (x86)", self.guide)
        self.assertIn("64-bit (x86_64)", self.guide)

    def test_mod_diagnostics_log_named(self):
        self.assertIn("GadgetCore/Logs/Controller Support.log", self.guide)


if __name__ == "__main__":
    unittest.main()
