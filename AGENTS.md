# Controller Support development

Read README.md, STATUS.md and docs/ARCHITECTURE.md before implementation or
runtime testing. This is an independent mod repository. Builds reference the
installed native Linux game; some research fixtures need locally generated
references through ROGUELANDS_REFERENCE_DIR.

Keep input ownership, native action dispatch and held-item conservation explicit.
Extend shared menu contexts and lifecycle operations instead of adding a second
input loop or inventory model. Preserve native game/loader guards, random-call
behavior and mouse/keyboard restoration. Use focused regressions for behavioral
changes, then the repository test runner for changes to shared fixture wiring.

Run fixtures with -p:UseSharedCompilation=false, at most two concurrently.
The tools/run_tests.py runner enforces these settings. Offline checks do not prove
Unity rendering, physical controls, persistence or multiplayer. Report built,
installed and physically tested states separately; update STATUS.md without
including personal save contents, character names or local-machine paths.
Before publication, run `python3 -B tools/check_public_files.py --history` and
the Python tooling tests. The audit covers candidate/index paths and HEAD ancestry;
other refs and local evidence bundles remain separate from the public branch.

Never add saves, profiles, save backups, game assemblies, generated game source,
build outputs or runtime logs to Git. Preserve current saves and archived evidence
bytes. Historical copies are evidence, never automatic restore sources. Do not
launch unsolicited game sessions or inspect game processes. The user closes the
game before requesting runtime work; do not ask for repeated closure confirmation.
Before authorized installation or runtime testing, preserve a fresh byte-identical
current target save and prior installed package, and record their hashes privately.
Do not restore saves without the user's direction.

An optional local research workspace may retain ignored Reports/, Labs/, HISTORY.md,
feature notes and .local-research/ at their original paths. They are not required
for a standalone clone. Consult them locally when investigating earlier decisions;
do not alter archived evidence bytes or republish personal details. The companion
workspace may also provide GameReference, Shared tools and MODDING-HANDOFF.md.
Treat generated game source as read-only input, not an editable game project.

For complex multi-file work, delegate well-scoped execution to qwen-worker when
available, otherwise an available worker, then review and verify the result.
Preserve other contributors' edits and keep concurrent file ownership separate.
