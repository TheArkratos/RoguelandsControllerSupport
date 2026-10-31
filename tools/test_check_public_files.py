"""Synthetic publication cases; never inspect a real save or private profile."""
import contextlib
import importlib.util
import io
from pathlib import Path
import subprocess
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("public_audit", Path(__file__).with_name("check_public_files.py"))
AUDIT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(AUDIT)


class PublicationAuditTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="controller-public-audit-")
        self.addCleanup(self.temporary.cleanup)
        self.repo = Path(self.temporary.name)
        self.git("init", "-q")
        self.git("config", "user.name", "Fixture")
        self.git("config", "user.email", "fixture@example.invalid")
        self.write(".gitignore", "\n".join("/" + path for path in AUDIT.PROBES) + "\n")
        self.write("README.md", "Authored fixture project.\n")

    def git(self, *args):
        return subprocess.run(["git", "-C", str(self.repo), *args], check=True, capture_output=True)

    def write(self, name, content):
        path = self.repo / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content)
        return path

    def commit(self, message="fixture"):
        self.git("add", ".")
        self.git("commit", "-qm", message)

    def issues(self, history=False):
        return AUDIT.audit(self.repo, history)[0]

    def test_clean_repo_and_ignored_private_path_are_not_read(self):
        self.write("nested/profile/state.txt", "synthetic private placeholder")
        self.assertEqual([], self.issues())

    def test_missing_ignore_protection_fails(self):
        self.write(".gitignore", "")
        self.assertTrue(any("missing ignore protection" in issue for issue in self.issues()))

    def test_force_staged_private_path_is_rejected_before_read(self):
        path = "nested/profile/state.txt"
        self.write(path, "synthetic placeholder")
        self.git("add", "-f", path)
        original = Path.read_bytes
        def guarded_read(target):
            self.assertNotEqual(target, self.repo / path, "prohibited contents were read")
            return original(target)
        from unittest.mock import patch
        with patch.object(Path, "read_bytes", guarded_read):
            self.assertTrue(any("prohibited candidate path" in issue for issue in self.issues()))

    def test_staged_secret_cannot_hide_behind_clean_worktree(self):
        secret = "ghp_" + "A" * 40
        self.write("settings.txt", secret)
        self.git("add", "settings.txt")
        self.write("settings.txt", "clean working copy")
        issues = self.issues()
        self.assertTrue(any("GitHub token: index file" in issue for issue in issues))
        self.assertFalse(any(secret in issue for issue in issues))

    def test_removed_historical_private_path_still_fails(self):
        path = "Reports/receipt.json"
        self.write(path, "synthetic placeholder")
        self.git("add", "-f", path)
        self.commit("historical private path")
        self.git("rm", path)
        self.commit("remove private path")
        self.assertEqual([], self.issues())
        self.assertTrue(any("prohibited HEAD history path" in issue for issue in self.issues(True)))

    def test_history_ignores_other_refs_but_scans_head_blobs(self):
        self.commit("clean base")
        self.git("checkout", "-qb", "unpublished-research")
        self.write("settings.txt", "ghp_" + "B" * 40)
        self.commit("old secret fixture")
        self.git("checkout", "-q", "-")
        self.assertEqual([], self.issues(True))
        self.write("settings.txt", "ghp_" + "C" * 40)
        self.commit("HEAD secret fixture")
        self.write("settings.txt", "clean")
        self.commit("remove HEAD secret")
        self.assertTrue(any("GitHub token: HEAD history" in issue for issue in self.issues(True)))

    def test_unignored_home_path_and_cli_do_not_echo_value(self):
        private_path = "/home/" + "synthetic-user/private-file"
        self.write("notes.txt", private_path)
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            code = AUDIT.main(["--repo", str(self.repo)])
        self.assertEqual(1, code)
        self.assertIn("personal home path", output.getvalue())
        self.assertNotIn(private_path, output.getvalue())

    def test_prohibited_names_and_normal_source(self):
        for path in ("SAVE/state.txt", "sub/PROFILE/state.txt", "Labs/tool.py", "HISTORY.md",
                     "PLAYERprefs.backup", "native.DLL", "save.bak2"):
            self.assertTrue(AUDIT.prohibited(path), path)
        for path in ("src/InventoryItemLifecycle.cs", "tests/SaveHandlingTests.cs", ".gitignore"):
            self.assertFalse(AUDIT.prohibited(path), path)


if __name__ == "__main__":
    unittest.main()
