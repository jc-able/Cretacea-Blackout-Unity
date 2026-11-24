# Round 4 hand-in: island (r4/island)

Took over from an engineer who was cut off. Their tree had no code changes, only a capture helper
(`Tools/agent/shots.sh`, which I kept) and the "before" captures in `Screenshots/r4i/before/`. Those were taken from
their base build of this clone, and I used them as the before set. Captures and INDEX: `Screenshots/handin_r4/`.

## Goals

1. **Impostor crowns read as shelves from eye level: PARTLY DONE.** `LookVegetationImpostors.SaveMesh`: the
   8-sided umbrella cap (rim at 88 % of the footprint, 13 % droop) is now a ragged 16-point rim at 72-88 % of the
   footprint. Its UVs are cropped with it, so the top view keeps its scale. The centre sits 2 % under the crown top
   and the rim drops 17-27 %, so it sinks into the leaf cards. Proof: `03a/03b` and the 2x crop in `09`. The
   straight lit ledges are gone and the right edge of the valley shows no plates. But the dusk hill still reads as
   a fine mosaic of lit crown blobs, not a soft canopy. Aerial green held: far canopy in `GoldenDay_aerial_sw`
   (x 100-700, y 40-200) measured 0.276/0.347/0.218 after vs 0.277/0.348/0.218 before (luminance 0.322, hue 93°,
   the bar is 0.30 / 80°). The real fix needs a cap that fades with view elevation (an impostor shader) or
   hemi-octahedral impostors. I did not build either inside the time box.
2. **Dying woodland band from the air: PARTLY DONE.**
   - Ground shadow at the impostor level: new `LookVegetationImpostors.BuildShadowCrown`, a ShadowsOnly copy of the
     crown cap (crown-shaped, with no crossed-card shadows), added to the impostor LOD of the play-area prefabs.
     `LookVegetationPrefabs.InstancedLods` skips ShadowsOnly renderers, so the hill jungle does not draw it.
   - Open-valley impostor grade: new `OpenValleyGrade`, for `jungle_tree` and the prefab copy only. The hill copies
     are made before it and are unchanged. It pulls the leaf hue toward 97° (factor 0.45), multiplies saturation
     by 1.22 and value by 1.06, and darkens the trunk to linear x0.6.
   - Valley band (x 300-1100, y 140-330 of `GoldenDay_valley_air`): 0.244/0.294/0.162, hue 83°, G/R 1.20. Before:
     0.251/0.292/0.170, hue 80°, G/R 1.16. Proof: `02a/02b`.
   - The later LOD switch (0.10 to 0.05) was tried and reverted. It kept the skeletal, pale-trunked LOD2 mesh twice
     as far out, and from the air that band looked worse than the impostors (after1 capture, not in the hand-in set).
   - **Not met:** brown trees at dawn. The dawn meadow box still measures hue 44° (`05a/05b`). That colour comes
     from the dawn light and grade on both the lawn and the trees. The lead could check the dawn grade.
3. **Tall grass reads as a crop: DONE.** In `LookVegetationDetails`:
   - Three tall-grass looks, each a recoloured copy of the grass atlas on its own material (`TallGrassMaterial`):
     - the main stand: about 0.72x saturation, with tips lighter and less saturated;
     - a dry seed-head look with straw tips, 1.15 m cards, about 22 %;
     - a darker, bluer green, 0.82 m cards, about 25 %.
   - The looks are mixed by two Perlin fields in drifts. New detail layers 4 and 5: `ProtoTallDry`, `ProtoTallDark`.
   - A blurred bare-ground map (about 4 m) starts the falloff about 2.5 m from the gravel, with a noisy front and
     tufts in the outer metre.
   - Stealth zones, `TallGrassZoneVolume` objects and triggers are untouched. Only the painted look changed.
   - Proof: `01a/01b`. Mixed greens and straw, no straight front along the road.
4. **Ending crane shot diorama: DONE (visual).** In `LookVegetation.ScatterPlayAreaTrees`:
   - Jungle trees 6-130 m outside the Old Yard now vary. About 44 % of those more than 14 m out become palms,
     palm_b or tree ferns. Heights are 0.62-1.5x, with more lean.
   - Jungle trunks get darker with distance from the ending camera: `MAT_jungle_tree_Leaf_DarkBark`, with the bark
     texels of the atlas at linear x0.5.
   - Visual only: each holder keeps its jungle-tree trunk collider and position, so the NavMesh input is unchanged.
   - Proof: `06a/06b`. Mixed heights and species, darker trunks, less of a repeating umbrella tree.
5. **Minors.**
   - Trees out of the pond: DONE. `InPond` rejects a tree whose trunk, or any point 3.5 m around it, is below lake
     level + 0.3 m. It runs after the cell has taken its random numbers, so the other trunks keep their places.
     `07a/07b`: no trunk in the water.
   - Orange slab rocks: DONE for namaqualand boulders and rock_face sets (`LookVegetationPrefabs.BasaltRock`, a
     `_Basalt` copy of the material tinted x(0.52, 0.62, 0.90): dark grey). See the pond bank in `07b`/`08b`. Not
     done: moss on top faces, and the headland shelves. I did not check whether those come from the same assets.
   - Not done: lawn tint/noise, grouping the lone trees at the end of the valley road, the identical buttress-trunk
     row (still visible in `08b`), the palm crown crushing to black (that needs LookMaterials, not my file), boat
     foam and wake, the shoreline from the air.

## Verification
- Final `build_all_scenes` with NavMesh: status ok, both NavMeshes baked, no build warnings. Compile: 0 errors,
  0 warnings.
- `route_test.py`: 45/45 checks passed in 286 s, with no new console errors (`Tools/agent/r4_route.log`).
- `verify_playmode_doors`: passed. 18/18 doors, 5/5 documents, both NavMeshes valid.
- Perf (`Tools/agent/perf2.sh`, hidden editor, GoldenDay, after only):
  - valley: GPU 4.8 ms, CPU 22.5 ms, 4.12 M triangles;
  - meadow: GPU 7.0 ms, CPU 30.6 ms, 10.69 M triangles.
- There is no before measurement (time box), so these numbers cannot show a change. The new parts are one
  ShadowsOnly 16-triangle renderer per play-area tree at the impostor level and up to about one more tall-grass
  card in the dark-green look (5 cards against 4). The hill jungle is unchanged, and so are the LOD distances.
- Console warnings are only 'no valid NavMesh' (from the earlier no-NavMesh test builds) and one HDRP reflection
  system notice.

## Caveat (please check)
- The near-tree arrangement in the meadow and valley captures looks different from the before set and from
  `_refs/final`, even in views my belt change does not reach. My only placement change there is `InPond`, which
  only rejects trees within 90 m of the lake and should not move other trees. I could not find the cause in time.
  It may be a stale before build or LOD selection rather than a real move. The route test covers walkability.

## Requests for the lead
- Dawn grade: the meadow (lawn and trees) is still brown-olive at dawn (hue 44°).
- `LookMaterials`: LitTranslucent for solid palm copies (the palm crown crushes to black in `GoldenDay_plaza_eye`).
- A view-elevation fade for the impostor cap (shader) would remove the remaining hill mosaic.
