# Round 2 package: Visitor Center and plaza

Clone: `~/CretaceaUnityClones/zone1`. Branch `fix/zone1`.
You own: `Assets/Editor/WorldBuilder/Zone1_VisitorCenterBuilder.cs`, `Assets/Editor/LookPass/LookLights.Zone1.cs`,
`Assets/Editor/LookPass/LookFixtures.cs` (lamp bodies; the kit package is finished), `LookRoofs.cs` for Zone 1 roofs,
your `MAT_Z1_*` materials.

Findings to fix (read `docs/review1/interiors.md`, `docs/review1/daylight.md` (plaza and building), the intro notes in
`docs/review1/intro.md` about the first room):
1. **BLOCKER — black band under every lit ceiling** (`breakroom_on`, `giftshop_on`, `office_on`, `lockers_on`,
   `corridor_on`): the top 0.5–1 m of each wall is near-black under a lit ceiling. The lead added a small shadowless
   wall-fill point light under each main lamp (`LookLights.EnsureWallFill`, share 0.11) — re-capture first. If the band
   is still there it is yours: wall material/lining at the top of the wall, troffer positions, wash lights
   (`_Wash` rule), or a brighter cornice. The ceiling and the top of the wall must read as one lit room.
2. **Major — rooms are oversized and nearly empty** ("test chambers"): security office 14x12 m with one desk, locker
   room with one locker row, break room 22x13 m with three tables, gift shop 22x20 m with one shelf of flat coloured
   boxes, records annex with one desk. Dress them like a 1990s park building: partitions, more furniture in groups,
   shelving, counters, notice boards with real paper, vending machines, plants, boxes, cables, bins, wall clocks,
   posters (use `Assets/Art` props and textures), without blocking the routes (prove with `route_test.py`).
3. **Major — break room and gift shop are sepia** (red/blue 1.7): the lead moved their lamps to 4000 K; fix the beige
   materials and brown ceilings so they match the office.
4. **Major — the lobby with the power on already looks blacked out**: only a floor pool and the fossil uplight; walls,
   banners, park logo and mezzanine unlit. Light the showpiece room (wall washers, mezzanine downlights, pendants,
   lit banners and reception), so the blackout has something to take away.
5. **Major — dead fixtures read as black wire grids** in the blackout: a switched-off troffer must read as an opal
   diffuser in the flashlight, not a black frame with a black cross.
6. **Major — control room**: the five window panes are a lit milky film (a window onto the night must be the darkest
   thing in a lit room); all six desk monitors are black while the radio tells the player to log the camera feeds
   (screens on with the power, dead in the blackout; pair them with the light group like the fixture lenses).
7. **Major — light leaks through the records desk** (amber pool on the floor under the desk from the beacon on top).
8. **Major — the collapse is one clean tilted concrete box** (`08_ch1_staff_exit_collapse`): rubble, broken ceiling
   grid, fallen tiles, hanging cable and duct, dust.
9. **Major — the first room's ceiling reads blue** (night-sky ambient on the ceiling between the troffers, intro
   frames 19–20): make the lamp light dominate (wash lights), the lead keeps the indoor ambient at 0.5.
10. **Major — plaza** (`GoldenDay_plaza_eye`, `NightCalm_plaza_eye`): an empty 131x64 m slab that reads as a parking
    lot; two dark 0.4 m strips across it; the slab's 0.3 m edge sits on the lawn like a box; the lamp post heads are
    dark (no glowing diffuser, the pool is centred on the foot of the pole: `Light_PlazaLamp` needs the lamp-post
    fixture family in `LookFixtures`), pools merge into one orange carpet. Make it a theme-park forecourt: planters
    and palms, a fountain or statue, ticket kiosks, flags and banners, queue rails, paving patterns, kerbs.
11. **Major — the Visitor Center reads as a glass shoebox**: no roofline or eaves, a tiny entrance canopy, nothing
    planted at its base, flat cream roof slabs from above.
12. Minors in those files for your rooms.
