# Aim-assist scope and integration regression

Runs production `ControllerAim`, `ControllerAimAssist`, geometry and lifecycle/IL
patch code against deterministic Unity/native doubles. No game, save, installed
profile, GPU or physical controller is used.

```bash
dotnet run --project Mods/ControllerSupport/tests/aim_assist_integration_repro/AimAssistIntegrationRepro.csproj
```

Checks acquisition only at a native attack-start seam, independent repeated
attacks, nearest eligible hitbox centers, unchanged visual aim, edge intersections
and clamped direction, delayed helper/projectile retention, sustained-target loss,
physical mouse takeover, native offensive-chip versus buff scopes, remote-player
exclusion, enemy ownership independence, body-center origin, stable ties,
segmented-boss and Hivemind damage forwarding, fresh same-frame gameplay guards,
freeze, toggle/reset and fail-closed IL layouts.

The native guard injection is checked using a synthetic metadata type with the
verified compiled iterator name and actual attacking-field instructions. Staff
spawn and iterator scope hooks are invoked directly; the fixture does not run
Unity RPCs or prove networking timing. Uncorrelated client-received staff
projectiles retain normal aim, rather than borrowing another activation. Native
registered custom weapon callbacks and arbitrary custom chip callbacks require
separate adapters; native weapon/chip paths are covered here.

The staff/lead follow-up also checks correlated local spawn ownership, independent
active staff ticks after release, target switching/loss/reacquisition, forward
fallback beyond the visible cursor, mouse takeover, actual projectile speed and
weapon-switch independence, partial velocity prediction and cyan cone clamping.
Ordinary delayed attacks still retain targets; tracking staves now reselect.

Red staff target glyph checks exercise the production marker module with native
mesh/material/scene doubles: per-projectile exact steering points, owned red tint
and original texture preservation, glyph-only cloning, scene rebuild and lifetime
cleanup. They verify state and geometry mapping, not real shader/GPU appearance.
