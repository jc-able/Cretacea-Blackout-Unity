# Round 2 hand-in: island (fix/island)

Clone `~/CretaceaUnityClones/terrain`, base 8ed04fa. Captures: `Screenshots/handin_r2/` (16 files + INDEX.md),
before set `Screenshots/r2before/` (same clone at the base commit, same cameras, Play mode).

## What changed, by finding

1. **BLOCKER, hill jungle as mustard cardboard at dusk and dawn**: fixed in large part, not fully. See 01/02, 03/04.
   - `LookVegetationImpostors`: the crossed-quad impostors had vertex normals that pointed left on one half of each
     card and right on the other, so at a low sun every card was lit on one half and black on the other. They now
     carry a baked **tangent-space crown normal map** (`*_Impostor_Normal.asset`). It is built from the gradient of the
     blurred leaf coverage (the outline of each leaf mass turns away), a sphere around the dense crown centre, an
     upward lean and 28 % of the real leaf normals. The quads use plain front-facing normals and
     `_DoubleSidedNormalMode = Mirror`.
   - **Canopy grade**: linear albedo × (0.46, 0.86, 0.74) on leaf texels and × 0.55 on trunks (the palm atlases
     were yellow-olive, with R about equal to G). Interior and lower parts are a little darker.
     Measured on the dusk hill (region 150..700 × 160..280 of plaza_eye): mean sRGB went from (131,120,83) to (98,96,81),
     and the 90th percentile from (231,186,99) to (184,153,91).
   - `LookTerrainLayers`: the hill ground under the canopy is now mostly moss and leaf litter (grass 0.12), so the
     gaps between the far crowns read as shade instead of sunlit lawn speckles.
   - Still open: from the **dusk aerial** the canopy is greener but still warm olive with a tan cast, and there is no
     aerial haze over the hills (fog is in the lead's profiles). The day hills now read as dark-green rounded
     crowns.
2. **BLOCKER, box rescue boat**: fixed. See 05/06, 07. `LookTerrainCoast.BuildCutter` builds a 25 m Coast Guard
   cutter in code:
   - **Hull**: lofted over 160 stations, with a rising sheer, a deep V forward under a flared bow and a transom.
     It is painted with antifouling, a black boot-top, white topsides and a dark rubbing strake. The orange and blue
     racing stripe is slanted. The topsides' station lines lean with the stripe, so its edges are straight, not
     stepped.
   - **Superstructure**: a two-level deckhouse with tumblehome, a wheelhouse with reverse-raked dark windows, side
     windows and portholes, and an exhaust stack.
   - **Mast and fittings**: a tapered mast with stays, a yard, an open-array radar, a radome, masthead, nav and blue
     strobe emissives, a searchlight, life-raft canisters and rails with stanchions. An orange rescue tender sits on a
     davit.
   - **Wake**: a foam wake on the HDRP sea (bow wave, Kelvin arms, churned trail). It is painted into the existing
     shore-foam water decal, because the HDRP water-decal atlas (1024) is already full with the shore foam and a
     second decal logged "No more space in the Water Decal Atlas".
   - **Placement**: moved closer and turned to run across the view towards the island, at (695, sea, -28) with yaw
     -112. Materials are in `Assets/Art/Terrain/Coast/`.
   - At the ending camera the cutter is a backlit silhouette, because the sun is behind it. The mast, radar and
     wheelhouse outline and the wake read there; the stripe and detail read in the close-up (07).
3. **Major, overlook rail made of boxes**: fixed. See 08/09 (also the silhouette in ending 1). The rail is now a
   galvanised pipe guard rail:
   - round posts on bolted base plates (4 bolt heads each) with caps;
   - a top rail and a mid rail with rounded ends.
   The old `Overlook_TopRail` box stays as an **invisible collider** with the same transform, so the cliff edge
   blocks the player exactly as before.
4. **Major, checkerboard lawn and golf-course meadow**: fixed from the air, improved at eye level. See 10/11.
   - The grass and moss textures had mid-frequency blotches and a low-frequency AO drift in the mask (G range 0.077
     across the tile quadrants) that repeated every 7.5 m. I re-derived them with a second flatten pass
     (`derive_terrain_textures.py`, `flat_ao`); the AO range is now 0.009.
   - The splat painter adds moss swales (~25 m), leaf-litter drifts and a 4 m speckle.
   - Titan Meadows trees now stand in groves (noise-gated) instead of as evenly spaced single palms. Shrub and fern
     clumps are scattered in the open grass.
   - Detail distance went from 55 to 70 m (the far meadow was bare terrain).
   - Night meadow: the clumps break the flat carpet a little. It is still a dark field at night (no capture in the
     set).
5. **Major, river as a dry ditch, and a flat sea**: partly fixed. See 12/13, 08/09.
   - **Stream**: the water stands 0.5 m above the bed (was 0.35), clearer green-brown water, more foam.
   - **Bed and banks**: mud bed with a moss fringe; reeds and sedge line the water's edge as terrain details.
   - **From the valley_air height** it still reads as a dark channel. It is better than the ditch, but it is not a
     sparkling river.
   - **Sea**: calm wind 12 → 26 km/h (storm 34 → 48), calm foam 0.30 → 0.42, ripples 6 → 10. Swell lines now show
     from the overlook (09, 06).
   - The olive band at the horizon is the day fog (lead's look system); it is still visible in 09.
6. **Major, the coast road ends in nothing**: partly fixed. A green escape-route marker lamp now stands at the north
   end of the overlook rail. It is a green steel mast, base plate, housing and emissive lens, with
   `Light_AuxRouteGreen_Overlook` in the AuxRoute group. It matches the existing core rule `^Light_AuxRouteGreen`
   (170 cd green, 9 m), so no new lamp rule was needed.
   - Verified in `Screenshots/route_r2/27_escape_coast_road.png`: it shows as a small green point at the end of the
     road. That is a destination, but a small one. The road's far end is still dark.
   - The sea view beyond the rail is the HDRP ocean with the stronger swell.
7. **Major, crane shot back at the island**: partly fixed. See 14/15.
   - Belts along walls now mix species: the broad jungle_tree share drops from 38 % to 22 %, and its scale varies
     more (0.55–1.15).
   - **No mist**: local volumetric fog only renders inside the fog's volumetric depth extent (80 m by default,
     dawn included). The valley is 150–250 m from the crane camera, so local fog volumes would be invisible. This
     needs the lead (see Requests).
8. **Minor, Talon pen floor as bare dirt**: fixed. See 16. The pen (X -90..20, Z 42..128) is splat-painted with
   leaf litter, moss and patchy mud, with fern and tuft details. Details only: no colliders, so the Control Room
   sightline and the raptor NavMesh are unchanged.

## Files
- `Assets/Editor/LookPass/LookVegetationImpostors.cs`: crown normal maps, canopy grade, front-facing quad normals.
- `Assets/Editor/LookPass/LookTerrainLayers.cs`: ground-cover variation, hill floor, Talon pen, stream bed and banks.
- `Assets/Editor/LookPass/LookVegetation.cs`: meadow groves and clumps, wall-belt species mix.
- `Assets/Editor/LookPass/LookVegetationDetails.cs`: stream reeds, pen ferns, detail distance 70 m.
- `Assets/Editor/LookPass/LookTerrainWater.cs`: stream level and colours, sea swell, cutter wake in the shore foam.
- `Assets/Editor/LookPass/LookTerrainCoast.cs` (new): the cutter, its wake, the overlook guard rail and the marker lamp.
- `Assets/Editor/WorldBuilder/Zone4_TerrainJungleAndDawnCoastBuilder.cs`: uses the above; rail collider kept.
- `Assets/Scripts/Rendering/WaterWeatherDriver.cs`: calm and storm sea values (serialized field defaults only;
  public API unchanged).
- `Assets/Art/Terrain/Source~/derive_terrain_textures.py` and `Assets/Art/Textures/terrain_jungle/tj_{grass,moss}_{BaseColor,MaskMap}.png`.
- `Assets/Art/Terrain/Coast/*` (meshes and materials of the cutter), `Assets/Art/Terrain/MAT_ShoreFoamDecal.mat`,
  `Meshes/StreamRibbon.asset`, `Assets/Prefabs/Foliage/*` (impostor meshes and materials; generated normal maps are
  ignored in `Meshes/.gitignore`).
- `Tools/agent/*`: my helpers, bound to this clone.
- Not committed: scenes, `CretaceaTerrain.asset`, and the UI `.meta` files the editor touched on import (not mine).

## Lamp rules added
None. The marker lamp uses the core `^Light_AuxRouteGreen` rule. The cutter's lights keep the `Light_Evac*` exemption.

## Performance (`perf_here`, Play mode, GoldenDay, 1280×720 game view, hidden editor)
- Valley (128,1.6,0), yaw 77: before gpu 6.0 ms / tris 2.77 M; after gpu 7.0 ms / tris 3.02 M.
  The extra triangles are ground cover out to 70 m plus the reeds. The hill jungle itself is unchanged: same
  instance counts, still 4 triangles per impostor.
- Meadow (-60,-4,-150): before gpu 8.2 ms / tris 3.49 M; after gpu 4.4 ms / tris 3.56 M.
- CPU frame times were 19–26 ms both before and after, because batch-mode editor overhead dominates.

## Requests for the lead
1. **Dawn mist / "misty island"** (`LookAtmosphereProfiles`, dawn): fogMeanFreePath 3200 → ~700–900 m, fogMaxHeight
   about 30–40 m above the valley floor, and fogDepthExtent ~250 m. The crane shot and the hills get haze from this,
   and local fog volumes become possible. The same applies to the dusk aerial: the hills get no aerial perspective.
2. **Day horizon band**: the olive-grey band between the sea and the sky is still visible in GoldenDay_coast_eye (09).
3. **Tooling, important**: `Tools/lookdev/ucli.sh` unconditionally exports `UNITY_PROJECT_PATH=~/CretaceaBlackoutUnity`.
   `capture_set.sh`, `nightlab.sh` and `perf_here.sh` source it, and `route_test.py` defaults `--project` to the
   owner's project. Run from a clone, they drive the **owner's editor**. I used copies bound to the clone
   (`Tools/agent/`) and `route_test.py --project <clone>`. Suggested fix: `export UNITY_PROJECT_PATH="${UNITY_PROJECT_PATH:-...}"`.
4. HDRP water-decal atlas: the shore foam takes the whole 1024 atlas. A bigger atlas in the HDRP asset would allow
   separate, sharper decals (the cutter's wake is now 2 m per texel inside the shore-foam texture).
5. Minor, not mine: cliff-top grass behind the rail looks wet and mirror-like at dawn (`TerrainWetnessDriver`).
6. Pre-existing console error at editor start: `CretaceaPbrImporter.cs:247` "RGBA 64 bit not valid with texture type
   Default". It is not from this package.

## Not verified / rough edges
- Route test (`route_test.py --project <clone> --out Screenshots/route_r2`) passed 38/38 in 240 s, with no new console
  errors. In that run's `28_ending_3` (real cutscene timing) the cutter's stripe and wake read.
- Night versions of the hills, the river and the sea were not captured after the change.
- The bare tan slope patches seen from the aerial (the headland step rocks at x≈520 and the steep mossy-rock layer)
  are not changed.
- Dusk aerial canopy: still warm olive; trunk streaks of distant palms are dimmed but still visible by day.

## Fix round (after the art director's review of a3c6c52)

Before = `Screenshots/fixbefore/` (a3c6c52 built in this clone, same cameras). After = `Screenshots/fixfinal/`.
Hand-in pairs: `Screenshots/handin/` (16 files + INDEX.md). Real cutscene views: `Screenshots/route_fix/`.
Region numbers are mean sRGB of the named pixel box at 1280x720.

1. **Hill jungle blocker (high)**: partly fixed.
   - Done as the critic proposed, in `LookVegetation.ScatterTerrainTrees`:
     - per-tree slope cut 44 -> 52 deg;
     - falloff SS(36,45)*0.7 -> SS(44,55)*0.5 (the hill floor term too);
     - mid layer 50 -> 58 deg, steep-face ferns 40-55 deg;
     - summit thinning 0.55 -> 0.25.
   - `LookTerrainLayers`: basalt SS(45,56) -> SS(38,48).
   - `LookVegetationImpostors`:
     - impostor trunks x0.55 -> x(0.24,0.27,0.25) linear, which removes the pale vertical "drip" streaks;
     - leaf grade (0.46,0.86,0.74) -> (0.40,0.84,0.76).
   - Verified:
     - The S-shaped bare streak and the speckled clearings are gone in dusk plaza (02), dusk meadow (04), and the dusk and dawn aerials (06, fixfinal/dawn_aerial).
     - Dusk plaza hill (150..700,160..280): (98,96,80) -> (88,87,78), G/R 0.98 -> 1.00.
   - Not met:
     - From the dusk aerial the canopy is still warm olive: G/R 0.92 -> 0.94. That comes from the orange key light on crossed-quad impostors.
     - The crowns still read as cards from above. A top-down impostor quad would be the next step; it is not done.
     - No haze. That is the lead's fog (request 1 below).
   - Also captured (not before): dusk shot 5, dawn valley eye, and the dawn and storm aerials (`fixfinal/`).
2. **Titan Meadows lawn (high)**: improved, not a wild meadow yet.
   - `LookVegetation`:
     - grove gate SS(0.60,0.72) -> SS(0.52,0.62), with grove share 0.55 -> 0.95, so trees stand in groves;
     - meadow clump gate SS(0.64,0.76) -> SS(0.48,0.58);
     - each accepted cell gets 3 jittered fern/banana/shrub plants (own hash RNG): about 600 meadow plants instead of about 40;
     - light species, because the meadow is the opening shot.
   - `LookVegetationDetails`: noise-gated tall-grass blobs across the meadow, and bald/thick swathes of tufts.
   - `LookTerrainLayers`, within 95 m of the meadow centre:
     - green grass / olive moss hollows at about 40 m, and tan leaf-litter drifts;
     - a muddy game trail from the lake towards the trike herd.
   - Measured:
     - day meadow eye, lower half: luma std 22.6 -> 39.0;
     - night meadow (0..1280,400..720): std 5.3 -> 9.3, mean abs diff 8.8/255, 99.5th percentile 43 -> 67.
   - Captures: 07/08 (opening shot 1 path), 09/10 (night), 04 (dusk).
   - It is still a dark field at night: the moonlight is the lead's.
3. **Final frame: cutter at the letterbox, bow slab (high)**: fixed.
   - `CutterPosition` (695,-28) -> (770,-10). In the real cutscene (`route_fix/28_ending_3`) the cutter sits at about row 530, ~130 px above the bar, ~80 px long.
   - `WakeFoam`:
     - the hull band is drawn only at t > 0.7 and behind the stem, at half amplitude with noise;
     - Kelvin arm minimum width 1.4 -> 2.5 m;
     - foam clamped to 0.65.
   - New find: the low dawn sun cast the cutter's ~60 m shadow towards the camera as a hard black band on the HDRP water. The cutter's renderers no longer cast shadows (Zone 4 builder).
   - See 11/12 and `fixfinal/boat_close.png`.
4. **Crane shot (medium)**: partly fixed.
   - New `ScatterYardBelt`: a belt 30 m out round the Old Yard, 55 m on the west side the crane looks across.
     - Dense banana, monstera, pachira (2.4-3.4) and shrub understory.
     - 417 leaning (4-9 deg) tree ferns (1.0-1.8) and palm_tree_b, with thin trunk colliders like the other trees.
   - Jungle trees within 60 m of the yard keep their 8ed04fa size: scaled up, they showed more trunk above the wall.
   - 15: the belt reads in the foreground. The far row of trunks behind the west wall is reduced but still visible. Mist remains a lead request.
5. **Sea (medium)**: improved.
   - `BuildSea`:
     - ambient/height scattering 0.15 -> 0.35;
     - scattering colour (0.03,0.30,0.30);
     - endSmoothness 0.92;
     - foam 0.5.
   - `WaterWeatherDriver` calmFoam 0.42 -> 0.55.
   - Measured with `--far_clip 4000`:
     - ending 3 sea (0..700,380..540): G/R 0.92 -> 1.09, R/B 1.50 -> 1.32;
     - day coast rows 370..430: (38,56,65) -> (38,63,70).
   - **The olive horizon band is gone when captured with a 4000 m far clip (16)**. It was the 1000 m default of `cretacea capture`, not the game.
   - In the real cutscene the sea is still warm grey: `route_fix/28_ending_3` (0..700,380..540) (44,41,37) -> (48,47,39). At that grazing angle the water mirrors the khaki dawn sky (lead's dawn key and sky, request 2).
6. **Coast road ends in a dot; double lamp fixture (medium)**: fixture fixed, destination still small.
   - My housing, lens and cap are deleted; the LookFixtures beacon dome is now the lamp head.
   - The mast moved inside the north fence onto the asphalt (z -42.45) and is 3.6 m tall, so the lamp at 4.7 m clears the 3.2 m fence top.
   - `sequenceOrder: 999` and `pulsingEmergency: true`.
   - `Light_EvacSearchlight` is aimed at (548,4,-46), so at dawn its beam crosses the end of the road.
   - `route_fix/27`: the green lamp now reads as a lit post at the end of the road, still small at night. An ~800 cd / 14 m rule is a lead request (3).
7. **River (medium)**: partly fixed.
   - Level = lower bank top (sampled at +/-6.5 m) - 0.25 m, clamped to bed + 0.35..0.95.
   - Scattering (0.10,0.20,0.16), ambient 0.35, foam 0.6.
   - Valley air: the upper channel now shows a sky-blue water surface instead of a brown ditch (`fixsheets/t6_river.png`). From that height it is still a narrow dark ribbon.
   - Foam and rocks on the bends: not done.
8. **Untouched review items (medium)**:
   - Frost grass: the ground-cover cards use their own `Assets/Art/Terrain/MAT_detail_grass.mat`: smoothness remap 0.05-0.32 (was 0.10-0.62), specular occlusion on. NightCalm valley (13): bright tips 99.5th percentile 103 -> 85 in the lower half.
   - Hedge walls: the tall-grass outline wanders +-2 m with noise and fades over ~2 m. It thins over the last 2 m to a gravel road or bare bank, and its fringe turns into tufts.
   - jungle_tree size: visual size Lerp(0.7,1.6). The collider keeps its 8ed04fa world size (see 9).
   - Lake shoreline: not changed beyond the meadow terrain variation.
9. **Colliders and NavMesh (medium)**: verified.
   - Every play-area trunk outside Titan Meadows is now exactly where 8ed04fa put it. `ScatterPlayAreaTrees` runs the base acceptance test in parallel and consumes the base random stream; new meadow trees use their own hash stream. Collider radius, height and base are rescaled to the base world size when a jungle tree is drawn bigger.
   - Measured on the built scene, world-space AABBs vs a build of 8ed04fa's `LookVegetation.cs` (`Tools/agent/cmp_trunks.py`, 0.1 m):
     - Tree_* 2870 -> 2885: 7 removed and 22 added, **all within 90 m of the meadow centre**; every other trunk is identical;
     - rocks and logs 567 -> 567, identical;
     - the new Old Yard belt adds 417 thin trunk colliders outside the yard walls.
   - `NearIntroDino`: 14 m discs round meadow_sauropod_a/b and the three trike spawns keep trees and plants away from the herd.
   - `verify_playmode_doors`: passed, 18/18 doors, 5/5 documents, raptor and T. rex NavMesh valid.
   - `route_test.py --project <clone> --out Screenshots/route_fix`: **38/38 in 266 s**, no new console errors.
10. **Performance (low)**:
    - `detailObjectDistance` 70 -> 60.
    - Play mode, GoldenDay, game view set to **1920x1080** in the hidden editor, same warm-up, 8 samples:

      | Spot | Before (a3c6c52) | After |
      |---|---|---|
      | Valley (128,1.6,0) yaw 77 | gpu 11.7 ms, 3.02 M tris | gpu 7.2 ms, 3.10 M tris |
      | Meadow (-60,-4,-150) | gpu 8.5 ms, 3.71 M tris | gpu 9.8 ms, 4.10 M tris |

    - The meadow's extra ~0.4 M triangles are the meadow plants and tall grass the fix asked for. I switched the clumps to the lighter species (fern_02 has 6 k triangles at LOD0, shrub_02 has 18 k).
    - GPU stays under 10 ms at 1080p. CPU (16-23 ms) is dominated by the batch-mode editor, both before and after.
    - **Not measured in a windowed editor**: this package may only run the hidden one.
11. **Helper scripts outside ownership**: `Tools/agent/*` is removed from the branch (`git rm --cached`). The scripts stay in the clone, untracked, as r2_common asks.

### Files (this round)
- `Assets/Editor/LookPass/LookVegetation.cs`
- `LookVegetationDetails.cs`
- `LookVegetationImpostors.cs`
- `LookTerrainLayers.cs`
- `LookTerrainWater.cs`
- `LookTerrainCoast.cs`
- `Assets/Editor/WorldBuilder/Zone4_TerrainJungleAndDawnCoastBuilder.cs`
- `Assets/Scripts/Rendering/WaterWeatherDriver.cs` (serialized default only)
- `Assets/Art/Terrain/MAT_detail_grass.mat` (new)
- `Assets/Art/Terrain/Meshes/StreamRibbon.asset`
- `Assets/Prefabs/Foliage/PF_detail_grass_*.prefab` (material reference)

### Requests for the lead (updated)
1. **Dawn and dusk mist and haze** (`LookAtmosphereProfiles`):
   - dawn: fogMaxHeight ~40 m above the valley floor, depth extent ~250 m, mean free path ~700-900 m. This is the crane shot's "misty island", and it gives the hills aerial perspective.
   - dusk: the same haze over the hills.
2. **Dawn sea colour in the cutscene**: the water mirrors the khaki dawn sky at a grazing angle. A peach horizon tint, bluer cloud shade and a lower sun (night_b_check) would make it read teal. The far plane is 4000 m already.
3. **Overlook lamp rule**: `^Light_AuxRouteGreen_Overlook` ~800 cd, 14 m, volumetric 1.2, placed before the core `^Light_AuxRouteGreen` rule.
4. Still open from before: a 2048 water-decal atlas, so the wake can get its own ~256 m decal; the `ucli.sh` default-path trap; LookProbes coast-road probe (blue band in route 27).

### Not verified / rough edges
- The dusk aerial canopy is still warm olive, and the crowns read as cards from above (no top-view impostor).
- The far trunk row in the crane shot is still partly visible. The night meadow is still mostly dark.
- The river from the air is still a narrow ribbon. The coast road destination is still a small lamp at night.
