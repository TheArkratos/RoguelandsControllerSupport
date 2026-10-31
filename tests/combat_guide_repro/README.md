# Combat guide fixture

Run from the mod root:

```bash
dotnet run --project tests/combat_guide_repro/CombatGuideRepro.csproj -p:UseSharedCompilation=false
```

This fixture runs production guide and cone geometry code against authored
synthetic Unity stubs. Its source plane is a generic two-triangle quad, with
ideal half-turn facing quaternions, a backface-culling material and representative
positive/negative camera rolls. No asset exports or local research reports are
read or bundled.

Checks cover facing/winding, single-fan cone triangulation, rotated viewport
clipping, material/mesh reuse, guide visibility, input ownership and restoration
of the source bars. Both horizontal-first and vertical-first source ordering
remain covered. Optional `--facing-staff` and `--facing-ray` arguments run focused
facing checks after the synthetic camera-roll cases.

These checks establish behavior for synthetic geometry and state. They do not
verify the installed game's actual mesh, material properties, animation keys,
camera settings or dialogue transitions. Historical native audits remain local
research evidence; physical rendering acceptance is separate.
