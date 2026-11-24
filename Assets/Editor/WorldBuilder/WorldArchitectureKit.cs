using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Cretacea.Core;
using Cretacea.Interact;

namespace Cretacea.Editor.WorldBuilder
{
    public enum OpeningKind
    {
        Doorway = 0,
        Window = 1,
        Archway = 2
    }

    public struct OpeningSpec
    {
        public OpeningKind Kind;
        public float CenterAlongWall; // Distance along wall from start -> end (meters)
        public float Width;
        public float SillY;           // 0 for doors/arches; > 0 for windows
        public float Height;
        public bool AddGlass;
        public int MullionCount;      // Vertical steel mullions inside window
        public Material FrameMaterial;
        public Material GlassMaterial;

        public float MinAlong => CenterAlongWall - Width * 0.5f;
        public float MaxAlong => CenterAlongWall + Width * 0.5f;
        public float TopY => SillY + Height;

        public static OpeningSpec Door(float centerAlong, float width, float height = 2.25f, Material frameMat = null)
        {
            return new OpeningSpec
            {
                Kind = OpeningKind.Doorway,
                CenterAlongWall = centerAlong,
                Width = width,
                SillY = 0f,
                Height = height,
                AddGlass = false,
                MullionCount = 0,
                FrameMaterial = frameMat
            };
        }

        public static OpeningSpec Arch(float centerAlong, float width, float height = 2.4f, Material frameMat = null)
        {
            return new OpeningSpec
            {
                Kind = OpeningKind.Archway,
                CenterAlongWall = centerAlong,
                Width = width,
                SillY = 0f,
                Height = height,
                AddGlass = false,
                MullionCount = 0,
                FrameMaterial = frameMat
            };
        }

        public static OpeningSpec Window(float centerAlong, float width, float sillY, float height, int mullions = 2, Material frameMat = null, Material glassMat = null)
        {
            return new OpeningSpec
            {
                Kind = OpeningKind.Window,
                CenterAlongWall = centerAlong,
                Width = width,
                SillY = sillY,
                Height = height,
                AddGlass = true,
                MullionCount = mullions,
                FrameMaterial = frameMat,
                GlassMaterial = glassMat
            };
        }
    }

    /// <summary>
    /// Precision modular architectural kit that MATHEMATICALLY eliminates Godot's wall-behind-door,
    /// dado-rail-crossing-doorway, fake-window, rain-leaking-indoors, and skybox-reflecting-floor bugs!
    /// Every wall with openings splits into solid piers + lintels + sills + structural jambs with zero veneer overlaps.
    /// </summary>
    public static class WorldArchitectureKit
    {
        /// <summary>
        /// Splits a wall cleanly between start -> end around all specified doors, archways, and windows.
        /// Skirting and stainless dado rails are segmented strictly BETWEEN openings — NEVER crossing a doorway!
        /// </summary>
        public static GameObject BuildWallWithOpenings(
            Transform parent,
            Vector3 start,
            Vector3 end,
            float baseY,
            float height,
            float thickness,
            Material material,
            OpeningSpec[] openings,
            bool addSkirting = false,
            bool addDadoRail = false,
            WorldMaterialPalette palette = null,
            string wallName = "Wall",
            bool includeInOcclusion = true)
        {
            Vector3 dir = end - start;
            dir.y = 0f;
            float length = dir.magnitude;
            if (length < 0.05f) return null;

            Vector3 forward = dir / length;
            Quaternion wallRot = Quaternion.LookRotation(forward, Vector3.up);

            var root = new GameObject(wallName);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(start.x, baseY, start.z), wallRot);
            root.layer = GameLayers.Environment;

            Material trimMat = palette != null ? palette.DarkPaintedMetal : material;
            Material steelMat = palette != null ? palette.BrushedSteel : trimMat;
            Material defaultGlass = palette != null ? palette.GlassArchitectural : material;

            var sorted = new List<OpeningSpec>();
            if (openings != null)
            {
                for (int i = 0; i < openings.Length; i++)
                {
                    if (openings[i].Width > 0.05f && openings[i].Height > 0.05f) sorted.Add(openings[i]);
                }
                sorted.Sort((a, b) => a.MinAlong.CompareTo(b.MinAlong));
            }

            float cursor = 0f;
            for (int i = 0; i < sorted.Count; i++)
            {
                OpeningSpec op = sorted[i];
                float opMin = Mathf.Clamp(op.MinAlong, cursor, length);
                float opMax = Mathf.Clamp(op.MaxAlong, opMin, length);

                // 1. Solid full-height pier BEFORE this opening (with segmented skirting & dado rail!)
                if (opMin - cursor > 0.04f)
                {
                    BuildSolidPier(root.transform, $"Pier_{i}", cursor, opMin, 0f, height, thickness, material, addSkirting, addDadoRail, trimMat, steelMat, includeInOcclusion);
                }

                // 2. Solid header lintel ABOVE the opening
                float lintelBottom = Mathf.Clamp(op.TopY, 0f, height);
                if (height - lintelBottom > 0.04f && opMax - opMin > 0.04f)
                {
                    BuildWallSegmentBox(root.transform, $"Lintel_{i}", opMin, opMax, lintelBottom, height, thickness, material, includeInOcclusion);
                }

                // 3. Solid sill wall BELOW a window (with segmented skirting & dado if below sill!)
                if (op.Kind == OpeningKind.Window && op.SillY > 0.04f && opMax - opMin > 0.04f)
                {
                    BuildSolidPier(root.transform, $"Sill_{i}", opMin, opMax, 0f, op.SillY, thickness, material, addSkirting, addDadoRail && op.SillY >= 1.05f, trimMat, steelMat, includeInOcclusion);
                }

                // 4. Recessed beveled frame jambs + window glass panes + vertical steel mullions
                BuildOpeningFrame(root.transform, $"Frame_{i}", op, opMin, opMax, thickness, op.FrameMaterial ?? trimMat, op.GlassMaterial ?? defaultGlass);

                cursor = Mathf.Max(cursor, opMax);
            }

            // Trailing solid pier after final opening
            if (length - cursor > 0.04f)
            {
                BuildSolidPier(root.transform, "Pier_End", cursor, length, 0f, height, thickness, material, addSkirting, addDadoRail, trimMat, steelMat, includeInOcclusion);
            }

            return root;
        }

        private static void BuildSolidPier(
            Transform wallRoot,
            string name,
            float z0,
            float z1,
            float y0,
            float y1,
            float thickness,
            Material wallMat,
            bool addSkirting,
            bool addDadoRail,
            Material skirtingMat,
            Material dadoMat,
            bool includeInOcclusion)
        {
            BuildWallSegmentBox(wallRoot, name, z0, z1, y0, y1, thickness, wallMat, includeInOcclusion);

            float len = z1 - z0;
            float centerZ = (z0 + z1) * 0.5f;

            if (addSkirting && y0 <= 0.01f && len > 0.08f)
            {
                CreateBox(wallRoot, $"{name}_Skirt", new Vector3(0f, 0.06f, centerZ), new Vector3(thickness + 0.03f, 0.12f, len), skirtingMat, addCollider: false);
            }

            if (addDadoRail && y0 <= 0.01f && y1 >= 1.15f && len > 0.08f)
            {
                CreateBox(wallRoot, $"{name}_Dado", new Vector3(0f, 1.0f, centerZ), new Vector3(thickness + 0.045f, 0.075f, len), dadoMat, addCollider: false);
            }
        }

        private static void BuildWallSegmentBox(
            Transform wallRoot,
            string name,
            float z0,
            float z1,
            float y0,
            float y1,
            float thickness,
            Material mat,
            bool includeInOcclusion)
        {
            float len = z1 - z0;
            float h = y1 - y0;
            GameObject seg = CreateBox(wallRoot, name, new Vector3(0f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(thickness, h, len), mat, addCollider: true);
            StaticEditorFlags flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.BatchingStatic;
            if (includeInOcclusion) flags |= StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
            else flags |= StaticEditorFlags.OccludeeStatic;
            GameObjectUtility.SetStaticEditorFlags(seg, flags);
        }

        private static void BuildOpeningFrame(
            Transform wallRoot,
            string name,
            OpeningSpec op,
            float z0,
            float z1,
            float thickness,
            Material frameMat,
            Material glassMat)
        {
            var frameRoot = new GameObject(name);
            frameRoot.transform.SetParent(wallRoot, false);
            frameRoot.layer = GameLayers.Environment;

            float frameDepth = thickness + 0.05f;
            const float jambThick = 0.065f;
            float centerZ = (z0 + z1) * 0.5f;
            float width = z1 - z0;

            // Left & Right Jambs + Header Transom
            CreateBox(frameRoot.transform, "Jamb_L", new Vector3(0f, op.SillY + op.Height * 0.5f, z0 + jambThick * 0.5f), new Vector3(frameDepth, op.Height, jambThick), frameMat, addCollider: false);
            CreateBox(frameRoot.transform, "Jamb_R", new Vector3(0f, op.SillY + op.Height * 0.5f, z1 - jambThick * 0.5f), new Vector3(frameDepth, op.Height, jambThick), frameMat, addCollider: false);
            CreateBox(frameRoot.transform, "Header", new Vector3(0f, op.TopY - jambThick * 0.5f, centerZ), new Vector3(frameDepth, jambThick, width), frameMat, addCollider: false);

            if (op.Kind == OpeningKind.Window)
            {
                // Recessed metallic window sill ledge
                CreateBox(frameRoot.transform, "SillLedge", new Vector3(0f, op.SillY + 0.03f, centerZ), new Vector3(frameDepth + 0.08f, 0.06f, width + 0.06f), frameMat, addCollider: false);

                // Vertical steel mullions
                if (op.MullionCount > 0)
                {
                    for (int m = 1; m <= op.MullionCount; m++)
                    {
                        float mz = Mathf.Lerp(z0, z1, m / (float)(op.MullionCount + 1));
                        CreateBox(frameRoot.transform, $"Mullion_{m}", new Vector3(0f, op.SillY + op.Height * 0.5f, mz), new Vector3(thickness * 0.72f, op.Height - 0.1f, 0.055f), frameMat, addCollider: false);
                    }
                }

                // Real transparent HDRP glass pane (solid collider keeps player/dinosaurs cleanly on either side, NEVER occluder-static!)
                if (op.AddGlass)
                {
                    GameObject pane = CreateBox(frameRoot.transform, "GlassPane", new Vector3(0f, op.SillY + op.Height * 0.5f, centerZ), new Vector3(0.03f, op.Height - 0.08f, width - 0.08f), glassMat, addCollider: true);
                    MeshRenderer mr = pane.GetComponent<MeshRenderer>();
                    if (mr != null) mr.shadowCastingMode = ShadowCastingMode.Off;
                    GameObjectUtility.SetStaticEditorFlags(pane, StaticEditorFlags.OccludeeStatic);
                }
            }
        }

        /// <summary>
        /// Mounts a framed interactive DoorController inside a cut opening with verified alignment,
        /// NavMeshObstacle, solid Layer 11 (Door) collider, handle hardware, optional keycard LED, and overhead signage.
        /// </summary>
        public static DoorController BuildFramedDoor(
            Transform parent,
            string doorId,
            Vector3 centerBasePos,
            float yawDeg,
            float width,
            float height,
            DoorMechanism mechanism,
            WorldMaterialPalette palette,
            bool startsOpen = false,
            bool startsLocked = false,
            string lockedHint = "Locked.",
            string soundPrefix = "door_interior",
            float primaryOpenAngleDeg = 95f,
            float secondaryOpenAngleDeg = -95f,
            Vector3 slideOpenOffset = default,
            bool bashableByRaptor = true,
            string overheadSignText = null,
            bool addKeycardLed = false,
            Material leafMaterial = null)
        {
            var doorRoot = new GameObject($"Door_{doorId}");
            doorRoot.transform.SetParent(parent, false);
            doorRoot.transform.SetPositionAndRotation(centerBasePos, Quaternion.Euler(0f, yawDeg, 0f));

            Material leafMat = leafMaterial != null ? leafMaterial : palette.DarkPaintedMetal;
            Material handleMat = palette.BrushedSteel;

            Transform primaryPivot;
            Transform secondaryPivot = null;

            if (mechanism == DoorMechanism.DoubleHingedOutward)
            {
                float halfW = width * 0.5f;
                // Left hinge at local -X = -halfW, Right hinge at local +X = +halfW
                var leftPivotGo = new GameObject("LeafA_Hinge");
                leftPivotGo.transform.SetParent(doorRoot.transform, false);
                leftPivotGo.transform.localPosition = new Vector3(-halfW, 0f, 0f);
                BuildDoorLeafGeometry(leftPivotGo.transform, "LeafA", halfW - 0.03f, height, 0.08f, +1f, leafMat, handleMat, palette, isChainlinkGate: true);
                primaryPivot = leftPivotGo.transform;

                var rightPivotGo = new GameObject("LeafB_Hinge");
                rightPivotGo.transform.SetParent(doorRoot.transform, false);
                rightPivotGo.transform.localPosition = new Vector3(+halfW, 0f, 0f);
                BuildDoorLeafGeometry(rightPivotGo.transform, "LeafB", halfW - 0.03f, height, 0.08f, -1f, leafMat, handleMat, palette, isChainlinkGate: true);
                secondaryPivot = rightPivotGo.transform;
            }
            else if (mechanism == DoorMechanism.SlidingHorizontal)
            {
                var slideGo = new GameObject("SlidingLeaf");
                slideGo.transform.SetParent(doorRoot.transform, false);
                slideGo.transform.localPosition = Vector3.zero;
                CreateBox(slideGo.transform, "GatePanel", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 0.24f), leafMat, addCollider: true);
                CreateBox(slideGo.transform, "TopBeam", new Vector3(0f, height - 0.15f, 0f), new Vector3(width + 0.2f, 0.30f, 0.32f), palette.RustedIron, addCollider: false);
                primaryPivot = slideGo.transform;
            }
            else if (mechanism == DoorMechanism.JammedRollerShutter)
            {
                // Jammed half-open shutter leaving exact 1.25 m clear crouch gap underneath (COL-2/DOOR-2 fixed!)
                const float clearGap = 1.25f;
                float shutterH = Mathf.Max(0.4f, height - clearGap);
                var rollerGo = new GameObject("JammedShutter");
                rollerGo.transform.SetParent(doorRoot.transform, false);
                CreateBox(rollerGo.transform, "ShutterCorrugated", new Vector3(0f, clearGap + shutterH * 0.5f, 0f), new Vector3(width - 0.06f, shutterH, 0.10f), palette.CorrugatedMetal, addCollider: true);
                CreateBox(rollerGo.transform, "BottomLip", new Vector3(0f, clearGap + 0.04f, 0f), new Vector3(width, 0.08f, 0.14f), palette.EmissiveAmber, addCollider: false);
                // Side guide rails
                CreateBox(doorRoot.transform, "Track_L", new Vector3(-width * 0.5f + 0.04f, height * 0.5f, 0f), new Vector3(0.08f, height, 0.16f), palette.DarkPaintedMetal, addCollider: false);
                CreateBox(doorRoot.transform, "Track_R", new Vector3(+width * 0.5f - 0.04f, height * 0.5f, 0f), new Vector3(0.08f, height, 0.16f), palette.DarkPaintedMetal, addCollider: false);
                primaryPivot = rollerGo.transform;
            }
            else
            {
                // Standard SingleHinged door pivoted cleanly on local left jamb (-width/2)
                var hingeGo = new GameObject("DoorHinge");
                hingeGo.transform.SetParent(doorRoot.transform, false);
                hingeGo.transform.localPosition = new Vector3(-width * 0.5f + 0.04f, 0f, 0f);
                BuildDoorLeafGeometry(hingeGo.transform, "DoorLeaf", width - 0.08f, height - 0.05f, 0.06f, +1f, leafMat, handleMat, palette, isChainlinkGate: false);
                primaryPivot = hingeGo.transform;
            }

            // Focus Anchor centered at eye-reachable door height
            var anchorGo = new GameObject("FocusAnchor");
            anchorGo.transform.SetParent(doorRoot.transform, false);
            anchorGo.transform.localPosition = new Vector3(0f, 1.25f, 0f);

            // NavMeshObstacle (carves dynamically ONLY when locked/jammed!)
            NavMeshObstacle obs = doorRoot.AddComponent<NavMeshObstacle>();
            obs.shape = NavMeshObstacleShape.Box;
            obs.center = new Vector3(0f, height * 0.5f, 0f);
            obs.size = new Vector3(width, height, 0.35f);
            obs.carving = startsLocked;
            obs.enabled = startsLocked;

            if (addKeycardLed)
            {
                CreateBox(doorRoot.transform, "CardReader", new Vector3(width * 0.5f + 0.14f, 1.28f, -0.15f), new Vector3(0.10f, 0.16f, 0.04f), palette.DarkPaintedMetal, addCollider: false);
                CreateBox(doorRoot.transform, "CardReaderLED", new Vector3(width * 0.5f + 0.14f, 1.33f, -0.175f), new Vector3(0.03f, 0.02f, 0.01f), startsLocked ? palette.EmissiveRed : palette.EmissiveGreen, addCollider: false);
            }

            if (!string.IsNullOrEmpty(overheadSignText))
            {
                BuildIlluminatedSign(doorRoot.transform, $"Sign_{doorId}", new Vector3(0f, height + 0.24f, 0f), Vector3.zero, new Vector2(Mathf.Min(width * 0.9f, 1.35f), 0.26f), palette.EmissiveGreen, LightGroupId.VCExit, overheadSignText);
            }

            // Wire DoorController fields via SerializedObject cleanly
            DoorController ctrl = doorRoot.AddComponent<DoorController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("doorId").stringValue = doorId;
            so.FindProperty("mechanism").enumValueIndex = (int)mechanism;
            so.FindProperty("startsOpen").boolValue = startsOpen;
            so.FindProperty("startsLocked").boolValue = startsLocked;
            so.FindProperty("allowPlayerToggle").boolValue = mechanism == DoorMechanism.SingleHinged;
            so.FindProperty("lockedHint").stringValue = lockedHint;
            so.FindProperty("primaryLeaf").objectReferenceValue = primaryPivot;
            so.FindProperty("secondaryLeaf").objectReferenceValue = secondaryPivot;
            so.FindProperty("focusAnchor").objectReferenceValue = anchorGo.transform;
            so.FindProperty("primaryOpenAngleDeg").floatValue = primaryOpenAngleDeg;
            so.FindProperty("secondaryOpenAngleDeg").floatValue = secondaryOpenAngleDeg;
            if (slideOpenOffset != default) so.FindProperty("slideOpenOffset").vector3Value = slideOpenOffset;
            so.FindProperty("bashableByRaptor").boolValue = bashableByRaptor;
            so.FindProperty("soundPrefix").stringValue = soundPrefix;
            so.FindProperty("navObstacle").objectReferenceValue = obs;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameLayers.SetLayerRecursively(doorRoot, GameLayers.Door);
            return ctrl;
        }

        private static void BuildDoorLeafGeometry(
            Transform pivot,
            string name,
            float leafWidth,
            float leafHeight,
            float thickness,
            float dirSign,
            Material leafMat,
            Material handleMat,
            WorldMaterialPalette palette,
            bool isChainlinkGate)
        {
            float cx = dirSign * leafWidth * 0.5f;
            CreateBox(pivot, name, new Vector3(cx, leafHeight * 0.5f, 0f), new Vector3(leafWidth, leafHeight, thickness), isChainlinkGate ? palette.ChainlinkFence : leafMat, addCollider: true);

            if (isChainlinkGate)
            {
                // Perimeter steel gate bars
                CreateBox(pivot, $"{name}_TopBar", new Vector3(cx, leafHeight - 0.06f, 0f), new Vector3(leafWidth, 0.12f, thickness + 0.04f), palette.DarkPaintedMetal, addCollider: false);
                CreateBox(pivot, $"{name}_BotBar", new Vector3(cx, 0.08f, 0f), new Vector3(leafWidth, 0.12f, thickness + 0.04f), palette.DarkPaintedMetal, addCollider: false);
                CreateBox(pivot, $"{name}_MidBar", new Vector3(cx, leafHeight * 0.5f, 0f), new Vector3(leafWidth, 0.09f, thickness + 0.04f), palette.DarkPaintedMetal, addCollider: false);
            }
            else
            {
                // Stainless push-bar / lever handle on both sides + upper recessed viewport trim
                float handleX = dirSign * (leafWidth - 0.14f);
                CreateBox(pivot, "Handle_Front", new Vector3(handleX, 1.02f, -thickness * 0.5f - 0.035f), new Vector3(0.14f, 0.035f, 0.05f), handleMat, addCollider: false);
                CreateBox(pivot, "Handle_Back", new Vector3(handleX, 1.02f, +thickness * 0.5f + 0.035f), new Vector3(0.14f, 0.035f, 0.05f), handleMat, addCollider: false);
                CreateBox(pivot, "KickPlate", new Vector3(cx, 0.14f, 0f), new Vector3(leafWidth - 0.04f, 0.24f, thickness + 0.01f), handleMat, addCollider: false);
            }
        }

        /// <summary>
        /// Creates a room floor slab with proper SurfaceTag + PhysicsMaterial, ceiling slab, and RainBlocker roof proxy.
        /// </summary>
        public static GameObject BuildBoxRoomShell(
            Transform parent,
            string roomName,
            Vector3 centerXZ,
            float baseY,
            float widthX,
            float depthZ,
            float ceilingY,
            Material floorMat,
            Material ceilingMat,
            SurfaceType floorSurface,
            WorldMaterialPalette palette,
            bool buildCeiling = true,
            bool addRainBlocker = true)
        {
            var root = new GameObject(roomName);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(centerXZ.x, baseY, centerXZ.z);
            root.layer = GameLayers.Environment;

            // Solid Floor Slab (top surface at exact baseY!)
            GameObject floor = CreateBox(root.transform, "Floor", new Vector3(0f, -0.15f, 0f), new Vector3(widthX, 0.30f, depthZ), floorMat, addCollider: true);
            ApplySurface(floor, floorSurface, palette);
            GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic);

            if (buildCeiling)
            {
                GameObject ceil = CreateBox(root.transform, "Ceiling", new Vector3(0f, ceilingY + 0.12f, 0f), new Vector3(widthX, 0.24f, depthZ), ceilingMat, addCollider: true);
                GameObjectUtility.SetStaticEditorFlags(ceil, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic);
            }

            if (addRainBlocker)
            {
                BuildRainBlockerSlab(root.transform, "RainBlocker_Roof", new Vector3(0f, ceilingY + 0.28f, 0f), new Vector3(widthX + 0.4f, 0.18f, depthZ + 0.4f));
            }

            return root;
        }

        /// <summary>
        /// Builds the Lobby Atrium aligned structural steel-truss glass skylight roof (X=-50..-12, POS-11 fix!).
        /// Leaves ONLY brokenPaneIndices open in the RainBlocker so storm rain falls through broken skylight panes only!
        /// </summary>
        public static GameObject BuildCeilingWithSkylight(
            Transform parent,
            string name,
            Vector3 centerBasePos,
            float widthX,
            float depthZ,
            float roofY,
            int baysX,
            int baysZ,
            WorldMaterialPalette palette,
            HashSet<int> brokenPaneIndices = null)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(centerBasePos.x, roofY, centerBasePos.z);
            root.layer = GameLayers.Environment;

            float bayW = widthX / Mathf.Max(1, baysX);
            float bayD = depthZ / Mathf.Max(1, baysZ);
            float x0 = -widthX * 0.5f;
            float z0 = -depthZ * 0.5f;

            // Structural steel I-beam trusses running along Z and cross-purlins along X
            for (int ix = 0; ix <= baysX; ix++)
            {
                float lx = x0 + ix * bayW;
                CreateBox(root.transform, $"TrussZ_{ix}", new Vector3(lx, 0f, 0f), new Vector3(0.28f, 0.45f, depthZ), palette.DarkPaintedMetal, addCollider: true);
            }
            for (int iz = 0; iz <= baysZ; iz++)
            {
                float lz = z0 + iz * bayD;
                CreateBox(root.transform, $"PurlinX_{iz}", new Vector3(0f, 0.08f, lz), new Vector3(widthX, 0.26f, 0.22f), palette.DarkPaintedMetal, addCollider: false);
            }

            int paneIdx = 0;
            for (int ix = 0; ix < baysX; ix++)
            {
                for (int iz = 0; iz < baysZ; iz++)
                {
                    bool broken = brokenPaneIndices != null && brokenPaneIndices.Contains(paneIdx);
                    float cx = x0 + (ix + 0.5f) * bayW;
                    float cz = z0 + (iz + 0.5f) * bayD;

                    if (!broken)
                    {
                        GameObject glass = CreateBox(root.transform, $"SkylightPane_{ix}_{iz}", new Vector3(cx, 0.22f, cz), new Vector3(bayW - 0.22f, 0.04f, bayD - 0.22f), palette.GlassArchitectural, addCollider: true);
                        MeshRenderer mr = glass.GetComponent<MeshRenderer>();
                        if (mr != null) mr.shadowCastingMode = ShadowCastingMode.Off;

                        // Intact glass panes block storm rain via Layer 13 RainBlocker!
                        BuildRainBlockerSlab(root.transform, $"RainBlock_Pane_{ix}_{iz}", new Vector3(cx, 0.30f, cz), new Vector3(bayW, 0.12f, bayD));
                    }
                    else
                    {
                        // Jagged broken pane frame edge (no RainBlocker here -> realistic localized rain shaft!)
                        CreateBox(root.transform, $"BrokenEdge_{ix}_{iz}", new Vector3(cx - bayW * 0.35f, 0.22f, cz), new Vector3(bayW * 0.22f, 0.04f, bayD * 0.65f), palette.GlassArchitectural, addCollider: false);
                    }
                    paneIdx++;
                }
            }

            return root;
        }

        /// <summary>
        /// Builds a real structural steel-and-stone staircase with handrails (fixes Lobby unreached mezzanine!).
        /// Ascends along local +Z from local (0,0,0) to (0, totalRise, totalRun).
        /// </summary>
        public static GameObject BuildStaircase(
            Transform parent,
            string name,
            Vector3 bottomStartPos,
            float yawDeg,
            float width,
            float totalRise,
            float totalRun,
            int stepCount,
            WorldMaterialPalette palette,
            SurfaceType surface = SurfaceType.Concrete)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(bottomStartPos, Quaternion.Euler(0f, yawDeg, 0f));
            root.layer = GameLayers.Environment;

            int steps = Mathf.Max(4, stepCount);
            float rise = totalRise / steps;
            float tread = totalRun / steps;

            for (int i = 0; i < steps; i++)
            {
                float y = (i + 0.5f) * rise;
                float z = (i + 0.5f) * tread;
                GameObject step = CreateBox(root.transform, $"Step_{i:00}", new Vector3(0f, y, z), new Vector3(width, rise, tread + 0.03f), palette.ConcreteBoardform, addCollider: true);
                ApplySurface(step, surface, palette);
            }

            // Diagonal steel stringers + safety handrails on both sides
            float diagLen = Mathf.Sqrt(totalRise * totalRise + totalRun * totalRun);
            float slopeDeg = -Mathf.Atan2(totalRise, totalRun) * Mathf.Rad2Deg;
            for (int side = -1; side <= 1; side += 2)
            {
                float sx = side * (width * 0.5f + 0.05f);
                CreateBox(root.transform, $"Stringer_{side}", new Vector3(sx, totalRise * 0.5f - 0.12f, totalRun * 0.5f), new Vector3(0.10f, 0.28f, diagLen), palette.DarkPaintedMetal, addCollider: false, euler: new Vector3(slopeDeg, 0f, 0f));
                CreateBox(root.transform, $"Handrail_{side}", new Vector3(sx, totalRise * 0.5f + 0.96f, totalRun * 0.5f), new Vector3(0.06f, 0.06f, diagLen + 0.2f), palette.BrushedSteel, addCollider: true, euler: new Vector3(slopeDeg, 0f, 0f));
                for (int p = 0; p <= 4; p++)
                {
                    float frac = p / 4f;
                    CreateBox(root.transform, $"Post_{side}_{p}", new Vector3(sx, frac * totalRise + 0.48f, frac * totalRun), new Vector3(0.05f, 0.96f, 0.05f), palette.DarkPaintedMetal, addCollider: false);
                }
            }

            return root;
        }

        /// <summary>
        /// Builds diegetic color-coded power conduit pipe runs along walls/ceilings guiding players between rooms/objectives.
        /// </summary>
        public static GameObject BuildConduitRun(
            Transform parent,
            string name,
            Vector3[] worldWaypoints,
            float radius,
            Material conduitMat,
            LightGroupId? optionalLightGroup = null)
        {
            if (worldWaypoints == null || worldWaypoints.Length < 2) return null;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.layer = GameLayers.Environment;

            var renderers = new List<Renderer>();
            for (int i = 0; i < worldWaypoints.Length - 1; i++)
            {
                Vector3 a = worldWaypoints[i];
                Vector3 b = worldWaypoints[i + 1];
                Vector3 delta = b - a;
                float len = delta.magnitude;
                if (len < 0.02f) continue;

                GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cyl.name = $"Pipe_{i}";
                cyl.transform.SetParent(root.transform, false);
                cyl.transform.position = (a + b) * 0.5f;
                cyl.transform.up = delta / len;
                cyl.transform.localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
                MeshRenderer mr = cyl.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sharedMaterial = conduitMat;
                    renderers.Add(mr);
                }
                Collider c = cyl.GetComponent<Collider>();
                if (c != null) UnityEngine.Object.DestroyImmediate(c);
                cyl.layer = GameLayers.Environment;
            }

            if (optionalLightGroup.HasValue && renderers.Count > 0)
            {
                AttachLightGroupMember(root, optionalLightGroup.Value, null, renderers.ToArray());
            }

            return root;
        }

        /// <summary>
        /// Creates a physically calibrated HDRP Light (HDAdditionalLightData w/ Volumetric Fog dimmer, Contact Shadows,
        /// real shadows on key lights) registered cleanly to a facility LightGroupId.
        /// </summary>
        public static Light BuildHdrpLight(
            Transform parent,
            string name,
            Vector3 worldPos,
            Vector3 eulerAngles,
            LightType lightType,
            Color color,
            float intensityLumens,
            float range,
            LightGroupId lightGroup,
            float volumetricDimmer = 1.0f,
            bool castShadows = false,
            float spotOuterAngle = 60f,
            float colorTempK = 5500f,
            int sequenceOrder = 0,
            bool rotatingBeacon = false,
            bool pulsingEmergency = false,
            float blackoutBackupFactor = 0f,
            Renderer[] pairedEmissives = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(eulerAngles));

            Light light = go.AddComponent<Light>();
            light.type = lightType;
            light.color = color;
            light.intensity = intensityLumens;
            light.range = range;
            light.useColorTemperature = true;
            light.colorTemperature = colorTempK;
            light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
            if (lightType == LightType.Spot)
            {
                light.spotAngle = spotOuterAngle;
                light.innerSpotAngle = spotOuterAngle * 0.55f;
            }

            HDAdditionalLightData hd = go.AddComponent<HDAdditionalLightData>();
            hd.affectsVolumetric = volumetricDimmer > 0.01f;
            hd.SetLightDimmer(1f, volumetricDimmer);
            hd.SetColor(color, colorTempK);
            hd.useContactShadow.useOverride = true;
            hd.useContactShadow.@override = true;

            AttachLightGroupMember(go, lightGroup, new[] { light }, pairedEmissives, sequenceOrder, rotatingBeacon, pulsingEmergency, blackoutBackupFactor);
            return light;
        }

        public static LightGroupMember AttachLightGroupMember(
            GameObject target,
            LightGroupId groupId,
            Light[] lights = null,
            Renderer[] emissives = null,
            int sequenceOrder = 0,
            bool rotatingBeacon = false,
            bool pulsingEmergency = false,
            float blackoutBackupFactor = 0f)
        {
            LightGroupMember member = target.GetComponent<LightGroupMember>() ?? target.AddComponent<LightGroupMember>();
            var so = new SerializedObject(member);
            so.FindProperty("groupId").enumValueIndex = (int)groupId;
            so.FindProperty("sequenceOrder").intValue = sequenceOrder;
            so.FindProperty("rotatingBeacon").boolValue = rotatingBeacon;
            so.FindProperty("pulsingEmergency").boolValue = pulsingEmergency;
            so.FindProperty("blackoutBackupFactor").floatValue = blackoutBackupFactor;

            if (lights != null)
            {
                SerializedProperty lp = so.FindProperty("managedLights");
                lp.arraySize = lights.Length;
                for (int i = 0; i < lights.Length; i++) lp.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            }

            if (emissives != null)
            {
                SerializedProperty ep = so.FindProperty("emissiveRenderers");
                ep.arraySize = emissives.Length;
                for (int i = 0; i < emissives.Length; i++) ep.GetArrayElementAtIndex(i).objectReferenceValue = emissives[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return member;
        }

        /// <summary>
        /// Places a Box-Projected HDRP Reflection Probe + InteriorZoneVolume + RainBlocker for an interior room.
        /// Completely eliminates Godot's night-skybox-mirroring interior floors (VIS-2 fix!).
        /// </summary>
        public static ReflectionProbe BuildRoomReflectionProbe(
            Transform parent,
            string roomId,
            Vector3 roomCenter,
            Vector3 roomSize,
            string audioZoneId = "vc_offices",
            bool createInteriorVolume = true)
        {
            var probeGo = new GameObject($"ReflectionProbe_{roomId}");
            probeGo.transform.SetParent(parent, false);
            probeGo.transform.position = roomCenter;

            ReflectionProbe probe = probeGo.AddComponent<ReflectionProbe>();
            probe.boxProjection = true;
            probe.size = roomSize;

            HDAdditionalReflectionData hdRefl = probeGo.AddComponent<HDAdditionalReflectionData>();
            ConfigureTruthfulProbe(hdRefl, outdoor: false);
            hdRefl.influenceVolume.shape = InfluenceShape.Box;
            hdRefl.influenceVolume.boxSize = roomSize;
            hdRefl.influenceVolume.boxBlendDistancePositive = Vector3.one * 0.45f;
            hdRefl.influenceVolume.boxBlendDistanceNegative = Vector3.one * 0.45f;

            if (createInteriorVolume)
            {
                var volGo = new GameObject($"InteriorZone_{roomId}");
                volGo.transform.SetParent(parent, false);
                volGo.transform.position = roomCenter;
                volGo.layer = GameLayers.Trigger;
                BoxCollider bc = volGo.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = roomSize - new Vector3(0.3f, 0.2f, 0.3f);
                InteriorZoneVolume iz = volGo.AddComponent<InteriorZoneVolume>();
                var so = new SerializedObject(iz);
                so.FindProperty("zoneId").stringValue = audioZoneId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return probe;
        }

        /// <summary>
        /// Reflection probe settings shared by room probes and the Look Pass's outdoor probes: realtime OnDemand
        /// (re-rendered by <see cref="Cretacea.Rendering.ReflectionProbeRefresh"/> when light groups finish switching and
        /// at chapter start), physical multiplier 1, sky behind windows, and a culling mask without the first-person
        /// viewmodel and the invisible rain-blocker slabs.
        /// </summary>
        public static void ConfigureTruthfulProbe(HDAdditionalReflectionData hdRefl, bool outdoor)
        {
            hdRefl.mode = ProbeSettings.Mode.Realtime;
            hdRefl.realtimeMode = ProbeSettings.RealtimeMode.OnDemand;
            hdRefl.weight = 1.0f;
            hdRefl.multiplier = 1.0f;
            hdRefl.settingsRaw.cameraSettings.bufferClearing.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
            hdRefl.settingsRaw.cameraSettings.culling.cullingMask = ~(GameLayers.Mask(GameLayers.Viewmodel) | GameLayers.Mask(GameLayers.RainBlocker));
            // HDRP stores a cube < 512 px as a 4x octahedral tile and >= 512 px as 2x: 256 and 512 both take a 1024
            // tile of the 4096^2 atlas (16 tiles), 128 takes 512. 256 keeps the 6-face render cheap; the atlas burst
            // is prevented by ReflectionProbeRefresh (requests only in view, one per frame). Outdoor probes only feed
            // rough wet ground: 128 px.
            hdRefl.settingsRaw.cubeResolution.useOverride = true;
            hdRefl.settingsRaw.cubeResolution.@override = outdoor ? CubeReflectionResolution.CubeReflectionResolution128 : CubeReflectionResolution.CubeReflectionResolution256;
            var refresh = hdRefl.GetComponent<Cretacea.Rendering.ReflectionProbeRefresh>() ?? hdRefl.gameObject.AddComponent<Cretacea.Rendering.ReflectionProbeRefresh>();
            refresh.Outdoor = outdoor;
            EditorUtility.SetDirty(hdRefl);
            EditorUtility.SetDirty(refresh);
        }

        public static GameObject BuildRainBlockerSlab(Transform parent, string name, Vector3 localPos, Vector3 size)
        {
            GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = name;
            blocker.transform.SetParent(parent, false);
            blocker.transform.localPosition = localPos;
            blocker.transform.localScale = size;
            blocker.layer = GameLayers.RainBlocker;

            // Invisible to main cameras (only rendered by StormRainOcclusionController's Layer 13 camera!)
            MeshRenderer mr = blocker.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = ShadowCastingMode.Off;
                // fully alpha-clipped: never visible in a colour camera or reflection probe that forgets to mask layer 13
                Material hidden = Look.LookRoofs.RainBlockerMaterial();
                if (hidden != null) mr.sharedMaterial = hidden;
            }
            Collider c = blocker.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.DestroyImmediate(c);
            return blocker;
        }

        /// <summary>
        /// A real sign: the artwork that matches the sign's text or name (Assets/Art/UI/Signs, see LookSigns) on a
        /// face quad in front of a dark backing plate, on both sides. Texts with no artwork are written on the plate
        /// with a world-space TextMeshPro label. Exit signs are back-lit (glow, paired with <paramref name="groupId"/>);
        /// room plaques are printed and only lit by the room. The Look Pass later moves faces out of the wall a
        /// door sign is embedded in and removes faces that look into a wall.
        /// </summary>
        public static GameObject BuildIlluminatedSign(
            Transform parent,
            string name,
            Vector3 localPos,
            Vector3 localEuler,
            Vector2 size,
            Material signMat,
            LightGroupId groupId,
            string text = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.layer = GameLayers.Environment;

            string stem = Look.LookSigns.ResolveStem(text, name, signMat);
            bool backlit = Look.LookSigns.IsBacklit(stem);
            Material faceMat = stem != null ? Look.LookSigns.SignMaterial(stem, backlit) : null;
            Vector2 faceSize = stem != null ? Look.LookSigns.FitToArt(stem, size) : size;
            Material plateMat = Look.LookSigns.PlateMaterial() ?? signMat;
            const float plateT = 0.02f;
            var emissives = new List<Renderer>();
            // a printed door plaque names the room behind the door: one face, on the approach side (a framed door's
            // local -Z, the corridor for every door of the kit), never a second copy inside the room it names.
            // Back-lit exit signs stay double-faced.
            bool printedDoorPlaque = !backlit && parent != null && parent.name.StartsWith("Door_");
            int sides = printedDoorPlaque ? 1 : 2;

            for (int side = 0; side < sides; side++)
            {
                // side 0 is seen from local -Z (a Quad's visible side), side 1 from +Z
                float s = side == 0 ? -1f : 1f;
                string suffix = side == 0 ? "Front" : "Back";
                Quaternion faceRot = side == 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);

                GameObject plate = CreateBox(go.transform, $"SignPlate_{suffix}", new Vector3(0f, 0f, s * plateT * 0.5f),
                    new Vector3(faceSize.x + 0.05f, faceSize.y + 0.05f, plateT), plateMat, addCollider: false);
                plate.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

                GameObject face = GameObject.CreatePrimitive(PrimitiveType.Quad);
                face.name = $"SignFace_{suffix}";
                face.layer = GameLayers.Environment;
                face.transform.SetParent(go.transform, false);
                face.transform.localPosition = new Vector3(0f, 0f, s * (plateT + 0.003f));
                face.transform.localRotation = faceRot;
                face.transform.localScale = new Vector3(faceSize.x, faceSize.y, 1f);
                Collider fc = face.GetComponent<Collider>();
                if (fc != null) UnityEngine.Object.DestroyImmediate(fc);
                MeshRenderer fr = face.GetComponent<MeshRenderer>();
                fr.shadowCastingMode = ShadowCastingMode.Off;

                if (faceMat != null)
                {
                    fr.sharedMaterial = faceMat;
                    if (backlit) emissives.Add(fr);
                }
                else
                {
                    // no artwork: dark plate face + the words on it
                    fr.sharedMaterial = plateMat;
                    BuildSignLabel(go.transform, $"SignLabel_{suffix}", string.IsNullOrEmpty(text) ? Look.LookSigns.TextFromName(name) : text,
                        new Vector3(0f, 0f, s * (plateT + 0.006f)), faceRot, faceSize);
                }
            }

            if (emissives.Count > 0) AttachLightGroupMember(go, groupId, null, emissives.ToArray());
            return go;
        }

        private static void BuildSignLabel(Transform signRoot, string name, string text, Vector3 localPos, Quaternion localRot, Vector2 faceSize)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) return;
            var go = new GameObject(name);
            go.layer = GameLayers.Environment;
            go.transform.SetParent(signRoot, false);
            go.transform.localPosition = localPos;
            // TextMeshPro faces +Z readers by default the same way a Quad does (visible from -Z)
            go.transform.localRotation = localRot;
            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            tmp.font = font;
            Material labelMat = Look.LookSigns.LabelMaterial(font);
            if (labelMat != null) tmp.fontSharedMaterial = labelMat;
            tmp.text = text;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.2f;
            tmp.fontSizeMax = 8f;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.color = new Color(0.92f, 0.92f, 0.88f, 1f);
            tmp.rectTransform.sizeDelta = new Vector2(faceSize.x * 0.9f, faceSize.y * 0.8f);
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        public static DecalProjector BuildDecal(
            Transform parent,
            string name,
            Vector3 worldPos,
            Vector3 eulerAngles,
            Vector3 size,
            Material decalMat,
            float fadeFactor = 0.85f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(eulerAngles));

            DecalProjector dp = go.AddComponent<DecalProjector>();
            dp.material = decalMat;
            dp.size = size;
            dp.fadeFactor = fadeFactor;
            dp.drawDistance = 65f;
            return dp;
        }

        public static WorldAnchor BuildWorldAnchor(Transform parent, string anchorId, Vector3 worldPos, float yawDeg = 0f, bool registerObject = true)
        {
            var go = new GameObject($"Anchor_{anchorId}");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0f, yawDeg, 0f));
            WorldAnchor wa = go.AddComponent<WorldAnchor>();
            var so = new SerializedObject(wa);
            so.FindProperty("anchorId").stringValue = anchorId;
            so.FindProperty("registerAsNamedObject").boolValue = registerObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            return wa;
        }

        public static StoryTriggerVolume BuildStoryTrigger(Transform parent, string triggerId, Vector3 worldPos, Vector3 size, bool oneShot = true)
        {
            var go = new GameObject($"Trigger_{triggerId}");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.layer = GameLayers.Trigger;
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = size;
            StoryTriggerVolume tv = go.AddComponent<StoryTriggerVolume>();
            var so = new SerializedObject(tv);
            so.FindProperty("triggerId").stringValue = triggerId;
            so.FindProperty("oneShot").boolValue = oneShot;
            so.ApplyModifiedPropertiesWithoutUndo();
            return tv;
        }

        public static GameObject BuildChainlinkFenceSpan(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            float baseY,
            float height,
            WorldMaterialPalette palette,
            bool addDinoBlocker = true,
            float postSpacing = 4.0f)
        {
            Vector3 delta = end - start;
            delta.y = 0f;
            float len = delta.magnitude;
            if (len < 0.2f) return null;

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(start.x, baseY, start.z), Quaternion.LookRotation(delta / len, Vector3.up));
            root.layer = GameLayers.Environment;

            // Chainlink mesh span
            CreateBox(root.transform, "MeshSpan", new Vector3(0f, height * 0.5f, len * 0.5f), new Vector3(0.08f, height, len), palette.ChainlinkFence, addCollider: true);
            CreateBox(root.transform, "TopRail", new Vector3(0f, height - 0.06f, len * 0.5f), new Vector3(0.10f, 0.10f, len), palette.DarkPaintedMetal, addCollider: false);
            CreateBox(root.transform, "MidRail", new Vector3(0f, height * 0.5f, len * 0.5f), new Vector3(0.08f, 0.08f, len), palette.DarkPaintedMetal, addCollider: false);

            int posts = Mathf.Max(2, Mathf.CeilToInt(len / postSpacing) + 1);
            for (int i = 0; i < posts; i++)
            {
                float pz = (i / (float)(posts - 1)) * len;
                CreateBox(root.transform, $"Post_{i}", new Vector3(0f, height * 0.5f, pz), new Vector3(0.14f, height + 0.35f, 0.14f), palette.DarkPaintedMetal, addCollider: false);
            }

            if (addDinoBlocker)
            {
                var blk = new GameObject("DinoBlocker_Span");
                blk.transform.SetParent(root.transform, false);
                blk.transform.localPosition = new Vector3(0f, height * 0.5f, len * 0.5f);
                blk.layer = GameLayers.DinoBlocker;
                BoxCollider bc = blk.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.6f, height + 1.0f, len);
            }

            return root;
        }

        public static GameObject CreateBox(
            Transform parent,
            string name,
            Vector3 localPos,
            Vector3 size,
            Material mat,
            bool addCollider = true,
            Vector3? euler = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.transform.localScale = size;
            go.layer = GameLayers.Environment;

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;

            if (!addCollider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) UnityEngine.Object.DestroyImmediate(c);
            }
            return go;
        }

        public static void ApplySurface(GameObject go, SurfaceType surface, WorldMaterialPalette palette)
        {
            if (go == null) return;
            SurfaceTag tag = go.GetComponent<SurfaceTag>() ?? go.AddComponent<SurfaceTag>();
            var so = new SerializedObject(tag);
            so.FindProperty("surfaceType").enumValueIndex = (int)surface;
            so.ApplyModifiedPropertiesWithoutUndo();

            Collider col = go.GetComponent<Collider>();
            if (col != null && palette != null)
            {
                PhysicsMaterial pm = palette.GetSurfacePhysicsMaterial(surface);
                if (pm != null) col.sharedMaterial = pm;
            }
        }
    }
}
