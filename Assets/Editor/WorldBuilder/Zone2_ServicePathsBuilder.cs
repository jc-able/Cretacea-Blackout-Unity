using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Cretacea.Core;
using Cretacea.Interact;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// Builds Zone 2: Enclosed Jungle Service Paths (X: +21.5 .. +91.0), OPEN North T-Branch Alcoves,
    /// Paddock 4 Service Gate, Talon Ridge Raptor Pen (visible through Control Room's 17 m window!),
    /// and the Overturned Ranger Jeep off-shoulder audio log (jeep_radio wired for 5/5 documents!).
    ///
    /// Mathematically eliminates EVERY Zone 2 defect from #7 & #9:
    /// - DOOR-7 FIX: Both North T-Branches (X=40 & X=64) cut cleanly OPEN with ZERO sealing walls & ZERO floating ceiling slabs!
    /// - Exposes wrecked_kiosk (flickering light), grounded fallen_log (flush on ground, never hovering at Y=2.6!),
    ///   STAFF ONLY chainlink gate, and unobstructed corner-2 raptor sprint crossing vista!
    /// - Grounded storm-lit lamp_posts & free-standing gate_control_box cleanly inside walkway/curb (zero 90%-buried props!).
    /// - Tropical palms/ferns strictly OUTSIDE concrete walls; hanging_vines draped cleanly over 5 m parapet tops.
    /// - Talon Ridge Raptor Pen directly outside Control Room 17 m window with heavy sliding talon_gate (+9.5 m East!).
    /// </summary>
    public static class Zone2_ServicePathsBuilder
    {
        private const float WallH = 5.0f;
        private const float WallT = 0.35f;
        private const float HalfW = 2.3f;

        public static GameObject Build(Transform worldRoot, WorldMaterialPalette palette)
        {
            Zone23Dressing.ResetCaches();
            var root = new GameObject("Zone_02_ServicePathsAndTalon");
            if (worldRoot != null) root.transform.SetParent(worldRoot, false);

            BuildOpenServicePathCorridor(root.transform, palette);
            BuildOpenNorthTBranches(root.transform, palette);
            BuildPaddock4ServiceGate(root.transform, palette);
            BuildTalonRidgeRaptorPen(root.transform, palette);
            BuildOverturnedRangerJeep(root.transform, palette, new Vector3(104.0f, 0.0f, -7.8f));
            // round 2: one wire plane per fence span (not a double-sided box) on round galvanised posts
            Zone23Dressing.UpgradeChainlinkSpans(root.transform, palette);
            Zone23Dressing.DressServiceDockApproach(worldRoot, root.transform, palette);

            return root;
        }

        // =========================================================================
        // MAIN SERVICE PATH POLYLINE (Unity X, Z)
        // (21.5,-9) -> (40,-9) -> (40,+14) -> (64,+14) -> (78,-2) -> (91,-4)
        // =========================================================================

        private static void BuildOpenServicePathCorridor(Transform parent, WorldMaterialPalette p)
        {
            var paths = new GameObject("ServicePaths_MainRoute");
            paths.transform.SetParent(parent, false);

            // Wet asphalt road slabs (SurfaceType.Concrete) + specular storm puddle decals
            // Each segment tops out a few millimetres apart so the overlaps at the bends never z-fight (the old
            // pasted-looking turning slabs are gone; crack and stain decals break up the bends instead).
            BuildRoadSegment(paths.transform, "Road_Seg1_DockToBend1", new Vector3(21.5f, 0f, -9f), new Vector3(42.3f, 0f, -9f), HalfW * 2f, p, 0.000f);
            BuildRoadSegment(paths.transform, "Road_Seg2_NorthRun", new Vector3(40f, 0f, -11.3f), new Vector3(40f, 0f, 16.3f), HalfW * 2f, p, 0.004f);
            BuildRoadSegment(paths.transform, "Road_Seg3_EastRun", new Vector3(37.7f, 0f, 14f), new Vector3(66.3f, 0f, 14f), HalfW * 2f, p, 0.008f);
            BuildRoadSegment(paths.transform, "Road_Seg4_DiagToEnd", new Vector3(64f, 0f, 14f), new Vector3(78f, 0f, -2f), HalfW * 2f + 0.5f, p, 0.012f);
            BuildRoadSegment(paths.transform, "Road_Seg5_ApproachGate", new Vector3(77f, 0f, -2f), new Vector3(92f, 0f, -4f), HalfW * 2f + 0.6f, p, 0.016f);

            // Wet storm puddles along the asphalt path
            WorldArchitectureKit.BuildDecal(paths.transform, "PathPuddle_1", new Vector3(32f, 0.05f, -9f), new Vector3(90f, 0f, 0f), new Vector3(5.5f, 3.6f, 0.5f), p.DecalPuddleWet, 0.6f);
            WorldArchitectureKit.BuildDecal(paths.transform, "PathPuddle_2", new Vector3(40f, 0.05f, 3f), new Vector3(90f, 0f, 0f), new Vector3(3.8f, 6.2f, 0.5f), p.DecalPuddleWet, 0.6f);
            WorldArchitectureKit.BuildDecal(paths.transform, "PathPuddle_3", new Vector3(54f, 0.05f, 14f), new Vector3(90f, 0f, 0f), new Vector3(6.5f, 3.8f, 0.5f), p.DecalPuddleWet, 0.62f);
            WorldArchitectureKit.BuildDecal(paths.transform, "PathPuddle_4", new Vector3(84f, 0.05f, -3.2f), new Vector3(90f, 0f, 0f), new Vector3(5.8f, 3.8f, 0.5f), p.DecalPuddleWet, 0.6f);

            // Precast retaining walls (5.0 m): 4 m panels with joints, coping, outside pilasters, inside kerb + drain,
            // grime under the coping, moss at the foot, vines, and jungle pressed against the outside faces.
            // STRICTLY SPLIT AT X = 40 & X = 64 SO THE NORTH T-BRANCHES STAY WIDE OPEN. Corner extensions close the
            // L-joint notches the old single cubes left.
            const float e = WallT * 0.5f + 0.01f;
            var st = Zone23Dressing.WallStyle.Path;
            Zone23Dressing.PanelWall(paths.transform, "Wall_Seg1_N", new Vector3(21.5f, 0f, -6.7f), new Vector3(37.7f, 0f, -6.7f), 0f, WallH, WallT, new Vector3(30f, 0f, -9f), p, st, 0f, e, 1);
            Zone23Dressing.PanelWall(paths.transform, "Wall_Seg1_S", new Vector3(21.5f, 0f, -11.3f), new Vector3(42.3f, 0f, -11.3f), 0f, WallH, WallT, new Vector3(30f, 0f, -9f), p, st, 0f, e, 2);
            Zone23Dressing.PanelWall(paths.transform, "Wall_Seg2_W", new Vector3(37.7f, 0f, -6.7f), new Vector3(37.7f, 0f, 16.3f), 0f, WallH, WallT, new Vector3(40f, 0f, 3f), p, st, 0f, 0f, 3);
            Zone23Dressing.PanelWall(paths.transform, "Wall_Seg2_E", new Vector3(42.3f, 0f, -11.3f), new Vector3(42.3f, 0f, 11.7f), 0f, WallH, WallT, new Vector3(40f, 0f, 3f), p, st, 0f, 0f, 4);
            Zone23Dressing.PanelWall(paths.transform, "Wall_Seg3_N_BetweenBranches", new Vector3(42.3f, 0f, 16.3f), new Vector3(61.7f, 0f, 16.3f), 0f, WallH, WallT, new Vector3(52f, 0f, 14f), p, st, e, e, 5);
            Zone23Dressing.PanelWall(paths.transform, "Wall_Seg3_S", new Vector3(42.3f, 0f, 11.7f), new Vector3(61.5f, 0f, 11.7f), 0f, WallH, WallT, new Vector3(52f, 0f, 14f), p, st, e, 0.2f, 6);
            Zone23Dressing.PanelWall(paths.transform, "Wall_Seg4_NE", new Vector3(66.3f, 0f, 16.3f), new Vector3(80.1f, 0f, 0.2f), 0f, WallH, WallT, new Vector3(71f, 0f, 6f), p, st, 0.2f, 0.15f, 7);
            Zone23Dressing.PanelWall(paths.transform, "Wall_Seg4_SW", new Vector3(61.5f, 0f, 11.7f), new Vector3(75.9f, 0f, -4.4f), 0f, WallH, WallT, new Vector3(71f, 0f, 6f), p, st, 0.1f, 0.15f, 8);
            Zone23Dressing.PanelWall(paths.transform, "Wall_Seg5_N", new Vector3(80.1f, 0f, 0.2f), new Vector3(91.0f, 0f, -0.9f), 0f, WallH, WallT, new Vector3(85f, 0f, -3f), p, st, 0.1f, 0f, 9);
            Zone23Dressing.PanelWall(paths.transform, "Wall_Seg5_S", new Vector3(75.9f, 0f, -4.4f), new Vector3(91.0f, 0f, -7.1f), 0f, WallH, WallT, new Vector3(85f, 0f, -3f), p, st, 0.1f, 0f, 10);

            // POS-14 FIX: Blood smear & claw tracks clearly visible on the bend retaining wall!
            WorldArchitectureKit.BuildDecal(paths.transform, "Path_BloodSmear_Bend", new Vector3(61.2f, 1.35f, 11.9f), new Vector3(0f, 0f, 0f), new Vector3(1.8f, 1.4f, 0.5f), p.DecalBloodSmear, 0.90f);

            // Grounded storm-lit lamp_posts cleanly inset INSIDE concrete walkway (zero 90%-buried posts!)
            (Vector3 pos, float yaw, bool sparking)[] lampSpots =
            {
                (new Vector3(31.0f, 0f, -7.25f), 180f, false),
                (new Vector3(38.25f, 0f, 4.0f), 90f, false),
                (new Vector3(52.0f, 0f, 15.75f), 180f, false),
                (new Vector3(62.2f, 0f, 15.65f), 180f, true), // Sparking corner-2 fault lamp!
                (new Vector3(71.5f, 0f, 7.6f), 225f, false),
                (new Vector3(85.0f, 0f, -1.6f), 180f, false)
            };

            for (int i = 0; i < lampSpots.Length; i++)
            {
                var spot = lampSpots[i];
                Zone23Dressing.LampPost(p, paths.transform, spot.pos, spot.yaw);
                LightGroupId group = spot.sparking ? LightGroupId.VCExit : LightGroupId.Paths;
                WorldArchitectureKit.BuildHdrpLight(paths.transform, $"Light_PathLamp_{i}", spot.pos + new Vector3(0f, 4.1f, 0f), new Vector3(90f, 0f, 0f),
                    LightType.Spot, spot.sparking ? new Color(0.85f, 0.92f, 1.0f) : new Color(1.0f, 0.86f, 0.62f),
                    spot.sparking ? 1800f : 2200f, 16f, group, volumetricDimmer: 1.6f, castShadows: spot.sparking, spotOuterAngle: 78f);

                if (spot.sparking)
                {
                    Vector3 sparkPos = spot.pos + new Vector3(0f, 4.1f, 0f);
                    WorldArchitectureKit.BuildWorldAnchor(paths.transform, "spark_lamp", sparkPos);
                    BuildPathLampSparksVfx(paths.transform, sparkPos, p);
                }
            }

            DressServicePathsLane(paths.transform, p);
            DressCorner2Faces(paths.transform, p);
            BuildPathBatteryBulkheads(paths.transform);

            // Service Path Story Triggers & Landmarks
            WorldArchitectureKit.BuildWorldAnchor(paths.transform, "paths_corner2", new Vector3(56f, 0f, 14f));
            WorldArchitectureKit.BuildWorldAnchor(paths.transform, "paths_mid", new Vector3(71f, 0f, 6f));
            WorldArchitectureKit.BuildWorldAnchor(paths.transform, "paths_end", new Vector3(85f, 0f, -4f));
            WorldArchitectureKit.BuildStoryTrigger(paths.transform, "paths_corner2", new Vector3(55f, 2f, 14f), new Vector3(5f, 4f, 4.8f), oneShot: true);
            WorldArchitectureKit.BuildStoryTrigger(paths.transform, "paths_mid", new Vector3(71f, 2f, 6f), new Vector3(5f, 4f, 5f), oneShot: true);
            WorldArchitectureKit.BuildStoryTrigger(paths.transform, "paths_end", new Vector3(85f, 2f, -3.5f), new Vector3(5f, 4f, 5.5f), oneShot: true);
        }

        // =========================================================================
        // OPEN NORTH T-BRANCH ALCOVES (DOOR-7 FIX — ZERO SEALING WALLS, ZERO ROOF SLABS!)
        // =========================================================================

        private static void BuildOpenNorthTBranches(Transform parent, WorldMaterialPalette p)
        {
            var branches = new GameObject("ServicePaths_OpenNorthBranches");
            branches.transform.SetParent(parent, false);

            // --- T-Branch 1 (X = 37.7..42.3, Z = +16.3..+34.0): Wrecked Kiosk + Grounded Fallen Log ---
            BuildRoadSegment(branches.transform, "Branch1_Asphalt", new Vector3(40f, 0f, 16.3f), new Vector3(40f, 0f, 34.0f), HalfW * 2f, p);
            var st = Zone23Dressing.WallStyle.Path;
            Zone23Dressing.PanelWall(branches.transform, "Wall_Branch1_W", new Vector3(37.7f, 0f, 16.3f), new Vector3(37.7f, 0f, 34.0f), 0f, WallH, WallT, new Vector3(40f, 0f, 25f), p, st, 0f, 0f, 11);
            Zone23Dressing.PanelWall(branches.transform, "Wall_Branch1_E", new Vector3(42.3f, 0f, 16.3f), new Vector3(42.3f, 0f, 34.0f), 0f, WallH, WallT, new Vector3(40f, 0f, 25f), p, st, 0f, 0f, 12);
            WorldArchitectureKit.BuildChainlinkFenceSpan(branches.transform, "Branch1_NorthEndFence", new Vector3(37.7f, 0f, 34.0f), new Vector3(42.3f, 0f, 34.0f), 0f, 4.5f, p);

            // Wrecked Park Kiosk with flickering emergency amber beacon (clearly visible as you walk north!)
            p.InstantiatePropOrFallback("wrecked_kiosk", branches.transform, new Vector3(40.4f, 0f, 21.0f), new Vector3(0f, -15f, 0f), new Vector3(2.8f, 2.8f, 2.4f), grounded: true, addCollider: true, fallbackMat: p.WoodPlanks);
            WorldArchitectureKit.BuildHdrpLight(branches.transform, "Light_Kiosk_Amber", new Vector3(40.2f, 2.4f, 20.5f), Vector3.zero, LightType.Point, new Color(1.0f, 0.56f, 0.16f), 480f, 9f, LightGroupId.VCExit, volumetricDimmer: 1.5f, pulsingEmergency: true);

            // Grounded fallen_log FLUSH at Y = 0 blocking past the kiosk (never hovering!)
            p.InstantiatePropOrFallback("fallen_log", branches.transform, new Vector3(40.0f, 0f, 26.2f), new Vector3(0f, 82f, 0f), new Vector3(1.1f, 1.0f, 4.4f), grounded: true, addCollider: true, fallbackMat: p.WoodPlanks, customName: "Branch1_FallenLog");

            // --- T-Branch 2 (X = 61.7..66.3, Z = +16.3..+34.0): STAFF ONLY Gate & Raptor Sprint Crossing Vista ---
            BuildRoadSegment(branches.transform, "Branch2_Asphalt", new Vector3(64f, 0f, 16.3f), new Vector3(64f, 0f, 34.0f), HalfW * 2f, p);
            Zone23Dressing.PanelWall(branches.transform, "Wall_Branch2_W", new Vector3(61.7f, 0f, 16.3f), new Vector3(61.7f, 0f, 34.0f), 0f, WallH, WallT, new Vector3(64f, 0f, 25f), p, st, 0f, 0f, 13);
            Zone23Dressing.PanelWall(branches.transform, "Wall_Branch2_E", new Vector3(66.3f, 0f, 16.3f), new Vector3(66.3f, 0f, 34.0f), 0f, WallH, WallT, new Vector3(64f, 0f, 25f), p, st, 0f, 0f, 14);

            // STAFF ONLY Chainlink Gate across Branch 2 (Z = +24.0) with backlit warning sign
            WorldArchitectureKit.BuildChainlinkFenceSpan(branches.transform, "Branch2_StaffOnlyGate", new Vector3(61.8f, 0f, 24.0f), new Vector3(66.2f, 0f, 24.0f), 0f, 4.2f, p, addDinoBlocker: false);
            Material staffSignMat = p.CreateTextureQuadMaterial("MAT_SignStaffOnly", "staff_only", emissive: true, emissiveIntensity: 1.5f, fallbackTint: new Color(0.95f, 0.65f, 0.14f));
            WorldArchitectureKit.BuildIlluminatedSign(branches.transform, "Sign_StaffOnly", new Vector3(64.0f, 2.3f, 23.85f), Vector3.zero, new Vector2(1.4f, 0.55f), staffSignMat, LightGroupId.VCExit);

            // Corner-2 Scripted Raptor Sprint Crossing Anchors (completely unobstructed sightline!)
            WorldArchitectureKit.BuildWorldAnchor(branches.transform, "raptor_cross_a", new Vector3(64.0f, 0f, 30.0f));
            WorldArchitectureKit.BuildWorldAnchor(branches.transform, "raptor_cross_b", new Vector3(64.0f, 0f, -6.0f));
        }

        // =========================================================================
        // PADDOCK 4 SERVICE GATE (X = 91.0, Z = -7.1 .. -0.9)
        // POS-6 FIX: Grounded fallen_log angled firmly onto bent north leaf;
        // Free-standing bollard-mounted gate_control_box (zero buried prop!).
        // =========================================================================

        private static void BuildPaddock4ServiceGate(Transform parent, WorldMaterialPalette p)
        {
            var gate = new GameObject("Paddock4_ServiceGate");
            gate.transform.SetParent(parent, false);

            // Heavy steel gate posts flanking X = 91.0 (gameplay boxes keep their colliders; they render as round posts)
            GameObject postN = WorldArchitectureKit.CreateBox(gate.transform, "GatePost_N", new Vector3(91.0f, 2.5f, -0.9f), new Vector3(0.38f, 5.0f, 0.38f), p.DarkPaintedMetal, addCollider: true);
            GameObject postS = WorldArchitectureKit.CreateBox(gate.transform, "GatePost_S", new Vector3(91.0f, 2.5f, -7.1f), new Vector3(0.38f, 5.0f, 0.38f), p.DarkPaintedMetal, addCollider: true);
            GameObject header = WorldArchitectureKit.CreateBox(gate.transform, "GateHeader", new Vector3(91.0f, 4.95f, -4.0f), new Vector3(0.42f, 0.35f, 6.6f), p.DarkPaintedMetal, addCollider: true);

            // Closed South Leaf (Z = -7.0 .. -3.4) and the bent-up North Leaf (Z = -3.4 .. -1.0, bottom edge at
            // Y = 1.24 m: the 1.22 m crouch gap). Review 1: 14 cm chain-link boxes read as grey sheets with polka dots
            // and the bent leaf's bottom lip glowed like a neon tube. Now one wire plane per leaf in a round tube frame,
            // the bent edge a rusted pipe with amber reflective tape. Colliders unchanged.
            GameObject southLeaf = WorldArchitectureKit.CreateBox(gate.transform, "SouthLeaf_Closed", new Vector3(91.0f, 2.4f, -5.2f), new Vector3(0.14f, 4.8f, 3.6f), p.ChainlinkFence, addCollider: true);
            WorldArchitectureKit.CreateBox(gate.transform, "SouthLeaf_Frame", new Vector3(91.0f, 0.12f, -5.2f), new Vector3(0.18f, 0.24f, 3.6f), p.RustedIron, addCollider: true);
            GameObject northLeaf = WorldArchitectureKit.CreateBox(gate.transform, "NorthLeaf_BentUp", new Vector3(91.15f, 3.05f, -2.2f), new Vector3(0.14f, 3.6f, 2.4f), p.ChainlinkFence, addCollider: true, euler: new Vector3(0f, 0f, -10f));
            Zone23Dressing.ThinChainlinkBox(southLeaf);
            Zone23Dressing.ThinChainlinkBox(northLeaf);
            Material galv = Zone23Dressing.GalvSteel(p);
            Zone23Dressing.TubeFrame(gate.transform, southLeaf, galv, 0.03f, midRail: true, brace: true);
            Zone23Dressing.TubeFrame(gate.transform, northLeaf, galv, 0.03f, midRail: false, brace: false);
            Zone23Dressing.RoundPost(gate.transform, postN, 0.15f, p.DarkPaintedMetal, galv);
            Zone23Dressing.RoundPost(gate.transform, postS, 0.15f, p.DarkPaintedMetal, galv);
            header.GetComponent<MeshRenderer>().enabled = false;
            Zone23Dressing.Cyl(gate.transform, "GateHeader_Pipe", new Vector3(91.0f, 4.95f, -7.1f), new Vector3(91.0f, 4.95f, -0.9f), 0.12f, p.DarkPaintedMetal);
            Zone23Dressing.Box(gate.transform, "GateHeader_Plate", new Vector3(91.0f, 4.72f, -4.0f), new Vector3(0.05f, 0.22f, 5.9f), p.DarkPaintedMetal, Quaternion.identity);
            // hinges and the drop bolt
            for (int h = 0; h < 3; h++)
                Zone23Dressing.Box(gate.transform, "SouthLeaf_Hinge", new Vector3(91.0f, 0.6f + h * 1.8f, -6.92f), new Vector3(0.12f, 0.12f, 0.16f), p.RustedIron, Quaternion.identity);
            Zone23Dressing.Cyl(gate.transform, "SouthLeaf_DropBolt", new Vector3(90.95f, 0.02f, -3.6f), new Vector3(90.95f, 0.9f, -3.6f), 0.018f, galv);
            // the bent edge: a rusted bottom pipe with three strips of amber reflective tape (they catch the flashlight)
            Vector3 lipA = northLeaf.transform.TransformPoint(new Vector3(0f, -0.5f, -0.5f));
            Vector3 lipB = northLeaf.transform.TransformPoint(new Vector3(0f, -0.5f, 0.5f));
            Zone23Dressing.Cyl(gate.transform, "NorthLeaf_BottomLip", lipA, lipB, 0.036f, p.DarkPaintedMetal);
            for (int k = 0; k < 3; k++)
            {
                Vector3 tp = Vector3.Lerp(lipA, lipB, 0.2f + k * 0.3f) + Vector3.left * 0.034f;
                Zone23Dressing.Box(gate.transform, "NorthLeaf_ReflectiveTape", tp, new Vector3(0.012f, 0.04f, 0.16f), Zone23Dressing.Reflector(p), Quaternion.identity);
            }

            // POS-6 FIX: Fallen log firmly grounded on the muddy east bank (93.4, 0, -0.4) leaning diagonally across the crushed gate top!
            p.InstantiatePropOrFallback("fallen_log", gate.transform, new Vector3(92.4f, 0.0f, -1.1f), new Vector3(26f, -52f, 18f), new Vector3(0.85f, 0.85f, 4.8f), grounded: true, addCollider: true, fallbackMat: p.WoodPlanks, customName: "ServiceGate_GroundedLog");

            // Free-standing bollard-mounted dead Gate Control Box cleanly offset at (89.6, 0, -6.0) — NOT buried in wall!
            WorldArchitectureKit.CreateBox(gate.transform, "ControlBollard", new Vector3(89.6f, 0.55f, -6.0f), new Vector3(0.20f, 1.10f, 0.20f), p.DarkPaintedMetal, addCollider: true);
            p.InstantiatePropOrFallback("gate_control_box", gate.transform, new Vector3(89.6f, 1.10f, -6.0f), new Vector3(0f, -90f, 0f), new Vector3(0.38f, 0.52f, 0.28f), grounded: true, addCollider: true);
            WorldArchitectureKit.BuildWorldAnchor(gate.transform, "service_gate_box", new Vector3(89.6f, 1.25f, -6.0f));
            Zone1_VisitorCenterBuilder.BuildSimpleInteractableBox(gate.transform, "Interact_ServiceGateBox", new Vector3(89.6f, 1.25f, -6.0f), new Vector3(0.9f, 1.2f, 0.9f),
                "service_gate_box", "Use Paddock 4 gate control {interact}", holdSec: 0f, singleUse: false);

            // Paddock 4 Entry Trigger & Checkpoint Anchor right behind crawl gap
            WorldArchitectureKit.BuildWorldAnchor(gate.transform, "fence_gap", new Vector3(91.0f, 0f, -2.2f));
            WorldArchitectureKit.BuildWorldAnchor(gate.transform, "paddock_entry", new Vector3(93.8f, 0.05f, -2.2f), yawDeg: 85f);
            WorldArchitectureKit.BuildStoryTrigger(gate.transform, "enter_paddock", new Vector3(93.5f, 1.5f, -2.2f), new Vector3(2.2f, 3.0f, 4.0f), oneShot: true);

            // Low amber guide light illuminating the crawl gap
            WorldArchitectureKit.BuildHdrpLight(gate.transform, "Light_ServiceGate_Beacon", new Vector3(90.5f, 4.4f, -2.2f), new Vector3(90f, 0f, 0f), LightType.Spot, new Color(1.0f, 0.52f, 0.14f), 950f, 10f, LightGroupId.VCExit, volumetricDimmer: 1.8f, spotOuterAngle: 68f);
        }

        // =========================================================================
        // TALON RIDGE RAPTOR PEN (X: -90 .. +20, Z: +42 .. +130)
        // Directly outside Control Room's un-occluded 17 m viewing window!
        // =========================================================================

        private static void BuildTalonRidgeRaptorPen(Transform parent, WorldMaterialPalette p)
        {
            var pen = new GameObject("TalonRidge_RaptorPen");
            pen.transform.SetParent(parent, false);

            // The paddock floor is the terrain itself now (plateau Y -0.05, mud/grass splat): no slab.
            // Heavy electrified fence on a 1 m concrete plinth along Z = +42.0, split around the 10 m sliding gate.
            const float plinthTop = 0.95f, fenceH = 6.8f;
            (string name, Vector3 a, Vector3 b, float posts, Vector3 inside)[] spans =
            {
                ("TalonFence_SouthW", new Vector3(-90f, 0f, 42f), new Vector3(-42.2f, 0f, 42f), 6f, new Vector3(-60f, 0f, 80f)),
                ("TalonFence_SouthE", new Vector3(-29.8f, 0f, 42f), new Vector3(20f, 0f, 42f), 6f, new Vector3(0f, 0f, 80f)),
                ("TalonFence_West", new Vector3(-90f, 0f, 42f), new Vector3(-90f, 0f, 128f), 8f, new Vector3(-40f, 0f, 85f)),
                ("TalonFence_East", new Vector3(20f, 0f, 42f), new Vector3(20f, 0f, 128f), 8f, new Vector3(-40f, 0f, 85f)),
            };
            foreach (var sp in spans)
            {
                WorldArchitectureKit.BuildChainlinkFenceSpan(pen.transform, sp.name, sp.a, sp.b, plinthTop, fenceH, p, postSpacing: sp.posts);
                BuildTalonPlinthAndWires(pen.transform, sp.name, sp.a, sp.b, plinthTop, fenceH, sp.posts, sp.inside, p);
            }

            // Massive Concrete Gate Pylons flanking the 10 m Talon Gate (dramatically framed in the Control Room window!)
            WorldArchitectureKit.CreateBox(pen.transform, "TalonPylon_W", new Vector3(-41.6f, 4.2f, 42f), new Vector3(1.2f, 8.4f, 1.2f), p.ConcreteBoardform, addCollider: true);
            WorldArchitectureKit.CreateBox(pen.transform, "TalonPylon_E", new Vector3(-30.4f, 4.2f, 42f), new Vector3(1.2f, 8.4f, 1.2f), p.ConcreteBoardform, addCollider: true);
            WorldArchitectureKit.CreateBox(pen.transform, "TalonGate_HeaderBeam", new Vector3(-36.0f, 8.0f, 42f), new Vector3(12.6f, 0.9f, 1.0f), p.DarkPaintedMetal, addCollider: true);

            // Heavy Sliding Containment Gate (slides +9.5 m East when grid fails OPEN during blackout!)
            WorldArchitectureKit.BuildFramedDoor(
                pen.transform, "talon_gate", new Vector3(-36.0f, 0f, 42.0f), 0f, 10.0f, 7.5f,
                DoorMechanism.SlidingHorizontal, p, startsOpen: false, startsLocked: true,
                lockedHint: "Talon Ridge containment gate.", soundPrefix: "gate_heavy_slide",
                slideOpenOffset: new Vector3(9.5f, 0f, 0f), bashableByRaptor: false, leafMaterial: p.DarkPaintedMetal);
            DressTalonGateAsBars(pen.transform, p);
            WorldArchitectureKit.BuildWorldAnchor(pen.transform, "talon_gate_view", new Vector3(-36.0f, 2.5f, 42.0f));

            // Rain-streaked Talon Ridge Volumetric Pen Floodlights (LightGroupId.Talon -> die 1.4s before emergency power!)
            WorldArchitectureKit.BuildHdrpLight(pen.transform, "Light_Talon_FloodW", new Vector3(-41.6f, 8.2f, 42.8f), new Vector3(48f, 22f, 0f), LightType.Spot, new Color(0.88f, 0.95f, 1.0f), 28000f, 48f, LightGroupId.Talon, volumetricDimmer: 2.2f, castShadows: true, spotOuterAngle: 72f);
            WorldArchitectureKit.BuildHdrpLight(pen.transform, "Light_Talon_FloodE", new Vector3(-30.4f, 8.2f, 42.8f), new Vector3(48f, -22f, 0f), LightType.Spot, new Color(0.88f, 0.95f, 1.0f), 28000f, 48f, LightGroupId.Talon, volumetricDimmer: 2.2f, castShadows: false, spotOuterAngle: 72f);

            // Red warning aircraft/security strobes atop gate pylons (stay lit on battery in blackout!)
            WorldArchitectureKit.BuildHdrpLight(pen.transform, "Light_Talon_StrobeW", new Vector3(-41.6f, 8.8f, 42f), Vector3.zero, LightType.Point, new Color(1.0f, 0.14f, 0.10f), 550f, 18f, LightGroupId.PaddockBattery, volumetricDimmer: 1.8f, pulsingEmergency: true);
            WorldArchitectureKit.BuildHdrpLight(pen.transform, "Light_Talon_StrobeE", new Vector3(-30.4f, 8.8f, 42f), Vector3.zero, LightType.Point, new Color(1.0f, 0.14f, 0.10f), 550f, 18f, LightGroupId.PaddockBattery, volumetricDimmer: 1.8f, pulsingEmergency: true);

            DressTalonPenInterior(pen.transform, p);
        }

        /// <summary>1 m concrete plinth under a Talon fence span + standoff insulators and live wires on the pen side,
        /// a cranked outrigger with three wires on every post, and warning plates on the outside every ~14 m.</summary>
        private static void BuildTalonPlinthAndWires(Transform parent, string name, Vector3 a, Vector3 b, float plinthTop, float fenceH, float postSpacing, Vector3 inside, WorldMaterialPalette p)
        {
            var root = new GameObject(name + "_PlinthAndWires");
            root.transform.SetParent(parent, false);
            Vector3 d = b - a; d.y = 0f; float len = d.magnitude; d /= len;
            Vector3 n = new Vector3(-d.z, 0f, d.x);
            if (Vector3.Dot(inside - (a + b) * 0.5f, n) < 0f) n = -n;            // n points into the pen
            Quaternion rot = Quaternion.LookRotation(d, Vector3.up);
            Vector3 mid = (a + b) * 0.5f;
            const float bottom = -0.45f;
            GameObject plinth = Zone23Dressing.Box(root.transform, "Plinth", mid + Vector3.up * ((plinthTop + bottom) * 0.5f), new Vector3(0.55f, plinthTop - bottom, len + 0.3f), p.ConcreteBoardform, rot, collider: true);
            Zone23Dressing.Box(root.transform, "PlinthCap", mid + Vector3.up * (plinthTop + 0.03f), new Vector3(0.62f, 0.06f, len + 0.36f), Zone23Dressing.Coping(p), rot);

            Material wire = Zone23Dressing.GalvSteel(p);
            Material porc = Zone23Dressing.Porcelain(p);
            int posts = Mathf.Max(2, Mathf.CeilToInt(len / postSpacing) + 1);
            float[] standoffY = { 1.2f, 2.6f, 4.0f, 5.4f };
            for (int i = 0; i < posts; i++)
            {
                Vector3 pp = a + d * (i / (float)(posts - 1) * len);
                foreach (float y in standoffY)
                {
                    Vector3 s0 = pp + Vector3.up * (plinthTop + y);
                    Zone23Dressing.Cyl(root.transform, "Standoff", s0, s0 + n * 0.22f, 0.012f, wire);
                    Zone23Dressing.Cyl(root.transform, "Insulator", s0 + n * 0.16f, s0 + n * 0.26f, 0.032f, porc);
                }
                // cranked outrigger leaning into the pen with three insulators
                Vector3 top = pp + Vector3.up * (plinthTop + fenceH);
                Vector3 tip = top + n * 0.85f + Vector3.up * 0.85f;
                Zone23Dressing.Cyl(root.transform, "Outrigger", top, tip, 0.03f, p.DarkPaintedMetal);
                for (int k = 1; k <= 3; k++)
                {
                    Vector3 q = Vector3.Lerp(top, tip, k / 3.2f);
                    Zone23Dressing.Cyl(root.transform, "OutriggerInsulator", q - Vector3.up * 0.02f, q + Vector3.up * 0.07f, 0.03f, porc);
                }
            }
            // wires: four standoff runs + three outrigger runs
            foreach (float y in standoffY)
            {
                Vector3 off = n * 0.26f + Vector3.up * (plinthTop + y);
                Zone23Dressing.Cyl(root.transform, "Wire", a + off, b + off, 0.007f, wire);
            }
            for (int k = 1; k <= 3; k++)
            {
                Vector3 off = Vector3.up * (plinthTop + fenceH) + Vector3.Lerp(Vector3.zero, n * 0.85f + Vector3.up * 0.85f, k / 3.2f) + Vector3.up * 0.07f;
                Zone23Dressing.Cyl(root.transform, "TopWire", a + off, b + off, 0.007f, wire);
            }
            // warning plates on the outside of the mesh
            // (talon_ridge_warning reads "DO NOT TAP ON GLASS": a viewing-window sign, so the fence carries HV plates)
            Material warn = Zone23Dressing.SignMat(p, "MAT_Z23_SignHighVoltage", "high_voltage");
            for (float s = 7f; s < len - 3f; s += 14f)
                Zone23Dressing.SignPlate(root.transform, "Sign_TalonHV", a + d * s - n * 0.07f + Vector3.up * (plinthTop + 1.45f), -n, 0.55f, 0.75f, warn, p.DarkPaintedMetal);
            // moss and grime on the plinth (both faces)
            for (float s = 3f; s < len - 2f; s += 9f)
            {
                Zone23Dressing.Decal(root.transform, "PlinthMossOut", a + d * s - n * 0.45f + Vector3.up * 0.3f, -n, new Vector3(3.2f, 1.2f, 0.6f), Zone23Dressing.Moss(p), 0.9f);
                Zone23Dressing.Decal(root.transform, "PlinthGrimeIn", a + d * (s + 4f) + n * 0.45f + Vector3.up * 0.4f, n, new Vector3(2.2f, 1.3f, 0.6f), Zone23Dressing.Grime(p, (int)s), 0.8f);
            }
        }

        /// <summary>The sliding Talon gate reads as a barred cage gate: the solid panel keeps its collider but stops
        /// rendering, and 5 cm bars, rails and braces ride on the sliding leaf (Door layer, so they move with it).</summary>
        private static void DressTalonGateAsBars(Transform parent, WorldMaterialPalette p)
        {
            Transform door = parent.Find("Door_talon_gate");
            Transform leaf = door != null ? door.Find("SlidingLeaf") : null;
            if (leaf == null) { Debug.LogWarning("[Zone2] talon_gate leaf not found; gate stays solid."); return; }
            Transform panel = leaf.Find("GatePanel");
            if (panel != null) { MeshRenderer mr = panel.GetComponent<MeshRenderer>(); if (mr != null) mr.enabled = false; }

            const float w = 10f, h = 7.5f;
            var bars = new GameObject("GateBars");
            bars.transform.SetParent(leaf, false);
            Material steel = p.DarkPaintedMetal;
            for (float x = -w * 0.5f + 0.12f; x <= w * 0.5f - 0.1f; x += 0.19f)
                WorldArchitectureKit.CreateBox(bars.transform, "Bar", new Vector3(x, h * 0.5f, 0f), new Vector3(0.05f, h - 0.1f, 0.05f), steel, addCollider: false);
            float[] rails = { 0.12f, 2.6f, 5.0f, h - 0.32f };
            foreach (float y in rails)
                WorldArchitectureKit.CreateBox(bars.transform, "Rail", new Vector3(0f, y, 0f), new Vector3(w, 0.16f, 0.14f), steel, addCollider: false);
            WorldArchitectureKit.CreateBox(bars.transform, "Stile_L", new Vector3(-w * 0.5f + 0.06f, h * 0.5f, 0f), new Vector3(0.16f, h, 0.16f), steel, addCollider: false);
            WorldArchitectureKit.CreateBox(bars.transform, "Stile_R", new Vector3(w * 0.5f - 0.06f, h * 0.5f, 0f), new Vector3(0.16f, h, 0.16f), steel, addCollider: false);
            float diag = Mathf.Sqrt(5f * 5f + 2.48f * 2.48f), ang = Mathf.Atan2(2.48f, 5f) * Mathf.Rad2Deg;
            WorldArchitectureKit.CreateBox(bars.transform, "Brace_L", new Vector3(-2.5f, 1.36f, -0.08f), new Vector3(diag, 0.12f, 0.06f), steel, addCollider: false, euler: new Vector3(0f, 0f, ang));
            WorldArchitectureKit.CreateBox(bars.transform, "Brace_R", new Vector3(2.5f, 1.36f, -0.08f), new Vector3(diag, 0.12f, 0.06f), steel, addCollider: false, euler: new Vector3(0f, 0f, -ang));
            // heavy top rail (the leaf's own TopBeam stays hidden: it would slide through the pylon and the plinth)
            WorldArchitectureKit.CreateBox(bars.transform, "TopRail", new Vector3(0f, h - 0.1f, 0f), new Vector3(w + 0.1f, 0.2f, 0.2f), steel, addCollider: false);
            Transform topBeam = leaf.Find("TopBeam");
            if (topBeam != null) { MeshRenderer tr = topBeam.GetComponent<MeshRenderer>(); if (tr != null) tr.enabled = false; }
            GameLayers.SetLayerRecursively(bars, GameLayers.Door);
            // a high-voltage plate bolted to the middle rail, facing the Control Room
            GameObject plate = Zone23Dressing.SignPlate(bars.transform, "Sign_TalonGate", leaf.position + new Vector3(0f, 3.0f, -0.1f), Vector3.back, 0.8f, 1.1f, Zone23Dressing.SignMat(p, "MAT_Z23_SignHighVoltage", "high_voltage"), steel);
            GameLayers.SetLayerRecursively(plate, GameLayers.Door);
            // the visible gate rides an outside track 0.75 m in front of the pylons (pylon face z 41.4, plinth from
            // z 41.69), so in the scripted beat the bars slide clear of the pylon, the plinth and the fence. The door,
            // its collider and its anchor stay where they are.
            bars.transform.localPosition = new Vector3(0f, 0f, -0.75f);
            float zr = leaf.position.z - 0.75f;
            Material rail = p.RustedIron;
            for (float x = -41.2f; x < -20.6f; x += 2.4f)
            {
                float x1 = Mathf.Min(x + 2.4f, -20.6f);
                float gy = Zone23Dressing.Ground((x + x1) * 0.5f, zr);
                Zone23Dressing.Box(parent, "GateGroundRail", new Vector3((x + x1) * 0.5f, gy + 0.03f, zr), new Vector3(x1 - x + 0.02f, 0.1f, 0.12f), rail, Quaternion.identity);
                Zone23Dressing.Box(parent, "GateGroundRailSleeper", new Vector3(x, gy - 0.01f, zr), new Vector3(0.22f, 0.1f, 0.6f), Zone23Dressing.Plinth(p), Quaternion.identity);
            }
            float sy = Zone23Dressing.Ground(-20.4f, zr);
            Zone23Dressing.Box(parent, "GateEndStopPost", new Vector3(-20.4f, sy + 0.7f, zr), new Vector3(0.3f, 1.5f, 0.3f), p.PaintedMetalYellow, Quaternion.identity, collider: true);
            Zone23Dressing.Box(parent, "GateEndStopFooting", new Vector3(-20.4f, sy + 0.02f, zr), new Vector3(0.7f, 0.14f, 0.7f), Zone23Dressing.Plinth(p), Quaternion.identity);
            Zone23Dressing.Box(parent, "GateEndStopBuffer", new Vector3(-20.6f, sy + 0.55f, zr), new Vector3(0.12f, 0.3f, 0.26f), Zone23Dressing.DrainVoid(p), Quaternion.identity);
        }

        /// <summary>Inside the pen: feeding cage and a hanging carcass on a gantry, framed by the gate; rock outcrops;
        /// hero jungle behind a 12 m mud apron; cold flood masts at the fence.</summary>
        private static void DressTalonPenInterior(Transform parent, WorldMaterialPalette p)
        {
            var d = new GameObject("TalonPen_Interior");
            d.transform.SetParent(parent, false);

            // feeding station framed by the gate (about 20 m in)
            p.InstantiatePropOrFallback("feeding_cage", d.transform, new Vector3(-37.5f, -0.05f, 61f), new Vector3(0f, 18f, 0f), new Vector3(3.4f, 3.4f, 3.4f), grounded: true, addCollider: true);
            Vector3 g = new Vector3(-33.2f, -0.05f, 60.2f);
            Material steel = p.RustedIron;
            Zone23Dressing.Box(d.transform, "MeatGantry_PostA", g + new Vector3(-1.4f, 2.1f, 0f), new Vector3(0.16f, 4.2f, 0.16f), steel, Quaternion.identity, collider: true);
            Zone23Dressing.Box(d.transform, "MeatGantry_PostB", g + new Vector3(1.4f, 2.1f, 0f), new Vector3(0.16f, 4.2f, 0.16f), steel, Quaternion.identity, collider: true);
            Zone23Dressing.Box(d.transform, "MeatGantry_Beam", g + new Vector3(0f, 4.15f, 0f), new Vector3(3.2f, 0.18f, 0.18f), steel, Quaternion.identity);
            Zone23Dressing.Cyl(d.transform, "MeatChain", g + new Vector3(0.2f, 4.05f, 0f), g + new Vector3(0.2f, 3.0f, 0f), 0.015f, p.DarkPaintedMetal);
            p.InstantiatePropOrFallback("hanging_meat", d.transform, g + new Vector3(0.2f, 1.45f, 0f), new Vector3(0f, 30f, 0f), new Vector3(1.6f, 1.6f, 1.6f), grounded: true, addCollider: false);
            Zone23Dressing.Decal(d.transform, "MeatBlood", g + new Vector3(0.3f, 0.1f, 0.2f), Vector3.up, new Vector3(2.4f, 2.4f, 0.6f), p.DecalBloodSmear, 0.8f);

            // rock outcrops and boulders (seated on the plateau)
            (string stem, Vector3 pos, float yaw, float size)[] rocks =
            {
                ("rock_face_01", new Vector3(-56f, 0f, 78f), 20f, 7.5f), ("rock_face_02", new Vector3(-18f, 0f, 84f), 200f, 8f),
                ("rock_face_01", new Vector3(-74f, 0f, 104f), 120f, 9f), ("rock_face_02", new Vector3(4f, 0f, 110f), 300f, 9.5f),
                ("rock_moss_set_01", new Vector3(-46f, 0f, 66f), 40f, 3.4f), ("rock_moss_set_02", new Vector3(-25f, 0f, 70f), 160f, 3.2f),
                ("namaqualand_boulder_03", new Vector3(-40f, 0f, 74f), 80f, 2.6f), ("namaqualand_boulder_02", new Vector3(-29f, 0f, 58f), 10f, 1.8f),
                ("namaqualand_boulder_04", new Vector3(-62f, 0f, 60f), 230f, 2.4f), ("rock_moss_set_01", new Vector3(-8f, 0f, 62f), 300f, 3.0f),
                ("namaqualand_boulder_03", new Vector3(-82f, 0f, 70f), 140f, 3.0f), ("rock_moss_set_02", new Vector3(10f, 0f, 76f), 60f, 3.6f),
            };
            foreach (var r in rocks)
                Zone23Dressing.Plant(p, d.transform, r.stem, r.pos, r.yaw, r.size, 0.01f, collider: false);

            // jungle: tall trees from 56 m back, ferns and banana understory, a few right against the inside of the fence
            string[] trees = { "jungle_tree", "palm_tree", "jungle_tree", "palm_tree_b", "tree_fern" };
            int k = 0;
            for (float z = 58f; z < 126f; z += 9f)
                for (float x = -86f; x < 16f; x += 10f, k++)
                {
                    float hx = Mathf.Repeat(Mathf.Sin(x * 3.7f + z * 1.3f) * 917.3f, 1f);
                    float hz = Mathf.Repeat(Mathf.Sin(x * 1.1f + z * 5.9f) * 431.7f, 1f);
                    Vector3 pos = new Vector3(x + hx * 6f, 0f, z + hz * 5f);
                    if (Mathf.Abs(pos.x + 36f) < 9f && pos.z < 72f) continue;          // keep the gate's view to the cage open
                    if (hx < 0.25f) continue;
                    string stem = trees[(k + (int)(hz * 5f)) % trees.Length];
                    float size = stem == "jungle_tree" ? 15f + 5f * hz : (stem == "tree_fern" ? 4.2f : 10f + 3f * hz);
                    Zone23Dressing.Plant(p, d.transform, stem, pos, 360f * hx, size, 0.008f);
                    if (hz > 0.4f) Zone23Dressing.Plant(p, d.transform, hz > 0.7f ? "banana_plant" : "tree_fern", pos + new Vector3(2.4f, 0f, -1.6f), 360f * hz, 2.6f, 0.03f, shadows: false);
                }
            for (float x = -86f; x < 18f; x += 5.5f)
            {
                if (x > -46f && x < -26f) continue;
                float h = Mathf.Repeat(Mathf.Sin(x * 7.1f) * 311.3f, 1f);
                Zone23Dressing.Plant(p, d.transform, h > 0.5f ? "tree_fern" : "monstera_clump", new Vector3(x + h * 2f, 0f, 44.6f + h * 1.5f), 360f * h, h > 0.5f ? 3.6f : 1.6f, 0.03f, shadows: false);
            }

            // cold flood masts at the fence (Talon group: lit in "Night", dead in the blackout)
            Vector3[] masts = { new(-68f, 0f, 41.2f), new(-4f, 0f, 41.2f) };
            for (int i = 0; i < masts.Length; i++)
            {
                float my = Zone23Dressing.Ground(masts[i].x, masts[i].z) - 0.15f;
                Zone23Dressing.Box(d.transform, $"TalonFloodMast_{i}", new Vector3(masts[i].x, (my + 9.2f) * 0.5f, masts[i].z), new Vector3(0.26f, 9.2f - my, 0.26f), p.DarkPaintedMetal, Quaternion.identity, collider: true);
                Zone23Dressing.Box(d.transform, $"TalonFloodMastFooting_{i}", new Vector3(masts[i].x, my + 0.2f, masts[i].z), new Vector3(0.7f, 0.3f, 0.7f), Zone23Dressing.Plinth(p), Quaternion.identity);
                Vector3 lp = masts[i] + new Vector3(0f, 9.0f, 0.4f);
                Vector3 aim = (new Vector3(masts[i].x * 0.6f - 14f, 0f, 70f) - lp).normalized;
                WorldArchitectureKit.BuildHdrpLight(d.transform, $"Light_Talon_FloodFence_{i}", lp, Quaternion.LookRotation(aim).eulerAngles, LightType.Spot, new Color(0.88f, 0.95f, 1.0f), 18000f, 50f, LightGroupId.Talon, volumetricDimmer: 1.0f, castShadows: i == 0, spotOuterAngle: 80f);
            }
        }

        // =========================================================================
        // OVERTURNED RANGER JEEP & RECORDED RADIO LOG (Document 4/5 — jeep_radio!)
        // Grounded cleanly off-shoulder with blinking amber hazards & cab radio prop!
        // =========================================================================

        public static GameObject BuildOverturnedRangerJeep(Transform parent, WorldMaterialPalette p, Vector3 worldPos)
        {
            var jeepRoot = new GameObject("OverturnedRangerJeep");
            jeepRoot.transform.SetParent(parent, false);
            jeepRoot.transform.position = worldPos;

            // Seated on the terrain (no slab: the old concrete pad was half buried, half floating on the slope).
            // round 3 (review 2: the near edge hovered ~25 cm over the shoulder that falls toward the road): seated on the
            // lowest of its four corners and sunk 0.18 m into the soft shoulder
            // fix round: the corners are sampled in the jeep's own frame (yaw -24, footprint ~1.9 x 4.4 m, long axis on
            // local z), so its ends are included; the radio document keeps the pre-round-3 seat height that the route
            // test was built on
            float ySeatOld = 0.5f * (Zone23Dressing.Ground(worldPos.x - 2f, worldPos.z) + Zone23Dressing.Ground(worldPos.x + 2f, worldPos.z));
            float gMin = float.MaxValue;
            Quaternion jy = Quaternion.Euler(0f, -24f, 0f);
            foreach (Vector3 o in new[] { new Vector3(-1f, 0f, -2.2f), new Vector3(1f, 0f, -2.2f), new Vector3(-1f, 0f, 2.2f), new Vector3(1f, 0f, 2.2f), Vector3.zero })
            {
                Vector3 q = jy * o;
                gMin = Mathf.Min(gMin, Zone23Dressing.Ground(worldPos.x + q.x, worldPos.z + q.z));
            }
            worldPos.y = gMin - 0.18f;
            jeepRoot.transform.position = worldPos;
            DressJeepCrashSite(jeepRoot.transform, p, worldPos);

            // Rolled Jeep chassis seated flush to ground (POS-15 & P1-11 fix!)
            p.InstantiatePropOrFallback("jeep", jeepRoot.transform, worldPos - Vector3.up * 0.04f, new Vector3(0f, -24f, 86f), new Vector3(2.1f, 1.9f, 4.4f), grounded: true, addCollider: true, fallbackMat: p.RustedIron, customName: "WreckedRangerJeep");

            // Glowing interior dashboard radio handset + DocumentPickup (registers AddDocument("jeep_radio") for 5/5 docs!)
            Vector3 radioPos = new Vector3(worldPos.x + 1.1f, ySeatOld + 0.88f, worldPos.z + 0.65f);
            WorldArchitectureKit.BuildWorldAnchor(jeepRoot.transform, "jeep_radio", radioPos);
            Zone1_VisitorCenterBuilder.BuildDocumentProp(
                jeepRoot.transform, radioPos, "jeep_radio", "Ranger Unit 4 - Recorded Radio Log",
                "Play Ranger Jeep Radio Log {interact}",
                new[] { "jeep_radio" },
                new[] { "RANGER UNIT 4: The fence is DEAD! Stay in the tall grass - it tracks the lights!" },
                p, assetStem: "walkie_talkie", isAudioLog: true, oneShot: true);

            // Blinking Amber Hazard Beacon (P1-11 calibrated: moody pulsing amber glow without eye-height camera flare!)
            WorldArchitectureKit.BuildHdrpLight(jeepRoot.transform, "Light_JeepHazard", worldPos + new Vector3(-1.4f, 0.82f, -0.4f), Vector3.zero, LightType.Point, new Color(1.0f, 0.54f, 0.08f), 160f, 8.0f, LightGroupId.PaddockBattery, volumetricDimmer: 0.45f, pulsingEmergency: true);
            WorldArchitectureKit.BuildHdrpLight(jeepRoot.transform, "Light_JeepRadioGlow", radioPos + Vector3.up * 0.18f, Vector3.zero, LightType.Point, new Color(0.22f, 0.92f, 0.45f), 24f, 2.2f, LightGroupId.PaddockBattery, volumetricDimmer: 0.35f);

            return jeepRoot;
        }

        /// <summary>
        /// Lane dressing: extra puddles and oil stains, raptor prints across the corner-2 crossing, a palm that has
        /// come down over the Seg4 coping (the "something crashes through the jungle above" beat at (75,4,9)), hazard
        /// paint and the Paddock 4 plate at the gate, edge clutter (no colliders: the lanes stay exactly as walkable).
        /// </summary>
        private static void DressServicePathsLane(Transform parent, WorldMaterialPalette p)
        {
            var dr = new GameObject("ServicePaths_Dressing");
            dr.transform.SetParent(parent, false);

            // more standing water at the low edges + oil stains in the middle of the lanes
            (Vector3 c, Vector3 s, float yaw)[] puddles =
            {
                (new Vector3(25.5f, 0.05f, -10.2f), new Vector3(3.2f, 1.8f, 0.5f), 0f), (new Vector3(39.2f, 0.05f, 10.5f), new Vector3(1.8f, 4.0f, 0.5f), 0f),
                (new Vector3(46.5f, 0.05f, 12.9f), new Vector3(4.5f, 2.0f, 0.5f), 0f), (new Vector3(60.5f, 0.05f, 14.8f), new Vector3(3.0f, 2.4f, 0.5f), 0f),
                (new Vector3(68.8f, 0.05f, 9.0f), new Vector3(2.4f, 4.6f, 0.5f), 41f), (new Vector3(74.5f, 0.05f, 2.0f), new Vector3(2.2f, 3.6f, 0.5f), 41f),
                (new Vector3(89.0f, 0.05f, -4.6f), new Vector3(2.6f, 3.2f, 0.5f), 0f), (new Vector3(40.2f, 0.05f, 29.5f), new Vector3(3.0f, 2.6f, 0.5f), 0f),
                (new Vector3(64.3f, 0.05f, 20.5f), new Vector3(2.8f, 3.0f, 0.5f), 0f),
            };
            for (int i = 0; i < puddles.Length; i++)
                WorldArchitectureKit.BuildDecal(dr.transform, $"PathPuddle_x{i}", puddles[i].c, new Vector3(90f, puddles[i].yaw, 0f), puddles[i].s, p.DecalPuddleWet, 0.58f);
            Vector3[] stains = { new(28f, 0.05f, -9.2f), new(40.3f, 0.05f, -3f), new(50f, 0.05f, 13.8f), new(72f, 0.05f, 4f), new(86.5f, 0.05f, -3.4f) };
            for (int i = 0; i < stains.Length; i++)
                Zone23Dressing.Decal(dr.transform, $"OilStain_{i}", stains[i], Vector3.up, new Vector3(1.6f, 2.2f, 0.4f), Zone23Dressing.Grime(p, i), 0.55f, rollDeg: 37f * i);
            Vector3[] cracks = { new(33f, 0.05f, -8.5f), new(40.6f, 0.05f, 8f), new(57f, 0.05f, 14.6f), new(81f, 0.05f, -2.8f) };
            for (int i = 0; i < cracks.Length; i++)
                Zone23Dressing.Decal(dr.transform, $"RoadCracks_{i}", cracks[i], Vector3.up, new Vector3(2.4f, 2.4f, 0.4f), i % 2 == 0 ? p.DecalCracks : Zone23Dressing.Cracks2(p), 0.8f, rollDeg: 70f * i);

            // raptor prints: out of the Branch 2 mouth, across the lane towards the smeared corner
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                Vector3 c = Vector3.Lerp(new Vector3(64.4f, 0.05f, 18.6f), new Vector3(62.6f, 0.05f, 12.4f), t) + new Vector3((i % 2 == 0 ? 0.22f : -0.22f), 0f, 0f);
                Zone23Dressing.Decal(dr.transform, $"RaptorPrint_{i}", c, Vector3.up, new Vector3(0.42f, 0.5f, 0.3f), Zone23Dressing.RaptorPrint(p), 0.9f, rollDeg: 196f);
            }

            // the fallen palm: rooted in the jungle outside Seg4_NE, its trunk smashed through the coping (a 0.6 m notch,
            // chunks on the kerb), crown over the lane; a snapped-off length of trunk lies on the lane edge
            Vector3 wallDir = new Vector3(13.8f, 0f, -16.1f).normalized;
            Vector3 inN = new Vector3(wallDir.z, 0f, -wallDir.x);            // points SW, into the lane
            Vector3 onWall = new Vector3(66.3f, 0f, 16.3f) + wallDir * 11.2f;     // ~(73.6, 7.8): opposite the bush sound
            const float lean = 24f;                                               // trunk rests in the notch at ~5.15 m
            Vector3 root = onWall - inN * (5.15f * Mathf.Tan(lean * Mathf.Deg2Rad));
            root.y = Zone23Dressing.Ground(root.x, root.z) - 0.05f;
            GameObject palm = p.InstantiatePropOrFallback("palm_tree", dr.transform, root, new Vector3(0f, 310f, 0f), new Vector3(17f, 17f, 17f), grounded: true, addCollider: false, fallbackMat: p.FoliageDark, customName: "FallenPalm_OverSeg4");   // trunk to crown ~8 m: ~2.5 m of trunk shows past the notch before the crown
            palm.transform.RotateAround(root, Vector3.Cross(Vector3.up, inN), lean);
            // its root plate torn out of the ground and broken wood at its foot (outside)
            p.InstantiatePropOrFallback("root_cluster_01", dr.transform, root - inN * 0.8f, new Vector3(0f, 20f, 0f), new Vector3(2.6f, 2.6f, 2.6f), grounded: true, addCollider: false, fallbackMat: p.FoliageDark);
            p.InstantiatePropOrFallback("debris_pile", dr.transform, root - inN * 0.4f + wallDir * 2.2f, new Vector3(0f, 40f, 0f), new Vector3(1.8f, 1.8f, 1.8f), grounded: true, addCollider: false);
            NotchCoping(parent.Find("Wall_Seg4_NE/Coping"), onWall, 0.6f, p);
            // coping chunks knocked onto the kerb and the drain, fronds under the crown (out of the walking line)
            (float along, float outN, Vector3 size, Vector3 eul)[] chunks =
            {
                (-0.35f, 0.32f, new Vector3(0.42f, 0.17f, 0.36f), new Vector3(8f, 33f, 4f)),
                (0.45f, 0.55f, new Vector3(0.3f, 0.15f, 0.26f), new Vector3(-12f, 71f, 18f)),
                (1.2f, 0.48f, new Vector3(0.22f, 0.12f, 0.2f), new Vector3(25f, 140f, -9f)),
            };
            for (int i = 0; i < chunks.Length; i++)
            {
                Vector3 cp = onWall + inN * (WallT * 0.5f + chunks[i].outN) + wallDir * chunks[i].along;
                cp.y = (chunks[i].outN < 0.4f ? 0.15f : 0.02f) + chunks[i].size.y * 0.42f;
                Zone23Dressing.Box(dr.transform, $"CopingChunk_{i}", cp, chunks[i].size, Zone23Dressing.Coping(p), Quaternion.Euler(chunks[i].eul));
            }
            Zone23Dressing.Decal(dr.transform, "PalmImpactScuff", onWall + inN * (WallT * 0.5f + 0.1f) + Vector3.up * 4.3f, inN, new Vector3(1.2f, 1.6f, 0.4f), Zone23Dressing.Grime(p, 1), 0.85f);
            Material bark = Zone23Dressing.Bark(p);
            Vector3 t0 = onWall + inN * (WallT * 0.5f + 0.62f) + wallDir * 1.5f + Vector3.up * 0.15f;
            Vector3 t1 = t0 + wallDir * 2.3f + inN * 0.18f + Vector3.up * 0.08f;
            Zone23Dressing.Cyl(dr.transform, "SnappedPalmTrunk", t0, t1, 0.15f, bark);
            for (int i = 0; i < 4; i++)                                           // splintered end
            {
                Vector3 sp = t0 - wallDir * 0.12f + Quaternion.AngleAxis(i * 90f + 20f, wallDir) * (Vector3.up * 0.08f);
                Zone23Dressing.Box(dr.transform, "TrunkSplinter", sp, new Vector3(0.05f, 0.05f, 0.34f - i * 0.05f), bark, Quaternion.LookRotation(-wallDir + inN * (i - 1.5f) * 0.15f + Vector3.up * 0.1f));
            }
            p.InstantiatePropOrFallback("fern_02", dr.transform, onWall + inN * 0.75f + wallDir * 0.6f, new Vector3(0f, 70f, 0f), new Vector3(0.9f, 0.9f, 0.9f), grounded: true, addCollider: false, fallbackMat: p.FoliageDark);

            // hazard paint on the lane-facing gate posts and wall ends + the Paddock 4 plate
            Zone23Dressing.Decal(dr.transform, "Hazard_GatePostN", new Vector3(90.75f, 1.2f, -0.9f), Vector3.left, new Vector3(0.5f, 2.4f, 0.4f), p.DecalCautionStripes, 0.95f);
            Zone23Dressing.Decal(dr.transform, "Hazard_GatePostS", new Vector3(90.75f, 1.2f, -7.1f), Vector3.left, new Vector3(0.5f, 2.4f, 0.4f), p.DecalCautionStripes, 0.95f);
            Zone23Dressing.Decal(dr.transform, "Hazard_GateKerb", new Vector3(90.2f, 0.05f, -4.0f), Vector3.up, new Vector3(0.5f, 6.0f, 0.4f), p.DecalCautionStripes, 0.8f);
            Zone23Dressing.SignPlate(dr.transform, "Sign_Paddock4Gate", new Vector3(90.912f, 2.55f, -6.3f), Vector3.left, 1.15f, 0.9f, Zone23Dressing.SignMat(p, "MAT_Z23_SignPaddock4", "paddock4_gate"), p.DarkPaintedMetal);
            Zone23Dressing.SignPlate(dr.transform, "Sign_HighVoltage_P4", new Vector3(90.912f, 2.1f, -4.6f), Vector3.left, 0.55f, 0.75f, Zone23Dressing.SignMat(p, "MAT_Z23_SignHighVoltage", "high_voltage"), p.DarkPaintedMetal);
            {
                // flush on Wall_Seg5_N's lane face (plate + backing are 18 mm deep)
                Vector3 w0 = new Vector3(80.1f, 0f, 0.2f), w1 = new Vector3(91.0f, 0f, -0.9f);
                Vector3 wd = (w1 - w0).normalized, wn = new Vector3(-wd.z, 0f, wd.x);
                if (Vector3.Dot(new Vector3(85f, 0f, -3f) - (w0 + w1) * 0.5f, wn) < 0f) wn = -wn;
                Vector3 sp = w0 + wd * 0.9f + wn * (WallT * 0.5f + 0.018f) + Vector3.up * 3.1f;
                Zone23Dressing.SignPlate(dr.transform, "Sign_ServiceRoad3", sp, wn, 1.7f, 0.6f, Zone23Dressing.SignMat(p, "MAT_Z23_SignServiceRoad3", "service_road_3"), p.DarkPaintedMetal);
            }

            // edge clutter in the dead-end Branch 1 (no colliders)
            p.InstantiatePropOrFallback("pallet", dr.transform, new Vector3(41.4f, 0.02f, 23.8f), new Vector3(0f, 84f, 0f), new Vector3(1.2f, 1.2f, 1.2f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("oil_drum", dr.transform, new Vector3(41.5f, 0.15f, 23.6f), new Vector3(0f, 30f, 0f), new Vector3(0.9f, 0.9f, 0.9f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("jerry_can", dr.transform, new Vector3(38.6f, 0.02f, 24.6f), new Vector3(0f, 200f, 0f), new Vector3(0.47f, 0.47f, 0.47f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("debris_pile", dr.transform, new Vector3(65.2f, 0.02f, 21.2f), new Vector3(0f, 95f, 0f), new Vector3(1.6f, 1.6f, 1.6f), grounded: true, addCollider: false);

            // jungle beyond the two open branch ends (the Branch 1 fence and the open Branch 2 end)
            string[] beyond = { "banana_plant", "tree_fern", "monstera_clump", "palm_tree_b", "fern_02", "palm_tree", "tree_fern", "palm_tree" };
            float[] bsize = { 2.8f, 3.6f, 1.5f, 9f, 1.0f, 11f, 3.2f, 10f };
            for (int i = 0; i < 8; i++)
            {
                Zone23Dressing.Plant(p, dr.transform, beyond[i], new Vector3(37.6f + i * 0.7f + (i % 3) * 0.6f, 0f, 35.6f + (i % 4) * 1.6f), i * 53f, bsize[i], 0.02f, shadows: bsize[i] > 5f);
                Zone23Dressing.Plant(p, dr.transform, beyond[(i + 3) % 8], new Vector3(61.8f + i * 0.6f, 0f, 35.0f + (i % 3) * 2.1f), i * 71f, bsize[(i + 3) % 8], 0.02f, shadows: bsize[(i + 3) % 8] > 5f);
            }
        }

        /// <summary>
        /// Round 4 (review 3, frame 15: 'three clean pale wall faces', 65 % of the frame): one large form per face that
        /// reads from 10 m and in silhouette. Right wall: a stencilled steel pump-room door on a flood sill. Back wall: a
        /// PADDOCK 4 arrow sign on stand-offs under the fence. Left wall: ferns and vines spilling over the copings, with
        /// the jungle pressing on the fence. Across the lane: a trench drain. All collider-free.
        /// </summary>
        private static void DressCorner2Faces(Transform parent, WorldMaterialPalette p)
        {
            var root = new GameObject("ServicePaths_Corner2Faces");
            root.transform.SetParent(parent, false);
            root.layer = GameLayers.Environment;
            Transform t = root.transform;
            Material galv = Zone23Dressing.GalvSteel(p), paint = p.DarkPaintedMetal, voidM = Zone23Dressing.DrainVoid(p);
            Quaternion q = Quaternion.identity;

            // 1. right wall: Wall_Seg3_S lane face (z = 11.875), door centred at x = 59.6 (Zone23Dressing.DoorSpans seed 6)
            {
                float x = 59.6f, zf = 11.7f + WallT * 0.5f;
                const float sill = 0.34f, dh = 2.06f;
                float yc = sill + dh * 0.5f;
                Zone23Dressing.Box(t, "PumpDoor_Recess", new Vector3(x, yc + 0.01f, zf + 0.004f), new Vector3(1.04f, dh + 0.04f, 0.008f), voidM, q);
                Zone23Dressing.Box(t, "PumpDoor_JambL", new Vector3(x - 0.565f, yc + 0.05f, zf + 0.035f), new Vector3(0.09f, dh + 0.12f, 0.07f), paint, q);
                Zone23Dressing.Box(t, "PumpDoor_JambR", new Vector3(x + 0.565f, yc + 0.05f, zf + 0.035f), new Vector3(0.09f, dh + 0.12f, 0.07f), paint, q);
                Zone23Dressing.Box(t, "PumpDoor_Head", new Vector3(x, sill + dh + 0.06f, zf + 0.035f), new Vector3(1.22f, 0.11f, 0.07f), paint, q);
                Zone23Dressing.Box(t, "PumpDoor_Sill", new Vector3(x, sill + 0.01f, zf + 0.05f), new Vector3(1.22f, 0.02f, 0.1f), galv, q);
                Zone23Dressing.Box(t, "PumpDoor_Leaf", new Vector3(x, yc, zf + 0.03f), new Vector3(0.99f, dh - 0.02f, 0.045f), p.PaintedMetalGreen, q);
                // pressed stiffener panels, a louvred vent, kick plate, lever, hinges, hasp and padlock
                Zone23Dressing.Box(t, "PumpDoor_PanelTop", new Vector3(x, sill + 1.62f, zf + 0.056f), new Vector3(0.78f, 0.62f, 0.006f), p.PaintedMetalGreen, q);
                Zone23Dressing.Box(t, "PumpDoor_Vent", new Vector3(x, sill + 0.62f, zf + 0.054f), new Vector3(0.62f, 0.42f, 0.006f), voidM, q);
                for (int k = 0; k < 6; k++)
                    Zone23Dressing.Box(t, "PumpDoor_VentSlat", new Vector3(x, sill + 0.45f + k * 0.068f, zf + 0.062f), new Vector3(0.6f, 0.012f, 0.045f), p.PaintedMetalGreen, Quaternion.Euler(-38f, 0f, 0f));
                Zone23Dressing.Box(t, "PumpDoor_Kick", new Vector3(x, sill + 0.13f, zf + 0.055f), new Vector3(0.95f, 0.22f, 0.005f), p.BrushedSteel, q);
                Zone23Dressing.Cyl(t, "PumpDoor_Rose", new Vector3(x - 0.39f, sill + 0.98f, zf + 0.052f), new Vector3(x - 0.39f, sill + 0.98f, zf + 0.075f), 0.03f, p.BrushedSteel);
                Zone23Dressing.Box(t, "PumpDoor_Lever", new Vector3(x - 0.33f, sill + 0.98f, zf + 0.085f), new Vector3(0.14f, 0.024f, 0.024f), p.BrushedSteel, q);
                for (int k = 0; k < 3; k++)
                    Zone23Dressing.Box(t, "PumpDoor_Hinge", new Vector3(x + 0.505f, sill + 0.25f + k * 0.78f, zf + 0.06f), new Vector3(0.035f, 0.13f, 0.04f), galv, q);
                Zone23Dressing.Box(t, "PumpDoor_Hasp", new Vector3(x - 0.49f, sill + 1.22f, zf + 0.07f), new Vector3(0.07f, 0.09f, 0.025f), galv, q);
                Zone23Dressing.Box(t, "PumpDoor_Padlock", new Vector3(x - 0.49f, sill + 1.13f, zf + 0.09f), new Vector3(0.055f, 0.065f, 0.026f), Zone23Dressing.PaintYellow(p), q);
                Zone23Dressing.Decal(t, "PumpDoor_Stencil", new Vector3(x, sill + 1.63f, zf + 0.06f), Vector3.forward, new Vector3(0.86f, 0.43f, 0.12f),
                    Zone23Dressing.DecalMat(p, "MAT_Z23_DecalDoorStencil", "z23_door_stencil", null, new Color(0.9f, 0.9f, 0.86f, 0.85f)), 0.92f);
                Zone23Dressing.Decal(t, "PumpDoor_RustRun", new Vector3(x + 0.4f, sill + 0.9f, zf + 0.08f), Vector3.forward, new Vector3(0.5f, 1.7f, 0.2f), Zone23Dressing.RunoffStrong(p), 0.75f);
                Zone23Dressing.SignPlate(t, "Sign_PumpDoor_HV", new Vector3(x + 1.02f, 1.62f, zf + 0.02f), Vector3.forward, 0.42f, 0.58f,
                    Zone23Dressing.SignMat(p, "MAT_Z23_SignHighVoltage", "high_voltage"), paint);
            }

            // 2. back wall: Wall_Seg4_NE, low panel 0 (fence above): PADDOCK 4 arrow sign on four stand-offs
            {
                Vector3 d = new Vector3(13.8f, 0f, -16.1f).normalized;
                Vector3 n = new Vector3(-d.z, 0f, d.x); n = -n;                       // into the lane
                Vector3 c = new Vector3(66.3f, 0f, 16.3f) - d * 0.2f + d * 2.15f;
                const float sw = 1.9f, sh = 0.95f, yc = 1.3f, off = 0.095f;
                Zone23Dressing.SignPlate(t, "Sign_Paddock4Arrow", c + n * (WallT * 0.5f + off) + Vector3.up * yc, n, sw, sh,
                    Zone23Dressing.SignMat(p, "MAT_Z23_SignPaddock4Arrow", "paddock4_arrow"), paint);
                for (int k = 0; k < 4; k++)
                {
                    Vector3 o = d * ((k % 2 == 0 ? -1f : 1f) * (sw * 0.5f - 0.15f)) + Vector3.up * (yc + (k < 2 ? -1f : 1f) * (sh * 0.5f - 0.12f));
                    Zone23Dressing.Cyl(t, "SignStandoff", c + n * (WallT * 0.5f) + o, c + n * (WallT * 0.5f + off - 0.015f) + o, 0.018f, galv);
                }
                Zone23Dressing.Decal(t, "Sign_Paddock4Arrow_Drip", c + n * (WallT * 0.5f + 0.1f) + Vector3.up * (yc - sh * 0.5f - 0.35f), n, new Vector3(1.6f, 0.8f, 0.25f), Zone23Dressing.Grime(p, 2), 0.7f);
            }

            // 3. left wall: Wall_Seg3_N (lane face z = 16.125). Low panel 3 (x 54.0-57.9, cap at 2.32 m) and full panel 4.
            {
                float zf = 16.3f - WallT * 0.5f;
                float[] fx = { 54.5f, 55.35f, 56.3f, 57.3f };
                for (int k = 0; k < fx.Length; k++)
                {
                    Zone23Dressing.Plant(p, t, "fern_02", new Vector3(fx[k], 2.32f, zf + 0.06f), 40f + 77f * k, 0.85f + 0.12f * (k % 3), 0.03f, seatOnTerrain: false, shadows: false);
                    float vh = 1.15f + 0.25f * (k % 3);
                    Zone23Dressing.Plant(p, t, "hanging_vines", new Vector3(fx[k] + 0.3f, 2.42f - vh, zf - 0.08f), 90f, vh, 0.04f, seatOnTerrain: false, shadows: false).name = "VinesOverCap";
                }
                float[] vx = { 58.5f, 59.7f, 61.0f };
                for (int k = 0; k < vx.Length; k++)
                {
                    float vh = 2.6f + 0.35f * k;
                    Zone23Dressing.Plant(p, t, "hanging_vines", new Vector3(vx[k], WallH + 0.29f - vh, zf - 0.1f), 90f, vh, 0.04f, seatOnTerrain: false, shadows: false).name = "VinesOverCoping";
                    Zone23Dressing.Plant(p, t, "fern_02", new Vector3(vx[k] - 0.3f, WallH + 0.17f, 16.25f), 25f + 61f * k, 1.1f, 0.03f, seatOnTerrain: false, shadows: false);
                }
                // the jungle presses on the fence over the low panel
                Zone23Dressing.Plant(p, t, "tree_fern", new Vector3(55.2f, 0f, 17.7f), 30f, 3.6f, 0.03f, shadows: false);
                Zone23Dressing.Plant(p, t, "banana_plant", new Vector3(56.9f, 0f, 17.9f), 140f, 3.1f, 0.03f, shadows: false);
                Zone23Dressing.Plant(p, t, "monstera_clump", new Vector3(54.3f, 0f, 17.2f), 260f, 1.6f, 0.03f, shadows: false);
            }

            // 4. a trench drain across the lane at the corner (walk-over, no collider)
            {
                const float x = 58.6f, z0 = 12.36f, z1 = 15.64f;
                Vector3 c = new Vector3(x, 0.036f, (z0 + z1) * 0.5f);
                Zone23Dressing.Box(t, "TrenchDrain", c, new Vector3(0.3f, 0.012f, z1 - z0), voidM, q);
                Zone23Dressing.Box(t, "TrenchDrainEdge", c + new Vector3(-0.165f, 0.004f, 0f), new Vector3(0.03f, 0.014f, z1 - z0), galv, q);
                Zone23Dressing.Box(t, "TrenchDrainEdge", c + new Vector3(0.165f, 0.004f, 0f), new Vector3(0.03f, 0.014f, z1 - z0), galv, q);
                DecalProjector g = Zone23Dressing.Decal(t, "TrenchDrainGrating", c + Vector3.up * 0.04f, Vector3.up, new Vector3(0.3f, z1 - z0, 0.16f), Zone23Dressing.Grating(p), 1f);
                g.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                g.uvScale = new Vector2(1f, (z1 - z0) / 0.6f);
            }
            Zone23Dressing.MergeStatic(t);
        }

        /// <summary>Cuts a broken notch of the given width out of a PanelWall coping box where a world point lies.</summary>
        private static void NotchCoping(Transform coping, Vector3 at, float width, WorldMaterialPalette p)
        {
            if (coping == null) { Debug.LogWarning("[Zone2] coping to notch not found"); return; }
            Vector3 c = coping.position, f = coping.forward, size = coping.localScale;
            float s = Vector3.Dot(at - c, f), half = size.z * 0.5f;
            float a0 = -half, a1 = s - width * 0.5f, b0 = s + width * 0.5f, b1 = half;
            Material m = coping.GetComponent<MeshRenderer>().sharedMaterial;
            Transform parent = coping.parent;
            Quaternion r = coping.rotation;
            if (a1 - a0 > 0.05f) Zone23Dressing.Box(parent, "Coping", c + f * ((a0 + a1) * 0.5f), new Vector3(size.x, size.y, a1 - a0), m, r);
            if (b1 - b0 > 0.05f) Zone23Dressing.Box(parent, "Coping", c + f * ((b0 + b1) * 0.5f), new Vector3(size.x, size.y, b1 - b0), m, r);
            // broken stubs in the notch: the coping is cracked, not cut clean
            Zone23Dressing.Box(parent, "CopingBrokenStub", c + f * (a1 + 0.06f) - Vector3.up * 0.04f, new Vector3(size.x * 0.7f, size.y * 0.55f, 0.12f), m, r * Quaternion.Euler(0f, 0f, 9f));
            Zone23Dressing.Box(parent, "CopingBrokenStub", c + f * (b0 - 0.05f) - Vector3.up * 0.05f, new Vector3(size.x * 0.6f, size.y * 0.45f, 0.1f), m, r * Quaternion.Euler(0f, 0f, -14f));
            Object.DestroyImmediate(coping.gameObject);
        }

        /// <summary>Skid furrows, a flattened bollard light, a torn fence panel and spilled cans around the jeep.</summary>
        private static void DressJeepCrashSite(Transform parent, WorldMaterialPalette p, Vector3 c)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 k = c + new Vector3(-3.5f - i * 2.6f, 0f, 2.2f + i * 0.9f);
                k.y = Zone23Dressing.Ground(k.x, k.z) + 0.2f;
                Zone23Dressing.Decal(parent, $"SkidFurrow_{i}", k, Vector3.up, new Vector3(0.9f, 2.8f, 0.8f), Zone23Dressing.Grime(p, i), 0.75f, rollDeg: 70f);
            }
            Vector3 b = c + new Vector3(-2.8f, 0f, 2.6f); b.y = Zone23Dressing.GroundMin(b.x, b.z, 0.5f) - 0.03f;
            GameObject fb = p.InstantiatePropOrFallback("bollard_light", parent, b, new Vector3(84f, 30f, 0f), new Vector3(0.32f, 1.15f, 0.32f), grounded: true, addCollider: false, customName: "FlattenedBollard");
            if (fb != null) DressFlattenedBollard(parent, fb, p);
            Vector3 f = c + new Vector3(2.6f, 0f, -2.4f); f.y = Zone23Dressing.Ground(f.x, f.z) + 0.05f;
            Zone23Dressing.Box(parent, "TornFencePanel", f + Vector3.up * 0.05f, new Vector3(3.2f, 0.04f, 2.0f), p.ChainlinkFence, Quaternion.Euler(4f, 28f, 7f));
            Zone23Dressing.Box(parent, "TornFenceRail", f + new Vector3(0f, 0.12f, 0.9f), new Vector3(3.4f, 0.06f, 0.06f), p.DarkPaintedMetal, Quaternion.Euler(0f, 28f, 6f));
            for (int i = 0; i < 2; i++)
            {
                Vector3 j = c + new Vector3(-1.6f + i * 0.5f, 0f, -1.8f - i * 0.3f); j.y = Zone23Dressing.Ground(j.x, j.z);
                p.InstantiatePropOrFallback("jerry_can", parent, j, new Vector3(i * 90f, 40f + i * 70f, 0f), new Vector3(0.47f, 0.47f, 0.47f), grounded: true, addCollider: false);
            }
            Zone23Dressing.Plant(p, parent, "shrub_02", c + new Vector3(4.5f, 0f, 3.5f), 20f, 2.0f);
            Zone23Dressing.Plant(p, parent, "banana_plant", c + new Vector3(5.6f, 0f, -3.0f), 140f, 2.8f);
        }

        /// <summary>Round 4 (review 3: 'a plain black capped cylinder that reads as a giant torch'): the knocked-down bollard
        /// gets its torn-off base flange, a yellow reflective band, a smashed lens with shards in the gravel, the sheared
        /// footing it was ripped from and the cable still running to it.</summary>
        private static void DressFlattenedBollard(Transform parent, GameObject fb, WorldMaterialPalette p)
        {
            var rs = fb.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds bb = rs[0].bounds;
            foreach (Renderer r in rs) bb.Encapsulate(r.bounds);
            Vector3 axis = fb.transform.up;                                       // the post's own axis (lying almost flat)
            float half = Mathf.Abs(Vector3.Dot(bb.extents, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z))));
            float rad = Mathf.Max(0.06f, bb.size.y * 0.5f);
            Vector3 ctr = bb.center, baseEnd = ctr - axis * half, topEnd = ctr + axis * half;
            var g = new GameObject("FlattenedBollard_Damage");
            g.transform.SetParent(parent, false);
            g.layer = GameLayers.Environment;
            Transform t = g.transform;
            Material galv = Zone23Dressing.GalvSteel(p), rubber = Zone23Dressing.Rubber(p), voidM = Zone23Dressing.DrainVoid(p);
            Quaternion along = Quaternion.LookRotation(axis, Vector3.up);
            // base flange with four bolt holes torn out
            Zone23Dressing.Box(t, "Flange", baseEnd - axis * 0.01f, new Vector3(rad * 2.6f, rad * 2.6f, 0.025f), galv, along);
            for (int k = 0; k < 4; k++)
            {
                Vector3 o = along * new Vector3((k % 2 == 0 ? -1f : 1f) * rad * 0.95f, (k < 2 ? -1f : 1f) * rad * 0.95f, -0.02f);
                Zone23Dressing.Box(t, "FlangeHole", baseEnd + o, new Vector3(0.03f, 0.03f, 0.01f), voidM, along);
            }
            // yellow reflective band near the head, and the smashed lens: a dark hole with a jagged rim and shards
            Zone23Dressing.Cyl(t, "ReflectiveBand", topEnd - axis * (half * 0.32f), topEnd - axis * (half * 0.32f + 0.09f), rad + 0.008f, Zone23Dressing.PaintYellow(p));
            Zone23Dressing.Cyl(t, "LensHole", topEnd - axis * 0.06f, topEnd + axis * 0.005f, rad * 0.8f, voidM);
            for (int k = 0; k < 5; k++)
            {
                float ang = k * 72f + 15f;
                Vector3 rim = topEnd + along * (Quaternion.Euler(0f, 0f, ang) * new Vector3(rad * 0.82f, 0f, 0.015f));
                Zone23Dressing.Box(t, "LensShardRim", rim, new Vector3(0.05f, 0.025f, 0.035f), Zone23Dressing.ReflectorWhite(p), along * Quaternion.Euler(0f, 0f, ang + 30f));
                Vector3 sp = topEnd + axis * (0.25f + 0.18f * k) + Vector3.Cross(axis, Vector3.up).normalized * (0.35f * ((k % 3) - 1));
                sp.y = Zone23Dressing.Ground(sp.x, sp.z) + 0.012f;
                Zone23Dressing.Box(t, "LensShard", sp, new Vector3(0.07f - k * 0.008f, 0.008f, 0.05f), Zone23Dressing.ReflectorWhite(p), Quaternion.Euler(0f, k * 47f, 0f));
            }
            // the footing it was ripped from: a concrete pad with sheared bolts, and the cable from the pad to the flange
            Vector3 flat = new Vector3(axis.x, 0f, axis.z).normalized;
            Vector3 foot = baseEnd - flat * 0.75f;
            foot.y = Zone23Dressing.Ground(foot.x, foot.z);
            Zone23Dressing.Box(t, "FootingPad", foot + Vector3.up * 0.04f, new Vector3(0.5f, 0.12f, 0.5f), Zone23Dressing.Kerb(p), Quaternion.Euler(0f, 30f, 0f));
            for (int k = 0; k < 4; k++)
            {
                Vector3 bo = foot + Quaternion.Euler(0f, 30f + k * 90f, 0f) * new Vector3(0.15f, 0f, 0.15f);
                Zone23Dressing.Cyl(t, "ShearedBolt", bo + Vector3.up * 0.09f, bo + Vector3.up * (0.13f + 0.02f * (k % 2)), 0.011f, galv);
            }
            Vector3 c0 = foot + Vector3.up * 0.1f, c2 = baseEnd + Vector3.up * 0.02f;
            Vector3 c1 = Vector3.Lerp(c0, c2, 0.5f) + Vector3.Cross(flat, Vector3.up) * 0.22f; c1.y = Zone23Dressing.Ground(c1.x, c1.z) + 0.02f;
            Zone23Dressing.Cyl(t, "TornCable", c0, c1, 0.014f, rubber);
            Zone23Dressing.Cyl(t, "TornCable", c1, c2, 0.014f, rubber);
            Zone23Dressing.Cyl(t, "CableCore", c0, c0 + (c0 - c1).normalized * 0.06f + Vector3.up * 0.04f, 0.006f, Zone23Dressing.PaintYellow(p));
            Zone23Dressing.Decal(t, "BollardScrape", Vector3.Lerp(foot, ctr, 0.5f) + Vector3.up * 0.2f, Vector3.up, new Vector3(0.7f, 2.2f, 0.6f), Zone23Dressing.Grime(p, 0), 0.8f,
                rollDeg: Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg);
            Zone23Dressing.MergeStatic(t);
        }

        /// <summary>Battery bulkheads (VCExit group: alive in the blackout) at the two darkest stretches of the paths.</summary>
        private static void BuildPathBatteryBulkheads(Transform parent)
        {
            WorldArchitectureKit.BuildHdrpLight(parent, "Light_PathsCornerBulkhead_Batt", new Vector3(37.7f + WallT * 0.5f + 0.25f, 3.5f, 12.6f), new Vector3(50f, 90f, 0f), LightType.Spot, new Color(1f, 0.72f, 0.42f), 300f, 10f, LightGroupId.VCExit, volumetricDimmer: 0.8f, spotOuterAngle: 120f);
            Vector3 a = new Vector3(61.5f, 0f, 11.7f), b = new Vector3(75.9f, 0f, -4.4f);
            Vector3 d = (b - a).normalized; Vector3 n = new Vector3(d.z, 0f, -d.x);                      // towards the lane (NE)
            if (Vector3.Dot(new Vector3(71f, 0f, 6f) - (a + b) * 0.5f, n) < 0f) n = -n;
            Vector3 pos = Vector3.Lerp(a, b, 0.52f) + n * (WallT * 0.5f + 0.25f) + Vector3.up * 3.5f;
            // the right-hand (east) wall of the north run was a black slab from the path start (review 1, 14)
            WorldArchitectureKit.BuildHdrpLight(parent, "Light_PathsEastBulkhead_Batt", new Vector3(42.3f - WallT * 0.5f - 0.25f, 3.3f, 7.5f), new Vector3(55f, -90f, 0f), LightType.Spot, new Color(1f, 0.72f, 0.42f), 300f, 10f, LightGroupId.VCExit, volumetricDimmer: 0.8f, spotOuterAngle: 120f);
            WorldArchitectureKit.BuildHdrpLight(parent, "Light_PathsDiagBulkhead_Batt", pos, Quaternion.LookRotation(n * 0.65f + Vector3.down * 0.76f).eulerAngles, LightType.Spot, new Color(1f, 0.72f, 0.42f), 300f, 10f, LightGroupId.VCExit, volumetricDimmer: 0.8f, spotOuterAngle: 120f);
        }

        private static ParticleSystem BuildPathLampSparksVfx(Transform parent, Vector3 worldPos, WorldMaterialPalette p)
        {
            var go = new GameObject("PathLampSparksVfx");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Emit downward from broken lamp head

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 2.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 5.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.0f, 0.84f, 0.38f, 1f), new Color(1.0f, 0.52f, 0.14f, 1f));
            main.gravityModifier = 1.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // Triggered in bursts by StoryDirector.UpdateElectricalSparks()

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 38f;
            shape.radius = 0.12f;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.renderMode = ParticleSystemRenderMode.Stretch;
                psr.velocityScale = 0.08f;
                psr.lengthScale = 1.6f;
                psr.sharedMaterial = p.EmissiveAmber;
            }

            // Register directly on PathLampSparksVfx so StoryDirector can resolve it via WorldRegistry or GameObject.Find!
            WorldAnchor wa = go.AddComponent<WorldAnchor>();
            var so = new SerializedObject(wa);
            so.FindProperty("anchorId").stringValue = "PathLampSparksVfx";
            so.FindProperty("registerAsNamedObject").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            return ps;
        }

        private static void BuildRoadSegment(Transform parent, string name, Vector3 a, Vector3 b, float width, WorldMaterialPalette p, float topOffset = 0f)
        {
            Vector3 delta = b - a;
            delta.y = 0f;
            float len = delta.magnitude;
            if (len < 0.1f) return;

            var seg = new GameObject(name);
            seg.transform.SetParent(parent, false);
            seg.transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(delta / len, Vector3.up));

            GameObject slab = WorldArchitectureKit.CreateBox(seg.transform, "Asphalt", new Vector3(0f, -0.15f + topOffset, 0f), new Vector3(width, 0.30f, len), p.AsphaltWet, addCollider: true);
            WorldArchitectureKit.ApplySurface(slab, SurfaceType.Concrete, p);
        }

    }

    /// <summary>
    /// Shared dressing helpers for Zones 2 + 3 (look pass): precast panel walls with joints, copings, pilasters,
    /// kerbs and drains; wall decals; LOD'd hero plants seated on the terrain; plain bolted sign plates; kit-built
    /// yard clutter. Everything here is collider-free unless a parameter says otherwise, so the walkable route and
    /// the NavMesh stay exactly as the gameplay builders made them.
    /// </summary>
    internal static partial class Zone23Dressing
    {
        private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        private static readonly Dictionary<string, bool> ModelExists = new Dictionary<string, bool>();

        public static float Ground(float x, float z) => Zone4_TerrainJungleAndDawnCoastBuilder.EvaluateWorldElevation(x, z);

        /// <summary>Lowest terrain height under a square footprint (so a prop never floats on a slope).</summary>
        public static float GroundMin(float x, float z, float halfSize)
        {
            float y = Ground(x, z);
            y = Mathf.Min(y, Ground(x - halfSize, z - halfSize));
            y = Mathf.Min(y, Ground(x + halfSize, z - halfSize));
            y = Mathf.Min(y, Ground(x - halfSize, z + halfSize));
            y = Mathf.Min(y, Ground(x + halfSize, z + halfSize));
            return y;
        }

        // ------------------------------------------------------------------ materials
        public static Material PanelA(WorldMaterialPalette p) => Surf(p, "MAT_Z23_PanelConcreteA", "concrete_wall", "BaseColor", "MaskMap", 3.2f, new Color(0.95f, 0.94f, 0.92f), 0.18f, 0.80f);   // round 4: was 1.10 (pale walls in 14-16)   // fix round: A/B/C pushed ~15 % apart
        public static Material PanelB(WorldMaterialPalette p) => Surf(p, "MAT_Z23_PanelConcreteB", "concrete_wall", "BaseColor", "MaskMap", 2.9f, new Color(0.63f, 0.645f, 0.64f), 0.22f, 0.84f);
        public static Material PanelC(WorldMaterialPalette p) => Surf(p, "MAT_Z23_PanelConcreteC", "concrete_cracked_ext", "BaseColor", "MaskMap", 3.5f, new Color(1.0f, 1.03f, 1.04f), 0.25f, 0.86f);   // round 4: was 1.42-1.48 (the pale cream faces of 15)
        public static Material Coping(WorldMaterialPalette p) => Surf(p, "MAT_Z23_Coping", "concrete_polished", "BaseColor", "MaskMap", 2.0f, new Color(1.15f, 1.15f, 1.15f), 0.20f, 0.70f);   // round 4: was 1.75 (a white stripe on every wall top)
        public static Material Plinth(WorldMaterialPalette p) => Surf(p, "MAT_Z23_PlinthConcrete", "concrete_wall_boardform", "BaseColor", "MaskMap", 2.4f, new Color(0.85f, 0.85f, 0.83f), 0.30f, 0.88f);
        public static Material Kerb(WorldMaterialPalette p) => Surf(p, "MAT_Z23_KerbConcrete", "concrete_polished", "BaseColor", "MaskMap", 1.6f, new Color(1.6f, 1.6f, 1.6f), 0.30f, 0.80f);
        public static Material Porcelain(WorldMaterialPalette p) => Surf(p, "MAT_Z23_InsulatorPorcelain", "tiles_white", "BaseColor", "MaskMap", 0.6f, new Color(0.52f, 0.30f, 0.18f), 0.70f, 0.95f);
        public static Material GalvSteel(WorldMaterialPalette p) => Surf(p, "MAT_Z23_GalvanisedSteel", "steel_brushed", "BaseColor", "MaskMap", 1.2f, new Color(0.72f, 0.74f, 0.76f), 0.30f, 0.70f, 1f);
        public static Material TransformerGrey(WorldMaterialPalette p) => Surf(p, "MAT_Z23_TransformerPaint", "metal_painted_worn", "grey_BaseColor", "MaskMap", 1.4f, new Color(0.62f, 0.68f, 0.64f), 0.30f, 0.80f, 0.3f);
        public static Material ContainerBlue(WorldMaterialPalette p) => Surf(p, "MAT_Z23_ContainerBlue", "metal_painted_worn", "grey_BaseColor", "MaskMap", 2f, new Color(0.55f, 0.75f, 1.15f), 0.15f, 0.7f, 0.6f);
        public static Material ContainerRed(WorldMaterialPalette p) => Surf(p, "MAT_Z23_ContainerRed", "metal_corrugated_rusty", "BaseColor", "MaskMap", 2.0f, new Color(1.25f, 0.85f, 0.78f), 0.15f, 0.70f, 0.6f);
        public static Material ContainerGreen(WorldMaterialPalette p) => Surf(p, "MAT_Z23_ContainerGreen", "metal_painted_worn", "green_BaseColor", "MaskMap", 2f, new Color(1f, 1f, 1f), 0.15f, 0.7f, 0.6f);
        // concrete_cracked_ext is brown (linear 0.079/0.058/0.037): this tint neutralises it to a ~0.12 grey, and two
        // bay variants (darker / lighter) break the slab into cast panels
        public static Material SlabConcrete(WorldMaterialPalette p) => Surf(p, "MAT_Z23_YardConcrete", "concrete_cracked_ext", "BaseColor", "MaskMap", 4.6f, new Color(1.21f, 1.39f, 1.71f), 0.28f, 0.88f);
        public static Material SlabConcreteDark(WorldMaterialPalette p) => Surf(p, "MAT_Z23_YardConcreteDark", "concrete_cracked_ext", "BaseColor", "MaskMap", 3.9f, new Color(1.1f, 1.27f, 1.56f), 0.30f, 0.90f);
        public static Material SlabConcreteLight(WorldMaterialPalette p) => Surf(p, "MAT_Z23_YardConcreteLight", "concrete_cracked_ext", "BaseColor", "MaskMap", 4.2f, new Color(1.31f, 1.5f, 1.85f), 0.26f, 0.86f);
        // round 3: the 3 mm cast-bay overlays on the yard slab get their own "Paving" materials. They are merged, and the
        // wetness driver only treats merged meshes as floor when the material name is ground-only (review 2, 23: the
        // light bay at the yard entry stayed matte and dry next to the wet asphalt). Same look as the slab tones.
        public static Material SlabPavingDark(WorldMaterialPalette p) => Surf(p, "MAT_Z23_YardPavingDark", "concrete_cracked_ext", "BaseColor", "MaskMap", 3.9f, new Color(1.1f, 1.27f, 1.56f), 0.30f, 0.90f);
        public static Material SlabPavingLight(WorldMaterialPalette p) => Surf(p, "MAT_Z23_YardPavingLight", "concrete_cracked_ext", "BaseColor", "MaskMap", 4.2f, new Color(1.31f, 1.5f, 1.85f), 0.26f, 0.86f);
        public static Material Bark(WorldMaterialPalette p) => Surf(p, "MAT_Z23_PalmBark", "bark_tropical", "BaseColor", "MaskMap", 1.2f, new Color(0.95f, 0.92f, 0.88f), 0.10f, 0.50f);
        public static Material DrainVoid(WorldMaterialPalette p) => Surf(p, "MAT_Z23_DrainVoid", "asphalt_wet", "BaseColor_Wet", "MaskMap_Wet", 1.0f, new Color(0.16f, 0.16f, 0.16f), 0.25f, 0.45f);

        private static Material Surf(WorldMaterialPalette p, string name, string set, string baseVar, string maskVar, float tile, Color tint, float sMin, float sMax, float metal = 0f)
        {
            if (Mats.TryGetValue(name, out Material m) && m != null) return m;
            m = p.Surface(name, set, baseVar, maskVar, tile, tint, sMin, sMax, metal, 1.0f);
            Mats[name] = m;
            return m;
        }

        public static Material DecalMat(WorldMaterialPalette p, string name, string albedo, string normal, Color fallback)
        {
            if (Mats.TryGetValue(name, out Material m) && m != null) return m;
            m = p.CreateDecalMaterial(name, albedo, normal, fallback);
            Mats[name] = m;
            return m;
        }

        public static Material Grime(WorldMaterialPalette p, int i)
        {
            switch (i % 3)
            {
                case 0: return p.DecalGrimeLeak;
                case 1: return DecalMat(p, "MAT_Z23_DecalGrime2", "grime_leak_2", null, new Color(0.12f, 0.13f, 0.11f, 0.65f));
                default: return DecalMat(p, "MAT_Z23_DecalGrime3", "grime_leak_3", null, new Color(0.12f, 0.13f, 0.11f, 0.65f));
            }
        }
        /// <summary>Fix round: the palette decals run at 50 % global opacity on textures whose alpha averages 14-24 %, so
        /// a streak or algae line on a lit wall was ~5 % dark (sub-visible). These two use copies of grime_leak_3 and
        /// moss_patch with the alpha boosted (z23_*_strong.png, derived from the originals) at full opacity.</summary>
        public static Material RunoffStrong(WorldMaterialPalette p) => StrongDecal(p, "MAT_Z23_DecalRunoff", "z23_runoff_strong", null);
        public static Material AlgaeStrong(WorldMaterialPalette p) => StrongDecal(p, "MAT_Z23_DecalAlgaeLine", "z23_algae_strong", "moss_patch_normal");
        private static Material StrongDecal(WorldMaterialPalette p, string name, string stem, string normal)
        {
            bool fresh = !Mats.ContainsKey(name);
            Material m = DecalMat(p, name, stem, normal, new Color(0.1f, 0.11f, 0.09f, 0.8f));
            if (fresh && m != null && m.HasProperty("_DecalBlend")) { m.SetFloat("_DecalBlend", 1f); EditorUtility.SetDirty(m); }
            return m;
        }
        public static Material Moss(WorldMaterialPalette p) => DecalMat(p, "MAT_Z23_DecalMoss", "moss_patch", "moss_patch_normal", new Color(0.2f, 0.32f, 0.12f, 0.8f));
        public static Material Grating(WorldMaterialPalette p) => DecalMat(p, "MAT_Z23_DecalGrating", "grating_albedo", "grating_normal", new Color(0.2f, 0.2f, 0.2f, 1f));
        public static Material Cracks2(WorldMaterialPalette p) => DecalMat(p, "MAT_Z23_DecalCracks2", "cracks_2", "cracks_2_normal", new Color(0.16f, 0.16f, 0.16f, 0.78f));
        public static Material RaptorPrint(WorldMaterialPalette p) => DecalMat(p, "MAT_Z23_DecalRaptorPrint", "footprint_raptor_albedo", "footprint_raptor_normal", new Color(0.1f, 0.08f, 0.06f, 0.8f));

        public static Material SignMat(WorldMaterialPalette p, string name, string stem, bool emissive = false, Color? tint = null)
        {
            if (Mats.TryGetValue(name, out Material m) && m != null) return m;
            m = p.CreateTextureQuadMaterial(name, stem, emissive, 1.5f, tint ?? new Color(0.8f, 0.8f, 0.8f));
            Mats[name] = m;
            return m;
        }

        // ------------------------------------------------------------------ primitives
        public static GameObject Box(Transform parent, string name, Vector3 worldPos, Vector3 size, Material mat, Quaternion rot, bool collider = false)
        {
            GameObject go = WorldArchitectureKit.CreateBox(parent, name, Vector3.zero, size, mat, addCollider: collider);
            go.transform.SetPositionAndRotation(worldPos, rot);
            return go;
        }

        public static GameObject Cyl(Transform parent, string name, Vector3 a, Vector3 b, float radius, Material mat)
        {
            Vector3 d = b - a; float len = d.magnitude;
            if (len < 0.005f) return null;
            GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            c.name = name;
            c.transform.SetParent(parent, false);
            c.transform.position = (a + b) * 0.5f;
            c.transform.up = d / len;
            c.transform.localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
            Object.DestroyImmediate(c.GetComponent<Collider>());
            c.GetComponent<MeshRenderer>().sharedMaterial = mat;
            c.layer = GameLayers.Environment;
            return c;
        }

        /// <summary>A decal projecting INTO a surface whose outward normal is n (box centred on worldPos).</summary>
        /// <summary>Weathering decals (grime, moss, cracks, stains) fade out at 35 m: ~500 of them line the route.</summary>
        public static DecalProjector Decal(Transform parent, string name, Vector3 worldPos, Vector3 n, Vector3 size, Material mat, float fade = 0.9f, float rollDeg = 0f, float drawDistance = 35f)
        {
            Quaternion rot = Quaternion.LookRotation(-n, Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up) * Quaternion.Euler(0f, 0f, rollDeg);
            DecalProjector dp = WorldArchitectureKit.BuildDecal(parent, name, worldPos, rot.eulerAngles, size, mat, fade);
            dp.pivot = Vector3.zero;
            dp.drawDistance = drawDistance;
            return dp;
        }

        /// <summary>A triangular prism (gable infill): base from z0 to z1 at y = 0, apex at (apexZ, h), thickness t along x.
        /// Triplanar materials need no UVs.</summary>
        public static GameObject Prism(Transform parent, string name, Vector3 worldBaseCentre, float t, float z0, float z1, float apexZ, float h, Material mat)
        {
            float x = t * 0.5f;
            var v = new[]
            {
                new Vector3(-x, 0f, z0), new Vector3(-x, 0f, z1), new Vector3(-x, h, apexZ),
                new Vector3(x, 0f, z0), new Vector3(x, 0f, z1), new Vector3(x, h, apexZ),
            };
            // flat-shaded: duplicate vertices per face so the normals stay crisp
            int[][] faces = { new[] { 0, 2, 1 }, new[] { 3, 4, 5 }, new[] { 0, 1, 4, 3 }, new[] { 1, 2, 5, 4 }, new[] { 2, 0, 3, 5 } };
            var verts = new List<Vector3>(); var tris = new List<int>();
            foreach (int[] f in faces)
            {
                int b = verts.Count;
                foreach (int k in f) verts.Add(v[k]);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);                  // clockwise seen from outside
                if (f.Length == 4) { tris.Add(b); tris.Add(b + 3); tris.Add(b + 2); }
            }
            var mesh = new Mesh { name = "Z23_Prism" };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldBaseCentre;
            go.layer = GameLayers.Environment;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>A plain bolted sign plate (not lit) whose readable face looks along faceDir.</summary>
        public static GameObject SignPlate(Transform parent, string name, Vector3 worldPos, Vector3 faceDir, float w, float h, Material mat, Material backMat = null)
        {
            faceDir.y = 0f; faceDir.Normalize();
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(worldPos, Quaternion.LookRotation(faceDir, Vector3.up));   // the cube's +Z face reads unmirrored
            GameObject plate = WorldArchitectureKit.CreateBox(root.transform, "Face", Vector3.zero, new Vector3(w, h, 0.012f), mat, addCollider: false);
            if (backMat != null)
                WorldArchitectureKit.CreateBox(root.transform, "Backing", new Vector3(0f, 0f, -0.012f), new Vector3(w + 0.04f, h + 0.04f, 0.012f), backMat, addCollider: false);
            plate.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
            return root;
        }

        // ------------------------------------------------------------------ plants (LOD'd, seated, no collider)
        private static bool HasModel(string stem)
        {
            if (ModelExists.TryGetValue(stem, out bool ok)) return ok;
            ok = false;
            foreach (string g in AssetDatabase.FindAssets(stem + " t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == stem) { ok = true; break; }
            }
            ModelExists[stem] = ok;
            return ok;
        }

        /// <summary>Plant/prop with LOD0/LOD1/LOD2 when the _LOD files exist; y &lt; -500 = seat on the terrain.</summary>
        public static GameObject Plant(WorldMaterialPalette p, Transform parent, string stem, Vector3 pos, float yaw, float size, float cullHeight = 0.02f, bool seatOnTerrain = true, Vector3? tilt = null, bool collider = false, bool shadows = true)
        {
            if (seatOnTerrain) pos.y = Ground(pos.x, pos.z) - 0.04f;
            Vector3 euler = new Vector3(tilt?.x ?? 0f, yaw, tilt?.z ?? 0f);
            var root = new GameObject($"Z23_{stem}");
            root.transform.SetParent(parent, false);
            var lods = new List<LOD>();
            // Budget: the route is lined with ~1500 plants, so away from the lanes the decimated _LOD1 mesh (40 % of the
            // triangles) is the top level, _LOD2 the far level, then culled. Within 12 m of a service-path lane the
            // full mesh is LOD0 down to 35 % of the screen (hero plants seen at 2 m are never decimated).
            // Understory and weeds cast no shadows; neither does the last level.
            bool hasL1 = HasModel(stem + "_LOD1");
            bool near = hasL1 && PathLaneDistance(pos) < 12f;
            string[] variants = !hasL1 ? new[] { stem } : (near ? new[] { stem, stem + "_LOD1", stem + "_LOD2" } : new[] { stem + "_LOD1", stem + "_LOD2" });
            float[] heights = near ? new[] { 0.35f, 0.12f, cullHeight } : new[] { 0.14f, cullHeight };
            for (int i = 0; i < variants.Length; i++)
            {
                if (i > 0 && !HasModel(variants[i])) continue;
                GameObject inst = p.InstantiatePropOrFallback(variants[i], root.transform, pos, euler, new Vector3(size, size, size), grounded: true, addCollider: collider && i == 0, fallbackMat: p.FoliageDark);
                Renderer[] rs = inst.GetComponentsInChildren<Renderer>();
                if (!shadows || (i > 0 && i == variants.Length - 1))
                    foreach (Renderer r in rs) r.shadowCastingMode = ShadowCastingMode.Off;
                lods.Add(new LOD(heights[i], rs));
            }
            if (lods.Count >= 1)
            {
                // the last LOD culls at cullHeight
                LOD last = lods[lods.Count - 1]; last.screenRelativeTransitionHeight = cullHeight; lods[lods.Count - 1] = last;
                for (int i = 0; i < lods.Count - 1; i++)
                {
                    LOD l = lods[i];
                    l.screenRelativeTransitionHeight = Mathf.Max(l.screenRelativeTransitionHeight, lods[i + 1].screenRelativeTransitionHeight + 0.01f);
                    lods[i] = l;
                }
                LODGroup lg = root.AddComponent<LODGroup>();
                lg.SetLODs(lods.ToArray());
                lg.RecalculateBounds();
            }
            return root;
        }

        /// <summary>lamp_post FBX with a pole-only collider (the automatic bounds box spanned the 2.3 m arms and put an
        /// invisible wall across half the lane under every lamp).</summary>
        public static GameObject LampPost(WorldMaterialPalette p, Transform parent, Vector3 pos, float yaw)
        {
            GameObject lamp = p.InstantiatePropOrFallback("lamp_post", parent, pos, new Vector3(0f, yaw, 0f), new Vector3(0.85f, 4.4f, 0.85f), grounded: true, addCollider: false);
            var col = new GameObject("PoleCollider");
            col.transform.SetParent(lamp.transform.parent, false);
            col.transform.SetPositionAndRotation(new Vector3(pos.x, pos.y + 2.2f, pos.z), Quaternion.Euler(0f, yaw, 0f));
            col.layer = GameLayers.Environment;
            col.AddComponent<BoxCollider>().size = new Vector3(0.3f, 4.4f, 0.3f);
            return lamp;
        }

        private static readonly Vector2[][] PathLanes =
        {
            new[] { new Vector2(21.5f, -9f), new Vector2(40f, -9f), new Vector2(40f, 14f), new Vector2(64f, 14f), new Vector2(78f, -2f), new Vector2(91f, -4f) },
            new[] { new Vector2(40f, 14f), new Vector2(40f, 34f) },
            new[] { new Vector2(64f, 14f), new Vector2(64f, 34f) },
        };

        /// <summary>Distance from the service-path lane centre lines (main route + both branches).</summary>
        public static float PathLaneDistance(Vector3 pt)
        {
            Vector2 q = new Vector2(pt.x, pt.z);
            float best = float.MaxValue;
            foreach (Vector2[] line in PathLanes)
                for (int i = 0; i < line.Length - 1; i++)
                {
                    Vector2 ab = line[i + 1] - line[i];
                    float t = Mathf.Clamp01(Vector2.Dot(q - line[i], ab) / ab.sqrMagnitude);
                    best = Mathf.Min(best, Vector2.Distance(q, line[i] + ab * t));
                }
            return best;
        }

        private static float Hash(float a, float b) => Mathf.Repeat(Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f, 1f);

        // ------------------------------------------------------------------ precast panel wall
        public struct WallStyle
        {
            public bool Pilasters;      // on the outside face every 2 panels
            public bool Kerb;           // kerb + drain on the inside foot
            public bool Grime;          // streaks under the coping (inside)
            public bool Moss;           // moss at the foot (both faces)
            public bool Vines;          // hanging vines on the inside coping
            public bool Jungle;         // hero plants against the outside face
            public bool LaneKeepOut;    // never put a plant where it could reach into a service-path lane
            public float PanelLen;
            public bool Detail;         // round 3: tie holes, conduits, trays, downpipes, signs, ferns, fence-topped low panels
            public static WallStyle Path => new WallStyle { Pilasters = true, Kerb = true, Grime = true, Moss = true, Vines = true, Jungle = true, LaneKeepOut = true, PanelLen = 4f, Detail = true };
        }

        // ------------------------------------------------------------------ round 3: path wall detail
        private const float LowPanelH = 2.2f;

        /// <summary>Wall-mounted things (the three battery bulkheads, the blood smear): the panels there stay full height.</summary>
        private static readonly Vector2[] KeepFullAt = { new(37.95f, 12.6f), new(42.05f, 7.5f), new(69.0f, 3.3f), new(61.2f, 11.9f) };

        /// <summary>Low (fence-topped) panels per wall seed, picked so every route view of the paths (14, 15, 16 and the
        /// diagonal) has an opening to the jungle. Seed 1 (dock approach dressing) stays closed; on seed 7 only the end panel at the Branch 2 mouth, away from the fallen palm.</summary>
        private static readonly Dictionary<int, int[]> LowPanelTable = new Dictionary<int, int[]>
        {
            { 2, new[] { 2, 3 } }, { 3, new[] { 1, 3 } }, { 4, new[] { 1, 2 } }, { 5, new[] { 1, 3 } },
            { 6, new[] { 1, 3 } }, { 7, new[] { 0 } }, { 8, new[] { 1, 3 } }, { 9, new[] { 1 } }, { 10, new[] { 1, 2 } },
        };

        /// <summary>Round 4: steel doors cut into a path wall (seed -> along-wall span in metres from the extended start).
        /// The wall detail keeps that span clear and the conduit hops over the door head.</summary>
        internal static readonly Dictionary<int, Vector2> DoorSpans = new Dictionary<int, Vector2> { { 6, new Vector2(16.75f, 18.25f) } };

        private static bool InDoor(int seed, float s, float pad = 0f) =>
            DoorSpans.TryGetValue(seed, out Vector2 ds) && s > ds.x - pad && s < ds.y + pad;

        private static bool NearKeepFull(Vector3 c, float pl)
        {
            foreach (Vector2 q in KeepFullAt)
                if (new Vector2(c.x - q.x, c.z - q.y).magnitude < pl * 0.5f + 0.5f) return true;
            return false;
        }

        private static bool[] LowPanels(string name, Vector3 a, Vector3 d, float pl, int count, float height, WallStyle st, int seed)
        {
            var low = new bool[count];
            if (!st.Detail || height < LowPanelH + 1.5f || !LowPanelTable.TryGetValue(seed, out int[] idx)) return low;
            foreach (int i in idx)
            {
                if (i < 0 || i >= count) continue;
                Vector3 c = a + d * ((i + 0.5f) * pl);
                bool keep = false;
                foreach (Vector2 q in KeepFullAt)
                    if (new Vector2(c.x - q.x, c.z - q.y).magnitude < pl * 0.5f + 0.5f) { keep = true; break; }
                if (!keep) low[i] = true;
            }
            return low;
        }

        /// <summary>The fence over a low panel: a cap on the low panel, a chain-link plane in a galvanised tube frame up to the
        /// wall height, three strands of barbed wire on top. Collider-free (the wall's own collider stays full height).</summary>
        private static void LowPanelFence(Transform parent, Vector3 baseMid, Vector3 d, Quaternion rot, float pl, float thick, float baseY, float lowH, float height, WorldMaterialPalette p, int i)
        {
            Box(parent, $"CopingLow_{i}", baseMid + Vector3.up * (baseY + lowH + 0.06f), new Vector3(thick + 0.12f, 0.12f, pl), Coping(p), rot);
            float y0 = baseY + lowH + 0.12f, y1 = baseY + height;
            GameObject box = Box(parent, $"FenceOnWall_{i}", baseMid + Vector3.up * ((y0 + y1) * 0.5f), new Vector3(0.03f, y1 - y0, pl - 0.06f), p.ChainlinkFence, rot);
            // fix round: the far-fading see-through copy vanished past ~10 m (the low panel read as a broken wall in front
            // of an invisible collider): the palette chain-link (the lead's alpha-clipped wire) and a heavier frame with
            // two intermediate posts, so the fence reads at 15 m
            MeshRenderer mesh = ThinChainlinkBox(box);
            if (mesh != null && p.ChainlinkFence != null) mesh.sharedMaterial = p.ChainlinkFence;
            Material galv = GalvSteel(p), wire = p.DarkPaintedMetal;
            TubeFrame(parent, box, galv, 0.038f, true, false);
            for (int k = 1; k <= 2; k++)
            {
                Vector3 pc = baseMid + d * ((k / 3f - 0.5f) * (pl - 0.06f));
                Cyl(parent, $"FencePost_{i}", pc + Vector3.up * y0, pc + Vector3.up * (y1 + 0.02f), 0.03f, galv);
            }
            Vector3 n = rot * Vector3.right;
            // barbed wire 8 mm in dark steel (4 mm galvanised was under a pixel past ~2 m and crawled under TAA)
            for (int k = 0; k < 3; k++)
            {
                Vector3 off = Vector3.up * (y1 + 0.12f + k * 0.11f) + n * (0.05f * (k - 1));
                Cyl(parent, $"BarbWire_{i}", baseMid - d * (pl * 0.5f - 0.03f) + off, baseMid + d * (pl * 0.5f - 0.03f) + off, 0.008f, wire);
            }
            for (int e = -1; e <= 1; e += 2)
                Cyl(parent, $"BarbArm_{i}", baseMid + d * (e * (pl * 0.5f - 0.03f)) + Vector3.up * y1, baseMid + d * (e * (pl * 0.5f - 0.03f)) + Vector3.up * (y1 + 0.36f), 0.016f, galv);
        }

        /// <summary>
        /// Round 3 (review 2: three blank 5 m walls fill 70 % of the frame): form-tie holes and lifting anchors on every
        /// panel, a weep hole, an algae line under the coping, a conduit with clamps and junction boxes, a cable tray or
        /// downpipes on alternate walls, a wall sign, and ferns rooted at the foot. Collider-free; primitives merged.
        /// </summary>
        private static void PathWallDetail(Transform wallRoot, string name, Vector3 a, Vector3 d, Vector3 n, Quaternion rot, float pl, int count, bool[] low,
            float thick, float baseY, float height, WorldMaterialPalette p, int seed)
        {
            var det = new GameObject("R3Detail");
            det.transform.SetParent(wallRoot, false);
            det.layer = GameLayers.Environment;
            Transform t = det.transform;
            Material dark = DrainVoid(p), galv = GalvSteel(p), paint = p.DarkPaintedMetal;
            float len = pl * count;
            Vector3 F(float s, float y, float off) => a + d * s + n * (thick * 0.5f + off) + Vector3.up * (baseY + y);
            float lowH = Mathf.Min(height, LowPanelH);

            for (int i = 0; i < count; i++)
            {
                float top = low[i] ? lowH : height;
                float s0 = i * pl;
                // form-tie holes: 3 columns, 2 rows on a low panel, 4 on a full one
                float[] rows = low[i] ? new[] { 0.8f, 1.75f } : new[] { 0.8f, 1.95f, 3.1f, 4.25f };
                float[] cols = { 0.45f, pl * 0.5f, pl - 0.45f };
                foreach (float ry in rows)
                    foreach (float cx in cols)
                        if (!InDoor(seed, s0 + cx, 0.12f) || ry > 2.7f)
                            Cyl(t, "TieHole", F(s0 + cx, ry, -0.004f), F(s0 + cx, ry, 0.005f), 0.021f, dark);
                if (!low[i])
                {
                    // lifting anchors (recessed pockets) near the top, and an algae line under the coping
                    Box(t, "LiftAnchor", F(s0 + pl * 0.27f, height - 0.38f, 0.002f), new Vector3(0.012f, 0.05f, 0.11f), dark, rot);
                    Box(t, "LiftAnchor", F(s0 + pl * 0.73f, height - 0.38f, 0.002f), new Vector3(0.012f, 0.05f, 0.11f), dark, rot);
                    // fix round (algae showed on few panels and was sub-pixel): a 0.6 m algae line under every coping, and
                    // a dark runoff streak from the coping down ~3.4 m on ~70 % of the panels
                    Decal(t, $"AlgaeLine_{i}", F(s0 + pl * 0.5f, height - 0.3f, 0.15f), n, new Vector3(pl * 0.97f, 0.6f, 0.4f), AlgaeStrong(p), 0.9f);
                    if (Hash(i + 53f, seed + 11f) < 0.7f)
                    {
                        float rsw = 0.9f + 0.7f * Hash(i, seed + 61f);
                        float sx = s0 + pl * (0.25f + 0.5f * Hash(seed + 67f, i));
                        Decal(t, $"RunoffStreak_{i}", F(sx, height - 1.7f, 0.2f), n, new Vector3(rsw, 3.4f, 0.45f), RunoffStrong(p), 0.8f);
                    }
                }
                // fix round: one eye-level object every two panels, under the conduit: a red hose-reel cabinet, a yellow
                // emergency phone or a grey isolator box, each fed by a short drop from the conduit
                if (i % 2 == (seed & 1) && !NearKeepFull(a + d * (s0 + pl * 0.5f), pl) && !InDoor(seed, s0 + pl * 0.5f, pl * 0.5f))
                {
                    float cyl = 1.45f + 0.35f * Hash(seed, 77f);
                    float ex = s0 + pl * (0.3f + 0.4f * Hash(i + 3f, seed + 71f));
                    int kind = (int)(Hash(i + 5f, seed + 73f) * 3f) % 3;
                    float ctop = cyl - 0.16f;
                    if (kind == 0)
                    {
                        Box(t, "HoseCabinet", F(ex, ctop - 0.32f, 0.09f), new Vector3(0.18f, 0.64f, 0.56f), PaintRed(p), rot);
                        Box(t, "HoseCabinetDoor", F(ex, ctop - 0.32f, 0.183f), new Vector3(0.008f, 0.56f, 0.48f), p.DarkPaintedMetal, rot);
                        Cyl(t, "HoseReelHub", F(ex, ctop - 0.32f, 0.186f), F(ex, ctop - 0.32f, 0.2f), 0.09f, PaintRed(p));
                    }
                    else if (kind == 1)
                    {
                        Box(t, "PhoneCabinet", F(ex, ctop - 0.26f, 0.08f), new Vector3(0.16f, 0.52f, 0.36f), p.PaintedMetalYellow, rot);
                        Box(t, "PhoneHood", F(ex, ctop + 0.01f, 0.12f), new Vector3(0.24f, 0.03f, 0.42f), p.PaintedMetalYellow, rot);
                        Box(t, "PhoneHandset", F(ex, ctop - 0.2f, 0.175f), new Vector3(0.04f, 0.2f, 0.06f), p.DarkPaintedMetal, rot);
                        Box(t, "PhoneKeypad", F(ex + 0.1f, ctop - 0.38f, 0.163f), new Vector3(0.006f, 0.1f, 0.08f), p.DarkPaintedMetal, rot);
                    }
                    else
                    {
                        Box(t, "IsolatorBox", F(ex, ctop - 0.22f, 0.075f), new Vector3(0.15f, 0.44f, 0.3f), TransformerGrey(p), rot);
                        Box(t, "IsolatorHandle", F(ex, ctop - 0.2f, 0.165f), new Vector3(0.04f, 0.12f, 0.04f), PaintRed(p), rot);
                        Box(t, "IsolatorLabel", F(ex, ctop - 0.06f, 0.152f), new Vector3(0.004f, 0.05f, 0.16f), p.PaintedMetalYellow, rot);
                    }
                    Cyl(t, "CabinetFeed", F(ex, ctop, 0.045f), F(ex, cyl, 0.045f), 0.016f, GalvSteel(p));
                }
                // weep hole above the plinth
                float wx = s0 + pl * (0.35f + 0.3f * Hash(i, seed + 3f));
                if (!InDoor(seed, wx, 0.1f))
                    Box(t, "WeepHole", F(wx, 0.47f, 0.002f), new Vector3(0.012f, 0.05f, 0.09f), dark, rot);
                // ferns rooted in the kerb joint at the wall foot
                if (Hash(i + 7f, seed + 19f) > 0.62f && !InDoor(seed, s0 + pl * (0.25f + 0.5f * Hash(seed, i + 2f)), 0.5f))
                    Plant(p, t, "fern_02", F(s0 + pl * (0.25f + 0.5f * Hash(seed, i + 2f)), 0.13f, 0.12f), 360f * Hash(i, seed + 29f), 0.5f + 0.3f * Hash(seed + 31f, i), 0.03f, seatOnTerrain: false, shadows: false);
            }

            // conduit at 1.45-1.8 m along the whole wall: saddle clamps every 1.2 m, a junction box every third panel, and
            // an elbow down into the ground at both ends
            float cy = 1.45f + 0.35f * Hash(seed, 77f);
            const float co = 0.045f;
            if (DoorSpans.TryGetValue(seed, out Vector2 door))
            {
                // round 4: the run hops over the steel door head (two elbows up, a short run at 2.62 m, two down)
                float h0 = door.x - 0.18f, h1 = door.y + 0.18f;
                const float hy = 2.62f;
                Cyl(t, "Conduit", F(0.35f, cy, co), F(h0, cy, co), 0.022f, galv);
                Cyl(t, "Conduit", F(h1, cy, co), F(len - 0.35f, cy, co), 0.022f, galv);
                Cyl(t, "ConduitRiser", F(h0, cy - 0.02f, co), F(h0, hy + 0.02f, co), 0.022f, galv);
                Cyl(t, "ConduitRiser", F(h1, cy - 0.02f, co), F(h1, hy + 0.02f, co), 0.022f, galv);
                Cyl(t, "Conduit", F(h0, hy, co), F(h1, hy, co), 0.022f, galv);
            }
            else
                Cyl(t, "Conduit", F(0.35f, cy, co), F(len - 0.35f, cy, co), 0.022f, galv);
            Cyl(t, "ConduitDrop", F(0.35f, 0.15f, co), F(0.35f, cy, co), 0.022f, galv);
            Cyl(t, "ConduitDrop", F(len - 0.35f, 0.15f, co), F(len - 0.35f, cy, co), 0.022f, galv);
            for (float sc = 0.9f; sc < len - 0.5f; sc += 1.2f)
                if (!InDoor(seed, sc, 0.25f)) Box(t, "ConduitClamp", F(sc, cy, co * 0.5f + 0.01f), new Vector3(co + 0.01f, 0.06f, 0.025f), galv, rot);
            for (int i = 1; i < count; i += 3)
            {
                float sj = (i + 0.5f) * pl;
                if (InDoor(seed, sj, 0.3f)) continue;
                Box(t, "JunctionBox", F(sj, cy, 0.06f), new Vector3(0.1f, 0.26f, 0.22f), paint, rot);
                Box(t, "JunctionLid", F(sj, cy, 0.112f), new Vector3(0.006f, 0.22f, 0.18f), galv, rot);
            }

            // even seeds: a cable tray at 3.7 m over every run of full-height panels; odd seeds: downpipes at joints
            if (seed % 2 == 0)
            {
                for (int i0 = 0; i0 < count;)
                {
                    int i1 = i0; while (i1 + 1 < count && low[i1 + 1] == low[i0]) i1++;
                    if (!low[i0] && i1 > i0)
                    {
                        float ta = i0 * pl + 0.3f, tb = (i1 + 1) * pl - 0.3f, tm = (ta + tb) * 0.5f, tl = tb - ta;
                        const float ty = 3.7f;
                        Box(t, "TrayBottom", F(tm, ty, 0.21f), new Vector3(0.3f, 0.008f, tl), galv, rot);
                        Box(t, "TrayRail", F(tm, ty + 0.04f, 0.06f), new Vector3(0.012f, 0.08f, tl), galv, rot);
                        Box(t, "TrayRail", F(tm, ty + 0.04f, 0.36f), new Vector3(0.012f, 0.08f, tl), galv, rot);
                        for (int k = 0; k < 3; k++)
                            Cyl(t, "TrayCable", F(ta + 0.05f, ty + 0.025f + 0.01f * k, 0.12f + 0.08f * k), F(tb - 0.05f, ty + 0.025f + 0.01f * (2 - k), 0.12f + 0.08f * k), 0.017f, Rubber(p));
                        for (float sb = ta + 0.2f; sb < tb; sb += 1.5f)
                        {
                            Box(t, "TrayBracketPlate", F(sb, ty - 0.08f, 0.006f), new Vector3(0.012f, 0.26f, 0.05f), paint, rot);
                            Box(t, "TrayBracketArm", F(sb, ty - 0.02f, 0.2f), new Vector3(0.38f, 0.03f, 0.04f), paint, rot);
                        }
                    }
                    i0 = i1 + 1;
                }
            }
            else
            {
                for (int j = 1; j < count; j++)
                {
                    if ((j + seed) % 3 != 0 || low[j - 1] || low[j]) continue;
                    float sp = j * pl + 0.22f;
                    Box(t, "Scupper", F(sp, height - 0.12f, 0.05f), new Vector3(0.1f, 0.12f, 0.14f), paint, rot);
                    Cyl(t, "Downpipe", F(sp, height - 0.15f, 0.08f), F(sp, 0.32f, 0.08f), 0.045f, paint);
                    Cyl(t, "DownpipeShoe", F(sp, 0.34f, 0.08f), F(sp, 0.18f, 0.24f), 0.045f, paint);
                    for (int k = 0; k < 3; k++)
                        Box(t, "PipeBracket", F(sp, 1.0f + k * 1.4f, 0.04f), new Vector3(0.08f, 0.04f, 0.11f), galv, rot);
                    Decal(t, "DownpipeStain", F(sp, 0.9f, 0.15f), n, new Vector3(0.5f, 1.6f, 0.35f), Grime(p, j + seed), 0.7f);
                }
            }

            // barbed wire on the coping of the full-height runs (review 2 check: forms that read in silhouette against the
            // sky): cranked arms leaning out over the jungle at every joint, three strands (not on the fallen-palm wall)
            if (seed != 7)
            {
                for (int i0 = 0; i0 < count;)
                {
                    int i1 = i0; while (i1 + 1 < count && low[i1 + 1] == low[i0]) i1++;
                    if (!low[i0])
                    {
                        float wa = i0 * pl + 0.15f, wb = (i1 + 1) * pl - 0.15f;
                        float yTop = height + 0.17f;
                        Vector3 lean = (-n * 0.7f + Vector3.up).normalized;
                        for (int k = 0; k < 3; k++)
                        {
                            Vector3 off = lean * (0.14f + 0.13f * k);
                            Cyl(t, "CopingWire", a + d * wa + Vector3.up * (baseY + yTop) + off, a + d * wb + Vector3.up * (baseY + yTop) + off, 0.008f, paint);
                        }
                        for (float sa = wa; sa <= wb + 0.01f; sa += Mathf.Max(1.0f, (wb - wa) / Mathf.Max(1, Mathf.RoundToInt((wb - wa) / 2f))))
                            Cyl(t, "CopingWireArm", a + d * sa + Vector3.up * (baseY + yTop - 0.02f), a + d * sa + Vector3.up * (baseY + yTop) + lean * 0.46f, 0.014f, galv);
                    }
                    i0 = i1 + 1;
                }
            }

            // one sign per wall on a full panel near the middle
            string stem = null, mat = null; float sw = 0f, sh = 0f;
            switch (seed)
            {
                case 2: case 6: stem = "staff_only"; mat = "MAT_Z23_SignStaffOnlyPlate"; sw = 1.1f; sh = 0.55f; break;
                case 3: stem = "paddock4_gate"; mat = "MAT_Z23_SignPaddock4"; sw = 0.9f; sh = 0.7f; break;
                case 4: case 10: stem = "high_voltage"; mat = "MAT_Z23_SignHighVoltage"; sw = 0.45f; sh = 0.62f; break;
                case 5: stem = "talon_ridge_warning"; mat = "MAT_Z23_SignTalonWarning"; sw = 1.0f; sh = 0.62f; break;
                case 8: stem = "service_road_3"; mat = "MAT_Z23_SignServiceRoad3"; sw = 1.3f; sh = 0.46f; break;
            }
            if (stem != null)
            {
                int k = count / 2;
                for (int tries = 0; tries < count && low[k]; tries++) k = (k + 1) % count;
                if (!low[k])
                    SignPlate(t, $"Sign_PathWall_{name}", F((k + 0.5f) * pl + 0.6f, 2.35f, 0.02f), n, sw, sh, SignMat(p, mat, stem), p.DarkPaintedMetal);
            }

            MergeStatic(t);
        }

        /// <summary>
        /// A free-standing precast wall from a to b: 4 m panels with recessed joints (one continuous invisible collider), a coping cap, outside pilasters, a plinth, an inside kerb and
        /// a drain with grating, grime, moss and vines. insidePoint picks which face is the "inside" (the lane).
        /// extendStart/End lengthen the wall past its end points (closes L-corner notches).
        /// </summary>
        public static void PanelWall(Transform parent, string name, Vector3 a, Vector3 b, float baseY, float height, float thick,
            Vector3 insidePoint, WorldMaterialPalette p, WallStyle st, float extendStart = 0f, float extendEnd = 0f, int seed = 0)
        {
            Vector3 d = b - a; d.y = 0f;
            float rawLen = d.magnitude; if (rawLen < 0.2f) return;
            d /= rawLen;
            a -= d * extendStart; b += d * extendEnd;
            float len = rawLen + extendStart + extendEnd;
            Vector3 n = new Vector3(-d.z, 0f, d.x);
            Vector3 mid = (a + b) * 0.5f;
            if (Vector3.Dot(new Vector3(insidePoint.x - mid.x, 0f, insidePoint.z - mid.z), n) < 0f) n = -n;   // n = inside normal
            Quaternion rot = Quaternion.LookRotation(d, Vector3.up);   // local z along the wall, local x = right of d

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.layer = GameLayers.Environment;

            float panelLen = st.PanelLen > 0.5f ? st.PanelLen : 4f;
            int count = Mathf.Max(1, Mathf.RoundToInt(len / panelLen));
            float pl = len / count;
            const float joint = 0.035f;
            Material[] pm = { PanelA(p), PanelB(p), PanelC(p) };
            // round 3 (review 2: a canyon of bare 5 m slabs): on some walls pairs of panels are a low 2.2 m wall with a
            // chain-link fence on top, so the jungle shows through. The invisible full-height collider is unchanged.
            bool[] low = LowPanels(name, a, d, pl, count, height, st, seed);
            float lowH = Mathf.Min(height, LowPanelH);
            for (int i = 0; i < count; i++)
            {
                float h = Hash(i + seed * 31f, rawLen);
                Material m = h < 0.55f ? pm[0] : (h < 0.85f ? pm[1] : pm[2]);
                float ph = low[i] ? lowH : height;
                Vector3 c = a + d * ((i + 0.5f) * pl) + Vector3.up * (baseY + ph * 0.5f);
                float lenI = pl - (i < count - 1 ? joint * 0.5f : 0f) - (i > 0 ? joint * 0.5f : 0f);
                Vector3 cc = c + d * ((i > 0 ? joint * 0.25f : 0f) - (i < count - 1 ? joint * 0.25f : 0f));
                Box(root.transform, $"Panel_{i}", cc, new Vector3(thick, ph, lenI), m, rot);
                if (i < count - 1)
                {
                    // recessed joint (dark mastic) 2.5 cm behind both faces; the panel colliders meet behind it
                    float jh = (low[i] || low[i + 1]) ? lowH : height;
                    Vector3 jc = a + d * ((i + 1) * pl) + Vector3.up * (baseY + jh * 0.5f);
                    Box(root.transform, $"Joint_{i}", jc, new Vector3(thick - 0.05f, jh, joint + 0.01f), DrainVoid(p), rot);
                }
                if (low[i]) LowPanelFence(root.transform, a + d * ((i + 0.5f) * pl), d, rot, pl, thick, baseY, lowH, height, p, i);
            }
            // one continuous, invisible collision box for the whole wall (no seams for the player or the NavMesh)
            GameObject wallCol = Box(root.transform, "WallCollider", (a + b) * 0.5f + Vector3.up * (baseY + height * 0.5f), new Vector3(thick, height, len), null, rot, collider: true);
            wallCol.GetComponent<MeshRenderer>().enabled = false;

            float eps = (seed % 7) * 0.0023f;   // walls that meet end to end never share a coplanar top
            // coping cap with a small overhang both sides and at the ends (one per run of full-height panels)
            for (int i0 = 0; i0 < count;)
            {
                int i1 = i0; while (i1 + 1 < count && low[i1 + 1] == low[i0]) i1++;
                if (!low[i0])
                {
                    float s0 = i0 * pl - (i0 == 0 ? 0.08f : 0f), s1 = (i1 + 1) * pl + (i1 == count - 1 ? 0.08f : 0f);
                    Box(root.transform, "Coping", a + d * ((s0 + s1) * 0.5f) + Vector3.up * (baseY + height + 0.085f + eps), new Vector3(thick + 0.16f, 0.17f, s1 - s0), Coping(p), rot);
                }
                i0 = i1 + 1;
            }
            // round 4 (review 3: 'the walls look dry and newly poured'): a strong runoff streak under every coping joint
            // of the full-height runs, where the rain sheets off the coping and stains the panel edges
            if (st.Grime)
                for (int i = 0; i < count - 1; i++)
                {
                    if (low[i] || low[i + 1]) continue;
                    float sw = 0.6f + 0.5f * Hash(i + 91f, seed);
                    float sh = 2.6f + 1.6f * Hash(seed + 93f, i);
                    Decal(root.transform, $"JointRunoff_{i}", a + d * ((i + 1) * pl) + n * (thick * 0.5f + 0.2f) + Vector3.up * (baseY + height - sh * 0.5f),
                        n, new Vector3(sw, sh, 0.45f), RunoffStrong(p), 0.9f);
                }
            // plinth (footing course) proud of both faces
            Box(root.transform, "Plinth", (a + b) * 0.5f + Vector3.up * (baseY + 0.17f + eps), new Vector3(thick + 0.10f, 0.34f, len + 0.02f), Plinth(p), rot);

            if (st.Pilasters)
            {
                for (int i = 0; i <= count; i += 2)
                {
                    bool lowL = i > 0 && low[i - 1], lowR = i < count && low[i];
                    float pih = ((i == 0 || lowL) && (i == count || lowR)) ? lowH : height;
                    Vector3 pc = a + d * (i * pl) - n * (thick * 0.5f + 0.11f) + Vector3.up * (baseY + pih * 0.5f);
                    Box(root.transform, $"Pilaster_{i}", pc, new Vector3(0.22f, pih, 0.5f), Plinth(p), rot);
                }
            }

            if (st.Kerb)
            {
                // kerb 15 cm high, then a 26 cm drain channel with a steel grating (both walk-through: no collider)
                Vector3 kc = (a + b) * 0.5f + n * (thick * 0.5f + 0.11f) + Vector3.up * (baseY + 0.075f + eps);
                Box(root.transform, "Kerb", kc, new Vector3(0.22f, 0.15f, len), Kerb(p), rot);
                Vector3 dc = (a + b) * 0.5f + n * (thick * 0.5f + 0.22f + 0.13f) + Vector3.up * (baseY + 0.024f + eps);
                Box(root.transform, "DrainChannel", dc, new Vector3(0.26f, 0.012f, len - 0.2f), DrainVoid(p), rot);
                DecalProjector g = Decal(root.transform, "DrainGrating", dc + Vector3.up * 0.05f, Vector3.up, new Vector3(0.28f, len - 0.2f, 0.2f), Grating(p), 1f);
                g.transform.rotation = Quaternion.LookRotation(Vector3.down, d);
                g.uvScale = new Vector2(1f, Mathf.Max(1f, (len - 0.2f) / 0.6f));
            }

            Vector3 inFace = n * (thick * 0.5f);
            for (int i = 0; i < count; i++)
            {
                float h1 = Hash(i * 3.1f + seed, rawLen * 0.37f);
                float along = (i + 0.2f + 0.6f * h1) * pl;
                Vector3 basePt = a + d * along;
                if (st.Grime && h1 > 0.2f && !low[i])
                {
                    float w = 1.0f + 1.4f * Hash(i, seed + 7f);
                    float gh = 2.2f + 1.6f * Hash(seed + 3f, i);
                    Decal(root.transform, $"Grime_{i}", basePt + inFace + n * 0.2f + Vector3.up * (baseY + height - gh * 0.5f), n, new Vector3(w, gh, 0.5f), Grime(p, i + seed), 0.75f + 0.2f * h1);
                    // the outside gets streaks too (seen over the jungle from the next lane)
                    if (h1 > 0.55f)
                        Decal(root.transform, $"GrimeOut_{i}", basePt - inFace - n * 0.25f + Vector3.up * (baseY + height - gh * 0.5f), -n, new Vector3(w, gh, 0.6f), Grime(p, i + seed + 1), 0.7f);
                }
                if (st.Moss && Hash(i + 11f, seed) > 0.35f)
                {
                    float mw = 1.6f + 2.0f * Hash(i, seed + 13f);
                    Decal(root.transform, $"MossIn_{i}", a + d * ((i + 0.5f) * pl) + inFace + n * 0.15f + Vector3.up * (baseY + 0.45f), n, new Vector3(mw, 1.1f, 0.9f), Moss(p), 0.85f);
                    Decal(root.transform, $"MossOut_{i}", a + d * ((i + 0.3f) * pl) - inFace - n * 0.2f + Vector3.up * (baseY + 0.7f), -n, new Vector3(mw + 0.8f, 1.6f, 0.9f), Moss(p), 0.95f);
                }
                if (Hash(i + 23f, seed + 5f) > 0.8f && !low[i])
                    Decal(root.transform, $"Cracks_{i}", basePt + inFace + n * 0.15f + Vector3.up * (baseY + 1.4f + 1.5f * h1), n, new Vector3(1.6f, 1.6f, 0.4f), Cracks2(p), 0.8f, rollDeg: 90f * h1);
            }

            if (st.Vines)
            {
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                for (float s = 2.5f + 3f * Hash(seed, 1f); s < len - 1.5f; s += 6.5f + 4f * Hash(s, seed))
                {
                    if (Hash(s * 0.7f, seed + 2f) < 0.25f) continue;
                    if (low[Mathf.Clamp((int)(s / pl), 0, count - 1)]) continue;
                    if (InDoor(seed, s, 0.7f)) continue;
                    float vh = 2.4f + 1.4f * Hash(s, seed + 9f);
                    Vector3 vp = a + d * s + n * (thick * 0.5f + 0.12f);
                    vp.y = baseY + height + 0.12f - vh;
                    Plant(p, root.transform, "hanging_vines", vp, yaw, vh, 0.04f, seatOnTerrain: false, shadows: false).name = "Vines";
                }
            }

            if (st.Detail) PathWallDetail(root.transform, name, a, d, n, rot, pl, count, low, thick, baseY, height, p, seed);

            if (st.Jungle)
            {
                // understory hugging the outside face, trees 2.5-5 m out so the crowns hang over the coping
                string[] under = { "banana_plant", "monstera_clump", "tree_fern", "fern_02", "monstera_clump", "tree_fern", "banana_plant", "fern_02" };
                float[] usize = { 2.6f, 1.5f, 3.4f, 1.0f, 1.3f, 3.0f, 3.0f, 0.9f };
                int k = 0;
                for (float s = 1.0f + 2f * Hash(seed, 4f); s < len - 0.5f; s += 4.6f + 2.6f * Hash(s, seed + 1f), k++)
                {
                    int u = (int)(Hash(s, seed + 17f) * under.Length) % under.Length;
                    float sz = usize[u] * (0.85f + 0.3f * Hash(seed, s));
                    float rad = sz * 0.5f + 0.15f;                       // keep the leaves out of this wall and the next one
                    if (s < rad + 0.4f || s > len - rad - 0.4f) continue;
                    Vector3 up = a + d * s - n * (thick * 0.5f + rad + 0.5f * Hash(s, seed + 3f));
                    if (st.LaneKeepOut && PathLaneDistance(up) < 2.55f + rad) continue;
                    Plant(p, root.transform, under[u], up, 360f * Hash(s, seed + 6f), sz, 0.03f, shadows: false);
                }
                for (float s = 2f + 3f * Hash(seed, 8f); s < len; s += 9.0f + 4f * Hash(s, seed + 12f))
                {
                    float r = Hash(s, seed + 21f);
                    string stem = r < 0.45f ? "palm_tree" : (r < 0.7f ? "palm_tree_b" : "jungle_tree");
                    float size = stem == "jungle_tree" ? 13f + 4f * Hash(s, 2f) : 9.5f + 3.5f * Hash(s, 3f);
                    // buttress roots and low limbs of the big jungle trees reach ~6 m: keep them clear of the lane
                    float off = stem == "jungle_tree" ? 7.0f + 2.0f * Hash(s, seed + 14f) : 2.6f + 2.4f * Hash(s, seed + 14f);
                    Vector3 tp = a + d * s - n * (thick * 0.5f + off);
                    if (st.LaneKeepOut && PathLaneDistance(tp) < 2.55f + (stem == "jungle_tree" ? 6.5f : 1.6f)) continue;
                    Plant(p, root.transform, stem, tp, 360f * Hash(seed + 2f, s), size, 0.012f);
                }
            }
        }
    }
}
