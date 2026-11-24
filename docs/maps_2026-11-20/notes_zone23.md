# Zone 2 + Zone 3 map (Service Paths, Talon Ridge, Paddock 4 gate, jeep / Tyrant Valley, Substation 3, AuxRoute, Old Maintenance Yard)

Mapper: read-only pass on Assets/Editor/WorldBuilder/Zone2_ServicePathsBuilder.cs (369 lines) and Zone3_SubstationAndYardBuilder.cs (563 lines) + kit helpers.
All paths relative to ~/CretaceaBlackoutUnity unless absolute. Coordinates = Unity world metres (X east, Y up, Z north) unless noted.

## 0. Entry points, call order, regeneration

- `cretacea build_all_scenes` -> `CretaceaWorldAssembler.BuildAllScenes` (Assets/Editor/WorldBuilder/CretaceaWorldAssembler.cs:170-237) -> `BuildMainWorldScene` creates `World` at origin (CretaceaWorldAssembler.cs:273) and calls Zone1, then `Zone2_ServicePathsBuilder.Build` (:277), `Zone3_SubstationAndYardBuilder.Build` (:280), THEN Zone4 (terrain) (:283). => Zone 2/3 run BEFORE the terrain exists: every Y is hard-coded (Zone2 Y=0; Substation `SubY=2.3` Zone3:29; Yard `YardY=1.0` Zone3:30). The terrain is shaped to match via plateaus in `Zone4_TerrainJungleAndDawnCoastBuilder.EvaluateWorldElevation` (public static, Zone4:288-347): VC+Paths+Talon plateau X -104..92, Z -96..130, Y -0.05, margin 18 (Zone4:341); Substation X 248..288, Z -19..19, Y 2.26, margin 20 (:342); Yard X 398..508, Z -97..52, Y 0.96, margin 18 (:343).
- Zone roots: `Zone_02_ServicePathsAndTalon` (Zone2:29, 213 transforms live) and `Zone_03_SubstationAndOldYard` (Zone3:34, 544 transforms live, env.txt). Whole scene is regenerated; any hand edit is lost.
- ALL geometry primitives come from `WorldArchitectureKit.CreateBox` = `GameObject.CreatePrimitive(Cube)` scaled by `localScale` (WorldArchitectureKit.cs:926-952). Default cube UVs are 0..1 per face and NO script sets tiling/triplanar (grep for `_UVBase|Triplanar|TextureScale|SetUVs` in Assets/**/*.cs = 0 hits; CretaceaPbrImporter.cs:460-533 sets only maps/remaps). Live dump confirms `tile=(1.00,1.00) uv=0` on every MAT_* (materials.txt). => one texture tile is stretched over each face, whatever its size (a 20.8 m x 5 m wall, a 110 x 146 m yard slab).
- Walls: `BuildWallWithOpenings(parent, start, end, baseY, height, thickness, mat, OpeningSpec[], addSkirting, addDadoRail, palette, wallName, includeInOcclusion)` (Kit:94-175) = one scaled cube per solid pier/lintel/sill (Kit:208-226) + 6.5 cm DarkPaintedMetal jamb/header frames around openings (Kit:228-276). No cap, no coping, no buttress, no texture break.
- Fences: `BuildChainlinkFenceSpan(parent, name, start, end, baseY, height, palette, addDinoBlocker=true, postSpacing=4)` (Kit:880-924) = 8 cm alpha-tested chainlink box + top rail + mid rail + posts every `postSpacing` (posts DO exist; they are 0.14 m square DarkPaintedMetal boxes, no footings).
- Props: `palette.InstantiatePropOrFallback(stem, parent, WORLD pos, euler, targetSize, grounded, addCollider, useMeshCollider, fallbackMat, customName)` (WorldMaterialPalette.cs:351-406): searches Assets/Art/Props, Art/Foliage, ... (WorldMaterialPalette.cs:27-35) by file stem, scales UNIFORMLY so the largest bounds axis = max(targetSize) (:408-429), `grounded` puts bounds.min.y at pos.y (no terrain sampling). Missing asset -> grey cube proxy.
- Lights: `BuildHdrpLight(parent, name, worldPos, euler, type, color, intensityLumens, range, LightGroupId, volumetricDimmer, castShadows, spotOuterAngle, colorTempK=5500, sequenceOrder, rotatingBeacon, pulsingEmergency, blackoutBackupFactor, pairedEmissives)` (Kit:657-704). It writes `light.intensity = intensityLumens` (:684) with no unit set -> the live dump reports every value as CANDELA (lights.txt). The "lm" comments in Zone2/Zone3 are wrong by 4*pi (point) / spot-solid-angle.
- Decals: `BuildDecal` (Kit:829-848) DecalProjector, drawDistance 65 m. Signs: `BuildIlluminatedSign` (Kit:809-827) = 4 cm cube plate with the given material, no light. Rain roofs: `BuildRainBlockerSlab` (Kit:792-807) = primitive cube with NO material assigned (DefaultHDMaterial) on layer 13 (hidden from main cam; likely part of the 62 DefaultHDMaterial renderers, not verified).
- Palette texture lookup is substring-on-path (WorldMaterialPalette.cs:526-550): ConcreteExterior -> concrete_wall set; ConcreteBoardform -> concrete_wall_boardform; AsphaltWet -> asphalt_wet; RockyCliff -> rock_cliff; RustedIron AND CorrugatedMetal both -> metal_corrugated_rusty (hint "rust" matches "rusty"); DarkPaintedMetal -> metal_painted_worn; PlasterPeeling90s -> plaster_painted_peeling; PlasterClean has NO texture (flat 0.76 grey-white). Unused texture sets that fit these zones: concrete_cracked_ext, gravel_road, mud, moss_ground, forest_floor, paving_stone, metal_rusty, rock_mossy, brick_painted (Assets/Art/Textures/).

## 1. Area-by-area table

### 1a. Zone 2 Service Paths corridor (`ServicePaths_MainRoute`, Zone2:46-137) - bounds X 21.5..91, Z -11.3..16.3, floor Y 0
- Centre polyline (21.5,-9)->(40,-9)->(40,14)->(64,14)->(78,-2)->(91,-4) (Zone2:43). Terrain under it = VC plateau Y -0.05 (Zone4:341); Zone4 forbids ALL trees/grass in rect X 20..94, Z -16..36 (Zone4:55) -> outside the walls is bare terrain (grass 85 % / mud 15 % splat, Zone4:124-125).
- Ground: `BuildRoadSegment` (Zone2:354-367) = AsphaltWet cube 0.30 m thick, top Y 0.0, SurfaceType.Concrete. Seg1 (21.5,-9)->(42.3,-9) 20.8 m x 4.6 m (:52); Seg2 (40,-11.3)->(40,16.3) 27.6 m (:53); Seg3 (37.7,14)->(66.3,14) 28.6 m (:54); Seg4 (64,14)->(78,-2) 21.3 m x 5.1 m (:55); Seg5 (77,-2)->(92,-4) 15.1 m x 5.2 m (:56). Segments OVERLAP at every bend with identical top Y 0.0 (coplanar -> z-fighting risk, not verified).
- Walls (all ConcreteExterior = concrete_wall texture, 5.0 m high `WallH` :23, 0.35 m thick `WallT` :24, base Y 0, centred on the road edge 2.3 m off the centre line): Wall_Seg1_N (21.5,-6.7)->(37.7,-6.7) 16.2 m (:66); Wall_Seg1_S (21.5,-11.3)->(42.3,-11.3) 20.8 m (:67); Wall_Seg2_W (37.7,-6.7)->(37.7,16.3) 23 m (:70); Wall_Seg2_E (42.3,-11.3)->(42.3,11.7) 23 m (:71); Wall_Seg3_N_BetweenBranches (42.3,16.3)->(61.7,16.3) (:75); Wall_Seg3_S (42.3,11.7)->(61.5,11.7) (:77); Wall_Seg4_NE (66.3,16.3)->(80.1,0.2) 21.2 m (:80); Wall_Seg4_SW (61.5,11.7)->(75.9,-4.4) 21.6 m (:81); Wall_Seg5_N (80.1,0.2)->(91,-0.9) (:82); Wall_Seg5_S (75.9,-4.4)->(91,-7.1) (:83). No openings anywhere -> each wall = ONE cube (Kit:169-172).
- Decals: PathPuddle_1 (32,0.05,-9) 5.5x3.6; _2 (40,0.05,3) 3.8x6.2; _3 (54,0.05,14) 6.5x3.8; _4 (84,0.05,-3.2) 5.8x3.8 (DecalPuddleWet = Assets/Settings/Rendering/MAT_WetPuddleDecal.mat if present, WorldMaterialPalette.cs:126) (Zone2:59-62); Path_BloodSmear_Bend (61.2,1.35,11.9) 1.8x1.4 on Seg3_S/Seg4_SW corner (:86).
- Lamps (:89-114): `lamp_post` FBX (Assets/Art/Props/lamp_post, single mesh M_lamp_post, no emissive part) scaled 4.4 m tall, collider, at (31,-7.25) yaw 180; (38.25,4) yaw 90; (52,15.75) yaw 180; (62.2,15.65) yaw 180 SPARKING; (71.5,7.6) yaw 225; (85,-1.6) yaw 180 - #0-#2 stand 0.375 m off the inner wall face, #4/#5 1.1-1.5 m off it. Light_PathLamp_0..5 Spot at post XZ +4.1 m (on the pole axis, not on a lamp head), aimed straight down, 2200 cd warm (1,0.86,0.62) [#3: 1800 cd cool (0.85,0.92,1.0), shadows], range 16, outer 78 deg, vol dimmer 1.6; group Paths (#3 VCExit) (:103-106). spark_lamp anchor + `PathLampSparksVfx` (62.2,4.1,15.65) cone-down stretched particles, EmissiveAmber, bursts from StoryDirector (:110-112, :303-352).
- Vegetation (:117-128): 9 trees OUTSIDE the walls, palm_tree/jungle_tree alternating, 8.2 m, no collider: (28,-14.5) (35,-3.5) (34.5,6) (45.5,2) (52,19.5) (55,8.2) (73,14) (82,-9.8) (86,3.2). hanging_vines 3.2x2.4 at (47,3.6,16.05) on Seg3_N and (71,3.6,8.8) yaw 45 = 1.3 m INSIDE the Seg4_NE wall line (floating in mid-air risk, not verified).
- Story: anchors/one-shot triggers paths_corner2 (55,2,14) 5x4x4.8, paths_mid (71,2,6) 5x4x5, paths_end (85,2,-3.5) 5x4x5.5 (:131-136). Reverb zone "Reverb_ServicePaths_Canyon" (56,2.5,4) 24x44 (CretaceaWorldAssembler.cs:1199).

### 1b. North T-branches (`ServicePaths_OpenNorthBranches`, Zone2:143-174) - X 37.7..42.3 and 61.7..66.3, Z 16.3..34
- Branch1: asphalt (40,16.3)->(40,34) (:149); Wall_Branch1_W/E at X 37.7/42.3, 17.7 m (:150-151); Branch1_NorthEndFence chainlink 4.5 m (37.7,34)->(42.3,34) (:152). `wrecked_kiosk` FBX (Assets/Art/Props/Structures/wrecked_kiosk) (40.4,0,21) yaw -15, 2.8 m (:155); Light_Kiosk_Amber Point (40.2,2.4,20.5) 480 cd (1,0.56,0.16) range 9, VCExit, pulsing (:156) - no fixture mesh; `fallen_log` (40,0,26.2) yaw 82, 4.4 m, collider = dead-end blocker (:159). This is what the ServicePaths chapter spawn (40,0.05,6.5) yaw 0 looks at (CretaceaWorldAssembler.cs:118-119): a 4.6 m slot, 5 m walls, kiosk 14.5 m ahead (= play_ServicePaths_5s.jpg).
- Branch2: asphalt (64,16.3)->(64,34) (:162); Wall_Branch2_W/E X 61.7/66.3 (:163-164); Branch2_StaffOnlyGate chainlink 4.2 m (61.8,24)->(66.2,24), no dino blocker (:167); Sign_StaffOnly plate 1.4x0.55 at (64,2.3,23.85), MAT_SignStaffOnly (Assets/Art/UI/Signs/staff_only.png, emissive x1.5), VCExit, no light (:168-169). North end at Z 34 is OPEN (no fence). raptor_cross_a (64,0,30) / raptor_cross_b (64,0,-6) (:172-173).

### 1c. Paddock 4 service gate (`Paddock4_ServiceGate`, Zone2:182-217) - X 89.6..93.8, Z -7.1..-0.9
- GatePost_N (91,2.5,-0.9) / GatePost_S (91,2.5,-7.1) 0.38x5x0.38 DarkPaintedMetal; GateHeader (91,4.95,-4) 0.42x0.35x6.6 (:188-190).
- SouthLeaf_Closed (91,2.4,-5.2) 0.14x4.8x3.6 ChainlinkFence + SouthLeaf_Frame RustedIron rail (:193-194); NorthLeaf_BentUp (91.15,3.05,-2.2) 0.14x3.6x2.4 chainlink slab tilted 10 deg about Z (a flat tilted plate, not bent) (:197); NorthLeaf_BottomLip (90.85,1.28,-2.2) EmissiveAmber bar = crawl hint (:198).
- `fallen_log` (92.4,0,-1.1) euler (26,-52,18) 4.8 m (:201). ControlBollard cube (89.6,0.55,-6) 0.2x1.1x0.2 + `gate_control_box` FBX on top (0.52 m) + Interact_ServiceGateBox (:204-208).
- Anchors fence_gap (91,0,-2.2), paddock_entry (93.8,0.05,-2.2) yaw 85; trigger enter_paddock (93.5,1.5,-2.2) (:211-213).
- Light_ServiceGate_Beacon Spot (90.5,4.4,-2.2) down, 950 cd amber (1,0.52,0.14), range 10, 68 deg, VCExit (:216) - floats 0.5 m west of the header, no lamp. Unused sign texture for it: Assets/Art/UI/Signs/paddock4_gate.png.

### 1d. Talon Ridge raptor pen (`TalonRidge_RaptorPen`, Zone2:224-266) - X -90..20, Z 42..131 (hero view from the Control Room 17 m window)
- TalonPen_Ground: ONE cube (-35,-0.25,86) 110x0.5x90, RockyCliff (rock_cliff texture, 1 tile over 110x90 m), SurfaceType.Mud, top Y 0.0 = 5 cm above the plateau (:230-231). Reads as a flat red-brown rectangle with hard edges (aerial_vc_close.jpg).
- Fences 7.5 m chainlink: SouthW (-90,42)->(-41,42) posts 6 m; SouthE (-31,42)->(20,42); West X -90 and East X 20, Z 42->128, posts 8 m (:234-237). No north fence; ground box runs to Z 131 past the fence ends.
- TalonPylon_W (-41.6,4.2,42) / _E (-30.4,4.2,42) 1.2x8.4x1.2 ConcreteBoardform; TalonGate_HeaderBeam (-36,8,42) 12.6x0.9x1 (:240-242). `talon_gate` = SlidingHorizontal solid GatePanel 10x7.5x0.24 DarkPaintedMetal + RustedIron TopBeam (Kit:329-336), locked, slides +9.5 m X (:245-249).
- Light_Talon_FloodW Spot (-41.6,8.2,42.8) euler (48,22,0) 28000 cd (0.88,0.95,1.0) range 48, 72 deg, shadows; _FloodE (-30.4,8.2,42.8) euler (48,-22,0) no shadows; group Talon (:253-254) - float 0.2 m off the pylon faces, no flood heads. Light_Talon_StrobeW/E Point (+-, 8.8, 42) 550 cd red range 18, PaddockBattery, pulsing (:257-258) - no beacon mesh (Assets/Art/Props/beacon_red exists).
- Only 5 trees inside (jungle/palm 10.5 m): (-46,58) (-34,68) (-22,55) (-52,76) (-14,72) (:261-265). Zone4 excludes all scatter in X -92..22, Z 40..132 (Zone4:54). Unused sign: Assets/Art/UI/Signs/talon_ridge_warning.png.

### 1e. Overturned ranger jeep (`OverturnedRangerJeep`, Zone2:273-301), root (104,0,-7.8) (Zone2:36)
- JeepShoulderPad cube local (0,-0.15,0) 11x0.3x9, ConcreteExterior texture but SurfaceType.Gravel (:280-281) - looks like a concrete slab, top Y 0.0. Site is 12 m outside the VC plateau edge (X 92) so terrain = natural Perlin blended 26 % toward -0.05 (Zone4:341, :361-369): pad may be buried or float (not verified).
- `jeep` FBX (Assets/Art/Props/Vehicles/jeep) at (104,-0.04,-7.8) euler (0,-24,86) (on its side), 4.4 m, collider (:284). jeep_radio DocumentPickup = walkie_talkie FBX at (105.1,0.88,-7.15) (:287-294).
- Light_JeepHazard Point (102.6,0.82,-8.2) 160 cd amber range 8, PaddockBattery pulsing; Light_JeepRadioGlow Point (105.1,1.06,-7.15) 24 cd green range 2.2 (:297-298).
- 13 m east of the Paddock 4 gate, 5.9 m south of Road 3's centre (road Z = -1.9 at X 104, Zone4:31-36). NOTE: prev blueprint put it at (182,-22); the live one is (104,-7.8) (Light_JeepHazard pos in lights.txt).

### 1f. Tyrant Valley structures (`TyrantValley_Structures`, Zone3:49-87) - X 93..250 along Road 3
- Ground is the Zone4 terrain (no Zone3 floor): Road 3 = gravel splat only where dist < 4.5 m from polyline (91,-3.5)(120,0)(160,8)(200,4)(240,0)(250,0) (Zone4:31-36, :138-149); stream trough X~205 cut 2.5 m (Zone4:295-301); 7 tall-grass stealth patches (Zone4:39-48, :420-450); 165 trees + 36 boulders scattered over X -160..528 (Zone4:467-528) = sparse.
- trex_drink anchor (192,0,40); trigger trex_reveal (128,2,0) 9x8x16 (:55-56).
- StreamBridge_Culvert root (201,0,4) (:60-68): BridgeDeck local (0,0.15,0) 11x0.35x5.4 ConcreteExterior (top Y 0.325); CulvertPier_W/E local (-+4.2,-1.1,0) 1.6x2.4x5.6 ConcreteBoardform; Guardrail_N/S local (0,0.78,+-2.55) 10.8x0.12x0.12 RustedIron = two bars with NO posts. Stream centre at z 4 is X 201.6 (Zone4:371); trough fades out only at 15 m, so both deck ends (X 195.5 / 206.5) sit ~2 m above the cut banks (expected, Perlin not evaluated -> not verified). No abutments, wing walls or ramps.
- `feeding_cage` FBX (150,0.2,62) euler (0,34,12), 3.4 m + BaitPen_Blood decal (150,0.3,59) 3.2x3.2 (:71-72) - 54 m north of the road, off-route.
- 4 `watchtower` FBX (Assets/Art/Props/Structures/watchtower) 13.5 m, bottom at hard-coded Y 1.5: (108,150) (236,-150) (382,140) (140,-120) yaw i*80 (:75-84); Light_WatchtowerStrobe_i Point at +14.2 m, 900 cd red range 40, PaddockBattery, pulsing (:85). (140,-120) is inside the Titan Meadows blend (Zone4:304-307, blend ~0.4) -> terrain there ~ -2 m -> tower likely floats ~3 m (not verified).

### 1g. Substation 3 (`Substation3_Compound`, Zone3:94-190) - pad X 249.8..286.2, Z -17.2..17.2, top Y 2.3
- Substation_ConcretePad cube (268,1.95,0) 36.4x0.7x34.4 ConcreteExterior (1 concrete_wall tile over 36x34 m), Concrete (:105-106); puddles SubPad_Puddle1 (264,2.35,2) 8.5x6.5, _2 (276,2.35,-4) 7x5.5 (:107-108). Gravel aprons W (246.5,1.93,0) / E (289.5,1.93,0) 7x0.66x11 rendered with RockyCliff (rock_cliff) but SurfaceType.Gravel, top Y 2.26 (:111-114).
- Fences 4.6 m chainlink on baseY 2.3, posts every 4 m: North Z 17, South Z -17 (X 250..286); WestN/WestS X 250, EastN/EastS X 286 with 6.4 m gaps at Z -3.2..3.2 (:117-122). The two gaps have NO gate leaves, no gate posts. Invisible DinoBlocker ring at +-20.2 m (:125-132).
- Breaker pedestals (`BuildBreakerStation` :192-212): ConcreteFooting 1.22x0.16x0.78 + 2 legs + shelf + backboard 1.04x1.38 + `breaker_box` FBX (1.32 m) at breaker_1 (256,2.3,13) yaw 180; breaker_2 (258,2.3,-13) yaw 0; breaker_3 (282,2.3,13) yaw 180 (:135-137). Each `BuildSubstationInteractStation` (:214-276) adds a red HandleArm cube, StatusLED cube (EmissiveRed), StatusLight Point 18 cd red range 3.2, and an EmissiveRed conduit (r 0.032) lying on the pad: breaker -> (x,2.42,9) -> (272,2.42,9) (:209-211) = the red floor lines in play_Substation_5s.jpg.
- Fuel: `diesel_fuel_tank_pump` FBX (265.2,2.3,12) yaw 180 3.2 m + 2 ConcreteBoardform cradles (:140-143); Station_FuelPump (263.8,3.35,11) (:145-146).
- Generator: Generator_VibrationPlinth (272,2.45,9) 4.6x0.3x2.6; `generator_diesel` FBX (272,2.6,9) 4.1 m; Generator_MetalGrating (272,2.33,6.6) 5.6x0.06x1.8 BrushedSteel = flat plate (Assets/Art/Decals/grating_albedo/normal unused); `generator_starter_panel` (273.5,2.3,7.1) 1.48 m (:149-161).
- Transfer: `switch_cabinet` (279.5,2.3,-7) 2.25 m + `power_transfer_switch` (277.8,2.3,-7) 1.75 m, BrushedSteel conduit between, Station_TransferLever (277.6,3.55,-7), red conduit to (285.5,2.55,-3.5) (:164-170).
- Bulkheads: BulkheadPost cube 0.14x2.9x0.14 + `lamp_bulkhead` FBX (0.32 m) + Light_SubBulkhead_i Point at +2.65 (Y 4.95), 420 cd (1,0.62,0.26), range 11, SubBattery: (279.5,-5.6) (253.5,4.2) (284.5,4.2) (:173-179). These 3 + 6 status lights are the ONLY lights during the Substation objective.
- Flood masts: FloodMast cube 0.24x8x0.24 at (252,15) (284,15) (252,-15) (284,-15) + Light_AuxSubFlood_m Spot at Y 10.1 aimed at (268,2.3,0), 36000 cd (1,0.88,0.68), range 45, 82 deg, AuxSub (OFF until Circuit B), shadows m<2 (:182-189). No flood heads.
- Missing for a "substation": no transformers, insulators, bus bars, cable trays, control hut, HV pylons/lines, signage (Assets/Art/UI/Signs/high_voltage.png unused).

### 1h. AuxRoute beacons (`AuxRoute_EastEscapeBeacons`, Zone3:282-309) - X 288..397.5
- 8 `bollard_light` FBX 1.15 m + AuxGreenCap_i cube 0.24x0.16x0.24 EmissiveGreen at +1.12 (a cube stuck on a modelled bollard) + Light_AuxRouteGreen_i Point at +1.35, 380 cd (0.18,1,0.36), range 12, AuxRoute, sequenceOrder i, paired emissive = cap (:288-308). Positions (hard-coded Y): (288,2.25,-2) (304,1.9,-6.2) (320,1.6,-10.5) (336,1.4,-14.2) (352,1.25,-17.6) (368,1.15,-20.2) (384,1.05,-21.8) (397.5,1.0,-22.8).
- They stand ON Road 3's centre line (road Z at X 304/320/336 = -6.0/-10.4/-14.2, Zone4:31-36), with colliders, not at the road edge. Between the two plateaus (Sub margin ends X 308, Yard margin starts X 380) the terrain is Perlin, so the linear Y guesses can float/bury (not verified).

### 1i. Old Maintenance Yard (`OldMaintenanceYard_1990s`, Zone3:316-535) - X 397..507, Z -95.5..50.5, top Y 1.0
- Lot: Yard_ConcreteSlab = ONE cube (452,0.7,-22.5) 110x0.6x146, ConcreteExterior (one concrete_wall tile over 110x146 m), Concrete (:322-323). Terrain plateau Y 0.96 (Zone4:343); Zone4 bans all scatter in X 396..510, Z -98..54 (Zone4:58).
- Perimeter walls 5.0 m x 0.45 m ConcreteExterior, base Y 1.0 (:328-357): West X 401 (Z -95..50) with squeeze arch at 72.2 m (Z -22.8) 1.6x3.2 -> two piers 71.4 m and 72 m long + lintel; North Z 50 and South Z -95 (104 m each, no openings); East X 505 with E-0 arch at 49 m (Z -46) 8x4.2, frame DarkPaintedMetal -> piers 45 m and 92 m + lintel. Each pier = one stretched cube.
- Squeeze gap: SqueezeGap_BentRustyDoor plate (401.95,2.15,-21.35) 1.15x2.3x0.08 RustedIron euler (0,-24,6) (:337); invisible DinoBlocker (:339); anchors yard_gap (401,1,-22.8), yard_gate_outside (397,1,-22.8); triggers yard_early_warn (398,2.5,-22.8), reach_yard (403,2.5,-22.8) (:341-344). Raptor anchors yard_raptor_a (445,1,20), _b (470,1,-30) (:365-366).
- Warehouse B (:369-392), X 414..456, Z 4..36: `BuildBoxRoomShell` (435,_,20) 42x32, ceiling at Y 9 -> Floor cube ConcreteFloor top Y 1.0 (COPLANAR with the yard slab top 1.0 over 42x32 m -> z-fighting risk), Ceiling cube CorrugatedMetal, RainBlocker_Roof (DefaultHDMaterial, layer 13) (Kit:445-482). Walls 8 m x 0.30 m CorrugatedMetal (metal_corrugated_rusty stretched): WH_West X 414, WH_North Z 36, WH_South Z 4 with arch X 429..436 (7x4.4), WH_East X 456 with arch Z 18..24 (6x4.4). Props: `utility_truck` (426,1,22) yaw 18 5.6 m; 4 `crate` (440/442.5, 1, 12/16.5/21/25.5) 1.4 m; 4 `oil_drum` (418,1,10/13.5/17/20.5). Light_WH_Aux Point (435,8.2,20) 8500 cd range 32, AuxYard, no fixture; ReflectionProbe_WarehouseB 41.5x7.6x31.5. Blueprint's "2 storm-torn roof holes" were never built (prev_agent_unity_aaa_blueprint.md:135).
- Workshop/office block (:394-491), X 412..462, Z -22..-10, wall height `wsH` 3.5: shell floor ConcreteFloor top 1.0 (also coplanar with the slab), ceiling = PlasterPeeling90s (the wall plaster used as ceiling) (:401). Exterior walls PlasterPeeling90s 0.26 m (green peeling interior plaster also on the OUTSIDE): North Z -10 with 6 windows centred X 416/424/432/440/448/456, 2.6 m wide, sill 1.15, h 1.45, 2 mullions, GlassArchitectural (:404-418); South Z -22 solid (:421); West X 412 arch Z -16 1.6x2.3 (:424-425); East X 462 door Z -16 1.6x2.25 + `workshop_door_e` RustedIron leaf with overhead sign "EXIT COURT" that is only an EmissiveGreen plate, no text, group VCExit (:428-432, Kit:381-384). No roof form (flat box top), no fascia, gutter, downpipe, door canopy or exterior lamp.
- Interior: corridor walls Z -14 (door X 422.5, arch X 442.5) and Z -18 (door X 432.5, arch X 452.5) (:435-438); partitions X 432 (north wing) and X 438 (south wing) (:441-442) -> rooms: Foreman office X 412..432/Z -14..-10, Parts archive X 432..462/Z -14..-10, 4 m x 50 m corridor Z -18..-14, Electrical closet X 412..438/Z -22..-18 (EMPTY, no props), Tool workshop X 438..462/Z -22..-18. office_door (422.5,1,-14) / tool_door (432.5,1,-18) WoodPlanks (:444-445). Steam pipe RustedIron r 0.09 (412.2->461.8, Y 3.75, Z -14.35) + 5 risers at X 417/426.5/436/445.5/455 + `industrial_valve_wheel` at Y 2.45 (:448-456). 5 pendants X 417/427/437/447/457, Z -16: rod 0.03x0.56 + PendantShade = flat 0.45x0.14x0.45 DarkPaintedMetal box (not emissive) + Light_WorkshopPendant_l Spot Y 3.8 down, 850 cd (1,0.78,0.48) range 10, 88 deg, YardBattery (l=0,2,4) / AuxYard (1,3), shadows only l=2 (:459-466). Map_Preserve1996 plate (442,2.65,-17.86) 2.2x1.45 (:469-472). Foreman: ForemanDesk = solid WoodPlanks cube 1.8x0.8x0.95 (420,1.4,-11.4), `crt_monitor`, `filing_cabinet` (413.2,1,-11.2), Halvorsen log = PlasterClean paper cube (:475-483). Tool room: ToolWorkbench = solid WoodPlanks cube 3.2x0.9x0.9 (452,1.45,-21.2) + `toolbox_case`; `archive_shelf` (446,1,-11) (:486-488). One box reflection probe for the whole 50 m block (:490).
- Exit court + E-0 (:493-535), X 462..505, Z -67..-22 (blueprint name): Porch_Canopy (466.5,4.6,-42) 5x0.2x26 CorrugatedMetal floating with NO posts, and it is NOT beside the workshop (workshop ends Z -22; canopy spans Z -55..-29) (:499-500); SouthDockBuilding = solid CorrugatedMetal cube (484,3.4,-63) 34x4.8x8, no doors/dock/windows (:501). `e0_gate_leaves` DoubleHingedOutward at (505,1,-46) yaw 90, 8x4 m, two chainlink leaves with DarkPaintedMetal bars, locked (:504-508, Kit:313-328). trigger through_e0_gate (515,3,-46) (:511). `gate_control_box` FBX "E0_GateControlBox" (503.8,1,-52.6) yaw -90 scaled to 1.55 m on the bare slab (the Paddock 4 one is 0.52 m on a bollard) + Interact_E0GateBox hold 9 s (:514-519). Sign_ServiceExitE0 plate 4.2x0.72 at (504.4,5.35,-46) yaw -90, MAT_SignE0 (e0_exit_decal.png), AuxYard - floats 0.36 m off the wall's inner face (:522-523). Light_E0ControlBox_Beacon Point (503.6,3.35,-52.6) 320 cd amber, YardBattery, pulsing - floats 0.8 m above the box (:524). Light_E0Gate_GreenOverhead Spot (502.6,6.2,-46) euler (76,90,0) 1400 cd green range 18, shadows, AuxYard - above the 6.0 m wall top, no fixture (:526). 3 CourtFloodPole cubes 0.22x7.6x0.22 at (474,-28) (492,-34) (482,-54) + Light_CourtAuxFlood_f Spot at Y 8.5 euler (68,45,0) 9500 cd range 34 82 deg AuxYard (:529-534) - nearly straight down, no heads.
- Coast road beyond E-0 (Zone4:546-550): fenced 2.2 m chainlink X 505..546 at Z -42.1 / -49.9.

## 2. Player route through Zones 2-3 (waypoints + beats)

Chapter spawns (CretaceaWorldAssembler.cs:118/128/138/148): ServicePaths (40,0.05,6.5) yaw 0; Substation (252.5,2.35,0) yaw 90; OldYard (404.5,1.05,-22.8) yaw 90 (AuxPower=true); Escape (495.5,1.05,-47.5) yaw 82. Beat handlers: StoryDirector.cs:1532-1580.
1. Dock exit (21.5,-9) -> east down Seg1 to bend (40,-9). Sees: 4.6 m slot, 5 m blank walls both sides, lamp L0 (31,-7.25), puddle (32,-9). Should see: jungle canopy spilling over the wall tops, drainage, stains.
2. North run X 40 (Z -9 -> 14), spawn point (40,6.5). Ahead: Branch 1 dead end with kiosk amber glow (40.4,21) + log (40,26.2). Lamp L1 (38.25,4).
3. East run Z 14 (X 40 -> 64). paths_corner2 trigger (55,14): raptor sprints raptor_cross_a (64,30) -> raptor_cross_b (64,-6) across the Branch 2 mouth + lightning 0.75 (StoryDirector.cs:980-990). Sparking lamp L3 (62.2,15.65), STAFF ONLY gate + sign 9.6 m up Branch 2, blood smear (61.2,1.35,11.9).
4. Diagonal (64,14)->(78,-2): paths_mid (71,6) = "[SOMETHING CRASHES THROUGH THE JUNGLE ABOVE]" bush sound at (75,4,9) (StoryDirector.cs:1589-1595) - there is NO jungle above the walls there (only tree (73,14)). Lamp L4 (71.5,7.6), vine (71,3.6,8.8).
5. (78,-2)->(91,-4): paths_end (85,-3.5) objective "crouch under the bent north leaf" (:1542-1545). Lamp L5 (85,-1.6), puddle (84,-3.2), Paddock 4 gate X 91 with amber crawl lip + amber beacon spot; dead control box (89.6,-6).
6. Crawl at (91,-2.2) -> enter_paddock (93.5,-2.2) -> teleport to paddock_entry (93.8,0.05,-2.2) yaw 85 (StoryDirector.cs:1010-1035). Immediately: jeep on its side 13 m ahead-right (104,-7.8) with amber hazard + green radio (doc 4/5).
7. Tyrant Valley along gravel Road 3 (91,-3.5)->(120,0)->(160,8)->(200,4)->(240,0)->(250,0): trex_reveal (128,0) -> T. rex at trex_drink (192,0,40) by the stream + lightning (StoryDirector.cs:1041-1075); tall grass patches (118,20) (140,-16) (152,26) (226,32) (232,-24); culvert bridge (201,4); watchtower strobes on the horizon.
8. Substation west gap (250,0) -> breakers (256,13) (258,-13) (282,13) -> fuel pump (263.8,11) -> starter (273.5,7.1) -> transfer lever (277.6,-7); red floor conduits turn green; AuxSub floods + AuxRoute ripple on.
9. East gap (286,0) -> AuxRoute bollards (288,-2) ... (397.5,-22.8) along Road 3 (286,0)->(310,-8)->(360,-20)->(401,-22.8), T. rex chase.
10. Squeeze gap (401,-22.8) -> reach_yard (403,-22.8). Yard view east: 7.5 m to the workshop west wall (X 412), west entry (412,-16) is the only lit thing (play_OldYard_5s.jpg). Warehouse B (north, optional) unseen in the dark.
11. Workshop corridor Z -16 from X 412 to 462: pipes, valves, pendants, site map (442,-17.86) VO trigger (442,-16); foreman office door (422.5,-14) -> Halvorsen log (420.4,1.83,-11.4) (doc 5/5).
12. workshop_door_e (462,-16) -> exit court -> E-0 control box (503.8,-52.6) hold 9 s -> e0 gate (505,-46) opens outward -> through_e0_gate (515,-46) -> coast road -> DawnCoast (538,1.05,-46).

## 3. Placeholder-looking geometry (what makes the tall flat walls and the flat walled lot)

- TALL FLAT SERVICE-PATH WALLS = 14 free-standing `BuildWallWithOpenings(..., openings: null)` calls (Zone2:66-83, :150-151, :163-164). With no openings the kit emits ONE `Pier_End` cube per wall (Kit:169-172 -> :177-206 -> :208-226 -> CreateBox :926-952): 5.0 m x 0.35 m x 16-23 m, MAT_ConcreteExterior. The concrete_wall texture is mapped once per face, so a 23 m x 5 m face stretches it 4.6:1 (pores become horizontal streaks, gemqa 35b_service_paths_branch1_day.jpg; orange slabs in play_ServicePaths_5s.jpg). No coping, panel joints, pilasters, kerb, drain, weep holes or stains; every L-joint leaves a 0.175 m square notch on one side of the corner (e.g. inside corner at (37.7,-6.7)) because each cube ends exactly on the shared centre-line point. Outside the walls Zone4 places nothing (exclusion X 20..94, Z -16..36, Zone4:55) -> from above they are thin beige ribbons in an empty brown field (aerial_vc_close.jpg). The design intent (Zone2:9-19 "Enclosed Jungle", "vines draped over 5 m parapets") is carried only by 9 trees + 2 vines.
- HUGE FLAT WALLED LOT = `Yard_ConcreteSlab` one cube 110 x 0.6 x 146 m (Zone3:322) with one concrete_wall tile (big blotches in aerial_yard.jpg) + 4 perimeter walls 5 m x 0.45 m, 104-145 m long, as 1-2 stretched cubes each (Zone3:328-357). Occupied area is only WH B (42x32), workshop (50x12), canopy (5x26), dock box (34x8): empty strips X 401..412, Z -95..-67 (whole south 28 m), X 456..505 x Z -10..50 (49 x 60 m NE quadrant), X 470..505 x Z -59..-22. No markings, kerbs, drains, puddles (zero decals in the yard), weeds, lamp posts, containers or vehicles outside WH B.
- Lights with no fixture: Talon floods (0.2 m off the pylons, Zone2:253-254), Light_ServiceGate_Beacon (Zone2:216), Light_Kiosk_Amber (Zone2:156), Talon strobes (Zone2:257-258), watchtower strobes (Zone3:85), Sub floods on bare 0.24 m sticks (Zone3:185-188), court floods on bare 0.22 m sticks (Zone3:532-533), Light_WH_Aux (Zone3:390), E-0 green overhead (Zone3:526), E-0 box beacon floating 0.8 m above the box (Zone3:524), pendant "shades" = non-emissive flat boxes (Zone3:463); path lamps sit on the pole axis, and lamp_post has no emissive part (single material M_lamp_post).
- Bare boxes standing in for objects: SouthDockBuilding solid 34x4.8x8 cube (Zone3:501); Porch_Canopy floating, post-less, not at the workshop (Zone3:499); ForemanDesk and ToolWorkbench solid cubes (Zone3:475, :486); Halvorsen log = white PlasterClean cube (Zone1_VisitorCenterBuilder.cs:881); bridge guardrails without posts (Zone3:67-68); Generator_MetalGrating flat steel plate (Zone3:154); NorthLeaf_BentUp tilted flat slab (Zone2:197); talon_gate solid 10x7.5 m slab (Kit:334); TalonPen_Ground 110x90 m box (Zone2:230); AuxGreenCap cubes stuck on bollards (Zone3:304); ControlBollard stick (Zone2:204); BulkheadPost sticks (Zone3:176).
- Wrong-reading materials: "gravel" aprons are RockyCliff boxes (Zone3:111-114); jeep "gravel" pad is ConcreteExterior (Zone2:280); workshop exterior = interior PlasterPeeling90s and its ceiling uses the same plaster (Zone3:401-429); RustedIron uses metal_corrugated_rusty (corrugation ribs on posts, rails, door leaves, bent door); chainlink texture is one tile per span (a 51 m x 7.5 m Talon span, a 36 m x 4.6 m Substation span) so the diamond mesh is stretched/aliased away - fences read as rails + posts only (play_Substation_5s.jpg); "EXIT COURT" sign is a text-less EmissiveGreen plate (Kit:381-384).
- Missing roofs/forms: workshop and WH B "roofs" are flat ceiling cubes (no eaves, parapet, gutters, roof holes); light-grey roofs in aerial shots are most likely the hidden RainBlocker slabs (no material = DefaultHDMaterial, layer 13, Kit:792-807; WH B + workshop + RainBlock_Porch Zone3:500) - not visible to the gameplay camera (culling mask CretaceaWorldAssembler.cs:597), visible to any capture camera that renders layer 13 (not verified). These 3 are the only DefaultHDMaterial renderers in Zones 2-3.
- Z-fighting risks (coplanar tops, not verified on screen): WH B floor and workshop floor (top Y 1.0, Kit:466) on the yard slab (top Y 1.0, Zone3:322); road segment overlaps at every service-path bend (all top Y 0.0, Zone2:52-56, :149, :162).
- Unused but ready assets for these zones: signs Assets/Art/UI/Signs/{talon_ridge_warning, paddock4_gate, high_voltage, warehouse_b, warehouse_b_decal, e0_exit}.png; decals DecalCautionStripes/DecalGrimeLeak/DecalCracks are built in the palette (WorldMaterialPalette.cs:125-129) and used by NO builder; Assets/Art/Decals/{moss_patch, footprint_trex, footprint_raptor, grating, cracks_2, grime_leak_2/3}.png unused; every prop has *_MaskMap.png (+ *_MaskMap_Wet.png) on disk that the FBX materials ignore (e.g. Assets/Art/Props/lamp_post/Textures/).

## 4. ASCII top-down sketches (north = up, X east; rasterised from the builder coordinates)

4a. Zone 2 (2 m per column, 2.3 m per row). `#` 5 m wall, `.` road centre, `F` chainlink, `L` lamp post (dark in blackout), `*` sparking lamp (VCExit), `T` tree, `v` hanging vines, `K` kiosk, `=` fallen log, `S` STAFF ONLY sign, `r` raptor_cross_a, `~` puddle, `G` gate posts, `b` gate box, `J` jeep, `@` ServicePaths spawn.
```
  33.7 |          FFF         #.#
  29.1 |          #.#         #r#
  26.8 |          #=#         #.#
  24.5 |          #.#         FSF
  19.9 |          #K#    T    #.#
  17.6 |          #.# v       #.#
  15.3 |          #.#####L####*.##
  13.0 |          #.......~......## T
  10.7 |          #.########### ..##
   8.4 |          #.#     T    ##.vL#
   6.1 |        T #@#           ##..##
   3.8 |          L~#            ##..##   T
   1.5 |          #.# T           ##..##
  -0.8 |          #.#              ##..###L#G=     ...  (Road 3 gravel)
  -3.1 |        T #.#               ##...~..........
  -5.4 |          #.#                #######b
  -7.7 |  ####L####.#                      #G      J
 -10.0 |  .....~....#                   T
 -12.3 |  ###########
 -14.6 |     T
       +----------------------------------------------
         20        40        60        80        100    X
```
4b. Substation 3 (1 m/col, 2 m/row). `F` fence (gaps at Z -3.2..3.2), `,` RockyCliff "gravel" aprons, `1 2 3` breakers, `T` fuel tank, `g` generator, `=` steel plate, `s` starter, `X` transfer lever, `C` switch cabinet, `B` bulkhead post, `M` 8 m flood mast, `- | /` red floor conduits, `~` puddles, `A` first AuxRoute bollard, `@` spawn.
```
  17.0 |       FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF
  15.0 |       F M                               M F
  13.0 |       F     1                         3   F
  11.0 |       F     |        T////ggggg       |   F
   9.0 |       F     -------------/ggggg--------   F
   7.0 |       F       |          ====s==          F
   5.0 |,,,,,,,F  B    |          =======         BF,,,,,,,
   3.0 |,,,,,,,F       |     ~                     F,,,,,,,
   1.0 |,,,,,,,        |                            ,,,,,,,
  -1.0 |,,,,,,,   @    |                            ,A,,,,,
  -3.0 |,,,,,,,F       |                          /F,,,,,,,
  -5.0 |,,,,,,,F       |                 ~  B //// F,,,,,,,
  -7.0 |       F       |                   XC/     F
 -13.0 |       F       2                           F
 -15.0 |       F M                               M F
 -17.0 |       FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF
       +---------------------------------------------------
               250       260       270       280       290   X
```
4c. Old Maintenance Yard (2 m/col, 5 m/row). `#` 5 m perimeter wall, `W` WH B 8 m corrugated walls (gaps = doorways), `w` workshop 3.5 m walls (gap = west entry, `d` = workshop_door_e), `:` roofed interiors, `U` utility truck, `x` crates, `o` drums, `r` raptor anchors, `h` Halvorsen log, `m` site map, `c` floating porch canopy, `D` solid dock box, `P` flood poles, `b` E-0 control box, `E` E-0 gate, `=` coast road, `>` squeeze gap, `A` last bollard, `@` OldYard spawn, `%` Escape spawn.
```
  52.0 |  #####################################################
  37.0 |  #      WWWWWWWWWWWWWWWWWWWWWW                       #
  22.0 |  #      W:::::U::::::::r:::::                        #
  17.0 |  #      W:o::::::::::x:::::::W                       #
   2.0 |  #      WWWWWWW W W WWWWWWWWWW                       #
  -3.0 |  #                                                   #
  -8.0 |  #     wwwwwwwwwwwwwwwwwwwwwwwwww                    #
 -13.0 |  #     w:::h::::::::::::::::::::w                    #
 -18.0 |  #      ::::::::::::::m:::::::::d                    #
 -23.0 | A> @   wwwwwwwwwwwwwwwwwwwwwwwwww                    #
 -28.0 |  #                               cccr P              #
 -33.0 |  #                               ccc           P     #
 -43.0 |  #                               ccc                 E
 -48.0 |  #                               ccc             %   E====
 -53.0 |  #                               ccc      P          b
 -58.0 |  #                                 DDDDDDDDDDDDDDDDD #
 -68.0 |  #                                 DDDDDDDDDDDDDDDDD #
 -78.0 |  #               (empty slab)                        #
 -93.0 |  #####################################################
       +-----------------------------------------------------------
          400       420       440       460       480       500      X
```
4d. Route overview (12 m/col, 16 m/row). `t` Talon pen, `T` talon gate, `V` visitor centre, `K` loading dock, `#` service paths, `G` Paddock 4 gate, `J` jeep, `R` trex_reveal, `D` trex_drink, `~` stream, `H` culvert bridge, `p` bait pen, `g` tall-grass patches, `S` Substation, `A` AuxRoute, `>` squeeze gap, `Y` Old Yard, `E` E-0, `W` watchtowers, `.` Road 3.
```
   144 |                 W        ~             W
   128 | tttttttttt              ~~
    64 | tttttttttt          p  ~
    48 | ttttTttttt             ~ ggg             YYYYYYYYY
    32 |                 gggggg D ggg     ggg     YYYYYYYYY
    16 |            ###  gggggg ~~gggSSSS ggg     YYYYYYYYY
     0 |     V      # ##GJ.R.....H...SSSA..gg     YYYYYYYYY
   -16 |          K##      ggg    ~~gSSSS .A..A.A.>YYYYYYYY
   -48 |                          ~~        gggg  YYYYYYYYE====
  -128 |                    W    ~~
  -144 |                         ~  W
       +--------------------------------------------------------
        -100    0        100     200     300      400     500     X
```

## 5. Dressing opportunities (existing assets; all edits go in the builders, never in the scene)

- GLOBAL first: world-space UVs. Either (a) make `CreateBox` (Kit:926-952) assign a cached cube mesh whose UVs = face size / tile (e.g. 3 m), or (b) in `GetOrBuildPbr` (WorldMaterialPalette.cs:132-161) switch HDRP Lit to triplanar/planar (`_UVBase`, `_TexWorldScale`, then `HDShaderUtils.ResetMaterialKeywords`; check the enum values in HDRP 17). Chainlink needs its own ~1-1.5 m tile or the mesh stays invisible. Also wire each prop's existing `*_MaskMap.png` / `*_MaskMap_Wet.png`.
- Service-path walls: emit 4 m panels (ConcreteExterior / ConcreteBoardform / concrete_cracked_ext), a 0.3 m coping cap at Y 5.0, outside pilasters every 8 m, an inner kerb + 0.3 m drain strip (Assets/Art/Decals/grating_*.png); DecalGrimeLeak streaks under the coping every 3-6 m, moss_patch at the base, DecalCracks on panels, DecalCautionStripes on wall ends at the bends and on the gate posts (91,*,-0.9/-7.1).
- Jungle around the slot: trees every 5-8 m, 2-6 m behind every wall (palm_tree, palm_tree_b, jungle_tree, tree_fern) so crowns overhang the lane; understory hugging the outer faces (monstera_clump, banana_plant, shrub_01..04, fern_02, calathea_orbifolia_01, anthurium_botany_01, pachira_aquatica_01, root_cluster_01, moss_01); hanging_vines every 6-8 m ON the coping (fix the floating one at (71,3.6,8.8) Zone2:128). Shrink the Zone4 exclusion X 20..94, Z -16..36 (Zone4:55) to the corridor polygon + 1 m. Put a fallen palm (fallen_log or palm_tree at ~80 deg) across the Seg4_NE coping near (75,4.5,9) to justify the "crashes through the jungle above" beat (StoryDirector.cs:1589-1595).
- Slot props: debris_pile at the Branch 2 mouth (63,0,17.5); pallet + oil_drum + jerry_can by the kiosk (41.5,0,23.5); utility_cart near the dock end (24,0,-7.6); tree_stump_01 / dead_tree_trunk against wall bases; footprint_raptor decals across Seg3 at X 64 (crossing line) leading to the blood smear; more puddles in the bend overlaps.
- Signs: paddock4_gate.png plate on GatePost_S (91,2.6,-7.3) facing west; high_voltage.png on the Paddock 4 leaves; a route plate at the corner-2 wall (61.2,2.6,11.9); talon_ridge_warning.png every ~15 m on the Talon south fence and on both pylons.
- Talon pen (Control Room window hero shot): delete/replace TalonPen_Ground (Zone2:230) by terrain (mud/forest_floor splat), let Zone4 scatter dense jungle inside except a 12 m apron behind the south fence (Zone4:54), back it with rock_face_01/02, rock_moss_set_01/02, namaqualand_boulder_02..04; feeding_cage + hanging_meat at (-36,0,62) framed by the gate; 2-3 m ConcreteBoardform plinth under the 7.5 m fence + insulator arms + 3 steel wires; beacon_red on the pylon tops (-41.6/-30.4,8.4,42); flood housings at the flood positions.
- Jeep (104,-7.8): swap the ConcreteExterior pad for gravel (gravel_road) or drop it and seat on `Zone4_TerrainJungleAndDawnCoastBuilder.EvaluateWorldElevation(x,z)` (public static, usable before the terrain exists, Zone4:288); add a torn fence panel / flattened sign, jerry_can, skid furrows (mud/cracks decals), a broken bollard_light.
- Valley: culvert abutment ramps from the deck ends down to terrain + guardrail posts every 1.8 m (Zone3:60-68); seat the 4 watchtowers and the feeding cage with EvaluateWorldElevation (Zone3:71-84); boulders (boulder_01, namaqualand_boulder_03, rock_face_01) every 20-30 m at 6-8 m off Road 3 (Zone4 keeps rocks 9.5 m away, Zone4:516); fallen_log/dead_tree_trunk near the reveal (128,0).
- Substation: add 2-3 transformers (no asset exists - kit boxes + Cylinder insulators in BrushedSteel/DarkPaintedMetal, e.g. at (262,2.3,-6) and (270,2.3,-10)), a bus gantry (two 6 m portal frames over X 258..278 at Z -2) with cables, a small brick_painted control hut (~(276,2.3,13.5)), gravel strip inside the fence, a grating cable trench under the red conduits, high_voltage.png on all fence sides and at both gaps, open double gates in the gaps (BuildFramedDoor DoubleHingedOutward), crossarm + 2 lamp housings on each FloodMast, oil_drum/jerry_can/pallet around the fuel tank, utility_truck outside the west apron (~(243,2.2,8)); gravel_road material for the aprons (Zone3:111-114).
- AuxRoute: move the 8 bollards >= 3 m off the road centre (alternate sides), seat on terrain, drop the cube caps (Zone3:304) for a small emissive lens.
- Old Yard: split the slab into AsphaltWet drive lanes (squeeze gap (401,-22.8) east along Z -26..-30, and workshop_door_e (462,-16) to E-0 (505,-46)) + concrete_cracked_ext panels with joint lines; e0_exit_decal stencil on the asphalt at ~(498,1.02,-46) (promised in prev blueprint:150, never built); DecalCautionStripes at the E-0 opening and dock edge; 8-12 DecalPuddleWet + grime oil stains; weeds (grass_medium_01/02, fern_02, shrub_*, moss_01) along every wall base. Clutter for cover/sightline breaks: pallet/crate/oil_drum/jerry_can rows in the NE quadrant (470..500, 10..45), a second utility_truck + an upright jeep in the south strip (430..470, -85..-75), debris_pile, utility_cart, kit-built containers (CorrugatedMetal 6x2.6x2.4). lamp_post on AuxYard along the drive (420,1,-28) (445,1,-28) (470,1,-34) (490,1,-40) + YardBattery lamp_bulkheads over the workshop west entry (412.3,3.4,-16) and inside the squeeze gap (401.4,4.2,-22.8). Workshop: exterior skin (brick_painted/concrete_cracked_ext), corrugated pitched roof with eaves/gutters, canopy over workshop_door_e (move Porch_Canopy there with 2 posts); WH B: warehouse_b.png on the south face (~(432.5,6.5,3.8)) + warehouse_b_decal, the 2 roof holes; SouthDockBuilding -> dock with roller doors (Kit:338-351) and a ramp; perimeter coping + chainlink/barbed top; jungle beyond the walls (shrink Zone4:58).

## 6. Lights in these zones (from builder + live dump)

Group state per chapter (StoryDirector.cs:302-317): in blackout (every Zone 2/3 chapter) Paths and Talon are OFF; VCExit, PaddockBattery, SubBattery, YardBattery ON; AuxSub/AuxRoute/AuxYard follow flag `aux_power` (Substation fix). => In the service paths only lamp #3 (sparking), the kiosk, the gate beacon and the STAFF ONLY plate are lit; 5 of 6 lamp posts are dark. The blackout sequence flickers Paths off over 2.0 s (StoryDirector.cs:716) and Talon off over 0.4 s (StoryDirector.cs:726).
All intensities below are what the dump reports (Candela, lights.txt lines 22-103). E = I/d^2 on the ground under the fixture, for comparison with moonlight 0.1-0.3 lx, road lighting 10-20 lx, emergency lighting 1-5 lx.
| Light (file:line) | Type, I | colour | range | group (blackout) | ground E |
|---|---|---|---|---|---|
| Light_PathLamp_0..5 (Zone2:104-106) | Spot 78 deg down, 2200 cd (#3 1800) | (1,0.86,0.62) / #3 (0.85,0.92,1) | 16 | Paths OFF / #3 VCExit ON | 131 lx below, ~62 lx at the wall |
| Light_Kiosk_Amber (Zone2:156) | Point 480 cd | (1,0.56,0.16) | 9 | VCExit ON, pulsing | 83 lx |
| Light_ServiceGate_Beacon (Zone2:216) | Spot 68 deg down 950 cd | (1,0.52,0.14) | 10 | VCExit ON | 49 lx |
| Light_Talon_FloodW/E (Zone2:253-254) | Spot 72 deg 28000 cd, pitch 48 | (0.88,0.95,1) | 48 | Talon OFF | ~170 lx at 11 m |
| Light_Talon_StrobeW/E (Zone2:257-258) | Point 550 cd | red | 18 | PaddockBattery ON | 7 lx |
| Light_JeepHazard / RadioGlow (Zone2:297-298) | Point 160 / 24 cd | amber / green | 8 / 2.2 | PaddockBattery ON | 18 lx at 3 m |
| Light_WatchtowerStrobe_0..3 (Zone3:85) | Point 900 cd at 15.7 m | red | 40 | PaddockBattery ON | 3.6 lx |
| StatusLight x6 (Zone3:239-247) | Point 18 cd, no group, useT off | red | 3.2 | always | 6 lx |
| Light_SubBulkhead_0..2 (Zone3:178) | Point 420 cd at 2.65 m | (1,0.62,0.26) | 11 | SubBattery ON | 60 lx below, 6 lx at 5 m |
| Light_AuxSubFlood_0..3 (Zone3:187-188) | Spot 82 deg 36000 cd at Y 10.1 aimed at pad centre | (1,0.88,0.68) | 45 | AuxSub | ~22 lx each at centre (x4) |
| Light_AuxRouteGreen_0..7 (Zone3:306-307) | Point 380 cd at 1.35 m, ripple | (0.18,1,0.36) | 12 | AuxRoute | 208 lx at the base, 14 lx at 3 m |
| Light_WH_Aux (Zone3:390) | Point 8500 cd at 7.2 m | (1,0.86,0.62) | 32 | AuxYard | 164 lx |
| Light_WorkshopPendant_0..4 (Zone3:464-465) | Spot 88 deg down 850 cd at 2.8 m | (1,0.78,0.48) | 10 | 0,2,4 YardBattery ON; 1,3 AuxYard | 108 lx |
| Light_E0ControlBox_Beacon (Zone3:524) | Point 320 cd | (1,0.62,0.16) | 9 | YardBattery ON, pulsing | 58 lx |
| Light_E0Gate_GreenOverhead (Zone3:526) | Spot 78 deg 1400 cd | (0.2,1,0.38) | 18 | AuxYard | ~47 lx |
| Light_CourtAuxFlood_0..2 (Zone3:533) | Spot 82 deg 9500 cd at 7.5 m, pitch 68 | (1,0.88,0.68) | 34 | AuxYard | ~135 lx |
- Every BuildHdrpLight also sets `useColorTemperature=true, colorTemperature=5500` (Kit:686-687, :698), so all filter colours are multiplied by a 5500 K tint; contact shadows forced on (:699-700). Only 6 lights here cast shadows (PathLamp_3, Talon_FloodW, AuxSubFlood_0/1, WorkshopPendant_2, E0 green).
- Emissive palette materials: _EmissiveColor = colour x 12-14 (WorldMaterialPalette.cs:118-123, :176-181), signs x1.5 (:206-211); at fixed EV 0.6 the 14-nit emissives clip to flat saturated blobs while the 1.5-nit sign textures (MAT_SignStaffOnly emis (1.43,0.98,0.21), MAT_SignE0 (0.33,1.43,0.54), materials.txt) stay legible.
- Gameplay readability gaps: the Old Yard has NO exterior light before aux power except the E-0 box beacon 100 m away; even with aux on (OldYard chapter AuxPower=true, CretaceaWorldAssembler.cs:138-143) the only exterior lights are the 3 court floods (X 474-492) and the E-0 green, all hidden behind the workshop from the squeeze gap, so the strip X 401..412 between the gap and the workshop is unlit (play_OldYard_5s.jpg is black but for the workshop west entry). The Substation objective is lit by 3 bulkheads (play_Substation_5s.jpg: orange pad, black above). The service paths in blackout are flashlight + kiosk only (play_ServicePaths_5s.jpg).
