# Round 3 package: service paths, substation, yard, E-0, coast road

Clone: `~/CretaceaUnityClones/zone23`. Branch `r3/zone23`.
You own: `Assets/Editor/WorldBuilder/Zone2_*.cs`, `Zone3_*.cs`, `Zone23Dressing.cs`,
`Assets/Editor/LookPass/LookLights.Zone23.cs`, `LookSigns.cs`, `LookRoofs.cs` (zones 2–3), your materials.

Read `docs/review2/night_b.md`, `docs/review2/night_a.md` and the yard item of `docs/review2/daylight.md`.
The lead is changing, in parallel: the chain-link material (darker wire, lower alpha: the gates read as frosted glass),
lightning brightness, flood volumetrics. Your part:
1. **Major — the service path is a canyon of bare 5 m precast slabs** (`14`, `15`, `16`): three blank walls fill 70 %
   of the frame with one vine. Break them up: panel joints and tie holes, stains and moss lines, cable trays, pipes,
   signs, lamps with brackets, drains, kerbs, planting in gaps, a lower wall with fence on top in places.
2. **Major — the lamp post beside the breaker cabinet is mis-assembled** (`20_ch5_generator_running`): a wall-mount
   caged bulkhead on top of a pole, its wall plate and bracket hanging in mid-air, the glow a separate orange ring
   round the pole. Build a proper pole-top or bracket fixture (the fixture bodies are made in `LookFixtures.cs`, which
   the Visitor Center package owns this round: build yours in the zone builder or `Zone23Dressing` and name the light
   so the fixture step skips it, or request the change).
3. **Major — the aux-power flood is a blown-out white card** (`21_ch5_aux_power_floods`): the housing, fins and lens do
   not read (the lead lowers flood haze and lens brightness; make sure the head model itself has visible structure).
4. **Major — the stacked shipping containers read as boxes clad in planks** (`GoldenDay_yard_eye`): flat 10 cm boards
   with dark gaps, no corrugation shading, a smeared streak band along the top. Use real container geometry
   (corrugated sides, corner posts, door bars) or a proper corrugated material with normal detail.
5. **Major — the concrete slab at the yard entry is bone-dry and matte in a downpour** (`23_ch6_yard_entry`) while the
   asphalt beside it mirrors the lamp: check it is in the wetness system (material, static flags, merge group).
6. **Major — the coast road shows no coast** (`27_escape_coast_road`): both sides are tan fences, the end is a dark band
   with a tiny green sign and a red flare; the sea is never seen. Open the view: lower or stop the fence toward the
   overlook, a visible lit goal, the sea horizon.
7. Minors for your zones in those files, if cheap.
Run `route_test.py` and `verify_playmode_doors` before hand-in.
