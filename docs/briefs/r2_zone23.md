# Round 2 package: service paths, Talon pen, Substation 3, Old Yard, Exit E-0, coast road

Clone: `~/CretaceaUnityClones/zone23`. Branch `fix/zone23`.
You own: `Assets/Editor/WorldBuilder/Zone2_*.cs`, `Zone3_*.cs`, `Zone23Dressing.cs`,
`Assets/Editor/LookPass/LookLights.Zone23.cs`, `LookSigns.cs`, `LookRoofs.cs` (zones 2–3), your materials.

Findings to fix (read `docs/review1/night_a.md` and `docs/review1/night_b.md`; yours are geometry, materials, signs
and lamps in these zones):
1. **Major — chain-link**: the lead changed `MAT_ChainlinkFence` to a blended material in `07fd73d` (far fences were
   solid dotted panels). Re-capture `19`, `20`, `21`, `16`, `26`, `27` and check every fence and gate reads as
   chain-link you can see through, near and far; fix posts, frames, the Paddock 4 gate leaf with its saturated glowing
   bar at the bottom edge. Requests for the material itself go to the lead.
2. **Major — glowing rods on the floor** (`19`, `20`, `21`): a straight red emissive rod across the pad like a laser
   tripwire, a diagonal one that turns into a neon-green tube. Conduits do not glow: make them real conduit/cable
   trays with small status LEDs or painted bands, and show the circuit state on the breaker cabinet, the pylons and
   the route bollards instead.
3. **Major — the E-0 exit sign is shredded** (`25`, `26`): "SERVICE EXIT" is an unreadable spray of green pixels,
   "E-0" and the arrow are broken into fragments. The target of the whole climax must be crisp at 20 m (a sign face
   with solid letters at a sane texture density, back-lit evenly, no alpha-tested hairlines; the kit also noted the
   sideways arrow over the gate).
4. **Major — escape-green emissives clip to neon** (`22`, `21`, `25`, `26`): bollard caps bloom into flat green
   pucks with pure-green pools, the nearest bollard shows two stacked green discs (the kit's collar lens plus your
   cap: keep one). The lead lowered the palette green to 12 nits and the bollard lamp to 170 cd; re-check and finish.
5. **Major — the loading dock** (`13_ch3_loading_dock`): the torch lands on a featureless pale-mint box (the
   dumpster) and a flat white wall panel with vines. Real dock dressing: a proper skip, pallets, crates, hoses, a
   roller door with ribs, bumpers, signs. (The lead fixed the black exposure there.)
6. **Major — the Old Yard is a blockout from the ending crane shot** (`28_ending_2`): empty slab, containers and a
   grey box as bare blocks, the workshop a yellow box with red trim, a plain tan perimeter wall with a flat grey door.
7. **Major — the run to the coast is a fenced concrete chute with nothing at the end** (`27_escape_coast_road`);
   the far half of the road is a saturated blue band like a tarp. Give the road rhythm (lamps or reflectors, kerbs,
   drains, signs) and a visible goal (the island package owns the sea view and the overlook itself).
8. **Major — the Paddock 4 gate** (`16`) and the dock/gate props named in the two files; minors for your zones.
Gameplay geometry is sacred (colliders, triggers, anchors, interactables). Run `route_test.py` before hand-in.
