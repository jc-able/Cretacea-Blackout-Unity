using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Cretacea.Core;
using Cretacea.Interact;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// Builds Zone 1: Visitor Center ("The Atrium", all 14 rooms), Main Plaza, Viewing Gallery, and Covered Loading Dock
    /// at exact Unity coordinates (unity_aaa_blueprint.md §1.4 & §1.9), mathematically eliminating EVERY Visitor Center defect:
    /// - Security Office: real west exterior window (DOOR-5), Ch.1 spawn facing 0 deg North (POS-9), 6 live 10Hz CCTV monitors,
    ///   amber-lit desk FlashlightPickup (FL-4), shift memo, clock-in terminal.
    /// - Control Room: real 17 m North observation window excluded from Occlusion (DOOR-4), telemetry console (UX-5),
    ///   map_park_modern bound strictly to LightGroupId.VCMain (POS-10).
    /// - Server Room: 4 server racks facing SOUTH inward (not backwards!).
    /// - Storage Room: ACTUAL dressed interior utility/storage room behind locked door_storage (DOOR-8).
    /// - Staff Corridor: segmented skirting & dado rails strictly BETWEEN openings (COL-3).
    /// - Lobby Atrium: aligned skylight roof X=-50..-12 with zero gaps/overhangs/light leaks (POS-11), dramatic museum uplights
    ///   on trex_skeleton plinth, real structural staircase to North Mezzanine, wet puddles under broken panes, ReflectionProbe.
    /// - Staff Exit: aligned staff_exit_door (zero 0.2 m slit, DOOR-9), real broken ceiling framing directly above collapse rubble.
    /// - Feed Prep & Loading Dock: SurfaceType.Tile, ZERO veneer wall behind door_wing (COL-1/DOOR-1), ZERO veneer wall hiding
    ///   dock_roller (COL-2/DOOR-2), storm-backlit 1.25 m crouch gap visible from both inside & outside!
    /// </summary>
    public static class Zone1_VisitorCenterBuilder
    {
        private const float CeilStd = 3.45f;
        private const float CeilCorr = 3.25f;
        private const float LobbyH = 11.0f;
        private const float WallT = 0.26f;

        public static GameObject Build(Transform worldRoot, WorldMaterialPalette palette)
        {
            var root = new GameObject("Zone_01_VisitorCenter");
            if (worldRoot != null) root.transform.SetParent(worldRoot, false);
            InitZoneMaterials(palette);
            s_palette = palette;
            s_cctvFeed = null;
            s_ceilFillRects.Clear();
            Random.State savedRandom = Random.state;
            Random.InitState(1996);   // stable chair yaw etc. between rebuilds

            BuildNorthRoomsShellAndWalls(root.transform, palette);
            BuildStaffCorridorAndDoors(root.transform, palette);
            BuildWestRoomsAndStaffExit(root.transform, palette);
            BuildLobbyAtrium(root.transform, palette);
            BuildFeedPrepWingAndLoadingDock(root.transform, palette);
            BuildPlazaAndViewingGallery(root.transform, palette);
            DressRoomsReview2(root.transform, palette);
            DressRoomsRound3(root.transform, palette);
            DressRoomsRound4(root.transform, palette);

            Random.state = savedRandom;
            RefitPropBoxColliders(root.transform);
            ConsolidateStaticPrimitives(root.transform);
            TagWashReceivers(root.transform);
            return root;
        }

        /// <summary>Rendering (light) layer of the perimeter washes. Every Zone 1 renderer except the ceiling slabs carries it
        /// as well as the default layer, so a wash lights the top of the walls but not the tiles beside the junction
        /// (review 2 fix round: aimed at the junction, a wash put a 176-luma band on the ceiling, brighter than the floor).</summary>
        private const uint WashLayer = 1u << 6;

        /// <summary>Rendering (light) layer that only the ceiling slabs carry: the CeilingFill lights reach the tiles and nothing
        /// else (review 2 round 3: charcoal ceilings at sRGB 34-46 over walls at 90-135, one hot oval per troffer).</summary>
        private const uint CeilLayer = 1u << 7;
        /// <summary>Depth of every ceiling fill source under its ceiling. One depth everywhere so one lamp rule (one candela)
        /// fits every room; 7 m under a cell of at most 6 x 6 m gives an edge/centre ratio of ~0.7 before the neighbours'
        /// overlap, i.e. an even plane. (12 m under 11 m cells with a 24 m range was tried first: the fills reached into the
        /// neighbouring rooms' light-list cells and HDRP dropped lights in blocks on the office and corridor walls.)</summary>
        private const float CeilFillDepth = 7f;

        /// <summary>The light a lit floor and lit walls send back up to a white drop ceiling: per cell of at most ~6 m, one
        /// shadowless upward spot 7 m under the tiles (far under the floor, so the tiles see an even, source-less wash), on
        /// the ceiling-only light layer, in the room's light group (off in the blackout). Calibrated by the "_CeilFill" rule.</summary>
        /// <summary>Plan rectangles (x, z) of every CeilingFill area: only ceiling slabs centred inside one get the ceiling-only
        /// light layer (fix round: a size test alone also tagged the control-room ceiling, the mezzanine deck and canopies,
        /// which neighbouring fills then lit through the walls).</summary>
        private static readonly List<Rect> s_ceilFillRects = new List<Rect>();

        private static void CeilingFill(Transform parent, string room, float x0, float x1, float z0, float z1, float ceilY, LightGroupId group, float kelvin = 5000f, float cell = 6f)
        {
            s_ceilFillRects.Add(Rect.MinMaxRect(x0 - 0.6f, z0 - 0.6f, x1 + 0.6f, z1 + 0.6f));
            int nx = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / cell - 0.05f));
            int nz = Mathf.Max(1, Mathf.CeilToInt((z1 - z0) / cell - 0.05f));
            int i = 0;
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    Vector3 pos = new Vector3(Mathf.Lerp(x0, x1, (ix + 0.5f) / nx), ceilY - CeilFillDepth, Mathf.Lerp(z0, z1, (iz + 0.5f) / nz));
                    string name = i == 0 ? $"Light_{room}_CeilFill" : $"Light_{room}_CeilFill_{i}";
                    Light l = WorldArchitectureKit.BuildHdrpLight(parent, name, pos, new Vector3(-90f, 0f, 0f), LightType.Spot, Color.white, 850f, 10f, group, volumetricDimmer: 0f, castShadows: false, spotOuterAngle: 90f, colorTempK: kelvin);
                    if (l != null)
                    {
                        l.innerSpotAngle = 66f;          // the whole cell in the full-intensity core, the fade spills into the neighbours
                        l.renderingLayerMask = (int)CeilLayer;
                        var hd = l.GetComponent<HDAdditionalLightData>();
                        if (hd != null) { hd.affectSpecular = false; hd.affectsVolumetric = false; }
                    }
                    i++;
                }
        }

        private static bool InCeilFillArea(Vector3 c)
        {
            foreach (Rect rc in s_ceilFillRects) if (rc.Contains(new Vector2(c.x, c.z))) return true;
            return false;
        }

        private static void TagWashReceivers(Transform root)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = r.bounds;
                bool ceilingSlab = b.size.y < 0.35f && b.min.y > 2.6f && b.size.x > 0.8f && b.size.z > 0.8f;
                if ((ceilingSlab || r.name == "Ceiling") && InCeilFillArea(b.center)) { r.renderingLayerMask |= CeilLayer; continue; }
                r.renderingLayerMask |= WashLayer;
            }
        }

        // =========================================================================
        // NORTH WING ROOMS (Z: +22 .. +34, X: -72 .. +8)
        // Security Office | Lockers | Control Room | Server Room | Records | Storage
        // =========================================================================

        private static void BuildNorthRoomsShellAndWalls(Transform parent, WorldMaterialPalette p)
        {
            var northRoot = new GameObject("NorthWing_Offices");
            northRoot.transform.SetParent(parent, false);

            // Individual room floor/ceiling shells with accurate SurfaceTypes + RainBlockers
            // P1-3 FIX: Security Office uses crisp FloorTileClean + SurfaceType.Tile (replaces dark gravel CarpetDark!)
            WorldArchitectureKit.BuildBoxRoomShell(northRoot.transform, "Room_SecurityOffice", new Vector3(-65f, 0f, 28f), 0f, 14f, 12f, CeilStd, mFloorTile, mCeilTile, SurfaceType.Tile, p);
            WorldArchitectureKit.BuildBoxRoomShell(northRoot.transform, "Room_LockerRoom", new Vector3(-53f, 0f, 28f), 0f, 10f, 12f, CeilStd, mFloorTile, mCeilTile, SurfaceType.Tile, p);
            WorldArchitectureKit.BuildBoxRoomShell(northRoot.transform, "Room_ControlRoom", new Vector3(-37f, 0f, 28f), 0f, 22f, 12f, CeilStd, mFloorTile, mCeilTile, SurfaceType.Tile, p);
            WorldArchitectureKit.BuildBoxRoomShell(northRoot.transform, "Room_ServerRoom", new Vector3(-22f, 0f, 28f), 0f, 8f, 12f, CeilStd, mFloorTile, mCeilTile, SurfaceType.Tile, p);
            WorldArchitectureKit.BuildBoxRoomShell(northRoot.transform, "Room_RecordsAnnex", new Vector3(-9f, 0f, 28f), 0f, 18f, 12f, CeilStd, mFloorTile, mCeilTile, SurfaceType.Tile, p);
            // DOOR-8 FIX: Real built floor/ceiling for Janitorial/Storage Closet (X: 0..+8, Z: +22..+34)!
            WorldArchitectureKit.BuildBoxRoomShell(northRoot.transform, "Room_StorageCloset", new Vector3(4f, 0f, 28f), 0f, 8f, 12f, CeilStd, p.ConcreteFloor, p.ConcreteBoardform, SurfaceType.Concrete, p);

            // Exterior North Wall (Z = +34, X = -72 -> +8, len = 80)
            // DOOR-4 FIX: Real 17 m multi-pane Control Room observation window at X = -37 (CenterAlong = 35.0 m), includeInOcclusion = false!
            WorldArchitectureKit.BuildWallWithOpenings(
                northRoot.transform,
                new Vector3(-72f, 0f, 34f),
                new Vector3(8f, 0f, 34f),
                0f, CeilStd, WallT, p.ConcreteBoardform,
                new[]
                {
                    OpeningSpec.Window(35.0f, 17.0f, 0.85f, 2.30f, mullions: 6, frameMat: p.DarkPaintedMetal, glassMat: mGlass)
                },
                addSkirting: true, addDadoRail: false, palette: p, wallName: "Wall_VC_NorthExterior", includeInOcclusion: false);

            // Exterior West Wall of Security Office (X = -72, Z = +22 -> +34, len = 12)
            // DOOR-5 FIX: Real recessed steel-framed exterior window (Z = +27..+30, CenterAlong = 6.5 m) for Opening Shot 6 push-in!
            WorldArchitectureKit.BuildWallWithOpenings(
                northRoot.transform,
                new Vector3(-72f, 0f, 22f),
                new Vector3(-72f, 0f, 34f),
                0f, CeilStd, WallT, p.ConcreteExterior,   // fix round: the lighter exterior concrete (board-form sat at sRGB ~14 in the 23:04 shot)
                new[]
                {
                    OpeningSpec.Window(6.5f, 3.0f, 1.10f, 1.45f, mullions: 2, frameMat: p.DarkPaintedMetal, glassMat: mGlass)
                },
                addSkirting: true, addDadoRail: false, palette: p, wallName: "Wall_Security_WestExterior", includeInOcclusion: false);

            // Exterior East Wall of Storage Closet (X = +8, Z = +22 -> +34)
            WorldArchitectureKit.BuildWallWithOpenings(northRoot.transform, new Vector3(8f, 0f, 22f), new Vector3(8f, 0f, 34f), 0f, CeilStd, WallT, p.ConcreteBoardform, null, addSkirting: true, palette: p, wallName: "Wall_Storage_EastExterior");

            // Interior North Room Partitions (Z = +22 -> +34)
            WorldArchitectureKit.BuildWallWithOpenings(northRoot.transform, new Vector3(-58f, 0f, 22f), new Vector3(-58f, 0f, 34f), 0f, CeilStd, 0.20f, mOfficePaint, null, addSkirting: true, palette: p, wallName: "Partition_Sec_Lockers");
            WorldArchitectureKit.BuildWallWithOpenings(northRoot.transform, new Vector3(-48f, 0f, 22f), new Vector3(-48f, 0f, 34f), 0f, CeilStd, 0.20f, mOfficePaint, null, addSkirting: true, palette: p, wallName: "Partition_Lockers_Control");
            WorldArchitectureKit.BuildWallWithOpenings(northRoot.transform, new Vector3(-26f, 0f, 22f), new Vector3(-26f, 0f, 34f), 0f, CeilStd, 0.20f, mOfficePaint, null, addSkirting: true, palette: p, wallName: "Partition_Control_Server");
            WorldArchitectureKit.BuildWallWithOpenings(northRoot.transform, new Vector3(-18f, 0f, 22f), new Vector3(-18f, 0f, 34f), 0f, CeilStd, 0.20f, p.BrickOld, null, addSkirting: true, palette: p, wallName: "Partition_Server_Records");
            WorldArchitectureKit.BuildWallWithOpenings(northRoot.transform, new Vector3(0f, 0f, 22f), new Vector3(0f, 0f, 34f), 0f, CeilStd, 0.20f, p.BrickOld, null, addSkirting: true, palette: p, wallName: "Partition_Records_Storage");

            // Interior linings: the exterior walls are boardform concrete on both faces, the rooms behind them are painted.
            const float hT = WallT * 0.5f;
            Liner(northRoot.transform, "Lining_Security_N", new Vector3(-72f, 0f, 34f), new Vector3(-58f, 0f, 34f), Vector3.back, hT, CeilStd, mOfficePaint);
            Liner(northRoot.transform, "Lining_Security_W", new Vector3(-72f, 0f, 22f), new Vector3(-72f, 0f, 34f), Vector3.right, hT, CeilStd, mOfficePaint, new[] { (5.0f, 8.0f, 1.10f, 2.55f) });
            Liner(northRoot.transform, "Lining_Lockers_N", new Vector3(-58f, 0f, 34f), new Vector3(-48f, 0f, 34f), Vector3.back, hT, CeilStd, mOfficePaint);
            Liner(northRoot.transform, "Lining_Control_N", new Vector3(-48f, 0f, 34f), new Vector3(-26f, 0f, 34f), Vector3.back, hT, CeilStd, mOfficePaint, new[] { (2.5f, 19.5f, 0.85f, 3.15f) });
            Liner(northRoot.transform, "Lining_Server_N", new Vector3(-26f, 0f, 34f), new Vector3(-18f, 0f, 34f), Vector3.back, hT, CeilStd, p.PlasterClean);
            Liner(northRoot.transform, "Lining_Records_N", new Vector3(-18f, 0f, 34f), new Vector3(0f, 0f, 34f), Vector3.back, hT, CeilStd, p.PlasterClean);

            // Dress each North Room
            DressSecurityOffice(northRoot.transform, p);
            DressLockerRoom(northRoot.transform, p);
            DressControlRoom(northRoot.transform, p);
            DressServerRoom(northRoot.transform, p);
            DressRecordsAnnex(northRoot.transform, p);
            DressStorageCloset(northRoot.transform, p);

            // Box-Projected Reflection Probes + InteriorZoneVolumes for all North Rooms
            WorldArchitectureKit.BuildRoomReflectionProbe(northRoot.transform, "Security", new Vector3(-65f, 1.7f, 28f), new Vector3(13.8f, 3.4f, 11.8f), "vc_offices");
            WorldArchitectureKit.BuildRoomReflectionProbe(northRoot.transform, "Control", new Vector3(-37f, 1.7f, 28f), new Vector3(21.8f, 3.4f, 11.8f), "vc_offices");
            WorldArchitectureKit.BuildRoomReflectionProbe(northRoot.transform, "Records", new Vector3(-9f, 1.7f, 28f), new Vector3(17.8f, 3.4f, 11.8f), "vc_offices");
            WorldArchitectureKit.BuildRoomReflectionProbe(northRoot.transform, "Storage", new Vector3(4f, 1.7f, 28f), new Vector3(7.8f, 3.4f, 11.8f), "vc_offices");
        }

        private static void DressSecurityOffice(Transform parent, WorldMaterialPalette p)
        {
            var sec = new GameObject("Props_SecurityOffice");
            sec.transform.SetParent(parent, false);

            // POS-9 FIX: Spawn facing 0 deg NORTH directly toward the CCTV Desk & glowing flashlight!
            WorldArchitectureKit.BuildWorldAnchor(sec.transform, "start", new Vector3(-64.5f, 0.05f, 28.0f), yawDeg: 0f);
            WorldArchitectureKit.BuildWorldAnchor(sec.transform, "opening_shot6_window", new Vector3(-72.0f, 1.8f, 28.5f), yawDeg: 90f);
            // warm wall-pack over the west window: the key light of the '23:04' push-in (intro check: the shot opened black)
            WorldArchitectureKit.BuildHdrpLight(sec.transform, "Light_SecWest_WallPack", new Vector3(-72.55f, 3.05f, 28.5f), new Vector3(62f, -90f, 0f), LightType.Spot, Color.white, 600f, 9f, LightGroupId.Plaza, volumetricDimmer: 0.6f, castShadows: false, spotOuterAngle: 100f, colorTempK: 2700f);

            DressSecurityWestExterior(sec.transform, p);

            // Main Security CCTV Desk (grounded flush at Y = 0; P1-3 warm matte wood top prevents white specular glare!)
            // CCTV operator desk (top at 0.82 m, where the flashlight, radio and memo sit)
            Transform cctvDesk = Desk(sec.transform, "CctvDesk", new Vector3(-65.0f, 0f, 31.2f), 0f, 3.4f, 1.0f, mVeneer, mCabinet, 0.82f);
            // round 4 (review 3 intro_20: "the keyboard, phone and logbook are black slabs"): a beige keyboard with key rows,
            // a desk phone with its handset and coiled cord, the open night logbook in hand-writing
            DeskKeyboard(cctvDesk, "Keyboard", new Vector3(0.0f, 0.822f, -0.22f), 0f);
            Box(cctvDesk, "Mouse", new Vector3(0.32f, 0.834f, -0.22f), new Vector3(0.06f, 0.025f, 0.1f), mBeigePlastic);
            Box(cctvDesk, "MousePad", new Vector3(0.32f, 0.823f, -0.21f), new Vector3(0.2f, 0.003f, 0.17f), mFelt);
            DeskPhone(cctvDesk, "DeskPhone", new Vector3(-1.3f, 0.822f, 0.15f), 14f);
            // (a cylinder mug with a handle and a round-shaded lamp: review 2 saw a cube and a slab on a stick in the
            // middle of the first gameplay frame)
            Prim(cctvDesk, "Mug", PrimitiveType.Cylinder, new Vector3(1.45f, 0.868f, 0.05f), new Vector3(0.082f, 0.048f, 0.082f), mPaper);
            Box(cctvDesk, "MugHandle", new Vector3(1.50f, 0.868f, 0.05f), new Vector3(0.030f, 0.055f, 0.012f), mPaper);
            Prim(cctvDesk, "MugCoffee", PrimitiveType.Cylinder, new Vector3(1.45f, 0.9125f, 0.05f), new Vector3(0.070f, 0.002f, 0.070f), mBlack);
            Logbook(cctvDesk, "Logbook", new Vector3(-0.45f, 0.822f, 0.15f), -8f,
                "19 Nov\n22:00 on shift - Vance\n22:40 Paddock 4 gate\n   sticking again\n23:15 radio check ok\n00:30 rain, all quiet\n02:10 T.Valley cam 3\n   flicker (reported)",
                "20 Nov\n21:58 on shift\n22:05 Talon Ridge feed ok\n22:30 storm warning\n   from the mainland\n23:00 Ops: switchover\n   at midnight\n23:04 ...");
            BinderRow(cctvDesk, "DeskBinders", new Vector3(-66.62f, 0.82f, 31.52f), 0f, 6, 11);   // world point (Group places in world space)
            // a balanced-arm lamp with a conical shade (its light is Light_Security_DeskTask, not part of this model)
            DeskLamp(cctvDesk, "DeskLamp", new Vector3(1.2f, 0.822f, 0.3f), 0f, mFeatureGreen);
            p.InstantiatePropOrFallback("office_chair", sec.transform, new Vector3(-65.0f, 0f, 29.9f), new Vector3(0f, 15f, 0f), new Vector3(0.65f, 1.05f, 0.65f));

            // FL-4 FIX: Tactical Flashlight Desk Pickup right in view of Ch. 1 spawn with warm amber rim LED!
            BuildFlashlightDeskPickup(sec.transform, new Vector3(-64.0f, 0.82f, 31.0f), p);
            p.InstantiatePropOrFallback("walkie_talkie", sec.transform, new Vector3(-64.35f, 0.82f, 31.05f), new Vector3(0f, -22f, 0f), new Vector3(0.08f, 0.22f, 0.06f), grounded: true, addCollider: false);

            // Document 1/5: Night Shift Switchover Memo on Security Desk
            BuildDocumentProp(sec.transform, new Vector3(-65.8f, 0.83f, 30.95f), "memo_switchover", "Night Shift Switchover Memo",
                "Read shift memo {interact}", new[] { "memo_switchover" },
                new[] { "SWITCHOVER MEMO - 00:00 cutover from Substation 1 to Substation 5. Log Talon Ridge & Tyrant Valley feeds first." }, p);

            // Clock-in Security Terminal Desk (-61, 0, +26)
            Transform termDesk = Desk(sec.transform, "TerminalDesk", new Vector3(-61.0f, 0f, 26.0f), 0f, 1.5f, 0.8f, mLaminate, mCabinet, 0.80f);
            Box(termDesk, "Keyboard", new Vector3(0.0f, 0.812f, -0.12f), new Vector3(0.44f, 0.022f, 0.15f), mBlack);
            Box(termDesk, "CardReader", new Vector3(0.48f, 0.84f, -0.05f), new Vector3(0.10f, 0.06f, 0.14f), mBlack);
            PaperStack(termDesk, "Timesheets", new Vector3(-61.5f, 0.80f, 25.95f), 10f, 3);
            p.InstantiatePropOrFallback("lcd_monitor", sec.transform, new Vector3(-61.0f, 0.80f, 26.15f), new Vector3(0f, 180f, 0f), new Vector3(0.58f, 0.44f, 0.20f));
            WorldArchitectureKit.BuildWorldAnchor(sec.transform, "terminal", new Vector3(-61.0f, 0.95f, 26.0f));
            BuildSimpleInteractableBox(sec.transform, "Interact_Terminal", new Vector3(-61.0f, 1.05f, 26.0f), new Vector3(1.1f, 0.9f, 0.9f),
                "terminal", "Clock in at security terminal {interact}", holdSec: 0f, singleUse: true, startsEnabled: true, startsLocked: true, lockedHint: "Grab your tactical flashlight from the CCTV desk first.");

            // Grounded storage lockers, filing cabinets & East wall security bulletin trim (P1-3 visual richness!)
            p.InstantiatePropOrFallback("filing_cabinet", sec.transform, new Vector3(-70.8f, 0f, 33.43f), new Vector3(0f, 90f, 0f), new Vector3(0.55f, 1.38f, 0.65f));
            p.InstantiatePropOrFallback("filing_cabinet", sec.transform, new Vector3(-69.98f, 0f, 33.43f), new Vector3(0f, 90f, 0f), new Vector3(0.55f, 1.38f, 0.65f));
            p.InstantiatePropOrFallback("lockers", sec.transform, new Vector3(-58.36f, 0f, 31.8f), new Vector3(0f, -90f, 0f), new Vector3(1.8f, 2.0f, 0.55f));
            CorkBoard(sec.transform, "SecEast_Bulletin", new Vector3(-58.1f, 1.6f, 27.4f), Vector3.left, 2.0f, 1.1f, 7);
            Poster(sec.transform, p, "SecEast_ParkMap", "map_park_modern", new Vector3(-58.1f, 1.65f, 24.6f), Vector3.left, new Vector2(1.6f, 0.95f));
            Poster(sec.transform, p, "SecWest_FireAssembly", "fire_assembly", new Vector3(-71.847f, 1.6f, 24.2f), Vector3.right, new Vector2(0.55f, 0.55f));
            // Key cabinet and wall clock by the door, radio charger shelf under the window
            Transform keys = Group(sec.transform, "KeyCabinet", new Vector3(-60.2f, 1.55f, 22.11f), 180f);
            Box(keys, "Box", new Vector3(0f, 0f, -0.05f), new Vector3(0.55f, 0.65f, 0.1f), mCabinet);
            Box(keys, "Door", new Vector3(0f, 0f, -0.102f), new Vector3(0.53f, 0.63f, 0.006f), mGlass);
            for (int k = 0; k < 8; k++) Box(keys, $"Key_{k}", new Vector3(-0.2f + (k % 4) * 0.13f, 0.15f - (k / 4) * 0.25f, -0.07f), new Vector3(0.02f, 0.07f, 0.01f), k % 3 == 0 ? mBinderRed : mSteel);
            Transform clock = Group(sec.transform, "WallClock", new Vector3(-65.0f, 2.75f, 33.847f), 0f);
            Box(clock, "Face", new Vector3(0f, 0f, -0.02f), new Vector3(0.32f, 0.32f, 0.04f), mPaper);
            Box(clock, "Rim", new Vector3(0f, 0f, -0.015f), new Vector3(0.36f, 0.36f, 0.03f), mBlack);
            Box(clock, "HandH", new Vector3(0.03f, 0.02f, -0.045f), new Vector3(0.09f, 0.012f, 0.005f), mBlack, false, new Vector3(0f, 0f, 35f));
            Box(clock, "HandM", new Vector3(-0.01f, 0.06f, -0.046f), new Vector3(0.008f, 0.13f, 0.005f), mBlack, false, new Vector3(0f, 0f, -10f));
            Transform wdesk = Desk(sec.transform, "RadioDesk", new Vector3(-71.35f, 0f, 28.5f), -90f, 1.6f, 0.9f, mLaminate, mCabinet);
            p.InstantiatePropOrFallback("crt_monitor", wdesk, new Vector3(-71.45f, 0.76f, 28.9f), new Vector3(0f, 90f, 0f), new Vector3(0.44f, 0.42f, 0.42f), grounded: true, addCollider: false);
            for (int r = 0; r < 3; r++)
                p.InstantiatePropOrFallback("walkie_talkie", wdesk, new Vector3(-71.55f, 0.76f, 27.95f + r * 0.12f), new Vector3(0f, 90f, 0f), new Vector3(0.08f, 0.22f, 0.06f), grounded: true, addCollider: false);
            Box(wdesk, "ChargerBase", new Vector3(0.45f, 0.775f, 0.2f), new Vector3(0.42f, 0.03f, 0.14f), mBlack);
            Chair(sec.transform, "RadioDeskChair", new Vector3(-70.6f, 0f, 28.7f), 80f);
            p.InstantiatePropOrFallback("trash_bin", sec.transform, new Vector3(-66.9f, 0f, 31.3f), Vector3.zero, new Vector3(0.36f, 0.6f, 0.36f));
            WallExtinguisher(sec.transform, p, new Vector3(-59.2f, 0f, 22.11f), Vector3.forward);   // clear of the open door leaf

            // 6-Monitor Live 10Hz CCTV Bank on North Wall (Z = +33.65)
            BuildSecurityCctvBank(sec.transform, p);

            // P1-3 CALIBRATED Security Office Lights: moody, readable night-shift atmosphere with ZERO desk whiteout!
            // Troffer grid: 4 fixtures (the first shadowed), a ceiling wash in the middle
            Vector2[] secFx = { new(-68.5f, 25f), new(-61.5f, 31f), new(-61.5f, 25f), new(-68.5f, 31f) };
            for (int i = 0; i < secFx.Length; i++)
                CeilingLamp(sec.transform, i == 0 ? "Light_Security_Main" : $"Light_Security_Main_{i + 1}", new Vector3(secFx[i].x, CeilStd - 0.15f, secFx[i].y), LightGroupId.VCMain, 3500f, shadows: i == 0);
            Washes(sec.transform, "Security", -71.5f, -58.5f, 22.5f, 33.5f, LightGroupId.VCMain, walls: "NNEES");
            WorldArchitectureKit.BuildHdrpLight(sec.transform, "Light_Security_DeskTask", new Vector3(-64.5f, 1.95f, 31.0f), new Vector3(72f, 0f, 0f), LightType.Spot, new Color(1.0f, 0.80f, 0.52f), 135f, 4.8f, LightGroupId.VCMain, volumetricDimmer: 0.35f, castShadows: false, spotOuterAngle: 56f);   // mains: it dies with the power (review 2)
            BatteryLight(sec.transform, "Security", -1, new Vector3(-61.2f, CeilStd - 0.15f, 22.9f));
        }

        /// <summary>Round 3 (review 2 intro_17: the '23:04' push-in is "a flat elevation of a featureless dark wall with one
        /// window and a wall pack"): the west exterior wall of the security office (outer face x = -72.13, z 22..34) gets a
        /// light fascia band and a gutter under the roof line, two downpipes with shoes, a condenser unit on a pad with its
        /// pipe run, a meter box with a HIGH VOLTAGE plate and conduit to the wall pack, drip streaks under the gutter and
        /// the sill, and wet puddles in front of the window that catch its glow. Nothing on a walkable route.</summary>
        private static void DressSecurityWestExterior(Transform parent, WorldMaterialPalette p)
        {
            var g = new GameObject("SecWest_Exterior");
            g.transform.SetParent(parent, false);
            Transform t = g.transform;
            const float face = -72.13f;
            Box(t, "Fascia", new Vector3(face - 0.03f, 3.58f, 28f), new Vector3(0.06f, 0.2f, 12.3f), mCabinet);
            Box(t, "Gutter", new Vector3(face - 0.13f, 3.72f, 28f), new Vector3(0.16f, 0.12f, 12.3f), mDarkMetal);
            Box(t, "GutterLip", new Vector3(face - 0.21f, 3.77f, 28f), new Vector3(0.02f, 0.05f, 12.3f), mDarkMetal);
            foreach (float z in new[] { 22.35f, 33.65f })
            {
                Prim(t, $"Downpipe_{z:F0}", PrimitiveType.Cylinder, new Vector3(face - 0.1f, 1.95f, z), new Vector3(0.1f, 1.75f, 0.1f), mDarkMetal);
                Box(t, $"DownpipeShoe_{z:F0}", new Vector3(face - 0.22f, 0.14f, z), new Vector3(0.28f, 0.1f, 0.1f), mDarkMetal);
                for (int b = 0; b < 3; b++)
                    Box(t, $"DownpipeBracket_{z:F0}_{b}", new Vector3(face - 0.05f, 0.8f + b * 1.1f, z), new Vector3(0.1f, 0.03f, 0.14f), mDarkMetal);
                WallDecal(t, $"Drip_{z:F0}", p.DecalGrimeLeak, new Vector3(face, 2.6f, z + (z < 28f ? 0.6f : -0.6f)), Vector3.left, new Vector2(0.9f, 2.0f), 0.7f);
                DecalAt(t, $"Puddle_Shoe_{z:F0}", p.DecalPuddleWet, new Vector3(face - 0.7f, 0.05f, z), 1.3f, z * 13f, 0.9f);
            }
            // condenser unit on a concrete pad, pipe run up the wall into the office
            // fix round: the interest moved within ~3 m of the window (z 27-30), so it stays in frame through the push-in of shot
            // 6: dummy steel door with a SECURITY plaque at z 31.3, condenser beyond it at z 32.9, meter box at z 26.3
            Transform ac = Group(t, "Condenser", new Vector3(face - 0.55f, 0f, 32.9f), 90f);
            Box(ac, "Pad", new Vector3(0f, 0.05f, 0f), new Vector3(1.1f, 0.1f, 0.7f), p.ConcreteExterior, true);
            Box(ac, "Cabinet", new Vector3(0f, 0.5f, 0f), new Vector3(0.95f, 0.8f, 0.42f), p.PlasterClean, true);   // light enamel: it read as a black cube
            Box(ac, "Grille", new Vector3(0f, 0.52f, -0.215f), new Vector3(0.72f, 0.62f, 0.01f), mSteel);
            for (int gl = 0; gl < 7; gl++) Box(ac, $"GrilleSlat_{gl}", new Vector3(0f, 0.27f + gl * 0.085f, -0.224f), new Vector3(0.72f, 0.012f, 0.008f), mDarkMetal);
            Prim(ac, "FanGuard", PrimitiveType.Cylinder, new Vector3(0f, 0.52f, -0.226f), new Vector3(0.52f, 0.004f, 0.52f), mDarkMetal, new Vector3(90f, 0f, 0f));
            Prim(ac, "FanHub", PrimitiveType.Cylinder, new Vector3(0f, 0.52f, -0.232f), new Vector3(0.1f, 0.006f, 0.1f), mSteel, new Vector3(90f, 0f, 0f));
            Box(ac, "ServicePanel", new Vector3(0.4f, 0.55f, 0f), new Vector3(0.16f, 0.5f, 0.43f), mCabinet);
            Transform acLbl = Group(ac, "RatingPlate", ac.TransformPoint(new Vector3(0.4f, 0.62f, -0.2165f)), ac.eulerAngles.y);
            Box(acLbl, "Plate", Vector3.zero, new Vector3(0.1f, 0.07f, 0.003f), mPaper);
            Box(ac, "TopCover", new Vector3(0f, 0.91f, 0f), new Vector3(0.98f, 0.03f, 0.45f), mCabinet);
            Box(t, "AcLineSet", new Vector3(face - 0.06f, 1.75f, 32.35f), new Vector3(0.08f, 2.1f, 0.12f), mCabinet);
            Box(t, "AcLineSetElbow", new Vector3(face - 0.2f, 0.75f, 32.35f), new Vector3(0.3f, 0.08f, 0.12f), mCabinet);
            // dummy steel door (no gameplay: the office is entered from the corridor) with frame, closer, kick plate, lever and
            // a printed SECURITY plaque, lit by the wall pack over it; a concrete step in front
            Transform dr = Group(t, "SecWest_SteelDoor", new Vector3(face, 0f, 31.3f), -90f);   // local +Z out of the wall, +X towards +z
            Box(dr, "JambL", new Vector3(-0.56f, 1.13f, 0.035f), new Vector3(0.08f, 2.26f, 0.07f), mDarkMetal);
            Box(dr, "JambR", new Vector3(0.56f, 1.13f, 0.035f), new Vector3(0.08f, 2.26f, 0.07f), mDarkMetal);
            Box(dr, "Head", new Vector3(0f, 2.3f, 0.035f), new Vector3(1.2f, 0.08f, 0.07f), mDarkMetal);
            Box(dr, "Leaf", new Vector3(0f, 1.1f, 0.02f), new Vector3(1.04f, 2.18f, 0.04f), p.PaintedMetalGreen);
            Box(dr, "KickPlate", new Vector3(0f, 0.16f, 0.043f), new Vector3(1.0f, 0.28f, 0.006f), mSteel);
            Box(dr, "LeverRose", new Vector3(0.4f, 1.05f, 0.05f), new Vector3(0.07f, 0.16f, 0.02f), mSteel);
            Box(dr, "Lever", new Vector3(0.33f, 1.05f, 0.075f), new Vector3(0.14f, 0.025f, 0.025f), mSteel);
            Box(dr, "Closer", new Vector3(-0.2f, 2.16f, 0.07f), new Vector3(0.36f, 0.07f, 0.07f), mDarkMetal);
            Box(dr, "CloserArm", new Vector3(-0.42f, 2.16f, 0.12f), new Vector3(0.3f, 0.025f, 0.025f), mDarkMetal, false, new Vector3(0f, 25f, 0f));
            Box(dr, "Step", new Vector3(0f, 0.09f, 0.35f), new Vector3(1.4f, 0.08f, 0.55f), p.ConcreteExterior);
            WorldArchitectureKit.BuildIlluminatedSign(dr, "SecWest_DoorSign", new Vector3(0f, 1.62f, 0.035f), new Vector3(0f, 180f, 0f), new Vector2(0.62f, 0.16f), p.EmissiveWarmWhite, LightGroupId.VCExit, "SECURITY");
            WallDecal(t, "Drip_Door", p.DecalGrimeLeak, new Vector3(face, 2.95f, 31.9f), Vector3.left, new Vector2(0.6f, 0.9f), 0.7f);
            // meter box, plate and conduit to the wall pack
            Box(t, "MeterBox", new Vector3(face - 0.07f, 1.45f, 26.3f), new Vector3(0.14f, 0.6f, 0.42f), mSteel);
            Box(t, "MeterBoxDoorSeam", new Vector3(face - 0.145f, 1.45f, 26.3f), new Vector3(0.01f, 0.56f, 0.01f), mBlack);
            Transform hv = Group(t, "MeterPlate", new Vector3(face - 0.147f, 1.55f, 26.38f), -90f);
            PrintQuad(hv, p, "Plate", "high_voltage", Vector3.zero, new Vector2(0.22f, 0.16f), true);
            Box(t, "Conduit_Up", new Vector3(face - 0.04f, 2.48f, 26.3f), new Vector3(0.05f, 1.46f, 0.05f), mSteel);
            Box(t, "Conduit_Run", new Vector3(face - 0.04f, 3.2f, 27.375f), new Vector3(0.05f, 0.05f, 2.15f), mSteel);
            for (int c = 0; c < 3; c++)
                Box(t, $"ConduitStrap_{c}", new Vector3(face - 0.035f, 3.2f, 26.65f + c * 0.75f), new Vector3(0.07f, 0.08f, 0.03f), mSteel);
            Box(t, "Conduit_Down", new Vector3(face - 0.04f, 0.85f, 26.3f), new Vector3(0.05f, 0.6f, 0.05f), mSteel);
            // streaks under the sill and the wall pack, puddles in front of the window that catch its glow
            WallDecal(t, "Drip_Sill", p.DecalGrimeLeak, new Vector3(face, 0.6f, 28.5f), Vector3.left, new Vector2(1.8f, 1.0f), 0.6f);
            WallDecal(t, "Drip_WallPack", p.DecalGrimeLeak, new Vector3(face, 2.85f, 28.5f), Vector3.left, new Vector2(0.7f, 0.9f), 0.55f);
            // a concrete apron along the wall base (the puddles lie on it, the window glow and the wall packs reflect in it)
            // and two more warm wall packs, over the meter box and the condenser, so the wall reads as lit scallops
            Box(t, "Apron", new Vector3(face - 0.75f, 0.0f, 28f), new Vector3(1.5f, 0.1f, 12.4f), p.ConcreteCracked);
            WorldArchitectureKit.BuildHdrpLight(parent, "Light_SecWest_WallPack_1", new Vector3(-72.55f, 3.05f, 24.4f), new Vector3(62f, -90f, 0f), LightType.Spot, Color.white, 450f, 8f, LightGroupId.Plaza, volumetricDimmer: 0.6f, castShadows: false, spotOuterAngle: 100f, colorTempK: 2700f);
            WorldArchitectureKit.BuildHdrpLight(parent, "Light_SecWest_WallPack_2", new Vector3(-72.55f, 3.05f, 32.6f), new Vector3(62f, -90f, 0f), LightType.Spot, Color.white, 450f, 8f, LightGroupId.Plaza, volumetricDimmer: 0.6f, castShadows: false, spotOuterAngle: 100f, colorTempK: 2700f);
            DecalAt(t, "Puddle_Window", p.DecalPuddleWet, new Vector3(face - 0.9f, 0.05f, 28.5f), 2.4f, 20f, 1.0f);   // under the sill: catches the window's glow
            DecalAt(t, "Puddle_Window_2", p.DecalPuddleWet, new Vector3(face - 3.2f, 0.05f, 29.6f), 1.8f, 75f, 0.9f);
        }

        private static void BuildFlashlightDeskPickup(Transform parent, Vector3 pos, WorldMaterialPalette p)
        {
            GameObject flObj = p.InstantiatePropOrFallback("flashlight_pickup", parent, pos, new Vector3(0f, 28f, 0f), new Vector3(0.24f, 0.06f, 0.06f), grounded: true, addCollider: false, fallbackMat: p.BrushedSteel, customName: "Pickup_TacticalFlashlight");
            flObj.layer = GameLayers.Interactable;

            BoxCollider col = flObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.55f, 0.35f, 0.55f);

            // Subtle amber charging LED indicator on the flashlight body
            GameObject ledMesh = WorldArchitectureKit.CreateBox(flObj.transform, "ChargingLED", new Vector3(0f, 0.045f, 0f), new Vector3(0.025f, 0.015f, 0.025f), p.EmissiveAmber, addCollider: false);
            var ledLightGo = new GameObject("ChargingLedLight");
            ledLightGo.transform.SetParent(flObj.transform, false);
            ledLightGo.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            Light ledLight = ledLightGo.AddComponent<Light>();
            ledLight.type = LightType.Point;
            ledLight.color = new Color(1.0f, 0.52f, 0.16f);
            ledLight.intensity = 6f;
            ledLight.range = 1.0f;
            ledLightGo.AddComponent<HDAdditionalLightData>();

            FlashlightPickup pickup = flObj.AddComponent<FlashlightPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("interactId").stringValue = "pickup_flashlight";
            so.FindProperty("chargingLedLight").objectReferenceValue = ledLight;
            so.FindProperty("chargingLedRenderer").objectReferenceValue = ledMesh.GetComponent<MeshRenderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildSecurityCctvBank(Transform parent, WorldMaterialPalette p)
        {
            var bankGo = new GameObject("Security_CCTV_Bank");
            bankGo.transform.SetParent(parent, false);
            bankGo.transform.position = new Vector3(-65.0f, 1.85f, 33.805f);   // bezels flush on the wall lining (face z 33.847)

            // 6 CCTV feeds overlooking iconic park landmarks (Unity space)
            (string label, Vector3 camPos, Vector3 camLook)[] feedSpecs =
            {
                ("CAM 01 — TALON RIDGE", new Vector3(-36f, 5.5f, 36.5f), new Vector3(-36f, 1.5f, 52f)),
                ("CAM 02 — TYRANT VALLEY", new Vector3(95f, 9.0f, -6f), new Vector3(165f, 1.0f, 18f)),
                ("CAM 03 — MAIN PLAZA", new Vector3(-52f, 8.5f, -35f), new Vector3(-30f, 1.0f, -68f)),
                ("CAM 04 — LOBBY ATRIUM", new Vector3(-47f, 6.8f, 16f), new Vector3(-31f, 2.0f, -6f)),
                ("CAM 05 — LOADING DOCK", new Vector3(20f, 4.6f, -3f), new Vector3(11f, 1.2f, -12f)),
                ("CAM 06 — STAFF CORRIDOR", new Vector3(-69f, 2.9f, 20.5f), new Vector3(-30f, 1.5f, 20.5f))
            };

            CctvMonitorController ctrl = bankGo.AddComponent<CctvMonitorController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("controllerId").stringValue = "vc_security_cctv";
            SerializedProperty feedsProp = so.FindProperty("feeds");
            feedsProp.arraySize = feedSpecs.Length;

            // 3 columns x 2 rows wall-mounted monitors facing SOUTH (-Z) toward player spawn
            for (int i = 0; i < feedSpecs.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                Vector3 localPos = new Vector3((col - 1) * 0.92f, (row == 0 ? 0.34f : -0.26f), 0f);

                GameObject bezel = WorldArchitectureKit.CreateBox(bankGo.transform, $"Monitor_{i + 1}_Bezel", localPos, new Vector3(0.84f, 0.50f, 0.08f), p.DarkPaintedMetal, addCollider: false);
                GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
                screen.name = $"Monitor_{i + 1}_Screen";
                screen.transform.SetParent(bezel.transform, false);
                screen.transform.localPosition = new Vector3(0f, 0f, -0.56f);
                screen.transform.localScale = new Vector3(0.92f, 0.88f, 1f);
                MeshRenderer screenMr = screen.GetComponent<MeshRenderer>();
                screenMr.sharedMaterial = p.CctvScreenLive;
                Object.DestroyImmediate(screen.GetComponent<Collider>());

                var camGo = new GameObject($"CCTV_Cam_{i + 1}_{feedSpecs[i].label}");
                camGo.transform.SetParent(bankGo.transform, false);
                camGo.transform.position = feedSpecs[i].camPos;
                camGo.transform.LookAt(feedSpecs[i].camLook, Vector3.up);
                Camera cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 56f;
                cam.nearClipPlane = 0.2f;
                cam.farClipPlane = 260f;
                cam.cullingMask = ~(GameLayers.Mask(GameLayers.Viewmodel) | GameLayers.Mask(GameLayers.RainBlocker));
                cam.enabled = false;

                SerializedProperty elem = feedsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("feedLabel").stringValue = feedSpecs[i].label;
                elem.FindPropertyRelative("feedCamera").objectReferenceValue = cam;
                elem.FindPropertyRelative("monitorScreenRenderer").objectReferenceValue = screenMr;
                elem.FindPropertyRelative("screenMaterialIndex").intValue = 0;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DressLockerRoom(Transform parent, WorldMaterialPalette p)
        {
            var lr = new GameObject("Props_LockerRoom");
            lr.transform.SetParent(parent, false);
            // A continuous bank of lockers on the north wall, a second bank on the west partition, a slatted bench between
            for (int i = 0; i < 6; i++)
                p.InstantiatePropOrFallback("lockers", lr.transform, new Vector3(-57.2f + i * 1.36f, 0f, 33.59f), new Vector3(0f, 180f, 0f), new Vector3(2.0f, 2.05f, 0.58f));
            for (int i = 0; i < 3; i++)
                p.InstantiatePropOrFallback("lockers", lr.transform, new Vector3(-57.64f, 0f, 26.2f + i * 1.36f), new Vector3(0f, 90f, 0f), new Vector3(2.0f, 2.05f, 0.58f));
            Bench(lr.transform, "LockerBench", new Vector3(-53.0f, 0f, 30.6f), 0f, 3.4f);
            Bench(lr.transform, "LockerBench_W", new Vector3(-55.9f, 0f, 27.6f), 90f, 2.4f);
            for (int h = 0; h < 6; h++)
            {
                Box(lr.transform, $"CoatHook_{h}", new Vector3(-48.12f, 1.7f, 25.0f + h * 0.45f), new Vector3(0.06f, 0.03f, 0.03f), mSteel);
            }
            Box(lr.transform, "CoatRail", new Vector3(-48.11f, 1.72f, 26.1f), new Vector3(0.02f, 0.10f, 2.8f), mVeneer);
            Poster(lr.transform, p, "Poster_LockerRaptor", "raptor_poster", new Vector3(-50.6f, 1.6f, 22.11f), Vector3.forward, new Vector2(0.62f, 0.88f));
            p.InstantiatePropOrFallback("trash_bin", lr.transform, new Vector3(-48.6f, 0f, 22.6f), Vector3.zero, new Vector3(0.36f, 0.6f, 0.36f));
            DecalAt(lr.transform, "Lockers_FloorGrime", p.DecalGrimeLeak, new Vector3(-53.0f, 0.02f, 32.6f), 3.0f);
            CeilingLamp(lr.transform, "Light_Lockers_Main", new Vector3(-53f, CeilStd - 0.15f, 25f), LightGroupId.VCMain, 4300f);
            CeilingLamp(lr.transform, "Light_Lockers_Main_2", new Vector3(-53f, CeilStd - 0.15f, 31f), LightGroupId.VCMain, 4300f, shadows: false);
            Washes(lr.transform, "Lockers", -57.5f, -48.5f, 22.5f, 33.5f, LightGroupId.VCMain, walls: "NESW");
            BatteryLight(lr.transform, "Lockers", -1, new Vector3(-51.6f, CeilStd - 0.15f, 22.9f));
        }

        private static void DressControlRoom(Transform parent, WorldMaterialPalette p)
        {
            var cr = new GameObject("Props_ControlRoom");
            cr.transform.SetParent(parent, false);

            // 2x4 Tiered Control Consoles (P1-8 FIX: rotated 180 deg so screens face SOUTH toward operator chairs & player!)
            for (int row = 0; row < 2; row++)
            {
                float rz = row == 0 ? 31.3f : 28.0f;
                for (int c = 0; c < 4; c++)
                {
                    float rx = -43.5f + c * 4.3f;
                    p.InstantiatePropOrFallback("control_console", cr.transform, new Vector3(rx, 0f, rz), new Vector3(0f, 180f, 0f), new Vector3(2.4f, 1.15f, 0.95f));
                    ConsoleScreens(cr.transform, $"ConsoleScreens_{row}_{c}", new Vector3(rx, 0f, rz), row * 4 + c);
                    p.InstantiatePropOrFallback("office_chair", cr.transform, new Vector3(rx, 0f, rz - 1.15f), new Vector3(0f, Random.Range(-25f, 25f), 0f), new Vector3(0.65f, 1.05f, 0.65f));
                }
            }

            // UX-5 FIX: Interactive Primary Paddock Telemetry Console + fallback area trigger!
            WorldArchitectureKit.BuildWorldAnchor(cr.transform, "control_console", new Vector3(-37.0f, 0.95f, 31.2f));
            BuildSimpleInteractableBox(cr.transform, "Interact_ControlConsole", new Vector3(-37.0f, 1.05f, 31.2f), new Vector3(1.8f, 1.0f, 1.0f),
                "control_console", "Log Talon Ridge & Tyrant Valley Feeds {interact}", holdSec: 0f, singleUse: true, startsEnabled: false);
            WorldArchitectureKit.BuildStoryTrigger(cr.transform, "control_switchover", new Vector3(-37.0f, 1.5f, 29.8f), new Vector3(7.0f, 3.0f, 3.8f), oneShot: true);

            // POS-10 FIX: East Status Wall + map_park_modern bound STRICTLY to LightGroupId.VCMain so it goes dark on blackout!
            var statusRoot = new GameObject("ControlRoom_StatusWall");
            statusRoot.transform.SetParent(cr.transform, false);
            statusRoot.transform.position = new Vector3(-26.1f, 1.85f, 28.0f);   // partition face (x -26.1)
            // Bezel on the partition face (Box = local position, so it hangs off the control room root at the origin), the map on its face.
            Box(cr.transform, "StatusWall_Bezel", new Vector3(-26.125f, 1.85f, 28f), new Vector3(0.05f, 2.3f, 4.6f), mBlack);
            // Fail-safe: a printed (non-emissive) copy 3 mm behind the glowing picture; the glowing one is switched off with VCMain.
            Poster(cr.transform, p, "MapParkModern_Print", "map_park_modern", new Vector3(-26.147f, 1.85f, 28.0f), Vector3.left, new Vector2(4.4f, 2.1f), frame: false);
            GameObject mapPic = Poster(statusRoot.transform, p, "MapParkModern_Screen", "map_park_modern", new Vector3(-26.15f, 1.85f, 28.0f), Vector3.left, new Vector2(4.4f, 2.1f), frame: false, emissive: true, emissiveIntensity: 8f);
            Transform mapGlow = mapPic.transform.Find("Picture");
            LightGroupMember statusMember = WorldArchitectureKit.AttachLightGroupMember(statusRoot, LightGroupId.VCMain, null, new Renderer[] { mapGlow.GetComponent<MeshRenderer>() });
            BindLightGroupToggleObject(statusMember, mapGlow.gameObject);   // SetActive(false) in the blackout, whatever the probes do
            WorldArchitectureKit.BuildWorldAnchor(cr.transform, "status_wall", statusRoot.transform.position);
            // Supervisor desk by the west partition, wall phone, binders, posters, bins
            Transform sup = Desk(cr.transform, "SupervisorDesk", new Vector3(-46.9f, 0f, 26.5f), -90f, 1.8f, 0.85f, mVeneer, mCabinet);
            p.InstantiatePropOrFallback("lcd_monitor", sup, new Vector3(-47.25f, 0.76f, 26.4f), new Vector3(0f, 90f, 0f), new Vector3(0.58f, 0.44f, 0.20f), grounded: true, addCollider: false);
            PaperStack(sup, "Logs", new Vector3(-46.8f, 0.76f, 27.0f), 80f, 4);
            BinderRow(sup, "SupBinders", new Vector3(-47.25f, 0.76f, 25.75f), 90f, 7, 21);
            Chair(cr.transform, "SupervisorChair", new Vector3(-46.1f, 0f, 26.6f), 100f);
            Poster(cr.transform, p, "Poster_TalonWarning", "talon_ridge_warning", new Vector3(-47.9f, 1.7f, 31.2f), Vector3.right, new Vector2(1.0f, 0.625f));
            Poster(cr.transform, p, "Poster_ControlFire", "fire_assembly", new Vector3(-27.4f, 1.6f, 22.11f), Vector3.forward, new Vector2(0.55f, 0.55f));
            p.InstantiatePropOrFallback("filing_cabinet", cr.transform, new Vector3(-47.6f, 0f, 33.4f), new Vector3(0f, 180f, 0f), new Vector3(0.55f, 1.38f, 0.65f));
            p.InstantiatePropOrFallback("trash_bin", cr.transform, new Vector3(-27.0f, 0f, 23.0f), Vector3.zero, new Vector3(0.36f, 0.6f, 0.36f));
            WallExtinguisher(cr.transform, p, new Vector3(-41.0f, 0f, 22.11f), Vector3.forward);   // outside the 1.6 m leaf swing
            for (int c = 0; c < 4; c++)
            {
                // cable trunking from each console row to the floor box
                Box(cr.transform, $"FloorCableCover_{c}", new Vector3(-43.5f + c * 4.3f, 0.012f, 29.65f), new Vector3(0.18f, 0.024f, 2.2f), mBlack);
            }

            // Main Control Room lighting (VCMain — P1-10 boosted for daytime readability under EV100=11.5!) + Emergency red beacons (VCEmergency)
            // Troffer grid 3 x 2 (two shadowed), two ceiling washes
            CeilingLamp(cr.transform, "Light_Control_MainL", new Vector3(-44f, CeilStd - 0.15f, 25f), LightGroupId.VCMain, 4500f);
            CeilingLamp(cr.transform, "Light_Control_MainR", new Vector3(-30f, CeilStd - 0.15f, 31f), LightGroupId.VCMain, 4500f);
            Vector2[] ctlFx = { new(-37f, 25f), new(-30f, 25f), new(-44f, 31f), new(-37f, 31f) };
            for (int i = 0; i < ctlFx.Length; i++)
                CeilingLamp(cr.transform, $"Light_Control_Main_{i + 2}", new Vector3(ctlFx[i].x, CeilStd - 0.15f, ctlFx[i].y), LightGroupId.VCMain, 4500f, shadows: false);
            Washes(cr.transform, "Control", -47.5f, -26.5f, 22.5f, 33.5f, LightGroupId.VCMain, walls: "NNNSS");
            BatteryLight(cr.transform, "Control", -1, new Vector3(-39.4f, CeilStd - 0.15f, 22.9f));   // one unit, by the exit (round 3)
        }

        /// <summary>
        /// Glowing faces on the three monitors of a control_console (review 1: six black screens in a powered control room
        /// while the radio asks the player to log the camera feeds). Positions measured on the FBX by raycasting its mesh:
        /// three 0.64 m screens 0.95-1.37 m up, 0.15 m behind the console centre, the outer two turned 3.6 deg inwards.
        /// Switched off with VCMain (renderers + toggle object), so they die with the troffers.
        /// </summary>
        private static void ConsoleScreens(Transform parent, string name, Vector3 consoleFloor, int seed)
        {
            Transform g = Group(parent, name, consoleFloor, 180f);   // same frame as the console (local +Z = towards the operator)
            var screens = new GameObject("Screens");
            screens.transform.SetParent(g, false);
            screens.layer = GameLayers.Environment;
            var rends = new List<Renderer>();
            (float x, float z, float yaw)[] mons = { (-0.80f, -0.120f, 183.6f), (0.0f, -0.146f, 180f), (0.80f, -0.120f, 176.4f) };
            for (int i = 0; i < mons.Length; i++)
            {
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = $"Screen_{i}";
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(screens.transform, false);
                q.transform.localPosition = new Vector3(mons[i].x, 1.155f, mons[i].z);
                q.transform.localRotation = Quaternion.Euler(0f, mons[i].yaw, 0f);
                q.transform.localScale = new Vector3(0.56f, 0.33f, 1f);
                q.layer = GameLayers.Environment;
                MeshRenderer mr = q.GetComponent<MeshRenderer>();
                bool feed = (i + seed) % 3 != 1;
                mr.sharedMaterial = feed ? CctvFeedMaterial() : mScreenFeed;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rends.Add(mr);
                if (feed)
                {
                    // round 4 (review 3, 03_ch1: "a flat khaki terrain texture with no glow"): a lit night camera feed with its
                    // camera ID and time stamp, as the radio's "log the camera feeds" needs (the overlay dies with the screen)
                    string[] cams = { "CAM 04  TALON RIDGE", "CAM 07  TYRANT VALLEY", "CAM 11  PADDOCK 4 GATE", "CAM 02  SERVICE ROAD", "CAM 09  SUBSTATION 3", "CAM 13  EAST DOCK" };
                    Quaternion qr = q.transform.localRotation;
                    Vector3 front = q.transform.localPosition + qr * new Vector3(0f, 0f, -0.002f);
                    Ink(screens.transform, $"Screen_{i}_Id", cams[(i + seed * 2) % cams.Length], front + qr * new Vector3(-0.08f, 0.13f, 0f), qr, new Vector2(0.36f, 0.04f), InkWhite, "CourierPrime-Bold", 0.25f, TMPro.TextAlignmentOptions.Left);
                    Ink(screens.transform, $"Screen_{i}_Time", $"20-11-96  23:{(4 + i) % 60:00}:{(17 + seed * 13 + i * 7) % 60:00}", front + qr * new Vector3(0.12f, -0.135f, 0f), qr, new Vector2(0.28f, 0.035f), InkWhite, "CourierPrime-Regular", 0.22f, TMPro.TextAlignmentOptions.Right);
                }
            }
            LightGroupMember m = WorldArchitectureKit.AttachLightGroupMember(g.gameObject, LightGroupId.VCMain, null, rends.ToArray());
            BindLightGroupToggleObject(m, screens);
        }

        private static void DressServerRoom(Transform parent, WorldMaterialPalette p)
        {
            var sr = new GameObject("Props_ServerRoom");
            sr.transform.SetParent(parent, false);

            // Visual Audit FIX: Rotate all 4 server racks 180 deg facing SOUTH toward doorway (not backwards!)
            for (int i = 0; i < 4; i++)
            {
                Vector3 rPos = new Vector3(-24.4f + i * 1.45f, 0f, 31.2f);
                p.InstantiatePropOrFallback("server_rack", sr.transform, rPos, new Vector3(0f, 180f, 0f), new Vector3(0.82f, 2.25f, 1.0f));
                // Front status LED strip bound to VCMain
                GameObject led = WorldArchitectureKit.CreateBox(sr.transform, $"RackLed_{i}", rPos + new Vector3(0f, 1.65f, -0.52f), new Vector3(0.42f, 0.04f, 0.02f), p.EmissiveCyan, addCollider: false);
                WorldArchitectureKit.AttachLightGroupMember(led, LightGroupId.VCMain, null, new Renderer[] { led.GetComponent<MeshRenderer>() });
            }

            Box(sr.transform, "UPS_Cabinet", new Vector3(-19.0f, 0.9f, 33.4f), new Vector3(1.4f, 1.8f, 0.8f), mBlack, true);
            Box(sr.transform, "UPS_Panel", new Vector3(-19.0f, 1.45f, 32.995f), new Vector3(0.5f, 0.3f, 0.01f), mCabinet);
            GameObject upsLed = Box(sr.transform, "UPS_Led", new Vector3(-18.75f, 1.45f, 32.988f), new Vector3(0.05f, 0.05f, 0.01f), p.EmissiveGreen);
            WorldArchitectureKit.AttachLightGroupMember(upsLed, LightGroupId.VCExit, null, new Renderer[] { upsLed.GetComponent<MeshRenderer>() });
            Box(sr.transform, "CableLadder", new Vector3(-22.2f, 2.95f, 31.2f), new Vector3(6.0f, 0.06f, 0.4f), mSteel);
            for (int h = 0; h < 4; h++) Box(sr.transform, $"CableLadderHanger_{h}", new Vector3(-24.6f + h * 1.6f, 3.2f, 31.2f), new Vector3(0.02f, 0.5f, 0.02f), mSteel);
            Box(sr.transform, "CableBundle", new Vector3(-22.2f, 3.0f, 31.2f), new Vector3(6.0f, 0.06f, 0.26f), mBinderBlue);
            Transform sdesk = Desk(sr.transform, "ServerDesk", new Vector3(-23.6f, 0f, 22.6f), 180f, 1.4f, 0.7f, mLaminate, mCabinet);
            p.InstantiatePropOrFallback("crt_monitor", sdesk, new Vector3(-23.6f, 0.76f, 22.62f), new Vector3(0f, 0f, 0f), new Vector3(0.44f, 0.42f, 0.42f), grounded: true, addCollider: false);
            DecalAt(sr.transform, "Server_FloorGrime", p.DecalGrimeLeak, new Vector3(-22.2f, 0.02f, 29.6f), 2.6f, 30f);
            WorldArchitectureKit.BuildConduitRun(sr.transform, "Server_CableTray", new[] { new Vector3(-25.2f, 2.95f, 31.2f), new Vector3(-19.2f, 2.95f, 31.2f) }, 0.06f, p.BrushedSteel);
            WorldArchitectureKit.BuildHdrpLight(sr.transform, "Light_Server_Blue", new Vector3(-22f, 2.5f, 30.1f), Vector3.zero, LightType.Point, new Color(0.28f, 0.65f, 1.0f), 750f, 9f, LightGroupId.VCMain, volumetricDimmer: 0.9f);
            CeilingLamp(sr.transform, "Light_Server_Main", new Vector3(-22f, CeilStd - 0.15f, 25.5f), LightGroupId.VCMain, 4300f);
            Washes(sr.transform, "Server", -25.5f, -18.5f, 22.5f, 33.5f, LightGroupId.VCMain, walls: "NS");
            BatteryLight(sr.transform, "Server", -1, new Vector3(-20.6f, CeilStd - 0.15f, 22.9f));
        }

        private static void DressRecordsAnnex(Transform parent, WorldMaterialPalette p)
        {
            var ra = new GameObject("Props_RecordsAnnex");
            ra.transform.SetParent(parent, false);

            WorldArchitectureKit.BuildWorldAnchor(ra.transform, "spawn_records", new Vector3(-6.0f, 0.05f, 24.5f), yawDeg: 140f);
            WorldArchitectureKit.BuildWorldAnchor(ra.transform, "manual", new Vector3(-3.6f, 0.82f, 27.3f));

            // 3x3 Archive Shelves
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    p.InstantiatePropOrFallback("archive_shelf", ra.transform, new Vector3(-15.5f + col * 3.2f + ((row * 3 + col) % 3 - 1) * 0.06f, 0f, 26.8f + row * 2.6f), new Vector3(0f, ((row + col) % 2 == 0 ? 0f : 180f) + ((row * 3 + col) % 3 - 1) * 1.2f, 0f), new Vector3(2.2f, 2.3f, 0.55f));
                }
            }

            // 1990s Archivist Desk + CRT + Document 2/5 (1996 Evacuation Manual Binder, 2 pages!)
            Transform rdesk = Desk(ra.transform, "RecordsDesk", new Vector3(-3.5f, 0f, 27.5f), 0f, 1.8f, 0.95f, mVeneer, mVeneer, 0.82f);
            p.InstantiatePropOrFallback("crt_monitor", ra.transform, new Vector3(-2.9f, 0.82f, 27.7f), new Vector3(0f, 165f, 0f), new Vector3(0.46f, 0.44f, 0.44f));
            Box(rdesk, "Blotter", new Vector3(-0.2f, 0.823f, -0.12f), new Vector3(0.6f, 0.004f, 0.42f), mBinderBlack);
            Prim(rdesk, "Mug", PrimitiveType.Cylinder, new Vector3(-0.78f, 0.868f, 0.25f), new Vector3(0.082f, 0.048f, 0.082f), mBinderRed);
            Box(rdesk, "MugHandle", new Vector3(-0.73f, 0.868f, 0.25f), new Vector3(0.030f, 0.055f, 0.012f), mBinderRed);
            PaperStack(rdesk, "LooseFiles", new Vector3(-3.2f, 0.82f, 27.25f), -12f, 4);
            Chair(ra.transform, "RecordsChair", new Vector3(-3.3f, 0f, 26.7f), 160f);
            // archive boxes left on the floor between the rows, a step ladder, a wall poster
            Material[] boxMats = { mCork, mPaper, mCork };
            for (int b = 0; b < 5; b++)
                Box(ra.transform, $"ArchiveBox_{b}", new Vector3(-12.9f + (b % 2) * 0.12f, 0.15f + (b / 2) * 0.3f * 0f, 25.2f + b * 0.46f), new Vector3(0.42f, 0.3f, 0.34f), boxMats[b % 3], true, new Vector3(0f, b * 9f - 12f, 0f));
            Box(ra.transform, "ArchiveBox_Top", new Vector3(-12.85f, 0.45f, 25.65f), new Vector3(0.42f, 0.3f, 0.34f), mCork, false, new Vector3(0f, 6f, 0f));
            Poster(ra.transform, p, "Poster_RecordsSign", "records", new Vector3(-0.1f, 2.1f, 25.0f), Vector3.left, new Vector2(1.2f, 0.35f), frame: false);
            DecalAt(ra.transform, "Records_FloorGrime", p.DecalGrimeLeak, new Vector3(-10.5f, 0.02f, 30.8f), 3.2f, 70f);
            WallDecal(ra.transform, "Records_WallLeak", p.DecalGrimeLeak, new Vector3(-17.9f, 2.4f, 30.5f), Vector3.right, new Vector2(1.6f, 2.0f));
            p.InstantiatePropOrFallback("filing_cabinet", ra.transform, new Vector3(-1.0f, 0f, 32.8f), new Vector3(0f, 180f, 0f), new Vector3(0.55f, 1.38f, 0.65f));

            BuildDocumentProp(ra.transform, new Vector3(-3.6f, 0.822f, 27.3f), "manual", "1996 Preserve Evacuation Manual",
                "Read 1996 Evacuation Manual {interact}",
                new[] { "manual_p1", "manual_p2" },
                new[]
                {
                    "EVACUATION PROCEDURES (1996) — 7.2 Service Exit E-0 at the Old Maintenance Yard. 7.4 E-0 requires AUXILIARY CIRCUIT B from SUBSTATION 3.",
                    "7.5 Restoring Circuit B: Reset 3 tripped breakers, prime the diesel fuel pump, start the generator, throw the TRANSFER lever. [Master Key attached]"
                }, p, assetStem: "binder_manual", oneShot: true);

            // Focused warm-red emergency work lamp illuminating the 1996 Manual binder in the blackout!
            WorldArchitectureKit.BuildHdrpLight(ra.transform, "Light_Records_ManualBeacon", new Vector3(-3.6f, 1.95f, 27.3f), new Vector3(90f, 0f, 0f), LightType.Spot, new Color(1.0f, 0.30f, 0.16f), 320f, 4.5f, LightGroupId.VCExit, volumetricDimmer: 1.1f, spotOuterAngle: 52f);
            Vector2[] recFx = { new(-13.5f, 25f), new(-4.5f, 31f), new(-4.5f, 25f), new(-13.5f, 31f) };
            for (int i = 0; i < recFx.Length; i++)
                CeilingLamp(ra.transform, i == 0 ? "Light_Records_Main" : $"Light_Records_Main_{i + 1}", new Vector3(recFx[i].x, CeilStd - 0.15f, recFx[i].y), LightGroupId.VCMain, 3200f, shadows: i == 0);
            Washes(ra.transform, "Records", -17.5f, -0.5f, 22.5f, 33.5f, LightGroupId.VCMain, walls: "NNEES");
            BatteryLight(ra.transform, "Records", -1, new Vector3(-10.6f, CeilStd - 0.15f, 22.9f));
        }

        private static void DressStorageCloset(Transform parent, WorldMaterialPalette p)
        {
            // DOOR-8 FIX: Fully dressed storage/utility interior behind locked door_storage (never skybox void!)
            var st = new GameObject("Props_StorageCloset");
            st.transform.SetParent(parent, false);
            p.InstantiatePropOrFallback("archive_shelf", st.transform, new Vector3(1.4f, 0f, 32.6f), Vector3.zero, new Vector3(2.2f, 2.2f, 0.55f));
            // P1-9 FIX: Seat upper crate dynamically onto the lower crate's real bounds.max.y (zero floating air gap!)
            GameObject lowerCrate = p.InstantiatePropOrFallback("crate", st.transform, new Vector3(5.8f, 0f, 31.8f), new Vector3(0f, 18f, 0f), new Vector3(0.9f, 0.9f, 0.9f));
            float upperCrateBaseY = GetCombinedBoundsMaxY(lowerCrate, 0.88f) - 0.01f;
            p.InstantiatePropOrFallback("crate", st.transform, new Vector3(5.9f, upperCrateBaseY, 31.7f), new Vector3(0f, -12f, 0f), new Vector3(0.75f, 0.75f, 0.75f), grounded: true);
            p.InstantiatePropOrFallback("jerry_can", st.transform, new Vector3(2.4f, 0f, 26.5f), new Vector3(0f, 40f, 0f), new Vector3(0.36f, 0.48f, 0.22f));
            p.InstantiatePropOrFallback("oil_drum", st.transform, new Vector3(6.4f, 0f, 26.0f), Vector3.zero, new Vector3(0.62f, 0.92f, 0.62f));
            WorldArchitectureKit.BuildHdrpLight(st.transform, "Light_Storage_Bulb", new Vector3(4f, 3.1f, 28f), Vector3.zero, LightType.Point, new Color(1.0f, 0.74f, 0.42f), 260f, 7.5f, LightGroupId.VCExit);
        }

        // =========================================================================
        // STAFF CORRIDOR (X: -72 .. +8, Z: +19 .. +22, Ceil: 3.25)
        // COL-3 FIX: Skirting & dado rails strictly segmented BETWEEN all doorways!
        // =========================================================================

        private static void BuildStaffCorridorAndDoors(Transform parent, WorldMaterialPalette p)
        {
            var corr = new GameObject("StaffCorridor_80m");
            corr.transform.SetParent(parent, false);

            GameObject corrShell = WorldArchitectureKit.BuildBoxRoomShell(corr.transform, "Corridor_Shell", new Vector3(-32f, 0f, 20.5f), 0f, 80f, 3.0f, CeilCorr, p.ConcreteFloor, mCeilTile, SurfaceType.Concrete, p);
            TuckCeiling(corrShell);   // full-size ceiling tucked 1 cm under the wall tops: no slot along the south wall
            Liner(corr.transform, "Lining_Corr_WestEnd", new Vector3(-72f, 0f, 19f), new Vector3(-72f, 0f, 22f), Vector3.right, WallT * 0.5f, CeilCorr, p.PlasterClean, wainscot: mWainscot);
            Liner(corr.transform, "Lining_Corr_EastEnd", new Vector3(8f, 0f, 19f), new Vector3(8f, 0f, 22f), Vector3.left, WallT * 0.5f, CeilCorr, p.PlasterClean, wainscot: mWainscot);
            // Blue-grey wainscot band under the steel dado rail on both long walls (doors left open)
            Liner(corr.transform, "Wainscot_Corr_N", new Vector3(-72f, 0f, 22f), new Vector3(8f, 0f, 22f), Vector3.back, 0.11f, CeilCorr, null,
                new[] { (9.0f, 10.0f, 0f, 2.2f), (18.5f, 19.5f, 0f, 2.2f), (34.2f, 35.8f, 0f, 2.2f), (49.5f, 50.5f, 0f, 2.2f), (62.5f, 63.5f, 0f, 2.2f), (75.5f, 76.5f, 0f, 2.2f) }, skirting: false, wainscot: mWainscot);
            Liner(corr.transform, "Wainscot_Corr_S", new Vector3(-72f, 0f, 19f), new Vector3(8f, 0f, 19f), Vector3.forward, 0.11f, CeilCorr, null,
                new[] { (10.5f, 11.5f, 0f, 2.2f), (39.8f, 42.2f, 0f, 2.4f), (69.4f, 70.6f, 0f, 2.2f) }, skirting: false, wainscot: mWainscot);
            WorldArchitectureKit.BuildWallWithOpenings(corr.transform, new Vector3(-72f, 0f, 19f), new Vector3(-72f, 0f, 22f), 0f, CeilCorr, WallT, p.ConcreteBoardform, null, addSkirting: true, palette: p, wallName: "Wall_Corr_WestEnd");
            WorldArchitectureKit.BuildWallWithOpenings(corr.transform, new Vector3(8f, 0f, 19f), new Vector3(8f, 0f, 22f), 0f, CeilCorr, WallT, p.ConcreteBoardform, null, addSkirting: true, palette: p, wallName: "Wall_Corr_EastEnd");

            // Corridor North Wall (Z = +22, X = -72 -> +8, len = 80) — 6 cut doorways, ZERO dado across doors!
            WorldArchitectureKit.BuildWallWithOpenings(
                corr.transform,
                new Vector3(-72f, 0f, 22f),
                new Vector3(8f, 0f, 22f),
                0f, CeilCorr, 0.22f, mOfficePaint,
                new[]
                {
                    OpeningSpec.Door(9.5f, 1.0f, 2.2f),   // door_security (X = -62.5)
                    OpeningSpec.Door(19.0f, 1.0f, 2.2f),  // door_lockers  (X = -53.0)
                    OpeningSpec.Door(35.0f, 1.6f, 2.2f),  // door_control  (X = -37.0)
                    OpeningSpec.Door(50.0f, 1.0f, 2.2f),  // door_server   (X = -22.0)
                    OpeningSpec.Door(63.0f, 1.0f, 2.2f),  // door_records  (X = -9.0)
                    OpeningSpec.Door(76.0f, 1.0f, 2.2f)   // door_storage  (X = +4.0)
                },
                addSkirting: true, addDadoRail: true, palette: p, wallName: "Wall_Corridor_North");

            // Corridor South Wall (Z = +19, X = -72 -> +8, len = 80) — Break Room + Lobby Arch + Feed Prep door_wing!
            // COL-1 / DOOR-1 FIX: Single solid double-sided wall with true opening at X = -2.0 (ZERO veneer behind door_wing!)
            WorldArchitectureKit.BuildWallWithOpenings(
                corr.transform,
                new Vector3(-72f, 0f, 19f),
                new Vector3(8f, 0f, 19f),
                0f, CeilCorr, 0.22f, mOfficePaint,
                new[]
                {
                    OpeningSpec.Door(11.0f, 1.0f, 2.2f),  // door_break (X = -61.0)
                    OpeningSpec.Arch(41.0f, 2.4f, 2.4f),  // arch_lobby (X = -31.0)
                    OpeningSpec.Door(70.0f, 1.2f, 2.2f)   // door_wing  (X = -2.0)
                },
                addSkirting: true, addDadoRail: true, palette: p, wallName: "Wall_Corridor_South");

            // Mount all 8 framed corridor doors cleanly inside their exact openings
            WorldArchitectureKit.BuildFramedDoor(corr.transform, "door_security", new Vector3(-62.5f, 0f, 22f), 0f, 1.0f, 2.2f, DoorMechanism.SingleHinged, p, startsOpen: true, soundPrefix: "door_interior", overheadSignText: "SECURITY");
            WorldArchitectureKit.BuildFramedDoor(corr.transform, "door_lockers", new Vector3(-53.0f, 0f, 22f), 0f, 1.0f, 2.2f, DoorMechanism.SingleHinged, p, soundPrefix: "door_interior");
            WorldArchitectureKit.BuildFramedDoor(corr.transform, "door_control", new Vector3(-37.0f, 0f, 22f), 0f, 1.6f, 2.2f, DoorMechanism.SingleHinged, p, soundPrefix: "door_interior", overheadSignText: "CONTROL ROOM", addKeycardLed: true);
            WorldArchitectureKit.BuildFramedDoor(corr.transform, "door_server", new Vector3(-22.0f, 0f, 22f), 0f, 1.0f, 2.2f, DoorMechanism.SingleHinged, p, soundPrefix: "door_interior");
            WorldArchitectureKit.BuildFramedDoor(corr.transform, "door_records", new Vector3(-9.0f, 0f, 22f), 0f, 1.0f, 2.2f, DoorMechanism.SingleHinged, p, soundPrefix: "door_old", overheadSignText: "RECORDS ANNEX", leafMaterial: p.WoodPlanks);
            WorldArchitectureKit.BuildFramedDoor(corr.transform, "door_storage", new Vector3(4.0f, 0f, 22f), 0f, 1.0f, 2.2f, DoorMechanism.SingleHinged, p, startsLocked: true, lockedHint: "Utility storage — deadbolted shut.", soundPrefix: "door_interior", bashableByRaptor: false);
            WorldArchitectureKit.BuildFramedDoor(corr.transform, "door_break", new Vector3(-61.0f, 0f, 19f), 180f, 1.0f, 2.2f, DoorMechanism.SingleHinged, p, soundPrefix: "door_interior");
            WorldArchitectureKit.BuildFramedDoor(corr.transform, "door_wing", new Vector3(-2.0f, 0f, 19f), 180f, 1.2f, 2.2f, DoorMechanism.SingleHinged, p, startsLocked: true, lockedHint: "Feed Prep — maglock failed shut. Needs the master key.", soundPrefix: "door_interior", overheadSignText: "FEED PREP", addKeycardLed: true);

            // P1-2 FIX: Industrial brushed/dark steel overhead power conduits (replaces garish 80 m full-bright neon red tube!)
            // Conduit tight to the north wall, cable tray beside it on hangers: both clear of the light fixtures (z 20.5)
            WorldArchitectureKit.BuildConduitRun(corr.transform, "Corridor_PowerConduit_Red", new[] { new Vector3(-71.6f, 3.08f, 21.82f), new Vector3(7.6f, 3.08f, 21.82f) }, 0.04f, p.DarkPaintedMetal);
            var tray = new GameObject("Corridor_CableTray");
            tray.transform.SetParent(corr.transform, false);
            Box(tray.transform, "TrayBase", new Vector3(-32f, 2.95f, 21.42f), new Vector3(79.2f, 0.012f, 0.30f), mDarkMetal);
            Box(tray.transform, "TrayLipN", new Vector3(-32f, 2.985f, 21.57f), new Vector3(79.2f, 0.07f, 0.012f), mDarkMetal);
            Box(tray.transform, "TrayLipS", new Vector3(-32f, 2.985f, 21.27f), new Vector3(79.2f, 0.07f, 0.012f), mDarkMetal);
            Box(tray.transform, "TrayCables", new Vector3(-32f, 2.975f, 21.43f), new Vector3(79.0f, 0.04f, 0.2f), mBlack);
            for (int h = 0; h < 27; h++)
            {
                float hx = -71.0f + h * 3.0f;
                Box(tray.transform, $"Hanger_{h}", new Vector3(hx, 3.1f, 21.42f), new Vector3(0.02f, 0.30f, 0.02f), mSteel);
                Box(tray.transform, $"HangerBar_{h}", new Vector3(hx, 2.94f, 21.42f), new Vector3(0.03f, 0.02f, 0.34f), mSteel);
            }

            // 10 recessed ceiling lights (VCMain) — P1-2 FIX: dark housing + toggleObject glowing diffuser so white fluorescent strips go 100% DARK in Blackout!
            for (int i = 0; i < 10; i++)
            {
                float lx = -68f + i * 8.0f;
                WorldArchitectureKit.CreateBox(corr.transform, $"CorrLightHousing_{i}", new Vector3(lx, CeilCorr - 0.025f, 20.5f), new Vector3(1.18f, 0.04f, 0.38f), mEnamel, addCollider: false);
                // a pale opal plate just behind the glowing diffuser: it stays when the diffuser is switched off in the
                // blackout, so a dead fixture reads as a pale panel in the beam (review 1: flat dark squares)
                WorldArchitectureKit.CreateBox(corr.transform, $"CorrLightOpal_{i}", new Vector3(lx, CeilCorr - 0.047f, 20.5f), new Vector3(1.07f, 0.008f, 0.29f), mOpal, addCollider: false);
                // diffuser at ~70 nits: the palette's 170-nit cool white bloomed into a round white orb down the corridor
                GameObject fixture = WorldArchitectureKit.CreateBox(corr.transform, $"CorrLightFixture_{i}", new Vector3(lx, CeilCorr - 0.055f, 20.5f), new Vector3(1.06f, 0.025f, 0.28f), mTubeGlow, addCollider: false);
                Light corrLight = WorldArchitectureKit.BuildHdrpLight(corr.transform, $"Light_Corr_Main_{i}", new Vector3(lx, CeilCorr - 0.16f, 20.5f), Vector3.zero, LightType.Point, new Color(0.92f, 0.96f, 1.0f), 620f, 9.5f, LightGroupId.VCMain, pairedEmissives: new Renderer[] { fixture.GetComponent<MeshRenderer>() });
                BindLightGroupToggleObject(corrLight != null ? corrLight.GetComponent<LightGroupMember>() : null, fixture);
            }
            for (int i = 0; i < 4; i++)
                CeilingWash(corr.transform, $"Light_Corr_Wash_{i}", new Vector3(-62f + i * 20f, 0f, 20.5f), LightGroupId.VCMain, 4300f);   // 4 over 80 m
            CeilingFill(corr.transform, "Corr", -71.5f, 7.5f, 19.25f, 21.75f, CeilCorr, LightGroupId.VCMain, 5000f, 7f);
            for (int i = 0; i < 4; i++)
                BatteryLight(corr.transform, "Corridor", i, new Vector3(-66f + i * 20f, CeilCorr - 0.12f, 19.45f));

            for (int i = 0; i < 5; i++)
            {
                float bx = -64f + i * 16.0f;
                p.InstantiatePropOrFallback("beacon_red", corr.transform, new Vector3(bx, 2.85f, 21.80f), new Vector3(90f, 0f, 0f), new Vector3(0.22f, 0.22f, 0.22f), grounded: false, addCollider: false, fallbackMat: p.EmissiveRed, customName: $"CorridorBeacon_{i}");
                WorldArchitectureKit.BuildHdrpLight(corr.transform, $"Light_Corr_Emerg_{i}", new Vector3(bx, 2.75f, 21.45f), Vector3.zero, LightType.Point, new Color(1.0f, 0.12f, 0.08f), 170f, 9.5f, LightGroupId.VCEmergency, volumetricDimmer: 1.05f, castShadows: i % 2 == 0, pulsingEmergency: true);
            }

            // Wall-mounted extinguishers, posters, bins, a drinking fountain and wear on the floor
            WallExtinguisher(corr.transform, p, new Vector3(-47.0f, 0f, 21.89f), Vector3.back);
            WallExtinguisher(corr.transform, p, new Vector3(-14.5f, 0f, 21.89f), Vector3.back);
            WallExtinguisher(corr.transform, p, new Vector3(4.0f, 0f, 19.11f), Vector3.forward);
            Poster(corr.transform, p, "Corr_Poster_Raptor", "raptor_poster", new Vector3(-57.5f, 1.55f, 19.11f), Vector3.forward, new Vector2(0.62f, 0.88f));
            Poster(corr.transform, p, "Corr_Poster_Rex", "rex_banner", new Vector3(-42.0f, 1.6f, 19.11f), Vector3.forward, new Vector2(0.55f, 1.1f));
            // seen through the lobby arch between the T. rex's feet: a framed poster instead of a blank stretch of wall
            Poster(corr.transform, p, "Corr_Poster_ArchView", "raptor_poster", new Vector3(-31.0f, 1.45f, 21.89f), Vector3.back, new Vector2(0.75f, 1.05f));
            Poster(corr.transform, p, "Corr_Poster_StaffOnly", "staff_only", new Vector3(-4.6f, 1.6f, 19.11f), Vector3.forward, new Vector2(0.7f, 0.35f));
            Poster(corr.transform, p, "Corr_Poster_FireAssembly", "fire_assembly", new Vector3(-29.0f, 1.6f, 21.89f), Vector3.back, new Vector2(0.55f, 0.55f));
            Poster(corr.transform, p, "Corr_Poster_Map", "map_park_modern", new Vector3(-66.5f, 1.65f, 21.89f), Vector3.back, new Vector2(1.3f, 0.78f));
            CorkBoard(corr.transform, "Corr_Notices", new Vector3(-17.5f, 1.55f, 21.89f), Vector3.back, 1.4f, 0.9f, 31);
            p.InstantiatePropOrFallback("trash_bin", corr.transform, new Vector3(-34.2f, 0f, 21.6f), Vector3.zero, new Vector3(0.4f, 0.68f, 0.4f));
            p.InstantiatePropOrFallback("trash_bin", corr.transform, new Vector3(-6.5f, 0f, 19.4f), Vector3.zero, new Vector3(0.4f, 0.68f, 0.4f));
            Transform fountain = Group(corr.transform, "DrinkingFountain", new Vector3(-50.8f, 0f, 19.11f), 180f);
            Box(fountain, "Basin", new Vector3(0f, 0.9f, -0.2f), new Vector3(0.45f, 0.2f, 0.36f), mSteel, true);
            Box(fountain, "Back", new Vector3(0f, 0.8f, -0.02f), new Vector3(0.45f, 0.5f, 0.04f), mSteel);
            Box(fountain, "Bubbler", new Vector3(0.1f, 1.02f, -0.18f), new Vector3(0.04f, 0.04f, 0.06f), mSteel);
            DecalAt(corr.transform, "Corr_Wear_0", p.DecalGrimeLeak, new Vector3(-37.0f, 0.02f, 20.5f), 2.6f, 15f, 0.55f);
            DecalAt(corr.transform, "Corr_Wear_1", p.DecalGrimeLeak, new Vector3(-9.0f, 0.02f, 20.6f), 2.4f, 80f, 0.5f);
            DecalAt(corr.transform, "Corr_Cracks_0", p.DecalCracks, new Vector3(-56.0f, 0.02f, 20.3f), 2.0f, 40f, 0.7f);
            WallDecal(corr.transform, "Corr_WallGrime_0", p.DecalGrimeLeak, new Vector3(5.0f, 2.6f, 21.89f), Vector3.back, new Vector2(1.4f, 1.4f));

            WorldArchitectureKit.BuildRoomReflectionProbe(corr.transform, "StaffCorridor", new Vector3(-32f, 1.6f, 20.5f), new Vector3(79.6f, 3.2f, 2.9f), "vc_offices");
        }

        // =========================================================================
        // WEST ROOMS: BREAK ROOM, STAFF EXIT CORRIDOR (REAL BROKEN CEILING!), GIFT SHOP
        // =========================================================================

        private static void BuildWestRoomsAndStaffExit(Transform parent, WorldMaterialPalette p)
        {
            var west = new GameObject("WestWing_BreakAndStaffExit");
            west.transform.SetParent(parent, false);

            // Break Room (X: -72..-50, Z: +6..+19) — P1-10 FIX: inset ceiling/RainBlocker away from Lobby West wall (X=-50) & use thin interior plaster liner!
            GameObject breakShell = WorldArchitectureKit.BuildBoxRoomShell(west.transform, "Room_BreakRoom", new Vector3(-61f, 0f, 12.5f), 0f, 22f, 13f, CeilStd, mFloorTile, mCeilTile, SurfaceType.Tile, p);
            TuckCeiling(breakShell);
            WorldArchitectureKit.BuildWallWithOpenings(west.transform, new Vector3(-72f, 0f, 6f), new Vector3(-72f, 0f, 19f), 0f, CeilStd, WallT, p.ConcreteBoardform, null, addSkirting: false, palette: p, wallName: "Wall_Break_West");
            // South wall faces the open service yard: concrete outside, painted inside
            WorldArchitectureKit.BuildWallWithOpenings(west.transform, new Vector3(-72f, 0f, 6f), new Vector3(-50f, 0f, 6f), 0f, CeilStd, WallT, p.ConcreteBoardform, null, addSkirting: false, palette: p, wallName: "Wall_Break_South");
            Liner(west.transform, "Lining_Break_W", new Vector3(-72f, 0f, 6f), new Vector3(-72f, 0f, 19f), Vector3.right, WallT * 0.5f, CeilStd, mOfficePaint, wainscot: mWainscot);
            Liner(west.transform, "Lining_Break_S", new Vector3(-72f, 0f, 6f), new Vector3(-50f, 0f, 6f), Vector3.forward, WallT * 0.5f, CeilStd, mOfficePaint, wainscot: mWainscot);
            Liner(west.transform, "Lining_Break_E", new Vector3(-50f, 0f, 6f), new Vector3(-50f, 0f, 19f), Vector3.left, 0.15f, CeilStd, mOfficePaint, wainscot: mWainscot);
            Liner(west.transform, "Lining_Break_N", new Vector3(-72f, 0f, 19f), new Vector3(-50f, 0f, 19f), Vector3.back, 0.11f, CeilCorr, mOfficePaint, new[] { (10.5f, 11.5f, 0f, 2.2f) }, thickness: 0.03f, wainscot: mWainscot);

            DressBreakRoom(west.transform, p);
            WorldArchitectureKit.BuildRoomReflectionProbe(west.transform, "BreakRoom", new Vector3(-61f, 1.7f, 12.5f), new Vector3(21.6f, 3.4f, 12.6f), "vc_offices");

            // Staff Exit Corridor (X: -72..-50, Z: -12..-8, Ceil: 3.25)
            // Build floor + split ceiling with REAL jagged hole directly above collapse rubble (X = -64.5, Z = -10.0)!
            WorldArchitectureKit.BuildBoxRoomShell(west.transform, "Room_StaffExitFloor", new Vector3(-61f, 0f, -10f), 0f, 22f, 4f, CeilCorr, p.ConcreteFloor, p.PlasterClean, SurfaceType.Concrete, p, buildCeiling: false, addRainBlocker: false);
            WorldArchitectureKit.CreateBox(west.transform, "StaffExit_CeilEast", new Vector3(-56.15f, CeilCorr + 0.108f, -10f), new Vector3(11.7f, 0.24f, 4.0f), mCeilTile, addCollider: true);
            WorldArchitectureKit.CreateBox(west.transform, "StaffExit_CeilWest", new Vector3(-69.5f, CeilCorr + 0.108f, -10f), new Vector3(5.0f, 0.24f, 4.0f), mCeilTile, addCollider: true);
            // Jagged broken rebar & bent concrete ceiling beams framing the collapse opening (X = -67..-62)
            for (int r = 0; r < 5; r++)
            {
                WorldArchitectureKit.CreateBox(west.transform, $"BrokenRebar_{r}", new Vector3(-66.6f + r * 1.1f, CeilCorr - 0.18f, -10f + (r % 2 == 0 ? 0.8f : -0.7f)), new Vector3(0.05f, 0.75f, 0.05f), p.RustedIron, addCollider: false, euler: new Vector3(22f, r * 35f, -35f));
            }
            WorldArchitectureKit.BuildRainBlockerSlab(west.transform, "RainBlock_StaffExitE", new Vector3(-56.15f, CeilCorr + 0.28f, -10f), new Vector3(11.7f, 0.16f, 4.0f));
            WorldArchitectureKit.BuildRainBlockerSlab(west.transform, "RainBlock_StaffExitW", new Vector3(-69.5f, CeilCorr + 0.28f, -10f), new Vector3(5.2f, 0.16f, 4.2f));

            WorldArchitectureKit.BuildWallWithOpenings(west.transform, new Vector3(-72f, 0f, -8f), new Vector3(-50f, 0f, -8f), 0f, CeilCorr, WallT, p.ConcreteBoardform, null, addSkirting: false, addDadoRail: false, palette: p, wallName: "Wall_StaffExit_North");
            WorldArchitectureKit.BuildWallWithOpenings(west.transform, new Vector3(-72f, 0f, -12f), new Vector3(-50f, 0f, -12f), 0f, CeilCorr, WallT, mSage, null, addSkirting: true, addDadoRail: true, palette: p, wallName: "Wall_StaffExit_South");
            // Service corridor: old green-painted plaster inside, raw concrete outside
            Liner(west.transform, "Lining_StaffExit_N", new Vector3(-72f, 0f, -8f), new Vector3(-50f, 0f, -8f), Vector3.back, WallT * 0.5f, CeilCorr, mSage, wainscot: mWainscot);
            Liner(west.transform, "Lining_StaffExit_W", new Vector3(-72f, 0f, -12f), new Vector3(-72f, 0f, -8f), Vector3.right, WallT * 0.5f, CeilCorr, mSage, new[] { (1.4f, 3.0f, 0f, 2.3f) });
            // the decay of the plain sage walls, all in decals at irregular spots (fix round): leak streaks under the ceiling
            // (1.2-2.0 m wide, fade 0.9-1.0), 7 crack patches per wall at random height, size and roll, scuffs at hip height
            {
                var rs = new System.Random(808);
                for (int i = 0; i < 9; i++)
                {
                    float x = -70.5f + i * 2.3f + (float)rs.NextDouble() * 1.2f;
                    bool north = i % 2 == 0;
                    float w = 1.2f + (float)rs.NextDouble() * 0.8f, h = 1.2f + (float)rs.NextDouble() * 1.3f;
                    WallDecal(west.transform, $"StaffExit_Leak_{i}", p.DecalGrimeLeak, new Vector3(x, CeilCorr - h * 0.5f, north ? -8.13f : -11.87f), north ? Vector3.back : Vector3.forward, new Vector2(w, h), 0.9f + (float)rs.NextDouble() * 0.1f);
                    if (i % 3 == 1)
                        WallDecal(west.transform, $"StaffExit_Scuff_{i}", p.DecalGrimeLeak, new Vector3(x + 1.1f, 0.55f, north ? -11.87f : -8.13f), north ? Vector3.forward : Vector3.back, new Vector2(1.4f, 0.5f), 0.6f);
                }
                for (int wall = 0; wall < 2; wall++)
                {
                    bool north = wall == 0;
                    Vector3 n = north ? Vector3.back : Vector3.forward;
                    float zf = north ? -8.13f : -11.87f;
                    float x = -71.3f + (float)rs.NextDouble() * 1.0f;
                    for (int c = 0; c < 7; c++)
                    {
                        float sz = 0.8f + (float)rs.NextDouble() * 1.0f;
                        float y = 0.9f + (float)rs.NextDouble() * (CeilCorr - 1.3f);
                        Vector3 pos = new Vector3(x, y, zf) + n * 0.1f;
                        Quaternion rot = Quaternion.LookRotation(-n) * Quaternion.Euler(0f, 0f, (float)rs.NextDouble() * 360f);
                        WorldArchitectureKit.BuildDecal(west.transform, $"StaffExit_Crack_{wall}_{c}", pos, rot.eulerAngles, new Vector3(sz, sz * (0.7f + (float)rs.NextDouble() * 0.5f), 0.4f), p.DecalCracks, 0.75f + (float)rs.NextDouble() * 0.25f);
                        x += 1.9f + (float)rs.NextDouble() * 2.4f;
                        if (x > -50.6f) break;
                    }
                }
            }
            Liner(west.transform, "Lining_StaffExit_E", new Vector3(-50f, 0f, -12f), new Vector3(-50f, 0f, -8f), Vector3.left, 0.15f, CeilCorr, mSage, new[] { (1.0f, 3.0f, 0f, 2.4f) });

            // DOOR-9 FIX: Exterior West Wall (X = -72, Z = -12 -> -8, len = 4) with opening CenterAlong = 2.2 (Z = -9.8, W = 1.6 -> Z: -10.6..-9.0) — EXACT 0.0 m error!
            WorldArchitectureKit.BuildWallWithOpenings(
                west.transform,
                new Vector3(-72f, 0f, -12f),
                new Vector3(-72f, 0f, -8f),
                0f, CeilCorr, WallT, p.ConcreteBoardform,
                new[] { OpeningSpec.Door(2.2f, 1.6f, 2.3f) },
                addSkirting: true, palette: p, wallName: "Wall_StaffExit_WestExterior");

            WorldArchitectureKit.BuildFramedDoor(west.transform, "staff_exit_door", new Vector3(-72f, 0f, -9.8f), 90f, 1.6f, 2.3f, DoorMechanism.SingleHinged, p, startsLocked: true, lockedHint: "Staff Exit - jammed shut from storm debris outside.", soundPrefix: "door_metal", bashableByRaptor: false, overheadSignText: "STAFF EXIT");
            // P2-7 FIX: Heavy steel weather-seal rabbet strip on right jamb closing the hairline light seam!
            WorldArchitectureKit.CreateBox(west.transform, "StaffExit_RightJambSeal", new Vector3(-72.0f, 1.18f, -9.00f), new Vector3(0.32f, 2.36f, 0.14f), p.DarkPaintedMetal, addCollider: false);

            // Collapse Rubble Pile (activated by StoryDirector on trigger staff_exit_collapse, positioned directly beneath broken ceiling hole!)
            var collapseGo = new GameObject("StaffExit_CollapseDebris");
            collapseGo.transform.SetParent(west.transform, false);
            collapseGo.transform.position = new Vector3(-64.5f, 0f, -10.0f);
            p.InstantiatePropOrFallback("debris_pile", collapseGo.transform, new Vector3(-64.5f, 0f, -10.0f), new Vector3(0f, 12f, 0f), new Vector3(3.8f, 2.4f, 3.8f), grounded: true, addCollider: true, fallbackMat: p.ConcreteExterior);
            // Review 1: the visible slab was one clean tilted box hiding the debris mesh. Its collider stays (the route stays
            // blocked), invisible; the debris_pile mesh is the mass, with broken slab chunks, fallen ceiling, cables and dust.
            var blocker = new GameObject("FallenSlab_Blocker");
            blocker.transform.SetParent(collapseGo.transform, false);
            blocker.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            blocker.transform.localRotation = Quaternion.Euler(14f, 8f, -18f);
            blocker.layer = GameLayers.Environment;
            blocker.AddComponent<BoxCollider>().size = new Vector3(2.8f, 2.3f, 3.6f);
            DressCollapse(collapseGo.transform, p);
            AttachNamedWorldAnchor(collapseGo, "staff_exit_collapse");
            WorldArchitectureKit.BuildWorldAnchor(west.transform, "staff_exit_look", new Vector3(-64.5f, 1.2f, -10.0f));
            WorldArchitectureKit.BuildStoryTrigger(west.transform, "staff_exit_collapse", new Vector3(-55.5f, 1.5f, -10.0f), new Vector3(5.5f, 3.0f, 3.6f), oneShot: true);
            CeilingLamp(west.transform, "Light_StaffExit_Main", new Vector3(-58f, CeilCorr - 0.15f, -10f), LightGroupId.VCMain, 3600f);
            BatteryLight(west.transform, "StaffExit", -1, new Vector3(-55.0f, CeilCorr - 0.15f, -10.6f));
            WorldArchitectureKit.BuildHdrpLight(west.transform, "Light_StaffExit_Emerg", new Vector3(-71.4f, 2.75f, -11.25f), new Vector3(90f, 0f, 0f), LightType.Spot, new Color(1.0f, 0.12f, 0.08f), 60f, 4.5f, LightGroupId.VCEmergency, volumetricDimmer: 0.6f, spotOuterAngle: 150f, pulsingEmergency: true);
            WorldArchitectureKit.BuildRoomReflectionProbe(west.transform, "StaffExit", new Vector3(-61f, 1.6f, -10f), new Vector3(21.6f, 3.2f, 3.8f), "vc_offices");

            // Gift Shop Showcase (X: -72..-50, Z: -32..-12) behind south glass facade — P1-10 inset ceiling/RainBlocker away from Lobby West wall!
            GameObject giftShell = WorldArchitectureKit.BuildBoxRoomShell(west.transform, "Room_GiftShop", new Vector3(-61f, 0f, -22f), 0f, 22f, 20f, CeilStd, p.ConcreteFloor, mCeilTile, SurfaceType.Concrete, p);
            TuckCeiling(giftShell);
            WorldArchitectureKit.BuildWallWithOpenings(west.transform, new Vector3(-72f, 0f, -32f), new Vector3(-50f, 0f, -32f), 0f, CeilStd, WallT, p.ConcreteBoardform,
                new[] { OpeningSpec.Window(11.0f, 18.0f, 0.4f, 2.75f, mullions: 5, glassMat: mGlass) }, palette: p, wallName: "Wall_GiftShop_SouthGlass", includeInOcclusion: false);
            // The missing west wall: solid boardform exterior, closes the room
            WorldArchitectureKit.BuildWallWithOpenings(west.transform, new Vector3(-72f, 0f, -32f), new Vector3(-72f, 0f, -12f), 0f, CeilStd, WallT, p.ConcreteBoardform, null, addSkirting: false, palette: p, wallName: "Wall_GiftShop_WestExterior");
            Liner(west.transform, "Lining_Gift_W", new Vector3(-72f, 0f, -32f), new Vector3(-72f, 0f, -12f), Vector3.right, WallT * 0.5f, CeilStd, mOfficePaint);
            Liner(west.transform, "Lining_Gift_S", new Vector3(-72f, 0f, -32f), new Vector3(-50f, 0f, -32f), Vector3.forward, WallT * 0.5f, CeilStd, mOfficePaint, new[] { (2.0f, 20.0f, 0.4f, 3.15f) });
            Liner(west.transform, "Lining_Gift_N", new Vector3(-72f, 0f, -12f), new Vector3(-50f, 0f, -12f), Vector3.back, WallT * 0.5f, CeilStd, mOfficePaint, thickness: 0.03f);
            Liner(west.transform, "Lining_Gift_E", new Vector3(-50f, 0f, -32f), new Vector3(-50f, 0f, -12f), Vector3.left, 0.15f, CeilStd, mFeatureGreen, new[] { (4.0f, 16.0f, 0.1f, 2.8f) });
            DressGiftShop(west.transform, p);
        }

        private static void DressBreakRoom(Transform parent, WorldMaterialPalette p)
        {
            var br = new GameObject("Props_BreakRoom");
            br.transform.SetParent(parent, false);
            const float eastFace = -50.173f;   // lining face on the lobby wall
            const float westFace = -71.847f;

            // Kitchenette along the east wall: base cabinets + sink + fridge + upper cabinets + microwave + coffee maker
            Transform kc = Counter(br.transform, "Kitchenette", new Vector3(eastFace - 0.33f, 0f, 12.2f), 90f, 4.4f, 0.64f, 0.9f, mLaminate, mCabinet);
            Box(kc, "SinkBasin", new Vector3(0.6f, 0.885f, 0f), new Vector3(0.62f, 0.04f, 0.44f), mSteel);
            Box(kc, "SinkInner", new Vector3(0.6f, 0.9f, 0f), new Vector3(0.52f, 0.012f, 0.36f), mBlack);
            Box(kc, "Tap", new Vector3(0.6f, 1.02f, 0.2f), new Vector3(0.03f, 0.24f, 0.03f), mSteel);
            Box(kc, "TapSpout", new Vector3(0.6f, 1.13f, 0.12f), new Vector3(0.025f, 0.025f, 0.16f), mSteel);
            Box(kc, "Microwave", new Vector3(-1.5f, 1.05f, 0.05f), new Vector3(0.5f, 0.30f, 0.38f), mBlack);
            Box(kc, "MicrowaveDoor", new Vector3(-1.56f, 1.05f, -0.145f), new Vector3(0.34f, 0.22f, 0.01f), mGlass);
            Box(kc, "CoffeeMaker", new Vector3(-0.7f, 1.06f, 0.1f), new Vector3(0.22f, 0.32f, 0.26f), mBlack);
            Box(kc, "CoffeePot", new Vector3(-0.7f, 0.97f, -0.06f), new Vector3(0.14f, 0.13f, 0.12f), mGlass);
            for (int i = 0; i < 4; i++)
            {
                Box(kc, $"UpperCab_{i}", new Vector3(-1.65f + i * 1.1f, 1.85f, 0.14f), new Vector3(1.06f, 0.70f, 0.34f), mCabinet);
                Box(kc, $"UpperCabSeam_{i}", new Vector3(-1.65f + i * 1.1f, 1.85f, -0.032f), new Vector3(0.006f, 0.66f, 0.004f), mBlack);
                Box(kc, $"UpperCabPull_{i}", new Vector3(-1.65f + i * 1.1f + 0.06f, 1.58f, -0.04f), new Vector3(0.012f, 0.1f, 0.015f), mSteel);
            }
            // Fridge at the south end of the run
            Transform fr = Group(br.transform, "Fridge", new Vector3(eastFace - 0.37f, 0f, 9.45f), 90f);
            Box(fr, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.78f, 1.8f, 0.72f), mLaminate, true);
            Box(fr, "DoorSeam", new Vector3(0f, 1.22f, -0.362f), new Vector3(0.76f, 0.008f, 0.004f), mBlack);
            Box(fr, "HandleTop", new Vector3(0.3f, 1.5f, -0.38f), new Vector3(0.025f, 0.36f, 0.03f), mSteel);
            Box(fr, "HandleBot", new Vector3(0.3f, 0.95f, -0.38f), new Vector3(0.025f, 0.30f, 0.03f), mSteel);

            // Vending machine against the west wall
            Transform vm = Group(br.transform, "VendingMachine", new Vector3(westFace + 0.44f, 0f, 9.4f), -90f);
            BoxCollider vbc = vm.gameObject.AddComponent<BoxCollider>();
            vbc.center = new Vector3(0f, 0.95f, 0f); vbc.size = new Vector3(1.0f, 1.9f, 0.86f);
            // Cabinet as a shell round a recess behind the glass (a solid box hid the trays, snacks and lit back panel)
            Box(vm, "CabinetBody", new Vector3(0f, 0.95f, 0.215f), new Vector3(1.0f, 1.9f, 0.43f), mVendRed);
            Box(vm, "CabinetStileL", new Vector3(-0.48f, 0.95f, -0.195f), new Vector3(0.04f, 1.9f, 0.39f), mVendRed);
            Box(vm, "CabinetPanelR", new Vector3(0.35f, 0.95f, -0.195f), new Vector3(0.30f, 1.9f, 0.39f), mVendRed);
            Box(vm, "CabinetBase", new Vector3(-0.13f, 0.2475f, -0.195f), new Vector3(0.66f, 0.495f, 0.39f), mVendRed);
            Box(vm, "CabinetHead", new Vector3(-0.13f, 1.8225f, -0.195f), new Vector3(0.66f, 0.155f, 0.39f), mVendRed);
            Box(vm, "Window", new Vector3(-0.13f, 1.12f, -0.395f), new Vector3(0.66f, 1.25f, 0.012f), mGlass);
            GameObject glow = Box(vm, "InteriorGlow", new Vector3(-0.13f, 1.12f, -0.2f), new Vector3(0.64f, 1.22f, 0.01f), mVendGlow);
            Material[] snack = { mBinderBlue, mPaper, p.PaintedMetalYellow, mFeatureGreen, mBinderRed };
            for (int r = 0; r < 5; r++)
            {
                float y = 0.62f + r * 0.235f;
                Box(vm, $"Tray_{r}", new Vector3(-0.13f, y, -0.28f), new Vector3(0.64f, 0.012f, 0.16f), mSteel);
                for (int c = 0; c < 5; c++)
                {
                    Box(vm, $"Snack_{r}_{c}", new Vector3(-0.39f + c * 0.13f, y + 0.08f, -0.3f), new Vector3(0.10f, 0.15f, 0.03f), snack[(r * 2 + c) % snack.Length]);
                    if ((r + c) % 2 == 0) ProductFace(vm, p, $"SnackWrap_{r}_{c}", new Vector3(-0.39f + c * 0.13f, y + 0.08f, -0.315f), new Vector2(0.085f, 0.12f), false, (r * 3 + c) % 4);
                }
            }
            Box(vm, "Keypad", new Vector3(0.33f, 1.25f, -0.4f), new Vector3(0.2f, 0.3f, 0.02f), mBlack);
            Box(vm, "CoinSlot", new Vector3(0.33f, 1.02f, -0.405f), new Vector3(0.06f, 0.1f, 0.02f), mSteel);
            Box(vm, "DeliveryFlap", new Vector3(-0.13f, 0.28f, -0.405f), new Vector3(0.6f, 0.18f, 0.02f), mBlack);
            WorldArchitectureKit.AttachLightGroupMember(glow, LightGroupId.VCMain, null, new Renderer[] { glow.GetComponent<MeshRenderer>() });

            // Three lunch tables with plastic chairs, a few cups and a newspaper
            for (int i = 0; i < 3; i++)
            {
                float tx = -66f + i * 4.6f;
                Table(br.transform, $"BreakTable_{i}", new Vector3(tx, 0f, 12.5f), 0f, 1.4f, 0.8f, 0.75f, mLaminate, mSteel);
                Chair(br.transform, $"BreakChair_{i}_S", new Vector3(tx - 0.3f, 0f, 11.75f), 188f + i * 7f);
                Chair(br.transform, $"BreakChair_{i}_S2", new Vector3(tx + 0.35f, 0f, 11.8f), 168f);
                Chair(br.transform, $"BreakChair_{i}_N", new Vector3(tx + 0.1f, 0f, 13.3f), (i - 1) * 14f);
                Box(br.transform, $"Cup_{i}", new Vector3(tx + 0.25f - i * 0.2f, 0.80f, 12.4f), new Vector3(0.08f, 0.10f, 0.08f), i == 1 ? mBinderRed : mPaper);
            }
            PaperStack(br.transform, "Newspaper", new Vector3(-61.4f, 0.75f, 12.6f), 23f, 2);
            p.InstantiatePropOrFallback("trash_bin", br.transform, new Vector3(-51.0f, 0f, 15.3f), Vector3.zero, new Vector3(0.45f, 0.75f, 0.45f));
            p.InstantiatePropOrFallback("wet_floor_sign", br.transform, new Vector3(-60.2f, 0f, 16.5f), new Vector3(0f, 20f, 0f), new Vector3(0.35f, 0.65f, 0.35f));

            // Notice board + posters
            Transform nb = Group(br.transform, "NoticeBoard", new Vector3(-62.0f, 1.55f, 6.153f), 0f);
            Box(nb, "Frame", new Vector3(0f, 0f, 0.012f), new Vector3(1.6f, 1.0f, 0.024f), mVeneer);
            Box(nb, "Cork", new Vector3(0f, 0f, 0.016f), new Vector3(1.5f, 0.9f, 0.02f), mCork);
            for (int i = 0; i < 5; i++)
                Box(nb, $"Note_{i}", new Vector3(-0.55f + i * 0.27f, (i % 2 == 0 ? 0.15f : -0.12f), 0.0275f), new Vector3(0.2f, 0.27f, 0.003f), mPaper, false, new Vector3(0f, 0f, (i - 2) * 3f));
            Poster(br.transform, p, "Poster_FireAssembly", "fire_assembly", new Vector3(-57.5f, 1.6f, 6.153f), Vector3.forward, new Vector2(0.6f, 0.6f));
            Poster(br.transform, p, "Poster_RaptorBreak", "raptor_poster", new Vector3(-66.5f, 1.6f, 6.153f), Vector3.forward, new Vector2(0.62f, 0.88f));

            // Lights: three ceiling fixtures (VCMain) + one battery unit
            for (int i = 0; i < 3; i++)
                CeilingLamp(br.transform, i == 0 ? "Light_Break_Main" : $"Light_Break_Main_{i}", new Vector3(-66f + i * 5.5f, CeilStd - 0.15f, 12.5f), LightGroupId.VCMain, 3600f);
            Washes(br.transform, "Break", -71.5f, -50.5f, 6.5f, 18.5f, LightGroupId.VCMain, walls: "SSSNEW");
            BatteryLight(br.transform, "Break", -1, new Vector3(-59.5f, CeilStd - 0.15f, 18.3f));   // beside the door, not over it
        }

        /// <summary>Collapse debris round the staff exit pile (local to the pile at x -64.5, z -10; the ceiling hole spans
        /// x -67..-62): fractured slab chunks leaning on the pile, fallen ceiling tiles and a bent troffer hanging out of
        /// the hole, a torn duct, dangling cables, broken T-bar grid, a fan of dust on the floor.</summary>
        private static void DressCollapse(Transform c, WorldMaterialPalette p)
        {
            // dusty mid-grey slab concrete (the palette's cracked exterior concrete read tan, like plywood, in the beam)
            Material slab = p.Surface("MAT_Z1_SlabConcrete", "concrete_cracked_ext", "BaseColor", "MaskMap", 1.2f, new Color(0.66f, 0.67f, 0.68f), 0.05f, 0.35f, 0f, 1.0f);
            // Review 2: nine crisp boxes read as tan boards. Now two fractured slab sections (each split in three pieces
            // that tilt apart, cracks projected on top), and the lumps are debris_pile meshes at rubble scale.
            (Vector3 pos, Vector3 size, Vector3 eul)[] slabs =
            {
                (new Vector3(-0.9f, 0.85f, 0.6f), new Vector3(1.9f, 0.24f, 1.5f), new Vector3(-28f, 22f, 31f)),
                (new Vector3(0.8f, 0.7f, -0.7f), new Vector3(1.6f, 0.22f, 1.3f), new Vector3(24f, -35f, -26f)),
            };
            for (int i = 0; i < slabs.Length; i++)
            {
                Transform sg = Group(c, $"SlabSection_{i}", c.TransformPoint(slabs[i].pos), 0f);
                sg.localRotation = Quaternion.Euler(slabs[i].eul);
                Vector3 sz = slabs[i].size;
                float[] cut = { 0.38f, 0.33f, 0.29f };          // fractions of the length, jagged by the tilt between pieces
                float x = -sz.x * 0.5f;
                for (int k = 0; k < 3; k++)
                {
                    float w = sz.x * cut[k];
                    Box(sg, $"SlabChunk_{i}_{k}", new Vector3(x + w * 0.5f + k * 0.035f, (k - 1) * 0.04f, (k % 2) * 0.05f),
                        new Vector3(w - 0.02f, sz.y * 1.3f * (1f - k * 0.08f), sz.z * (1f - k * 0.12f)), slab, false, new Vector3(k * 7f - 6f, k * 9f - 9f, (k - 1) * 11f));
                    x += w;
                }
                WorldArchitectureKit.BuildDecal(sg, $"SlabCracks_{i}", sg.position + sg.up * (sz.y * 0.5f + 0.05f), Quaternion.LookRotation(-sg.up, sg.forward).eulerAngles,
                    new Vector3(sz.x * 1.05f, sz.z * 1.05f, 0.3f), p.DecalCracks, 0.85f);
            }
            // rubble: debris_pile meshes at 0.8-1.8 m round the foot of the main pile (no colliders: the pile's own collider
            // and the slab blocker keep the route blocked)
            (Vector3 pos, float size)[] rubble =
            {
                (new Vector3(1.75f, 0f, 0.9f), 1.4f), (new Vector3(2.2f, 0f, -0.7f), 1.1f), (new Vector3(-1.9f, 0f, -0.95f), 1.6f),
                (new Vector3(2.75f, 0f, 0.3f), 0.8f), (new Vector3(1.4f, 0f, 1.55f), 0.9f), (new Vector3(-1.6f, 0f, 1.3f), 1.8f),
                (new Vector3(0.3f, 0f, -1.6f), 1.2f),
            };
            for (int i = 0; i < rubble.Length; i++)
            {
                float sz = rubble[i].size;
                p.InstantiatePropOrFallback("debris_pile", c, c.TransformPoint(rubble[i].pos), new Vector3(0f, 37f + i * 71f, 0f),
                    new Vector3(sz, sz * 0.42f, sz * 0.9f), grounded: true, addCollider: false, fallbackMat: slab, customName: $"Rubble_{i}");
            }
            // rebar sticking out of the big chunks
            for (int r = 0; r < 4; r++)
                Box(c, $"ChunkRebar_{r}", new Vector3(-0.4f + r * 0.45f, 1.1f + (r % 2) * 0.3f, 0.2f - r * 0.2f), new Vector3(0.022f, 0.022f, 1.1f), p.RustedIron, false, new Vector3(-20f + r * 13f, 30f + r * 41f, 0f));
            // fallen ceiling tiles on the floor east of the pile (towards the player): dusty grey, not paper white, half of
            // them snapped into L-shaped pairs
            Material fallen = p.Surface("MAT_Z1_FallenTile", "ceiling_tiles", "BaseColor", "MaskMap", 2.4f, new Color(0.62f, 0.61f, 0.60f), 0.02f, 0.2f, 0f, 1.0f);
            var rng = new System.Random(808);
            for (int t = 0; t < 11; t++)
            {
                float x = 1.6f + (float)rng.NextDouble() * 2.6f, z = -1.5f + (float)rng.NextDouble() * 3.0f;
                Vector3 e = new Vector3((float)rng.NextDouble() * 8f - 4f, (float)rng.NextDouble() * 90f, (float)rng.NextDouble() * 6f - 3f);
                Vector3 at = new Vector3(x, 0.012f + t * 0.002f, z);
                if (t % 2 == 0)
                    Box(c, $"FallenTile_{t}", at, new Vector3(0.6f, 0.016f, t % 3 == 0 ? 0.32f : 0.6f), fallen, false, e);
                else
                {
                    // an L: a 0.6 x 0.3 strip and a 0.3 x 0.3 square broken off and lying a few cm apart
                    Transform lt = Group(c, $"FallenTileL_{t}", c.TransformPoint(at), 0f);
                    lt.localRotation = Quaternion.Euler(e);
                    Box(lt, "A", new Vector3(0f, 0f, -0.15f), new Vector3(0.6f, 0.016f, 0.3f), fallen);
                    Box(lt, "B", new Vector3(-0.17f, 0.004f, 0.17f), new Vector3(0.28f, 0.016f, 0.29f), fallen, false, new Vector3(0f, 9f + t, 3f));
                }
            }
            for (int t = 0; t < 4; t++)
                Box(c, $"PileTile_{t}", new Vector3(-0.6f + t * 0.5f, 1.65f + (t % 2) * 0.25f, -0.4f + t * 0.3f), new Vector3(0.6f, 0.016f, 0.6f), fallen, false, new Vector3(30f - t * 17f, t * 50f, 20f + t * 9f));
            // broken T-bar grid hanging from the hole edges (the hole is local x -2.5..+2.5, ceiling at 3.25 m)
            for (int b = 0; b < 6; b++)
            {
                float x = b < 3 ? 2.45f : -2.45f;
                float z = -1.4f + (b % 3) * 1.2f;
                Box(c, $"TBar_{b}", new Vector3(x - Mathf.Sign(x) * 0.42f, 2.98f, z), new Vector3(0.03f, 0.02f, 0.9f), mEnamel, false, new Vector3(0f, 90f + b * 7f, Mathf.Sign(x) * (28f + b * 6f)));
            }
            // bent troffer hanging out of the hole on its cable
            Transform tf = Group(c, "HangingTroffer", c.position + new Vector3(1.6f, 2.35f, 0.6f), 23f);
            tf.localRotation = Quaternion.Euler(52f, 23f, 14f);
            Box(tf, "Body", Vector3.zero, new Vector3(1.22f, 0.09f, 0.62f), mEnamel);
            Box(tf, "Opal", new Vector3(0f, -0.048f, 0f), new Vector3(1.16f, 0.006f, 0.56f), mOpal);
            Box(tf, "LouvreA", new Vector3(-0.3f, -0.056f, 0f), new Vector3(0.016f, 0.012f, 0.56f), mEnamel);
            Box(tf, "LouvreB", new Vector3(0.3f, -0.056f, 0f), new Vector3(0.016f, 0.012f, 0.56f), mEnamel);
            Box(c, "TrofferCable", new Vector3(1.85f, 2.9f, 0.75f), new Vector3(0.012f, 0.8f, 0.012f), mBlack, false, new Vector3(6f, 0f, -14f));
            // torn duct stub leaning out of the void, insulation flaps
            Box(c, "DuctStub", new Vector3(-1.4f, 2.75f, -1.2f), new Vector3(0.5f, 0.32f, 1.6f), p.BrushedSteel, false, new Vector3(-24f, 12f, 4f));
            Box(c, "DuctFlap", new Vector3(-1.32f, 2.2f, -0.45f), new Vector3(0.46f, 0.004f, 0.36f), p.BrushedSteel, false, new Vector3(-62f, 18f, 9f));
            // dangling cables of different lengths
            for (int k = 0; k < 6; k++)
            {
                float len = 0.7f + (k % 3) * 0.45f;
                Box(c, $"Cable_{k}", new Vector3(-1.9f + k * 0.75f, 3.2f - len * 0.5f, 1.25f - (k % 2) * 2.4f), new Vector3(0.016f, len, 0.016f), k == 2 ? p.PaintedMetalYellow : mBlack, false, new Vector3((k - 2) * 7f, 0f, (k % 2 == 0 ? 1f : -1f) * (6f + k * 3f)));
            }
            // pale plaster/concrete dust fanned across the floor towards the player and banked against the walls. The grime
            // texture is a dark mask (RGB ~0.1), so the decal colour is boosted to land at ~0.75 albedo.
            Material dust = p.CreateDecalMaterial("MAT_Z1_DecalDust", "grime_leak_1", null, Color.white);
            dust.SetColor("_BaseColor", new Color(5.4f, 5.2f, 4.9f, 0.7f));
            EditorUtility.SetDirty(dust);
            DecalAt(c, "Collapse_DustFan", dust, c.position + new Vector3(2.4f, 0.03f, 0f), 5.0f, 90f, 0.9f);
            DecalAt(c, "Collapse_DustFan2", dust, c.position + new Vector3(4.4f, 0.03f, -0.3f), 3.4f, 160f, 0.75f);
            DecalAt(c, "Collapse_DustFan3", dust, c.position + new Vector3(3.6f, 0.03f, 0.9f), 2.6f, 35f, 0.6f);
            WallDecal(c, "Collapse_DustWallN", dust, c.position + new Vector3(1.5f, 0.35f, 1.86f), Vector3.back, new Vector2(4.5f, 0.7f), 0.7f);
            WallDecal(c, "Collapse_DustWallS", dust, c.position + new Vector3(1.5f, 0.35f, -1.86f), Vector3.forward, new Vector2(4.5f, 0.7f), 0.7f);
        }

        /// <summary>Stackable plastic chair: seat, back, four steel legs. yaw: the way the sitter faces is local -Z.</summary>
        private static void Chair(Transform parent, string name, Vector3 floorPos, float yaw)
        {
            Transform t = Group(parent, name, floorPos, yaw);
            Box(t, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(0.44f, 0.035f, 0.42f), mFabric, true);
            Box(t, "Back", new Vector3(0f, 0.72f, 0.2f), new Vector3(0.42f, 0.34f, 0.03f), mFabric, false, new Vector3(-8f, 0f, 0f));
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0 ? -1f : 1f) * 0.19f;
                float sz = (i < 2 ? -1f : 1f) * 0.18f;
                Box(t, $"Leg_{i}", new Vector3(sx, 0.22f, sz), new Vector3(0.022f, 0.44f, 0.022f), mSteel);
            }
            Box(t, "BackPostL", new Vector3(-0.19f, 0.6f, 0.19f), new Vector3(0.02f, 0.3f, 0.02f), mSteel);
            Box(t, "BackPostR", new Vector3(0.19f, 0.6f, 0.19f), new Vector3(0.02f, 0.3f, 0.02f), mSteel);
        }

        private static void DressGiftShop(Transform parent, WorldMaterialPalette p)
        {
            var g = new GameObject("Props_GiftShop");
            g.transform.SetParent(parent, false);

            // Three double-sided gondolas running north-south, a wall bay on the west wall, a checkout by the north wall
            for (int i = 0; i < 3; i++)
            {
                Gondola(g.transform, p, $"Gondola_{i}", new Vector3(-67.0f + i * 4.2f, 0f, -22.5f), 90f, 4.8f, 100 + i);
            }
            WallShelf(g.transform, p, "WallBay_W_0", new Vector3(-71.45f, 0f, -27.2f), -90f, 3.6f, 200);
            WallShelf(g.transform, p, "WallBay_W_1", new Vector3(-71.45f, 0f, -16.8f), -90f, 3.6f, 201);
            Poster(g.transform, p, "Banner_RexWest", "rex_banner", new Vector3(-71.847f, 1.85f, -22.0f), Vector3.right, new Vector2(1.25f, 2.5f));
            Poster(g.transform, p, "Poster_Raptor", "raptor_poster", new Vector3(-66.0f, 1.65f, -12.163f), Vector3.back, new Vector2(0.9f, 1.27f));
            Poster(g.transform, p, "Poster_ParkLogoE", "park_logo", new Vector3(-50.173f, 1.75f, -13.9f), Vector3.left, new Vector2(1.3f, 1.3f));

            // Checkout: counter facing the shop floor, register, card reader, bag stack, back shelf with the park logo
            Transform co = Counter(g.transform, "CheckoutCounter", new Vector3(-56.5f, 0f, -14.6f), 0f, 3.2f, 0.7f, 1.0f, mLaminate, mFeatureGreen);
            Box(co, "Register", new Vector3(-0.7f, 1.08f, 0.05f), new Vector3(0.42f, 0.12f, 0.38f), mBlack);
            Box(co, "RegisterScreen", new Vector3(-0.7f, 1.24f, 0.16f), new Vector3(0.32f, 0.22f, 0.03f), mBlack, false, new Vector3(-15f, 0f, 0f));
            Box(co, "CashDrawer", new Vector3(-0.7f, 0.995f, -0.1f), new Vector3(0.40f, 0.05f, 0.30f), mBlack);
            Box(co, "BagStack", new Vector3(0.6f, 1.04f, 0.1f), new Vector3(0.36f, 0.05f, 0.28f), mPaper);
            PaperStack(co, "Receipts", new Vector3(-56.4f, 1.0f, -14.48f), 12f, 2);
            Poster(g.transform, p, "Poster_ParkLogoCheckout", "park_logo", new Vector3(-56.5f, 2.15f, -12.163f), Vector3.back, new Vector2(1.0f, 1.0f));
            WallShelf(g.transform, p, "BackShelf", new Vector3(-58.9f, 0f, -12.56f), 0f, 2.0f, 202, levels: 3);

            // Display tables behind the south window (seen from the plaza), with stacked merchandise
            Material[] prod = { mVendRed, mBinderBlue, mFeatureGreen, p.PaintedMetalYellow, mPaper };
            for (int t = 0; t < 3; t++)
            {
                Vector3 tp = new Vector3(-67.0f + t * 6.0f, 0f, -29.6f);
                Transform tb = Table(g.transform, $"DisplayTable_{t}", tp, 0f, 1.8f, 0.9f, 0.78f, mVeneer, mBlack);
                var rng = new System.Random(300 + t);
                for (int k = 0; k < 5; k++)
                {
                    float w = 0.22f + (float)rng.NextDouble() * 0.16f;
                    float h = 0.06f + (float)rng.NextDouble() * 0.10f;
                    int layers = 1 + rng.Next(3);
                    for (int l = 0; l < layers; l++)
                        Box(tb, $"Merch_{k}_{l}", new Vector3(-0.68f + k * 0.34f, 0.78f + h * (l + 0.5f), (float)rng.NextDouble() * 0.2f - 0.1f), new Vector3(w, h * 0.98f, w * 0.8f), prod[(k + t) % prod.Length], false, new Vector3(0f, (float)rng.NextDouble() * 20f - 10f, 0f));
                }
            }
            p.InstantiatePropOrFallback("trash_bin", g.transform, new Vector3(-53.0f, 0f, -14.2f), Vector3.zero, new Vector3(0.45f, 0.75f, 0.45f));

            // Lighting: four ceiling fixtures (VCMain) and one battery unit for the blackout
            Vector2[] fx = { new(-66f, -17f), new(-56f, -17f), new(-66f, -27f), new(-56f, -27f) };
            for (int i = 0; i < fx.Length; i++)
                CeilingLamp(g.transform, i == 0 ? "Light_GiftShop" : $"Light_GiftShop_{i}", new Vector3(fx[i].x, CeilStd - 0.15f, fx[i].y), LightGroupId.VCMain, 3400f);
            Washes(g.transform, "GiftShop", -71.5f, -50.5f, -31.5f, -12.5f, LightGroupId.VCMain, walls: "NNNSEW");
            BatteryLight(g.transform, "GiftShop", -1, new Vector3(-55.5f, CeilStd - 0.15f, -13.2f));
            WorldArchitectureKit.BuildRoomReflectionProbe(g.transform, "GiftShop", new Vector3(-61f, 1.7f, -22f), new Vector3(21.6f, 3.4f, 19.6f), "vc_offices");
        }

        /// <summary>Double-sided shop gondola: base, pegboard spine, end caps, four shelves per side filled with boxed merchandise.</summary>
        private static void Gondola(Transform parent, WorldMaterialPalette p, string name, Vector3 floorPos, float yaw, float length, int seed)
        {
            Transform t = Group(parent, name, floorPos, yaw);
            BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.85f, 0f); bc.size = new Vector3(length, 1.7f, 0.9f);
            Box(t, "Base", new Vector3(0f, 0.06f, 0f), new Vector3(length, 0.12f, 0.88f), mBlack);
            Box(t, "Spine", new Vector3(0f, 0.85f, 0f), new Vector3(length, 1.5f, 0.04f), mCabinet);
            Box(t, "CapA", new Vector3(-length * 0.5f, 0.82f, 0f), new Vector3(0.04f, 1.64f, 0.9f), mCabinet);
            Box(t, "CapB", new Vector3(length * 0.5f, 0.82f, 0f), new Vector3(0.04f, 1.64f, 0.9f), mCabinet);
            Box(t, "Header", new Vector3(0f, 1.68f, 0f), new Vector3(length + 0.04f, 0.14f, 0.08f), mFeatureGreen);
            float[] ys = { 0.12f, 0.52f, 0.92f, 1.30f };
            var rng = new System.Random(seed);
            Material[] prod = { mVendRed, mBinderBlue, mFeatureGreen, p.PaintedMetalYellow, mPaper, mBinderBlack };
            for (int side = -1; side <= 1; side += 2)
            {
                for (int s = 0; s < ys.Length; s++)
                {
                    float y = ys[s];
                    if (s > 0) Box(t, $"Shelf_{side}_{s}", new Vector3(0f, y, side * 0.23f), new Vector3(length - 0.06f, 0.025f, 0.42f), mCabinet);
                    float x = -length * 0.5f + 0.06f;
                    int k = 0;
                    Material shelfMat = prod[rng.Next(prod.Length)];
                    while (true)
                    {
                        float w = 0.16f + (float)rng.NextDouble() * 0.22f;
                        if (x + w > length * 0.5f - 0.06f) break;
                        float h = 0.12f + (float)rng.NextDouble() * (s == 3 ? 0.22f : 0.24f);
                        float d = 0.22f + (float)rng.NextDouble() * 0.14f;
                        if (rng.NextDouble() < 0.12) { x += w * 0.6f; continue; }   // a gap where stock sold out
                        Material m = rng.NextDouble() < 0.7 ? shelfMat : prod[rng.Next(prod.Length)];
                        Box(t, $"Item_{side}_{s}_{k}", new Vector3(x + w * 0.5f, y + 0.0125f + h * 0.5f, side * (0.04f + d * 0.5f + 0.02f)), new Vector3(w * 0.96f, h, d), m);
                        if (w > 0.15f && h > 0.13f && rng.NextDouble() < 0.9)   // printed box art (review 1: flat coloured boxes)
                            ProductFace(t, p, $"ItemArt_{side}_{s}_{k}", new Vector3(x + w * 0.5f, y + 0.0125f + h * 0.5f, side * (0.06f + d)), new Vector2(w * 0.8f, h * 0.8f), side > 0, rng.Next(4));
                        else   // round 4: no blank boxes among the printed stock (no rng draw: same layout)
                            ProductFace(t, p, $"ItemArt_{side}_{s}_{k}", new Vector3(x + w * 0.5f, y + 0.0125f + h * 0.5f, side * (0.06f + d)), new Vector2(w * 0.8f, h * 0.75f), side > 0, (s * 5 + k + (side > 0 ? 2 : 0)) % 4);
                        k++;
                        x += w + 0.015f;
                    }
                }
            }
        }

        private static readonly string[] ProductArt = { "park_logo", "raptor_poster", "rex_banner", "map_park_modern" };

        /// <summary>Printed face (box art / wrapper) on the front of a product box. localPos = centre of the front face in the
        /// group's space; backSide = the face looks along local +Z instead of -Z.</summary>
        private static void ProductFace(Transform t, WorldMaterialPalette p, string name, Vector3 localPos, Vector2 size, bool backSide, int art)
        {
            string stem = ProductArt[art % ProductArt.Length];
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(t, false);
            quad.transform.localPosition = localPos + new Vector3(0f, 0f, backSide ? 0.002f : -0.002f);
            quad.transform.localRotation = Quaternion.Euler(0f, backSide ? 180f : 0f, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = p.CreateTextureQuadMaterial($"MAT_Z1_Print_{stem}", stem, false, 1f, Color.white);
            quad.layer = GameLayers.Environment;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
        }

        /// <summary>Single-sided wall shelving bay (back panel + shelves + stock). floorPos = front-centre at the wall; yaw so the front faces the room.</summary>
        private static void WallShelf(Transform parent, WorldMaterialPalette p, string name, Vector3 floorPos, float yaw, float length, int seed, int levels = 4)
        {
            Transform t = Group(parent, name, floorPos, yaw);
            BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 1.0f, 0.2f); bc.size = new Vector3(length, 2.0f, 0.42f);
            Box(t, "Back", new Vector3(0f, 1.05f, 0.38f), new Vector3(length, 2.1f, 0.03f), mCabinet);
            Box(t, "SideA", new Vector3(-length * 0.5f, 1.05f, 0.2f), new Vector3(0.03f, 2.1f, 0.4f), mCabinet);
            Box(t, "SideB", new Vector3(length * 0.5f, 1.05f, 0.2f), new Vector3(0.03f, 2.1f, 0.4f), mCabinet);
            Box(t, "Kick", new Vector3(0f, 0.05f, 0.2f), new Vector3(length, 0.10f, 0.38f), mBlack);
            var rng = new System.Random(seed);
            Material[] prod = { mVendRed, mBinderBlue, mFeatureGreen, p.PaintedMetalYellow, mPaper, mBinderBlack };
            for (int s = 0; s < levels; s++)
            {
                float y = 0.10f + s * (1.9f / levels);
                Box(t, $"Shelf_{s}", new Vector3(0f, y, 0.2f), new Vector3(length - 0.04f, 0.025f, 0.38f), mCabinet);
                float x = -length * 0.5f + 0.05f;
                int k = 0;
                Material shelfMat = prod[rng.Next(prod.Length)];
                while (true)
                {
                    float w = 0.14f + (float)rng.NextDouble() * 0.2f;
                    if (x + w > length * 0.5f - 0.05f) break;
                    float h = 0.10f + (float)rng.NextDouble() * 0.22f;
                    if (rng.NextDouble() < 0.1) { x += w * 0.6f; continue; }
                    Material m = rng.NextDouble() < 0.7 ? shelfMat : prod[rng.Next(prod.Length)];
                    Box(t, $"Item_{s}_{k}", new Vector3(x + w * 0.5f, y + 0.0125f + h * 0.5f, 0.22f), new Vector3(w * 0.95f, h, 0.28f), m);
                    if (w > 0.15f && h > 0.12f && rng.NextDouble() < 0.9)
                        ProductFace(t, p, $"ItemArt_{s}_{k}", new Vector3(x + w * 0.5f, y + 0.0125f + h * 0.5f, 0.08f), new Vector2(w * 0.8f, h * 0.8f), false, rng.Next(4));
                    else   // round 4 (review 3: a blank cream box among printed stock): small items get their print too (no rng draw: same layout)
                        ProductFace(t, p, $"ItemArt_{s}_{k}", new Vector3(x + w * 0.5f, y + 0.0125f + h * 0.5f, 0.08f), new Vector2(w * 0.8f, h * 0.75f), false, (s * 7 + k) % 4);
                    k++;
                    x += w + 0.015f;
                }
            }
        }

        // =========================================================================
        // LOBBY ATRIUM (X: -50 .. -12, Z: -32 .. +19, H: 11.0 m)
        // POS-11 FIX: Aligned skylight roof X=-50..-12, ZERO west gap / east overhang / east wall slit!
        // Real staircase to Mezzanine, dramatic museum uplights on trex_skeleton!
        // =========================================================================

        private static void BuildLobbyAtrium(Transform parent, WorldMaterialPalette p)
        {
            var lobby = new GameObject("LobbyAtrium");
            lobby.transform.SetParent(parent, false);

            // Wet polished atrium floor slab (38 x 51 m) — ceiling built via aligned BuildCeilingWithSkylight!
            WorldArchitectureKit.BuildBoxRoomShell(lobby.transform, "Lobby_Floor", new Vector3(-31f, 0f, -6.5f), 0f, 38f, 51f, LobbyH, p.ConcreteFloorWet, p.ConcreteBoardform, SurfaceType.Concrete, p, buildCeiling: false, addRainBlocker: false);

            // POS-11 FIX: Exact 38 m x 51 m steel-truss skylight aligned strictly to X = -50..-12, Z = -32..+19!
            var brokenPanes = new HashSet<int> { 18, 25, 26 };
            WorldArchitectureKit.BuildCeilingWithSkylight(lobby.transform, "Lobby_SkylightRoof", new Vector3(-31f, 0f, -6.5f), 38f, 51f, LobbyH, baysX: 6, baysZ: 8, palette: p, brokenPaneIndices: brokenPanes);

            // West Atrium Wall (X = -50, Z = -32 -> +19, len = 51) — Staff Exit Archway at Z = -10 (CenterAlong = 22.0 m)
            WorldArchitectureKit.BuildWallWithOpenings(
                lobby.transform,
                new Vector3(-50f, 0f, -32f),
                new Vector3(-50f, 0f, 19f),
                0f, LobbyH, 0.30f, p.ConcreteBoardform,
                new[]
                {
                    OpeningSpec.Window(10.0f, 12.0f, 0.1f, 2.7f, mullions: 4, glassMat: mGlass),   // gift shop shopfront (glazed, not walkable)
                    OpeningSpec.Arch(22.0f, 2.0f, 2.4f)
                },
                addSkirting: true, palette: p, wallName: "Wall_Lobby_West");

            // East Atrium Wall (X = -12, Z = -32 -> +19, len = 51) — Full 11.0 m continuous seal (ZERO 0.4 m light leak slit!)
            WorldArchitectureKit.BuildWallWithOpenings(
                lobby.transform,
                new Vector3(-12f, 0f, -32f),
                new Vector3(-12f, 0f, 19f),
                0f, LobbyH, 0.30f, p.ConcreteBoardform, null,
                addSkirting: true, palette: p, wallName: "Wall_Lobby_East");

            // Upper North Atrium Wall above Staff Corridor (Z = +19, Y = 3.25 -> 11.0, X = -50 -> -12)
            WorldArchitectureKit.BuildWallWithOpenings(
                lobby.transform,
                new Vector3(-50f, 0f, 19f),
                new Vector3(-12f, 0f, 19f),
                CeilCorr, LobbyH - CeilCorr, 0.28f, p.ConcreteBoardform, null,
                palette: p, wallName: "Wall_Lobby_NorthUpper");
            // The corridor wall's lobby face gets boardform too, flush with the upper wall (no painted band under the concrete)
            Liner(lobby.transform, "Lining_Lobby_NorthLower", new Vector3(-50f, 0f, 19f), new Vector3(-12f, 0f, 19f), Vector3.back, 0.11f, CeilCorr, p.ConcreteBoardform, new[] { (17.8f, 20.2f, 0f, 2.4f) }, thickness: 0.03f);

            // South 11 m Glass Facade (Z = -32, X = -50 -> -12, len = 38) — Entrance shutter bay at X = -32 (CenterAlong = 18.0 m, W = 6 m)
            WorldArchitectureKit.BuildWallWithOpenings(
                lobby.transform,
                new Vector3(-50f, 0f, -32f),
                new Vector3(-12f, 0f, -32f),
                0f, LobbyH, 0.30f, p.ConcreteBoardform,
                new[]
                {
                    OpeningSpec.Window(7.5f, 13.0f, 0.3f, 9.8f, mullions: 4, glassMat: mGlass),
                    OpeningSpec.Arch(18.0f, 6.0f, 4.0f), // Main Entrance Shutter Bay
                    OpeningSpec.Window(29.5f, 13.0f, 0.3f, 9.8f, mullions: 4, glassMat: mGlass)
                },
                palette: p, wallName: "Wall_Lobby_SouthGlassFacade", includeInOcclusion: false);

            // Jammed Blackout Entrance Security Shutter (P1-10 FIX: WorldAnchor attached DIRECTLY to shutter so Intro hides it!)
            GameObject shutter = WorldArchitectureKit.CreateBox(lobby.transform, "Lobby_SecurityShutter", new Vector3(-32f, 2.0f, -31.65f), new Vector3(6.0f, 4.0f, 0.14f), mShutterPaint, addCollider: true);
            AttachNamedWorldAnchor(shutter, "lobby_shutter_closed");
            WorldArchitectureKit.BuildWorldAnchor(lobby.transform, "lobby_shutter_look", new Vector3(-32f, 2.0f, -31.6f));
            WorldArchitectureKit.BuildWorldAnchor(lobby.transform, "shutter_motor", new Vector3(-28.6f, 3.6f, -31.4f));
            BuildSimpleInteractableBox(lobby.transform, "Interact_ShutterControl", new Vector3(-28.8f, 1.35f, -31.2f), new Vector3(1.2f, 1.6f, 0.9f),
                "shutter_control", "Try entrance shutter control {interact}", holdSec: 0f, singleUse: false);
            // Shutter hardware that stays when the shutter is raised: head box with motor, side guide rails, control box + conduit
            Box(lobby.transform, "Shutter_HeadBox", new Vector3(-32f, 4.28f, -31.6f), new Vector3(6.6f, 0.56f, 0.46f), mDarkMetal);
            Box(lobby.transform, "Shutter_Motor", new Vector3(-28.75f, 4.22f, -31.45f), new Vector3(0.5f, 0.42f, 0.42f), mBlack);
            Box(lobby.transform, "Shutter_GuideW", new Vector3(-35.04f, 2.0f, -31.7f), new Vector3(0.09f, 4.0f, 0.16f), mDarkMetal);
            Box(lobby.transform, "Shutter_GuideE", new Vector3(-28.96f, 2.0f, -31.7f), new Vector3(0.09f, 4.0f, 0.16f), mDarkMetal);
            p.InstantiatePropOrFallback("gate_control_box", lobby.transform, new Vector3(-28.15f, 0f, -31.55f), new Vector3(0f, 0f, 0f), new Vector3(0.55f, 1.35f, 0.4f), grounded: true, addCollider: true);
            WorldArchitectureKit.BuildConduitRun(lobby.transform, "Shutter_ControlConduit", new[] { new Vector3(-28.15f, 1.35f, -31.8f), new Vector3(-28.15f, 4.05f, -31.8f), new Vector3(-28.55f, 4.05f, -31.8f) }, 0.025f, mDarkMetal);
            WorldArchitectureKit.BuildWorldAnchor(lobby.transform, "lobby_shutter_sparks", new Vector3(-28.6f, 3.6f, -31.4f));
            WallDecal(lobby.transform, "Shutter_CautionStripe", p.DecalCautionStripes, new Vector3(-32f, 0.05f, -31.3f), Vector3.up, new Vector2(6.0f, 0.5f), 0.7f);

            // North Mezzanine Balcony (Y = 4.6, Z = +14.8..+18.8) + REAL Structural Staircase!
            GameObject mezz = WorldArchitectureKit.CreateBox(lobby.transform, "Lobby_MezzanineDeck", new Vector3(-31f, 4.48f, 16.8f), new Vector3(37.4f, 0.24f, 4.2f), p.ConcreteBoardform, addCollider: true);
            WorldArchitectureKit.ApplySurface(mezz, SurfaceType.Concrete, p);
            // Balustrade in two runs, open at the stair head (X -17.95..-15.05): posts every ~2 m, glass panels, steel cap
            GlassBalustrade(lobby.transform, p, "Mezz_RailWest", -49.6f, -17.95f, 14.78f, 4.6f);
            GlassBalustrade(lobby.transform, p, "Mezz_RailEast", -15.05f, -12.4f, 14.78f, 4.6f);
            Box(lobby.transform, "Mezz_Fascia", new Vector3(-31f, 4.36f, 14.66f), new Vector3(37.4f, 0.5f, 0.05f), mDarkMetal);
            float[] mezzCols = { -46.0f, -39.0f, -23.0f, -19.6f };
            for (int c = 0; c < mezzCols.Length; c++)
                Box(lobby.transform, $"Mezz_Column_{c}", new Vector3(mezzCols[c], 2.18f, 15.05f), new Vector3(0.36f, 4.36f, 0.36f), p.ConcreteBoardform, true);
            // Soffit under the stair flight: a sloped slab tucked under the step corners (no floating stepped blocks)
            float stairSlope = Mathf.Atan2(4.6f, 9.4f) * Mathf.Rad2Deg;
            Box(lobby.transform, "Stair_Soffit", new Vector3(-16.5f, 2.3f - 0.115f, 5.2f + 4.7f), new Vector3(2.6f, 0.2f, Mathf.Sqrt(4.6f * 4.6f + 9.4f * 9.4f)), p.ConcreteBoardform, false, new Vector3(-stairSlope, 0f, 0f));
            // Mezzanine: benches facing the atrium, two planters, an exhibit panel
            for (int b = 0; b < 3; b++) Bench(lobby.transform, $"Mezz_Bench_{b}", new Vector3(-44f + b * 9f, 4.6f, 16.3f), 0f, 2.2f);
            p.InstantiatePropOrFallback("planter_concrete", lobby.transform, new Vector3(-48.6f, 4.6f, 17.6f), Vector3.zero, new Vector3(1.1f, 1.1f, 1.1f));
            p.InstantiatePropOrFallback("planter_concrete", lobby.transform, new Vector3(-21.5f, 4.6f, 17.6f), Vector3.zero, new Vector3(1.1f, 1.1f, 1.1f));
            WorldArchitectureKit.BuildStaircase(lobby.transform, "Lobby_MezzanineStairs", new Vector3(-16.5f, 0f, 5.2f), yawDeg: 0f, width: 2.6f, totalRise: 4.6f, totalRun: 9.4f, stepCount: 22, palette: p);

            // Centerpiece: 12 m T. rex Fossil Cast on Beveled Dark Granite Plinth + 4 Dramatic Museum Uplights!
            WorldArchitectureKit.CreateBox(lobby.transform, "TrexFossil_PlinthBase", new Vector3(-31f, 0.30f, -5f), new Vector3(4.8f, 0.60f, 12.6f), p.DarkGranitePlinth, addCollider: true);
            WorldArchitectureKit.CreateBox(lobby.transform, "TrexFossil_PlinthTrim", new Vector3(-31f, 0.62f, -5f), new Vector3(4.5f, 0.08f, 12.3f), p.BrushedSteel, addCollider: false);
            p.InstantiatePropOrFallback("trex_skeleton", lobby.transform, new Vector3(-31f, 0.66f, -5f), new Vector3(0f, 180f, 0f), new Vector3(3.4f, 4.8f, 11.8f), grounded: true, addCollider: false, fallbackMat: p.PlasterClean, customName: "Hero_TRexSkeleton");

            Vector3[] uplightOffsets = { new(-33.4f, 0.72f, -9.8f), new(-28.6f, 0.72f, -9.8f), new(-33.4f, 0.72f, -0.2f), new(-28.6f, 0.72f, -0.2f) };
            for (int u = 0; u < uplightOffsets.Length; u++)
            {
                Vector3 dir = (new Vector3(-31f, 3.6f, -5f) - uplightOffsets[u]).normalized;
                // P1-10 FIX: 14000 lm under GoldenDay (EV100=11.5) while blackoutBackupFactor=0.045 keeps ~630 lm dramatic museum silhouette at night!
                WorldArchitectureKit.BuildHdrpLight(lobby.transform, $"Light_FossilUplight_{u}", uplightOffsets[u], Quaternion.LookRotation(dir).eulerAngles,
                    LightType.Spot, new Color(1.0f, 0.86f, 0.62f), 14000f, 14f, LightGroupId.VCMain, volumetricDimmer: 1.2f, castShadows: u < 2, spotOuterAngle: 56f, colorTempK: 3600f, blackoutBackupFactor: 0.045f);
            }

            // Reception Desk, Modern Atrium Benches & Planters (kept cleanly away from Staff Exit archway!)
            BuildReceptionDesk(lobby.transform, p, new Vector3(-42.0f, 0f, -25.0f));
            // Benches around the fossil, bins, big banners and the park logo high on the walls
            Bench(lobby.transform, "Lobby_Bench_W", new Vector3(-36.2f, 0f, -5.0f), 90f, 2.6f);
            Bench(lobby.transform, "Lobby_Bench_E", new Vector3(-25.8f, 0f, -5.0f), 90f, 2.6f);
            Bench(lobby.transform, "Lobby_Bench_S", new Vector3(-38.6f, 0f, -17.0f), 30f, 2.6f);   // off the entrance axis
            p.InstantiatePropOrFallback("trash_bin", lobby.transform, new Vector3(-37.6f, 0f, -14.2f), Vector3.zero, new Vector3(0.45f, 0.75f, 0.45f));
            p.InstantiatePropOrFallback("trash_bin", lobby.transform, new Vector3(-13.0f, 0f, 12.0f), Vector3.zero, new Vector3(0.45f, 0.75f, 0.45f));
            Poster(lobby.transform, p, "Lobby_Banner_RexE", "rex_banner", new Vector3(-12.15f, 6.8f, -12.0f), Vector3.left, new Vector2(2.6f, 5.2f));
            Poster(lobby.transform, p, "Lobby_Banner_RaptorE", "raptor_poster", new Vector3(-12.15f, 6.6f, -2.0f), Vector3.left, new Vector2(2.6f, 3.7f));
            Poster(lobby.transform, p, "Lobby_ParkLogoN", "park_logo", new Vector3(-31f, 7.6f, 18.86f), Vector3.back, new Vector2(3.6f, 3.6f), frame: false);
            Poster(lobby.transform, p, "Lobby_GiftShopSign", "lobby_welcome", new Vector3(-49.85f, 3.35f, -22.0f), Vector3.right, new Vector2(3.2f, 0.8f), frame: false);
            DecalAt(lobby.transform, "Lobby_FloorWear_Arch", p.DecalGrimeLeak, new Vector3(-31f, 0.02f, 16.5f), 3.0f, 0f, 0.45f);
            p.InstantiatePropOrFallback("planter_concrete", lobby.transform, new Vector3(-44.5f, 0f, -4.5f), Vector3.zero, new Vector3(1.6f, 0.85f, 1.6f));
            p.InstantiatePropOrFallback("planter_concrete", lobby.transform, new Vector3(-17.5f, 0f, -4.5f), Vector3.zero, new Vector3(1.6f, 0.85f, 1.6f));
            p.InstantiatePropOrFallback("planter_concrete", lobby.transform, new Vector3(-44.5f, 0f, -20.0f), Vector3.zero, new Vector3(1.6f, 0.85f, 1.6f));
            p.InstantiatePropOrFallback("planter_concrete", lobby.transform, new Vector3(-17.5f, 0f, -20.0f), Vector3.zero, new Vector3(1.6f, 0.85f, 1.6f));

            // Blackout Storm Damage Group (P1-10 FIX: WorldAnchor attached DIRECTLY to dmg so daytime Intro hides storm debris!)
            var dmg = new GameObject("Lobby_BlackoutStormDamage");
            dmg.transform.SetParent(lobby.transform, false);
            dmg.transform.position = new Vector3(-31f, 0f, -12f);
            AttachNamedWorldAnchor(dmg, "lobby_damage");
            p.InstantiatePropOrFallback("utility_cart", dmg.transform, new Vector3(-24.5f, 0f, -12.5f), new Vector3(0f, 38f, 82f), new Vector3(1.2f, 0.9f, 0.7f));
            p.InstantiatePropOrFallback("toolbox_case", dmg.transform, new Vector3(-26.0f, 0f, -14.0f), new Vector3(0f, -25f, 0f), new Vector3(0.55f, 0.30f, 0.35f));
            // Puddles straight under the three broken skylight panes (bays centred at the coordinates below), glass on the floor
            // Standing water: the polished wet floor already runs smoothness 0.62-0.97, so a wet decal adds no contrast and the
            // palette's puddle mask is a tiling noise (square edges). Each puddle is a few overlapping flat ellipses of dark,
            // mirror-smooth water (3 mm proud of the floor) with a grime ring round it.
            StandingWater(dmg.transform, p, "Lobby_SkylightPuddle_1", new Vector3(-34.2f, 0f, -16.1f), 2.0f, 11);
            StandingWater(dmg.transform, p, "Lobby_SkylightPuddle_2", new Vector3(-27.8f, 0f, -16.1f), 1.8f, 23);
            StandingWater(dmg.transform, p, "Lobby_SkylightPuddle_3", new Vector3(-27.8f, 0f, -22.4f), 2.1f, 37);
            WorldArchitectureKit.BuildDecal(dmg.transform, "Lobby_GlassCracks_1", new Vector3(-34.0f, 0.05f, -15.6f), new Vector3(90f, 40f, 0f), new Vector3(2.4f, 2.4f, 0.4f), p.DecalCracks, 0.6f);
            WorldArchitectureKit.BuildDecal(dmg.transform, "Lobby_GlassCracks_2", new Vector3(-28.2f, 0.05f, -22.0f), new Vector3(90f, 160f, 0f), new Vector3(2.2f, 2.2f, 0.4f), p.DecalCracks, 0.6f);
            for (int g = 0; g < 10; g++)
            {
                float gx = (g < 5 ? -34.2f : -27.8f) + Mathf.Sin(g * 2.3f) * 1.4f;
                float gz = (g < 5 ? -16.1f : (g < 8 ? -16.1f : -22.4f)) + Mathf.Cos(g * 1.7f) * 1.3f;
                Box(dmg.transform, $"GlassShard_{g}", new Vector3(gx + 31f, 0.006f, gz + 12f),   // dmg sits at (-31,0,-12); Box is local
                    new Vector3(0.18f + (g % 3) * 0.07f, 0.008f, 0.12f + (g % 2) * 0.09f), mGlass, false, new Vector3(0f, g * 37f, 0f));
            }

            // Illuminated Wayfinding Signs inside Lobby (UX-5 FIX!)
            WorldArchitectureKit.BuildIlluminatedSign(lobby.transform, "Sign_ToCorridor", new Vector3(-31f, 2.8f, 18.8f), Vector3.zero, new Vector2(2.2f, 0.34f), p.EmissiveGreen, LightGroupId.VCExit);
            WorldArchitectureKit.BuildIlluminatedSign(lobby.transform, "Sign_ToStaffExit", new Vector3(-49.8f, 2.8f, -10f), new Vector3(0f, 90f, 0f), new Vector2(1.8f, 0.32f), p.EmissiveGreen, LightGroupId.VCExit);

            // Atrium Main (P1-10 boosted for crisp daytime GoldenDay exposure!) + Emergency Lighting + Story Trigger + ReflectionProbe
            WorldArchitectureKit.BuildHdrpLight(lobby.transform, "Light_Lobby_MainN", new Vector3(-31f, 9.8f, 6f), new Vector3(90f, 0f, 0f), LightType.Spot, new Color(1.0f, 0.94f, 0.84f), 58000f, 28f, LightGroupId.VCMain, volumetricDimmer: 0.65f, spotOuterAngle: 105f);
            WorldArchitectureKit.BuildHdrpLight(lobby.transform, "Light_Lobby_MainS", new Vector3(-31f, 9.8f, -18f), new Vector3(90f, 0f, 0f), LightType.Spot, new Color(1.0f, 0.94f, 0.84f), 58000f, 28f, LightGroupId.VCMain, volumetricDimmer: 0.65f, spotOuterAngle: 105f);
            WorldArchitectureKit.BuildHdrpLight(lobby.transform, "Light_Lobby_SkylightDayFill", new Vector3(-31f, 9.6f, -5f), Vector3.zero, LightType.Point, new Color(1.0f, 0.96f, 0.88f), 85000f, 34f, LightGroupId.VCMain, volumetricDimmer: 0.45f);
            // Review 1: with the power on the atrium already looked blacked out (only a floor pool and the fossil). The
            // showpiece room gets architectural light that the blackout can take away (all VCMain, shadowless):
            // wall grazers under the roof washing the long concrete walls, downlights in the mezzanine soffit, accent
            // spots on the banners, the park logo and the reception desk.
            float[] eastZ = { -26f, -16f, -6f, 4f, 13f };
            for (int i = 0; i < eastZ.Length; i++) Grazer(lobby.transform, $"Light_Lobby_Grazer_E{i}", new Vector3(-12.75f, 10.3f, eastZ[i]), Vector3.right);
            float[] westZ = { -16.5f, -4f, 7f };
            for (int i = 0; i < westZ.Length; i++) Grazer(lobby.transform, $"Light_Lobby_Grazer_W{i}", new Vector3(-49.25f, 10.3f, westZ[i]), Vector3.left);
            float[] northX = { -43f, -31f, -19f };
            for (int i = 0; i < northX.Length; i++) Grazer(lobby.transform, $"Light_Lobby_Grazer_N{i}", new Vector3(northX[i], 10.3f, 18.35f), Vector3.forward);
            for (int i = 0; i < 7; i++)
                WorldArchitectureKit.BuildHdrpLight(lobby.transform, $"Light_Lobby_MezzDown_{i}", new Vector3(-46f + i * 5f, 4.30f, 16.6f), new Vector3(90f, 0f, 0f), LightType.Spot, Color.white, 700f, 7f, LightGroupId.VCMain, volumetricDimmer: 0f, castShadows: false, spotOuterAngle: 120f, colorTempK: 3200f);
            Accent(lobby.transform, "Light_Lobby_Accent_RexE", new Vector3(-17.5f, 10.4f, -12f), new Vector3(-12.15f, 6.8f, -12f));
            Accent(lobby.transform, "Light_Lobby_Accent_RaptorE", new Vector3(-17.5f, 10.4f, -2f), new Vector3(-12.15f, 6.6f, -2f));
            Accent(lobby.transform, "Light_Lobby_Accent_LogoN", new Vector3(-31f, 10.4f, 12.5f), new Vector3(-31f, 7.6f, 18.86f));
            Accent(lobby.transform, "Light_Lobby_Accent_Reception", new Vector3(-42f, 10.4f, -20.5f), new Vector3(-42f, 0.9f, -25f));
            Accent(lobby.transform, "Light_Lobby_Accent_GiftSign", new Vector3(-45f, 10.4f, -22f), new Vector3(-49.85f, 3.35f, -22f));
            // reception task lights: two pendants over the desk (DeskPendant bodies)
            for (int i = 0; i < 2; i++)
                WorldArchitectureKit.BuildHdrpLight(lobby.transform, $"Light_Lobby_ReceptionTask_{i}", new Vector3(-43f + i * 2f, 2.6f, -25f), new Vector3(90f, 0f, 0f), LightType.Spot, Color.white, 300f, 4f, LightGroupId.VCMain, volumetricDimmer: 0f, castShadows: false, spotOuterAngle: 100f, colorTempK: 3000f);

            // Blackout: warm battery units high on the walls; one small red pulsing accent at the jammed shutter control
            BatteryLight(lobby.transform, "Lobby", 0, new Vector3(-31f, 4.2f, 15.4f));
            BatteryLight(lobby.transform, "Lobby", 1, new Vector3(-48.6f, 5.0f, -10f));
            BatteryLight(lobby.transform, "Lobby", 2, new Vector3(-13.4f, 5.0f, -4f));
            BatteryLight(lobby.transform, "Lobby", 3, new Vector3(-36.8f, 4.9f, -30.6f));
            // round 4: moonlight through the glass roof with the power on (the lit lobby read one amber-brown): a cool 7000 K
            // spot high over the roof from the moon's side (east-south-east), steeper than the moon so it reaches the floor
            // over the east wall; its cached shadow draws the roof beams on the floor (LookLights.Zone1: Light_Lobby_MoonFill)
            {
                Vector3 moonDir = new Vector3(0.453f, 0.866f, -0.211f).normalized;
                Vector3 floorAim = new Vector3(-31f, 0f, -7f);
                WorldArchitectureKit.BuildHdrpLight(lobby.transform, "Light_Lobby_MoonFill", floorAim + moonDir * 26f, Quaternion.LookRotation(-moonDir).eulerAngles, LightType.Spot, Color.white, 7000f, 36f, LightGroupId.VCMain, volumetricDimmer: 0f, castShadows: true, spotOuterAngle: 110f, colorTempK: 7000f);
            }
            WorldArchitectureKit.BuildHdrpLight(lobby.transform, "Light_Lobby_Emerg", new Vector3(-28.4f, 3.0f, -31.2f), new Vector3(90f, 0f, 0f), LightType.Spot, new Color(1.0f, 0.12f, 0.08f), 140f, 7f, LightGroupId.VCEmergency, volumetricDimmer: 0.8f, spotOuterAngle: 150f, pulsingEmergency: true);

            WorldArchitectureKit.BuildStoryTrigger(lobby.transform, "lobby_beat", new Vector3(-31f, 2.0f, -6.5f), new Vector3(37.0f, 4.0f, 50.0f), oneShot: true);
            WorldArchitectureKit.BuildRoomReflectionProbe(lobby.transform, "LobbyAtrium", new Vector3(-31f, 4.5f, -6.5f), new Vector3(37.6f, 10.8f, 50.6f), "vc_lobby");
        }

        /// <summary>Lobby wall washer: a spot head hung 4 m off the wall under the roof, aimed at the wall's 5 m line with a
        /// 150 deg cone, so each head fills ~12 m of wall and the walls between heads are lit too (at 0.6 m it grazed: hot
        /// ovals on a dark wall, luma 40-50 between them)
        /// (rule ^Light_Lobby_Grazer). pos = the point on the wall line under the roof; towardsWall = room -> wall.</summary>
        private static void Grazer(Transform parent, string name, Vector3 pos, Vector3 towardsWall)
        {
            Vector3 head = pos - towardsWall * 3.4f;          // callers pass a point 0.6 m off the wall: the head hangs 4 m off it
            Vector3 target = new Vector3(pos.x, 5.0f, pos.z) + towardsWall * 0.6f;
            Vector3 aim = (target - head).normalized;
            pos = head;
            WorldArchitectureKit.BuildHdrpLight(parent, name, pos, Quaternion.LookRotation(aim, towardsWall).eulerAngles, LightType.Spot, Color.white, 2600f, 16f, LightGroupId.VCMain, volumetricDimmer: 0.15f, castShadows: false, spotOuterAngle: 100f, colorTempK: 3400f);
        }

        /// <summary>Accent spot hung from the skylight truss, aimed at a target (rule ^Light_Lobby_Accent).</summary>
        private static void Accent(Transform parent, string name, Vector3 pos, Vector3 target)
        {
            WorldArchitectureKit.BuildHdrpLight(parent, name, pos, Quaternion.LookRotation((target - pos).normalized).eulerAngles, LightType.Spot, Color.white, 3200f, 18f, LightGroupId.VCMain, volumetricDimmer: 0.2f, castShadows: false, spotOuterAngle: 34f, colorTempK: 3300f);
        }

        /// <summary>A puddle of standing water: 4 overlapping flat ellipses (dark, smoothness 0.97, no collider, no shadow) round
        /// floorCentre, plus a grime ring decal. radius = rough outer radius in metres.</summary>
        private static void StandingWater(Transform parent, WorldMaterialPalette p, string name, Vector3 floorCentre, float radius, int seed)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(floorCentre.x, 0f, floorCentre.z);
            root.layer = GameLayers.Environment;
            var rng = new System.Random(seed);
            for (int i = 0; i < 4; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = i == 0 ? 0f : radius * (0.35f + 0.25f * (float)rng.NextDouble());
                float sx = radius * (i == 0 ? 1.15f : 0.55f + 0.35f * (float)rng.NextDouble());
                float sz = sx * (0.55f + 0.3f * (float)rng.NextDouble());
                GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                c.name = $"Water_{i}";
                c.transform.SetParent(root.transform, false);
                c.transform.localPosition = new Vector3(Mathf.Cos(a) * r, 0.002f + i * 0.0004f, Mathf.Sin(a) * r);
                c.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f);
                c.transform.localScale = new Vector3(sx * 2f, 0.002f, sz * 2f);
                Object.DestroyImmediate(c.GetComponent<Collider>());
                MeshRenderer mr = c.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mWater;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                c.layer = GameLayers.Environment;
            }
            DecalAt(root.transform, name + "_GrimeRing", p.DecalGrimeLeak, new Vector3(floorCentre.x, 0.05f, floorCentre.z), radius * 2.9f, seed * 31f, 0.8f);
        }

        /// <summary>Glass balustrade along X at z: steel posts every ~2 m, glass panels between, steel cap rail. Colliders on the panels.</summary>
        private static void GlassBalustrade(Transform parent, WorldMaterialPalette p, string name, float x0, float x1, float z, float deckY)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            float len = x1 - x0;
            int bays = Mathf.Max(1, Mathf.RoundToInt(len / 2.0f));
            float bay = len / bays;
            for (int i = 0; i <= bays; i++)
                Box(root.transform, $"Post_{i}", new Vector3(x0 + i * bay, deckY + 0.53f, z), new Vector3(0.06f, 1.06f, 0.06f), mSteel);
            for (int i = 0; i < bays; i++)
            {
                GameObject pane = Box(root.transform, $"Glass_{i}", new Vector3(x0 + (i + 0.5f) * bay, deckY + 0.52f, z), new Vector3(bay - 0.1f, 0.88f, 0.02f), mGlass, true);
                MeshRenderer mr = pane.GetComponent<MeshRenderer>();
                if (mr != null) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Box(root.transform, "Cap", new Vector3((x0 + x1) * 0.5f, deckY + 1.08f, z), new Vector3(len + 0.06f, 0.05f, 0.08f), mSteel, true);
        }

        /// <summary>Lobby reception desk: dark plinth, green front panel with the park logo, raised transaction ledge, work top, monitors.</summary>
        private static void BuildReceptionDesk(Transform parent, WorldMaterialPalette p, Vector3 floorCentre)
        {
            Transform t = Group(parent, "ReceptionDesk", floorCentre, 0f);
            BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.6f, 0f); bc.size = new Vector3(6.2f, 1.2f, 1.1f);
            Box(t, "Plinth", new Vector3(0f, 0.05f, 0.04f), new Vector3(6.1f, 0.10f, 1.0f), mBlack);
            Box(t, "FrontPanel", new Vector3(0f, 0.6f, -0.46f), new Vector3(6.2f, 1.0f, 0.06f), mFeatureGreen);
            Box(t, "SideW", new Vector3(-3.07f, 0.55f, 0f), new Vector3(0.06f, 1.0f, 1.0f), mFeatureGreen);
            Box(t, "SideE", new Vector3(3.07f, 0.55f, 0f), new Vector3(0.06f, 1.0f, 1.0f), mFeatureGreen);
            Box(t, "Ledge", new Vector3(0f, 1.14f, -0.38f), new Vector3(6.3f, 0.04f, 0.34f), p.DarkGranitePlinth);
            Box(t, "LedgeUpstand", new Vector3(0f, 1.0f, -0.3f), new Vector3(6.1f, 0.26f, 0.04f), mLaminate);
            Box(t, "WorkTop", new Vector3(0f, 0.76f, 0.15f), new Vector3(6.0f, 0.04f, 0.7f), mLaminate);
            Box(t, "SteelKick", new Vector3(0f, 0.16f, -0.495f), new Vector3(6.2f, 0.12f, 0.01f), mSteel);
            GameObject logo = Poster(t, p, "FrontLogo", "park_logo", floorCentre + new Vector3(0f, 0.62f, -0.49f), Vector3.back, new Vector2(0.8f, 0.8f), frame: false);
            logo.transform.SetParent(t, true);
            for (int m = 0; m < 2; m++)
            {
                p.InstantiatePropOrFallback("lcd_monitor", t, floorCentre + new Vector3(-1.4f + m * 2.6f, 0.78f, 0.2f), new Vector3(0f, 0f, 0f), new Vector3(0.58f, 0.44f, 0.20f), grounded: true, addCollider: false);
                Box(t, $"Keyboard_{m}", new Vector3(-1.4f + m * 2.6f, 0.79f, 0.42f), new Vector3(0.44f, 0.02f, 0.15f), mBlack);
                Chair(t.parent, $"ReceptionChair_{m}", floorCentre + new Vector3(-1.4f + m * 2.6f, 0f, 1.05f), m == 0 ? 10f : -14f);
            }
            Box(t, "Phone", new Vector3(0.1f, 0.81f, 0.25f), new Vector3(0.2f, 0.07f, 0.22f), mBlack);
            for (int b = 0; b < 3; b++) Box(t, $"Brochures_{b}", new Vector3(2.2f + b * 0.24f, 1.2f, -0.38f), new Vector3(0.2f, 0.08f, 0.26f), b == 1 ? mFeatureGreen : mPaper, false, new Vector3(0f, b * 7f, 0f));
            PaperStack(t, "VisitorLog", t.TransformPoint(new Vector3(-2.4f, 1.16f, -0.38f)), 6f, 2);
        }

        // =========================================================================
        // FEED PREP WING & COVERED LOADING DOCK (X: -12 .. +22, Z: -18 .. +19)
        // COL-1/DOOR-1 FIX: ZERO tile veneer behind door_wing!
        // COL-2/DOOR-2 FIX: ZERO tile veneer hiding dock_roller!
        // =========================================================================

        private static void BuildFeedPrepWingAndLoadingDock(Transform parent, WorldMaterialPalette p)
        {
            var wing = new GameObject("EastWing_FeedPrepAndDock");
            wing.transform.SetParent(parent, false);

            // Wet Ceramic Tile Floors (SurfaceType.Tile!) — P1-10 FIX: inset ceiling/RainBlocker away from Lobby East wall (X=-12) to eliminate mid-wall white bar!
            GameObject feedShell = WorldArchitectureKit.BuildBoxRoomShell(wing.transform, "Room_FeedPrepWing", new Vector3(-2f, 0f, 3.0f), 0f, 20f, 32f, CeilStd, p.FloorTileWet, p.PlasterClean, SurfaceType.Tile, p);
            TuckCeiling(feedShell);
            // White tile inside on the walls the wing shares with the lobby and the corridor
            Liner(wing.transform, "Lining_Wing_W", new Vector3(-12f, 0f, -13f), new Vector3(-12f, 0f, 19f), Vector3.right, 0.15f, CeilStd, p.LabTileWhite);
            Liner(wing.transform, "Lining_Wing_N", new Vector3(-12f, 0f, 19f), new Vector3(8f, 0f, 19f), Vector3.back, 0.11f, CeilCorr, p.LabTileWhite, new[] { (9.4f, 10.6f, 0f, 2.2f) }, thickness: 0.03f);
            Liner(wing.transform, "Lining_Wing_S", new Vector3(-12f, 0f, -13f), new Vector3(8f, 0f, -13f), Vector3.forward, WallT * 0.5f, CeilStd, p.LabTileWhite);
            Liner(wing.transform, "Lining_Wing_E", new Vector3(8f, 0f, -13f), new Vector3(8f, 0f, 19f), Vector3.left, WallT * 0.5f, CeilStd, p.LabTileWhite, new[] { (0.5f, 3.5f, 0f, 3.0f) });

            // Interior East Wing Partition Walls (all built via BuildWallWithOpenings — ZERO veneer overlays!)
            // Prep Hall <-> Cold Storage / Vet Clinic Wall (Z = +3, X = -12 -> +8, len = 20)
            WorldArchitectureKit.BuildWallWithOpenings(
                wing.transform,
                new Vector3(-12f, 0f, 3f),
                new Vector3(8f, 0f, 3f),
                0f, CeilStd, 0.22f, p.LabTileWhite,
                new[]
                {
                    OpeningSpec.Door(5.2f, 1.4f, 2.25f),  // door_cold (X = -6.8)
                    OpeningSpec.Door(15.0f, 1.4f, 2.25f)  // door_vet  (X = +3.0)
                },
                addSkirting: true, palette: p, wallName: "Wall_Prep_ColdVet");
            // Review 1 (12_ch3_feed_prep): both doorways were flat black rectangles. Steel frames on both faces, a leaf
            // standing open against the wall inside, and a battery unit in each room so there is a surface beyond.
            foreach (float dx in new[] { -6.8f, 3.0f })
            {
                Transform fr = Group(wing.transform, $"DoorFrame_{(dx < 0 ? "Cold" : "Vet")}", new Vector3(dx, 0f, 3f), 0f);
                for (int sgn = -1; sgn <= 1; sgn += 2)
                    Box(fr, $"Jamb_{(sgn < 0 ? "W" : "E")}", new Vector3(sgn * 0.73f, 1.15f, 0f), new Vector3(0.07f, 2.3f, 0.30f), mSteel);
                Box(fr, "Head", new Vector3(0f, 2.285f, 0f), new Vector3(1.53f, 0.07f, 0.30f), mSteel);
                Box(fr, "LeafOpen", new Vector3(-0.7f - 0.12f, 1.11f, -0.11f - 0.68f), new Vector3(1.36f, 2.2f, 0.045f), mCabinet, false, new Vector3(0f, 100f, 0f));
                Box(fr, "LeafKick", new Vector3(-0.7f - 0.12f + 0.004f, 0.16f, -0.11f - 0.68f), new Vector3(1.3f, 0.25f, 0.05f), mSteel, false, new Vector3(0f, 100f, 0f));
                Box(fr, "Vision", new Vector3(-0.7f - 0.12f - 0.003f, 1.5f, -0.11f - 0.68f), new Vector3(0.25f, 0.5f, 0.05f), mGlass, false, new Vector3(0f, 100f, 0f));
            }
            BatteryLight(wing.transform, "Cold", -1, new Vector3(-7.6f, CeilStd - 0.15f, -0.4f));
            BatteryLight(wing.transform, "Vet", -1, new Vector3(4.5f, CeilStd - 0.15f, -0.6f));

            // Cold Storage <-> Vet Clinic Divider (X = -2, Z = -9 -> +3, len = 12)
            WorldArchitectureKit.BuildWallWithOpenings(
                wing.transform,
                new Vector3(-2f, 0f, -9f),
                new Vector3(-2f, 0f, 3f),
                0f, CeilStd, 0.22f, p.LabTileWhite,
                new[] { OpeningSpec.Arch(6.0f, 1.2f, 2.25f) },
                addSkirting: true, palette: p, wallName: "Wall_Cold_Vet");

            // Cold/Vet <-> Wing South Corridor Wall (Z = -9, X = -12 -> +8, len = 20)
            WorldArchitectureKit.BuildWallWithOpenings(
                wing.transform,
                new Vector3(-12f, 0f, -9f),
                new Vector3(8f, 0f, -9f),
                0f, CeilStd, 0.22f, p.LabTileWhite,
                new[]
                {
                    OpeningSpec.Door(5.2f, 1.4f, 2.25f),  // door_cold_s (X = -6.8)
                    OpeningSpec.Arch(15.0f, 1.4f, 2.25f)  // arch_vet_s  (X = +3.0)
                },
                addSkirting: true, palette: p, wallName: "Wall_ColdVet_SouthCorr");

            // Wing South Exterior Wall (Z = -13, X = -12 -> +8)
            WorldArchitectureKit.BuildWallWithOpenings(wing.transform, new Vector3(-12f, 0f, -13f), new Vector3(8f, 0f, -13f), 0f, CeilStd, WallT, p.ConcreteBoardform, null, addSkirting: false, palette: p, wallName: "Wall_Wing_SouthExterior");

            // COL-2 / DOOR-2 FIX: East Exterior Wall (X = +8, Z = -13 -> +19, len = 32)
            // True 3.0 x 3.0 m open steel-framed loading bay doorway at Z = -11.0 (CenterAlong = 2.0 m) — ZERO hiding tile wall!
            WorldArchitectureKit.BuildWallWithOpenings(
                wing.transform,
                new Vector3(8f, 0f, -13f),
                new Vector3(8f, 0f, 19f),
                0f, CeilStd, WallT, p.ConcreteBoardform,
                new[] { OpeningSpec.Door(2.0f, 3.0f, 3.0f, frameMat: p.DarkPaintedMetal) },
                addSkirting: false, palette: p, wallName: "Wall_Wing_EastToDock", includeInOcclusion: false);

            // Mount bashable stainless wing doors + visible jammed dock_roller (1.25 m clear crouch gap!)
            WorldArchitectureKit.BuildFramedDoor(wing.transform, "door_cold", new Vector3(-6.8f, 0f, 3f), 180f, 1.4f, 2.25f, DoorMechanism.SingleHinged, p, soundPrefix: "door_metal", leafMaterial: p.BrushedSteel);
            WorldArchitectureKit.BuildFramedDoor(wing.transform, "door_vet", new Vector3(3.0f, 0f, 3f), 180f, 1.4f, 2.25f, DoorMechanism.SingleHinged, p, soundPrefix: "door_metal", leafMaterial: p.BrushedSteel);
            WorldArchitectureKit.BuildFramedDoor(wing.transform, "door_cold_s", new Vector3(-6.8f, 0f, -9f), 0f, 1.4f, 2.25f, DoorMechanism.SingleHinged, p, soundPrefix: "door_metal", leafMaterial: p.BrushedSteel);
            WorldArchitectureKit.BuildFramedDoor(wing.transform, "dock_roller", new Vector3(8.0f, 0f, -11.0f), 90f, 3.0f, 3.0f, DoorMechanism.JammedRollerShutter, p, startsLocked: true, overheadSignText: "LOADING DOCK EXIT", bashableByRaptor: false);
            // P2-3 FIX: Heavy dark-steel side jamb trim channels sealing the two 10 cm light slivers flanking dock_roller!
            WorldArchitectureKit.CreateBox(wing.transform, "DockRoller_NorthJambTrim", new Vector3(8.0f, 1.54f, -9.50f), new Vector3(0.38f, 3.08f, 0.24f), p.DarkPaintedMetal, addCollider: false);
            WorldArchitectureKit.CreateBox(wing.transform, "DockRoller_SouthJambTrim", new Vector3(8.0f, 1.54f, -12.50f), new Vector3(0.38f, 3.08f, 0.24f), p.DarkPaintedMetal, addCollider: false);

            // Feed Prep Props: 3 stainless counters, grounded overhead meat rail + hanging meat (POS-13 fix!), Keeper's Note (3/5)
            float[] counterXs = { -8f, -3f, 2f };
            for (int i = 0; i < counterXs.Length; i++)
            {
                PrepTable(wing.transform, $"PrepCounter_{i}", new Vector3(counterXs[i], 0f, 11.0f), 1.1f, 6.2f, 0.9f, 40 + i);
            }
            DressFeedPrepWing(wing.transform, p);
            WorldArchitectureKit.BuildConduitRun(wing.transform, "OverheadMeatRail", new[] { new Vector3(5.5f, 2.75f, 5.0f), new Vector3(5.5f, 2.75f, 17.0f) }, 0.035f, p.BrushedSteel);
            for (int m = 0; m < 4; m++)
            {
                float mz = 6.5f + m * 3.0f;
                WorldArchitectureKit.CreateBox(wing.transform, $"MeatHook_{m}", new Vector3(5.5f, 2.55f, mz), new Vector3(0.03f, 0.42f, 0.03f), p.BrushedSteel, addCollider: false);
                p.InstantiatePropOrFallback("hanging_meat", wing.transform, new Vector3(5.5f, 1.64f, mz), new Vector3(0f, m * 45f, 0f), new Vector3(0.45f, 1.05f, 0.45f), grounded: false, addCollider: false, fallbackMat: p.RustedIron);
            }

            // Document 3/5: Feed Prep Keeper's Note on counter
            BuildDocumentProp(wing.transform, new Vector3(2.0f, 0.91f, 13.5f), "keeper_note", "Feed Prep Keeper's Note",
                "Read keeper's note {interact}", new[] { "keeper_note" },
                new[] { "KEEPER'S NOTE — Raptors test the north latches in storms. Loading dock roller motor is stuck half-open." }, p);

            // Vet Clinic props: stainless exam table, cabinets, broken feeding cage
            PrepTable(wing.transform, "VetExamTable", new Vector3(3.0f, 0f, -3.0f), 2.2f, 1.0f, 0.9f, 50);
            p.InstantiatePropOrFallback("feeding_cage", wing.transform, new Vector3(0.2f, 0f, -6.8f), new Vector3(0f, 24f, 0f), new Vector3(1.6f, 1.4f, 1.6f));
            WorldArchitectureKit.BuildDecal(wing.transform, "VetBloodClaw", new Vector3(0.2f, 0.05f, -5.0f), new Vector3(90f, 0f, 0f), new Vector3(2.2f, 2.2f, 0.4f), p.DecalBloodSmear, 0.85f);

            // Anchors & Triggers for Wing Raptor + Crouch Hint
            WorldArchitectureKit.BuildWorldAnchor(wing.transform, "wing_hall", new Vector3(-2f, 0f, 11f));
            WorldArchitectureKit.BuildWorldAnchor(wing.transform, "wing_cold", new Vector3(-7f, 0f, -4f));
            WorldArchitectureKit.BuildWorldAnchor(wing.transform, "wing_vet", new Vector3(3f, 0f, -3f));
            WorldArchitectureKit.BuildWorldAnchor(wing.transform, "wing_south", new Vector3(0f, 0f, -11f));
            WorldArchitectureKit.BuildWorldAnchor(wing.transform, "dock_door", new Vector3(8f, 0f, -11f));
            WorldArchitectureKit.BuildStoryTrigger(wing.transform, "dock_crouch_hint", new Vector3(7.1f, 1.5f, -11.0f), new Vector3(1.4f, 3.0f, 3.2f), oneShot: false);

            // Wing Lighting & ReflectionProbes
            // Power on: four ceiling fixtures in the prep hall, one each in cold storage, vet clinic and the south corridor
            CeilingLamp(wing.transform, "Light_Prep_Main_0", new Vector3(-5.5f, CeilStd - 0.15f, 7.5f), LightGroupId.VCMain, 4300f);
            CeilingLamp(wing.transform, "Light_Prep_Main_1", new Vector3(-5.5f, CeilStd - 0.15f, 14.5f), LightGroupId.VCMain, 4300f);
            CeilingLamp(wing.transform, "Light_Prep_Main_2", new Vector3(3.0f, CeilStd - 0.15f, 7.5f), LightGroupId.VCMain, 4300f);
            CeilingLamp(wing.transform, "Light_Prep_Main_3", new Vector3(3.0f, CeilStd - 0.15f, 14.5f), LightGroupId.VCMain, 4300f);
            CeilingWash(wing.transform, "Light_Prep_Wash", new Vector3(-1.2f, 0f, 11f), LightGroupId.VCMain, 4300f);
            // fix round: the feed-prep hall's white ceiling read black over lit walls (first capture of the room): the same even
            // ceiling fills as the other rooms, 3 x 3 cells over the hall
            CeilingFill(wing.transform, "Prep", -11.3f, 7.3f, 3.5f, 18.4f, CeilStd, LightGroupId.VCMain, 5000f, 6.5f);
            CeilingLamp(wing.transform, "Light_Cold_Main", new Vector3(-7f, CeilStd - 0.15f, -1.5f), LightGroupId.VCMain, 5000f);
            CeilingLamp(wing.transform, "Light_Vet_Main", new Vector3(3f, CeilStd - 0.15f, -3f), LightGroupId.VCMain, 4300f);
            CeilingLamp(wing.transform, "Light_WingS_Main", new Vector3(-2f, CeilStd - 0.15f, -11f), LightGroupId.VCMain, 4300f);
            // Blackout: warm battery units, one per space
            BatteryLight(wing.transform, "Prep", -1, new Vector3(-2f, CeilStd - 0.15f, 18.3f));
            // (the cold room and vet room keep the one unit by their doorway, placed with the door frames above)
            BatteryLight(wing.transform, "WingS", -1, new Vector3(5.5f, CeilStd - 0.15f, -12.3f));
            WorldArchitectureKit.BuildHdrpLight(wing.transform, "Light_Cold_Blue", new Vector3(-7f, 3.15f, -3f), Vector3.zero, LightType.Point, new Color(0.32f, 0.68f, 1.0f), 320f, 11f, LightGroupId.VCExit, volumetricDimmer: 1.5f);
            WorldArchitectureKit.BuildRoomReflectionProbe(wing.transform, "FeedPrepWing", new Vector3(-2f, 1.7f, 3.0f), new Vector3(19.6f, 3.4f, 31.6f), "vc_wing");

            // COVERED LOADING DOCK (X: +8..+22, Z: -18..-2, Canopy Y = 5.2 m)
            var dock = new GameObject("CoveredLoadingDock");
            dock.transform.SetParent(wing.transform, false);
            // round 4: a dry concrete pad under the roof and a galvanized (grey painted) corrugated deck that returns the torch
            // light; the rust-red deck over wet asphalt made the dock a near-black void in the blackout (review 3 13_ch3)
            WorldArchitectureKit.BuildBoxRoomShell(dock.transform, "Dock_Pad", new Vector3(15f, 0f, -10f), 0f, 14f, 16f, 5.2f, p.ConcreteFloor, p.CorrugatedPainted, SurfaceType.Concrete, p, buildCeiling: true, addRainBlocker: true);
            // rain blown in under the open east and south edges: wet strips along them only (thin, no collider)
            Box(dock.transform, "Dock_WetEdge_E", new Vector3(20.75f, 0.003f, -10.0f), new Vector3(2.5f, 0.006f, 15.9f), p.ConcreteFloorWet);
            Box(dock.transform, "Dock_WetEdge_S", new Vector3(14.0f, 0.003f, -16.85f), new Vector3(11.5f, 0.006f, 2.2f), p.ConcreteFloorWet);
            // Canopy structure: columns on the east edge and the open south-west corner, perimeter beams, and a concrete
            // upstand on top of the wing wall so the canopy's west edge bears on the building instead of floating
            WorldArchitectureKit.CreateBox(dock.transform, "CanopyCol_NE", new Vector3(21.6f, 2.6f, -2.4f), new Vector3(0.32f, 5.2f, 0.32f), p.DarkPaintedMetal, addCollider: true);
            WorldArchitectureKit.CreateBox(dock.transform, "CanopyCol_E", new Vector3(21.6f, 2.6f, -10.0f), new Vector3(0.32f, 5.2f, 0.32f), p.DarkPaintedMetal, addCollider: true);
            WorldArchitectureKit.CreateBox(dock.transform, "CanopyCol_SE", new Vector3(21.6f, 2.6f, -17.6f), new Vector3(0.32f, 5.2f, 0.32f), p.DarkPaintedMetal, addCollider: true);
            WorldArchitectureKit.CreateBox(dock.transform, "CanopyCol_SW", new Vector3(8.3f, 2.6f, -17.6f), new Vector3(0.32f, 5.2f, 0.32f), p.DarkPaintedMetal, addCollider: true);
            Box(dock.transform, "CanopyBeam_W", new Vector3(8.3f, 5.0f, -10.0f), new Vector3(0.3f, 0.4f, 16.0f), mDarkMetal);
            Box(dock.transform, "CanopyBeam_E", new Vector3(21.6f, 5.0f, -10.0f), new Vector3(0.3f, 0.4f, 16.0f), mDarkMetal);
            Box(dock.transform, "CanopyBeam_N", new Vector3(15.0f, 5.0f, -2.15f), new Vector3(13.6f, 0.4f, 0.3f), mDarkMetal);
            Box(dock.transform, "CanopyBeam_S", new Vector3(15.0f, 5.0f, -17.85f), new Vector3(13.6f, 0.4f, 0.3f), mDarkMetal);
            for (int r = 0; r < 4; r++) Box(dock.transform, $"CanopyRafter_{r}", new Vector3(15.0f, 5.06f, -14.8f + r * 3.2f), new Vector3(13.0f, 0.28f, 0.16f), mDarkMetal);
            Box(dock.transform, "Wing_Upstand", new Vector3(8.0f, (CeilStd + 5.44f) * 0.5f, -7.5f), new Vector3(0.26f, 5.44f - CeilStd, 11.0f), p.ConcreteBoardform, true);
            Box(dock.transform, "CanopyFascia_E", new Vector3(21.8f, 5.3f, -10.0f), new Vector3(0.06f, 0.36f, 16.2f), mDarkMetal);
            DressLoadingDock(dock.transform, p);

            p.InstantiatePropOrFallback("utility_truck", dock.transform, new Vector3(15.5f, 0f, -5.5f), new Vector3(0f, 90f, 0f), new Vector3(2.4f, 2.4f, 5.6f));
            p.InstantiatePropOrFallback("pallet", dock.transform, new Vector3(12.5f, 0f, -16.4f), Vector3.zero, new Vector3(1.2f, 0.18f, 1.2f));
            p.InstantiatePropOrFallback("crate", dock.transform, new Vector3(14.5f, 0f, -16.4f), new Vector3(0f, 10f, 0f), new Vector3(1.0f, 1.0f, 1.0f));
            p.InstantiatePropOrFallback("oil_drum", dock.transform, new Vector3(17.0f, 0f, -16.2f), Vector3.zero, new Vector3(0.62f, 0.92f, 0.62f));

            // Storm-blue backlight flooding under the jammed dock_roller gap so players immediately spot the exit!
            WorldArchitectureKit.BuildHdrpLight(dock.transform, "Light_Dock_StormBacklight", new Vector3(10.5f, 3.6f, -11.0f), new Vector3(55f, -90f, 0f), LightType.Spot, new Color(0.42f, 0.72f, 1.0f), 1900f, 16f, LightGroupId.VCExit, volumetricDimmer: 2.0f, castShadows: true, spotOuterAngle: 75f);
            // round 4: a high-bay on a drop rod (LookFixtures.HighBay), not four flat ceiling panels; low enough for its bell
            WorldArchitectureKit.BuildHdrpLight(dock.transform, "Light_Dock_Main", new Vector3(13.4f, 4.45f, -10f), Vector3.zero, LightType.Point, new Color(1.0f, 0.92f, 0.78f), 2400f, 20f, LightGroupId.DockMain);
            // the downlight leaves the canopy beams and rafters black: a soft upward wash from the pad lights the underside
            CeilingWash(dock.transform, "Light_Dock_Wash", new Vector3(15f, 0f, -10.5f), LightGroupId.DockMain, 3500f);
            // round 4: hung from the canopy between two rafters over the walk from the roller door to the Service Road 3 exit
            // (north-east corner), so the player crosses one warm pool (it was on the wall behind the player)
            BatteryLight(dock.transform, "Dock", -1, new Vector3(17.2f, 4.6f, -10.0f));

            WorldArchitectureKit.BuildWorldAnchor(dock.transform, "dock", new Vector3(18.0f, 0.05f, -10.0f), yawDeg: 90f);
            WorldArchitectureKit.BuildStoryTrigger(dock.transform, "reach_dock", new Vector3(10.2f, 1.5f, -11.0f), new Vector3(2.8f, 3.0f, 3.8f), oneShot: true);
        }

        private static void PrepTable(Transform parent, string name, Vector3 floorPos, float w, float d, float h, int seed)
        {
            // Stainless prep table: top with a rolled edge, under-shelf, legs every ~1.5 m; one solid collider (same footprint as before)
            Transform t = Group(parent, name, floorPos, 0f);
            BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, h * 0.5f, 0f); bc.size = new Vector3(w, h, d);
            Box(t, "Top", new Vector3(0f, h - 0.02f, 0f), new Vector3(w, 0.04f, d), mSteel);
            Box(t, "EdgeL", new Vector3(-w * 0.5f, h - 0.035f, 0f), new Vector3(0.03f, 0.07f, d), mSteel);
            Box(t, "EdgeR", new Vector3(w * 0.5f, h - 0.035f, 0f), new Vector3(0.03f, 0.07f, d), mSteel);
            Box(t, "UnderShelf", new Vector3(0f, 0.22f, 0f), new Vector3(w - 0.1f, 0.025f, d - 0.1f), mSteel);
            int pairs = Mathf.Max(2, Mathf.CeilToInt(d / 1.6f) + 1);
            for (int i = 0; i < pairs; i++)
            {
                float z = -d * 0.5f + 0.05f + i * (d - 0.1f) / (pairs - 1);
                Box(t, $"LegL_{i}", new Vector3(-w * 0.5f + 0.05f, (h - 0.04f) * 0.5f, z), new Vector3(0.04f, h - 0.04f, 0.04f), mSteel);
                Box(t, $"LegR_{i}", new Vector3(w * 0.5f - 0.05f, (h - 0.04f) * 0.5f, z), new Vector3(0.04f, h - 0.04f, 0.04f), mSteel);
            }
            var rng = new System.Random(seed);
            // a few tubs and trays on the under-shelf
            for (int k = 0; k < Mathf.FloorToInt(d / 1.2f); k++)
            {
                float z = -d * 0.5f + 0.5f + k * 1.15f + (float)rng.NextDouble() * 0.2f;
                // round 4 (review 3: "a blank cream box under the feed-prep table"): lidded food tubs with a rim, handles
                // and a marker label
                float tx = (float)rng.NextDouble() * 0.2f - 0.1f;
                Material tm = k % 2 == 0 ? mTub : mBinderBlue;
                Box(t, $"Tub_{k}", new Vector3(tx, 0.325f, z), new Vector3(0.48f, 0.19f, 0.34f), tm);
                Box(t, $"TubRim_{k}", new Vector3(tx, 0.425f, z), new Vector3(0.52f, 0.02f, 0.38f), tm);
                Box(t, $"TubLid_{k}", new Vector3(tx, 0.438f, z), new Vector3(0.46f, 0.008f, 0.32f), k % 2 == 0 ? mBinderBlue : mTub);
                for (int hdl = 0; hdl < 2; hdl++) Box(t, $"TubHandle_{k}_{hdl}", new Vector3(tx + (hdl == 0 ? -0.255f : 0.255f), 0.39f, z), new Vector3(0.02f, 0.03f, 0.14f), tm);
                Ink(t, $"TubLabel_{k}", k % 2 == 0 ? "FISH 20kg\nthaw 19/11" : "GOAT\nquarters", new Vector3(tx, 0.33f, z - 0.172f), Quaternion.identity, new Vector2(0.3f, 0.12f), InkBlack, "Kalam-Regular", 0.5f, TMPro.TextAlignmentOptions.Center);
            }
        }

        private static void DressFeedPrepWing(Transform wing, WorldMaterialPalette p)
        {
            var fp = new GameObject("Props_FeedPrep");
            fp.transform.SetParent(wing, false);
            const float westFace = -11.827f;   // white tile lining on the lobby wall
            const float eastFace = 7.847f;

            // Double sink run on the west wall, hose reel, feeding schedule board, feed bins
            Transform sink = Counter(fp.transform, "SinkCounter", new Vector3(westFace + 0.36f, 0f, 14.6f), -90f, 3.2f, 0.7f, 0.92f, mSteel, mSteel);
            for (int b = 0; b < 2; b++)
            {
                Box(sink, $"Basin_{b}", new Vector3(-0.6f + b * 1.0f, 0.905f, 0f), new Vector3(0.7f, 0.03f, 0.5f), mBlack);
                Box(sink, $"Tap_{b}", new Vector3(-0.6f + b * 1.0f, 1.08f, 0.28f), new Vector3(0.03f, 0.32f, 0.03f), mSteel);
                Box(sink, $"Spout_{b}", new Vector3(-0.6f + b * 1.0f, 1.22f, 0.18f), new Vector3(0.025f, 0.025f, 0.2f), mSteel);
            }
            Box(fp.transform, "Splashback", new Vector3(westFace + 0.01f, 1.25f, 14.6f), new Vector3(0.02f, 0.6f, 3.2f), mSteel);
            Transform reel = Group(fp.transform, "HoseReel", new Vector3(westFace, 1.3f, 11.2f), 90f);
            Box(reel, "Bracket", new Vector3(0f, 0f, 0.04f), new Vector3(0.1f, 0.5f, 0.08f), mBlack);
            Box(reel, "Drum", new Vector3(0f, 0f, 0.2f), new Vector3(0.5f, 0.5f, 0.16f), mVendRed);
            Box(reel, "Hose", new Vector3(0f, 0f, 0.2f), new Vector3(0.4f, 0.4f, 0.2f), mBlack);
            CorkBoard(fp.transform, "FeedingSchedule", new Vector3(westFace, 1.55f, 8.0f), Vector3.right, 1.6f, 1.0f, 41);
            Poster(fp.transform, p, "FeedPrep_Sign", "feed_prep", new Vector3(-6.5f, 2.35f, 18.857f), Vector3.back, new Vector2(1.4f, 0.52f), frame: false);
            Poster(fp.transform, p, "FeedPrep_RaptorPoster", "raptor_poster", new Vector3(eastFace, 1.6f, 16.5f), Vector3.left, new Vector2(0.62f, 0.88f));
            for (int c = 0; c < 4; c++)
            {
                GameObject crate = p.InstantiatePropOrFallback("crate", fp.transform, new Vector3(eastFace - 0.5f, 0f, 9.0f + c * 0.95f), new Vector3(0f, c * 7f - 10f, 0f), new Vector3(0.85f, 0.85f, 0.85f));
                if (c % 2 == 0) p.InstantiatePropOrFallback("crate", fp.transform, new Vector3(eastFace - 0.5f, GetCombinedBoundsMaxY(crate, 0.45f) - 0.01f, 9.0f + c * 0.95f), new Vector3(0f, c * 11f, 0f), new Vector3(0.7f, 0.7f, 0.7f), grounded: true);
            }
            Box(fp.transform, "FloorDrain_0", new Vector3(-5.5f, 0.004f, 11.0f), new Vector3(0.4f, 0.008f, 0.4f), mBlack);
            Box(fp.transform, "FloorDrain_1", new Vector3(-0.5f, 0.004f, 11.0f), new Vector3(0.4f, 0.008f, 0.4f), mBlack);
            DecalAt(fp.transform, "Prep_DrainGrime_0", p.DecalGrimeLeak, new Vector3(-5.5f, 0.02f, 11.0f), 1.8f, 30f, 0.7f);
            DecalAt(fp.transform, "Prep_DrainGrime_1", p.DecalGrimeLeak, new Vector3(-0.5f, 0.02f, 11.0f), 1.6f, 120f, 0.6f);
            DecalAt(fp.transform, "Prep_BloodSmear", p.DecalBloodSmear, new Vector3(5.0f, 0.02f, 9.5f), 1.4f, 60f, 0.55f);
            WallExtinguisher(fp.transform, p, new Vector3(-10.4f, 0f, 18.857f), Vector3.back);
            p.InstantiatePropOrFallback("trash_bin", fp.transform, new Vector3(-10.9f, 0f, 4.0f), Vector3.zero, new Vector3(0.45f, 0.75f, 0.45f));
            p.InstantiatePropOrFallback("jerry_can", fp.transform, new Vector3(-10.9f, 0f, 12.6f), new Vector3(0f, 80f, 0f), new Vector3(0.36f, 0.48f, 0.22f));

            // Cold storage: shelving with crates along the walls, a chest freezer
            p.InstantiatePropOrFallback("archive_shelf", fp.transform, new Vector3(westFace + 0.36f, 0f, -1.5f), new Vector3(0f, 90f, 0f), new Vector3(2.2f, 2.2f, 0.55f));
            p.InstantiatePropOrFallback("archive_shelf", fp.transform, new Vector3(westFace + 0.36f, 0f, -5.5f), new Vector3(0f, 90f, 0f), new Vector3(2.2f, 2.2f, 0.55f));
            Transform frz = Group(fp.transform, "ChestFreezer", new Vector3(-10.2f, 0f, 1.95f), 180f);   // outside door_cold's swing
            Box(frz, "Body", new Vector3(0f, 0.43f, 0f), new Vector3(1.6f, 0.86f, 0.75f), mLaminate, true);
            Box(frz, "Lid", new Vector3(0f, 0.88f, 0f), new Vector3(1.62f, 0.05f, 0.77f), mLaminate);
            Box(frz, "Handle", new Vector3(0f, 0.8f, -0.39f), new Vector3(0.5f, 0.03f, 0.03f), mSteel);
            Box(fp.transform, "StripCurtainRail", new Vector3(-6.8f, 2.3f, -8.75f), new Vector3(1.5f, 0.06f, 0.08f), mSteel);
            DecalAt(fp.transform, "Cold_FloorGrime", p.DecalGrimeLeak, new Vector3(-7.0f, 0.02f, -4.0f), 2.4f, 10f, 0.6f);

            // Vet clinic: wall cabinets with a sink on the east wall, a medicine cabinet, an exam lamp over the table
            Transform vc = Counter(fp.transform, "VetCabinets", new Vector3(eastFace - 0.33f, 0f, -4.0f), 90f, 3.0f, 0.64f, 0.9f, mLaminate, mCabinet);
            Box(vc, "VetSink", new Vector3(0.7f, 0.89f, 0f), new Vector3(0.5f, 0.02f, 0.4f), mSteel);
            Box(vc, "VetTap", new Vector3(0.7f, 1.04f, 0.22f), new Vector3(0.03f, 0.28f, 0.03f), mSteel);
            for (int i = 0; i < 3; i++) Box(vc, $"VetUpper_{i}", new Vector3(-1.0f + i * 1.0f, 1.85f, 0.14f), new Vector3(0.96f, 0.7f, 0.34f), mCabinet);
            Box(vc, "SharpsBox", new Vector3(-1.1f, 0.98f, 0.1f), new Vector3(0.22f, 0.16f, 0.14f), p.PaintedMetalYellow);
            Box(fp.transform, "ExamLampArm", new Vector3(3.0f, 2.85f, -3.0f), new Vector3(0.04f, 1.2f, 0.04f), mSteel);
            Box(fp.transform, "ExamLampHead", new Vector3(3.0f, 2.22f, -3.0f), new Vector3(0.5f, 0.1f, 0.5f), mCabinet);
            Box(fp.transform, "ExamTowel", new Vector3(3.3f, 0.905f, -3.1f), new Vector3(0.6f, 0.012f, 0.4f), mBinderBlue);

            // South corridor by the jammed roller: a pallet, a drum, caution stripes on the floor at the shutter
            p.InstantiatePropOrFallback("pallet", fp.transform, new Vector3(-8.5f, 0f, -11.3f), new Vector3(0f, 5f, 0f), new Vector3(1.2f, 0.18f, 1.2f));
            p.InstantiatePropOrFallback("oil_drum", fp.transform, new Vector3(-10.9f, 0f, -12.2f), Vector3.zero, new Vector3(0.62f, 0.92f, 0.62f));
            DecalAt(fp.transform, "WingS_RollerCaution", p.DecalCautionStripes, new Vector3(7.3f, 0.02f, -11.0f), 3.0f, 90f, 0.7f);
        }

        private static void DressLoadingDock(Transform dock, WorldMaterialPalette p)
        {
            // Steel bollards protecting the roller door (outside its 3 m width), stacked freight, a dumpster, a hand truck
            for (int b = 0; b < 2; b++)
            {
                Box(dock, $"DoorBollard_{b}", new Vector3(9.0f, 0.55f, b == 0 ? -9.1f : -12.9f), new Vector3(0.2f, 1.1f, 0.2f), mPaintYellow, true);
                Box(dock, $"DoorBollardCap_{b}", new Vector3(9.0f, 1.11f, b == 0 ? -9.1f : -12.9f), new Vector3(0.22f, 0.03f, 0.22f), mBlack);
            }
            GameObject p2 = p.InstantiatePropOrFallback("pallet", dock, new Vector3(19.6f, 0f, -16.3f), new Vector3(0f, 85f, 0f), new Vector3(1.2f, 0.18f, 1.2f));
            float py = GetCombinedBoundsMaxY(p2, 0.18f) - 0.005f;
            p.InstantiatePropOrFallback("crate", dock, new Vector3(19.4f, py, -16.5f), new Vector3(0f, 4f, 0f), new Vector3(0.9f, 0.9f, 0.9f), grounded: true);
            p.InstantiatePropOrFallback("crate", dock, new Vector3(19.9f, py, -15.8f), new Vector3(0f, -7f, 0f), new Vector3(0.7f, 0.7f, 0.7f), grounded: true);
            p.InstantiatePropOrFallback("oil_drum", dock, new Vector3(17.8f, 0f, -16.9f), Vector3.zero, new Vector3(0.62f, 0.92f, 0.62f));
            p.InstantiatePropOrFallback("jerry_can", dock, new Vector3(16.3f, 0f, -16.8f), new Vector3(0f, 25f, 0f), new Vector3(0.36f, 0.48f, 0.22f));
            // (the box dumpster that stood here is gone: zones 2-3 build a real skip on this spot, Zone23Dressing.BuildSkip)
            Transform ht = Group(dock, "HandTruck", new Vector3(10.4f, 0f, -15.9f), 30f);
            Box(ht, "Frame", new Vector3(0f, 0.65f, 0.1f), new Vector3(0.45f, 1.3f, 0.04f), mVendRed, false, new Vector3(-12f, 0f, 0f));
            Box(ht, "Nose", new Vector3(0f, 0.02f, -0.1f), new Vector3(0.45f, 0.03f, 0.25f), mSteel);
            Box(ht, "Wheels", new Vector3(0f, 0.12f, 0.15f), new Vector3(0.55f, 0.24f, 0.08f), mBlack);
            // Painted edge of the pad and a caution band in front of the roller
            DecalAt(dock, "Dock_CautionRoller", p.DecalCautionStripes, new Vector3(9.4f, 0.02f, -11.0f), 3.2f, 90f, 0.75f);
            DecalAt(dock, "Dock_OilStain_0", p.DecalGrimeLeak, new Vector3(15.5f, 0.02f, -5.4f), 3.4f, 20f, 0.7f);
            DecalAt(dock, "Dock_Cracks_0", p.DecalCracks, new Vector3(13.0f, 0.02f, -12.5f), 3.0f, 70f, 0.7f);
            Poster(dock, p, "Dock_StaffOnly", "staff_only", new Vector3(8.13f, 2.1f, -7.6f), Vector3.right, new Vector2(0.9f, 0.45f));
        }

        // =========================================================================
        // MAIN PLAZA & T. REX VIEWING GALLERY (X: -101.5 .. +56, Z: -96 .. -32)
        // =========================================================================

        private static void BuildPlazaAndViewingGallery(Transform parent, WorldMaterialPalette p)
        {
            var ext = new GameObject("PlazaAndViewingGallery");
            ext.transform.SetParent(parent, false);

            // Paved plaza apron (one walkable slab, paving stones mapped in world space) with kerbs on every edge
            GameObject plaza = WorldArchitectureKit.CreateBox(ext.transform, "MainPlaza_Paving", new Vector3(-35.75f, -0.15f, -64f), new Vector3(131.5f, 0.30f, 64f), mPlazaPaving, addCollider: true);
            WorldArchitectureKit.ApplySurface(plaza, SurfaceType.Concrete, p);
            Box(ext.transform, "PlazaKerb_S", new Vector3(-35.75f, 0.06f, -95.85f), new Vector3(131.5f, 0.12f, 0.3f), mKerb, true);
            Box(ext.transform, "PlazaKerb_W", new Vector3(-101.35f, 0.06f, -64f), new Vector3(0.3f, 0.12f, 64f), mKerb, true);
            Box(ext.transform, "PlazaKerb_E", new Vector3(29.85f, 0.06f, -64f), new Vector3(0.3f, 0.12f, 64f), mKerb, true);
            // A strip of darker paving along the facade and the entrance axis

            // (review 2: the two 0.4 m dark strips read as tar seams; the forecourt builds a 9 m processional axis instead)

            // Viewing rail on the south edge: posts every 2 m, glass panels, steel cap (was one solid slab)
            GlassBalustrade(ext.transform, p, "SouthViewingRail", -101.3f, 29.7f, -95.6f, 0f);

            // Plaza Bollard Lights (LightGroupId.Plaza)
            for (int i = 0; i < 6; i++)
            {
                float bx = -80f + i * 19f;
                p.InstantiatePropOrFallback("bollard_light", ext.transform, new Vector3(bx, 0f, -44f), Vector3.zero, new Vector3(0.28f, 1.05f, 0.28f));
                WorldArchitectureKit.BuildHdrpLight(ext.transform, $"Light_PlazaBollard_{i}", new Vector3(bx, 1.15f, -44f), Vector3.zero, LightType.Point, new Color(1.0f, 0.84f, 0.58f), 680f, 12f, LightGroupId.Plaza, volumetricDimmer: 0.9f);
            }

            // Lamp posts lining the entrance axis (Plaza group)
            for (int i = 0; i < 4; i++)
            {
                float lz = -48f - i * 13f;
                for (int side = 0; side < 2; side++)
                {
                    float lx = side == 0 ? -40.5f : -23.5f;
                    p.InstantiatePropOrFallback("lamp_post", ext.transform, new Vector3(lx, 0f, lz), new Vector3(0f, side == 0 ? 90f : -90f, 0f), new Vector3(0.5f, 4.6f, 0.5f), grounded: true, addCollider: true);
                    WorldArchitectureKit.BuildHdrpLight(ext.transform, $"Light_PlazaLamp_{i * 2 + side}", new Vector3(lx, 4.25f, lz), new Vector3(90f, 0f, 0f), LightType.Spot, new Color(1f, 0.85f, 0.65f), 1900f, 16f, LightGroupId.Plaza, volumetricDimmer: 0.8f, castShadows: true, spotOuterAngle: 120f, colorTempK: 2700f);
                }
            }

            // Raised planting beds with tropical planting, benches and bins between them
            // bed 3 sits east of the lobby raptor's flee line (-36,-36) -> (10,-60), which crossed it at (-4,-52)
            Vector2[] beds = { new(-74f, -52f), new(-60f, -70f), new(-74f, -84f), new(6f, -50f), new(10f, -70f), new(-4f, -84f) };
            for (int b = 0; b < beds.Length; b++)
            {
                PlantingBed(ext.transform, p, $"PlantingBed_{b}", new Vector3(beds[b].x, 0f, beds[b].y), 7.0f, 3.4f, 70 + b);
            }
            for (int i = 0; i < 4; i++)
            {
                float bz = -56f - i * 13f;
                p.InstantiatePropOrFallback("park_bench", ext.transform, new Vector3(-43.2f, 0f, bz), new Vector3(0f, 90f, 0f), new Vector3(1.8f, 0.85f, 0.65f));
                p.InstantiatePropOrFallback("park_bench", ext.transform, new Vector3(-20.8f, 0f, bz), new Vector3(0f, -90f, 0f), new Vector3(1.8f, 0.85f, 0.65f));
                if (i % 2 == 0)
                {
                    p.InstantiatePropOrFallback("trash_bin", ext.transform, new Vector3(-43.2f, 0f, bz - 1.6f), Vector3.zero, new Vector3(0.5f, 0.85f, 0.5f));
                    p.InstantiatePropOrFallback("trash_bin", ext.transform, new Vector3(-20.8f, 0f, bz - 1.6f), Vector3.zero, new Vector3(0.5f, 0.85f, 0.5f));
                }
            }
            // Park map board by the entrance, fire assembly point sign
            SignBoard(ext.transform, p, "Plaza_MapBoard", "map_park_modern", new Vector3(-44.0f, 0f, -38.5f), 0f, new Vector2(2.4f, 1.44f), 1.0f);
            SignBoard(ext.transform, p, "Plaza_FireAssembly", "fire_assembly", new Vector3(-86.0f, 0f, -60.0f), 0f, new Vector2(0.9f, 0.9f), 1.4f);

            BuildEntranceCanopy(ext.transform, p);
            BuildForecourtReview2(ext.transform, p);

            // T. rex Viewing Gallery (X: +30..+56, Z: -70..-40): roof on columns and edge beams, benches, downlights
            WorldArchitectureKit.BuildBoxRoomShell(ext.transform, "ViewingGallery_Deck", new Vector3(43f, 0f, -55f), 0f, 26f, 30f, 5.2f, p.ConcreteFloor, p.ConcreteBoardform, SurfaceType.Concrete, p);
            WorldArchitectureKit.BuildWallWithOpenings(
                ext.transform,
                new Vector3(56f, 0f, -70f),
                new Vector3(56f, 0f, -40f),
                0f, 5.2f, 0.28f, p.ConcreteBoardform,
                new[] { OpeningSpec.Window(15.0f, 24.0f, 0.6f, 4.0f, mullions: 7, glassMat: mGlass) },
                palette: p, wallName: "Wall_Gallery_EastGlass", includeInOcclusion: false);
            var gal = new GameObject("ViewingGallery_Structure");
            gal.transform.SetParent(ext.transform, false);
            float[] colZ = { -69.7f, -62.3f, -55f, -47.7f, -40.3f };
            for (int c = 0; c < colZ.Length; c++) Box(gal.transform, $"GalleryCol_W_{c}", new Vector3(30.3f, 2.6f, colZ[c]), new Vector3(0.4f, 5.2f, 0.4f), p.ConcreteBoardform, true);
            float[] colX = { 36.8f, 43.3f, 49.8f };
            for (int c = 0; c < colX.Length; c++)
            {
                Box(gal.transform, $"GalleryCol_N_{c}", new Vector3(colX[c], 2.6f, -40.3f), new Vector3(0.4f, 5.2f, 0.4f), p.ConcreteBoardform, true);
                Box(gal.transform, $"GalleryCol_S_{c}", new Vector3(colX[c], 2.6f, -69.7f), new Vector3(0.4f, 5.2f, 0.4f), p.ConcreteBoardform, true);
            }
            Box(gal.transform, "GalleryBeam_W", new Vector3(30.3f, 4.95f, -55f), new Vector3(0.45f, 0.5f, 30f), p.ConcreteBoardform);
            Box(gal.transform, "GalleryBeam_N", new Vector3(43f, 4.95f, -40.3f), new Vector3(26f, 0.5f, 0.45f), p.ConcreteBoardform);
            Box(gal.transform, "GalleryBeam_S", new Vector3(43f, 4.95f, -69.7f), new Vector3(26f, 0.5f, 0.45f), p.ConcreteBoardform);
            for (int b = 0; b < 3; b++) Bench(gal.transform, $"GalleryBench_{b}", new Vector3(50.5f, 0f, -63f + b * 8f), 90f, 3.0f);
            SignBoard(gal.transform, p, "Gallery_RexPanel", "rex_banner", new Vector3(38.0f, 0f, -55f), 90f, new Vector2(1.1f, 2.2f), 0.4f);
            SignBoard(gal.transform, p, "Gallery_RaptorPanel", "raptor_poster", new Vector3(38.0f, 0f, -47f), 90f, new Vector2(1.1f, 1.56f), 0.6f);
            for (int i = 0; i < 6; i++)
            {
                Vector3 lp = new Vector3(37f + (i % 3) * 7.5f, 4.9f, -62f + (i / 3) * 14f);
                // VCMain, not Plaza: the opening cutscene keeps the Plaza group dark until shot 5b, so the gallery read as
                // a crushed black ceiling in the afternoon T. rex beat (intro check, missed finding)
                CeilingLamp(gal.transform, $"Light_Gallery_Down_{i}", lp, LightGroupId.VCMain, 3000f, shadows: false);
            }
        }

        private static void PlantingBed(Transform parent, WorldMaterialPalette p, string name, Vector3 centre, float w, float d, int seed)
        {
            Transform t = Group(parent, name, centre, 0f);
            const float h = 0.42f;
            Box(t, "KerbN", new Vector3(0f, h * 0.5f, d * 0.5f - 0.15f), new Vector3(w, h, 0.3f), mKerb, true);
            Box(t, "KerbS", new Vector3(0f, h * 0.5f, -d * 0.5f + 0.15f), new Vector3(w, h, 0.3f), mKerb, true);
            Box(t, "KerbW", new Vector3(-w * 0.5f + 0.15f, h * 0.5f, 0f), new Vector3(0.3f, h, d - 0.6f), mKerb, true);
            Box(t, "KerbE", new Vector3(w * 0.5f - 0.15f, h * 0.5f, 0f), new Vector3(0.3f, h, d - 0.6f), mKerb, true);
            Box(t, "Soil", new Vector3(0f, h - 0.08f, 0f), new Vector3(w - 0.6f, 0.12f, d - 0.6f), p.MudGround, true);
            var rng = new System.Random(seed);
            // one palm per bed for a silhouette above the plaza, under-planting around it
            p.InstantiatePropOrFallback(seed % 2 == 0 ? "palm_tree" : "palm_tree_b", t, centre + new Vector3((seed % 3 - 1) * 1.2f, h + 0.02f, 0f), new Vector3(0f, seed * 47f, 0f), new Vector3(7.5f, 7.5f, 7.5f), grounded: true, addCollider: false);
            string[] plants = { "banana_plant", "tree_fern", "monstera_clump", "monstera_clump" };
            int n = 4;
            for (int i = 0; i < n; i++)
            {
                string stem = plants[(i + seed) % plants.Length];
                float x = -w * 0.5f + 0.9f + i * (w - 1.8f) / (n - 1) + ((float)rng.NextDouble() - 0.5f) * 0.4f;
                float z = ((float)rng.NextDouble() - 0.5f) * (d - 1.6f);
                float size = stem == "monstera_clump" ? 1.3f : (stem == "tree_fern" ? 2.8f : 2.4f);
                p.InstantiatePropOrFallback(stem, t, centre + new Vector3(x, h + 0.02f, z), new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), new Vector3(size, size, size), grounded: true, addCollider: false);
            }
        }

        /// <summary>Free-standing sign board on two posts. floorPos = centre between the posts, picture faces local -Z.</summary>
        private static void SignBoard(Transform parent, WorldMaterialPalette p, string name, string stem, Vector3 floorPos, float yaw, Vector2 size, float bottom)
        {
            Transform t = Group(parent, name, floorPos, yaw);
            float top = bottom + size.y;
            Box(t, "PostL", new Vector3(-size.x * 0.5f - 0.06f, top * 0.5f + 0.05f, 0.03f), new Vector3(0.08f, top + 0.1f, 0.08f), mDarkMetal, true);
            Box(t, "PostR", new Vector3(size.x * 0.5f + 0.06f, top * 0.5f + 0.05f, 0.03f), new Vector3(0.08f, top + 0.1f, 0.08f), mDarkMetal, true);
            Box(t, "Backing", new Vector3(0f, bottom + size.y * 0.5f, 0.03f), new Vector3(size.x + 0.08f, size.y + 0.08f, 0.04f), mDarkMetal);
            GameObject pic = Poster(t, p, "Face", stem, t.TransformPoint(new Vector3(0f, bottom + size.y * 0.5f, 0.01f)), t.TransformDirection(Vector3.back), size, frame: false);
            pic.transform.SetParent(t, true);
        }

        /// <summary>Welcome canopy over the main entrance: a 3 m slab hung from the facade on two tie rods (no columns on the
        /// plaza, so the scripted lobby raptor's mark at (-36,-36) stays clear), downlights, the welcome sign on the front edge,
        /// the park logo above.</summary>
        private static void BuildEntranceCanopy(Transform parent, WorldMaterialPalette p)
        {
            var c = new GameObject("EntranceCanopy");
            c.transform.SetParent(parent, false);
            const float zFace = -32.15f;   // facade outer face
            const float depth = 5.0f;      // review 2: a 3 m slab read as tiny on the hero facade
            const float width = 10.4f;
            float zMid = zFace - depth * 0.5f, zFront = zFace - depth;
            float xW = -32f - width * 0.5f, xE = -32f + width * 0.5f;
            Box(c.transform, "CanopySlab", new Vector3(-32f, 4.55f, zMid), new Vector3(width, 0.26f, depth), p.ConcreteBoardform, true);
            Box(c.transform, "CanopySoffit", new Vector3(-32f, 4.41f, zMid), new Vector3(width - 0.1f, 0.03f, depth - 0.1f), p.WoodPlanks);
            // two-tone fascia (white enamel band over a dark drip) and timber battens under the soffit: a plain dark slab before
            Box(c.transform, "CanopyFascia", new Vector3(-32f, 4.62f, zFront - 0.03f), new Vector3(width + 0.1f, 0.4f, 0.08f), mCabinet);
            Box(c.transform, "CanopyFasciaW", new Vector3(xW - 0.05f, 4.62f, zMid), new Vector3(0.08f, 0.4f, depth + 0.08f), mCabinet);
            Box(c.transform, "CanopyFasciaE", new Vector3(xE + 0.05f, 4.62f, zMid), new Vector3(0.08f, 0.4f, depth + 0.08f), mCabinet);
            Box(c.transform, "CanopyDrip", new Vector3(-32f, 4.33f, zFront - 0.04f), new Vector3(width + 0.12f, 0.18f, 0.1f), mDarkMetal);
            Box(c.transform, "CanopyDripW", new Vector3(xW - 0.06f, 4.33f, zMid), new Vector3(0.1f, 0.18f, depth + 0.1f), mDarkMetal);
            Box(c.transform, "CanopyDripE", new Vector3(xE + 0.06f, 4.33f, zMid), new Vector3(0.1f, 0.18f, depth + 0.1f), mDarkMetal);
            for (int bt = 0; bt < 13; bt++)
                Box(c.transform, $"CanopyBatten_{bt}", new Vector3(xW + 0.4f + bt * (width - 0.8f) / 12f, 4.375f, zMid), new Vector3(0.06f, 0.05f, depth - 0.2f), mVeneer);
            WorldArchitectureKit.BuildRainBlockerSlab(c.transform, "RainBlock_Canopy", new Vector3(-32f, 4.78f, zMid), new Vector3(width, 0.12f, depth));
            // Two tie rods from the front corners up to plates on the solid facade above the shutter bay
            for (int k = 0; k < 4; k++)
            {
                float x = k == 0 ? xW + 0.4f : (k == 1 ? -34.2f : (k == 2 ? -29.8f : xE - 0.4f));
                Vector3 lo = new Vector3(x, 4.68f, zFront + 0.15f), hi = new Vector3(x, 8.6f, zFace - 0.05f);
                Vector3 d = hi - lo;
                Box(c.transform, $"CanopyTieRod_{k}", (lo + hi) * 0.5f, new Vector3(0.08f, 0.08f, d.magnitude), mSteel, false, Quaternion.LookRotation(d).eulerAngles);
                Box(c.transform, $"CanopyTiePlate_{k}", new Vector3(x, 8.6f, zFace - 0.015f), new Vector3(0.24f, 0.32f, 0.03f), mDarkMetal);
                Box(c.transform, $"CanopyTieShoe_{k}", new Vector3(x, 4.71f, zFront + 0.15f), new Vector3(0.14f, 0.06f, 0.2f), mDarkMetal);
            }
            // Welcome sign standing on the front edge of the canopy
            Box(c.transform, "WelcomeSign_Back", new Vector3(-32f, 5.4f, zFront + 0.2f), new Vector3(5.9f, 1.55f, 0.08f), mDarkMetal);
            Poster(c.transform, p, "WelcomeSign", "lobby_welcome", new Vector3(-32f, 5.4f, zFront + 0.16f), Vector3.back, new Vector2(5.6f, 1.4f), frame: false, emissive: true, emissiveIntensity: 6f);
            Poster(c.transform, p, "Facade_ParkLogo", "park_logo", new Vector3(-31.5f, 7.9f, zFace), Vector3.back, new Vector2(3.2f, 3.2f), frame: false);
            for (int i = 0; i < 4; i++)
                CeilingLamp(c.transform, $"Light_Canopy_Down_{i}", new Vector3(-35.6f + i * 2.4f, 4.38f, zMid - 0.1f), LightGroupId.Plaza, 3000f, shadows: false);
        }

                // =========================================================================
        // LOOK PASS: zone materials and dressing helpers
        // Everything here is built from the palette's world-space (triplanar) surfaces, so any box of any size shows its
        // texture at real scale. Furniture is assembled from parts (tops, legs, plinths, seams) instead of solid slabs.
        // =========================================================================

        private static Material mWainscot, mVeneer, mLaminate, mCabinet, mBlack, mPaper, mCork, mVendRed, mKerb, mFeatureGreen;
        private static Material mOfficePaint, mShutterPaint, mPlazaStrip, mWater, mTubeGlow;
        private static Material mFloorTile, mCeilTile, mGlass, mOpal, mEnamel, mScreenCyan, mScreenFeed, mSage;
        private static Material mPlazaPaving, mVendGlow, mBinderBlue, mBinderRed, mBinderBlack, mFabric, mSteel, mPaintYellow, mDarkMetal;

        private static void InitZoneMaterials(WorldMaterialPalette p)
        {
            mSteel        = p.BrushedSteel;
            mPaintYellow  = p.PaintedMetalYellow;
            mDarkMetal    = p.DarkPaintedMetal;
            mPlazaPaving  = p.Surface("MAT_Z1_PlazaPaving", "paving_stone", "BaseColor", "MaskMap", 1.25f, new Color(0.95f, 0.97f, 1.0f), 0.25f, 0.86f, 0f, 1.0f);
            // Neutral office paint (albedo ~0.42/0.39/0.35): the palette's DrywallPaint is beige (red/blue 1.8) and turns the
            // offices amber-monochrome under 3500-4300 K lamps. Used on office/corridor walls, partitions and linings.
            mOfficePaint  = p.Surface("MAT_Z1_OfficePaint", "drywall_paint", "BaseColor", "MaskMap", 3f, new Color(1.10f, 1.20f, 1.32f), 0.10f, 0.42f, 0f, 0.8f);
            // Grey painted roller shutter (the palette's CorrugatedMetal shares the rust texture and glows red-orange at the lobby's focal point)
            mShutterPaint = p.Surface("MAT_Z1_ShutterPaint", "metal_painted_worn", "grey_BaseColor", "MaskMap", 1.2f, new Color(0.62f, 0.65f, 0.68f), 0.25f, 0.62f, 0.35f, 1.0f);
            // Darker paving strips: a neutral grey concrete (the kerb concrete at tint 2.1 read light yellow)
            mPlazaStrip   = p.Surface("MAT_Z1_PlazaStrip", "concrete_cracked_ext", "BaseColor", "MaskMap", 2.0f, new Color(0.62f, 0.64f, 0.66f), 0.25f, 0.85f, 0f, 1.0f);
            // Standing water on the lobby floor: dark (tint ~0.35) and mirror smooth, so it reads against the polished concrete
            mWater        = p.Surface("MAT_Z1_StandingWater", "concrete_polished", "BaseColor", "MaskMap", 2.0f, new Color(0.50f, 0.52f, 0.55f), 0.96f, 0.98f, 0f, 0.15f);
            mTubeGlow     = p.GetOrBuildEmissive("MAT_Z1_CorridorTube", new Color(0.86f, 0.92f, 1.0f), 70f);
            mWainscot     = p.Surface("MAT_Z1_WainscotPaint", "drywall_paint", "BaseColor", "MaskMap", 3.0f, new Color(0.62f, 0.78f, 1.05f), 0.18f, 0.55f, 0f, 0.6f);
            mFeatureGreen = p.Surface("MAT_Z1_FeaturePaintGreen", "drywall_paint", "BaseColor", "MaskMap", 3.0f, new Color(0.27f, 0.41f, 0.35f), 0.12f, 0.45f, 0f, 0.6f);
            mVeneer       = p.Surface("MAT_Z1_WoodVeneer", "wood_planks_weathered", "BaseColor", "MaskMap", 1.1f, new Color(0.95f, 0.80f, 0.66f), 0.30f, 0.70f, 0f, 0.30f);
            mLaminate     = p.Surface("MAT_Z1_Laminate", "drywall_paint", "BaseColor", "MaskMap", 2.0f, new Color(1.25f, 1.24f, 1.20f), 0.45f, 0.80f, 0f, 0.15f);
            mCabinet      = p.Surface("MAT_Z1_CabinetPaint", "metal_painted_worn", "grey_BaseColor", "MaskMap", 1.2f, new Color(0.92f, 0.93f, 0.92f), 0.30f, 0.72f, 0.20f, 0.5f);
            mBlack        = p.Surface("MAT_Z1_BlackPlastic", "metal_painted_worn", "grey_BaseColor", "MaskMap", 1.0f, new Color(0.13f, 0.13f, 0.14f), 0.30f, 0.62f, 0f, 0.3f);
            mPaper        = p.Surface("MAT_Z1_Paper", "drywall_paint", "BaseColor", "MaskMap", 0.8f, new Color(1.75f, 1.72f, 1.62f), 0.05f, 0.25f, 0f, 0.1f);
            mCork         = p.Surface("MAT_Z1_Cork", "carpet_office", "BaseColor", "MaskMap", 0.5f, new Color(1.9f, 1.25f, 0.75f), 0.0f, 0.2f, 0f, 0.8f);
            mVendRed      = p.Surface("MAT_Z1_VendingRed", "metal_painted_worn", "grey_BaseColor", "MaskMap", 1.4f, new Color(1.25f, 0.22f, 0.15f), 0.35f, 0.82f, 0.20f, 0.5f);
            mKerb         = p.Surface("MAT_Z1_KerbConcrete", "concrete_cracked_ext", "BaseColor", "MaskMap", 2.0f, new Color(2.1f, 2.1f, 2.05f), 0.20f, 0.80f, 0f, 1.0f);
            mBinderBlue   = p.Surface("MAT_Z1_BinderBlue", "metal_painted_worn", "grey_BaseColor", "MaskMap", 0.6f, new Color(0.30f, 0.42f, 0.80f), 0.30f, 0.60f, 0f, 0.2f);
            mBinderRed    = p.Surface("MAT_Z1_BinderRed", "metal_painted_worn", "grey_BaseColor", "MaskMap", 0.6f, new Color(0.85f, 0.25f, 0.20f), 0.30f, 0.60f, 0f, 0.2f);
            mBinderBlack  = p.Surface("MAT_Z1_BinderBlack", "metal_painted_worn", "grey_BaseColor", "MaskMap", 0.6f, new Color(0.22f, 0.22f, 0.24f), 0.30f, 0.60f, 0f, 0.2f);
            mFabric       = p.Surface("MAT_Z1_SeatFabric", "carpet_office", "BaseColor", "MaskMap", 0.8f, new Color(0.75f, 0.90f, 1.15f), 0.0f, 0.2f, 0f, 0.8f);
            mVendGlow     = p.GetOrBuildEmissive("MAT_Z1_VendingGlow", new Color(1.0f, 0.92f, 0.80f), 14f);   // a lit back panel, not a lamp (60 nits bloomed white)
            // Review 2 neutral interiors: the textures are warm (drywall red/blue 1.31, floor tile 1.12); under 4300 K tubes
            // the rooms read sepia, so the zone's floor and ceiling carry a slight cool tint that cancels the texture.
            mFloorTile    = p.Surface("MAT_Z1_FloorTile", "floor_tiles_industrial", "BaseColor", "MaskMap", 2.4f, new Color(1.38f, 1.47f, 1.60f), 0.25f, 0.82f, 0f, 1.0f);
            mCeilTile     = p.Surface("MAT_Z1_CeilingTile", "ceiling_tiles", "BaseColor", "MaskMap", 2.4f, new Color(0.96f, 0.98f, 1.02f), 0.05f, 0.30f, 0f, 1.0f);
            mEnamel       = p.Surface("MAT_Z1_FixtureEnamel", "drywall_paint", "BaseColor", "MaskMap", 1.0f, new Color(1.42f, 1.52f, 1.66f), 0.45f, 0.62f, 0f, 0.2f);
            mOpal         = p.Surface("MAT_Z1_OpalDiffuser", "drywall_paint", "BaseColor", "MaskMap", 0.6f, new Color(1.50f, 1.60f, 1.74f), 0.55f, 0.70f, 0f, 0.05f);
            mGlass        = BuildWindowGlass(p);
            // staff exit: institutional sage instead of the lime peeling paint, cracks at real scale (review 1 minor)
            // round 3 (review 2, 08_ch1_staff_exit_collapse: "giant crackle wallpaper", red-brown blotches repeating in a grid):
            // the white-paint variant of the set (no rust blotches) tinted sage, at a 1.25 m tile with a softer crack normal;
            // irregular leak streaks and scuffs on the walls (DressStaffExitWalls) break the remaining repeat
            // fix round: the peeling set still repeated its blotches and crack cells every 1.25 m down a 22 m wall, so the field
            // is now plain sage paint on the drywall set (3 m tile, no pattern to repeat) and all the decay is in decals at
            // irregular spots (BuildWestRoomsAndStaffExit: cracks with random roll and size, leak streaks)
            mSage         = p.Surface("MAT_Z1_SagePlaster", "drywall_paint", "BaseColor", "MaskMap", 3f, new Color(0.62f, 0.74f, 0.64f), 0.08f, 0.40f, 0f, 0.5f);
            mScreenCyan   = p.CreateTextureQuadMaterial("MAT_Z1_FeedJungle", "backdrop_jungle_aerial", true, 3.2f, Color.white);   // a camera feed
            mScreenFeed   = BuildFeedScreen(p);
            InitDressMaterials(p);
            InitRound4Materials(p);
        }

        // =========================================================================
        // REVIEW 2 DRESSING: helpers for a 1990s park building (furniture in groups, shelving, paper, ceiling and wall
        // detail). Small clutter has no collider; anything a player could walk into keeps a box collider.
        // =========================================================================

        private static Material mCardboard, mBottleBlue, mTerracotta, mSoil;
        private static WorldMaterialPalette s_palette;

        private static void InitDressMaterials(WorldMaterialPalette p)
        {
            mCardboard  = p.Surface("MAT_Z1_Cardboard", "drywall_paint", "BaseColor", "MaskMap", 0.9f, new Color(1.08f, 1.06f, 1.00f), 0.05f, 0.25f, 0f, 0.4f);
            mBottleBlue = p.Surface("MAT_Z1_BottleBlue", "concrete_polished", "BaseColor", "MaskMap", 1.0f, new Color(0.55f, 0.85f, 1.35f), 0.85f, 0.95f, 0f, 0.05f);
            mTerracotta = p.Surface("MAT_Z1_Terracotta", "concrete_wall", "BaseColor", "MaskMap", 0.8f, new Color(1.11f, 0.85f, 0.73f), 0.10f, 0.30f, 0f, 0.6f);
            mSoil       = p.MudGround;
        }

        private static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.layer = GameLayers.Environment;
            return go;
        }

        /// <summary>A printed sheet (texture quad) in a group's local space; facePlusZ = visible from local +Z.</summary>
        private static void PrintQuad(Transform t, WorldMaterialPalette p, string name, string stem, Vector3 localPos, Vector2 size, bool facePlusZ, float roll = 0f)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(t, false);
            quad.transform.localPosition = localPos;
            quad.transform.localRotation = Quaternion.Euler(0f, facePlusZ ? 180f : 0f, roll);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = p.CreateTextureQuadMaterial($"MAT_Z1_Print_{stem}", stem, false, 1f, Color.white);
            quad.layer = GameLayers.Environment;
        }

        // generic notices only: the story documents (memo_switchover, keeper_note, log_halvorsen, id_badge) are chapter-1
        // collectibles and must not hang on the boards as non-interactive copies (review 2)
        private static readonly string[] NoticeArt = { "fire_assembly", "restroom", "staff_only", "talon_ridge_warning", "high_voltage" };

        /// <summary>Open steel office shelving with binders and labelled archive boxes. floorPos = front centre, the
        /// front faces local -Z (yaw it so -Z looks into the room), depth 0.4 m behind the front.</summary>
        private static void OfficeShelf(Transform parent, WorldMaterialPalette p, string name, Vector3 floorPos, float yaw, float len, int seed, int levels = 5, float height = 2.0f)
        {
            // review 2: the box-built shelf read as primitives; the modelled archive_shelf (steel frame with archive boxes)
            // replaces it. The model's front is its local +Z, the callers' front is local -Z, hence the extra 180 deg.
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            p.InstantiatePropOrFallback("archive_shelf", parent, floorPos + rot * new Vector3(0f, 0f, 0.25f), new Vector3(0f, yaw + 180f + (seed % 3 - 1) * 0.8f, 0f),
                new Vector3(len, height, 0.5f), grounded: true, addCollider: true, fallbackMat: mDarkMetal, customName: name);
        }

        /// <summary>Ceiling detail over a room: smoke detectors, sprinkler heads and return-air grilles, placed on a
        /// half-offset grid so they sit between the troffers.</summary>
        private static void CeilingDetails(Transform parent, string name, float x0, float x1, float z0, float z1, float ceilY, int seed)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rng = new System.Random(seed);
            int i = 0;
            for (float x = x0 + 2.2f; x < x1 - 1.0f; x += 4.4f)
            for (float z = z0 + 2.4f; z < z1 - 1.0f; z += 3.6f, i++)
            {
                Vector3 c = new Vector3(x + 0.75f, ceilY, z - 0.6f);
                int kind = (i + seed) % 3;
                if (kind == 0)
                {   // smoke detector
                    Prim(root.transform, $"Smoke_{i}", PrimitiveType.Cylinder, c + new Vector3(0f, -0.02f, 0f), new Vector3(0.13f, 0.02f, 0.13f), mPaper);
                }
                else if (kind == 1)
                {   // sprinkler head: stem + deflector
                    Prim(root.transform, $"Sprinkler_{i}", PrimitiveType.Cylinder, c + new Vector3(0f, -0.03f, 0f), new Vector3(0.03f, 0.03f, 0.03f), mSteel);   // sub-cm plates and roses dropped (perf)
                }
                else
                {   // return-air grille
                    Box(root.transform, $"Grille_{i}", c + new Vector3(0f, -0.006f, 0f), new Vector3(0.6f, 0.012f, 0.6f), mEnamel);
                    for (int s = 0; s < 6; s++)
                        Box(root.transform, $"GrilleSlot_{i}_{s}", c + new Vector3(-0.22f + s * 0.088f, -0.0125f, 0f), new Vector3(0.05f, 0.002f, 0.5f), mBlack);
                }
            }
        }

        /// <summary>Light switch / socket plates on a wall. pos = point on the wall face, facing = into the room.</summary>
        private static void WallPlate(Transform parent, string name, Vector3 pos, Vector3 facing, bool socket)
        {
            Transform t = Group(parent, name, pos, YawFacing(facing) + 180f);   // local +Z into the room
            Box(t, "Plate", new Vector3(0f, 0f, 0.004f), socket ? new Vector3(0.14f, 0.08f, 0.008f) : new Vector3(0.08f, 0.12f, 0.008f), mPaper);
            if (socket) { Box(t, "SlotA", new Vector3(-0.03f, 0f, 0.0085f), new Vector3(0.012f, 0.03f, 0.002f), mBlack); Box(t, "SlotB", new Vector3(0.03f, 0f, 0.0085f), new Vector3(0.012f, 0.03f, 0.002f), mBlack); }
            else Box(t, "Rocker", new Vector3(0f, 0f, 0.01f), new Vector3(0.025f, 0.05f, 0.006f), mPaper);
        }

        private static void WallClock(Transform parent, string name, Vector3 pos, Vector3 facing, float handA = 35f, float handB = -10f)
        {
            Transform t = Group(parent, name, pos, YawFacing(facing) + 180f);
            Prim(t, "Rim", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.02f), new Vector3(0.34f, 0.02f, 0.34f), mBlack, new Vector3(90f, 0f, 0f));
            Prim(t, "Face", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.041f), new Vector3(0.30f, 0.002f, 0.30f), mPaper, new Vector3(90f, 0f, 0f));
            Box(t, "HandH", new Vector3(0.025f, 0.015f, 0.046f), new Vector3(0.08f, 0.012f, 0.004f), mBlack, false, new Vector3(0f, 0f, handA));
            Box(t, "HandM", new Vector3(-0.01f, 0.05f, 0.047f), new Vector3(0.008f, 0.12f, 0.004f), mBlack, false, new Vector3(0f, 0f, handB));
        }

        /// <summary>Bottled water cooler: white cabinet, blue bottle, two taps. floorPos = centre, front faces local -Z.</summary>
        private static void WaterCooler(Transform parent, string name, Vector3 floorPos, float yaw)
        {
            Transform t = Group(parent, name, floorPos, yaw);
            Box(t, "Cabinet", new Vector3(0f, 0.5f, 0f), new Vector3(0.32f, 1.0f, 0.32f), mEnamel, true);
            Box(t, "Plinth", new Vector3(0f, 0.03f, -0.005f), new Vector3(0.30f, 0.06f, 0.30f), mDarkMetal);
            Box(t, "TopCollar", new Vector3(0f, 1.01f, 0f), new Vector3(0.26f, 0.02f, 0.26f), mCabinet);
            // round 4 (review 3: 'the blue and red tap cubes over the black drip tray read as a cartoon face'): one recessed
            // grey tap panel with two small white push taps (only a thin coloured ring on each), a steel grille drip tray,
            // a ribbed bottle with shoulders, and a cup tube on the side
            Box(t, "TapRecess", new Vector3(0f, 0.74f, -0.155f), new Vector3(0.24f, 0.22f, 0.012f), mDarkMetal);
            Box(t, "TapHood", new Vector3(0f, 0.86f, -0.17f), new Vector3(0.26f, 0.03f, 0.04f), mEnamel);
            for (int k = 0; k < 2; k++)
            {
                float x = k == 0 ? -0.055f : 0.055f;
                Prim(t, $"Tap_{k}", PrimitiveType.Cylinder, new Vector3(x, 0.80f, -0.172f), new Vector3(0.028f, 0.012f, 0.028f), mPaper, new Vector3(90f, 0f, 0f));
                Box(t, $"TapLever_{k}", new Vector3(x, 0.775f, -0.182f), new Vector3(0.022f, 0.035f, 0.01f), mPaper);
                Prim(t, $"TapRing_{k}", PrimitiveType.Cylinder, new Vector3(x, 0.80f, -0.185f), new Vector3(0.012f, 0.002f, 0.012f), k == 0 ? mBinderBlue : mBinderRed, new Vector3(90f, 0f, 0f));
            }
            Box(t, "DripTray", new Vector3(0f, 0.645f, -0.19f), new Vector3(0.22f, 0.025f, 0.07f), mSteel);
            for (int g = 0; g < 6; g++) Box(t, $"DripSlot_{g}", new Vector3(-0.075f + g * 0.03f, 0.6585f, -0.19f), new Vector3(0.012f, 0.002f, 0.055f), mDarkMetal);
            Prim(t, "Bottle", PrimitiveType.Cylinder, new Vector3(0f, 1.27f, 0f), new Vector3(0.27f, 0.17f, 0.27f), mBottleBlue);
            Prim(t, "BottleShoulder", PrimitiveType.Sphere, new Vector3(0f, 1.10f, 0f), new Vector3(0.27f, 0.12f, 0.27f), mBottleBlue);
            Prim(t, "BottleBase", PrimitiveType.Sphere, new Vector3(0f, 1.44f, 0f), new Vector3(0.27f, 0.06f, 0.27f), mBottleBlue);
            Prim(t, "BottleNeck", PrimitiveType.Cylinder, new Vector3(0f, 1.035f, 0f), new Vector3(0.08f, 0.03f, 0.08f), mBottleBlue);
            for (int rb = 0; rb < 3; rb++) Prim(t, $"BottleRib_{rb}", PrimitiveType.Cylinder, new Vector3(0f, 1.19f + rb * 0.08f, 0f), new Vector3(0.276f, 0.006f, 0.276f), mBottleBlue);
            Prim(t, "CupTube", PrimitiveType.Cylinder, new Vector3(0.19f, 0.78f, -0.02f), new Vector3(0.075f, 0.16f, 0.075f), mSteel);
            Prim(t, "CupStack", PrimitiveType.Cylinder, new Vector3(0.19f, 0.6f, -0.02f), new Vector3(0.068f, 0.03f, 0.068f), mPaper);
        }

        /// <summary>Potted plant: terracotta or black pot, soil, a tropical plant model. floorPos = pot centre.</summary>
        private static void PottedPlant(Transform parent, WorldMaterialPalette p, string name, Vector3 floorPos, string stem, float potD, float plantSize, int seed)
        {
            Transform t = Group(parent, name, floorPos, seed * 37f);
            float h = potD * 0.9f;
            Prim(t, "Pot", PrimitiveType.Cylinder, new Vector3(0f, h * 0.5f, 0f), new Vector3(potD, h * 0.5f, potD), seed % 2 == 0 ? mTerracotta : mBlack);
            Prim(t, "Soil", PrimitiveType.Cylinder, new Vector3(0f, h - 0.03f, 0f), new Vector3(potD * 0.9f, 0.01f, potD * 0.9f), mSoil);
            BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, h * 0.5f, 0f); bc.size = new Vector3(potD, h, potD);
            p.InstantiatePropOrFallback(stem, t, floorPos + new Vector3(0f, h - 0.03f, 0f), new Vector3(0f, seed * 53f, 0f), new Vector3(plantSize, plantSize, plantSize), grounded: true, addCollider: false);
        }

        /// <summary>Two- or three-seat lounge sofa of fabric cushions. floorPos = centre, the sitter faces local -Z.</summary>
        private static void Sofa(Transform parent, string name, Vector3 floorPos, float yaw, float len)
        {
            Transform t = Group(parent, name, floorPos, yaw);
            BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.4f, 0f); bc.size = new Vector3(len, 0.8f, 0.85f);
            Box(t, "Base", new Vector3(0f, 0.2f, 0f), new Vector3(len, 0.28f, 0.85f), mFabric);
            int seats = Mathf.Max(2, Mathf.RoundToInt((len - 0.4f) / 0.6f));
            float sw = (len - 0.36f) / seats;
            for (int i = 0; i < seats; i++)
            {
                Box(t, $"Seat_{i}", new Vector3(-len * 0.5f + 0.18f + sw * (i + 0.5f), 0.41f, -0.06f), new Vector3(sw - 0.02f, 0.14f, 0.66f), mFabric, false, new Vector3(-2f, 0f, 0f));
                Box(t, $"Back_{i}", new Vector3(-len * 0.5f + 0.18f + sw * (i + 0.5f), 0.66f, 0.32f), new Vector3(sw - 0.02f, 0.42f, 0.18f), mFabric, false, new Vector3(-10f, 0f, 0f));
            }
            Box(t, "ArmL", new Vector3(-len * 0.5f + 0.09f, 0.45f, 0f), new Vector3(0.18f, 0.36f, 0.85f), mFabric);
            Box(t, "ArmR", new Vector3(len * 0.5f - 0.09f, 0.45f, 0f), new Vector3(0.18f, 0.36f, 0.85f), mFabric);
            for (int f = 0; f < 4; f++) Box(t, $"Foot_{f}", new Vector3((f % 2 == 0 ? -1f : 1f) * (len * 0.5f - 0.08f), 0.03f, (f < 2 ? -1f : 1f) * 0.35f), new Vector3(0.05f, 0.06f, 0.05f), mBlack);
        }

        /// <summary>Upright steel filing cabinet (4 drawers, labels and pulls) built from parts. floorPos = centre, the
        /// drawers face local -Z.</summary>
        private static void FilingCabinet(Transform parent, string name, Vector3 floorPos, float yaw, Material body)
        {
            // the modelled filing_cabinet (front = local +Z) instead of a box build; body is the fallback material
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            s_palette.InstantiatePropOrFallback("filing_cabinet", parent, floorPos + rot * new Vector3(0f, 0f, 0.02f), new Vector3(0f, yaw + 180f, 0f),
                new Vector3(0.47f, 1.33f, 0.62f), grounded: true, addCollider: true, fallbackMat: body, customName: name);
        }

        /// <summary>
        /// Review 2: the rooms were 3-6x real size and nearly empty ("test chambers"). Dress them to the density of a 1990s
        /// park building, in groups, without moving any gameplay geometry or blocking the routes (spawn -> CCTV desk ->
        /// terminal -> door_security; door_records -> records desk; the break room and lockers are dead ends).
        /// </summary>
        private static void DressRoomsReview2(Transform parent, WorldMaterialPalette p)
        {
            var root = new GameObject("Props_Review2");
            root.transform.SetParent(parent, false);
            Transform r = root.transform;

            // ---- painted cornice bands: the last 0.2 m under every lit ceiling (where a downward fixture's light only grazes
            //      the wall) is a white band with a small crown trim, so the ceiling and the top of the walls read as one lit room
            Material cornice = p.Surface("MAT_Z1_Cornice", "drywall_paint", "BaseColor", "MaskMap", 2.0f, new Color(1.30f, 1.36f, 1.46f), 0.12f, 0.40f, 0f, 0.3f);
            Cornice(r, "Sec_Cornice", -71.847f, -58.1f, 22.11f, 33.847f, CeilStd, cornice);
            Cornice(r, "Lock_Cornice", -57.9f, -48.1f, 22.11f, 33.847f, CeilStd, cornice);
            Cornice(r, "Ctl_Cornice", -47.9f, -26.1f, 22.11f, 33.847f, CeilStd, cornice);
            Cornice(r, "Rec_Cornice", -17.9f, -0.1f, 22.11f, 33.847f, CeilStd, cornice);
            Cornice(r, "Brk_Cornice", -71.847f, -50.173f, 6.153f, 18.89f, CeilStd - 0.012f, cornice);
            Cornice(r, "Gift_Cornice", -71.847f, -50.173f, -31.847f, -12.163f, CeilStd - 0.012f, cornice);

            // ---- Security office (x -71.85..-58.1, z 22.11..33.85) ----------------------------------------------------
            for (int i = 0; i < 3; i++) FilingCabinet(r, $"Sec_FileS_{i}", new Vector3(-71.3f + i * 0.5f, 0f, 22.45f), 180f, mCabinet);
            OfficeShelf(r, p, "Sec_ShelfW", new Vector3(-71.45f, 0f, 26.0f), -90f, 2.0f, 41);
            WaterCooler(r, "Sec_WaterCooler", new Vector3(-58.5f, 0f, 33.35f), 90f);
            Transform brief = Table(r, "Sec_BriefingTable", new Vector3(-68.6f, 0f, 25.4f), 0f, 1.8f, 0.9f, 0.75f, mLaminate, mSteel);
            Chair(r, "Sec_BriefChair_0", new Vector3(-69.1f, 0f, 24.65f), 172f);
            Chair(r, "Sec_BriefChair_1", new Vector3(-68.1f, 0f, 24.7f), 195f);
            Chair(r, "Sec_BriefChair_2", new Vector3(-68.8f, 0f, 26.2f), 8f);
            PaperStack(r, "Sec_BriefPapers", new Vector3(-68.9f, 0.75f, 25.35f), 14f, 3);
            Prim(r, "Sec_BriefMug", PrimitiveType.Cylinder, new Vector3(-68.0f, 0.80f, 25.5f), new Vector3(0.08f, 0.05f, 0.08f), mBinderRed);
            Box(r, "Sec_BriefRadio", new Vector3(-68.3f, 0.79f, 25.2f), new Vector3(0.07f, 0.08f, 0.04f), mBlack, false, new Vector3(0f, 30f, 0f));
            Poster(r, p, "Sec_ParkMap", "map_park_modern", new Vector3(-67.6f, 1.6f, 22.115f), Vector3.forward, new Vector2(1.6f, 1.0f));
            PottedPlant(r, p, "Sec_Plant", new Vector3(-67.4f, 0f, 33.3f), "calathea_orbifolia_01", 0.42f, 0.9f, 3);
            WallPlate(r, "Sec_Switch", new Vector3(-61.75f, 1.2f, 22.115f), Vector3.forward, false);
            WallPlate(r, "Sec_Socket_0", new Vector3(-66.0f, 0.3f, 22.115f), Vector3.forward, true);
            WallPlate(r, "Sec_Socket_1", new Vector3(-71.84f, 0.3f, 30.6f), Vector3.right, true);
            WallPlate(r, "Sec_Socket_2", new Vector3(-63.3f, 0.3f, 33.84f), Vector3.back, true);
            CeilingDetails(r, "Sec_Ceiling", -72f, -58f, 22f, 34f, CeilStd - 0.012f, 1);
            WallDecal(r, "Sec_DoorGrime", p.DecalGrimeLeak, new Vector3(-61.3f, 0.35f, 22.12f), Vector3.forward, new Vector2(1.2f, 0.6f), 0.5f);

            // ---- Locker room (x -57.9..-48.1, z 22.1..33.85) --------------------------------------------------------------
            for (int i = 0; i < 3; i++)
                p.InstantiatePropOrFallback("lockers", r, new Vector3(-48.62f, 0f, 29.6f + i * 1.36f), new Vector3(0f, -90f, 0f), new Vector3(2.0f, 2.05f, 0.58f));
            // kit bags: a flattened roll with two carry straps and an end pocket (review 3: 'a black capsule in the middle
            // of the room')
            for (int d = 0; d < 2; d++)
            {
                Vector3 at = d == 0 ? new Vector3(-53.6f, 0.60f, 30.6f) : new Vector3(-55.9f, 0.58f, 26.9f);
                float yaw = d == 0 ? 8f : 96f;
                Material body = d == 0 ? mBinderBlack : mFeatureGreen;
                Transform bag = Group(r, $"Lock_Duffel_{d}", at, yaw);
                Prim(bag, "Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.30f, 0.30f, 0.24f), body, new Vector3(0f, 0f, 90f));
                Box(bag, "StrapA", new Vector3(-0.13f, 0.005f, 0f), new Vector3(0.035f, 0.262f, 0.312f), mBlack);
                Box(bag, "StrapB", new Vector3(0.13f, 0.005f, 0f), new Vector3(0.035f, 0.262f, 0.312f), mBlack);
                Box(bag, "Handle", new Vector3(0f, 0.15f, 0f), new Vector3(0.30f, 0.018f, 0.03f), mBlack);
                Box(bag, "EndPocket", new Vector3(0.27f, -0.02f, 0f), new Vector3(0.03f, 0.16f, 0.2f), body);
                Box(bag, "ZipLine", new Vector3(0f, 0.122f, 0.05f), new Vector3(0.44f, 0.006f, 0.012f), mPaper);
            }
            FoldedTowel(r, "Lock_Towel", new Vector3(-52.3f, 0.455f, 30.62f), 12f);
            for (int b = 0; b < 3; b++)
            {
                // work boots: sole, toe cap, foot and a taller shaft (they were single black boxes)
                for (int side = 0; side < 2; side++)
                {
                    Transform boot = Group(r, side == 0 ? $"Lock_BootL_{b}" : $"Lock_BootR_{b}",
                        new Vector3((side == 0 ? -54.2f : -54.05f) + b * 0.9f, 0f, side == 0 ? 30.95f : 30.98f), side == 0 ? b * 9f : -b * 6f);
                    Box(boot, "Sole", new Vector3(0f, 0.012f, 0f), new Vector3(0.105f, 0.024f, 0.285f), mPaper);
                    Box(boot, "Foot", new Vector3(0f, 0.058f, 0.01f), new Vector3(0.098f, 0.07f, 0.26f), mBlack);
                    Prim(boot, "Toe", PrimitiveType.Sphere, new Vector3(0f, 0.062f, -0.105f), new Vector3(0.098f, 0.085f, 0.11f), mBlack);
                    Box(boot, "Shaft", new Vector3(0f, 0.165f, 0.075f), new Vector3(0.092f, 0.17f, 0.11f), mBlack, false, new Vector3(-5f, 0f, 0f));
                    Box(boot, "Cuff", new Vector3(0f, 0.253f, 0.082f), new Vector3(0.10f, 0.02f, 0.118f), mBinderBlack);
                }
            }
            Prim(r, "Lock_Hamper", PrimitiveType.Cylinder, new Vector3(-49.0f, 0.32f, 27.4f), new Vector3(0.5f, 0.32f, 0.5f), mFabric);
            Box(r, "Lock_MirrorFrame", new Vector3(-48.13f, 1.55f, 23.6f), new Vector3(0.02f, 0.9f, 0.7f), mSteel);
            Box(r, "Lock_Mirror", new Vector3(-48.15f, 1.55f, 23.6f), new Vector3(0.01f, 0.84f, 0.64f), mGlass);
            Box(r, "Lock_SinkCounter", new Vector3(-48.45f, 0.84f, 23.6f), new Vector3(0.6f, 0.05f, 0.9f), mLaminate, true);
            Box(r, "Lock_Sink", new Vector3(-48.5f, 0.83f, 23.6f), new Vector3(0.38f, 0.04f, 0.44f), mSteel);
            Box(r, "Lock_Tap", new Vector3(-48.28f, 0.95f, 23.6f), new Vector3(0.03f, 0.2f, 0.03f), mSteel);
            for (int l = 0; l < 2; l++) Box(r, $"Lock_SinkLeg_{l}", new Vector3(-48.6f, 0.41f, 23.25f + l * 0.7f), new Vector3(0.04f, 0.82f, 0.04f), mSteel);
            WallClock(r, "Lock_Clock", new Vector3(-53f, 2.65f, 22.115f), Vector3.forward, 120f, 70f);
            WallPlate(r, "Lock_Switch", new Vector3(-52.25f, 1.2f, 22.115f), Vector3.forward, false);
            CeilingDetails(r, "Lock_Ceiling", -58f, -48f, 22f, 34f, CeilStd - 0.012f, 2);
            DecalAt(r, "Lock_FloorWear", p.DecalGrimeLeak, new Vector3(-53.0f, 0.02f, 24.0f), 2.2f, 40f, 0.5f);

            // ---- Control room (x -47.9..-26.1, z 22.1..33.85) ------------------------------------------------------------
            FilingCabinet(r, "Ctl_File_0", new Vector3(-47.55f, 0f, 23.4f), -90f, mCabinet);
            FilingCabinet(r, "Ctl_File_1", new Vector3(-47.55f, 0f, 23.9f), -90f, mCabinet);
            Transform pc = Counter(r, "Ctl_PrinterCab", new Vector3(-27.0f, 0f, 31.8f), -90f, 1.2f, 0.6f, 0.8f, mLaminate, mCabinet);
            Box(pc, "Printer", new Vector3(0.1f, 0.92f, 0f), new Vector3(0.5f, 0.22f, 0.42f), mPaper);
            Box(pc, "PrinterTray", new Vector3(0.1f, 0.86f, -0.27f), new Vector3(0.34f, 0.02f, 0.14f), mBlack);
            Box(pc, "Fax", new Vector3(-0.4f, 0.87f, 0.02f), new Vector3(0.32f, 0.12f, 0.3f), mBlack);
            PaperStack(r, "Ctl_Printouts", new Vector3(-27.1f, 1.03f, 31.4f), 10f, 3);
            WallClock(r, "Ctl_Clock", new Vector3(-47.88f, 2.7f, 27.0f), Vector3.right, 40f, 140f);
            WaterCooler(r, "Ctl_WaterCooler", new Vector3(-26.55f, 0f, 23.2f), -90f);
            for (int c = 0; c < 8; c++)
            {
                float mx = -43.5f + (c % 4) * 4.3f + 0.75f, mz = (c < 4 ? 31.3f : 28.0f) - 0.25f;
                Prim(r, $"Ctl_Mug_{c}", PrimitiveType.Cylinder, new Vector3(mx, 0.86f, mz), new Vector3(0.08f, 0.05f, 0.08f), c % 3 == 0 ? mBinderRed : mPaper);
            }
            CeilingDetails(r, "Ctl_Ceiling", -48f, -26f, 22f, 34f, CeilStd - 0.012f, 3);

            // ---- Records annex (x -17.85..-0.1, z 22.11..33.85) ----------------------------------------------------------
            for (int i = 0; i < 4; i++) FilingCabinet(r, $"Rec_FileE_{i}", new Vector3(-0.45f, 0f, 29.7f + i * 0.5f), 90f, i % 2 == 0 ? mCabinet : mDarkMetal);
            OfficeShelf(r, p, "Rec_ShelfW", new Vector3(-17.45f, 0f, 24.6f), -90f, 2.6f, 51, 6, 2.2f);
            OfficeShelf(r, p, "Rec_ShelfS", new Vector3(-15.0f, 0f, 22.5f), 180f, 3.0f, 52, 6, 2.2f);
            Transform chest = Counter(r, "Rec_MapChest", new Vector3(-6.5f, 0f, 33.4f), 180f, 1.6f, 0.9f, 0.92f, mDarkMetal, mDarkMetal);
            for (int d = 0; d < 5; d++) Box(chest, $"Drawer_{d}", new Vector3(0f, 0.12f + d * 0.16f, -0.46f), new Vector3(1.5f, 0.012f, 0.01f), mBlack);
            Transform mapTop = Group(r, "Rec_MapOnChest", new Vector3(-6.5f, 0.927f, 33.4f), 0f);
            mapTop.localRotation = Quaternion.Euler(90f, 8f, 0f);   // lying flat on the chest, visible from above
            PrintQuad(mapTop, p, "Map", "map_park_modern", Vector3.zero, new Vector2(1.1f, 0.68f), false);
            Transform fiche = Table(r, "Rec_FicheTable", new Vector3(-0.8f, 0f, 24.6f), 90f, 1.2f, 0.7f, 0.74f, mLaminate, mSteel);
            // round 4 (review 3: "a plain grey cube"): a microfiche reader, base with the film stage, a column, a hood with its
            // slanted screen and two knobs; front (screen) to the west, into the room
            Transform fr4 = Group(r, "Rec_FicheReader", new Vector3(-0.72f, 0.74f, 24.6f), 90f);
            Box(fr4, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(0.46f, 0.1f, 0.42f), mCabinet);
            Box(fr4, "Stage", new Vector3(0f, 0.112f, -0.07f), new Vector3(0.40f, 0.025f, 0.24f), mBlack);
            Box(fr4, "StageGlass", new Vector3(0f, 0.126f, -0.07f), new Vector3(0.30f, 0.004f, 0.18f), mGlass);
            Box(fr4, "Column", new Vector3(0f, 0.26f, 0.13f), new Vector3(0.30f, 0.32f, 0.12f), mCabinet);
            Box(fr4, "Hood", new Vector3(0f, 0.50f, 0.03f), new Vector3(0.46f, 0.38f, 0.34f), mCabinet, false, new Vector3(-12f, 0f, 0f));
            Box(fr4, "Bezel", new Vector3(0f, 0.488f, -0.135f), new Vector3(0.42f, 0.32f, 0.012f), mBlack, false, new Vector3(-12f, 0f, 0f));
            Box(fr4, "Screen", new Vector3(0f, 0.49f, -0.142f), new Vector3(0.36f, 0.27f, 0.004f), mScreenFeed, false, new Vector3(-12f, 0f, 0f));
            for (int v = 0; v < 5; v++) Box(fr4, $"Vent_{v}", new Vector3(0.231f, 0.56f - v * 0.03f, 0.08f), new Vector3(0.004f, 0.012f, 0.18f), mBlack, false, new Vector3(-12f, 0f, 0f));
            for (int k = 0; k < 2; k++) Prim(fr4, $"Knob_{k}", PrimitiveType.Cylinder, new Vector3(-0.15f + k * 0.3f, 0.055f, -0.215f), new Vector3(0.035f, 0.012f, 0.035f), mBlack, new Vector3(90f, 0f, 0f));
            for (int b = 0; b < 3; b++) Box(r, $"Rec_FloorBox_A{b}", new Vector3(-7.55f, 0.13f + b * 0.26f, 26.7f + (b % 2) * 0.05f), new Vector3(0.42f, 0.25f, 0.34f), mCardboard, b == 0, new Vector3(0f, b * 11f - 5f, 0f));
            for (int b = 0; b < 2; b++) Box(r, $"Rec_FloorBox_B{b}", new Vector3(-7.6f, 0.13f + b * 0.26f, 29.5f), new Vector3(0.42f, 0.25f, 0.34f), mCardboard, b == 0, new Vector3(0f, b * -14f, 0f));
            Transform lad = Group(r, "Rec_StepLadder", new Vector3(-11.2f, 0f, 30.75f), 70f);
            Box(lad, "RailL", new Vector3(-0.22f, 0.62f, 0.1f), new Vector3(0.04f, 1.28f, 0.05f), mPaintYellow, false, new Vector3(-12f, 0f, 0f));
            Box(lad, "RailR", new Vector3(0.22f, 0.62f, 0.1f), new Vector3(0.04f, 1.28f, 0.05f), mPaintYellow, false, new Vector3(-12f, 0f, 0f));
            for (int st = 0; st < 4; st++) Box(lad, $"Step_{st}", new Vector3(0f, 0.25f + st * 0.3f, 0.16f - st * 0.065f), new Vector3(0.44f, 0.03f, 0.12f), mSteel);
            Box(lad, "BackL", new Vector3(-0.22f, 0.6f, 0.45f), new Vector3(0.035f, 1.22f, 0.04f), mPaintYellow, false, new Vector3(14f, 0f, 0f));
            Box(lad, "BackR", new Vector3(0.22f, 0.6f, 0.45f), new Vector3(0.035f, 1.22f, 0.04f), mPaintYellow, false, new Vector3(14f, 0f, 0f));
            WallClock(r, "Rec_Clock", new Vector3(-4.0f, 2.65f, 22.115f), Vector3.forward, 200f, 15f);
            Transform sort = Table(r, "Rec_SortingTable", new Vector3(-5.2f, 0f, 31.0f), 0f, 2.4f, 1.0f, 0.76f, mVeneer, mSteel);
            for (int b = 0; b < 4; b++) Box(r, $"Rec_SortBox_{b}", new Vector3(-6.0f + b * 0.55f, 0.89f, 31.1f + (b % 2) * 0.12f), new Vector3(0.42f, 0.25f, 0.34f), mCardboard, false, new Vector3(0f, b * 13f - 10f, 0f));
            // printed end labels on the loose archive boxes (records-room label art)
            var boxes = new List<Transform>();
            foreach (Transform bx in r)
                if (bx.name.StartsWith("Rec_SortBox_") || bx.name.StartsWith("Rec_FloorBox_")) boxes.Add(bx);
            foreach (Transform bx in boxes)
            {
                Transform lab = Group(r, bx.name + "_Label", bx.position, bx.eulerAngles.y);
                PrintQuad(lab, p, "Label", "records", new Vector3(0f, 0.02f, -0.172f), new Vector2(0.2f, 0.07f), false);
            }
            PaperStack(r, "Rec_SortPapers", new Vector3(-4.6f, 0.76f, 30.75f), -18f, 4);
            PaperStack(r, "Rec_SortPapers2", new Vector3(-4.1f, 0.76f, 31.2f), 25f, 3);
            Chair(r, "Rec_SortChair", new Vector3(-5.0f, 0f, 30.2f), 170f);
            OfficeShelf(r, p, "Rec_ShelfN", new Vector3(-3.4f, 0f, 33.45f), 0f, 2.4f, 53, 6, 2.2f);
            CorkBoard(r, "Rec_NoticeE", new Vector3(-0.115f, 1.6f, 27.3f), Vector3.left, 1.4f, 0.9f, 29);
            p.InstantiatePropOrFallback("utility_cart", r, new Vector3(-1.3f, 0f, 27.6f), new Vector3(0f, 80f, 0f), new Vector3(1.0f, 0.9f, 0.6f));
            WallPlate(r, "Rec_Switch", new Vector3(-8.25f, 1.2f, 22.115f), Vector3.forward, false);
            CeilingDetails(r, "Rec_Ceiling", -18f, 0f, 22f, 34f, CeilStd - 0.012f, 4);
            DecalAt(r, "Rec_FloorWear", p.DecalGrimeLeak, new Vector3(-7.0f, 0.02f, 24.5f), 2.6f, 120f, 0.5f);

            // ---- Break room (x -71.85..-50.17, z 6.15..18.89) ------------------------------------------------------------
            for (int i = 0; i < 2; i++)
            {
                float tx = i == 0 ? -66f : -56.6f;
                Table(r, $"Brk_Table2_{i}", new Vector3(tx, 0f, 16.0f), 0f, 1.4f, 0.8f, 0.75f, mLaminate, mSteel);
                Chair(r, $"Brk_Chair2_{i}_S", new Vector3(tx - 0.25f, 0f, 15.25f), 176f + i * 9f);
                Chair(r, $"Brk_Chair2_{i}_N", new Vector3(tx + 0.3f, 0f, 16.8f), -6f - i * 12f);
                Chair(r, $"Brk_Chair2_{i}_E", new Vector3(tx + 0.95f, 0f, 16.1f), 95f);
                Prim(r, $"Brk_Mug2_{i}", PrimitiveType.Cylinder, new Vector3(tx - 0.3f, 0.80f, 16.1f), new Vector3(0.08f, 0.05f, 0.08f), i == 0 ? mPaper : mBinderBlue);
            }
            Sofa(r, "Brk_Sofa", new Vector3(-68.4f, 0f, 6.62f), 180f, 2.1f);
            Transform ct = Table(r, "Brk_CoffeeTable", new Vector3(-68.4f, 0f, 7.85f), 0f, 1.0f, 0.5f, 0.42f, mVeneer, mBlack);
            PaperStack(r, "Brk_Magazines", new Vector3(-68.6f, 0.42f, 7.85f), 22f, 3);
            PottedPlant(r, p, "Brk_Plant_0", new Vector3(-71.3f, 0f, 6.7f), "monstera_clump", 0.5f, 1.1f, 6);
            PottedPlant(r, p, "Brk_Plant_1", new Vector3(-50.7f, 0f, 6.7f), "anthurium_botany_01", 0.38f, 0.7f, 7);
            WaterCooler(r, "Brk_WaterCooler", new Vector3(-50.6f, 0f, 17.7f), 90f);
            CorkBoard(r, "Brk_NoticeN", new Vector3(-66.0f, 1.55f, 18.885f), Vector3.back, 1.5f, 0.95f, 17);
            WallClock(r, "Brk_Clock", new Vector3(-56.0f, 2.6f, 18.885f), Vector3.back, 300f, 200f);
            // drinks machine beside the snack machine: blue cabinet, lit front panel with bottle rows (VCMain)
            Transform dm = Group(r, "Brk_DrinksMachine", new Vector3(-71.42f, 0f, 10.6f), -90f);
            BoxCollider dbc = dm.gameObject.AddComponent<BoxCollider>();
            dbc.center = new Vector3(0f, 0.95f, 0f); dbc.size = new Vector3(0.95f, 1.9f, 0.8f);
            Box(dm, "Body", new Vector3(0f, 0.95f, 0.02f), new Vector3(0.95f, 1.9f, 0.76f), mBinderBlue);
            GameObject dglow = Box(dm, "Panel", new Vector3(-0.1f, 1.15f, -0.365f), new Vector3(0.6f, 1.2f, 0.01f), mVendGlow);
            for (int rr = 0; rr < 4; rr++)
            for (int cc = 0; cc < 5; cc++)
                Prim(dm, $"Bottle_{rr}_{cc}", PrimitiveType.Cylinder, new Vector3(-0.34f + cc * 0.12f, 0.72f + rr * 0.28f, -0.375f), new Vector3(0.06f, 0.09f, 0.06f), (rr + cc) % 3 == 0 ? mBinderRed : ((rr + cc) % 3 == 1 ? mFeatureGreen : mBottleBlue));
            Box(dm, "Selector", new Vector3(0.33f, 1.2f, -0.375f), new Vector3(0.16f, 0.5f, 0.02f), mBlack);
            Box(dm, "Flap", new Vector3(-0.1f, 0.3f, -0.38f), new Vector3(0.55f, 0.16f, 0.02f), mBlack);
            WorldArchitectureKit.AttachLightGroupMember(dglow, LightGroupId.VCMain, null, new Renderer[] { dglow.GetComponent<MeshRenderer>() });
            // kettle, fruit bowl, paper towels on the kitchenette (counter top 0.9 m at x -50.5, z 10..14.4)
            Prim(r, "Brk_Kettle", PrimitiveType.Cylinder, new Vector3(-50.55f, 1.0f, 13.6f), new Vector3(0.16f, 0.1f, 0.16f), mSteel);
            Prim(r, "Brk_FruitBowl", PrimitiveType.Sphere, new Vector3(-50.55f, 0.93f, 11.2f), new Vector3(0.26f, 0.08f, 0.26f), mPaper);
            Prim(r, "Brk_Towels", PrimitiveType.Cylinder, new Vector3(-50.35f, 1.03f, 14.1f), new Vector3(0.11f, 0.13f, 0.11f), mPaper, new Vector3(0f, 0f, 0f));
            CeilingDetails(r, "Brk_Ceiling", -72f, -50f, 6f, 19f, CeilStd - 0.024f, 5);
            DecalAt(r, "Brk_FloorWear", p.DecalGrimeLeak, new Vector3(-61.0f, 0.02f, 17.6f), 2.0f, 10f, 0.45f);

            // ---- Gift shop (x -71.85..-50.17, z -31.85..-12.16) ----------------------------------------------------------
            ShirtRack(r, p, "Gift_ShirtRack_0", new Vector3(-53.6f, 0f, -20.5f), 11);
            ShirtRack(r, p, "Gift_ShirtRack_1", new Vector3(-53.6f, 0f, -25.5f), 12);
            PlushBin(r, "Gift_PlushBin_0", new Vector3(-59.2f, 0f, -17.2f), 21);
            PlushBin(r, "Gift_PlushBin_1", new Vector3(-64.8f, 0f, -27.6f), 22);
            CardSpinner(r, p, "Gift_Spinner", new Vector3(-64.5f, 0f, -14.6f));
            // review 2: the middle of the shop was bare floor. A nesting pair of feature tables with stacked boxed
            // merchandise (printed box art on the faces) between the gondolas and the checkout.
            for (int ft = 0; ft < 2; ft++)
            {
                float w = ft == 0 ? 1.4f : 1.0f, h = ft == 0 ? 0.78f : 0.56f;
                Transform tb = Table(r, $"Gift_FeatureTable_{ft}", new Vector3(-62.2f + ft * 1.15f, 0f, -18.2f - ft * 0.4f), ft * 12f - 6f, w, w * 0.7f, h, mVeneer, mDarkMetal);
                int n = ft == 0 ? 6 : 4;
                for (int bx = 0; bx < n; bx++)
                {
                    int layer = bx / (n / 2);
                    float x = -w * 0.3f + (bx % (n / 2)) * (w * 0.6f / Mathf.Max(1, n / 2 - 1));
                    Transform box = Group(tb, $"Merch_{bx}", tb.TransformPoint(new Vector3(x, h + 0.13f + layer * 0.25f, -0.02f * layer)), tb.eulerAngles.y + (bx * 7f % 11f) - 5f);
                    Box(box, "Box", Vector3.zero, new Vector3(0.3f, 0.24f, 0.22f), mCardboard);
                    ProductFace(box, p, "Front", new Vector3(0f, 0f, -0.111f), new Vector2(0.29f, 0.23f), false, bx + ft * 3);
                    ProductFace(box, p, "Back", new Vector3(0f, 0f, 0.111f), new Vector2(0.29f, 0.23f), true, bx + ft * 3 + 1);
                }
            }
            PottedPlant(r, p, "Gift_Plant_0", new Vector3(-71.2f, 0f, -31.2f), "monstera_clump", 0.55f, 1.3f, 8);
            PottedPlant(r, p, "Gift_Plant_1", new Vector3(-50.9f, 0f, -31.2f), "pachira_aquatica_01", 0.5f, 1.8f, 9);
            // hanging "GIFTS" sign over the checkout (double-sided park logo on chains) and warm pendants over the counter
            Transform hs = Group(r, "Gift_HangingSign", new Vector3(-56.5f, 2.55f, -15.4f), 0f);
            Box(hs, "Board", Vector3.zero, new Vector3(1.6f, 0.5f, 0.04f), mFeatureGreen);
            PrintQuad(hs, p, "LogoFront", "lobby_welcome", new Vector3(0f, 0f, -0.022f), new Vector2(1.5f, 0.4f), false);
            PrintQuad(hs, p, "LogoBack", "lobby_welcome", new Vector3(0f, 0f, 0.022f), new Vector2(1.5f, 0.4f), true);
            for (int ch = 0; ch < 2; ch++) Box(hs, $"Chain_{ch}", new Vector3(ch == 0 ? -0.7f : 0.7f, 0.6f, 0f), new Vector3(0.01f, 0.7f, 0.01f), mSteel);
            for (int pd = 0; pd < 2; pd++)
                WorldArchitectureKit.BuildHdrpLight(r, $"Light_GiftShop_Pendant_{pd}", new Vector3(-57.4f + pd * 1.8f, 2.35f, -14.6f), new Vector3(90f, 0f, 0f), LightType.Spot, Color.white, 260f, 6f, LightGroupId.VCMain, volumetricDimmer: 0f, castShadows: false, spotOuterAngle: 110f, colorTempK: 2800f);
            CeilingDetails(r, "Gift_Ceiling", -72f, -50f, -32f, -12f, CeilStd - 0.024f, 6);
        }

        /// <summary>White cornice band round a room just under the ceiling (inner wall faces x0..x1, z0..z1).</summary>
        // =========================================================================
        // ROUND 3 DRESSING (review 2: "rooms read as empty greybox halls, every surface spotless"): wall-mounted things,
        // floor mats at the doors, bins, stacked boxes and chairs, cable runs, scuffs and stains. Everything stands
        // against a wall (inside 0.9 m of it) and away from the door lines, so the walking routes stay as they were.
        // =========================================================================

        private static void Whiteboard(Transform parent, string name, Vector3 pos, Vector3 facing, float w, float h, int seed)
        {
            Transform t = Group(parent, name, pos, YawFacing(facing) + 180f);   // local +Z into the room
            Box(t, "Frame", new Vector3(0f, 0f, 0.012f), new Vector3(w, h, 0.024f), mSteel);
            Box(t, "Board", new Vector3(0f, 0f, 0.022f), new Vector3(w - 0.05f, h - 0.05f, 0.006f), mPaper);
            Box(t, "Tray", new Vector3(0f, -h * 0.5f - 0.02f, 0.05f), new Vector3(w * 0.6f, 0.025f, 0.07f), mSteel);
            // round 4 (review 3: "the whiteboard's writing is nine bars"): real marker handwriting
            string text = BoardText(seed, out Color boardInk);
            Ink(t, "Writing", text, new Vector3(0f, 0.01f, 0.0262f), Quaternion.Euler(0f, 180f, 0f), new Vector2(w - 0.14f, h - 0.12f), boardInk, "Kalam-Regular", 0.9f, TMPro.TextAlignmentOptions.TopLeft);
            for (int m = 0; m < 3; m++)
                Box(t, $"Marker_{m}", new Vector3(-0.15f + m * 0.12f, -h * 0.5f - 0.0f, 0.055f), new Vector3(0.11f, 0.018f, 0.018f), m == 0 ? mBinderRed : (m == 1 ? mBinderBlue : mBinderBlack));
        }

        private static void WallPhone(Transform parent, string name, Vector3 pos, Vector3 facing)
        {
            Transform t = Group(parent, name, pos, YawFacing(facing) + 180f);
            Box(t, "Base", new Vector3(0f, 0f, 0.03f), new Vector3(0.18f, 0.24f, 0.06f), mCabinet);
            Box(t, "Handset", new Vector3(-0.05f, 0f, 0.075f), new Vector3(0.05f, 0.22f, 0.04f), mCabinet);
            Box(t, "Keys", new Vector3(0.04f, -0.03f, 0.062f), new Vector3(0.07f, 0.1f, 0.004f), mBlack);
            Box(t, "Cord", new Vector3(-0.05f, -0.2f, 0.07f), new Vector3(0.012f, 0.22f, 0.012f), mBlack);
        }

        private static void KeyCabinet(Transform parent, string name, Vector3 pos, Vector3 facing)
        {
            Transform t = Group(parent, name, pos, YawFacing(facing) + 180f);
            Box(t, "Body", new Vector3(0f, 0f, 0.04f), new Vector3(0.46f, 0.56f, 0.08f), mCabinet);
            Box(t, "DoorSeam", new Vector3(0f, 0f, 0.081f), new Vector3(0.42f, 0.52f, 0.002f), mSteel);
            Box(t, "Lock", new Vector3(0.17f, 0f, 0.085f), new Vector3(0.03f, 0.05f, 0.01f), mSteel);
            Transform lbl = Group(t, "Label", t.TransformPoint(new Vector3(0f, 0.2f, 0.0835f)), t.eulerAngles.y);
            if (s_palette != null) PrintQuad(lbl, s_palette, "Print", "staff_only", Vector3.zero, new Vector2(0.2f, 0.07f), true);
        }

        private static void Mat(Transform parent, string name, Vector3 floorPos, float yaw, float w, float d)
        {
            Box(parent, name, floorPos + new Vector3(0f, 0.006f, 0f), new Vector3(w, 0.012f, d), s_palette != null ? s_palette.CarpetDark : mBlack, false, new Vector3(0f, yaw, 0f));
        }

        private static void BoxStack(Transform parent, WorldMaterialPalette p, string name, Vector3 floorPos, float yaw, int n, int seed)
        {
            Transform t = Group(parent, name, floorPos, yaw);
            var rng = new System.Random(seed);
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = 0.42f + (float)rng.NextDouble() * 0.18f, h = 0.28f + (float)rng.NextDouble() * 0.12f;
                Box(t, $"Box_{i}", new Vector3((float)rng.NextDouble() * 0.06f, y + h * 0.5f, 0f), new Vector3(w, h, 0.36f), mCardboard, i == 0, new Vector3(0f, (float)rng.NextDouble() * 10f - 5f, 0f));
                Box(t, $"Tape_{i}", new Vector3(0f, y + h + 0.001f, 0f), new Vector3(0.05f, 0.002f, 0.37f), mPaper);
                rng.NextDouble();   // round 4 (review 3: blank cream boxes among printed stock): every carton carries its print
                ProductFace(t, p, $"Label_{i}", new Vector3(0f, y + h * 0.5f, -0.181f), new Vector2(w * 0.5f, h * 0.5f), false, rng.Next(4));
                y += h;
            }
        }

        private static void ChairStack(Transform parent, string name, Vector3 floorPos, float yaw, int n)
        {
            for (int i = 0; i < n; i++) Chair(parent, $"{name}_{i}", floorPos + new Vector3(0f, i * 0.09f, 0f), yaw);
        }

        private static void Scuffs(Transform parent, WorldMaterialPalette p, string room, Vector2[] pts, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < pts.Length; i++)
                DecalAt(parent, $"{room}_Scuff_{i}", p.DecalGrimeLeak, new Vector3(pts[i].x, 0.05f, pts[i].y), 1.0f + (float)rng.NextDouble() * 1.4f, (float)rng.NextDouble() * 360f, 0.35f + (float)rng.NextDouble() * 0.25f);
        }

        private static void DressRoomsRound3(Transform parent, WorldMaterialPalette p)
        {
            var r = new GameObject("Props_Round3");
            r.transform.SetParent(parent, false);
            Transform t = r.transform;

            // ---- security office (x -71.87..-58.13, z 22.13..33.87; door at x -62.5 on the south wall)
            Whiteboard(t, "Sec_Whiteboard", new Vector3(-58.15f, 1.55f, 24.6f), Vector3.left, 1.8f, 1.05f, 31);
            Whiteboard(t, "Sec_ShiftBoard", new Vector3(-61.4f, 1.5f, 33.85f), Vector3.back, 1.4f, 0.9f, 35);
            BoxStack(t, p, "Sec_Boxes3", new Vector3(-58.75f, 0f, 30.4f), 90f, 2, 47);
            KeyCabinet(t, "Sec_KeyCabinet", new Vector3(-71.85f, 1.5f, 24.9f), Vector3.right);
            WallPhone(t, "Sec_WallPhone", new Vector3(-60.6f, 1.35f, 22.15f), Vector3.forward);
            WallPlate(t, "Sec_Switch2", new Vector3(-63.5f, 1.2f, 22.14f), Vector3.forward, false);
            Mat(t, "Sec_DoorMat", new Vector3(-62.5f, 0f, 22.75f), 0f, 1.3f, 0.85f);
            BoxStack(t, p, "Sec_Boxes", new Vector3(-71.35f, 0f, 33.3f), 12f, 3, 41);
            BoxStack(t, p, "Sec_Boxes2", new Vector3(-70.8f, 0f, 33.35f), -6f, 2, 42);
            p.InstantiatePropOrFallback("trash_bin", t, new Vector3(-58.6f, 0f, 32.9f), Vector3.zero, new Vector3(0.42f, 0.7f, 0.42f));
            Box(t, "Sec_CableRun", new Vector3(-65.0f, 0.02f, 33.8f), new Vector3(4.2f, 0.025f, 0.05f), mBlack);
            Box(t, "Sec_CableDrop", new Vector3(-66.9f, 0.45f, 33.82f), new Vector3(0.03f, 0.86f, 0.03f), mBlack);
            Scuffs(t, p, "Sec", new[] { new Vector2(-62.4f, 23.6f), new Vector2(-64.6f, 29.8f), new Vector2(-60.4f, 27.0f), new Vector2(-68.8f, 30.2f) }, 51);

            // ---- break room (x -71.85..-50.17, z 6.15..18.87; door at x -61 on the north wall)
            Whiteboard(t, "Break_Roster", new Vector3(-66.5f, 1.55f, 18.85f), Vector3.back, 2.0f, 1.1f, 32);
            WallClock(t, "Break_Clock2", new Vector3(-55.5f, 2.35f, 18.85f), Vector3.back, 120f, 20f);
            WallPhone(t, "Break_WallPhone", new Vector3(-62.4f, 1.35f, 18.86f), Vector3.back);
            WallExtinguisher(t, p, new Vector3(-71.85f, 0f, 11.72f), Vector3.right);   // fix round: between the drinks machine and the kitchenette
            Mat(t, "Break_DoorMat", new Vector3(-61f, 0f, 18.25f), 0f, 1.3f, 0.85f);
            ChairStack(t, "Break_ChairStack", new Vector3(-55.2f, 0f, 6.8f), 180f, 4);
            ChairStack(t, "Break_ChairStack2", new Vector3(-54.4f, 0f, 6.75f), 175f, 3);
            Whiteboard(t, "Break_Menu", new Vector3(-69.6f, 1.55f, 6.16f), Vector3.forward, 1.5f, 0.95f, 36);
            Mat(t, "Break_Rug", new Vector3(-68.2f, 0f, 7.75f), 0f, 3.0f, 2.1f);   // fix round: under the lounge set, not a slab mid-floor
            Box(t, "Break_Credenza", new Vector3(-52.6f, 0.42f, 6.45f), new Vector3(2.2f, 0.84f, 0.5f), mVeneer, true);
            PaperStack(t, "Break_CredenzaPapers", new Vector3(-52.2f, 0.84f, 6.45f), 12f, 3);
            Box(t, "Break_CredenzaKettle", new Vector3(-53.3f, 0.95f, 6.45f), new Vector3(0.16f, 0.22f, 0.16f), mSteel);
            p.InstantiatePropOrFallback("trash_bin", t, new Vector3(-51.0f, 0f, 16.2f), new Vector3(0f, 40f, 0f), new Vector3(0.45f, 0.75f, 0.45f));
            BoxStack(t, p, "Break_Boxes", new Vector3(-50.75f, 0f, 7.2f), 90f, 3, 43);
            PottedPlant(t, p, "Break_Plant3", new Vector3(-71.3f, 0f, 18.3f), "monstera_clump", 0.5f, 1.3f, 61);
            Scuffs(t, p, "Break", new[] { new Vector2(-61.0f, 17.4f), new Vector2(-55.6f, 12.9f), new Vector2(-51.6f, 11.8f), new Vector2(-66.4f, 12.0f), new Vector2(-60.6f, 9.2f) }, 52);

            // ---- gift shop (x -71.85..-50.15, z -31.85..-12.15)
            Mat(t, "Gift_DoorMat", new Vector3(-50.85f, 0f, -22.0f), 90f, 1.6f, 0.9f);
            BoxStack(t, p, "Gift_Stock", new Vector3(-71.3f, 0f, -12.7f), 8f, 4, 44);
            BoxStack(t, p, "Gift_Stock2", new Vector3(-70.7f, 0f, -12.75f), -4f, 2, 45);
            BoxStack(t, p, "Gift_Stock3", new Vector3(-50.75f, 0f, -31.3f), 90f, 3, 46);
            p.InstantiatePropOrFallback("trash_bin", t, new Vector3(-71.3f, 0f, -31.3f), Vector3.zero, new Vector3(0.42f, 0.7f, 0.42f));
            p.InstantiatePropOrFallback("utility_cart", t, new Vector3(-69.6f, 0f, -12.9f), new Vector3(0f, 90f, 0f), new Vector3(0.9f, 0.9f, 0.55f));
            CardSpinner(t, p, "Gift_Spinner2", new Vector3(-68.6f, 0f, -13.4f));
            PlushBin(t, "Gift_PlushBin3", new Vector3(-66.2f, 0f, -13.3f), 77);
            WallClock(t, "Gift_Clock", new Vector3(-71.85f, 2.4f, -22.0f), Vector3.right, 200f, 60f);
            WallExtinguisher(t, p, new Vector3(-50.15f, 0f, -14.5f), Vector3.left);
            Scuffs(t, p, "Gift", new[] { new Vector2(-51.8f, -22.0f), new Vector2(-56.0f, -20.5f), new Vector2(-61.0f, -17.0f), new Vector2(-64.5f, -25.5f) }, 53);

            // ---- feed prep hall (x -11.83..7.85, z 3..18.86)
            Transform hooks = Group(t, "Prep_HookRail", new Vector3(4.6f, 0f, 18.6f), 0f);
            Box(hooks, "Rail", new Vector3(0f, 2.35f, 0f), new Vector3(4.0f, 0.06f, 0.06f), mSteel);
            for (int b = 0; b < 3; b++) Box(hooks, $"Bracket_{b}", new Vector3(-1.8f + b * 1.8f, 2.35f, 0.14f), new Vector3(0.05f, 0.05f, 0.28f), mSteel);
            for (int h = 0; h < 5; h++)
            {
                Box(hooks, $"Hook_{h}", new Vector3(-1.6f + h * 0.8f, 2.22f, 0f), new Vector3(0.015f, 0.22f, 0.015f), mSteel);
                if (h % 2 == 0) p.InstantiatePropOrFallback("hanging_meat", hooks, hooks.TransformPoint(new Vector3(-1.6f + h * 0.8f, 1.35f, 0f)), new Vector3(0f, h * 40f, 0f), new Vector3(0.35f, 0.8f, 0.3f), grounded: true, addCollider: false);
            }
            Transform reel = Group(t, "Prep_HoseReel", new Vector3(7.84f, 1.2f, 13.4f), -90f);
            Prim(reel, "Drum", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.15f), new Vector3(0.5f, 0.09f, 0.5f), mPaintYellow, new Vector3(90f, 0f, 0f));
            Prim(reel, "Hose", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.15f), new Vector3(0.44f, 0.07f, 0.44f), mFeatureGreen, new Vector3(90f, 0f, 0f));
            Box(reel, "HoseDrop", new Vector3(0.2f, -0.6f, 0.2f), new Vector3(0.04f, 1.2f, 0.04f), mFeatureGreen);
            for (int u = 0; u < 3; u++)
            {
                Transform tub = Group(t, $"Prep_Tub_{u}", new Vector3(-11.2f, 0f, 5.0f + u * 0.75f), 0f);
                Box(tub, "Tub", new Vector3(0f, 0.22f, 0f), new Vector3(0.6f, 0.44f, 0.5f), mBinderBlue, u == 0);
                Box(tub, "Inner", new Vector3(0f, 0.43f, 0f), new Vector3(0.52f, 0.01f, 0.42f), mBlack);
            }
            Box(t, "Prep_Drain3", new Vector3(3.0f, 0.004f, 15.0f), new Vector3(0.4f, 0.008f, 0.4f), mDarkMetal);
            Scuffs(t, p, "Prep", new[] { new Vector2(-2.0f, 17.6f), new Vector2(-3.5f, 11.0f), new Vector2(3.0f, 15.2f), new Vector2(1.0f, 7.0f), new Vector2(-8.0f, 9.0f) }, 54);
            DressRoomCentresFixRound(t, p);
        }

        // ---- fix round (review of the round 3 hand-in: "room centres are still bare floor"): furniture groups in the middle
        // of the four worst rooms. Every group keeps >= 1.5 m aisles to its neighbours and stays off the door-to-door lines
        // (security office: door (-62.5, 22) -> spawn (-64.5, 28); break room: door (-61, 19) south; gift shop: east door
        // (-50, -22) west along z -22; feed prep: (-2, 19) -> (-6.8, 3) / (3, 3) down the counter aisles).

        private static void ChairAt(Transform parent, string name, Vector3 floorPos, Vector3 faceDir)
            => Chair(parent, name, floorPos, Mathf.Atan2(-faceDir.x, -faceDir.z) * Mathf.Rad2Deg);

        /// <summary>Lunch table for four: table, two chairs on each long side, a mug or two.</summary>
        private static void LunchGroup(Transform parent, string name, Vector3 c, int seed)
        {
            var rng = new System.Random(seed);
            Table(parent, name + "_Table", c, 0f, 1.4f, 0.8f, 0.75f, mLaminate, mSteel);
            for (int k = 0; k < 4; k++)
            {
                float sx = (k % 2 == 0 ? -0.36f : 0.36f) + (float)rng.NextDouble() * 0.1f - 0.05f;
                float sz = k < 2 ? -0.78f : 0.78f;
                float yawJ = (float)rng.NextDouble() * 16f - 8f;
                Chair(parent, $"{name}_Chair_{k}", c + new Vector3(sx, 0f, sz + (float)rng.NextDouble() * 0.08f), (k < 2 ? 180f : 0f) + yawJ);
            }
            Prim(parent, name + "_Mug", PrimitiveType.Cylinder, c + new Vector3(0.3f, 0.80f, -0.1f), new Vector3(0.08f, 0.05f, 0.08f), seed % 2 == 0 ? mBinderRed : mPaper);
            if (seed % 3 != 0) PaperStack(parent, name + "_Paper", c + new Vector3(-0.35f, 0.75f, 0.12f), (float)rng.NextDouble() * 40f, 2);
        }

        private static void DressRoomCentresFixRound(Transform t, WorldMaterialPalette p)
        {
            // ---- break room: kitchenette run on the west wall (fridge, base cabinets with a double sink, splashback, wall
            // cabinets, microwave, coffee machine), two lunch groups in the open south half, armchairs round the lounge sofa
            Transform kit = Group(t, "Break_Kitchenette", new Vector3(-71.85f, 0f, 14.8f), 90f);   // local +Z into the room, x along the wall (towards -z)
            Counter(t, "Break_KitCounter", new Vector3(-71.53f, 0f, 15.25f), -90f, 4.1f, 0.62f, 0.9f, mLaminate, mCabinet);
            Box(kit, "Splashback", new Vector3(-0.45f, 1.2f, 0.006f), new Vector3(4.1f, 0.6f, 0.012f), p.LabTileWhite);
            Box(kit, "WallCabinets", new Vector3(-0.45f, 1.85f, 0.18f), new Vector3(4.1f, 0.7f, 0.36f), mCabinet);
            for (int d = 1; d < 6; d++) Box(kit, $"WallCabSeam_{d}", new Vector3(-0.45f - 2.05f + d * 0.683f, 1.85f, 0.362f), new Vector3(0.006f, 0.66f, 0.004f), mBlack);
            for (int d = 0; d < 6; d++) Box(kit, $"WallCabPull_{d}", new Vector3(-0.45f - 2.05f + d * 0.683f + (d % 2 == 0 ? 0.58f : 0.1f), 1.58f, 0.368f), new Vector3(0.10f, 0.012f, 0.015f), mSteel);
            Box(kit, "SinkRim", new Vector3(0.09f, 0.901f, 0.33f), new Vector3(1.0f, 0.004f, 0.46f), mSteel);
            Box(kit, "SinkBowlL", new Vector3(-0.15f, 0.9045f, 0.33f), new Vector3(0.42f, 0.004f, 0.38f), mDarkMetal);
            Box(kit, "SinkBowlR", new Vector3(0.33f, 0.9045f, 0.33f), new Vector3(0.42f, 0.004f, 0.38f), mDarkMetal);
            Box(kit, "TapPost", new Vector3(0.09f, 1.0f, 0.08f), new Vector3(0.03f, 0.2f, 0.03f), mSteel);
            Box(kit, "TapSpout", new Vector3(0.09f, 1.09f, 0.18f), new Vector3(0.025f, 0.025f, 0.22f), mSteel);
            Box(kit, "Microwave", new Vector3(-1.75f, 1.06f, 0.3f), new Vector3(0.5f, 0.3f, 0.38f), mCabinet);
            Box(kit, "MicrowaveDoor", new Vector3(-1.82f, 1.06f, 0.492f), new Vector3(0.32f, 0.22f, 0.006f), mBlack);
            Box(kit, "CoffeeMachine", new Vector3(-1.1f, 1.08f, 0.25f), new Vector3(0.24f, 0.36f, 0.28f), mBlack);
            Box(kit, "CoffeePot", new Vector3(-1.1f, 0.98f, 0.36f), new Vector3(0.13f, 0.14f, 0.13f), mBottleBlue);
            Box(kit, "DishRack", new Vector3(0.95f, 0.95f, 0.3f), new Vector3(0.45f, 0.1f, 0.34f), mSteel);
            for (int m = 0; m < 3; m++) Prim(kit, $"RackMug_{m}", PrimitiveType.Cylinder, new Vector3(0.8f + m * 0.14f, 1.02f, 0.3f), new Vector3(0.08f, 0.05f, 0.08f), m == 1 ? mBinderRed : mPaper);
            PaperStack(kit, "TeaTowel", new Vector3(1.55f, 0.9f, 0.3f), 8f, 1);
            Transform fr = Group(t, "Break_Fridge", new Vector3(-71.47f, 0f, 12.72f), -90f);
            BoxCollider fbc = fr.gameObject.AddComponent<BoxCollider>();
            fbc.center = new Vector3(0f, 0.9f, 0f); fbc.size = new Vector3(0.72f, 1.8f, 0.7f);
            // round 4 (review 3: "a tan box with two bar handles that reads as a cardboard carton"): white enamel cabinet with
            // two proud doors and a gap between them, rounded top edge, recessed pull handles, a kick grille and magnets
            Box(fr, "Body", new Vector3(0f, 0.9f, 0.015f), new Vector3(0.72f, 1.78f, 0.67f), mEnamelFridge);
            Prim(fr, "TopRound", PrimitiveType.Cylinder, new Vector3(0f, 1.78f, -0.29f), new Vector3(0.05f, 0.36f, 0.05f), mEnamelFridge, new Vector3(0f, 0f, 90f));
            Box(fr, "DoorTop", new Vector3(0f, 1.505f, -0.335f), new Vector3(0.715f, 0.53f, 0.03f), mEnamelFridge);
            Box(fr, "DoorBottom", new Vector3(0f, 0.695f, -0.335f), new Vector3(0.715f, 1.05f, 0.03f), mEnamelFridge);
            Box(fr, "DoorGap", new Vector3(0f, 1.232f, -0.33f), new Vector3(0.715f, 0.014f, 0.03f), mBlack);
            Box(fr, "HandleTop", new Vector3(0.31f, 1.33f, -0.357f), new Vector3(0.03f, 0.18f, 0.016f), mDarkMetal);
            Box(fr, "HandleBottom", new Vector3(0.31f, 1.07f, -0.357f), new Vector3(0.03f, 0.22f, 0.016f), mDarkMetal);
            Box(fr, "KickGrille", new Vector3(0f, 0.075f, -0.33f), new Vector3(0.66f, 0.1f, 0.02f), mDarkMetal);
            for (int g = 0; g < 7; g++) Box(fr, $"GrilleSlot_{g}", new Vector3(-0.27f + g * 0.09f, 0.075f, -0.341f), new Vector3(0.05f, 0.012f, 0.002f), mBlack);
            Box(fr, "Badge", new Vector3(-0.22f, 1.72f, -0.351f), new Vector3(0.12f, 0.025f, 0.002f), mSteel);
            for (int n = 0; n < 3; n++) Box(fr, $"Note_{n}", new Vector3(-0.15f + n * 0.12f, 1.55f - n * 0.08f, -0.351f), new Vector3(0.1f, 0.12f, 0.002f), n == 1 ? p.PaintedMetalYellow : mPaper, false, new Vector3(0f, 0f, n * 7f - 7f));
            for (int n = 0; n < 3; n++) Prim(fr, $"Magnet_{n}", PrimitiveType.Cylinder, new Vector3(-0.15f + n * 0.12f, 1.60f - n * 0.08f, -0.354f), new Vector3(0.02f, 0.004f, 0.02f), n == 0 ? mBinderRed : (n == 1 ? mBinderBlue : mFeatureGreen), new Vector3(90f, 0f, 0f));

            LunchGroup(t, "Break_LunchA", new Vector3(-63.0f, 0f, 8.85f), 71);
            LunchGroup(t, "Break_LunchB", new Vector3(-57.6f, 0f, 8.85f), 72);
            Sofa(t, "Break_ArmchairE", new Vector3(-66.75f, 0f, 7.85f), 90f, 0.95f);
            Sofa(t, "Break_ArmchairW", new Vector3(-70.15f, 0f, 7.95f), -90f, 0.95f);
            PottedPlant(t, p, "Break_Plant4", new Vector3(-66.8f, 0f, 6.6f), "anthurium_botany_01", 0.38f, 0.7f, 63);

            // ---- gift shop: a round tiered display of plush toys in the open north-west floor, two short gondola runs
            // east-west in the open south half
            Transform disp = Group(t, "Gift_RoundDisplay", new Vector3(-67.0f, 0f, -17.1f), 15f);
            BoxCollider dbc = disp.gameObject.AddComponent<BoxCollider>();
            dbc.center = new Vector3(0f, 0.45f, 0f); dbc.size = new Vector3(1.3f, 0.9f, 1.3f);
            Prim(disp, "Tier0", PrimitiveType.Cylinder, new Vector3(0f, 0.2f, 0f), new Vector3(1.3f, 0.2f, 1.3f), mFeatureGreen);
            Prim(disp, "Tier0Top", PrimitiveType.Cylinder, new Vector3(0f, 0.405f, 0f), new Vector3(1.34f, 0.006f, 1.34f), mVeneer);
            Prim(disp, "Tier1", PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0f), new Vector3(0.8f, 0.2f, 0.8f), mFeatureGreen);
            Prim(disp, "Tier1Top", PrimitiveType.Cylinder, new Vector3(0f, 0.805f, 0f), new Vector3(0.84f, 0.006f, 0.84f), mVeneer);
            Material[] plush = { mVendRed, mBinderBlue, mFeatureGreen, p.PaintedMetalYellow, mPaper };
            var prng = new System.Random(88);
            for (int k = 0; k < 14; k++)
            {
                bool top = k >= 9;
                float a = k * (top ? 72f : 40f) * Mathf.Deg2Rad, rr = top ? 0.24f : 0.52f;
                float s = 0.14f + (float)prng.NextDouble() * 0.08f;
                Vector3 lp = new Vector3(Mathf.Cos(a) * rr, (top ? 0.81f : 0.41f) + s * 0.5f, Mathf.Sin(a) * rr);
                Prim(disp, $"Plush_{k}", PrimitiveType.Sphere, lp, new Vector3(s, s * 1.1f, s * 0.9f), plush[k % plush.Length]);
                Prim(disp, $"PlushHead_{k}", PrimitiveType.Sphere, lp + new Vector3(0f, s * 0.62f, 0f), new Vector3(s * 0.62f, s * 0.6f, s * 0.6f), plush[k % plush.Length]);
            }
            Transform sign = Group(disp, "PriceSign", disp.TransformPoint(new Vector3(0f, 1.15f, 0f)), 15f);
            Box(sign, "Post", new Vector3(0f, -0.15f, 0f), new Vector3(0.025f, 0.3f, 0.025f), mSteel);
            Box(sign, "Card", new Vector3(0f, 0.08f, 0f), new Vector3(0.36f, 0.22f, 0.012f), p.PaintedMetalYellow);
            Gondola(t, p, "Gift_GondolaS_0", new Vector3(-68.0f, 0f, -27.0f), 0f, 3.2f, 120);
            Gondola(t, p, "Gift_GondolaS_1", new Vector3(-58.4f, 0f, -27.0f), 0f, 3.2f, 121);

            // ---- security office: a second pair of desks face to face east of the spawn, with chairs, monitors and paper
            for (int k = 0; k < 2; k++)
            {
                float z = k == 0 ? 28.95f : 29.75f;
                Transform dk = Desk(t, $"Sec_DeskPair_{k}", new Vector3(-60.3f, 0f, z), k == 0 ? 0f : 180f, 1.4f, 0.75f, mLaminate, mCabinet);
                Box(dk, "Monitor", new Vector3(-0.2f, 0.98f, 0.15f), new Vector3(0.42f, 0.32f, 0.05f), mBlack);
                Box(dk, "MonitorFoot", new Vector3(-0.2f, 0.78f, 0.17f), new Vector3(0.18f, 0.02f, 0.14f), mBlack);
                Box(dk, "Keyboard", new Vector3(-0.2f, 0.77f, -0.12f), new Vector3(0.38f, 0.02f, 0.13f), mBlack);
                PaperStack(dk, "Papers", dk.TransformPoint(new Vector3(0.35f, 0.76f, -0.05f)), 10f + k * 30f, 3);
                ChairAt(t, $"Sec_DeskPairChair_{k}", new Vector3(-60.4f + k * 0.15f, 0f, k == 0 ? 28.05f : 30.65f), k == 0 ? Vector3.forward : Vector3.back);
            }

            // ---- feed prep: a double sink on the west wall, two steel prep tables north-west, a walk-in freezer door on the
            // north wall east of the wing door
            Transform sink = Group(t, "Prep_DoubleSink", new Vector3(-11.83f, 0f, 9.6f), 90f);   // local +Z into the room
            BoxCollider sbc = sink.gameObject.AddComponent<BoxCollider>();
            sbc.center = new Vector3(0f, 0.45f, 0.33f); sbc.size = new Vector3(1.6f, 0.9f, 0.66f);
            Box(sink, "Basin", new Vector3(0f, 0.8f, 0.33f), new Vector3(1.6f, 0.2f, 0.62f), mSteel);
            Box(sink, "BowlL", new Vector3(-0.38f, 0.905f, 0.34f), new Vector3(0.66f, 0.012f, 0.48f), mDarkMetal);
            Box(sink, "BowlR", new Vector3(0.38f, 0.905f, 0.34f), new Vector3(0.66f, 0.012f, 0.48f), mDarkMetal);
            Box(sink, "Divider", new Vector3(0f, 0.91f, 0.34f), new Vector3(0.05f, 0.02f, 0.5f), mSteel);
            Box(sink, "Upstand", new Vector3(0f, 1.05f, 0.03f), new Vector3(1.6f, 0.3f, 0.04f), mSteel);
            for (int l = 0; l < 4; l++) Box(sink, $"Leg_{l}", new Vector3(l % 2 == 0 ? -0.75f : 0.75f, 0.35f, l < 2 ? 0.08f : 0.58f), new Vector3(0.04f, 0.7f, 0.04f), mSteel);
            Box(sink, "Shelf", new Vector3(0f, 0.2f, 0.33f), new Vector3(1.5f, 0.025f, 0.55f), mSteel);
            Box(sink, "TapPost", new Vector3(0f, 1.25f, 0.1f), new Vector3(0.035f, 0.35f, 0.035f), mSteel);
            Box(sink, "TapArm", new Vector3(0f, 1.42f, 0.3f), new Vector3(0.03f, 0.03f, 0.4f), mSteel);
            Box(sink, "SprayHose", new Vector3(0.12f, 1.15f, 0.26f), new Vector3(0.02f, 0.5f, 0.02f), mBlack);
            PrepTable(t, "Prep_TableN_0", new Vector3(-9.2f, 0f, 16.7f), 1.8f, 0.8f, 0.9f, 91);
            PrepTable(t, "Prep_TableN_1", new Vector3(-6.0f, 0f, 16.7f), 1.8f, 0.8f, 0.9f, 92);
            Box(t, "Prep_CuttingBoard", new Vector3(-9.4f, 0.91f, 16.7f), new Vector3(0.6f, 0.025f, 0.4f), mPaper);
            Box(t, "Prep_Cleaver", new Vector3(-9.0f, 0.93f, 16.75f), new Vector3(0.28f, 0.01f, 0.09f), mSteel, false, new Vector3(0f, 25f, 0f));
            Box(t, "Prep_Scale", new Vector3(-5.6f, 0.96f, 16.7f), new Vector3(0.35f, 0.1f, 0.3f), mSteel);
            Box(t, "Prep_ScaleDial", new Vector3(-5.6f, 1.08f, 16.82f), new Vector3(0.2f, 0.18f, 0.05f), mPaper);
            Transform fz = Group(t, "Prep_FreezerDoor", new Vector3(0.9f, 0f, 18.86f), 180f);   // local +Z into the room
            Box(fz, "Frame", new Vector3(0f, 1.15f, 0.05f), new Vector3(1.6f, 2.3f, 0.1f), mSteel);
            Box(fz, "Leaf", new Vector3(0f, 1.1f, 0.12f), new Vector3(1.3f, 2.1f, 0.08f), mCabinet);
            Box(fz, "KickPlate", new Vector3(0f, 0.18f, 0.165f), new Vector3(1.26f, 0.3f, 0.01f), mSteel);
            Box(fz, "LatchBody", new Vector3(0.5f, 1.1f, 0.19f), new Vector3(0.1f, 0.3f, 0.07f), mSteel);
            Box(fz, "LatchHandle", new Vector3(0.38f, 1.08f, 0.24f), new Vector3(0.28f, 0.04f, 0.04f), mSteel);
            for (int h = 0; h < 2; h++) Box(fz, $"Hinge_{h}", new Vector3(-0.67f, 0.5f + h * 1.2f, 0.18f), new Vector3(0.08f, 0.22f, 0.06f), mSteel);
            Box(fz, "Plate", new Vector3(0f, 1.8f, 0.165f), new Vector3(0.5f, 0.14f, 0.008f), mBinderBlue);
            Box(fz, "Thermo", new Vector3(0.95f, 1.5f, 0.03f), new Vector3(0.16f, 0.24f, 0.05f), mBlack);
            Box(fz, "ThermoScreen", new Vector3(0.95f, 1.55f, 0.056f), new Vector3(0.1f, 0.06f, 0.002f), mScreenCyan);
        }

        private static void Cornice(Transform parent, string name, float x0, float x1, float z0, float z1, float ceilY, Material m)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            const float h = 0.22f, d = 0.012f;
            float y = ceilY - h * 0.5f;
            Box(root.transform, "N", new Vector3((x0 + x1) * 0.5f, y, z1 - d * 0.5f), new Vector3(x1 - x0, h, d), m);
            Box(root.transform, "S", new Vector3((x0 + x1) * 0.5f, y, z0 + d * 0.5f), new Vector3(x1 - x0, h, d), m);
            Box(root.transform, "W", new Vector3(x0 + d * 0.5f, y, (z0 + z1) * 0.5f), new Vector3(d, h, z1 - z0 - 2f * d), m);
            Box(root.transform, "E", new Vector3(x1 - d * 0.5f, y, (z0 + z1) * 0.5f), new Vector3(d, h, z1 - z0 - 2f * d), m);
            const float c = 0.045f;   // crown trim
            Box(root.transform, "TrimN", new Vector3((x0 + x1) * 0.5f, ceilY - c * 0.5f, z1 - d - c * 0.5f), new Vector3(x1 - x0, c, c), m);
            Box(root.transform, "TrimS", new Vector3((x0 + x1) * 0.5f, ceilY - c * 0.5f, z0 + d + c * 0.5f), new Vector3(x1 - x0, c, c), m);
            Box(root.transform, "TrimW", new Vector3(x0 + d + c * 0.5f, ceilY - c * 0.5f, (z0 + z1) * 0.5f), new Vector3(c, c, z1 - z0 - 2f * (d + c)), m);
            Box(root.transform, "TrimE", new Vector3(x1 - d - c * 0.5f, ceilY - c * 0.5f, (z0 + z1) * 0.5f), new Vector3(c, c, z1 - z0 - 2f * (d + c)), m);
        }

        /// <summary>Round raised tree island on the plaza: a ring of granite kerb blocks, soil, one big tree and
        /// under-planting.</summary>
        private static Material mKerbRound, mMulch;

        /// <summary>Round tree island (round 3, review 2 GoldenDay_plaza_eye: "a ring of near-black glossy box segments round a
        /// flat brown soil disc"): a round mid-grey precast kerb (two 20-sided cylinders turned half a facet, so the outline
        /// reads round) with a coping lip, a mounded bed of leaf mulch, the tree and 10 ground-cover clumps that fill the bed.
        /// The 14 old kerb boxes stay as invisible colliders, so walking and the NavMesh are unchanged.</summary>
        private static void TreeIsland(Transform parent, WorldMaterialPalette p, string name, Vector3 centre, float radius, int seed)
        {
            if (mKerbRound == null)
            {
                mKerbRound = p.Surface("MAT_Z1_KerbRound", "concrete_cracked_ext", "BaseColor", "MaskMap", 1.6f, new Color(1.28f, 1.30f, 1.32f), 0.12f, 0.45f, 0f, 0.8f);
                mMulch     = p.Surface("MAT_Z1_Mulch", "forest_floor", "BaseColor", "MaskMap", 1.4f, new Color(1.05f, 1.0f, 0.95f), 0.05f, 0.40f, 0f, 1.0f);
            }
            Transform t = Group(parent, name, centre, seed * 17f);
            const int seg = 14;
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                GameObject k = Box(t, $"KerbCollider_{i}", new Vector3(Mathf.Cos(a) * radius, 0.22f, Mathf.Sin(a) * radius), new Vector3(0.36f, 0.44f, 2f * radius * Mathf.Sin(Mathf.PI / seg) + 0.05f), mKerbRound, true, new Vector3(0f, -a * Mathf.Rad2Deg, 0f));
                if (k != null)
                {
                    Object.DestroyImmediate(k.GetComponent<MeshRenderer>());
                    Object.DestroyImmediate(k.GetComponent<MeshFilter>());
                }
            }
            float ro = radius + 0.18f;
            for (int h = 0; h < 2; h++)
            {
                Prim(t, $"Kerb_{h}", PrimitiveType.Cylinder, new Vector3(0f, 0.21f, 0f), new Vector3(2f * ro, 0.21f, 2f * ro), mKerbRound, new Vector3(0f, h * 9f, 0f));
                Prim(t, $"Coping_{h}", PrimitiveType.Cylinder, new Vector3(0f, 0.44f, 0f), new Vector3(2f * ro + 0.06f, 0.025f, 2f * ro + 0.06f), mKerbRound, new Vector3(0f, h * 9f + 4.5f, 0f));
            }
            // the bed tops out 1 cm proud of the coping (0.475 vs 0.465: the old 0.465/0.465 tops z-fought in a ring), mounded
            // towards the trunk
            Prim(t, "Mulch", PrimitiveType.Cylinder, new Vector3(0f, 0.455f, 0f), new Vector3(2f * ro - 0.44f, 0.02f, 2f * ro - 0.44f), mMulch);
            Prim(t, "Mound", PrimitiveType.Sphere, new Vector3(0f, 0.41f, 0f), new Vector3(2f * ro - 0.9f, 0.32f, 2f * ro - 0.9f), mMulch);
            p.InstantiatePropOrFallback(seed % 3 == 0 ? "jungle_tree" : (seed % 2 == 0 ? "palm_tree" : "palm_tree_b"), t, centre + new Vector3(0f, 0.5f, 0f), new Vector3(0f, seed * 31f, 0f), seed % 3 == 0 ? new Vector3(9f, 9f, 9f) : new Vector3(8f, 8f, 8f), grounded: true, addCollider: false);
            string[] under = { "fern_02", "calathea_orbifolia_01", "monstera_clump", "shrub_02", "anthurium_botany_01", "shrub_03", "grass_medium_01" };
            var rng = new System.Random(seed);
            for (int k = 0; k < 10; k++)
            {
                bool inner = k >= 7;
                float a = (inner ? (k - 7) * 2.09f + 0.9f : k * 0.8976f) + (float)rng.NextDouble() * 0.35f;
                float rr = (inner ? 0.42f : 0.70f) * radius + (float)rng.NextDouble() * 0.12f;
                float sc = (inner ? 1.2f : 1.45f) + (float)rng.NextDouble() * 0.6f;   // clumps that fill the bed
                string stem = under[(k * 3 + seed) % under.Length];
                p.InstantiatePropOrFallback(stem, t, centre + new Vector3(Mathf.Cos(a) * rr, inner ? 0.5f : 0.46f, Mathf.Sin(a) * rr), new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), new Vector3(sc, sc, sc), grounded: true, addCollider: false);
            }
            // a warm can uplight in the bed aimed at the trunk and the crown (Plaza group: on from dusk)
            // the lamp sits 5 cm above the bed surface there, so LookFixtures' floor can (plaza to lamp) shows its lens
            float rUp = radius * 0.48f, rMound = ro - 0.45f;
            float bedY = Mathf.Max(0.475f, 0.41f + 0.16f * Mathf.Sqrt(Mathf.Max(0f, 1f - (rUp / rMound) * (rUp / rMound))));
            Vector3 up = centre + new Vector3(rUp, bedY + 0.05f, 0f);
            Vector3 aim = (centre + new Vector3(0f, 4.5f, 0f) - up).normalized;
            WorldArchitectureKit.BuildHdrpLight(t, $"Light_TreeUp_{name}", up, Quaternion.LookRotation(aim).eulerAngles, LightType.Spot, Color.white, 520f, 9f, LightGroupId.Plaza, volumetricDimmer: 0f, castShadows: false, spotOuterAngle: 46f, colorTempK: 3000f);
            BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.6f, 0f); bc.size = new Vector3(0.5f, 1.2f, 0.5f);   // the trunk
        }

        /// <summary>Round T-shirt rack: base, pole, ring rail, shirts on hangers all round with a printed logo.</summary>
        private static void ShirtRack(Transform parent, WorldMaterialPalette p, string name, Vector3 floorPos, int seed)
        {
            Transform t = Group(parent, name, floorPos, seed * 13f);
            BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.75f, 0f); bc.size = new Vector3(1.2f, 1.5f, 1.2f);
            Prim(t, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.6f, 0.02f, 0.6f), mSteel);
            Prim(t, "Pole", PrimitiveType.Cylinder, new Vector3(0f, 0.7f, 0f), new Vector3(0.04f, 0.7f, 0.04f), mSteel);
            Prim(t, "Ring", PrimitiveType.Cylinder, new Vector3(0f, 1.38f, 0f), new Vector3(1.0f, 0.006f, 1.0f), mSteel);
            Material[] cols = { mFeatureGreen, mPaper, mBinderBlack, mFabric, mPaintYellow, mFeatureGreen };
            var rng = new System.Random(seed);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                Transform s = Group(t, $"Shirt_{i}", t.TransformPoint(new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f)), t.eulerAngles.y - a * Mathf.Rad2Deg);
                Material m = cols[rng.Next(cols.Length)];
                Box(s, "Body", new Vector3(0f, 1.03f, 0f), new Vector3(0.02f, 0.62f, 0.46f), m);
                Box(s, "Shoulders", new Vector3(0f, 1.31f, 0f), new Vector3(0.022f, 0.08f, 0.56f), m);
                Box(s, "Hanger", new Vector3(0f, 1.36f, 0f), new Vector3(0.01f, 0.04f, 0.04f), mBlack);
            }
            for (int i = 0; i < 4; i++)   // logos on the outward-facing shirts
            {
                float a = (i * 4 + 1) * Mathf.PI * 2f / 16f;
                Vector3 outDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Transform q = Group(t, $"ShirtPrint_{i}", t.TransformPoint(outDir * 0.523f + Vector3.up * 1.08f), 0f);
                q.rotation = Quaternion.LookRotation(-t.TransformDirection(outDir));
                PrintQuad(q, p, "Print", i % 2 == 0 ? "park_logo" : "raptor_poster", Vector3.zero, new Vector2(0.24f, 0.24f), false);
            }
        }

        /// <summary>Wire bin of plush dinosaurs (bodies, heads, tails from spheres and capsules).</summary>
        private static void PlushBin(Transform parent, string name, Vector3 floorPos, int seed)
        {
            Transform t = Group(parent, name, floorPos, seed * 21f);
            BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.4f, 0f); bc.size = new Vector3(1.0f, 0.8f, 0.7f);
            Box(t, "Floor", new Vector3(0f, 0.2f, 0f), new Vector3(0.96f, 0.02f, 0.66f), mSteel);
            // wire basket: six thin rails per side and a top rim (solid steel plates went dark and hid the plush)
            for (int s = 0; s < 4; s++)
            {
                bool x = s < 2;
                float len = x ? 0.66f : 0.96f;
                for (int r = 0; r < 6; r++)
                {
                    float a = -len * 0.5f + 0.04f + r * (len - 0.08f) / 5f;
                    Vector3 c = x ? new Vector3(s == 0 ? -0.48f : 0.48f, 0.425f, a) : new Vector3(a, 0.425f, s == 2 ? -0.33f : 0.33f);
                    Box(t, $"Rail_{s}_{r}", c, new Vector3(0.012f, 0.45f, 0.012f), mDarkMetal);
                }
                Box(t, $"Rim_{s}", x ? new Vector3(s == 0 ? -0.48f : 0.48f, 0.65f, 0f) : new Vector3(0f, 0.65f, s == 2 ? -0.33f : 0.33f), x ? new Vector3(0.016f, 0.016f, 0.68f) : new Vector3(0.98f, 0.016f, 0.016f), mDarkMetal);
            }
            for (int l = 0; l < 4; l++) Box(t, $"Leg_{l}", new Vector3((l % 2 == 0 ? -1f : 1f) * 0.46f, 0.1f, (l < 2 ? -1f : 1f) * 0.31f), new Vector3(0.025f, 0.2f, 0.025f), mSteel);
            // bright, non-metal plush colours (lime, orange, sky blue, purple, sand)
            Material[] fur =
            {
                s_palette.Surface("MAT_Z1_PlushLime", "carpet_office", "BaseColor", "MaskMap", 0.4f, new Color(1.10f, 1.70f, 0.55f), 0.0f, 0.15f, 0f, 0.4f),
                s_palette.Surface("MAT_Z1_PlushOrange", "carpet_office", "BaseColor", "MaskMap", 0.4f, new Color(1.90f, 1.05f, 0.45f), 0.0f, 0.15f, 0f, 0.4f),
                s_palette.Surface("MAT_Z1_PlushBlue", "carpet_office", "BaseColor", "MaskMap", 0.4f, new Color(0.75f, 1.20f, 1.90f), 0.0f, 0.15f, 0f, 0.4f),
                s_palette.Surface("MAT_Z1_PlushPurple", "carpet_office", "BaseColor", "MaskMap", 0.4f, new Color(1.15f, 0.80f, 1.60f), 0.0f, 0.15f, 0f, 0.4f),
                s_palette.Surface("MAT_Z1_PlushSand", "carpet_office", "BaseColor", "MaskMap", 0.4f, new Color(1.70f, 1.55f, 1.15f), 0.0f, 0.15f, 0f, 0.4f),
            };
            var rng = new System.Random(seed);
            for (int i = 0; i < 9; i++)
            {
                Material m = fur[rng.Next(fur.Length)];
                Vector3 c = new Vector3(-0.32f + (i % 3) * 0.32f + (float)rng.NextDouble() * 0.06f, 0.42f + (i / 3) * 0.12f, -0.18f + ((i / 3) % 2) * 0.2f);
                Vector3 e = new Vector3((float)rng.NextDouble() * 50f - 25f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 40f - 20f);
                Transform d = Group(t, $"Plush_{i}", t.TransformPoint(c), t.eulerAngles.y + e.y);
                d.localRotation = Quaternion.Euler(e.x, e.y, e.z);
                Prim(d, "Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.2f, 0.17f, 0.26f), m);
                Prim(d, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.1f, 0.15f), new Vector3(0.12f, 0.11f, 0.15f), m);
                Prim(d, "Tail", PrimitiveType.Capsule, new Vector3(0f, -0.02f, -0.18f), new Vector3(0.06f, 0.1f, 0.06f), m, new Vector3(70f, 0f, 0f));
                Prim(d, "EyeL", PrimitiveType.Sphere, new Vector3(-0.035f, 0.13f, 0.215f), new Vector3(0.02f, 0.02f, 0.02f), mBlack);
                Prim(d, "EyeR", PrimitiveType.Sphere, new Vector3(0.035f, 0.13f, 0.215f), new Vector3(0.02f, 0.02f, 0.02f), mBlack);
            }
        }

        /// <summary>Four-sided postcard and guidebook spinner: pole, wire faces, printed cards.</summary>
        private static void CardSpinner(Transform parent, WorldMaterialPalette p, string name, Vector3 floorPos)
        {
            Transform t = Group(parent, name, floorPos, 20f);
            BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.85f, 0f); bc.size = new Vector3(0.6f, 1.7f, 0.6f);
            Prim(t, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.5f, 0.02f, 0.5f), mBlack);
            Prim(t, "Pole", PrimitiveType.Cylinder, new Vector3(0f, 0.85f, 0f), new Vector3(0.035f, 0.85f, 0.035f), mSteel);
            string[] art = { "park_logo", "raptor_poster", "rex_banner", "map_park_modern", "lobby_welcome", "talon_ridge_warning" };
            for (int f = 0; f < 4; f++)
            {
                Transform face = Group(t, $"Face_{f}", t.position, t.eulerAngles.y + f * 90f);
                Box(face, "Wire", new Vector3(0f, 1.0f, -0.2f), new Vector3(0.44f, 1.2f, 0.01f), mSteel);
                for (int row = 0; row < 5; row++)
                for (int col = 0; col < 3; col++)
                    PrintQuad(face, p, $"Card_{row}_{col}", art[(f * 5 + row * 3 + col) % art.Length], new Vector3(-0.14f + col * 0.14f, 0.55f + row * 0.22f, -0.21f), new Vector2(0.12f, 0.17f), false);
            }
        }

        /// <summary>
        /// Review 2: the plaza read as an empty 131 x 64 m parking lot with two dark seams, a slab edge sitting on the lawn
        /// like a box, and the Visitor Center as a glass shoebox. A theme-park forecourt: a paved processional axis with
        /// granite setts and cross bands, a fountain with the park monolith on the axis, ticket kiosks, flagpoles, banners
        /// on the lamp posts and the facade, queue rails, planted beds along the facade, a deep eave and a bigger canopy.
        /// Kept clear: the lobby raptor's mark (-36,-36) and its flee line to (10,-60), the entrance bay x -35..-29.
        /// </summary>
        /// <summary>Timber shade pergola, 7 x 4.4 m, 2.9 m clear: six posts on granite footings, two beams, slatted roof,
        /// two benches underneath. centre = floor centre; yaw turns the long side.</summary>
        private static void Pergola(Transform parent, WorldMaterialPalette p, string name, Vector3 centre, float yaw)
        {
            Transform t = Group(parent, name, centre, yaw);
            for (int i = 0; i < 6; i++)
            {
                float x = -3.3f + (i % 3) * 3.3f, z = i < 3 ? -2.0f : 2.0f;
                Box(t, $"Post_{i}", new Vector3(x, 1.5f, z), new Vector3(0.2f, 3.0f, 0.2f), p.WoodPlanks, true);
                Box(t, $"Footing_{i}", new Vector3(x, 0.1f, z), new Vector3(0.36f, 0.2f, 0.36f), p.DarkGranitePlinth);
            }
            Box(t, "BeamN", new Vector3(0f, 3.08f, 2.0f), new Vector3(7.4f, 0.26f, 0.14f), p.WoodPlanks);
            Box(t, "BeamS", new Vector3(0f, 3.08f, -2.0f), new Vector3(7.4f, 0.26f, 0.14f), p.WoodPlanks);
            for (int r = 0; r < 12; r++)
                Box(t, $"Slat_{r}", new Vector3(-3.3f + r * 0.6f, 3.27f, 0f), new Vector3(0.08f, 0.14f, 4.8f), p.WoodPlanks);
            Bench(t, "BenchA", t.TransformPoint(new Vector3(-1.6f, 0f, 0f)), yaw, 2.2f);
            Bench(t, "BenchB", t.TransformPoint(new Vector3(1.6f, 0f, 0f)), yaw + 180f, 2.2f);
        }

        private static void BuildForecourtReview2(Transform parent, WorldMaterialPalette p)
        {
            var f = new GameObject("Plaza_Forecourt");
            f.transform.SetParent(parent, false);
            Transform t = f.transform;
            Material granite = p.DarkGranitePlinth;
            Material axisPave = p.Surface("MAT_Z1_PlazaAxisPaving", "paving_stone", "BaseColor", "MaskMap", 2.2f, new Color(0.92f, 0.93f, 0.95f), 0.25f, 0.82f, 0f, 1.0f);
            // light granite setts and bands (review 2: dark granite edges and cross bands read as a striped road)
            Material sett = p.Surface("MAT_Z1_PlazaSett", "paving_stone", "BaseColor", "MaskMap", 0.8f, new Color(1.15f, 1.15f, 1.13f), 0.25f, 0.80f, 0f, 1.0f);
            Material grass = p.Surface("MAT_Z1_GrassBerm", "grass_ground", "BaseColor", "MaskMap", 3.0f, new Color(0.92f, 1.0f, 0.90f), 0.05f, 0.35f, 0f, 1.0f);

            // ---- processional axis: 9 m of darker, larger pavers framed by granite setts, granite cross bands every 13 m
            Box(t, "Axis_Paving", new Vector3(-32f, 0.004f, -64.5f), new Vector3(9f, 0.008f, 61f), axisPave);
            Box(t, "Axis_SettW", new Vector3(-36.9f, 0.008f, -64.5f), new Vector3(0.8f, 0.016f, 61f), sett);
            Box(t, "Axis_SettE", new Vector3(-27.1f, 0.008f, -64.5f), new Vector3(0.8f, 0.016f, 61f), sett);
            for (int i = 0; i < 4; i++)
                Box(t, $"Axis_Band_{i}", new Vector3(-32f, 0.006f, -41.5f - i * 13f), new Vector3(9f, 0.012f, 1.2f), sett);
            Box(t, "Facade_Band", new Vector3(-31f, 0.006f, -33.2f), new Vector3(40f, 0.012f, 0.9f), sett);

            // ---- slab edge: a grassed berm along the west, east and south edges so the 0.3 m slab never sits on the lawn
            Box(t, "Berm_W", new Vector3(-101.5f, -0.43f, -64f), new Vector3(0.62f, 0.62f, 64.6f), grass, false, new Vector3(0f, 0f, 45f));
            Box(t, "Berm_E", new Vector3(30.0f, -0.43f, -64f), new Vector3(0.62f, 0.62f, 64.6f), grass, false, new Vector3(0f, 0f, 45f));
            Box(t, "Berm_S", new Vector3(-35.75f, -0.43f, -96.0f), new Vector3(132f, 0.62f, 0.62f), grass, false, new Vector3(45f, 0f, 0f));
            string[] shrubs = { "shrub_01", "shrub_02", "shrub_03", "shrub_04" };
            for (int i = 0; i < 14; i++)
            {
                float z = -36f - i * 4.4f;
                p.InstantiatePropOrFallback(shrubs[i % 4], t, new Vector3(-102.2f, -0.2f, z), new Vector3(0f, i * 61f, 0f), new Vector3(1.6f, 1.2f, 1.6f), grounded: true, addCollider: false);
                p.InstantiatePropOrFallback(shrubs[(i + 2) % 4], t, new Vector3(30.7f, -0.2f, z - 2f), new Vector3(0f, i * 47f, 0f), new Vector3(1.6f, 1.2f, 1.6f), grounded: true, addCollider: false);
            }

            // ---- fountain on the axis with the park monolith
            Vector3 fc = new Vector3(-32f, 0f, -67f);
            const float fr = 4.6f;
            for (int i = 0; i < 20; i++)
            {
                float a = i * Mathf.PI * 2f / 20f;
                Vector3 pos = fc + new Vector3(Mathf.Cos(a) * fr, 0.3f, Mathf.Sin(a) * fr);
                Box(t, $"Fountain_Rim_{i}", pos, new Vector3(0.5f, 0.6f, 2f * fr * Mathf.Sin(Mathf.PI / 20f) + 0.06f), sett, true, new Vector3(0f, -a * Mathf.Rad2Deg, 0f));
            }
            Prim(t, "Fountain_Water", PrimitiveType.Cylinder, fc + new Vector3(0f, 0.42f, 0f), new Vector3(2f * fr - 0.3f, 0.01f, 2f * fr - 0.3f), mWater);
            Prim(t, "Fountain_Basin", PrimitiveType.Cylinder, fc + new Vector3(0f, 0.2f, 0f), new Vector3(2f * fr - 0.2f, 0.2f, 2f * fr - 0.2f), granite);
            // a raised bowl (1.6 m) on a fluted stem so the fountain reads above the rim from across the plaza
            Prim(t, "Fountain_Bowl", PrimitiveType.Cylinder, fc + new Vector3(0f, 1.6f, 0f), new Vector3(3.4f, 0.14f, 3.4f), sett);
            Prim(t, "Fountain_BowlLip", PrimitiveType.Cylinder, fc + new Vector3(0f, 1.42f, 0f), new Vector3(2.6f, 0.08f, 2.6f), sett);
            Prim(t, "Fountain_BowlWater", PrimitiveType.Cylinder, fc + new Vector3(0f, 1.75f, 0f), new Vector3(3.1f, 0.01f, 3.1f), mWater);
            Prim(t, "Fountain_Stem", PrimitiveType.Cylinder, fc + new Vector3(0f, 0.95f, 0f), new Vector3(0.8f, 0.55f, 0.8f), sett);
            for (int fl = 0; fl < 8; fl++)
            {
                float a = fl * Mathf.PI / 4f;
                Box(t, $"Fountain_StemFlute_{fl}", fc + new Vector3(Mathf.Cos(a) * 0.4f, 0.95f, Mathf.Sin(a) * 0.4f), new Vector3(0.06f, 1.1f, 0.12f), granite, false, new Vector3(0f, -a * Mathf.Rad2Deg, 0f));
            }
            Transform mono = Group(t, "Fountain_Monolith", fc + new Vector3(0f, 1.75f, 0f), 0f);
            Box(mono, "Stone", new Vector3(0f, 1.6f, 0f), new Vector3(1.3f, 3.2f, 1.3f), granite, true);
            Box(mono, "Cap", new Vector3(0f, 3.27f, 0f), new Vector3(1.45f, 0.14f, 1.45f), mSteel);
            for (int s = 0; s < 4; s++)
            {
                Transform face = Group(mono, $"Plaque_{s}", mono.position, s * 90f);
                PrintQuad(face, p, "Logo", s % 2 == 0 ? "park_logo" : "rex_banner", new Vector3(0f, 2.1f, -0.656f), s % 2 == 0 ? new Vector2(1.0f, 1.0f) : new Vector2(0.6f, 1.2f), false);
            }
            WorldArchitectureKit.ApplySurface(t.Find("Fountain_Basin").gameObject, SurfaceType.Concrete, p);

            // ---- ticket kiosks flanking the approach, a bulb inside each (Plaza group)
            for (int k = 0; k < 2; k++)
            {
                float kx = k == 0 ? -50.5f : -13.5f;
                Transform ks = Group(t, $"TicketKiosk_{k}", new Vector3(kx, 0f, -42f), k == 0 ? 90f : -90f);   // window faces the axis
                BoxCollider kbc = ks.gameObject.AddComponent<BoxCollider>();
                kbc.center = new Vector3(0f, 1.3f, 0f); kbc.size = new Vector3(2.6f, 2.6f, 2.2f);
                Box(ks, "Base", new Vector3(0f, 0.58f, 0f), new Vector3(2.6f, 0.84f, 2.2f), mFeatureGreen);
                for (int cp = 0; cp < 4; cp++)
                    Box(ks, $"CornerPost_{cp}", new Vector3(cp % 2 == 0 ? -1.28f : 1.28f, 1.36f, cp < 2 ? -1.08f : 1.08f), new Vector3(0.1f, 2.72f, 0.1f), mCabinet);
                Box(ks, "Counter", new Vector3(0f, 1.03f, -1.15f), new Vector3(2.4f, 0.06f, 0.32f), mVeneer);
                Box(ks, "BackWall", new Vector3(0f, 1.8f, 1.05f), new Vector3(2.6f, 1.6f, 0.1f), mFeatureGreen);
                Box(ks, "SideL", new Vector3(-1.25f, 1.8f, 0f), new Vector3(0.1f, 1.6f, 2.2f), mFeatureGreen);
                Box(ks, "SideR", new Vector3(1.25f, 1.8f, 0f), new Vector3(0.1f, 1.6f, 2.2f), mFeatureGreen);
                Box(ks, "Window", new Vector3(0f, 1.85f, -1.05f), new Vector3(2.4f, 1.3f, 0.02f), mGlass);
                Box(ks, "Mullion", new Vector3(0f, 1.85f, -1.06f), new Vector3(0.05f, 1.3f, 0.04f), mDarkMetal);
                // framed window (white painted frame, sill) and a striped canvas awning over the counter
                Box(ks, "FrameTop", new Vector3(0f, 2.53f, -1.08f), new Vector3(2.5f, 0.08f, 0.07f), mCabinet);
                Box(ks, "FrameL", new Vector3(-1.21f, 1.85f, -1.08f), new Vector3(0.08f, 1.42f, 0.07f), mCabinet);
                Box(ks, "FrameR", new Vector3(1.21f, 1.85f, -1.08f), new Vector3(0.08f, 1.42f, 0.07f), mCabinet);
                Box(ks, "Sill", new Vector3(0f, 1.0f, -1.16f), new Vector3(2.6f, 0.05f, 0.2f), mCabinet);
                Box(ks, "Plinth", new Vector3(0f, 0.08f, 0f), new Vector3(2.7f, 0.16f, 2.3f), sett);
                for (int st = 0; st < 6; st++)
                {
                    Transform aw = Group(ks, $"Awning_{st}", ks.TransformPoint(new Vector3(-1.25f + st * 0.5f + 0.25f, 2.62f, -1.5f)), ks.eulerAngles.y);
                    aw.localRotation = Quaternion.Euler(-22f, 0f, 0f);
                    Box(aw, "Canvas", Vector3.zero, new Vector3(0.5f, 0.02f, 0.95f), st % 2 == 0 ? mPaintYellow : mPaper);
                    Box(aw, "Valance", new Vector3(0f, -0.1f, -0.47f), new Vector3(0.5f, 0.2f, 0.015f), st % 2 == 0 ? mPaintYellow : mPaper);
                }
                Box(ks, "Roof", new Vector3(0f, 2.72f, -0.2f), new Vector3(3.2f, 0.16f, 3.0f), p.WoodPlanks);
                Box(ks, "RoofFascia", new Vector3(0f, 2.72f, -1.71f), new Vector3(3.22f, 0.24f, 0.04f), mDarkMetal);
                PrintQuad(ks, p, "Sign", "lobby_welcome", new Vector3(0f, 3.15f, -1.0f), new Vector2(2.4f, 0.6f), false);
                Box(ks, "SignBack", new Vector3(0f, 3.15f, -0.97f), new Vector3(2.5f, 0.66f, 0.05f), mDarkMetal);
                // Round 3 (review 2 intro_05/07: the back of kiosk 1 is the centre of the facade glide and read as a plain green
                // box with a blank black plate): a real back. Staff door in a painted frame with handle and kick plate, a
                // louvred vent, a framed park poster and a STAFF ONLY plate, a corner downpipe, a base trim, the top sign
                // printed on both sides, and the roof's rafter ends showing under the eave.
                Box(ks, "BackFrame", new Vector3(-0.55f, 1.1f, 1.115f), new Vector3(1.02f, 2.08f, 0.03f), mCabinet);
                Box(ks, "BackDoor", new Vector3(-0.55f, 1.08f, 1.13f), new Vector3(0.86f, 1.96f, 0.035f), mShutterPaint);
                Box(ks, "BackDoorKick", new Vector3(-0.55f, 0.24f, 1.15f), new Vector3(0.84f, 0.22f, 0.01f), mSteel);
                Box(ks, "BackDoorHandle", new Vector3(-0.2f, 1.05f, 1.17f), new Vector3(0.14f, 0.025f, 0.04f), mSteel);
                Box(ks, "BackDoorLock", new Vector3(-0.2f, 1.2f, 1.155f), new Vector3(0.05f, 0.08f, 0.01f), mSteel);
                Transform so = Group(ks, "BackStaffPlate", ks.TransformPoint(new Vector3(-0.55f, 1.62f, 1.152f)), ks.eulerAngles.y);
                PrintQuad(so, p, "Plate", "staff_only", Vector3.zero, new Vector2(0.36f, 0.18f), true);
                Box(ks, "BackVent", new Vector3(0.55f, 2.2f, 1.115f), new Vector3(0.5f, 0.32f, 0.03f), mCabinet);
                for (int lv = 0; lv < 5; lv++)
                    Box(ks, $"BackVentLouvre_{lv}", new Vector3(0.55f, 2.09f + lv * 0.055f, 1.135f), new Vector3(0.44f, 0.018f, 0.03f), mDarkMetal, false, new Vector3(35f, 0f, 0f));
                Box(ks, "BackPosterFrame", new Vector3(0.55f, 1.45f, 1.115f), new Vector3(0.62f, 0.86f, 0.03f), mDarkMetal);
                Transform bp = Group(ks, "BackPoster", ks.TransformPoint(new Vector3(0.55f, 1.45f, 1.133f)), ks.eulerAngles.y);
                PrintQuad(bp, p, "Art", k == 0 ? "rex_banner" : "raptor_poster", Vector3.zero, new Vector2(0.54f, 0.78f), true);
                Box(ks, "BackTrim", new Vector3(0f, 1.0f, 1.12f), new Vector3(2.62f, 0.05f, 0.04f), mCabinet);
                Box(ks, "BackKick", new Vector3(0f, 0.22f, 1.115f), new Vector3(2.62f, 0.12f, 0.03f), mDarkMetal);
                Prim(ks, "Downpipe", PrimitiveType.Cylinder, new Vector3(1.2f, 1.36f, 1.17f), new Vector3(0.07f, 1.36f, 0.07f), mDarkMetal);
                Box(ks, "Gutter", new Vector3(0f, 2.66f, 1.32f), new Vector3(3.2f, 0.08f, 0.1f), mDarkMetal);
                for (int rf = 0; rf < 6; rf++)
                    Box(ks, $"RafterEnd_{rf}", new Vector3(-1.4f + rf * 0.56f, 2.6f, 1.2f), new Vector3(0.06f, 0.1f, 0.2f), mVeneer);
                // both side walls (the shot-2 glide passes kiosk 1's south side close up): a framed park map board with a
                // ticket-price strip under it and a vertical trim at the corner posts
                for (int sd = 0; sd < 2; sd++)
                {
                    float sx = sd == 0 ? -1.3f : 1.3f;
                    Transform sb = Group(ks, $"SideBoard_{sd}", ks.TransformPoint(new Vector3(sx + (sd == 0 ? -0.02f : 0.02f), 1.75f, 0.0f)), ks.eulerAngles.y + (sd == 0 ? -90f : 90f));
                    Box(sb, "Frame", new Vector3(0f, 0f, 0.0f), new Vector3(1.5f, 1.05f, 0.04f), mDarkMetal);
                    PrintQuad(sb, p, "Map", "map_park_modern", new Vector3(0f, 0.05f, 0.022f), new Vector2(1.4f, 0.85f), true);
                    Box(sb, "PriceStrip", new Vector3(0f, -0.62f, 0.0f), new Vector3(1.5f, 0.16f, 0.03f), mPaintYellow);
                    Transform pl = Group(sb, "PricePrint", sb.TransformPoint(new Vector3(0f, -0.62f, 0.017f)), sb.eulerAngles.y);
                    PrintQuad(pl, p, "Print", "park_logo", Vector3.zero, new Vector2(0.14f, 0.14f), true);
                }
                PrintQuad(ks, p, "SignRear", "lobby_welcome", new Vector3(0f, 3.15f, -0.943f), new Vector2(2.4f, 0.6f), true);
                Box(ks, "Stool", new Vector3(0.3f, 0.75f, 0.2f), new Vector3(0.35f, 0.05f, 0.35f), mBlack);
                Box(ks, "Register", new Vector3(-0.5f, 1.12f, -0.9f), new Vector3(0.35f, 0.12f, 0.3f), mBlack);
                PaperStack(ks, "Tickets", ks.TransformPoint(new Vector3(0.4f, 1.06f, -1.12f)), 5f, 2);
                WorldArchitectureKit.BuildHdrpLight(ks, $"Light_Kiosk_Interior_{k}", ks.TransformPoint(new Vector3(0f, 2.3f, 0f)), Vector3.zero, LightType.Point, Color.white, 160f, 4f, LightGroupId.Plaza, volumetricDimmer: 0f, castShadows: false, colorTempK: 3000f);
            }

            // ---- flagpoles along the facade band and banners on the lamp posts
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -48f + i * 4f : -20f + (i - 2) * 4f;
                Vector3 b = new Vector3(x, 0f, -36.5f);
                Prim(t, $"Flag_Pole_{i}", PrimitiveType.Cylinder, b + new Vector3(0f, 4.5f, 0f), new Vector3(0.1f, 4.5f, 0.1f), mSteel);
                Prim(t, $"Flag_Finial_{i}", PrimitiveType.Sphere, b + new Vector3(0f, 9.05f, 0f), new Vector3(0.18f, 0.18f, 0.18f), p.PaintedMetalYellow);
                Prim(t, $"Flag_Base_{i}", PrimitiveType.Cylinder, b + new Vector3(0f, 0.1f, 0f), new Vector3(0.4f, 0.1f, 0.4f), granite);
                Transform flag = Group(t, $"Flag_{i}", b + new Vector3(0.05f, 8.1f, 0f), i * 9f - 12f);
                // cloth in eight folded strips (a sine wave along the fly, drooping towards the free end) instead of a board
                Material cloth = i % 2 == 0 ? mFeatureGreen : mPaintYellow;
                float px = 0f, pz = 0f;
                for (int st = 0; st < 8; st++)
                {
                    float ang = Mathf.Sin(st * 1.15f + i) * 28f;
                    float w = 0.24f;
                    float cx = px + Mathf.Cos(ang * Mathf.Deg2Rad) * w * 0.5f, cz = pz - Mathf.Sin(ang * Mathf.Deg2Rad) * w * 0.5f;
                    Box(flag, $"Cloth_{st}", new Vector3(cx, -st * 0.035f, cz), new Vector3(w + 0.012f, 1.1f - st * 0.03f, 0.01f), cloth, false, new Vector3(0f, ang, -st * 1.2f));
                    px += Mathf.Cos(ang * Mathf.Deg2Rad) * w; pz -= Mathf.Sin(ang * Mathf.Deg2Rad) * w;
                }
            }
            for (int i = 0; i < 4; i++)
            for (int side = 0; side < 2; side++)
            {
                float lx = side == 0 ? -40.5f : -23.5f, lz = -48f - i * 13f;
                Transform bn = Group(t, $"PostBanner_{i}_{side}", new Vector3(lx, 0f, lz), side == 0 ? -90f : 90f);
                Box(bn, "ArmTop", new Vector3(0f, 3.25f, -0.32f), new Vector3(0.04f, 0.04f, 0.64f), mSteel);
                Box(bn, "ArmBottom", new Vector3(0f, 1.95f, -0.32f), new Vector3(0.04f, 0.04f, 0.64f), mSteel);
                Box(bn, "Cloth", new Vector3(0f, 2.6f, -0.42f), new Vector3(0.012f, 1.25f, 0.5f), (i + side) % 2 == 0 ? mFeatureGreen : mBinderRed);
                Transform pf = Group(bn, "PrintA", bn.TransformPoint(new Vector3(0.008f, 2.6f, -0.42f)), bn.eulerAngles.y + 90f);
                PrintQuad(pf, p, "Art", (i + side) % 2 == 0 ? "rex_banner" : "raptor_poster", Vector3.zero, new Vector2(0.46f, 1.1f), true);
                Transform pb = Group(bn, "PrintB", bn.TransformPoint(new Vector3(-0.008f, 2.6f, -0.42f)), bn.eulerAngles.y - 90f);
                PrintQuad(pb, p, "Art", (i + side) % 2 == 0 ? "rex_banner" : "raptor_poster", Vector3.zero, new Vector2(0.46f, 1.1f), true);
            }

            // ---- round tree islands in both halves of the field and a café terrace with umbrellas by the gift shop
            Vector2[] islands = { new(-88f, -55f), new(-88f, -81f), new(-52f, -62f), new(20f, -57f), new(20f, -84f), new(-12f, -67f) };
            for (int i = 0; i < islands.Length; i++) TreeIsland(t, p, $"TreeIsland_{i}", new Vector3(islands[i].x, 0f, islands[i].y), 2.3f, 140 + i);
            // review 2: the west and south fields were still bare paving. More tree islands, concrete planters and three
            // timber shade pergolas with benches; the raptor's line (-36,-36) -> (10,-60) and the axis stay clear.
            Vector2[] islands2 = { new(-74f, -56f), new(-74f, -76f), new(-61f, -88f), new(-92f, -68f), new(-17f, -87f), new(4f, -89f), new(9f, -75f), new(-49f, -76f), new(-47f, -86f) };
            for (int i = 0; i < islands2.Length; i++) TreeIsland(t, p, $"TreeIsland_W{i}", new Vector3(islands2[i].x, 0f, islands2[i].y), 2.1f, 150 + i);
            Vector3[] planters = { new(-64f, 0f, -50f), new(-82f, 0f, -90f), new(-54f, 0f, -88f), new(14f, 0f, -92f), new(-8f, 0f, -92f), new(-44f, 0f, -90f), new(-96f, 0f, -48f), new(24f, 0f, -70f) };
            for (int i = 0; i < planters.Length; i++)
            {
                p.InstantiatePropOrFallback("planter_concrete", t, planters[i], new Vector3(0f, i * 23f, 0f), new Vector3(1.5f, 0.8f, 1.5f), grounded: true, addCollider: true);
                p.InstantiatePropOrFallback(i % 2 == 0 ? "shrub_02" : "monstera_clump", t, planters[i] + new Vector3(0f, 0.75f, 0f), new Vector3(0f, i * 57f, 0f), new Vector3(1.3f, 1.1f, 1.3f), grounded: true, addCollider: false);
            }
            Pergola(t, p, "Pergola_W", new Vector3(-66f, 0f, -67f), 0f);
            Pergola(t, p, "Pergola_S", new Vector3(-6f, 0f, -80f), 90f);
            Pergola(t, p, "Pergola_SW", new Vector3(-84f, 0f, -79f), 90f);
            for (int u = 0; u < 4; u++)
            {
                Vector3 c = new Vector3(-80f + u * 5.2f, 0f, -39.5f - (u % 2) * 2.2f);
                Transform ut = Group(t, $"Cafe_Table_{u}", c, u * 23f);
                BoxCollider ubc = ut.gameObject.AddComponent<BoxCollider>();
                ubc.center = new Vector3(0f, 0.38f, 0f); ubc.size = new Vector3(0.9f, 0.76f, 0.9f);
                Prim(ut, "Top", PrimitiveType.Cylinder, new Vector3(0f, 0.74f, 0f), new Vector3(0.9f, 0.02f, 0.9f), mLaminate);
                Prim(ut, "Leg", PrimitiveType.Cylinder, new Vector3(0f, 0.37f, 0f), new Vector3(0.06f, 0.37f, 0.06f), mSteel);
                Prim(ut, "Foot", PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f), new Vector3(0.5f, 0.015f, 0.5f), mSteel);
                Prim(ut, "UmbrellaPole", PrimitiveType.Cylinder, new Vector3(0f, 1.2f, 0f), new Vector3(0.04f, 1.2f, 0.04f), mSteel);
                Material cloth = u % 2 == 0 ? mFeatureGreen : mPaintYellow;
                Prim(ut, "Canopy", PrimitiveType.Cylinder, new Vector3(0f, 2.3f, 0f), new Vector3(2.6f, 0.03f, 2.6f), cloth);
                Prim(ut, "CanopyCrown", PrimitiveType.Cylinder, new Vector3(0f, 2.42f, 0f), new Vector3(1.4f, 0.09f, 1.4f), cloth);
                Prim(ut, "Finial", PrimitiveType.Sphere, new Vector3(0f, 2.55f, 0f), new Vector3(0.1f, 0.1f, 0.1f), mSteel);
                for (int ch = 0; ch < 2; ch++) Chair(ut, $"Chair_{ch}", ut.TransformPoint(new Vector3(ch == 0 ? -0.75f : 0.75f, 0f, 0f)), ut.eulerAngles.y + (ch == 0 ? 90f : -90f));
            }
            for (int d = 0; d < 6; d++)   // wear on the paving where people walk
                DecalAt(t, $"Plaza_Wear_{d}", p.DecalGrimeLeak, new Vector3(-32f + (d % 2 == 0 ? -2.5f : 2.5f), 0.03f, -40f - d * 9f), 3.2f, d * 47f, 0.35f);

            // ---- queue rails west of the entrance (no colliders: cosmetic, clear of the raptor's mark)
            for (int q = 0; q < 4; q++)
            {
                float z = -36.5f - q * 1.4f;
                for (int s = 0; s < 2; s++)
                {
                    float x = s == 0 ? -42.4f : -38.4f;
                    Prim(t, $"Queue_Post_{q}_{s}", PrimitiveType.Cylinder, new Vector3(x, 0.5f, z), new Vector3(0.06f, 0.5f, 0.06f), mSteel);
                    Prim(t, $"Queue_Foot_{q}_{s}", PrimitiveType.Cylinder, new Vector3(x, 0.015f, z), new Vector3(0.32f, 0.015f, 0.32f), mBlack);
                }
                Box(t, $"Queue_Belt_{q}", new Vector3(-40.4f, 0.92f, z), new Vector3(4.0f, 0.05f, 0.01f), mBinderRed);
            }

            // ---- facade: granite plinth, planted beds, banners, a deep eave on the atrium, uplights
            Box(t, "Facade_Plinth_W", new Vector3(-42.6f, 0.15f, -32.32f), new Vector3(15.0f, 0.3f, 0.34f), granite, true);
            Box(t, "Facade_Plinth_E", new Vector3(-20.5f, 0.15f, -32.32f), new Vector3(17.0f, 0.3f, 0.34f), granite, true);
            PlantingBed(t, p, "Facade_Bed_W", new Vector3(-44.5f, 0f, -34.6f), 8.0f, 2.2f, 90);
            PlantingBed(t, p, "Facade_Bed_E", new Vector3(-19.0f, 0f, -34.6f), 8.0f, 2.2f, 91);
            for (int bnr = 0; bnr < 2; bnr++)
            {
                float x = bnr == 0 ? -42.5f : -20.5f;
                Transform fb = Group(t, $"Facade_Banner_{bnr}", new Vector3(x, 0f, -32.55f), 0f);
                Box(fb, "RodTop", new Vector3(0f, 9.6f, 0f), new Vector3(2.3f, 0.06f, 0.06f), mSteel);
                Box(fb, "RodBottom", new Vector3(0f, 4.4f, 0f), new Vector3(2.3f, 0.06f, 0.06f), mSteel);
                Box(fb, "BracketTop", new Vector3(0f, 9.6f, 0.18f), new Vector3(0.06f, 0.06f, 0.36f), mSteel);
                Box(fb, "Cloth", new Vector3(0f, 7.0f, 0f), new Vector3(2.2f, 5.1f, 0.012f), mFeatureGreen);
                PrintQuad(fb, p, "Art", bnr == 0 ? "rex_banner" : "raptor_poster", new Vector3(0f, 7.0f, -0.008f), new Vector2(2.1f, 4.9f), false);
                WorldArchitectureKit.BuildHdrpLight(t, $"Light_FacadeUplight_{bnr}", new Vector3(x, 0.32f, -34.0f), Quaternion.LookRotation((new Vector3(x, 7.0f, -32.6f) - new Vector3(x, 0.32f, -34.0f)).normalized).eulerAngles, LightType.Spot, Color.white, 1800f, 14f, LightGroupId.Plaza, volumetricDimmer: 0.1f, castShadows: false, spotOuterAngle: 60f, colorTempK: 3000f);
            }
            // deep eave along the top of the atrium facade (timber soffit, dark fascia) and returns down the sides
            Box(t, "Eave_Soffit", new Vector3(-31f, 11.05f, -33.6f), new Vector3(40.4f, 0.12f, 2.9f), p.WoodPlanks, false);
            Box(t, "Eave_Deck", new Vector3(-31f, 11.25f, -33.6f), new Vector3(40.6f, 0.28f, 3.0f), p.RoofBitumen, false);
            Box(t, "Eave_Fascia", new Vector3(-31f, 11.2f, -35.12f), new Vector3(40.8f, 0.7f, 0.08f), mDarkMetal, false);
            for (int b = 0; b < 9; b++)
                Box(t, $"Eave_Rafter_{b}", new Vector3(-50.2f + b * 4.8f, 10.92f, -33.6f), new Vector3(0.16f, 0.16f, 2.9f), p.WoodPlanks, false);
            WorldArchitectureKit.BuildRainBlockerSlab(t, "RainBlock_Eave", new Vector3(-31f, 11.45f, -33.6f), new Vector3(40.4f, 0.12f, 2.9f));
            // returns of the eave down both sides of the atrium (review 2: the eave was a slab over the front only, so the
            // building still read as a flat-lidded box from the plaza and from the air)
            for (int sd = 0; sd < 2; sd++)
            {
                float x = sd == 0 ? -51.2f : -10.8f, fx = sd == 0 ? -52.66f : -9.34f;
                Box(t, $"EaveReturn_Soffit_{sd}", new Vector3(x, 11.05f, -6.6f), new Vector3(2.9f, 0.12f, 51.0f), p.WoodPlanks, false);
                Box(t, $"EaveReturn_Deck_{sd}", new Vector3(x, 11.25f, -6.6f), new Vector3(3.0f, 0.28f, 51.2f), p.RoofBitumen, false);
                Box(t, $"EaveReturn_Fascia_{sd}", new Vector3(fx, 11.2f, -6.6f), new Vector3(0.08f, 0.7f, 51.4f), mDarkMetal, false);
                for (int b = 0; b < 11; b++)
                    Box(t, $"EaveReturn_Rafter_{sd}_{b}", new Vector3(x, 10.92f, -30f + b * 4.8f), new Vector3(2.9f, 0.16f, 0.16f), p.WoodPlanks, false);
            }
        }

        /// <summary>Window glass for the Visitor Center: the palette glass with a near-black base colour (review 1: the
        /// 0.70 base at 14 % coverage was lit by the room lamps into a milky film; a window onto the night must be the
        /// darkest thing in a lit room). Keeps the specular reflections and a faint blue-green tint.</summary>
        private static Material BuildWindowGlass(WorldMaterialPalette p)
        {
            const string path = WorldMaterialPalette.MaterialsOutputFolder + "/MAT_Z1_WindowGlass.mat";
            Material src = p.GlassArchitectural;
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(src) { name = "MAT_Z1_WindowGlass" }; AssetDatabase.CreateAsset(m, path); }
            else m.CopyPropertiesFromMaterial(src);
            m.SetColor("_BaseColor", new Color(0.020f, 0.030f, 0.032f, 0.16f));
            m.SetFloat("_Smoothness", 0.97f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Control-room desk screens: a camera-feed picture (the park map as a status display) glowing cyan-white
        /// at ~30 nits, switched off with VCMain.</summary>
        private static Material s_cctvFeed;
        /// <summary>Night CCTV feed: the jungle aerial as a cool, emissive monochrome-ish picture bright enough to glow at the
        /// lit-room exposure (the old MAT_Z1_FeedJungle at 3.2 read as khaki terrain).</summary>
        private static Material CctvFeedMaterial()
        {
            if (s_cctvFeed == null && s_palette != null)
                s_cctvFeed = s_palette.CreateTextureQuadMaterial("MAT_Z1_CctvFeed", "backdrop_jungle_aerial", true, 5.0f, new Color(0.46f, 0.62f, 0.80f));
            return s_cctvFeed != null ? s_cctvFeed : mScreenCyan;
        }

        private static Material BuildFeedScreen(WorldMaterialPalette p)
        {
            Material m = p.CreateTextureQuadMaterial("MAT_Z1_FeedScreen", "map_park_modern", true, 3.0f, Color.white);
            return m;
        }

        /// <summary>Perimeter washes (rule "_Wash"): a shadowless spot 2.5 m off a wall, 1.5 m up, aimed 45 deg up at the
        /// wall/ceiling junction, standing in for the light a lit room throws back onto the top of its walls. The troffer
        /// cones stop just under the tiles, so without them a dark seam sits on every far junction (review 2 blocker).
        /// x0..x1 / z0..z1 are the room's inner bounds (0.5 m inside the wall lines); walls = one letter per wash
        /// (N = +z, S = -z, E = +x, W = -x; a letter repeated spreads its washes along that wall). At most 6 per room:
        /// HDRP drops lights from crowded light-list cells (blocky patches); the companion troffers gave up their wall
        /// fills and half their bounces to pay for them (LookFixtures.ThinCompanionBounce).</summary>
        private static void Washes(Transform parent, string room, float x0, float x1, float z0, float z1, LightGroupId group, float kelvin = 5000f, string walls = "NESW")
        {
            // fix round: the gift shop (21 x 19 m) in 7.5 m cells, 9 fills instead of 16 (light-list pressure)
            if (room != "Control") CeilingFill(parent, room, x0, x1, z0, z1, CeilStd, group, kelvin, room == "GiftShop" ? 7.5f : 6f);   // the control room's ceiling already reads (review 2: ~120 over ~106 walls)
            const float off = 2.5f - 0.5f;   // 2.5 m from the wall line: the core's 0.45 inner cone gives each wash a ~7 m stretch
            int i = 0;
            foreach (char side in "NESW")
            {
                int n = 0;
                foreach (char c in walls) if (c == side) n++;
                for (int k = 0; k < n && i < 6; k++)
                {
                    float t = (k + 0.5f) / n;
                    Vector3 pos; float yaw;
                    switch (side)
                    {
                        case 'N': pos = new Vector3(Mathf.Lerp(x0, x1, t), 1.5f, z1 - off); yaw = 0f; break;
                        case 'S': pos = new Vector3(Mathf.Lerp(x0, x1, t), 1.5f, z0 + off); yaw = 180f; break;
                        case 'E': pos = new Vector3(x1 - off, 1.5f, Mathf.Lerp(z0, z1, t)); yaw = 90f; break;
                        default:  pos = new Vector3(x0 + off, 1.5f, Mathf.Lerp(z0, z1, t)); yaw = 270f; break;
                    }
                    string name = i == 0 ? $"Light_{room}_Wash" : $"Light_{room}_Wash_{i}";
                    Light w = WorldArchitectureKit.BuildHdrpLight(parent, name, pos, new Vector3(-45f, yaw, 0f), LightType.Spot, Color.white, 450f, 7.5f, group, volumetricDimmer: 0f, castShadows: false, spotOuterAngle: 150f, colorTempK: kelvin);
                    // light layer: walls and furniture only (TagWashReceivers), so the tiles near the junction get no hot band
                    if (w != null) w.renderingLayerMask = (int)WashLayer;
                    i++;
                }
            }
        }

        // =========================================================================
        // ROUND 4 (review 3): recognisable desk objects, written words (TextMeshPro in the park's own OFL fonts), and the
        // dressing of the halls the review cameras face. Helpers first, then DressRoomsRound4.
        // =========================================================================

        private static readonly Dictionary<string, Material> s_inkMats = new Dictionary<string, Material>();

        /// <summary>HDRP-lit SDF material for world text in one font and one ink colour (MAT_Z1_Ink_*).</summary>
        private static Material InkMat(TMPro.TMP_FontAsset font, string fontKey, Color ink)
        {
            string name = $"MAT_Z1_Ink_{fontKey}_{ColorUtility.ToHtmlStringRGB(ink)}";
            if (s_inkMats.TryGetValue(name, out Material cached) && cached != null) return cached;
            Shader sh = Shader.Find("TextMeshPro/SRP/TMP_SDF-HDRP LIT");
            if (font == null || sh == null) return null;
            string folder = WorldMaterialPalette.MaterialsOutputFolder + "/Text";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(WorldMaterialPalette.MaterialsOutputFolder, "Text");
            string path = $"{folder}/{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(font.material) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetTexture("_MainTex", font.atlasTexture);
            if (m.HasProperty("_FaceColor")) m.SetColor("_FaceColor", ink);
            if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", 0f);
            EditorUtility.SetDirty(m);
            s_inkMats[name] = m;
            return m;
        }

        private static readonly Color InkBlue = new Color(0.10f, 0.20f, 0.55f), InkRed = new Color(0.62f, 0.10f, 0.08f),
            InkBlack = new Color(0.07f, 0.07f, 0.08f), InkGreen = new Color(0.08f, 0.36f, 0.16f), InkWhite = new Color(0.93f, 0.93f, 0.90f),
            InkPencil = new Color(0.22f, 0.22f, 0.25f);

        /// <summary>World text on a surface. localPos/localRot in the parent's space; the text is visible from local -Z of
        /// its own rotation (like a Quad): pass Quaternion.Euler(0,180,0) for a face whose readers stand at the parent's +Z,
        /// Euler(90,0,0) for text lying flat and read from -Z. size = rect in metres; the font shrinks to fit (max fontMax).</summary>
        private static TMPro.TextMeshPro Ink(Transform parent, string name, string text, Vector3 localPos, Quaternion localRot, Vector2 size,
            Color ink, string font = "Caveat-Regular", float fontMax = 0.6f, TMPro.TextAlignmentOptions align = TMPro.TextAlignmentOptions.TopLeft, float lineSpacing = 0f)
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>($"Assets/Resources/UI/Fonts/{font} SDF.asset");
            if (fa == null) return null;
            var go = new GameObject(name);
            go.layer = GameLayers.Environment;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            tmp.font = fa;
            Material mat = InkMat(fa, font.Replace("-", ""), ink);
            if (mat != null) tmp.fontSharedMaterial = mat;
            tmp.text = text;
            tmp.alignment = align;
            tmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.02f;
            tmp.fontSizeMax = fontMax;
            tmp.lineSpacing = lineSpacing;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = size;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tmp;
        }

        private static readonly Dictionary<string, Mesh> s_meshes = new Dictionary<string, Mesh>();

        /// <summary>Open frustum (lamp shade, bucket, cone), axis local +Y, both faces drawn. Mesh is embedded in the scene.</summary>
        private static GameObject Frustum(Transform parent, string name, Vector3 localPos, float rBottom, float rTop, float height, Material mat, Vector3? euler = null, int seg = 28, bool capTop = false)
        {
            string key = $"Z1Frustum_{rBottom:F3}_{rTop:F3}_{height:F3}_{seg}_{capTop}";
            if (!s_meshes.TryGetValue(key, out Mesh mesh) || mesh == null)
            {
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
                float slope = (rBottom - rTop) / height;
                for (int side = 0; side < 2; side++)
                {
                    int b = v.Count;
                    for (int i = 0; i <= seg; i++)
                    {
                        float a = i * Mathf.PI * 2f / seg; float c = Mathf.Cos(a), s = Mathf.Sin(a);
                        Vector3 nrm = new Vector3(c, slope, s).normalized * (side == 0 ? 1f : -1f);
                        v.Add(new Vector3(c * rBottom, 0f, s * rBottom)); n.Add(nrm); uv.Add(new Vector2(i / (float)seg, 0f));
                        v.Add(new Vector3(c * rTop, height, s * rTop)); n.Add(nrm); uv.Add(new Vector2(i / (float)seg, 1f));
                    }
                    for (int i = 0; i < seg; i++)
                    {
                        int i0 = b + i * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                        if (side == 0) { tri.Add(i0); tri.Add(i1); tri.Add(i2); tri.Add(i2); tri.Add(i1); tri.Add(i3); }
                        else { tri.Add(i0); tri.Add(i2); tri.Add(i1); tri.Add(i2); tri.Add(i3); tri.Add(i1); }
                    }
                }
                if (capTop)
                {
                    int cIdx = v.Count; v.Add(new Vector3(0f, height, 0f)); n.Add(Vector3.up); uv.Add(new Vector2(0.5f, 0.5f));
                    int rb = v.Count;
                    for (int i = 0; i <= seg; i++) { float a = i * Mathf.PI * 2f / seg; v.Add(new Vector3(Mathf.Cos(a) * rTop, height, Mathf.Sin(a) * rTop)); n.Add(Vector3.up); uv.Add(new Vector2(0.5f, 0.5f)); }
                    for (int i = 0; i < seg; i++) { tri.Add(cIdx); tri.Add(rb + i + 1); tri.Add(rb + i); }
                }
                mesh = new Mesh { name = key };
                mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                s_meshes[key] = mesh;
            }
            var go = new GameObject(name);
            go.layer = GameLayers.Environment;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static Material mBeigePlastic, mKeycap, mKeycapDark, mEnamelFridge, mTowel, mJacketRed, mJacketNavy, mRubberMat, mBrass, mFirstAid, mFelt;

        private static void InitRound4Materials(WorldMaterialPalette p)
        {
            // 1990s office plastics: warm-grey ABS (the 'black slabs' of review 3 were mBlack at 13 % albedo)
            mBeigePlastic = p.Surface("MAT_Z1_BeigeABS", "drywall_paint", "BaseColor", "MaskMap", 0.6f, new Color(1.12f, 1.08f, 0.98f), 0.35f, 0.55f, 0f, 0.1f);
            mKeycap       = p.Surface("MAT_Z1_Keycap", "drywall_paint", "BaseColor", "MaskMap", 0.5f, new Color(1.25f, 1.22f, 1.14f), 0.40f, 0.60f, 0f, 0.05f);
            mKeycapDark   = p.Surface("MAT_Z1_KeycapDark", "drywall_paint", "BaseColor", "MaskMap", 0.5f, new Color(0.62f, 0.62f, 0.62f), 0.40f, 0.60f, 0f, 0.05f);
            mEnamelFridge = p.Surface("MAT_Z1_FridgeEnamel", "drywall_paint", "BaseColor", "MaskMap", 1.5f, new Color(1.42f, 1.40f, 1.30f), 0.58f, 0.66f, 0f, 0.05f);
            mTowel        = p.Surface("MAT_Z1_Towel", "carpet_office", "BaseColor", "MaskMap", 0.35f, new Color(0.70f, 1.05f, 1.45f), 0.0f, 0.15f, 0f, 0.6f);
            mJacketRed    = p.Surface("MAT_Z1_JacketRed", "carpet_office", "BaseColor", "MaskMap", 0.6f, new Color(1.25f, 0.30f, 0.22f), 0.05f, 0.30f, 0f, 0.5f);
            mJacketNavy   = p.Surface("MAT_Z1_JacketNavy", "carpet_office", "BaseColor", "MaskMap", 0.6f, new Color(0.30f, 0.38f, 0.62f), 0.05f, 0.30f, 0f, 0.5f);
            mRubberMat    = p.Surface("MAT_Z1_RubberMat", "floor_tiles_industrial", "BaseColor", "MaskMap", 0.3f, new Color(0.32f, 0.33f, 0.34f), 0.10f, 0.30f, 0f, 0.8f);
            mBrass        = p.Surface("MAT_Z1_Brass", "steel_brushed", "BaseColor", "MaskMap", 1.0f, new Color(1.25f, 1.00f, 0.55f), 0.55f, 0.80f, 0.9f, 0.1f);
            mFirstAid     = p.Surface("MAT_Z1_FirstAidGreen", "metal_painted_worn", "grey_BaseColor", "MaskMap", 1.0f, new Color(0.25f, 0.72f, 0.40f), 0.35f, 0.65f, 0.1f, 0.2f);
            InitRound4Extra(p);
            mFelt         = p.Surface("MAT_Z1_FeltBoard", "carpet_office", "BaseColor", "MaskMap", 0.4f, new Color(0.38f, 0.52f, 0.72f), 0.0f, 0.15f, 0f, 0.6f);
        }

        /// <summary>A 1990s beige keyboard: case with a raised back edge, five rows of keycaps with the darker modifier keys,
        /// a space bar, F-key row and the numpad. pos = centre of the case on the desk top; front faces local -Z.</summary>
        private static void DeskKeyboard(Transform parent, string name, Vector3 localPos, float yaw)
        {
            var g = new GameObject(name); g.layer = GameLayers.Environment; g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos; g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform t = g.transform;
            const float W = 0.46f, D = 0.17f;
            Box(t, "Case", new Vector3(0f, 0.011f, 0f), new Vector3(W, 0.022f, D), mBeigePlastic, false, new Vector3(-3f, 0f, 0f));
            Box(t, "BackEdge", new Vector3(0f, 0.024f, D * 0.5f - 0.012f), new Vector3(W, 0.012f, 0.024f), mBeigePlastic);
            const float k = 0.0175f, pitch = 0.019f;
            float x0 = -W * 0.5f + 0.018f;
            int[] rowKeys = { 15, 14, 13, 12 };
            for (int r = 0; r < 4; r++)
            {
                float z = -D * 0.5f + 0.045f + r * pitch;
                float y = 0.026f + r * 0.0012f;
                float off = r * 0.0045f;
                for (int c = 0; c < rowKeys[r]; c++)
                {
                    bool mod = c == 0 || c == rowKeys[r] - 1;
                    float w = mod ? k * (1.2f + r * 0.15f) : k;
                    float x = x0 + off + c * pitch + (mod && c > 0 ? (w - k) * 0.5f : 0f);
                    Box(t, $"K{r}_{c}", new Vector3(x, y, z), new Vector3(w, 0.008f, k), mod ? mKeycapDark : mKeycap);
                }
            }
            // bottom row: ctrl / alt / space
            float zb = -D * 0.5f + 0.045f - pitch;
            Box(t, "Space", new Vector3(x0 + 0.13f, 0.025f, zb), new Vector3(0.115f, 0.008f, k), mKeycap);
            for (int c = 0; c < 3; c++) Box(t, $"KB_L{c}", new Vector3(x0 + 0.004f + c * 0.022f, 0.025f, zb), new Vector3(0.02f, 0.008f, k), mKeycapDark);
            for (int c = 0; c < 3; c++) Box(t, $"KB_R{c}", new Vector3(x0 + 0.205f + c * 0.022f, 0.025f, zb), new Vector3(0.02f, 0.008f, k), mKeycapDark);
            // function row
            for (int c = 0; c < 12; c++) Box(t, $"F{c}", new Vector3(x0 + 0.03f + c * 0.0215f + (c / 4) * 0.008f, 0.0285f, D * 0.5f - 0.035f), new Vector3(0.017f, 0.008f, 0.013f), mKeycapDark);
            // arrows + numpad
            float nx = x0 + 0.33f;
            for (int r = 0; r < 5; r++) for (int c = 0; c < 4; c++)
                if (!(r == 0 && c == 3)) Box(t, $"N{r}_{c}", new Vector3(nx + c * pitch, 0.025f + r * 0.0012f, -D * 0.5f + 0.026f + r * pitch), new Vector3(k, 0.008f, k), (c == 3 || r == 4) ? mKeycapDark : mKeycap);
            Box(t, "LedStrip", new Vector3(nx + 0.03f, 0.0285f, D * 0.5f - 0.035f), new Vector3(0.05f, 0.004f, 0.01f), mBlack);
        }

        /// <summary>1990s desk phone: sloped body, handset in its cradle, 12-key pad, line buttons, a coiled cord to the
        /// handset. pos = centre of the base on the desk; front faces local -Z.</summary>
        private static void DeskPhone(Transform parent, string name, Vector3 localPos, float yaw)
        {
            var g = new GameObject(name); g.layer = GameLayers.Environment; g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos; g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform t = g.transform;
            Box(t, "Base", new Vector3(0f, 0.018f, 0f), new Vector3(0.19f, 0.036f, 0.22f), mBeigePlastic);
            Box(t, "Slope", new Vector3(0f, 0.045f, 0.01f), new Vector3(0.19f, 0.03f, 0.2f), mBeigePlastic, false, new Vector3(-12f, 0f, 0f));
            Box(t, "Cradle", new Vector3(0f, 0.075f, 0.075f), new Vector3(0.19f, 0.03f, 0.06f), mBeigePlastic);
            // handset: grip and the two cups
            Box(t, "HandsetGrip", new Vector3(0f, 0.098f, 0.075f), new Vector3(0.15f, 0.022f, 0.045f), mBeigePlastic);
            Box(t, "HandsetEar", new Vector3(-0.085f, 0.096f, 0.072f), new Vector3(0.045f, 0.034f, 0.058f), mBeigePlastic, false, new Vector3(0f, 0f, 8f));
            Box(t, "HandsetMouth", new Vector3(0.085f, 0.096f, 0.072f), new Vector3(0.045f, 0.034f, 0.058f), mBeigePlastic, false, new Vector3(0f, 0f, -8f));
            for (int r = 0; r < 4; r++) for (int c = 0; c < 3; c++)
                Box(t, $"Key{r}{c}", new Vector3(-0.035f + c * 0.025f, 0.058f - r * 0.0045f, 0.025f - r * 0.021f), new Vector3(0.019f, 0.006f, 0.015f), mKeycapDark, false, new Vector3(-12f, 0f, 0f));
            for (int b = 0; b < 4; b++)
                Box(t, $"Line{b}", new Vector3(0.062f, 0.058f - b * 0.0045f, 0.025f - b * 0.021f), new Vector3(0.022f, 0.006f, 0.013f), b == 0 ? mBinderRed : mKeycap, false, new Vector3(-12f, 0f, 0f));
            // coiled cord: from the handset's mouth end down the side to the desk, a run of short rings
            Vector3 a = new Vector3(0.11f, 0.085f, 0.06f), m = new Vector3(0.14f, 0.004f, 0.02f), e = new Vector3(0.10f, 0.012f, -0.05f);
            for (int i = 0; i < 14; i++)
            {
                float s = i / 13f;
                Vector3 pnt = Vector3.Lerp(Vector3.Lerp(a, m, s), Vector3.Lerp(m, e, s), s);
                Prim(t, $"Coil{i}", PrimitiveType.Cylinder, pnt + new Vector3(0f, 0.004f, 0f), new Vector3(0.014f, 0.004f, 0.014f), mBlack, new Vector3(90f, 0f, 60f * s));
            }
        }

        /// <summary>The night logbook: an open green ring binder with two ruled, hand-written pages. pos = spine centre on the
        /// desk, pages read from local -Z.</summary>
        private static void Logbook(Transform parent, string name, Vector3 localPos, float yaw, string left, string right)
        {
            var g = new GameObject(name); g.layer = GameLayers.Environment; g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos; g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform t = g.transform;
            const float pw = 0.21f, pd = 0.29f;
            Box(t, "CoverL", new Vector3(-pw * 0.5f - 0.012f, 0.002f, 0f), new Vector3(pw + 0.02f, 0.004f, pd + 0.02f), mFeatureGreen);
            Box(t, "CoverR", new Vector3(pw * 0.5f + 0.012f, 0.002f, 0f), new Vector3(pw + 0.02f, 0.004f, pd + 0.02f), mFeatureGreen);
            Box(t, "Spine", new Vector3(0f, 0.003f, 0f), new Vector3(0.03f, 0.006f, pd + 0.02f), mBinderBlack);
            Box(t, "PagesL", new Vector3(-pw * 0.5f - 0.008f, 0.008f, 0f), new Vector3(pw, 0.008f, pd), mPaper);
            Box(t, "PagesR", new Vector3(pw * 0.5f + 0.008f, 0.009f, 0f), new Vector3(pw, 0.010f, pd), mPaper);
            for (int r = 0; r < 3; r++) Prim(t, $"Ring{r}", PrimitiveType.Cylinder, new Vector3(0f, 0.014f, -0.09f + r * 0.09f), new Vector3(0.024f, 0.003f, 0.024f), mSteel, new Vector3(0f, 0f, 90f));
            // ruled lines (pale blue), then the entries
            for (int l = 0; l < 12; l++)
            {
                Box(t, $"RuleL{l}", new Vector3(-pw * 0.5f - 0.008f, 0.0122f, -pd * 0.5f + 0.03f + l * 0.021f), new Vector3(pw - 0.02f, 0.0004f, 0.0012f), mBinderBlue);
                Box(t, $"RuleR{l}", new Vector3(pw * 0.5f + 0.008f, 0.0142f, -pd * 0.5f + 0.03f + l * 0.021f), new Vector3(pw - 0.02f, 0.0004f, 0.0012f), mBinderBlue);
            }
            Ink(t, "WritingL", left, new Vector3(-pw * 0.5f - 0.004f, 0.0126f, -0.005f), Quaternion.Euler(90f, 0f, 0f), new Vector2(pw - 0.03f, pd - 0.03f), InkBlue, "Caveat-Regular", 0.16f, TMPro.TextAlignmentOptions.TopLeft, 8f);
            Ink(t, "WritingR", right, new Vector3(pw * 0.5f + 0.012f, 0.0146f, -0.005f), Quaternion.Euler(90f, 0f, 0f), new Vector2(pw - 0.03f, pd - 0.03f), InkBlue, "Caveat-Regular", 0.16f, TMPro.TextAlignmentOptions.TopLeft, 8f);
            Prim(t, "Pen", PrimitiveType.Cylinder, new Vector3(pw * 0.5f + 0.03f, 0.022f, 0.02f), new Vector3(0.008f, 0.07f, 0.008f), mBinderBlue, new Vector3(90f, 35f, 0f));
        }

        /// <summary>Balanced-arm desk lamp: weighted round base, lower and upper arms with their springs, an elbow knuckle and
        /// a conical metal shade with its bulb inside. pos = base centre on the desk; the shade points local -Z, down.</summary>
        private static void DeskLamp(Transform parent, string name, Vector3 localPos, float yaw, Material body)
        {
            var g = new GameObject(name); g.layer = GameLayers.Environment; g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos; g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform t = g.transform;
            Prim(t, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.012f, 0f), new Vector3(0.16f, 0.012f, 0.16f), body);
            Frustum(t, "BaseDome", new Vector3(0f, 0.024f, 0f), 0.08f, 0.03f, 0.03f, body, null, 24, true);
            Prim(t, "Pivot", PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.035f, 0.012f, 0.035f), mSteel, new Vector3(0f, 0f, 90f));
            // lower arm leans back (+Z) 20 deg, upper arm comes forward and down
            Vector3 elbow = new Vector3(0f, 0.06f, 0f) + Quaternion.Euler(-20f, 0f, 0f) * new Vector3(0f, 0.34f, 0f);
            for (int s = -1; s <= 1; s += 2)
                Prim(t, $"Lower{(s < 0 ? "L" : "R")}", PrimitiveType.Cylinder, (new Vector3(0f, 0.06f, 0f) + elbow) * 0.5f + new Vector3(s * 0.012f, 0f, 0f), new Vector3(0.009f, 0.17f, 0.009f), body, new Vector3(-20f, 0f, 0f));
            Prim(t, "SpringLow", PrimitiveType.Cylinder, (new Vector3(0f, 0.06f, 0f) + elbow) * 0.5f + new Vector3(0f, 0f, -0.03f), new Vector3(0.012f, 0.12f, 0.012f), mSteel, new Vector3(-16f, 0f, 0f));
            Prim(t, "Elbow", PrimitiveType.Cylinder, elbow, new Vector3(0.03f, 0.014f, 0.03f), mSteel, new Vector3(0f, 0f, 90f));
            Vector3 head = elbow + Quaternion.Euler(-115f, 0f, 0f) * new Vector3(0f, 0.30f, 0f);
            for (int s = -1; s <= 1; s += 2)
                Prim(t, $"Upper{(s < 0 ? "L" : "R")}", PrimitiveType.Cylinder, (elbow + head) * 0.5f + new Vector3(s * 0.012f, 0f, 0f), new Vector3(0.008f, 0.15f, 0.008f), body, new Vector3(-115f, 0f, 0f));
            Prim(t, "SpringUp", PrimitiveType.Cylinder, (elbow + head) * 0.5f + new Vector3(0f, 0.025f, 0f), new Vector3(0.011f, 0.1f, 0.011f), mSteel, new Vector3(-112f, 0f, 0f));
            Prim(t, "HeadKnuckle", PrimitiveType.Cylinder, head, new Vector3(0.026f, 0.012f, 0.026f), mSteel, new Vector3(0f, 0f, 90f));
            // the shade hangs from the knuckle, mouth down and a little forward
            var shade = new GameObject("Shade"); shade.layer = GameLayers.Environment; shade.transform.SetParent(t, false);
            shade.transform.localPosition = head; shade.transform.localRotation = Quaternion.Euler(-150f, 0f, 0f);
            Prim(shade.transform, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 0.025f, 0f), new Vector3(0.035f, 0.025f, 0.035f), body);
            Frustum(shade.transform, "Cone", new Vector3(0f, 0.05f, 0f), 0.035f, 0.085f, 0.11f, body, null, 32, false);
            Frustum(shade.transform, "Rim", new Vector3(0f, 0.158f, 0f), 0.085f, 0.088f, 0.006f, mSteel, null, 32, false);
            Prim(shade.transform, "Bulb", PrimitiveType.Sphere, new Vector3(0f, 0.11f, 0f), new Vector3(0.055f, 0.065f, 0.055f), mOpal);
            // cable from the base to the back of the desk
            Box(t, "Cable", new Vector3(0f, 0.004f, 0.12f), new Vector3(0.006f, 0.006f, 0.2f), mBlack);
        }


        /// <summary>Hand-written texts for the whiteboards, by board seed.</summary>
        private static string BoardText(int seed, out Color ink)
        {
            ink = InkBlue;
            switch (seed)
            {
                case 31: return "<color=#1b2f8c>NIGHT SHIFT - SECURITY</color>\n- Log Talon Ridge + Tyrant Valley feeds <u>first</u>\n- Paddock 4 gate: sticks, report to Ops\n- <color=#9b1a14>NO ONE past E-0 after 22:00</color>\n- Storm kit: torches, batteries, ponchos\n- Radio check every 30 min (ch. 3)";
                case 35: return "<color=#9b1a14>DUTY ROSTER  wk 47</color>\n<color=#111>MON  Vance  / Okafor\nTUE  Vance  / Lindqvist\nWED  Reyes  / Okafor\nTHU  Vance  / Reyes\nFRI  Lindqvist / Halvorsen\nSAT  Reyes  / Vance</color>\nswap? ask Dana";
                case 32: return "<color=#1b2f8c>BREAK ROOM</color>\nFridge clean-out FRIDAY!\nlabel your food - <color=#9b1a14>this means you Marco</color>\nCoffee fund: $2 / week\nRaptor team BBQ sat 6pm\n   bring chairs";
                case 36: return "<color=#14532a>TODAY</color>\nchicken + rice\nveg curry\nkey lime pie\n<color=#9b1a14>kitchen shuts 21:30</color>";
                default: return "check list\n- feeds\n- gates\n- radio";
            }
        }

        /// <summary>Plain notice sheet, typed (Courier) with a coloured heading, on a wall or board. pos on the surface,
        /// facing = into the room.</summary>
        private static void TypedNotice(Transform parent, string name, Vector3 pos, Vector3 facing, Vector2 size, string heading, string body, Color headInk, float roll = 0f, Material sheet = null)
        {
            Transform t = Group(parent, name, pos, YawFacing(facing) + 180f);
            Box(t, "Sheet", new Vector3(0f, 0f, 0.0015f), new Vector3(size.x, size.y, 0.002f), sheet ?? mPaper, false, new Vector3(0f, 0f, roll));
            Quaternion face = Quaternion.Euler(0f, 180f, roll);
            Ink(t, "Head", heading, new Vector3(0f, size.y * 0.36f, 0.0032f), face, new Vector2(size.x * 0.86f, size.y * 0.18f), headInk, "BarlowCondensed-Bold", 0.6f, TMPro.TextAlignmentOptions.Center);
            Ink(t, "Body", body, new Vector3(0f, -size.y * 0.1f, 0.0032f), face, new Vector2(size.x * 0.84f, size.y * 0.66f), InkBlack, "CourierPrime-Regular", 0.3f, TMPro.TextAlignmentOptions.TopLeft);
            Box(t, "Pin", new Vector3(0f, size.y * 0.46f, 0.004f), new Vector3(0.012f, 0.012f, 0.006f), mBinderRed);
        }

        /// <summary>Plaque / door sign: a dark plate with light lettering. pos on the wall, facing into the corridor.</summary>
        private static void RoomPlate(Transform parent, string name, Vector3 pos, Vector3 facing, string text, Vector2 size, Material plate = null, Color? ink = null)
        {
            Transform t = Group(parent, name, pos, YawFacing(facing) + 180f);
            Box(t, "Plate", new Vector3(0f, 0f, 0.005f), new Vector3(size.x, size.y, 0.01f), plate ?? mDarkMetal);
            Ink(t, "Text", text, new Vector3(0f, 0f, 0.0105f), Quaternion.Euler(0f, 180f, 0f), new Vector2(size.x * 0.88f, size.y * 0.8f), ink ?? InkWhite, "BarlowCondensed-SemiBold", 1.2f, TMPro.TextAlignmentOptions.Center);
        }


        private static Material mJacketYellow, mMirror, mRope, mBone, mTub;

        private static void InitRound4Extra(WorldMaterialPalette p)
        {
            mJacketYellow = p.Surface("MAT_Z1_JacketYellow", "carpet_office", "BaseColor", "MaskMap", 0.6f, new Color(1.45f, 1.12f, 0.25f), 0.20f, 0.45f, 0f, 0.4f);
            // no probe in the locker room: a true mirror showed the dark sky probe as a black panel; a glossy grey-blue plate reads as glass
            mMirror       = p.Surface("MAT_Z1_Mirror", "concrete_polished", "BaseColor", "MaskMap", 4.0f, new Color(0.62f, 0.68f, 0.74f), 0.90f, 0.93f, 0.25f, 0.0f);
            mRope         = p.Surface("MAT_Z1_VelvetRope", "carpet_office", "BaseColor", "MaskMap", 0.3f, new Color(1.05f, 0.18f, 0.16f), 0.05f, 0.25f, 0f, 0.5f);
            mBone         = p.Surface("MAT_Z1_FossilBone", "concrete_wall", "BaseColor", "MaskMap", 0.4f, new Color(1.20f, 1.02f, 0.80f), 0.10f, 0.35f, 0f, 0.8f);
            mTub          = p.Surface("MAT_Z1_TubHDPE", "drywall_paint", "BaseColor", "MaskMap", 0.6f, new Color(1.30f, 1.36f, 1.40f), 0.45f, 0.65f, 0f, 0.05f);
        }

        /// <summary>Folded bath towel: three stacked layers with a rolled fold edge and a woven stripe. pos = bottom centre.</summary>
        private static void FoldedTowel(Transform parent, string name, Vector3 pos, float yaw)
        {
            Transform t = Group(parent, name, pos, yaw);
            for (int l = 0; l < 3; l++)
                Box(t, $"Layer_{l}", new Vector3(l * 0.004f, 0.012f + l * 0.024f, l * -0.003f), new Vector3(0.44f, 0.022f, 0.28f), mTowel);
            Prim(t, "FoldEdge", PrimitiveType.Cylinder, new Vector3(0.004f, 0.036f, -0.14f), new Vector3(0.072f, 0.22f, 0.06f), mTowel, new Vector3(0f, 0f, 90f));
            Box(t, "Stripe", new Vector3(0.008f, 0.0735f, 0.08f), new Vector3(0.442f, 0.002f, 0.035f), mPaper);
            Box(t, "Stripe2", new Vector3(0.008f, 0.0735f, 0.04f), new Vector3(0.442f, 0.002f, 0.012f), mPaper);
        }

        /// <summary>A jacket on a wall hook: shoulders, body, sleeves hanging down, hood. hookPos = hook tip on the wall.</summary>
        private static void HangingJacket(Transform parent, string name, Vector3 hookPos, Vector3 facing, Material mat, float tilt)
        {
            Transform t = Group(parent, name, hookPos, YawFacing(facing) + 180f);   // local +Z into the room
            Transform j = new GameObject("Jacket").transform; j.SetParent(t, false); j.gameObject.layer = GameLayers.Environment;
            j.localPosition = new Vector3(0f, 0f, 0.07f); j.localRotation = Quaternion.Euler(0f, 0f, tilt);
            Box(j, "Hood", new Vector3(0f, -0.02f, 0.02f), new Vector3(0.22f, 0.16f, 0.14f), mat, false, new Vector3(-10f, 0f, 0f));
            Box(j, "Shoulders", new Vector3(0f, -0.12f, 0.03f), new Vector3(0.40f, 0.10f, 0.15f), mat);
            Box(j, "Body", new Vector3(0f, -0.45f, 0.04f), new Vector3(0.42f, 0.60f, 0.13f), mat, false, new Vector3(-3f, 0f, 0f));
            Box(j, "Hem", new Vector3(0f, -0.76f, 0.045f), new Vector3(0.43f, 0.04f, 0.135f), mat);
            Box(j, "SleeveL", new Vector3(-0.22f, -0.42f, 0.05f), new Vector3(0.10f, 0.56f, 0.11f), mat, false, new Vector3(0f, 0f, -5f));
            Box(j, "SleeveR", new Vector3(0.22f, -0.42f, 0.05f), new Vector3(0.10f, 0.56f, 0.11f), mat, false, new Vector3(0f, 0f, 5f));
            Box(j, "Zip", new Vector3(0f, -0.45f, 0.112f), new Vector3(0.012f, 0.58f, 0.004f), mBlack);
            Box(j, "Reflect", new Vector3(0f, -0.62f, 0.112f), new Vector3(0.43f, 0.03f, 0.004f), mSteel);
        }

        /// <summary>Recessed red fire-extinguisher cabinet with a glass door, the extinguisher inside and a label.
        /// pos = centre of the cabinet front on the wall face.</summary>
        private static void ExtinguisherCabinet(Transform parent, WorldMaterialPalette p, string name, Vector3 pos, Vector3 facing)
        {
            Transform t = Group(parent, name, pos, YawFacing(facing) + 180f);
            Box(t, "Box", new Vector3(0f, 0f, 0.07f), new Vector3(0.36f, 0.78f, 0.14f), mVendRed);
            Box(t, "Back", new Vector3(0f, 0f, 0.012f), new Vector3(0.30f, 0.70f, 0.01f), mDarkMetal);
            Box(t, "Glass", new Vector3(0f, -0.04f, 0.142f), new Vector3(0.30f, 0.56f, 0.004f), mGlass);
            Box(t, "Label", new Vector3(0f, 0.30f, 0.142f), new Vector3(0.30f, 0.11f, 0.004f), mPaper);
            Ink(t, "LabelText", "FIRE\nEXTINGUISHER", new Vector3(0f, 0.30f, 0.1445f), Quaternion.Euler(0f, 180f, 0f), new Vector2(0.27f, 0.095f), InkRed, "BarlowCondensed-Bold", 0.5f, TMPro.TextAlignmentOptions.Center);
            Prim(t, "Bottle", PrimitiveType.Cylinder, new Vector3(0f, -0.08f, 0.075f), new Vector3(0.13f, 0.21f, 0.13f), mVendRed);
            Box(t, "Valve", new Vector3(0f, 0.17f, 0.075f), new Vector3(0.05f, 0.06f, 0.05f), mBlack);
            Box(t, "Hose", new Vector3(0.07f, 0.02f, 0.11f), new Vector3(0.02f, 0.26f, 0.02f), mBlack);
            Box(t, "Handle", new Vector3(0.13f, -0.04f, 0.15f), new Vector3(0.015f, 0.12f, 0.02f), mSteel);
        }

        /// <summary>Brass stanchion post (weighted base, pole, ball top). floorPos = base centre.</summary>
        private static void Stanchion(Transform parent, string name, Vector3 floorPos)
        {
            Transform t = Group(parent, name, floorPos, 0f);
            Prim(t, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.012f, 0f), new Vector3(0.30f, 0.012f, 0.30f), mBrass);
            Frustum(t, "BaseDome", new Vector3(0f, 0.024f, 0f), 0.15f, 0.04f, 0.05f, mBrass, null, 24, true);
            Prim(t, "Pole", PrimitiveType.Cylinder, new Vector3(0f, 0.50f, 0f), new Vector3(0.05f, 0.45f, 0.05f), mBrass);
            Prim(t, "Top", PrimitiveType.Sphere, new Vector3(0f, 0.97f, 0f), new Vector3(0.075f, 0.075f, 0.075f), mBrass);
        }

        /// <summary>Velvet rope from a to b (world, at the hook height) sagging 0.12 m in the middle (two straight runs).</summary>
        private static void Rope(Transform parent, string name, Vector3 a, Vector3 b)
        {
            Vector3 mid = (a + b) * 0.5f + Vector3.down * 0.12f;
            for (int s = 0; s < 2; s++)
            {
                Vector3 p0 = s == 0 ? a : mid, p1 = s == 0 ? mid : b;
                Vector3 d = p1 - p0;
                Prim(parent, $"{name}_{s}", PrimitiveType.Cylinder, (p0 + p1) * 0.5f, new Vector3(0.032f, d.magnitude * 0.5f, 0.032f), mRope,
                    Quaternion.FromToRotation(Vector3.up, d.normalized).eulerAngles);
            }
        }

        /// <summary>Museum display case: granite plinth with a label, glass hood, a fossil on a rock slab inside.
        /// floorPos = plinth centre; label faces local -Z.</summary>
        private static void DisplayCase(Transform parent, WorldMaterialPalette p, string name, Vector3 floorPos, float yaw, string title, string caption, int kind)
        {
            Transform t = Group(parent, name, floorPos, yaw);
            BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.8f, 0f); bc.size = new Vector3(1.25f, 1.6f, 0.85f);
            Box(t, "Plinth", new Vector3(0f, 0.45f, 0f), new Vector3(1.2f, 0.9f, 0.8f), p.DarkGranitePlinth);
            Box(t, "PlinthCap", new Vector3(0f, 0.91f, 0f), new Vector3(1.24f, 0.03f, 0.84f), mSteel);
            Box(t, "Kick", new Vector3(0f, 0.04f, 0f), new Vector3(1.16f, 0.08f, 0.76f), mBlack);
            Box(t, "Glass", new Vector3(0f, 1.26f, 0f), new Vector3(1.14f, 0.66f, 0.74f), mGlass);
            Box(t, "GlassCap", new Vector3(0f, 1.595f, 0f), new Vector3(1.16f, 0.015f, 0.76f), mSteel);
            Box(t, "Slab", new Vector3(0f, 0.97f, 0.02f), new Vector3(0.8f, 0.09f, 0.46f), p.RockyCliff, false, new Vector3(4f, 6f, 0f));
            if (kind == 0)
            {
                // sickle claw: a curved run of tapering segments, plus two toe bones
                for (int i = 0; i < 7; i++)
                {
                    float a = i * 17f;
                    Vector3 c = new Vector3(-0.18f + Mathf.Sin(a * Mathf.Deg2Rad) * 0.22f, 1.07f + Mathf.Cos(a * Mathf.Deg2Rad) * 0.0f + i * 0.012f, -0.02f + (1f - Mathf.Cos(a * Mathf.Deg2Rad)) * 0.18f);
                    float r = 0.06f - i * 0.0075f;
                    Prim(t, $"Claw_{i}", PrimitiveType.Sphere, c, new Vector3(r, r, r * 1.6f), mBone, new Vector3(0f, -a, 0f));
                }
                Prim(t, "Toe_0", PrimitiveType.Capsule, new Vector3(0.18f, 1.04f, 0.0f), new Vector3(0.05f, 0.11f, 0.05f), mBone, new Vector3(90f, 30f, 0f));
                Prim(t, "Toe_1", PrimitiveType.Capsule, new Vector3(0.26f, 1.04f, 0.12f), new Vector3(0.045f, 0.09f, 0.045f), mBone, new Vector3(90f, 50f, 0f));
            }
            else
            {
                // horn core: a long tapering cone on its stand
                Frustum(t, "Horn", new Vector3(-0.25f, 1.05f, 0.05f), 0.075f, 0.012f, 0.62f, mBone, new Vector3(0f, 0f, -72f), 20, true);
                Prim(t, "HornBase", PrimitiveType.Sphere, new Vector3(-0.25f, 1.05f, 0.05f), new Vector3(0.17f, 0.12f, 0.15f), mBone);
            }
            Box(t, "LabelPlate", new Vector3(0f, 0.72f, -0.405f), new Vector3(0.7f, 0.2f, 0.01f), mBrass);
            Ink(t, "LabelTitle", title, new Vector3(0f, 0.765f, -0.4115f), Quaternion.identity, new Vector2(0.64f, 0.07f), InkBlack, "BarlowCondensed-Bold", 0.6f, TMPro.TextAlignmentOptions.Center);
            Ink(t, "LabelCaption", caption, new Vector3(0f, 0.69f, -0.4115f), Quaternion.identity, new Vector2(0.64f, 0.1f), InkBlack, "Barlow-Regular", 0.35f, TMPro.TextAlignmentOptions.Center);
        }

        /// <summary>Round 4 (review 3: "half the spaces are still bare halls"): the walls and floors the lockers, corridor,
        /// lobby and feed-prep cameras face. Nothing on a walkable line; only the display cases, the info desk and the cart
        /// carry colliders, all against walls or off the route.</summary>
        private static void DressRoomsRound4(Transform root, WorldMaterialPalette p)
        {
            var go = new GameObject("Props_Round4");
            go.transform.SetParent(root, false);
            go.layer = GameLayers.Environment;
            Transform t = go.transform;
            DressLockersR4(t, p);
            DressCorridorR4(t, p);
            DressLobbyR4(t, p);
            DressFeedPrepR4(t, p);
        }

        private static void DressLockersR4(Transform t, WorldMaterialPalette p)
        {
            const float N = 33.843f, W = -57.89f, E = -48.10f;   // N = face of Lining_Lockers_N
            // far wall above the locker run: clock, room sign, a typed notice, the park badge
            WallClock(t, "Lock_Clock", new Vector3(-53.0f, 2.72f, N), Vector3.back, 125f, 62f);
            RoomPlate(t, "Lock_RoomSign", new Vector3(-55.9f, 2.62f, N), Vector3.back, "STAFF LOCKERS", new Vector2(1.25f, 0.24f));
            TypedNotice(t, "Lock_NoticeFriday", new Vector3(-50.2f, 2.62f, N), Vector3.back, new Vector2(0.40f, 0.52f), "NOTICE",
                "Lockers are emptied\nevery FRIDAY 18:00.\nLabel your padlock.\nNo food overnight.\n(We had ants. And\na monkey.)\n   - Facilities", InkRed, 1.5f);
            Poster(t, p, "Lock_ParkBadge", "park_logo", new Vector3(-51.4f, 2.62f, N), Vector3.back, new Vector2(0.42f, 0.42f));
            // east wall: three jackets on the existing hooks, a notice board, the first-aid cabinet
            Material[] jm = { mJacketYellow, mJacketNavy, mJacketRed };
            float[] jz = { 25.45f, 26.35f, 27.25f };
            for (int j = 0; j < 3; j++) HangingJacket(t, $"Lock_Jacket_{j}", new Vector3(E - 0.04f, 1.69f, jz[j]), Vector3.left, jm[j], j == 1 ? -4f : 3f);
            CorkBoard(t, "Lock_Board", new Vector3(E, 1.55f, 23.65f), Vector3.left, 1.4f, 0.95f, 17);   // south of the east locker bank (z 28.6..33.3)
            TypedNotice(t, "Lock_RosterSheet", new Vector3(E - 0.04f, 1.6f, 24.75f), Vector3.left, new Vector2(0.24f, 0.32f), "SHOWERS", "Hot water 05:30-07:00\nand 21:30-23:00 only.\nBoiler is old.", InkBlue, -3f);
            Transform fa = Group(t, "Lock_FirstAid", new Vector3(W, 1.52f, 32.6f), YawFacing(Vector3.right) + 180f);
            Box(fa, "Box", new Vector3(0f, 0f, 0.07f), new Vector3(0.42f, 0.50f, 0.14f), mFirstAid);
            Box(fa, "DoorSeam", new Vector3(0f, 0f, 0.1405f), new Vector3(0.004f, 0.48f, 0.002f), mBlack);
            Box(fa, "CrossV", new Vector3(0f, 0.04f, 0.142f), new Vector3(0.06f, 0.20f, 0.004f), mPaper);
            Box(fa, "CrossH", new Vector3(0f, 0.04f, 0.142f), new Vector3(0.20f, 0.06f, 0.004f), mPaper);
            Ink(fa, "Label", "FIRST AID", new Vector3(0f, -0.16f, 0.1445f), Quaternion.Euler(0f, 180f, 0f), new Vector2(0.34f, 0.07f), InkWhite, "BarlowCondensed-Bold", 0.6f, TMPro.TextAlignmentOptions.Center);
            Box(fa, "Latch", new Vector3(0.17f, 0f, 0.145f), new Vector3(0.02f, 0.06f, 0.012f), mSteel);
            // west wall beyond the locker bank: a mirror over a shelf, a paper-towel dispenser
            Transform mr = Group(t, "Lock_Mirror", new Vector3(W, 1.55f, 31.75f), YawFacing(Vector3.right) + 180f);
            Box(mr, "Frame", new Vector3(0f, 0f, 0.012f), new Vector3(0.66f, 0.92f, 0.024f), mSteel);
            Box(mr, "Glass", new Vector3(0f, 0f, 0.0255f), new Vector3(0.60f, 0.86f, 0.004f), mMirror);
            Box(mr, "Shelf", new Vector3(0f, -0.52f, 0.07f), new Vector3(0.62f, 0.02f, 0.13f), mSteel);
            Prim(mr, "Soap", PrimitiveType.Cylinder, new Vector3(-0.18f, -0.45f, 0.07f), new Vector3(0.05f, 0.06f, 0.05f), mFeatureGreen);
            Prim(mr, "Deodorant", PrimitiveType.Cylinder, new Vector3(0.12f, -0.44f, 0.08f), new Vector3(0.04f, 0.07f, 0.04f), mBinderBlue);
            Transform pt = Group(t, "Lock_TowelDispenser", new Vector3(W, 1.35f, 30.75f), YawFacing(Vector3.right) + 180f);
            Box(pt, "Body", new Vector3(0f, 0f, 0.06f), new Vector3(0.30f, 0.38f, 0.12f), mCabinet);
            Box(pt, "Window", new Vector3(0f, 0.08f, 0.1205f), new Vector3(0.12f, 0.05f, 0.002f), mBlack);
            Box(pt, "Slot", new Vector3(0f, -0.185f, 0.06f), new Vector3(0.22f, 0.012f, 0.06f), mPaper);
            // floor: rubber mats in front of both locker banks, a drain with its grime, the wet-floor sign
            Box(t, "Lock_MatN", new Vector3(-53.0f, 0.006f, 32.85f), new Vector3(6.6f, 0.012f, 0.75f), mRubberMat);
            Box(t, "Lock_MatW", new Vector3(-56.95f, 0.006f, 27.55f), new Vector3(0.7f, 0.012f, 3.9f), mRubberMat);
            Prim(t, "Lock_Drain", PrimitiveType.Cylinder, new Vector3(-53.0f, 0.003f, 28.2f), new Vector3(0.30f, 0.003f, 0.30f), mSteel);
            for (int s = 0; s < 5; s++) Box(t, $"Lock_DrainSlot_{s}", new Vector3(-53.08f + s * 0.04f, 0.0062f, 28.2f), new Vector3(0.012f, 0.001f, 0.18f), mBlack);
            DecalAt(t, "Lock_DrainGrime", p.DecalGrimeLeak, new Vector3(-53.0f, 0.02f, 28.2f), 1.3f, 40f, 0.55f);
            p.InstantiatePropOrFallback("wet_floor_sign", t, new Vector3(-50.2f, 0f, 29.3f), new Vector3(0f, -35f, 0f), new Vector3(0.32f, 0.62f, 0.30f), grounded: true, addCollider: false);
        }

        private static void DressCorridorR4(Transform t, WorldMaterialPalette p)
        {
            const float CN = 21.885f, CS = 19.115f;
            // door plates on the latch side of every door, at eye height
            RoomPlate(t, "Corr_Plate_Security", new Vector3(-61.55f, 1.6f, CN), Vector3.back, "SECURITY", new Vector2(0.46f, 0.15f));
            RoomPlate(t, "Corr_Plate_Lockers", new Vector3(-52.05f, 1.6f, CN), Vector3.back, "STAFF LOCKERS", new Vector2(0.46f, 0.15f));
            RoomPlate(t, "Corr_Plate_Control", new Vector3(-35.75f, 1.6f, CN), Vector3.back, "CONTROL ROOM", new Vector2(0.46f, 0.15f));
            RoomPlate(t, "Corr_Plate_Server", new Vector3(-21.05f, 1.6f, CN), Vector3.back, "SERVER ROOM", new Vector2(0.46f, 0.15f));
            RoomPlate(t, "Corr_Plate_Records", new Vector3(-8.05f, 1.6f, CN), Vector3.back, "RECORDS", new Vector2(0.46f, 0.15f));
            RoomPlate(t, "Corr_Plate_Storage", new Vector3(4.95f, 1.6f, CN), Vector3.back, "UTILITY", new Vector2(0.46f, 0.15f));
            RoomPlate(t, "Corr_Plate_Break", new Vector3(-60.05f, 1.6f, CS), Vector3.forward, "BREAK ROOM", new Vector2(0.46f, 0.15f));
            RoomPlate(t, "Corr_Plate_FeedPrep", new Vector3(-0.75f, 1.6f, CS), Vector3.forward, "FEED PREP", new Vector2(0.46f, 0.15f));
            // a pin board and typed notices on both walls, two extinguisher cabinets
            CorkBoard(t, "Corr_PinBoard_S", new Vector3(-47.6f, 1.55f, CS), Vector3.forward, 1.6f, 1.0f, 23);
            CorkBoard(t, "Corr_PinBoard_N", new Vector3(-44.4f, 1.55f, CN), Vector3.back, 1.2f, 0.9f, 29);
            TypedNotice(t, "Corr_Notice_Radio", new Vector3(-25.2f, 1.55f, CN), Vector3.back, new Vector2(0.30f, 0.40f), "RADIO CHECK", "Channel 3, every\n30 minutes after\n22:00. Log it.\n  - Ops", InkBlue, 2f);
            TypedNotice(t, "Corr_Notice_Storm", new Vector3(-26.2f, 1.6f, CS), Vector3.forward, new Vector2(0.30f, 0.40f), "STORM DRILL", "If the mains drop,\nbattery lamps run\n90 minutes.\nDo NOT open\npaddock gates.", InkRed, -2f);
            TypedNotice(t, "Corr_Notice_Keys", new Vector3(-13.2f, 1.58f, CS), Vector3.forward, new Vector2(0.30f, 0.40f), "MASTER KEYS", "Signed out at the\nRecords desk only.\nReturn by 06:00.", InkBlack, 1f);
            ExtinguisherCabinet(t, p, "Corr_ExtCab_0", new Vector3(-47.35f + 1.55f, 1.15f, CN), Vector3.back);
            ExtinguisherCabinet(t, p, "Corr_ExtCab_1", new Vector3(-34.7f, 1.15f, CS), Vector3.forward);
            ExtinguisherCabinet(t, p, "Corr_ExtCab_2", new Vector3(-15.0f, 1.15f, CN), Vector3.back);
            // a housekeeping cart parked tight to the south wall
            p.InstantiatePropOrFallback("utility_cart", t, new Vector3(-38.6f, 0f, 19.55f), new Vector3(0f, 0f, 0f), new Vector3(1.0f, 0.95f, 0.5f), grounded: true, addCollider: true);
            // a water main and a sprinkler feed under the ceiling along the south side, hung every 2.5 m
            Prim(t, "Corr_WaterMain", PrimitiveType.Cylinder, new Vector3(-32.0f, 3.02f, 19.85f), new Vector3(0.11f, 39.6f, 0.11f), p.PaintedMetalGreen, new Vector3(0f, 0f, 90f));
            Prim(t, "Corr_SprinklerFeed", PrimitiveType.Cylinder, new Vector3(-32.0f, 3.10f, 20.02f), new Vector3(0.06f, 39.6f, 0.06f), mVendRed, new Vector3(0f, 0f, 90f));
            for (int h = 0; h < 32; h++)
            {
                float x = -70.5f + h * 2.5f;
                Box(t, $"Corr_PipeHanger_{h}", new Vector3(x, 3.15f, 19.93f), new Vector3(0.025f, 0.2f, 0.025f), mDarkMetal);
                Box(t, $"Corr_PipeStrap_{h}", new Vector3(x, 2.96f, 19.93f), new Vector3(0.03f, 0.02f, 0.26f), mDarkMetal);
                if (h % 3 == 1) Prim(t, $"Corr_Sprinkler_{h}", PrimitiveType.Cylinder, new Vector3(x + 1.25f, 3.03f, 20.02f), new Vector3(0.03f, 0.04f, 0.03f), mBrass);
            }
            // floor wear down the walking line and scuffs on the wainscot
            DecalAt(t, "Corr_Wear_R4_0", p.DecalGrimeLeak, new Vector3(-45.0f, 0.02f, 20.3f), 2.2f, 55f, 0.45f);
            DecalAt(t, "Corr_Wear_R4_1", p.DecalGrimeLeak, new Vector3(-27.5f, 0.02f, 20.7f), 2.0f, 140f, 0.4f);
            DecalAt(t, "Corr_Wear_R4_2", p.DecalGrimeLeak, new Vector3(-16.0f, 0.02f, 20.4f), 2.4f, 10f, 0.45f);
            WallDecal(t, "Corr_Scuff_R4_0", p.DecalGrimeLeak, new Vector3(-40.5f, 0.35f, 19.25f), Vector3.forward, new Vector2(1.4f, 0.5f), 0.5f);
            WallDecal(t, "Corr_Scuff_R4_1", p.DecalGrimeLeak, new Vector3(-30.0f, 0.35f, 21.75f), Vector3.back, new Vector2(1.2f, 0.45f), 0.45f);
        }

        private static void DressLobbyR4(Transform t, WorldMaterialPalette p)
        {
            // stanchions and a velvet rope round the skeleton's plinth, a plaque on a lectern facing the entrance
            const float x0 = -34.5f, x1 = -27.5f, z0 = -12.7f, z1 = 2.7f;
            var posts = new List<Vector3>();
            for (int i = 0; i <= 3; i++) posts.Add(new Vector3(Mathf.Lerp(x0, x1, i / 3f), 0f, z0));
            for (int i = 1; i <= 6; i++) posts.Add(new Vector3(x1, 0f, Mathf.Lerp(z0, z1, i / 6f)));
            for (int i = 1; i <= 3; i++) posts.Add(new Vector3(Mathf.Lerp(x1, x0, i / 3f), 0f, z1));
            for (int i = 1; i < 6; i++) posts.Add(new Vector3(x0, 0f, Mathf.Lerp(z1, z0, i / 6f)));
            Transform ring = Group(t, "Lobby_Stanchions", Vector3.zero, 0f);
            for (int i = 0; i < posts.Count; i++)
            {
                Stanchion(ring, $"Post_{i}", posts[i]);
                Vector3 a = posts[i] + Vector3.up * 0.9f, b = posts[(i + 1) % posts.Count] + Vector3.up * 0.9f;
                Rope(ring, $"Rope_{i}", a, b);
            }
            Transform pl = Group(t, "Lobby_RexPlaque", new Vector3(-31.0f, 0f, -13.5f), 180f);   // local +Z faces the entrance
            Box(pl, "Post", new Vector3(0f, 0.45f, 0.02f), new Vector3(0.14f, 0.9f, 0.14f), mDarkMetal);
            Box(pl, "Foot", new Vector3(0f, 0.015f, 0.02f), new Vector3(0.45f, 0.03f, 0.35f), mDarkMetal);
            Box(pl, "Plate", new Vector3(0f, 0.98f, 0f), new Vector3(0.82f, 0.52f, 0.03f), mBrass, false, new Vector3(-58f, 0f, 0f));
            Quaternion face = Quaternion.Euler(-58f, 0f, 0f) * Quaternion.Euler(0f, 180f, 0f);
            Vector3 n = Quaternion.Euler(-58f, 0f, 0f) * Vector3.forward;
            Ink(pl, "Title", "TYRANNOSAURUS REX", new Vector3(0f, 0.98f, 0f) + n * 0.0165f + (face * Vector3.up) * 0.17f, face, new Vector2(0.74f, 0.1f), InkBlack, "BarlowCondensed-Bold", 1.0f, TMPro.TextAlignmentOptions.Center);
            Ink(pl, "Body", "Late Cretaceous, 68-66 million years ago.\nLength 12.3 m  -  Weight about 8 tonnes.\nCast from the Isla Tormenta specimen, excavated 1991.\nPlease do not touch the fossil cast.", new Vector3(0f, 0.98f, 0f) + n * 0.0165f + (face * Vector3.up) * -0.05f, face, new Vector2(0.74f, 0.3f), InkBlack, "Barlow-Regular", 0.45f, TMPro.TextAlignmentOptions.Center);
            // a brass-edged granite inlay band round the exhibit and two brass lines on the entrance axis
            Material inlay = p.DarkGranitePlinth;
            Box(t, "Lobby_InlayS", new Vector3(-31.0f, 0.003f, -13.05f), new Vector3(8.1f, 0.006f, 0.3f), inlay);
            Box(t, "Lobby_InlayN", new Vector3(-31.0f, 0.003f, 3.05f), new Vector3(8.1f, 0.006f, 0.3f), inlay);
            Box(t, "Lobby_InlayW", new Vector3(-34.9f, 0.003f, -5.0f), new Vector3(0.3f, 0.006f, 16.4f), inlay);
            Box(t, "Lobby_InlayE", new Vector3(-27.1f, 0.003f, -5.0f), new Vector3(0.3f, 0.006f, 16.4f), inlay);
            for (int s = -1; s <= 1; s += 2)
                Box(t, $"Lobby_InlayLine_{(s < 0 ? "W" : "E")}", new Vector3(-31.0f + s * 1.3f, 0.003f, -22.6f), new Vector3(0.12f, 0.006f, 18.6f), inlay);
            Box(t, "Lobby_EntranceMat", new Vector3(-32.0f, 0.006f, -29.9f), new Vector3(5.0f, 0.012f, 2.4f), mRubberMat);
            // donor wall left of the corridor link and a big park map right of it, under the mezzanine
            const float NW = 18.855f;
            Transform dw = Group(t, "Lobby_DonorWall", new Vector3(-40.6f, 0f, NW), 180f);   // local +Z into the hall (south)
            Box(dw, "Panel", new Vector3(0f, 1.75f, 0.03f), new Vector3(8.6f, 2.3f, 0.06f), mFeatureGreen);
            Box(dw, "HeaderBand", new Vector3(0f, 2.72f, 0.065f), new Vector3(8.2f, 0.26f, 0.01f), mBrass);
            Ink(dw, "Header", "FOUNDING FRIENDS OF CRETACEA PARK  -  1991", new Vector3(0f, 2.72f, 0.0712f), Quaternion.Euler(0f, 180f, 0f), new Vector2(7.8f, 0.2f), InkBlack, "BarlowCondensed-Bold", 2.0f, TMPro.TextAlignmentOptions.Center);
            string[] donors = {
                "The Alvarez Family", "Dr. Helen Marsh", "Okonkwo Holdings", "R. & S. Lindqvist", "Tormenta Port Authority", "The Brightwater Trust",
                "Dr. Arturo Reyes", "Mei & Daniel Chen", "Halvorsen Shipping", "The Duval Foundation", "Island Rotary Club", "P. Nakamura",
                "Vance Engineering", "The Ferreira Family", "Coastline Airways", "Margaret O'Hare", "Dr. K. Mensah", "Isla Fruit Co.",
                "The Kowalski Family", "Southern Cross Bank", "J. & A. Moreau", "Dr. Ingrid Solberg", "The Park Volunteers", "Anonymous" };
            for (int i = 0; i < donors.Length; i++)
            {
                int row = i / 6, col = i % 6;
                Vector3 c = new Vector3(-3.45f + col * 1.38f, 2.25f - row * 0.36f, 0.064f);
                Box(dw, $"NamePlate_{i}", c, new Vector3(1.24f, 0.24f, 0.008f), mBrass);
                Ink(dw, $"Name_{i}", donors[i], c + new Vector3(0f, 0f, 0.0052f), Quaternion.Euler(0f, 180f, 0f), new Vector2(1.14f, 0.16f), InkBlack, "Barlow-SemiBold", 0.9f, TMPro.TextAlignmentOptions.Center);
            }
            Poster(t, p, "Lobby_MapMural", "map_park_modern", new Vector3(-23.4f, 1.85f, NW), Vector3.back, new Vector2(5.0f, 2.98f));
            // banners above the mezzanine, on the north wall the player faces
            Poster(t, p, "Lobby_BannerN_Rex", "rex_banner", new Vector3(-41.0f, 8.0f, 18.86f), Vector3.back, new Vector2(2.4f, 4.8f), frame: false);
            Poster(t, p, "Lobby_BannerN_Raptor", "raptor_poster", new Vector3(-21.0f, 7.8f, 18.86f), Vector3.back, new Vector2(2.6f, 3.7f), frame: false);
            // two exhibit cases north of the skeleton and an information desk off the walking line
            DisplayCase(t, p, "Lobby_Case_Claw", new Vector3(-38.6f, 0f, 5.2f), 180f + 20f, "VELOCIRAPTOR SICKLE CLAW", "Cast. Talon Ridge quarry, 1989.", 0);
            DisplayCase(t, p, "Lobby_Case_Horn", new Vector3(-23.4f, 0f, 5.2f), 180f - 20f, "TRICERATOPS BROW HORN", "Horn core, 1.1 m. Lent by the Duval Foundation.", 1);
            Transform info = Group(t, "Lobby_InfoDesk", new Vector3(-23.4f, 0f, 12.6f), 180f);
            BoxCollider ibc = info.gameObject.AddComponent<BoxCollider>();
            ibc.center = new Vector3(0f, 0.53f, 0f); ibc.size = new Vector3(1.5f, 1.06f, 1.5f);
            Prim(info, "Drum", PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 0.5f, 1.4f), mFeatureGreen);
            Prim(info, "Top", PrimitiveType.Cylinder, new Vector3(0f, 1.025f, 0f), new Vector3(1.52f, 0.025f, 1.52f), mLaminate);
            Prim(info, "Kick", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(1.34f, 0.04f, 1.34f), mBlack);
            Box(info, "Pole", new Vector3(0f, 1.75f, 0f), new Vector3(0.06f, 1.45f, 0.06f), mSteel);
            RoomPlate(info, "InfoSign", info.TransformPoint(new Vector3(0f, 2.35f, 0.05f)), info.forward, "i  INFORMATION", new Vector2(1.1f, 0.3f), mFeatureGreen);
            for (int b = 0; b < 4; b++) Box(info, $"Leaflets_{b}", new Vector3(-0.3f + b * 0.2f, 1.06f, -0.45f), new Vector3(0.15f, 0.02f, 0.21f), b % 2 == 0 ? mPaper : mBinderBlue, false, new Vector3(0f, b * 9f - 12f, 0f));
        }

        private static void DressFeedPrepR4(Transform t, WorldMaterialPalette p)
        {
            const float SF = 3.18f;   // north face of the tiled wall between door_cold and door_vet
            // a second double sink on the south wall, between the two doors, with its splashback and soap
            Transform sink = Counter(t, "Prep_SinkS", new Vector3(-2.95f, 0f, SF + 0.36f), 180f, 3.0f, 0.7f, 0.92f, mSteel, mSteel);
            for (int b = 0; b < 2; b++)
            {
                Box(sink, $"Basin_{b}", new Vector3(-0.75f + b * 1.5f, 0.905f, 0f), new Vector3(0.8f, 0.03f, 0.5f), mBlack);
                Box(sink, $"Tap_{b}", new Vector3(-0.75f + b * 1.5f, 1.08f, 0.28f), new Vector3(0.03f, 0.32f, 0.03f), mSteel);
                Box(sink, $"Spout_{b}", new Vector3(-0.75f + b * 1.5f, 1.22f, 0.18f), new Vector3(0.025f, 0.025f, 0.2f), mSteel);
            }
            Box(t, "Prep_KnifeRail", new Vector3(-2.95f, 1.72f, SF + 0.02f), new Vector3(0.6f, 0.05f, 0.02f), mBlack);
            for (int k = 0; k < 5; k++) Box(t, $"Prep_Knife_{k}", new Vector3(-3.17f + k * 0.11f, 1.62f, SF + 0.035f), new Vector3(0.03f, 0.22f - (k % 2) * 0.05f, 0.004f), mSteel);
            TypedNotice(t, "Prep_HandWash", new Vector3(-1.9f, 1.75f, SF + 0.025f), Vector3.forward, new Vector2(0.30f, 0.40f), "WASH HANDS", "30 seconds, hot\nwater, soap.\nThen glove up.\nRaw meat side\nis the LEFT sink.", InkGreen, 1f);
            // a meat rail with hooks and two hanging carcass quarters, right of the sinks
            Box(t, "Prep_MeatRail", new Vector3(0.2f, 2.3f, SF + 0.42f), new Vector3(1.4f, 0.06f, 0.06f), mSteel);
            for (int s = 0; s < 2; s++) Box(t, $"Prep_RailArm_{s}", new Vector3(-0.45f + s * 1.3f, 2.3f, SF + 0.21f), new Vector3(0.05f, 0.05f, 0.42f), mSteel);
            for (int h = 0; h < 4; h++)
            {
                float x = -0.4f + h * 0.4f;
                Box(t, $"Prep_Hook_{h}", new Vector3(x, 2.17f, SF + 0.42f), new Vector3(0.012f, 0.2f, 0.012f), mSteel);
                Box(t, $"Prep_HookTip_{h}", new Vector3(x, 2.075f, SF + 0.45f), new Vector3(0.012f, 0.012f, 0.06f), mSteel);
            }
            p.InstantiatePropOrFallback("hanging_meat", t, new Vector3(-0.4f, 1.62f, SF + 0.45f), new Vector3(0f, 15f, 0f), new Vector3(0.38f, 0.8f, 0.3f), grounded: false, addCollider: false);
            p.InstantiatePropOrFallback("hanging_meat", t, new Vector3(0.4f, 1.68f, SF + 0.45f), new Vector3(0f, -25f, 0f), new Vector3(0.34f, 0.7f, 0.28f), grounded: false, addCollider: false);
            DecalAt(t, "Prep_MeatDrip", p.DecalBloodSmear, new Vector3(0.0f, 0.02f, SF + 0.5f), 1.0f, 20f, 0.45f);
            // the cold store reads as a freezer: a blue plate over door_cold and a temperature display beside it
            RoomPlate(t, "Prep_ColdStoreSign", new Vector3(-6.8f, 2.55f, SF + 0.01f), Vector3.forward, "COLD STORE   -18 C", new Vector2(1.3f, 0.24f), mBinderBlue);
            Transform th = Group(t, "Prep_ColdStoreThermo", new Vector3(-5.55f, 1.6f, SF), YawFacing(Vector3.forward) + 180f);
            Box(th, "Body", new Vector3(0f, 0f, 0.03f), new Vector3(0.2f, 0.14f, 0.06f), mCabinet);
            Box(th, "Screen", new Vector3(0f, 0.015f, 0.0605f), new Vector3(0.15f, 0.07f, 0.002f), mBlack);
            Ink(th, "Digits", "-18.4", new Vector3(0f, 0.015f, 0.0625f), Quaternion.Euler(0f, 180f, 0f), new Vector2(0.14f, 0.06f), InkRed, "CourierPrime-Bold", 0.6f, TMPro.TextAlignmentOptions.Center);
        }

        private static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, bool collider = false, Vector3? euler = null)
            => WorldArchitectureKit.CreateBox(parent, name, localPos, size, mat, collider, euler);

        /// <summary>Empty group placed in world space with a yaw; children use local coordinates (x right, z forward).</summary>
        private static Transform Group(Transform parent, string name, Vector3 worldPos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0f, yaw, 0f));
            go.layer = GameLayers.Environment;
            return go.transform;
        }

        /// <summary>Yaw (deg) that turns a Unity Quad (visible from local -Z) so its picture faces the world direction n.</summary>
        private static float YawFacing(Vector3 n) => Quaternion.LookRotation(-n).eulerAngles.y;

        /// <summary>Legged table: top slab, apron, four legs. Origin = floor centre. Collider on the top only.</summary>
        private static Transform Table(Transform parent, string name, Vector3 floorPos, float yaw, float w, float d, float h, Material top, Material legs, bool modesty = false)
        {
            Transform t = Group(parent, name, floorPos, yaw);
            Box(t, "Top", new Vector3(0f, h - 0.02f, 0f), new Vector3(w, 0.04f, d), top, true);
            Box(t, "ApronF", new Vector3(0f, h - 0.08f, -d * 0.5f + 0.06f), new Vector3(w - 0.12f, 0.08f, 0.025f), legs);
            Box(t, "ApronB", new Vector3(0f, h - 0.08f, d * 0.5f - 0.06f), new Vector3(w - 0.12f, 0.08f, 0.025f), legs);
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0 ? -1f : 1f) * (w * 0.5f - 0.06f);
                float sz = (i < 2 ? -1f : 1f) * (d * 0.5f - 0.06f);
                Box(t, $"Leg_{i}", new Vector3(sx, (h - 0.04f) * 0.5f, sz), new Vector3(0.045f, h - 0.04f, 0.045f), legs);
            }
            if (modesty) Box(t, "ModestyPanel", new Vector3(0f, h * 0.55f, d * 0.5f - 0.08f), new Vector3(w - 0.14f, h * 0.55f, 0.02f), legs);
            return t;
        }

        /// <summary>Office desk: top, side panels, modesty panel, drawer pedestal on the right with drawer seams. Front = local -Z.</summary>
        private static Transform Desk(Transform parent, string name, Vector3 floorPos, float yaw, float w, float d, Material top, Material body, float h = 0.76f)
        {
            Transform t = Group(parent, name, floorPos, yaw);
            Box(t, "Top", new Vector3(0f, h - 0.02f, 0f), new Vector3(w, 0.04f, d), top, true);
            Box(t, "SideL", new Vector3(-w * 0.5f + 0.02f, (h - 0.04f) * 0.5f, 0f), new Vector3(0.03f, h - 0.04f, d - 0.04f), body, true);
            Box(t, "SideR", new Vector3(w * 0.5f - 0.02f, (h - 0.04f) * 0.5f, 0f), new Vector3(0.03f, h - 0.04f, d - 0.04f), body, true);
            Box(t, "Modesty", new Vector3(0f, h * 0.58f, d * 0.5f - 0.06f), new Vector3(w - 0.06f, h * 0.5f, 0.02f), body);
            float px = w * 0.5f - 0.24f;
            Box(t, "Pedestal", new Vector3(px, (h - 0.06f) * 0.5f, -0.02f), new Vector3(0.42f, h - 0.06f, d - 0.1f), body);
            for (int i = 0; i < 3; i++)
            {
                float y = 0.14f + i * 0.22f;
                Box(t, $"DrawerSeam_{i}", new Vector3(px, y + 0.1f, -d * 0.5f + 0.045f), new Vector3(0.40f, 0.008f, 0.005f), mBlack);
                Box(t, $"DrawerPull_{i}", new Vector3(px, y + 0.03f, -d * 0.5f + 0.04f), new Vector3(0.12f, 0.015f, 0.015f), mBlack);
            }
            return t;
        }

        /// <summary>Base-cabinet counter: recessed black plinth, painted carcass with door seams, overhanging top. Front = local -Z.</summary>
        private static Transform Counter(Transform parent, string name, Vector3 floorPos, float yaw, float w, float d, float h, Material top, Material body, bool doorsFront = true, bool doorsBack = false)
        {
            Transform t = Group(parent, name, floorPos, yaw);
            Box(t, "Plinth", new Vector3(0f, 0.05f, 0f), new Vector3(w - 0.06f, 0.10f, d - 0.12f), mBlack, true);
            Box(t, "Carcass", new Vector3(0f, 0.10f + (h - 0.14f) * 0.5f, 0f), new Vector3(w, h - 0.14f, d - 0.04f), body, true);
            Box(t, "Top", new Vector3(0f, h - 0.02f, 0f), new Vector3(w + 0.04f, 0.04f, d + 0.03f), top, true);
            int doors = Mathf.Max(1, Mathf.RoundToInt(w / 0.6f));
            for (int side = 0; side < 2; side++)
            {
                if ((side == 0 && !doorsFront) || (side == 1 && !doorsBack)) continue;
                float fz = side == 0 ? -(d - 0.04f) * 0.5f - 0.003f : (d - 0.04f) * 0.5f + 0.003f;
                for (int i = 1; i < doors; i++)
                {
                    float x = -w * 0.5f + i * (w / doors);
                    Box(t, $"Seam_{side}_{i}", new Vector3(x, 0.10f + (h - 0.14f) * 0.5f, fz), new Vector3(0.006f, h - 0.2f, 0.004f), mBlack);
                }
                Box(t, $"TopSeam_{side}", new Vector3(0f, h - 0.17f, fz), new Vector3(w - 0.02f, 0.006f, 0.004f), mBlack);
                for (int i = 0; i < doors; i++)
                {
                    float x = -w * 0.5f + (i + 0.5f) * (w / doors) + (i % 2 == 0 ? 0.2f : -0.2f) * (w / doors) * 0.6f;
                    Box(t, $"Pull_{side}_{i}", new Vector3(x, h - 0.26f, fz + (side == 0 ? -0.01f : 0.01f)), new Vector3(0.012f, 0.10f, 0.015f), mSteel);
                }
            }
            return t;
        }

        /// <summary>Framed picture (poster, sign, map) on a wall. pos = point on the wall face, facing = wall normal (into the room).</summary>
        private static GameObject Poster(Transform parent, WorldMaterialPalette p, string name, string stem, Vector3 pos, Vector3 facing, Vector2 size, bool frame = true, bool emissive = false, float emissiveIntensity = 1f)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, YawFacing(facing), 0f));
            root.layer = GameLayers.Environment;

            Material mat = p.CreateTextureQuadMaterial(emissive ? $"MAT_Z1_Glow_{stem}" : $"MAT_Z1_Print_{stem}", stem, emissive, emissiveIntensity, Color.white);
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Picture";
            quad.transform.SetParent(root.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, frame ? -0.018f : -0.004f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
            quad.layer = GameLayers.Environment;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            if (frame)
            {
                Box(root.transform, "Frame", new Vector3(0f, 0f, -0.008f), new Vector3(size.x + 0.05f, size.y + 0.05f, 0.016f), mBlack);
            }
            return root;
        }

        /// <summary>
        /// Interior lining on one face of a wall whose material is meant for the other face (e.g. raw boardform concrete
        /// outside, painted plaster inside). Axis-aligned walls only. holes = (minAlong, maxAlong, bottomY, topY) in metres
        /// from start, matching the wall's openings, so frames and glass stay visible. mat = null: wainscot band only.
        /// </summary>
        private static void Liner(Transform parent, string name, Vector3 start, Vector3 end, Vector3 inward, float wallHalfT, float height, Material mat,
            (float a0, float a1, float y0, float y1)[] holes = null, bool skirting = true, float thickness = 0.02f, Material wainscot = null)
        {
            Vector3 dir = end - start; dir.y = 0f;
            float len = dir.magnitude;
            if (len < 0.05f) return;
            dir /= len;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(start.x, 0f, start.z), Quaternion.LookRotation(dir, Vector3.up));
            root.layer = GameLayers.Environment;
            // local +x = right of the wall direction; which side is "inward":
            float side = Mathf.Sign(Vector3.Dot(Vector3.Cross(Vector3.up, dir), inward));
            if (mat == null) thickness = 0f;
            float off = side * (wallHalfT + 0.003f + thickness * 0.5f);

            var list = new List<(float a0, float a1, float y0, float y1)>();
            if (holes != null) list.AddRange(holes);
            list.Sort((a, b) => a.a0.CompareTo(b.a0));
            float cursor = 0f;
            int n = 0;
            void Span(float a0, float a1, float y0, float y1, bool floorTrim)
            {
                if (a1 - a0 < 0.02f || y1 - y0 < 0.02f) return;
                float mid = (a0 + a1) * 0.5f;
                if (mat != null) Box(root.transform, $"Lining_{n}", new Vector3(off, (y0 + y1) * 0.5f, mid), new Vector3(thickness, y1 - y0, a1 - a0), mat, false);
                if (floorTrim && y0 < 0.01f)
                {
                    if (wainscot != null)
                    {
                        Box(root.transform, $"Wainscot_{n}", new Vector3(off + side * (thickness * 0.5f + 0.004f), 0.5f, mid), new Vector3(0.008f, 1.0f, a1 - a0), wainscot, false);
                        if (mat != null) Box(root.transform, $"DadoCap_{n}", new Vector3(off + side * (thickness * 0.5f + 0.012f), 1.0f, mid), new Vector3(0.024f, 0.05f, a1 - a0), mSteel, false);
                    }
                    if (skirting) Box(root.transform, $"Skirt_{n}", new Vector3(off + side * (thickness * 0.5f + 0.01f), 0.06f, mid), new Vector3(0.02f, 0.12f, a1 - a0), mBlack, false);
                }
                n++;
            }
            foreach (var h in list)
            {
                float a0 = Mathf.Clamp(h.a0, 0f, len), a1 = Mathf.Clamp(h.a1, 0f, len);
                Span(cursor, a0, 0f, height, true);
                Span(a0, a1, 0f, h.y0, true);
                Span(a0, a1, h.y1, height, false);
                cursor = Mathf.Max(cursor, a1);
            }
            Span(cursor, len, 0f, height, true);
        }

        /// <summary>Lower a room's ceiling slab by ~1 cm so it tucks under the abutting wall tops (no slot, no z-fight).</summary>
        private static void TuckCeiling(GameObject roomShell, float drop = 0.012f)
        {
            if (roomShell == null) return;
            Transform t = roomShell.transform.Find("Ceiling");
            if (t != null) t.localPosition += new Vector3(0f, -drop, 0f);
        }

        /// <summary>Ceiling fixture: a downward wide spot, calibrated by the lamp table. No bare point under the ceiling
        /// (Look Bible 4: the hot spot on the tiles fools the auto exposure).</summary>
        private static Light CeilingLamp(Transform parent, string name, Vector3 pos, LightGroupId group, float kelvin, Renderer[] emissives = null, bool shadows = true)
        {
            return WorldArchitectureKit.BuildHdrpLight(parent, name, pos, new Vector3(90f, 0f, 0f), LightType.Spot, Color.white, 1000f, 12f, group, volumetricDimmer: 0.35f, castShadows: shadows, spotOuterAngle: 150f, colorTempK: kelvin, pairedEmissives: emissives);
        }

        /// <summary>Soft ceiling wash standing in for the floor bounce: an upward 160 deg spot 1.2 m off the floor, ~20 lux on
        /// the tiles with no hot spot, unshadowed. Calibrated by the "_Wash" rule. One or two per room, same group as the room.</summary>
        private static void CeilingWash(Transform parent, string name, Vector3 floorPos, LightGroupId group, float kelvin)
        {
            WorldArchitectureKit.BuildHdrpLight(parent, name, new Vector3(floorPos.x, 1.2f, floorPos.z), new Vector3(-90f, 0f, 0f), LightType.Spot, Color.white, 60f, 6f, group, volumetricDimmer: 0f, castShadows: false, spotOuterAngle: 160f, colorTempK: kelvin);
        }

        /// <summary>Warm battery emergency unit (VCEmergency): a low pool of 3000-3500 K light. Calibrated by the "_Batt" rule.</summary>
        private static void BatteryLight(Transform parent, string room, int index, Vector3 pos)
        {
            string n = index < 0 ? $"Light_{room}_Batt" : $"Light_{room}_Batt_{index}";
            WorldArchitectureKit.BuildHdrpLight(parent, n, pos, new Vector3(90f, 0f, 0f), LightType.Spot, new Color(1f, 0.86f, 0.68f), 340f, 10f, LightGroupId.VCEmergency, volumetricDimmer: 0.6f, castShadows: true, spotOuterAngle: 150f, colorTempK: 3200f);
        }

        /// <summary>Cork notice board with pinned sheets. pos = centre on the wall face, facing = wall normal.</summary>
        private static void CorkBoard(Transform parent, string name, Vector3 pos, Vector3 facing, float w, float h, int seed)
        {
            Transform t = Group(parent, name, pos, YawFacing(facing) + 180f);   // local +Z = into the room
            Box(t, "Frame", new Vector3(0f, 0f, 0.012f), new Vector3(w, h, 0.024f), mVeneer);
            Box(t, "Cork", new Vector3(0f, 0f, 0.016f), new Vector3(w - 0.08f, h - 0.08f, 0.02f), mCork);
            var rng = new System.Random(seed);
            int n = Mathf.RoundToInt(w * 4f);
            for (int i = 0; i < n; i++)
            {
                float x = -w * 0.5f + 0.18f + (float)rng.NextDouble() * (w - 0.36f);
                float y = -h * 0.5f + 0.2f + (float)rng.NextDouble() * (h - 0.4f);
                float roll = (float)rng.NextDouble() * 10f - 5f;
                Box(t, $"Sheet_{i}", new Vector3(x, y, 0.0275f + i * 0.0004f), new Vector3(0.21f, 0.28f, 0.002f), rng.NextDouble() < 0.2 ? mPaintYellow : mPaper, false, new Vector3(0f, 0f, roll));
                if (rng.NextDouble() < 0.65 && s_palette != null)   // printed notices, memos and rosters (review 1: blank notes)
                    PrintQuad(t, s_palette, $"Print_{i}", NoticeArt[rng.Next(NoticeArt.Length)], new Vector3(x, y, 0.0292f + i * 0.0004f), new Vector2(0.19f, 0.25f), true, roll);
                Prim(t, $"Pin_{i}", PrimitiveType.Sphere, new Vector3(x, y + 0.11f, 0.033f + i * 0.0004f), new Vector3(0.012f, 0.012f, 0.012f), i % 2 == 0 ? mBinderRed : mBinderBlue);
            }
        }

        /// <summary>Wall-mounted fire extinguisher on a bracket, 0.45 m off the floor. pos = floor point on the wall face.</summary>
        private static void WallExtinguisher(Transform parent, WorldMaterialPalette p, Vector3 wallFloorPos, Vector3 facing)
        {
            Vector3 n = facing.normalized;
            Box(parent, $"ExtinguisherBracket_{wallFloorPos.x:F0}_{wallFloorPos.z:F0}", wallFloorPos + n * 0.01f + Vector3.up * 0.75f, new Vector3(Mathf.Abs(n.z) > 0.5f ? 0.14f : 0.02f, 0.5f, Mathf.Abs(n.z) > 0.5f ? 0.02f : 0.14f), mBlack);
            p.InstantiatePropOrFallback("fire_extinguisher", parent, wallFloorPos + n * 0.15f + Vector3.up * 0.45f, new Vector3(0f, YawFacing(-n), 0f), new Vector3(0.22f, 0.65f, 0.22f), grounded: true, addCollider: false);
        }

        /// <summary>Slatted bench: three wooden slats on two steel frames. yaw: length along local x.</summary>
        private static void Bench(Transform parent, string name, Vector3 floorPos, float yaw, float length)
        {
            Transform t = Group(parent, name, floorPos, yaw);
            BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.23f, 0f); bc.size = new Vector3(length, 0.46f, 0.4f);
            for (int i = 0; i < 3; i++)
                Box(t, $"Slat_{i}", new Vector3(0f, 0.44f, -0.13f + i * 0.13f), new Vector3(length, 0.03f, 0.11f), mVeneer);
            for (int k = 0; k < 2; k++)
            {
                float x = (k == 0 ? -1f : 1f) * (length * 0.5f - 0.2f);
                Box(t, $"FrameTop_{k}", new Vector3(x, 0.41f, 0f), new Vector3(0.04f, 0.03f, 0.38f), mDarkMetal);
                Box(t, $"LegF_{k}", new Vector3(x, 0.2f, -0.16f), new Vector3(0.04f, 0.4f, 0.04f), mDarkMetal);
                Box(t, $"LegB_{k}", new Vector3(x, 0.2f, 0.16f), new Vector3(0.04f, 0.4f, 0.04f), mDarkMetal);
            }
        }

        /// <summary>Floor decal (projected straight down).</summary>
        private static void DecalAt(Transform parent, string name, Material mat, Vector3 pos, float size, float yaw = 0f, float fade = 0.8f)
        {
            WorldArchitectureKit.BuildDecal(parent, name, pos, new Vector3(90f, yaw, 0f), new Vector3(size, size, 0.5f), mat, fade);
        }

        /// <summary>Wall decal projected into a wall. pos on the wall face, facing = wall normal.</summary>
        private static void WallDecal(Transform parent, string name, Material mat, Vector3 pos, Vector3 facing, Vector2 size, float fade = 0.75f)
        {
            WorldArchitectureKit.BuildDecal(parent, name, pos + facing.normalized * 0.1f, Quaternion.LookRotation(-facing.normalized).eulerAngles, new Vector3(size.x, size.y, 0.4f), mat, fade);
        }

        /// <summary>Small stack of paper / folders lying on a surface.</summary>
        private static void PaperStack(Transform parent, string name, Vector3 pos, float yaw, int sheets = 3)
        {
            Transform t = Group(parent, name, pos, yaw);
            for (int i = 0; i < sheets; i++)
            {
                Box(t, $"Sheet_{i}", new Vector3(i * 0.006f, 0.004f + i * 0.008f, -i * 0.004f), new Vector3(0.22f, 0.008f, 0.30f), i == sheets - 1 ? mPaper : (i % 2 == 0 ? mBinderBlue : mPaper), false, new Vector3(0f, i * 4f, 0f));
            }
        }

        /// <summary>Row of upright binders on a shelf or desk; pos = left end on the surface, along +x of yaw.</summary>
        private static void BinderRow(Transform parent, string name, Vector3 pos, float yaw, int count, int seed)
        {
            Transform t = Group(parent, name, pos, yaw);
            var rng = new System.Random(seed);
            float x = 0f;
            Material[] mats = { mBinderBlue, mBinderRed, mBinderBlack, mBinderBlue, mPaper };
            for (int i = 0; i < count; i++)
            {
                float w = 0.05f + (float)rng.NextDouble() * 0.03f;
                float h = 0.29f + (float)rng.NextDouble() * 0.03f;
                Box(t, $"Binder_{i}", new Vector3(x + w * 0.5f, h * 0.5f, 0f), new Vector3(w, h, 0.26f), mats[rng.Next(mats.Length)]);
                x += w + 0.004f;
            }
        }

        // =========================================================================
        // REUSABLE INTERACTABLE & DOCUMENT HELPERS
        // =========================================================================

        public static SimpleInteractable BuildSimpleInteractableBox(
            Transform parent,
            string name,
            Vector3 worldPos,
            Vector3 boxSize,
            string interactId,
            string promptText,
            float holdSec = 0f,
            bool preserveProgress = false,
            bool singleUse = true,
            bool startsEnabled = true,
            bool startsLocked = false,
            string lockedHint = "No power.")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.layer = GameLayers.Interactable;

            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = boxSize;

            SimpleInteractable si = go.AddComponent<SimpleInteractable>();
            var so = new SerializedObject(si);
            so.FindProperty("interactId").stringValue = interactId;
            so.FindProperty("promptText").stringValue = promptText;
            so.FindProperty("lockedHint").stringValue = lockedHint;
            so.FindProperty("holdDuration").floatValue = holdSec;
            so.FindProperty("preserveHoldProgress").boolValue = preserveProgress;
            so.FindProperty("oneShot").boolValue = singleUse;
            so.FindProperty("startsEnabled").boolValue = startsEnabled;
            so.FindProperty("startsLocked").boolValue = startsLocked;
            so.ApplyModifiedPropertiesWithoutUndo();
            return si;
        }

        public static DocumentPickup BuildDocumentProp(
            Transform parent,
            Vector3 worldPos,
            string docId,
            string displayTitle,
            string promptText,
            string[] pageKeys,
            string[] pageSummaries,
            WorldMaterialPalette p,
            string assetStem = null,
            bool isAudioLog = false,
            bool oneShot = false)
        {
            GameObject go;
            if (!string.IsNullOrEmpty(assetStem))
            {
                go = p.InstantiatePropOrFallback(assetStem, parent, worldPos, new Vector3(0f, 24f, 0f), new Vector3(0.36f, 0.08f, 0.28f), grounded: true, addCollider: false, fallbackMat: p.PlasterClean, customName: $"Doc_{docId}");
            }
            else
            {
                go = WorldArchitectureKit.CreateBox(parent, $"Doc_{docId}", worldPos, new Vector3(0.24f, 0.012f, 0.32f), p.PlasterClean, addCollider: false, euler: new Vector3(0f, -14f, 0f));
            }
            go.layer = GameLayers.Interactable;

            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(0.55f, 0.35f, 0.55f);

            DocumentPickup dp = go.AddComponent<DocumentPickup>();
            var so = new SerializedObject(dp);
            so.FindProperty("documentId").stringValue = docId;
            so.FindProperty("displayTitle").stringValue = displayTitle;
            so.FindProperty("promptText").stringValue = promptText;
            so.FindProperty("isAudioLog").boolValue = isAudioLog;
            so.FindProperty("oneShot").boolValue = oneShot;

            SerializedProperty keysProp = so.FindProperty("pageAssetKeys");
            keysProp.arraySize = pageKeys != null ? pageKeys.Length : 0;
            for (int i = 0; i < keysProp.arraySize; i++) keysProp.GetArrayElementAtIndex(i).stringValue = pageKeys[i];

            SerializedProperty sumsProp = so.FindProperty("pageSummaries");
            sumsProp.arraySize = pageSummaries != null ? pageSummaries.Length : 0;
            for (int i = 0; i < sumsProp.arraySize; i++) sumsProp.GetArrayElementAtIndex(i).stringValue = pageSummaries[i];

            so.ApplyModifiedPropertiesWithoutUndo();
            return dp;
        }

        private static void AttachNamedWorldAnchor(GameObject target, string anchorId)
        {
            if (target == null || string.IsNullOrWhiteSpace(anchorId)) return;
            WorldAnchor wa = target.GetComponent<WorldAnchor>() ?? target.AddComponent<WorldAnchor>();
            var so = new SerializedObject(wa);
            so.FindProperty("anchorId").stringValue = anchorId;
            so.FindProperty("registerAsNamedObject").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The palette sizes a prop's BoxCollider from its WORLD bounds but applies it in LOCAL axes, so a prop turned 90 deg
        /// gets a box turned 90 deg (invisible wall on one axis, walk-through ends on the other). Refit every collider the
        /// palette added to a prop instance from the meshes' bounds in the prop's own space. Props at 0/180 deg come out the
        /// same; lamp posts get a capsule round the pole instead of a box round pole + arm.
        /// </summary>
        private static void RefitPropBoxColliders(Transform root)
        {
            foreach (BoxCollider bc in root.GetComponentsInChildren<BoxCollider>(true))
            {
                GameObject go = bc.gameObject;
                if (bc.isTrigger || !PrefabUtility.IsAnyPrefabInstanceRoot(go) || !PrefabUtility.IsAddedComponentOverride(bc)) continue;
                Matrix4x4 toLocal = go.transform.worldToLocalMatrix;
                bool has = false;
                Bounds lb = default;
                foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null) continue;
                    Bounds mb = mf.sharedMesh.bounds;
                    Matrix4x4 m = toLocal * mf.transform.localToWorldMatrix;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                        Vector3 q = m.MultiplyPoint3x4(c);
                        if (!has) { lb = new Bounds(q, Vector3.zero); has = true; } else lb.Encapsulate(q);
                    }
                }
                if (!has) continue;
                if (go.name == "lamp_post")
                {
                    // pole only, full height; the arm and lantern are overhead. The FBX's pole stands at local (-0.06, -0.69),
                    // 0.8 m from the pivot (measured from the mesh's vertices); its footing is ~0.4 m across.
                    float s = Mathf.Max(0.001f, Mathf.Max(Mathf.Abs(go.transform.lossyScale.x), Mathf.Abs(go.transform.lossyScale.z)));
                    CapsuleCollider cc = go.AddComponent<CapsuleCollider>();
                    cc.direction = 1;
                    cc.center = new Vector3(-0.06f, lb.center.y, -0.69f);
                    cc.height = lb.size.y;
                    cc.radius = 0.2f / s;
                    Object.DestroyImmediate(bc);
                    continue;
                }
                bc.center = lb.center;
                bc.size = lb.size;
            }
        }

        private static void BindLightGroupToggleObject(LightGroupMember member, GameObject toggleObj)
        {
            if (member == null || toggleObj == null) return;
            var so = new SerializedObject(member);
            SerializedProperty prop = so.FindProperty("toggleObjects");
            if (prop != null)
            {
                prop.arraySize = 1;
                prop.GetArrayElementAtIndex(0).objectReferenceValue = toggleObj;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// Performance: the dressing is thousands of small boxes. Merge every static, single-material primitive (Cube/Quad)
        /// on the Environment layer into one mesh per material, keeping colliders on their original objects. Anything a
        /// component points at (light-group emissives, toggled diffusers, CCTV screens, door leaves, pickups, story-toggled
        /// groups) is left alone, and so is glass (no shadows) and the rain blockers (own layer).
        /// </summary>
        private static void ConsolidateStaticPrimitives(Transform root)
        {
            // 1. everything referenced by a component, or under a component that moves/toggles, is protected
            var protectedGos = new HashSet<GameObject>();
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                bool toggles = mb is LightGroupMember || mb is WorldAnchor || mb is DoorController || mb is DocumentPickup || mb is SimpleInteractable || mb is FlashlightPickup;
                if (toggles) foreach (Transform t in mb.GetComponentsInChildren<Transform>(true)) protectedGos.Add(t.gameObject);
                var so = new SerializedObject(mb);
                SerializedProperty it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    Object o = it.objectReferenceValue;
                    GameObject g = o is GameObject go ? go : (o is Component c ? c.gameObject : null);
                    if (g == null) continue;
                    foreach (Transform t in g.GetComponentsInChildren<Transform>(true)) protectedGos.Add(t.gameObject);
                }
            }
            foreach (Animator a in root.GetComponentsInChildren<Animator>(true)) foreach (Transform t in a.GetComponentsInChildren<Transform>(true)) protectedGos.Add(t.gameObject);

            // 2. gather candidates per material
            // grouped per material AND per 16 m cell: one merged mesh per material spanning the whole zone was drawn in
            // every view and every shadow map that saw any part of it (review 2 fix round: +2 M triangles in office_on)
            var groups = new Dictionary<(Material, int, int), List<MeshRenderer>>();
            foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                string mn = mf.sharedMesh.name;
                // review 2: ~420 new cylinders, spheres and capsules (ceiling detail, plush, pins, cafe, bottles) stayed
                // separate renderers; built-in primitive meshes of any shape are merged per material now
                if (mn != "Cube" && mn != "Quad" && mn != "Cylinder" && mn != "Sphere" && mn != "Capsule") continue;
                GameObject g = mr.gameObject;
                if (g.layer != GameLayers.Environment || protectedGos.Contains(g)) continue;
                // ceiling slabs stay separate: LookRoofs finds flat roofs by their "Ceil" name and Cube mesh (review 1: the
                // merged break-room and gift-shop ceilings were never capped and read as flat cream slabs from above)
                if (g.name.IndexOf("Ceil", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (mr.sharedMaterials.Length != 1 || mr.sharedMaterial == null) continue;
                if (mr.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.On) continue;
                Vector3 bc = mr.bounds.center;
                var key = (mr.sharedMaterial, Mathf.FloorToInt(bc.x / 16f), Mathf.FloorToInt(bc.z / 16f));
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<MeshRenderer>();
                list.Add(mr);
            }

            // 3. build merged meshes (chunks of <= 60k vertices), then strip the originals
            var merged = new GameObject("Merged_StaticDressing");
            merged.transform.SetParent(root, false);
            merged.layer = GameLayers.Environment;
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            int meshCount = 0, removed = 0;
            foreach (var kv in groups)
            {
                var combine = new List<CombineInstance>();
                int verts = 0, chunk = 0;
                void Flush()
                {
                    if (combine.Count == 0) return;
                    var mesh = new Mesh { name = $"Z1_Merged_{kv.Key.Item1.name}_{kv.Key.Item2}_{kv.Key.Item3}_{chunk}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    mesh.CombineMeshes(combine.ToArray(), true, true);
                    mesh.RecalculateBounds();
                    var go = new GameObject(mesh.name);
                    go.transform.SetParent(merged.transform, false);
                    go.layer = GameLayers.Environment;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = kv.Key.Item1;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccludeeStatic);
                    combine.Clear(); verts = 0; chunk++; meshCount++;
                }
                foreach (MeshRenderer mr in kv.Value)
                {
                    Mesh m = mr.GetComponent<MeshFilter>().sharedMesh;
                    if (verts + m.vertexCount > 60000) Flush();
                    combine.Add(new CombineInstance { mesh = m, transform = toRoot * mr.localToWorldMatrix });
                    verts += m.vertexCount;
                }
                Flush();
                foreach (MeshRenderer mr in kv.Value)
                {
                    GameObject g = mr.gameObject;
                    bool keepGo = g.GetComponent<Collider>() != null || g.transform.childCount > 0 || g.GetComponents<Component>().Length > 3;
                    if (keepGo)
                    {
                        Object.DestroyImmediate(mr);
                        Object.DestroyImmediate(g.GetComponent<MeshFilter>());
                    }
                    else Object.DestroyImmediate(g);
                    removed++;
                }
            }
            Debug.Log($"[Zone1] merged {removed} primitives into {meshCount} meshes");
        }

        private static float GetCombinedBoundsMaxY(GameObject obj, float fallbackY)
        {
            if (obj == null) return fallbackY;
            Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return fallbackY;
            Bounds combined = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) combined.Encapsulate(rends[i].bounds);
            return combined.max.y;
        }
    }
}
