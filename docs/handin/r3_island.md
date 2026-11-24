# Round 3 hand-in: island (far jungle, greens, valley trees)

Branch `r3/island` (base 5c57ee7). Captures: `Screenshots/handin_r3/` (16 files, before/after pairs, `INDEX.md`).
All numbers are region means of 1280x720 captures (sRGB 0..1), measured with `Tools/agent/reg3.py` / `hill.sh`.

## Root cause of the "burnt forest" (blocker 1)
The tropical trees (jungle_tree, palms, tree fern, banana, monstera) use ONE solid atlas for bark and leaves, without
alpha (`MAT_<stem>`, alpha clip off). The impostor bake told leaves from bark by the alpha-clip flag, so every texel of
these trees was graded as **trunk** (x0.24-0.27 in linear) on top of the self-shade: the far jungle_tree impostor
averaged sRGB 0.15/0.16/0.11. The LightProbeUsage.Off suspicion was tested and is not the cause (switching to
BlendProbes changed the frame by < 0.001 luma; reverted).

## What changed
1. **Impostors rebuilt** (`LookVegetationImpostors.cs`)
   - No canopy grade. Leaf/bark told apart per texel by colour (green-yellow and saturation > 0.18 = leaf); bark kept
     in shade (x0.45 linear). Self-shade lightened (1.0-0.88 and 0.90-1.0).
   - Leaf texels matched to the mesh LODs in the same light: saturation x1.22, value x1.34, hue nudged 18 % towards
     105 deg (measured GoldenDay_valley_air: mesh crowns 0.30-0.34/sat 0.47 vs plain bake 0.22/0.31).
   - **Horizontal crown cap**: a third, top-down view is rasterised into the atlas (now 768x512) and drawn on a
     horizontal quad at the mean height of the crown's top surface, with sky-facing normals. 6 tris per tree (was 4).
2. **Tropical green at the source** (`Assets/Art/Terrain/Source~/green_leaf_atlases.py`, new):
   copies of the six solid atlases with only the leaf texels shifted to hue ~94-104 deg (bark untouched), in
   `Assets/Prefabs/Foliage/Textures/*_BaseColor_Leaf.png` (2048 px, 35 MB). `LookVegetationPrefabs` binds them to
   copies of the plant materials (`Assets/Prefabs/Foliage/Materials/MAT_<stem>_Leaf.mat`, refreshed from the
   LookMaterials original every build). Alpha-clipped leaf cards (fern_02, grass_medium_02, shrub_02, pachira leaves)
   get a per-species green `_BaseColor` tint on a copy instead. LookMaterials itself is untouched.
   Without the PNGs the builder falls back to the original materials (no error).
3. **Grass and ground** (`LookTerrainLayers.cs`, `LookVegetationDetails.cs`): TL_Grass and TL_Moss tint (0.80, 1.0,
   0.80); detail grass cards (0.74, 1, 0.80) x base, ground-cover ferns on their own green copy
   (`Assets/Art/Terrain/MAT_detail_fern.mat`). Under the hill canopy the ground is now moss (0.85) instead of leaf
   litter (0.40 -> 0.16), so gaps between far crowns are green, not brown-black. (A 9th terrain layer with
   `backdrop_jungle_aerial` was not added: HDRP TerrainLit would need a second pass over every terrain tile.)
4. **Valley trees** (`LookVegetation.cs`, ScatterPlayAreaTrees): every broad jungle tree gets its own crown width
   (0.75-1.35) and depth (x0.82-1.15) independent of its height, a 4-8 deg lean, and 0-3 understory plants
   (banana, tree fern, monstera, fern) round its foot (no colliders, like all understory). The trunk collider now sits
   on an upright holder GameObject `Tree_jungle_tree_NNNN` at exactly its old world position, radius, height and base,
   so the NavMesh is unchanged; the visual prefab is its child. Positions and the random stream are as before.

## Verified (captures looked at)
| View | Before | After | Target |
|---|---|---|---|
| GoldenDay aerial far canopy | luma 0.145, sat 0.19, hue 57 | 0.315, 0.58, 82 | >= 0.30, >= 0.40, 85-110 |
| GoldenDay valley_air far forest (top band) | 0.164, sat 0.08 | 0.267-0.286, sat 0.44-0.47 | match near mesh +-10 % |
| same frame, near mesh crowns | 0.31-0.34 (r3_b) | 0.41, sat 0.57 | |
| GoldenDay meadow hill | 0.162, sat 0.15, hue 54 | 0.195, sat 0.40, hue 82 | 0.30-0.40 needs the haze (lead) |
| DuskTransition meadow hill | 0.443/0.397/0.341, hue 33 (tan) | 0.330/0.332/0.258, hue 62 | >= 70 |
| DawnCoast aerial canopy | hue 35 brown | hue 53 | dawn grade = lead |
- Valley Play views (route 17/18, day and night): varied crowns, leans and understory clumps instead of clones.
- Final full rebuild WITH NavMesh: ok, raptor and T. rex NavMesh baked. `verify_playmode_doors` 18/18 doors, 5/5
  documents, both NavMeshes valid. `route_test.py` **45/45** (Screenshots/route_r3). Recompile 0 errors 0 warnings;
  console after the route test: 0 errors, 0 warnings.
- Perf (`perf_here.sh`, Play, GoldenDay, hidden editor, player camera; base measured on a clean base build):
  valley (128,1.2,0 yaw 70): before gpu 4.8 ms / cpu 22.1 ms / 4.11 M tris -> after 4.9 ms / 15.7 ms / 4.49 M;
  meadow (-60,1.5,-150 yaw 10): before 7.4 / 28.1 / 6.74 M -> after 5.3 / 21.5 / 7.14 M.
  +6-9 % triangles (crown caps and tree understory); the hidden editor's CPU times are noisy.

## Goal 5: the mist slab
Not mine: the slab is `LookWeather.PlaceMist` (ValleyGroundMist_* / StreamMist_* LocalVolumetricFog, no runtime
control), confirmed by `docs/review2/daylight_check.md`. In my GoldenDay Play-mode valley views (player at 128,1.2,0
and 93.6,0.3,-2.2) no slab was visible; the free-camera day valley frames showed none either. Request below.

## Requests for the lead
- LookWeather / AtmosphereController: drive the valley and stream mist per preset (off in GoldenDay, faint at dusk) and
  fade the box top (daylight finding 'GoldenDay_valley_eye' weather).
- GoldenDay haze: fogMeanFreePath 5200 -> ~2500-3000 m, pale blue, so the hill at 300-600 m lifts to 0.30-0.40 with a
  blue-green cast (the hill albedo is fixed now; it is the missing aerial perspective that keeps it at 0.20).
- Dawn grade (yours): the canopy now carries green, the brown at dawn is the grade.

## Not done / rough edges
- Dusk hill hue 62 deg (target 70): green is clearly back, but a 5.5 deg orange sun still warms it to olive-gold.
- No per-instance colour variation and no 2-3 bake variants per species (would need an instanced colour property in
  the impostor shader path); random yaw on the crossed quads and the cap gives the variety there is.
- Hill mesh LOD range left at 0.35 (the seam is matched in brightness instead of pushed out, to keep triangles).
- The orange flecks in `tj_grass_BaseColor` were not repainted (the green tint covers most of it).
- `LookVegetationPrefabs` keeps LOD cross-fade off (draw-call reasons documented in the code), so the switch still pops,
  but now between matched colours.

## Files
- `Assets/Editor/LookPass/LookVegetationImpostors.cs`, `LookVegetationPrefabs.cs`, `LookVegetation.cs`,
  `LookVegetationDetails.cs`, `LookTerrainLayers.cs`
- new `Assets/Art/Terrain/Source~/green_leaf_atlases.py`, `Assets/Art/Terrain/MAT_detail_fern.mat`
- new `Assets/Prefabs/Foliage/Textures/*_BaseColor_Leaf.png`, `Assets/Prefabs/Foliage/Materials/*_Leaf.mat`
- regenerated (owned): `Assets/Prefabs/Foliage/**` prefabs, impostor meshes and materials, `TL_Grass`, `TL_Moss`,
  `MAT_detail_grass`
- helpers: `Tools/agent/r3.sh`, `r3_views.sh`, `r3_perf.sh`, `hill.sh`, `reg3.py`, `q_imp.cs`, `q_mats.cs`

## Fix round (art director review of a1d05e3)
All numbers: 1280x720 Play-mode captures (free camera = capture_set.sh poses, preset forced), final build with NavMesh.
New helpers: `Tools/agent/fixstats.py` (neon/sat>0.8/near-black shares), `lodmatch2.sh` + `lodmatch.py` + `lodsplit.py`
(same view with every valley tree of one species forced to LOD2, to its impostor and hidden; compares the pixels both
cover), `q_counts.cs`, `q_bounds.cs`. Captures 17-33 in `Screenshots/handin_r3/` (INDEX.md).

| Finding | What I did | Result (verified in a capture) |
|---|---|---|
| Neon-lime impostor crowns / lawn confetti (high) | `LookVegetationImpostors`: per-stem leaf factors instead of the global x1.34/x1.22 (`LeafFactors`: palms 1.0/1.0, tree_fern 0.88/1.05, jungle_tree 0.90/0.95 after measuring); leaf texels clamped to v <= 0.50, s <= 0.62 before the shade; cap shade Lerp(0.85, 0.70) | neon (sat>0.72 & G>0.5): aerial 4.06 % -> 0.07 %, valley_air 3.10 -> 0.02 %, day_valley 2.39 -> 0.07 %; sat>0.8 0.0 % in both aerial views. Impostor/LOD2 luma, same view: palm_tree 1.00, palm_tree_b 0.98, tree_fern 0.92, jungle_tree 1.13 (leaf pixels 1.10; was 1.24-1.26 with the old factors) |
| Two greens per species (high) | Done in my own file instead of a request: `LookVegetation.UnifyLeafGreens` (runs at the start of the zone-4 scatter, after zones 1-3 are built) switches every scene renderer on `MAT_<stem>` of the six solid tropical atlases to the same leaf-green copy `LookVegetationPrefabs.GreenLeafMaterial` gives the island plants. Material only; no object, collider or transform changes. LookMaterials only swaps FBX-embedded materials, so it keeps the swap | 648 zone 1-3 renderers switched, 0 renderers left on the old `MAT_palm_tree(_b)/banana/tree_fern`. Plaza palm fronds hue 57 -> 84 (26 vs 25). Route-14 service path: not separately captured |
| Far kapoks = caps on black sticks; trunk seam (medium) | Two impostor albedos per tree: the prefab LODGroup impostor (open valley, lawn) with bark x1.0 linear (x0.80 was still darker than the sunlit mesh trunks), and dense hill copies with bark x0.45 bound through `InstancedLods(..., impostorVariant)` for the instanced hill only | 21/33: far valley trunks pale grey like the mesh, the dark-stick band is gone; hill trunks stay dark under the closed canopy. Trunk luma imp/LOD2 on three trees: 0.75, 1.07, 1.01 (one sunlit trunk is still 25 % darker). No cross-fade (draw-call reason unchanged) |
| Far canopy one stamped colour, brighter than the lawn (medium) | 3 dense variants per tree species (`{stem}_Impostor_V0..2`: hue 0/-6/+6 deg, value 1.0/0.88/1.08, V2 mirrored with mirrored normals); the hill scatter has one prototype per variant and picks it by a position hash (random streams and positions unchanged, no shader change). Dense copies of jungle_tree get the canopy brightness back (x1.49 value, x1.28 sat, same clamps) and all dense copies are pulled 30 % further to 110 deg | 18: canopy 0.272 / sat 0.49 / hue 90 vs lawn 0.287-0.290: canopy/lawn 0.94 (target 0.85-1.0; was 0.38/0.30). Valley-air far band hue 94 |
| Valley = one umbrella tree in rows (medium) | `ScatterPlayAreaTrees` s==0: independent height factor 0.85-1.15; for trees seen at foot level (`SeenAtFootLevel`: 40 m from the valley road, a camera line or a stealth patch) `TreeCompanions`: 1-2 palm_tree/palm_tree_b/tree_fern at 4-9 m (scale 0.9-1.3, own lean), root_cluster_01 at 50 % of trunks, `hanging_vines` (new prefab spec) in 30 % of crowns. Trunk collider holders untouched | 176 companions, 94 root clusters, 83 vines. 23/30/31: palms and tree ferns between the kapoks, vines, varied heights. Positions of the kapoks are still the old stream (NavMesh) |
| Greens stop at ~70, highlights yellow, grass oversaturated (medium) | `green_leaf_atlases.py`: targets 100-108 deg, a lower saturation gate for bright texels (v>0.45: smooth(0.08,0.18,s)); atlases regenerated. Tall grass `MAT_detail_grass` tint (r0.72, g1.04, b1.20) x base (x1.45 blue went teal, hue 117); TL_Grass/TL_Moss (0.74, 1.0, 0.94) | tall grass hue 83/sat 0.88 -> 98/0.65 (target 90-100 / 0.60-0.68); day_valley sat>0.8 16.5 % -> 2.4 %; meadow grass hue 87-91, sat 0.56-0.65; hill hue 82 -> 88. **Not met:** dawn_valley pixels < 0.05 = 30.3 % (a1d05e3 28.7 %, base 19.7 %; target < 22 %): the extra dark is the kapok crowns against the dawn sky and the shaded foreground under the amber grade |
| Mist-slab note contradicts its capture (medium) | Note corrected: the slab is visible in Play mode too. 24 = Play GoldenDay, player 128,1.2,0 yaw 75: grey band at the kapok feet, x860-1000 y368-378 = 0.29/0.39/0.30, sat 0.24 (grass 0.6), hard top edge. It is `LookWeather.PlaceMist` (ValleyGroundMist_* / StreamMist_*), not mine; request below stands | |
| Lean/widen without clearance, collider or water checks (medium) | Crown test with the real crown: reach 6.5*vis*max(wide,deep), lean offset at 12*vis*tall; if `NearStructure` hits, wide = deep = tall = 1 and lean = the old tilt. A player-only leaned capsule (`PlayerBody`, layer IgnoreRaycast: collides with the player, outside the NavMesh layer mask Default/Environment/Door, ignored by raycasts) of radius 0.55*vis*max(wide,deep) on every broad tree, and one per companion. `TreeUnderstory` and companions skip ground below LakeLevel+0.15 / +0.4 | 1,133 player bodies; NavMesh unchanged in size (both baked and valid); route_test 45/45 |
| Budget: triangles, GameObjects (low) | Tree understory 0-2 per tree (was 0-3) and only for trees seen at foot level | Trees group now: banana 31, monstera 19, fern_02 27 understory (was +290-381 each); new: 176 companions + 176 body holders, 94 roots, 83 vines (~700 fewer objects than a1d05e3). perf_here (Play, GoldenDay): valley gpu 5.3 ms / cpu 16.8 / **4.53 M** tris (base 4.8 / 22.1 / 4.11 M, a1d05e3 4.9 / 15.7 / 4.49 M); meadow 5.7 / 22.1 / **7.17 M** (base 7.4 / 28.1 / 6.74 M; a1d05e3 5.3 / 21.5 / 7.14 M). Triangles stay +10 % / +6 % over base: accepted cost of the crown caps and the valley companions, GPU 5.3-5.7 ms |
| Hand-in overclaims (low) | Corrected here: the old capsule tilted 0-3 deg with the tree, the holder capsule is upright (same position, radius, height, base); "bark untouched" is not strictly true: the leaf-atlas script also shifts tan bark texels (hue 30-42) of jungle_tree towards green. dusk_aerial, day_valley, day_plaza are now in the hand-in (29, 22/23, 25/26) | |

Verified at the end: recompile 0 errors 0 warnings; final full rebuild WITH NavMesh ok (raptor + T. rex baked, no
builder warnings); `route_test.py` **45/45** (Screenshots/route_fix_r3, "console: no new errors during the run");
`verify_playmode_doors` 18/18 doors, 5/5 documents, both NavMeshes valid. Console after the run: 0 errors; one HDRP
engine warning on Play enter/exit ("ScriptableRuntimeReflectionSystemSettings.system is assigned more than once"),
not from this package. The "Failed to create agent because there is no valid NavMesh" warnings seen mid-session come
from Play sessions on builds made with `--bake_navmesh false`; none after the NavMesh build.

### Still not met (honest)
- Dusk: meadow hill hue 63 and dusk aerial canopy hue 58 (check >= 70). The albedo is green (day hue 88-94); a 5 deg
  orange sun under the storm deck keeps it olive-gold. Further would need a cooler dusk shade (lead's grade).
- Dawn: 30 % of dawn_valley below 0.05 luma (target < 22 %): dawn grade (lead's) plus the greener crowns.
- jungle_tree open impostor 13 % brighter than its LOD2 over the whole tree (leaf pixels 10 %); one sunlit trunk 25 %
  darker. No LOD cross-fade.
- Valley kapok positions are the old stream (the NavMesh is baked from them), so their spacing is unchanged; the
  companions, height factor, roots and vines break the rows up.
- Triangles +10 % (valley) / +6 % (meadow) over the base.

### Requests for the lead (fix round)
- LookWeather / AtmosphereController: the valley/stream mist shows in GoldenDay Play mode (24): ValleyGroundMist_* and
  StreamMist_* off in GoldenDay, faint at dusk, density faded over the box height.
- LookMaterials (optional now): `UnifyLeafGreens` already puts zone 1-3 plants on the leaf-green copies at build time.
  If you prefer it in LookMaterials, bind `Assets/Prefabs/Foliage/Textures/<stem>_BaseColor_Leaf.png` on the six solid
  tropical `MAT_<stem>` and tint alpha-clipped leaf `_BaseColor` by `LookVegetationPrefabs.LeafTint(stem)`; then the
  swap becomes a no-op.
- Dusk/dawn grade: cooler shade (smhShadows towards blue-green) so the green canopy does not go olive/mustard.

### Files (fix round)
`Assets/Editor/LookPass/LookVegetationImpostors.cs`, `LookVegetationPrefabs.cs`, `LookVegetation.cs`,
`LookVegetationDetails.cs`, `LookTerrainLayers.cs`, `Assets/Art/Terrain/Source~/green_leaf_atlases.py`; regenerated:
`Assets/Prefabs/Foliage/Textures/*_BaseColor_Leaf.png`, `Assets/Prefabs/Foliage/**` (prefabs incl. new
`PF_hanging_vines`, impostor textures/meshes, new `*_Impostor_V0..2` textures and materials), `Assets/Art/Terrain/`
materials and `HillJungleInstances.bytes`; helpers in `Tools/agent/`.
