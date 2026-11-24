# Round 4 critic report: island (independent art director, read-only)

## Overall

Not amazing yet, and not mergeable as is. Real wins: pond cleared, boulders grey, belt trunks darker, tall grass mixes three looks. But InPond's skipped holders shift the tree random stream, so every play-area tree north of the pond moved (995 to 937 jungle trees; trunks and NavMesh re-rolled): the jeep's framing tree in 17 is gone, a palm blocks the meadow view, and the before/after pairs no longer compare. Both vegetation majors stay unmet: the dusk hill is still a faceted mosaic, impostor shadows cannot reach the band past 260 m, crowns are G/R 1.10. Fix the stream and the belt bodies, redo goals 1-2, measure perf before and after.

## Goals

- **not_met** — 1 (major) Impostor crowns read as stacked ledges from eye level: no straight or domed shelf from eye level AND aerial far canopy luminance >=0.30, hue >=80
  - Evidence: 09 (2x, after at the bottom): the dusk hill is still a mosaic of hard-edged lit 16-gon cap tops over dark gaps; 03b hill luminance std 0.161 vs 0.153 before, lit tops hue 49. DawnCoast_valley_eye and DuskTransition_valley_eye were not recaptured. The aerial condition holds: 04b far canopy 0.286/0.349/0.204 (luminance 0.325, hue 86).
- **not_met** — 2 (major) Dying woodland band: later switch (~0.05), ground shadow at the impostor level, crown G/R >=1.15 and hue >=75 at the switch, trunk ~0.6, no brown trees at dawn
  - Evidence: Switch left at 0.10. The band (02b y140-330) is beyond the 260 m GoldenDay shadow distance and c02 shows no shadow pools there. Crown clusters G/R 1.10, hue 77 (lit tops 71); the quoted 1.20 is the lawn-dominated band mean. Trunks are darker (c02: brown-grey poles, not white). 05b dawn meadow trees still hue 43.
- **partly** — 3 (major) Tall grass reads as a planted crop: dry and dark prototypes by noise, main ~0.45 saturation with lighter tips, falloff ~2.5 m from the gravel, stealth unchanged
  - Evidence: 01b: straw and darker blades mixed in drifts, left front broken into tufts. But stand saturation 0.56 vs 0.60 before (target ~0.45), straw is saturated gold (hue 51, sat 0.57) over whole blades up to ~2 m, right front still near-straight at x~775, no dusk/dawn/storm captures. TallGrassPatch trigger spheres untouched in code.
- **partly** — 4 (major) Ending crane shot reads as a diorama: vary scale (9-22 m), species, tilt, setback; darken trunks with distance
  - Evidence: 06b and the 2x crop: fewer pale trunks and some height variety, but the crown band still reads as one umbrella species with pale trunks at regular spacing; no palm or tree-fern crowns read; belt heights reach ~34 m (vis 1.5 x tall 1.25), past 22 m; a new big crown covers the yard's east corner.
- **not_met** — Minor: lawn reads as a golf course (low-frequency tint and noise, more clumps)
  - Evidence: Not attempted, although it was the cheapest minor in the brief.
- **partly** — Minor: trees out of the Titan Meadows pond, broken bank, reeds
  - Evidence: 04b: no trunks or crowns in the water. The pond is still a smooth ellipse with an even sand ring (crater read), now inside a bare lawn ring; 05b dawn pond matches the lawn (0.231/0.216/0.094 vs 0.220/0.227/0.090).
- **not_met** — Minor: group the lone umbrella trees at the end of the valley road (18)
  - Evidence: Not attempted; route_r4/18 still ends the road on a lone jungle tree and a lone palm.
- **partly** — Minor: orange slab rocks (intro_02/03) and bald orange headland shelves to dark volcanic rock with moss
  - Evidence: Namaqualand boulders now neutral grey (07b 0.278/0.282/0.271), but the slab by the log stays orange (07b 0.343/0.251/0.173, hue 28) and the headland shelves are unchanged (04b 0.522/0.424/0.294 vs 04a 0.522/0.424/0.298). No moss.
- **not_met** — Minor: row of identical pale buttress trunks (intro_02/03)
  - Evidence: Not attempted; 08b now shows four identical pale flared trunks at even spacing across the left third.
- **not_shown** — Minor: palm crown crushing to black (GoldenDay_plaza_eye)
  - Evidence: Not attempted (handed to the lead as a LookMaterials request); no plaza capture in the set.
- **not_met** — Minor: rescue boat without hull foam or wake (28_ending_1/3)
  - Evidence: Not attempted, although WaterWeatherDriver.cs is in the package.
- **not_met** — Minor: shoreline from the air (sand, surf line, pale shallows)
  - Evidence: Not attempted; the 04b coast is unchanged.
- **not_met** — perf_here.sh in the valley and the meadow before and after
  - Evidence: After only: valley 4.8 ms GPU / 4.12 M tris, meadow 7.0 ms / CPU 30.6 ms / 10.69 M tris. r3_island.md measured 7.17 M at the same meadow spot.
- **met** — route_test.py 45/45, verify_playmode_doors, 0 compile errors and warnings, no new console errors
  - Evidence: Tools/agent/r4_route.log: 45/45 in 286 s, no new console errors; r4_doors.log passed (18/18 doors, 5/5 documents, both NavMeshes valid). The route test teleports between checkpoints, so it does not exercise the moved trunks or the belt player bodies.

## Findings

- **[high] InPond re-rolls the whole play-area forest (trunk colliders, NavMesh, staged views)**
  - Evidence: Built MainWorld.unity: 937 Tree_jungle_tree holders vs 995 in the lead's HEAD scene, PlayerBody 1120 vs 1180. Before matches _refs/final (valley_air 0.0 % px >40) but after does not (18.3 %). 01b/02b: valley trees all moved, tan bluff now bare; 03b: new jungle tree + palm block the meadow view; route_r4/17: big tree over the crashed jeep gone; 18: road-end silhouettes changed. So pairs 01-03/07/08 are not like-for-like.
  - Fix: NearStructure (LookVegetation.cs:111) counts earlier holders' Environment capsules; PickTree's b.ok uses it and ry/rt/rd are drawn only if b.ok, so one skipped pond holder shifts the stream for all later cells. Replace 'if (InPond) continue;' with: build holder + NavMesh capsule only (no visual, PlayerBody, dressing), list it, DestroyImmediate after the loop. Prove 0 trunk moves beyond 90 m of (-62,-205) with dump_trunks.cs + cmp_trunks.py.
- **[high] Goal 1 not met: the dusk hill is still a faceted mosaic of lit cap plates**
  - Evidence: 09 bottom (2x): each hill crown is still a hard-edged lit 16-gon top over a dark gap. 03b hill box x150-420,y190-260: luminance std 0.161 vs 0.153 before (not softer), lit tops hue 49 (olive-gold). DawnCoast_valley_eye and DuskTransition_valley_eye (named in the brief) were not recaptured; 01b far line still shows flat umbrella tops. Aerial half passes: 04b far canopy 0.286/0.349/0.204, lum 0.325, hue 86.
  - Fix: Fade the cap by view elevation: tag cap vertices in SaveMesh (uv2.x=1) and give the impostor an HDRP Shader Graph (in Assets/Prefabs/Foliage) that raises alpha clip on tagged texels by saturate((0.34-viewDir.y)/0.17), keeping the 8-tri cap. No-shader fallback: rim 0.55-0.70, centre capY-0.10h, droop 0.30-0.40h, plus two card planes at 45 deg. Recapture the four named eye views at 2x and the aerial.
- **[high] Goal 2 not met: impostor shadows cannot reach the band, crowns still grey-olive, dawn still brown**
  - Evidence: The band (GoldenDay_valley_air y140-330) is ~260-520 m from that camera, past GoldenDay shadowDistance 260 (LookAtmosphereProfiles.cs:228): the ShadowsOnly crowns cannot draw there and c02 (2x) shows none. Band G/R 1.20 is lawn-dominated; k-means crown clusters 0.209/0.230/0.155 and lit tops 0.402/0.440/0.234 = G/R 1.10, hue 77/71. 05b dawn meadow trees 0.161/0.141/0.090, hue 43. Later switch not done.
  - Fix: Add a soft dark ground blob to the impostor mesh (quad at y+0.05, 0.8x crown footprint, radial alpha from a spare atlas cell) so it shows at any range; set the shadow crown's staticShadowCaster=false. OpenValleyGrade: hue pull 0.45->0.75, sat x1.22->x1.4 (cap 0.70), value cap 0.56->0.62; accept on crown clusters G/R>=1.15, hue>=75. Darken LOD2's trunk and thicken its crown, then retry Screen 0.05.
- **[medium] Belt palms and tree ferns get jungle-tree player bodies and skip crown clearance**
  - Evidence: Belt code: palm/fern vis=cand (palm 1.0-1.7, fern 1.6-2.4) with no NearStructure test; AddPlayerBody still uses radius 0.55*vis*max(wide,deep), height 7*vis*tall and the full lean: invisible capsules up to 1.45 m (palm) / 2.05 m (fern) radius, ~19 m tall, leaning 4-8 deg while the visual leans half. The belt covers the coast road east of the yard and Substation 3. route_test.py teleports, so this is untested. 'Visual only' is false.
  - Fix: For beltStem != jungle_tree: wide=deep=tall=1; AddPlayerBody(..., lean*0.5f, leanDir, (fern?0.22f:0.30f)*vis, 4f*vis, sink); set vis=cand only if !NearStructure(ground, (fern?1.6f:3.0f)*cand*0.5f, 4.5f*cand, 9f*cand). Clamp jungle belt vis*tall to 1.22 (22 m; now up to 1.875 = 34 m). Walk E-0 to the overlook in Play mode and log any PlayerBody radius >0.6 m in the belt.
- **[medium] Perf: meadow +49 % triangles with no before number; every impostor 12 to 20 tris; 4 uncompressed 22 MB textures**
  - Evidence: Meadow 10.69 M tris / CPU 30.6 ms vs 7.17 M / 22.1 ms at the same spot (-60,-150, yaw 10) in r3_island.md. jungle_tree_Impostor.asset indexCount 36->60: all impostors incl. ~120k hill instances (HillJungleInstances.bytes 4.32 MB / 36 B) gain 8 tris, yet the notes say the hill is unchanged. 3 tall-grass atlases + dark-bark atlas: 2048 RGBA32, readable, 22.4 MB each plus CPU copy; the source atlas is compressed, non-readable.
  - Fix: Fix finding 1 first (it put a LOD0 jungle tree and a palm in front of the meadow camera), then run perf_here on a base build and after at both spots. Keep the 8-tri cap with the shader fade (or 10 rim points). In TallGrassMaterial/DarkBarkMaterial: EditorUtility.CompressTexture(tex, BC7, Normal), Apply(false, true), and EditorUtility.CopySerialized into the existing asset so the GUID stays.
- **[medium] Goal 3 partly: main stand still ~0.57 saturation, straw is saturated gold, right front still straight**
  - Evidence: 01b right stand (x800-1280,y430-720): overall saturation 0.56 vs 0.60 before (target ~0.45). Straw 0.532/0.488/0.229, hue 51, sat 0.57, over most of each blade and up to 1.96 m tall (1.15 m card x maxHeight 1.7): reads as dying maize leaves, not tan seed heads. Right stand still starts on a near-vertical line at x~775. No dusk, dawn or storm captures of the new grass.
  - Fix: TallGrassMaterial look 0: ss *= 0.55f*(1-0.3f*tip); give looks 0 and 2 their own _BaseColor (0.85,1.0,1.0) instead of the shared (0.72,1.04,1.2) that re-saturates. Look 1: tip = SS(0.65,0.95,t), ss to 0.30, hue 0.10. tallDry maxHeight 1.7->1.3. Near roads: tallD *= 1 - nearRoad*0.9. Capture DuskTransition and DawnCoast valley_eye plus a Records-checkpoint storm frame in the valley.
- **[low] Belt overreaches: the 130 m ring re-dresses chapter 5 and adds a big crown to the crane-shot foreground**
  - Evidence: belt = OutsideYard 6-130 m, so it also covers Substation 3 (x~300, ~95 m out) and the valley east of x 265; chapter 5 frames route_r4/19 and 22 changed 4.4-4.8 % in the upper half and were not reviewed. 06b adds a large crown over the yard's east corner (x860-1280, y545-660), against the r3 rule that kept trees within 60 m of the yard small. c06 (2x): many pale trunks remain; no palm or tree-fern crowns read.
  - Fix: Restrict the belt to the crane shot's view: x > 380 and |z+46| < 140, west of the camera (540,-46); enlarge only where oy > 14; darkBark probability 1.0 beyond 60 m from the camera; palms in the crown layer at 1.3-1.8 only where !NearStructure. Re-shoot 28_ending_1-3 and the chapter 5/6 route frames.
- **[low] Rock and pond minors half done: orange slab stays, headlands untouched, pond still a crater**
  - Evidence: 07b pond-bank slab by the log: 0.343/0.251/0.173 (hue 28, sat 0.50). BasaltRock tints only namaqualand*/rock_face*; the slab is another stem (root_cluster_01 texture 0.391/0.275/0.192, or mountainside/boulder_01). 04b headland shelf A 0.522/0.424/0.294, hue 34, same as 04a. 04b pond: smooth ellipse and even sand ring, now in a bare lawn ring; 05b dawn pond 0.231/0.216/0.094 = lawn 0.220/0.227/0.090. Boulders fixed (0.278/0.282/0.271).
  - Fix: Probe which stem/layer forms the slab and the headlands (r4_near.cs-style dump). Extend BasaltRock to them (mountainside, boulder_01; root_cluster to dark wet bark x(0.6,0.55,0.5)); if the shelves are the terrain Rock layer, tint it in LookTerrainLayers. Break the lake rim with a noise carve plus reeds, and give the lake a cooler, darker dawn colour in LookTerrainWater. Then do the lawn tint (cheapest minor).
- **[low] Files committed outside ownership (beacon glow zeroed, 13 lens materials) and orphan .meta files**
  - Evidence: Assets/Art/Materials/World/Fixtures/MAT_beacon_red_Glow.mat: _EmissiveColor (70,0.70,0.34) -> (0,0,0), LDR -> 0; LightGroupMember takes sharedMaterial emissive as its base, so beacons stay dark until a full rebuild rewrites it. 13 MAT_Lens_*/MAT_Z1_Lens_K*.mat: _Color churn. .meta files committed for gitignored MAT_detail_grass_tall*_Tex.asset (x3) and jungle_tree_DarkBark_Tex.asset: orphan metas on a clean checkout (new warnings).
  - Fix: git checkout 3987803 -- Assets/Art/Materials/World/ and amend. Add 'MAT_detail_grass_tall*_Tex.asset.meta' to Assets/Art/Terrain/.gitignore and 'jungle_tree_DarkBark_Tex.asset.meta' to Assets/Prefabs/Foliage/Textures/.gitignore, then git rm --cached the four metas (same pattern as *_Impostor_Tex.asset.meta). Note in the hand-in that something in the session zeroed the beacon asset.
- **[low] Evidence gaps and false claims in the hand-in**
  - Evidence: No after capture of DawnCoast_valley_eye or DuskTransition_valley_eye (goal 1), of the grass at dusk, dawn or storm (goal 3), or of pond_air/road_end (both in shots.sh). 08a/08b framing differs far more than 'a fraction of a second' (trikes cut by the frame bottom). The notes claim the other trunks keep their places, trunk colliders are unchanged and the hill jungle is unchanged: findings 1, 4 and 5 show otherwise.
  - Fix: After the fixes, shoot every pair from one base build ('a') and one final build ('b'), with the intro captured at a fixed StoryDirector time instead of a sleep, and add the four named eye views, pond_air, road_end and a Records-checkpoint storm frame in the valley. Correct the notes.

## Files changed outside ownership

- Assets/Art/Materials/World/Fixtures/MAT_beacon_red_Glow.mat (emissive zeroed)
- Assets/Art/Materials/World/MAT_Lens_CoolWhite.mat
- Assets/Art/Materials/World/MAT_Lens_Tungsten.mat
- Assets/Art/Materials/World/MAT_Lens_WarmWhite.mat
- Assets/Art/Materials/World/MAT_Z1_Lens_K2700.mat
- Assets/Art/Materials/World/MAT_Z1_Lens_K2800.mat
- Assets/Art/Materials/World/MAT_Z1_Lens_K2900.mat
- Assets/Art/Materials/World/MAT_Z1_Lens_K3000.mat
- Assets/Art/Materials/World/MAT_Z1_Lens_K3100.mat
- Assets/Art/Materials/World/MAT_Z1_Lens_K3200.mat
- Assets/Art/Materials/World/MAT_Z1_Lens_K3300.mat
- Assets/Art/Materials/World/MAT_Z1_Lens_K3500.mat
- Assets/Art/Materials/World/MAT_Z1_Lens_K3600.mat
- Assets/Art/Materials/World/MAT_Z1_Lens_K5000.mat
