# Round 5 hand-in: island, second pass (`r5/island2`)

Base `de6e8d9`. Captures: `Screenshots/handin_r5/` (INDEX.md; `_a` = base build in this clone, `_b` = final build,
same cameras). Measurements: `Tools/agent/r5_sat.py <png> x0,y0,x1,y1` (mean sRGB, mean pixel saturation, hue of the
mean, luminance mean/std, G/R).

No animation work in this package (vegetation is static), so no motion clips: every claim below is a still pair.

## Goal 1: dusk hill mosaic — NOT MET (partly improved)
What I did:
- `LookVegetationImpostors.SaveMesh`: the crown cap is now the critic's no-shader fallback: a 10-point cone, centre
  capY-0.10h, rim at 0.55-0.70 of the footprint, droop 0.30-0.40h, rim normals lean 0.25 (was 0.55); plus two more
  leaf cards at 45 deg (4 cards). 18 triangles per impostor instead of 20.
- A view-elevation fade without a shader graph: the hill renderer (`InstancedFoliageRenderer`) now draws the cap
  only where the camera looks down on the crowns (`capElevation`, tan 0.10, per far cell / per near instance); from
  eye level the hill uses a capless copy of the impostor (`<stem>_ImpostorCards.asset`, `<stem>_Inst_LOD*_Cards`).
  Wired for 12 of 57 levels (4 species x 3 hill variants), checked by eval.
Evidence: `17_hill_crop_2x_a_left_b_right.png`, `01_DuskTransition_meadow_eye_a/b`. Hill box (150,190)-(420,260):
luminance std 0.161 -> 0.160, hue 58 -> 63. The faceted look is still there: with the caps gone from eye level the
lit plates remain, so they are the crossed leaf cards themselves (hard alpha-tested crown silhouettes, each card lit
on its sun side), not the cap. I also tried leaning the hill copies' card normals 60 % to the sky (one sun-angle for
every crown): std rose to 0.170, so I reverted it.
Aerial condition holds: `GoldenDay_aerial_sw` far canopy (0,0)-(500,250) luminance 0.315 -> 0.313, hue 80 -> 81.
What would fix it: softer crown silhouettes in the hill atlas (alpha dither / blurred coverage at the crown edge) or a
real impostor shader with a lighting term that does not follow the card normal; not done in the time box.

## Goal 2: valley band from the air — PARTLY MET
- New `LookVegetationImpostors.BuildGroundBlob` + `PaintGroundBlob`: a soft dark ground patch (dithered, lobed,
  0.8 x crown footprint, 0.25 m over the base) in the spare upper half of the atlas's third column, one quad on the
  impostor level of the play-area LODGroups only (skipped for the hill renderer in `InstancedLods`). It is drawn at
  any range, so the band past the 260 m shadow distance has shade pools (`05_GoldenDay_valley_air_a/b`, `19_pairs_air`).
- `OpenValleyGrade`: hue pull 0.45 -> 0.75 towards 97 deg, saturation x1.4 (cap 0.70), value cap 0.62.
- Shadow crown `staticShadowCaster = false` (critic).
Band (0,140)-(1280,330) mean: G/R 1.07 -> 1.08, hue 68 -> 69 (lawn-dominated; I did not run the k-means crown
cluster measure, so crown G/R >= 1.15 is NOT verified). The later switch (0.05) was not retried. Dawn meadow trees
(`09_DawnCoast_aerial_sw`, box (400,250)-(900,500)) are still hue 51: "no brown trees at dawn" is NOT met.

## Goal 3: tall grass — PARTLY MET
`LookVegetationDetails`: main stand texels x0.42 saturation (was x0.72) with lighter tips; main and dark looks get
their own tint (0.84, 1.0, 1.12) instead of the shared (0.72, 1.04, 1.2); dry look only turns the top of the blade
(tip SS(0.65,0.95)) to a pale seed head (sat 0.30, hue 0.10); dry stand max height 1.7 -> 1.3; near the road the
tall density falls by 0.9 (was 0.75), so the right front breaks into tufts. Evidence: `06/07_*_a/b`, `18_pairs_eye`
(day, dusk, dawn), `20_grass_dusk_dawn_storm` (dusk, dawn, a Records-checkpoint storm frame). Visually the straw is no
longer gold maize and the stand is cooler, but the measured pixel saturation of the right stand in
`GoldenDay_valley_eye` (1000,480)-(1280,640) stays 0.70 (the light and grade dominate): the 0.45 target is not met.
Stealth zones and their triggers are untouched (`route_test` 45/45 includes the tall-grass checks).

## Goal 4: ending belt — DONE in code, partly verified
`LookVegetation.ScatterPlayAreaTrees`: the belt only where the crane shot looks (x 380-540, |z+46| < 140, OutsideYard
6-130); palms in the crown layer at 1.3-1.8, tree ferns 1.6-2.4, each with its own clearance test
(`NearStructure(ground, (fern 1.6 | 3.0) x cand x 0.5, 4.5 cand, 9 cand)`), wide = deep = tall = 1 and their own thin
player bodies (radius 0.22/0.30 x vis, height 4 vis, half lean); jungle belt trees enlarge only beyond 14 m from the
yard and are capped at 22 m (vis x tall <= 1.22); dark bark on every belt jungle tree beyond 60 m of the camera.
Checked in the built scene: 27 belt palm/fern bodies, largest radius 0.54 m, none over 0.6 m. 975 jungle-tree
holders (the random stream is untouched: palms/ferns use their own seeded generator and PlayerBody is outside the
NearStructure mask; I did not run cmp_trunks.py against a base dump). `10_28_ending_2_a/b`: palm crowns now read in
the belt; some pale palm trunks show between the crowns.

## Minors
- Rock: `BasaltRock` now also covers `mountainside` (the bald orange headland shelves and the tan bluff at the end of
  the valley road: dark grey in `05_valley_air_b`, `02/03/06_*valley_eye_b`) and `root_cluster_01` as dark wet bark.
  Moss on top faces not done.
- Pond: ragged shore in `Zone4.EvaluateWorldElevation` (noise + 3-lobe term that only ever moves the shore inwards,
  so the sauropod anchor and the ground round the lake stay dry). The change is small from the air
  (`08_GoldenDay_pond_air_a/b`): the pond still reads mostly as an ellipse. Dawn water colour not changed.
- Texture compression: the three tall-grass atlases and the dark-bark atlas are now BC7 without a CPU copy (built
  fresh and `CopySerialized` into the existing asset, name kept: no "Main Object Name" warning). Orphan metas: added
  `MAT_detail_grass_tall*_Tex.asset.meta` and `jungle_tree_DarkBark_Tex.asset.meta` to the folders' .gitignore and
  untracked the four metas.
- Lawn tint and noise: not done (the meadow already has the r3 hue/moss/litter fields; no time for the valley).
- Perf (Play mode, GoldenDay, player camera, `Tools/agent/r5_perf.sh`): meadow (-60,-150 yaw 10) GPU 6.3 ms / CPU
  27.1 ms / 9.76 M tris; valley (128,0 yaw 80) GPU 6.7 ms / CPU 22.2 ms / 8.24 M tris. I have no same-session before
  number (the r4 critic measured meadow 7.0 ms / 30.6 ms / 10.69 M at the same spot on the r4 branch).

## Checks
- Final build with NavMesh: both NavMeshes baked. `route_test.py` 45/45 in 278 s, no new console errors
  (`Tools/agent/r5_route.log`). `verify_playmode_doors`: 18/18 doors, 5/5 documents, flashlight OK, both NavMeshes
  valid (`Tools/agent/r5_doors.log`). Compile: 0 errors, 0 warnings. Console after the final build: 0 errors; the
  only warning was "ScriptableRuntimeReflectionSystemSettings.system is assigned more than once" (x2, engine, during
  the Play sessions; not from this package's code).

## Files
Code: `Assets/Editor/LookPass/LookVegetation.cs`, `LookVegetationDetails.cs`, `LookVegetationImpostors.cs`,
`LookVegetationPrefabs.cs`, `Assets/Editor/WorldBuilder/Zone4_TerrainJungleAndDawnCoastBuilder.cs`,
`Assets/Scripts/Rendering/InstancedFoliageRenderer.cs` (new serialized fields `LodLevel.capless`, `capElevation`;
no signature changes). Generated, committed: impostor/shadow/blob/cards meshes, foliage prefabs and impostor
materials, tall-grass materials, new `_Basalt` materials, the two .gitignore files. Not committed: scenes,
`CretaceaTerrain.asset`, world lens materials, fonts, other regenerated churn.

## Requests for the lead
- Goal 1 needs an impostor shader (Shader Graph) or softer hill crown silhouettes; I would start with the alpha
  edge of the hill atlas (`MakeVariant` + `SaveTexture` coverage) before any lighting change.
- The valley crown G/R and the brown dawn meadow trees need the k-means measure and probably a LOD2 leaf tint pass in
  `LookVegetationPrefabs.TintLeaves` (mesh trees, not impostors).

## Fix round (after the r5 critic)
Same clone and branch. Final build with NavMesh; captures in `Screenshots/handin_r5/fix/` and `30-35_fix_*` (INDEX.md).
Measurements: `Tools/agent/r5_sat.py`, plus a new k-means tool, `Tools/agent/kmeans_box.py <png> box k`.

| # | Finding | What I did | Evidence / result |
|---|---|---|---|
| 1 | Hill impostors got worse (slab wall, star crowns) | `LookVegetationImpostors.SaveMesh`: took out the 45 deg cards (`CardVerts` 8, `CardIndices` 12). Cap rim back to 0.70-0.85, droop 0.18-0.26 h, centre 0.04 h under the top, rim normals lean 0.15, 12 points. New `SoftenHillCrowns` (called by `MakeVariant`, hill copies only): coverage blurred about 3 px and re-thresholded with a dither (closes pinholes, breaks the crisp crown edge), leaf colour pulled 45 % to its local mean. No Shader Graph (no time to author one by hand safely). | Aerial far canopy (0,0)-(500,250): lum 0.313 -> 0.315, hue 81 -> 89 (condition >= 0.30 / >= 80 holds). Star / tent crowns gone from the air (`33_fix_pairs_air`). Dusk hill box (150,190)-(420,260): std 0.160 -> 0.158, hue 63 -> 72: a little softer and greener, **the eye-level mosaic is NOT fixed**. In the dawn pond view (`32`, bottom) the near hill is still columns of vertical cards (seen from below). Goal 1 stays NOT MET; it needs a real impostor shader. |
| 2 | Valley grade and dawn leaves changed nothing | Checked the LOD first (`Tools/agent/q_bandlod.cs`, camera (150,140,-200), 260-520 m): the band draws impostors (134 jungle, 251 palm, 261 fern) plus a few Z23 mesh trees at LOD1-2. The atlas was already hue 98, G/R 1.8 linear, so the old pull did nothing. Now: `OpenValleyGrade` pulls to 110 deg (0.85), sat x1.45 (cap 0.74); leaf tints cut red about 15 % (`LeafTint`: jungle/fern 0.60, palms 0.66/0.64); the solid tree atlases (jungle, both palms, tree fern; mesh LODs and the impostor bake) get base colour (0.85, 1, 0.94) in `GreenAtlasMaterial`. | Atlas: jungle G/R 1.82 -> 2.17 hue 98 -> 116; palm 1.91 -> 2.20 hue 96 -> 107. k-means on 05 (0,140)-(1280,330): main crown cluster G/R 1.14 -> **1.18, hue 74 -> 77** (52 %); second 1.13 / 76; lit crowns 1.05 / 66 -> 1.14 / 74. **Met for the main cluster**, lit crowns just under. Dawn 09 (800,250)-(1280,600): mid clusters still G/R 0.93 / 0.87, hue 52 / 45 (dark cluster 1.01 -> 1.06): **"no brown trees at dawn" NOT met**: the brown there is the 8.5 deg sun on crowns and ground; albedo changes do not move it. |
| 3 | Pond: shrubs on the water, ellipse, no reeds | `LookVegetation.InLakeWater` (ground < lake level + 0.2 m within 95 m of the lake): rejected in `ScatterUnderstory` and `MeadowClump` after all random numbers are drawn (streams unchanged). Shore rag in `Zone4.EvaluateWorldElevation` x20 -> x35, noise 0.055 -> 0.03. New `ScatterReeds`: grass clumps (`grass_medium_02` x1.7-2.8) in water 0-0.4 m deep, in patches round about half the rim, own random stream. | Probe `Tools/agent/q_pond.cs`: understory renderers on ground under the lake level 1,000+ -> 0. 48 reed clumps. `32` bottom: no plants on the water. `33` bottom: the pond has lobes and bays from the air. Dawn water colour not changed. |
| 4 | Untracked texture metas | Removed the `.meta` lines from both `.gitignore` files; the four metas on disk still had the original GUIDs (465394e9, b3063224, e85bae6d, 5e61ad7b, checked against de6e8d9) and are committed again. Only the `.asset` atlases stay ignored. | `git status` shows the four metas tracked. |
| 5 | Tall grass reads as a crop, saturation missed | Tall main prototype minHeight 0.5 / maxHeight 1.9, noiseSpread 0.2; dark 0.45 / 1.7 / 0.25. Road front `nearRoad = SS(0.04, 0.45, bareBlur + (edgeN-0.5)*0.6)`. Texels: main hue pulled 0.85 to 97 deg, sat x0.38, lighter tips; dark sat x0.42. Tint: I tried the critic's neutral (1,1,1). Measured: khaki-olive, hue 60, G/R 1.00 (`Tools/agent/fr/grass_crop.png`). So I set a red cut with no blue lift (0.76, 1, 0.84) instead. | 06 right stand (1000,480)-(1280,640): hue 73 -> 72, G/R 1.16 -> 1.15. Pixel saturation 0.70 -> 0.69: **the 0.45 target is NOT met** (the golden light and grade set the rendered saturation). It reads as a natural green with pale tips, not teal (`Tools/agent/fr/grass_crop2.png`). The far dusk stand has more height variation; the front still mostly follows the road (`35`). |
| 6 | Ending belt barely visible | Belt = the crane view wedge: x 250-540, abs(z+46) < 0.6*(540-x)+40, OutsideYard > 6 (no 130 m limit). Dark bark on wedge jungle trees beyond 60 m as before. | `34`: more palm crowns and darker trunks between the wall and the hill. Some pale trunks are still visible on the right. Chapter 5/6 route frames not captured (route_test passes through them). |
| 7 | 22 m cap pancakes crowns | `vis = 1.22 / tall` when `vis * tall > 1.22` (uniform). | Code. |
| 8 | Belt palms/ferns keep the upright jungle collider | `hc.radius = min(0.55 scale, 0.22/0.30 x vis)` for belt plants; NavMesh rebaked. | route_test and doors below. Not walked by hand in Play mode. |
| 9 | Cap switch pops, changes shadows | `InstancedFoliageRenderer`: per-cell and per-instance state in `CameraState`, capped above `capElevation + capHysteresis` (0.12), capless below 0.08. A capless batch is drawn with shadows Off, and the capped mesh is drawn ShadowsOnly for the same instances. New serialized field `capHysteresis`; no signature changes. | `30_fix_crane_down_hill_dusk.mp4` / `31` (16 frames, 90 m -> 3 m): no visible pop on the hill. Each `cretacea capture` frame is a fresh camera, so this clip shows the switch, not the hysteresis memory. |
| 10 | No same-session perf; blob on slopes | Built de6e8d9's code in this session (the four generated atlases moved aside for that build, then restored with their GUIDs). Same spots, `Tools/agent/r5_perf.sh`. `DropBlobOnSlope`: the ground-shade quad is switched off where the slope is over 4 deg. | Base de6e8d9: meadow GPU 8.3 / CPU 27.6 ms / 10.74 M tris; valley 5.5 / 21.3 / 8.66 M. Fix round: meadow **5.5 / 24.9 / 9.43 M**; valley **7.1 / 21.4 / 7.95 M** (`Tools/agent/perf_before_fr.log`, `perf_after_fr.log`). Valley GPU +1.6 ms (single 8-frame samples). |

### Fix-round checks
- Compile: 0 errors, 0 warnings. Build with NavMesh: both NavMeshes baked, builder warnings [].
- Capture session (Play mode, 17 views + storm): 0 console errors (`fix/errors.json`).
- `route_test.py` on the final NavMesh build: **45/45 in 283 s**, no new console errors (`Tools/agent/fr_route.log`).
- `verify_playmode_doors`: 18/18 doors, 5/5 documents, flashlight OK, both NavMeshes valid (`Tools/agent/fr_doors.log`).
- Console since the final build: 0 errors. Warnings: only the engine's 'ScriptableRuntimeReflectionSystemSettings.system is assigned more than once' (x4, from the Play sessions; the same as the first hand-in, not this package's code).

### Still open (honest)
- Goal 1, the eye-level hill mosaic and the card columns seen from below: needs an HDRP Shader Graph impostor (sky-biased hemisphere lighting and an elevation dither). Not started.
- Goal 2, the dawn brown: lighting-dominated. A dawn-only leaf grade would have to live in the look system (the lead's).
- Goal 3, saturation 0.45: the rendered saturation does not follow the albedo saturation.
- Lawn tint and noise, and moss on the rocks: not done.
