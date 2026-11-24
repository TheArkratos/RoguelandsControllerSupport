# Mapped controller contract fixture

```bash
dotnet run --project tests/mapped_controller_repro/MappedControllerRepro.csproj -p:UseSharedCompilation=false
```

Deterministic offline adapter checks only: SDL2 logical snapshots map to the
existing canonical Xbox js tokens, BindingConfig defaults, frame edges, trigger
normalization, D-pad diagonal/rearm rules, initial/reconnected held suppression,
disconnect clearing, and LastInputSource keyboard takeover. Apply is called once
per simulated frame with six axes and fifteen buttons in SDL2 logical order.

D-pad snapshots are atomic: all twelve distinct cardinal-to-cardinal transitions
retain the original direction until a complete neutral snapshot. Every diagonal
remains ambiguous. Opposing button pairs (including all four buttons held) are
represented as an ambiguous diagonal, not neutral: they preserve an existing
latch and cannot emit a new cardinal action. An unlatched conflict may resolve to
a fresh unambiguous cardinal; an initial/reconnected conflict requires neutral
first, just like any initially held D-pad snapshot.

This is not a Steam Deck reproduction or a native SDL integration test. It does
not establish device discovery, Steam Input compatibility, physical controls,
Unity behavior, installation, persistence, or multiplayer acceptance.
