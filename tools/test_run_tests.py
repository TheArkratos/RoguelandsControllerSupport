#!/usr/bin/env python3
"""Synthetic runner checks; do not invoke dotnet or touch game files."""

import contextlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import run_tests


class RunnerTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.project = self.root / "tests/fixture/Test.csproj"
        self.project.parent.mkdir(parents=True)
        self.managed, self.reference = self.root / "managed", self.root / "reference"

    def classify(self, xml, program=""):
        self.project.write_text(xml)
        (self.project.parent / "Program.cs").write_text(program)
        return run_tests.classify(self.project, self.root, self.managed, self.reference)

    def test_pure_fixture(self):
        self.assertEqual(self.classify("<Project />").categories, ("pure",))

    def test_declared_generated_and_installed_references(self):
        fixture = self.classify('''<Project><ItemGroup>
          <Compile Include="$(RoguelandsReferenceDir)/Assembly-CSharp/ActionCursor.cs" />
          <Reference Include="Harmony"><HintPath>$(RoguelandsManagedDir)/0Harmony.dll</HintPath></Reference>
        </ItemGroup></Project>''')
        self.assertEqual(fixture.categories, ("generated-source", "installed-assemblies"))
        self.assertIn(self.reference / "Assembly-CSharp/ActionCursor.cs", fixture.required_files)
        self.assertIn(self.managed / "0Harmony.dll", fixture.required_files)

    def test_runtime_il_dependencies(self):
        fixture = self.classify('<Project><Compile Include="Program.cs" /></Project>', '''
          LocalReferences.Get("RoguelandsManagedDir");
          LocalReferences.Get("RoguelandsReferenceDir");
          Path.Combine(root, "build", "ControllerSupport.dll");''')
        self.assertEqual(fixture.categories, ("built-mod", "generated-source", "installed-assemblies"))
        self.assertIn(self.root / "build/ControllerSupport.dll", fixture.required_files)
        self.assertIn(self.reference / "GadgetCore/GadgetCore.Patches/Patch_GameScript_Update.cs", fixture.required_files)

    def test_generator_needs_original_game_source(self):
        fixture = self.classify('<Project><RequiresRoguelandsReference>true</RequiresRoguelandsReference></Project>')
        self.assertEqual(fixture.required_files, (self.reference / "Assembly-CSharp/GameScript.cs",))

    def test_discovery_ignores_build_cache(self):
        self.project.write_text("<Project />")
        cache = self.project.parent / "obj/Generated.csproj"
        cache.parent.mkdir()
        cache.write_text("<Project />")
        fixtures = run_tests.discover(self.root, self.managed, self.reference)
        self.assertEqual([fixture.project for fixture in fixtures], [self.project])

    def test_nonzero_exit_is_failure_and_log_is_saved(self):
        fixture = self.classify("<Project />")
        with patch.object(run_tests.subprocess, "run") as invoke:
            invoke.return_value.returncode = 7
            result = run_tests.run_fixture(fixture, self.root, self.root / "logs", "dotnet")
        self.assertEqual(result["status"], "FAIL")
        self.assertEqual(result["returncode"], 7)
        self.assertIn("-p:UseSharedCompilation=false", invoke.call_args.args[0])
        self.assertTrue(Path(result["log"]).is_file())

    def test_aggregation_keeps_skips_separate(self):
        summary = run_tests.summarize([
            {"project": "a", "status": "PASS"}, {"project": "b", "status": "FAIL"},
            {"project": "c", "status": "SKIP"}])
        self.assertEqual((summary["total"], summary["pass"], summary["fail"], summary["skip"]), (3, 1, 1, 1))

    def test_main_reports_missing_prerequisite_as_unsuccessful_skip(self):
        self.project.write_text('''<Project><Reference Include="Harmony">
          <HintPath>$(RoguelandsManagedDir)/0Harmony.dll</HintPath>
        </Reference></Project>''')
        output = self.root / "results"
        with patch.object(run_tests, "ROOT", self.root), \
                patch.object(run_tests.shutil, "which", return_value="unused-dotnet"), \
                patch.object(run_tests, "paths_for", return_value=(self.managed, self.reference)), \
                contextlib.redirect_stdout(io.StringIO()):
            code = run_tests.main(["--output-dir", str(output)])
        summary = json.loads((output / "summary.json").read_text())
        self.assertEqual(code, 1)
        self.assertEqual((summary["pass"], summary["fail"], summary["skip"]), (0, 0, 1))

    def test_main_aggregates_execution_failures(self):
        self.project.write_text("<Project />")
        output = self.root / "results"
        result = {"project": "tests/fixture/Test.csproj", "status": "FAIL",
                  "seconds": 0.0, "returncode": 7, "log": "synthetic.log"}
        with patch.object(run_tests, "ROOT", self.root), \
                patch.object(run_tests.shutil, "which", return_value="unused-dotnet"), \
                patch.object(run_tests, "run_fixture", return_value=result), \
                contextlib.redirect_stdout(io.StringIO()):
            code = run_tests.main(["--output-dir", str(output)])
        summary = json.loads((output / "summary.json").read_text())
        self.assertEqual(code, 1)
        self.assertEqual((summary["pass"], summary["fail"], summary["skip"]), (0, 1, 0))


if __name__ == "__main__":
    unittest.main()
