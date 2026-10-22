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
