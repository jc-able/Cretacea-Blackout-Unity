using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Cretacea.UI
{
    /// <summary>
    /// A 1990s park-service map for the field tablet, drawn from the world itself: a warm hillshade with contour
    /// lines sampled from the island terrain (sea, lake and the Tyrant Valley stream in muted blue), buildings and
    /// fences at their real footprints, labels kept clear of the linework, a compass and a scale bar, and live
    /// markers placed by true world XZ. Two layouts: the whole park route and the Visitor Center floor plan.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class ParkMapView : MonoBehaviour
    {
        public enum Layout { Park, VisitorCenter }

        // ---- palette (sRGB; the overlay UI is stored in 8-bit linear, so dark tones are picked on its steps) ----
        private static readonly Color Ink = new Color(0.93f, 0.90f, 0.84f, 1f);
        private static readonly Color InkMuted = new Color(0.68f, 0.64f, 0.57f, 1f);
        private static readonly Color InkFaint = new Color(0.55f, 0.51f, 0.44f, 0.85f);
        private static readonly Color Wall = new Color(0.60f, 0.55f, 0.45f, 1f);
        private static readonly Color BuildingFill = new Color(0.20f, 0.18f, 0.15f, 1f);
        private static readonly Color RoomFill = new Color(0.135f, 0.11f, 0.086f, 1f);
        private static readonly Color RoadCasing = new Color(0.27f, 0.24f, 0.19f, 1f);
        private static readonly Color Fence = new Color(0.62f, 0.57f, 0.47f, 0.75f);
        private static readonly Color Water = new Color(0.36f, 0.52f, 0.60f, 1f);
        private static readonly Color Amber = CretaceaUiFactory.AccentAmber;
        private static readonly Color RouteDone = new Color(0.62f, 0.57f, 0.47f, 0.9f);
        private static readonly Color Green = CretaceaUiFactory.AccentGreen;
        private static readonly Color SheetFill = new Color(0.051f, 0.051f, 0.051f, 1f);   // #0D0D0D, neutral step
        private static readonly Color GridLine = new Color(0.11f, 0.086f, 0.05f, 1f);      // #1C160D, opaque warm step
        private static readonly Color OutdoorFill = new Color(0.16f, 0.14f, 0.11f, 1f);

        // ---- world data (metres, XZ; from the zone builders) -------------------------------------------------
        public static readonly Rect ParkWorld = new Rect(-112f, -262f, 800f, 515.5f);   // out to the sea east of the coast
        public static readonly Rect VisitorCenterWorld = new Rect(-86f, -41f, 122f, 79f);
        public static readonly Rect VisitorCenterBounds = new Rect(-73f, -33f, 96f, 68f);   // building + dock

        private const float SeaLevel = -40f, LakeLevel = -7.65f;
        private static readonly Vector2 LakeCentre = new Vector2(-62f, -205f);
        private static float StreamX(float z) => 205f + 18f * Mathf.Sin(-z / 45f) + 6f * Mathf.Sin(-z / 13f);

        // Walking route after the manual: Records Annex -> Feed Prep -> dock -> service path -> Paddock 4 gate ->
        // Tyrant Valley -> Substation 3 -> green route lights -> Old Yard -> E-0 -> coast.
        private static readonly Vector2[] RouteToSubstation =
        {
            // Records door (x -9) -> corridor -> door_wing (x -2) -> Feed Prep -> door_vet (x 3) -> vet arch -> dock roller (z -11)
            new Vector2(-6f, 26f), new Vector2(-9f, 24f), new Vector2(-9f, 20.5f), new Vector2(-2f, 20.5f), new Vector2(-2f, 15f),
            new Vector2(3f, 6f), new Vector2(3f, -11f), new Vector2(8f, -11f), new Vector2(15f, -10f), new Vector2(40f, -9f), new Vector2(40f, 14f), new Vector2(64f, 14f),
            new Vector2(78f, -2f), new Vector2(94f, -2f), new Vector2(128f, 0f), new Vector2(170f, 2f), new Vector2(201f, 4f),
            new Vector2(230f, 2f), new Vector2(250f, 0f), new Vector2(266f, 0f)
        };
        private static readonly Vector2[] RouteToExit =
        {
            new Vector2(266f, 0f), new Vector2(286f, 0f), new Vector2(289f, 2f), new Vector2(303f, -9f), new Vector2(321f, -7f),
            new Vector2(335f, -18f), new Vector2(353f, -15f), new Vector2(368f, -24f), new Vector2(384f, -18f), new Vector2(397f, -25f),
            new Vector2(401f, -23f), new Vector2(470f, -26f), new Vector2(486f, -46f), new Vector2(505f, -46f)
        };
        private static readonly Vector2[] RouteToCoast = { new Vector2(505f, -46f), new Vector2(543f, -46f) };
        private static readonly Vector2[] ServiceRoad =
        {
            new Vector2(22f, -9f), new Vector2(40f, -9f), new Vector2(40f, 14f), new Vector2(64f, 14f), new Vector2(78f, -2f), new Vector2(92f, -3f)
        };

        private struct Room { public string Name; public Rect R; public Room(string n, float x0, float x1, float z0, float z1) { Name = n; R = Rect.MinMaxRect(x0, z0, x1, z1); } }
        private static readonly Room[] Rooms =
        {
            new Room("SECURITY\nOFFICE", -72f, -58f, 22f, 34f), new Room("LOCKERS", -58f, -48f, 22f, 34f),
            new Room("CONTROL ROOM", -48f, -26f, 22f, 34f), new Room("SERVER", -26f, -18f, 22f, 34f),
            new Room("RECORDS\nANNEX", -18f, 0f, 22f, 34f), new Room("STORAGE", 0f, 8f, 22f, 34f),
            new Room("", -72f, 8f, 19f, 22f),
            new Room("BREAK ROOM", -72f, -50f, 6f, 19f), new Room("STAFF EXIT", -72f, -50f, -12f, -8f),
            new Room("GIFT SHOP", -72f, -50f, -32f, -12f), new Room("LOBBY ATRIUM", -50f, -12f, -32f, 19f),
            new Room("FEED PREP", -12f, 8f, 3f, 19f), new Room("COLD\nSTORE", -12f, -2f, -9f, 3f),
            new Room("VET", -2f, 8f, -9f, 3f), new Room("", -12f, 8f, -13f, -9f),
            new Room("LOADING\nDOCK", 8f, 22f, -18f, -2f),
        };

        // Doorways (world XZ centre, clear width, true = wall runs along X). From Zone1_VisitorCenterBuilder's openings.
        private static readonly Vector4[] Doors =
        {
            new Vector4(-62.5f, 22f, 1.0f, 1f), new Vector4(-53f, 22f, 1.0f, 1f), new Vector4(-37f, 22f, 1.6f, 1f),
            new Vector4(-22f, 22f, 1.0f, 1f), new Vector4(-9f, 22f, 1.0f, 1f), new Vector4(4f, 22f, 1.0f, 1f),
            new Vector4(-61f, 19f, 1.0f, 1f), new Vector4(-31f, 19f, 2.4f, 1f), new Vector4(-2f, 19f, 1.2f, 1f),
            new Vector4(-6.8f, 3f, 1.4f, 1f), new Vector4(3f, 3f, 1.4f, 1f), new Vector4(-2f, -3f, 1.2f, 0f),
            new Vector4(-6.8f, -9f, 1.4f, 1f), new Vector4(3f, -9f, 1.4f, 1f), new Vector4(8f, -11f, 3.0f, 0f),
            new Vector4(-72f, -9.8f, 1.6f, 0f), new Vector4(-50f, -10f, 2.0f, 0f), new Vector4(-32f, -32f, 6.0f, 1f),
        };
        // Inside the outer walls but no room: the service void between the break room and the staff exit.
        private static readonly Rect ServiceVoid = Rect.MinMaxRect(-72f, -8f, -50f, 6f);
        // Building outline (the yard south of the east wing, x -12..8 z -32..-13, is outdoors).
        private static readonly Vector2[] BuildingOutline =
        {
            new Vector2(-72f, -32f), new Vector2(-12f, -32f), new Vector2(-12f, -13f), new Vector2(8f, -13f),
            new Vector2(8f, 34f), new Vector2(-72f, 34f), new Vector2(-72f, -32f)
        };

        // ---- instance ---------------------------------------------------------------------------------------
        public Layout Kind { get; private set; }
        private RectTransform _rt;
        private ParkMapGraphic _base, _route;
        private RectTransform _labels;
        private RectTransform _player, _playerArrow, _beacon;
        private TextMeshProUGUI _youLabel;
        private TextMeshProUGUI _scaleLabel;
        private RectTransform _scaleBar;
        private int _routeStage = -1;
        private Vector2 _highlightTarget = new Vector2(float.NaN, float.NaN);
        private Image _highlightRoom;

        public static ParkMapView Create(RectTransform parent, Layout layout)
        {
            var go = new GameObject(layout == Layout.Park ? "ParkMap" : "VisitorCenterPlan", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.AddComponent<RectMask2D>();
            ParkMapView v = go.AddComponent<ParkMapView>();
            v.Build(rt, layout);
            return v;
        }

        private void Build(RectTransform rt, Layout layout)
        {
            _rt = rt;
            Kind = layout;
            Rect world = layout == Layout.Park ? ParkWorld : VisitorCenterWorld;

            // Ground: hillshaded terrain for the park, a dark drafting sheet with a 5 m grid for the floor plan.
            if (layout == Layout.Park)
            {
                s_ParkViews++;
                _holdsTerrain = true;
                var bg = new GameObject("Terrain", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                RectTransform brt = bg.GetComponent<RectTransform>();
                brt.SetParent(rt, false);
                RawImage ri = bg.GetComponent<RawImage>();
                ri.raycastTarget = false;
                ri.texture = ParkTerrainTexture.Get(ParkWorld);
                ri.color = Color.white;
                FitToWorld(brt, ParkWorld);
            }
            else
            {
                Image sheet = CretaceaUiFactory.CreatePanel(rt, "Sheet", SheetFill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<Image>();
                sheet.raycastTarget = false;
            }

            _base = NewLayer(rt, "Base", world);
            _route = NewLayer(rt, "Route", world);
            if (layout == Layout.Park) DrawPark(_base); else DrawVisitorCenter(_base);

            var lg = new GameObject("Labels", typeof(RectTransform));
            _labels = lg.GetComponent<RectTransform>();
            _labels.SetParent(rt, false);
            _labels.anchorMin = Vector2.zero; _labels.anchorMax = Vector2.one;
            _labels.offsetMin = _labels.offsetMax = Vector2.zero;
            if (layout == Layout.Park) LabelPark(); else LabelVisitorCenter();

            BuildMarkers(rt);
            BuildCompassAndScale(rt, layout);
        }

        private static ParkMapGraphic NewLayer(RectTransform parent, string name, Rect world)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ParkMapGraphic));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            ParkMapGraphic g = go.GetComponent<ParkMapGraphic>();
            g.raycastTarget = false;
            g.World = world;
            return g;
        }

        /// <summary>Sizes a child so that it covers exactly the world rect inside the (uniformly fitted) map.</summary>
        private void FitToWorld(RectTransform child, Rect world)
        {
            var fitter = child.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = world.width / world.height;
        }

        // ---- park layout -------------------------------------------------------------------------------------

        private static void DrawPark(ParkMapGraphic g)
        {
            // Tyrant Valley stream
            var stream = new Vector2[41];
            for (int i = 0; i < stream.Length; i++) { float z = -200f + i * 10f; stream[i] = new Vector2(StreamX(z), z); }
            g.Line(Water, 3f, stream);

            // Paddock fences (dashed) and the Talon Ridge pen
            g.Outline(Fence, 2f, 92f, 397f, -175f, 170f, 9f, 6f);
            g.Outline(Fence, 2f, -90f, 28f, 41f, 134f, 9f, 6f);

            // Roads: service path, coast road
            g.Line(RoadCasing, 7f, ServiceRoad);
            g.Line(RoadCasing, 6f, new Vector2(505f, -46f), new Vector2(547f, -46f));

            // Buildings at their footprints
            foreach (Room r in Rooms) g.Box(BuildingFill, r.R.xMin, r.R.xMax, r.R.yMin, r.R.yMax);
            g.Outline(Wall, 1.5f, -72f, 8f, -32f, 34f);
            g.Box(BuildingFill, 241f, 293f, -33f, 26f); g.Outline(Wall, 1.5f, 241f, 293f, -33f, 26f);   // Substation 3
            g.Outline(Wall, 2f, 401f, 505f, -95f, 50f);                                               // yard wall
            g.Box(BuildingFill, 414f, 456f, 4f, 36f); g.Outline(Wall, 1.5f, 414f, 456f, 4f, 36f);       // Warehouse B
            g.Box(BuildingFill, 401f, 466f, -23f, -9f); g.Outline(Wall, 1.5f, 401f, 466f, -23f, -9f);   // workshop
            g.Box(BuildingFill, 89f, 95f, -7f, 1f);                                                   // gate house
            // watchtowers: a small ring with a dot (a 6 m square read as a dead pixel)
            foreach (Vector2 t in new[] { new Vector2(108f, 150f), new Vector2(236f, -150f), new Vector2(382f, 140f), new Vector2(140f, -120f) })
            {
                g.Dot(InkMuted, t, 10f);
                g.Dot(new Color(0.10f, 0.086f, 0.066f, 1f), t, 6.5f);
                g.Dot(InkMuted, t, 3f);
            }
        }

        private void LabelPark()
        {
            Label("VISITOR CENTER", new Vector2(-32f, -44f), 19f, Ink, top: true);
            Label("TALON RIDGE", new Vector2(-31f, 100f), 18f, InkMuted);
            Label("RAPTOR PEN", new Vector2(-31f, 84f), 18f, InkFaint);
            Label("TITAN MEADOWS", new Vector2(-40f, -158f), 18f, InkMuted);
            Label("SERVICE PATH", new Vector2(52f, 37f), 18f, InkMuted);   // clear of the route bend at z 14
            Label("PADDOCK 4 GATE", new Vector2(93f, -14f), 18f, InkMuted, top: true);
            // east of the stream (x ~202) and west of the paddock fence (x 397)
            Label("TYRANT VALLEY", new Vector2(295f, 128f), 24f, InkFaint, spacing: 8f);
            Label("PADDOCK 4", new Vector2(295f, 106f), 18f, InkFaint, spacing: 5f);
            Label("SUBSTATION 3", new Vector2(267f, 34f), 19f, Ink, bottom: true);
            Label("OLD MAINTENANCE YARD", new Vector2(453f, 60f), 17f, Ink, bottom: true);
            Label("WHSE B", new Vector2(435f, 20f), 15f, InkMuted, spacing: 1f);   // fits inside its 42 m box
            Label("EXIT E-0", new Vector2(514f, -40f), 18f, Green, left: true, bottom: true);
            Label("TORMENTA\nBAY", new Vector2(640f, -150f), 18f, new Color(0.62f, 0.74f, 0.80f, 0.95f), spacing: 4f);
            Label("DAWN COAST", new Vector2(548f, -74f), 18f, InkMuted, top: true);
        }

        // ---- Visitor Center floor plan ----------------------------------------------------------------------

        private void DrawVisitorCenter(ParkMapGraphic g)
        {
            for (float x = -85f; x <= 35f; x += 5f) g.Line(GridLine, 1f, new Vector2(x, -41f), new Vector2(x, 38f));
            for (float z = -40f; z <= 38f; z += 5f) g.Line(GridLine, 1f, new Vector2(-86f, z), new Vector2(36f, z));

            // Plaza and the service yard south of the east wing (outdoors)
            g.Box(OutdoorFill, -72f, 22f, -41f, -32f);
            g.Box(OutdoorFill, -12f, 8f, -32f, -13f);
            foreach (Room r in Rooms) g.Box(RoomFill, r.R.xMin, r.R.xMax, r.R.yMin, r.R.yMax);
            // No-access service void: room fill with a 45-degree hatch, clipped to the cell
            g.Box(RoomFill, ServiceVoid.xMin, ServiceVoid.xMax, ServiceVoid.yMin, ServiceVoid.yMax);
            Hatch(g, ServiceVoid, 2.5f, new Color(Wall.r, Wall.g, Wall.b, 0.35f));
            // highlighted objective room goes between fill and walls
            var hl = CretaceaUiFactory.CreatePanel(g.rectTransform.parent, "RoomHighlight", new Color(Amber.r, Amber.g, Amber.b, 0.16f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            hl.SetSiblingIndex(g.rectTransform.GetSiblingIndex() + 1);
            _highlightRoom = hl.GetComponent<Image>();
            _highlightRoom.raycastTarget = false;
            _highlightRoom.enabled = false;
            foreach (Room r in Rooms) g.Outline(Wall, 2f, r.R.xMin, r.R.xMax, r.R.yMin, r.R.yMax);
            g.Outline(Wall, 2f, ServiceVoid.xMin, ServiceVoid.xMax, ServiceVoid.yMin, ServiceVoid.yMax);
            g.Line(Ink, 3f, BuildingOutline);
            // Doorways: cut the wall lines open (at least 1.6 m so a door reads at this scale) and mark the jambs
            foreach (Vector4 d in Doors)
            {
                float half = Mathf.Max(1.6f, d.z) * 0.5f;
                bool alongX = d.w > 0.5f;
                Color gap = d.y <= -31.9f ? OutdoorFill : RoomFill;
                if (alongX) g.Box(gap, d.x - half, d.x + half, d.y - 0.45f, d.y + 0.45f);
                else g.Box(gap, d.x - 0.45f, d.x + 0.45f, d.y - half, d.y + half);
                Vector2 a = alongX ? new Vector2(d.x - half, d.y) : new Vector2(d.x, d.y - half);
                Vector2 b = alongX ? new Vector2(d.x + half, d.y) : new Vector2(d.x, d.y + half);
                Vector2 n = alongX ? new Vector2(0f, 0.45f) : new Vector2(0.45f, 0f);
                g.Line(Wall, 2f, a - n, a + n);
                g.Line(Wall, 2f, b - n, b + n);
            }
        }

        /// <summary>45-degree hatch lines clipped to a world rect.</summary>
        private static void Hatch(ParkMapGraphic g, Rect r, float step, Color c)
        {
            for (float k = -r.height; k < r.width; k += step)
            {
                // line x = r.xMin + k + t, z = r.yMin + t
                float t0 = Mathf.Max(0f, -k), t1 = Mathf.Min(r.height, r.width - k);
                if (t1 <= t0) continue;
                g.Line(c, 1f, new Vector2(r.xMin + k + t0, r.yMin + t0), new Vector2(r.xMin + k + t1, r.yMin + t1));
            }
        }

        private const float PlanPixelsPerMetre = 7.37f;   // 582 ref px of viewport height over the plan's 79 m

        private void LabelVisitorCenter()
        {
            foreach (Room r in Rooms)
            {
                if (string.IsNullOrEmpty(r.Name)) continue;
                bool narrow = r.R.width < 11f;
                // north-wing rooms carry their name high, so the objective beacon (low in the room) never covers it;
                // Feed Prep and the dock carry theirs clear of the route through them
                Vector2 at = r.R.yMin >= 21f ? new Vector2(r.R.center.x, 30.2f) : r.R.center;
                string name = r.Name;
                if (name == "FEED PREP") { name = "FEED\nPREP"; at = new Vector2(-7f, 12f); }
                bool topAnchored = false;
                if (name == "LOADING\nDOCK") { at = new Vector2(15f, -2.85f); topAnchored = true; }   // ~6 px under the dock's top wall (z -2), clear of the route
                else if (name == "VET") at = new Vector2(5.4f, -6f);   // between the route (vet doors at x 3) and the dock wall at x 8
                TextMeshProUGUI t = Label(name, at, narrow ? 18f : 20f, InkMuted, top: topAnchored, spacing: narrow ? 0f : 2f);
                // keep the name inside its walls at the tablet's scale (~7.4 px per metre) with ~6 px of air each
                // side: shrink to 16 px, then tighten the tracking, then go down to 13 px for the narrowest rooms
                float room = r.R.width * PlanPixelsPerMetre - (name == "VET" ? 30f : 12f);
                while (t.fontSize > 16f && t.GetPreferredValues(name).x > room) t.fontSize -= 1f;
                if (t.GetPreferredValues(name).x > room) t.characterSpacing = -1f;
                while (t.fontSize > 13f && t.GetPreferredValues(name).x > room) t.fontSize -= 1f;
                _roomLabels[r.Name] = t;
            }
            _corridorLabel = Label("STAFF CORRIDOR", CorridorLabelAt(new Vector2(9999f, 0f)), 18f, InkFaint, spacing: 2f);
            _corridorLabelIndex = _labelTexts.Count - 1;
            Label("MAIN PLAZA", new Vector2(-25f, -37f), 18f, InkFaint, spacing: 2f);
            Label("SERVICE\nPATH", new Vector2(29f, -5f), 18f, InkFaint, spacing: 2f);
        }

        // The Staff Corridor (x -72..8, z 19..22) is where the player opens the tablet most: its name sits in the half
        // of the corridor away from the player, so the YOU dot never lands on it (review 3: a ghost 'AFF CORRIDOR').
        private static readonly Rect StaffCorridor = Rect.MinMaxRect(-72f, 19f, 8f, 22f);
        private TextMeshProUGUI _corridorLabel;
        private int _corridorLabelIndex = -1;

        private static Vector2 CorridorLabelAt(Vector2 playerXZ)
        {
            if (playerXZ.x < -72f || playerXZ.x > 8f) return new Vector2(-32f, 20.5f);
            return new Vector2(playerXZ.x < -32f ? -8f : -57f, 20.5f);
        }

        // ---- labels -----------------------------------------------------------------------------------------

        private TextMeshProUGUI Label(string text, Vector2 world, float size, Color c, bool top = false, bool bottom = false, bool left = false, float spacing = 2f)
        {
            TextMeshProUGUI t = CretaceaUiFactory.CreateText(_labels, "L_" + text.Replace("\n", " "), text, size, c, UiFont.Label, TextAlignmentOptions.Center);
            t.characterSpacing = spacing;
            t.lineSpacing = -18f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform rt = t.rectTransform;
            rt.sizeDelta = new Vector2(260f, 60f);
            rt.pivot = new Vector2(left ? 0f : 0.5f, top ? 1f : (bottom ? 0f : 0.5f));
            t.alignment = left ? (bottom ? TextAlignmentOptions.BottomLeft : (top ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft))
                               : (top ? TextAlignmentOptions.Top : (bottom ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Center));
            Place(rt, world);
            _labelWorld.Add(new System.Collections.Generic.KeyValuePair<RectTransform, Vector2>(rt, world));
            _labelTexts.Add(t);
            _labelExtents.Add(Vector2.zero);
            return t;
        }

        private readonly System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<RectTransform, Vector2>> _labelWorld =
            new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<RectTransform, Vector2>>();
        private readonly System.Collections.Generic.List<TextMeshProUGUI> _labelTexts = new System.Collections.Generic.List<TextMeshProUGUI>();
        // Text extents measured from the font (textBounds is empty until a label's mesh has been built, which is
        // why the near-player fade never fired on the first tablet open).
        private readonly System.Collections.Generic.List<Vector2> _labelExtents = new System.Collections.Generic.List<Vector2>();
        private readonly System.Collections.Generic.Dictionary<string, TextMeshProUGUI> _roomLabels =
            new System.Collections.Generic.Dictionary<string, TextMeshProUGUI>();
        private TextMeshProUGUI _hlLabel;
        // Bright ink for the highlighted (target) room's name: the highlight is a 16 % amber wash over the dark plan,
        // so cream holds about 6.5:1 where the muted grey held 3.3:1 (near-black ink would drop to about 2:1).
        private static readonly Color InkOnAmber = new Color(0.97f, 0.94f, 0.88f, 1f);

        private void Place(RectTransform rt, Vector2 world)
        {
            Vector2 n = _base.ToNormalized(world);
            rt.anchorMin = rt.anchorMax = n;
            rt.anchoredPosition = Vector2.zero;
        }

        private static int s_ParkViews;
        private bool _holdsTerrain;

        private void OnDestroy()
        {
            // the terrain texture is shared and DontSave: the last park view frees it
            if (_holdsTerrain && --s_ParkViews <= 0) { s_ParkViews = 0; ParkTerrainTexture.Release(); }
        }

        private Vector2 _lastSize;
        private void LateUpdate()
        {
            // Anchors depend on the fitted size: re-place labels if the map was resized.
            Vector2 size = _rt.rect.size;
            if (size == _lastSize) return;
            _lastSize = size;
            foreach (var kv in _labelWorld) Place(kv.Key, kv.Value);
            UpdateScaleBar();
            if (!float.IsNaN(_highlightTarget.x)) { Vector2 t = _highlightTarget; _highlightTarget = new Vector2(float.NaN, float.NaN); HighlightRoomAt(t); }
        }

        // ---- route --------------------------------------------------------------------------------------------

        /// <summary>0 = no route (before the manual), 1 = to the substation, 2 = to E-0, 3 = to the coast.</summary>
        public void SetRouteStage(int stage)
        {
            if (stage == _routeStage) return;
            _routeStage = stage;
            _route.Clear();
            if (stage <= 0) return;
            DrawLeg(RouteToSubstation, stage == 1 ? 1 : 0);
            DrawLeg(RouteToExit, stage == 2 ? 1 : (stage > 2 ? 0 : 2));
            DrawLeg(RouteToCoast, stage >= 3 ? 1 : 2);
            // E-0 symbol: green square with a white door bar
            _route.Box(new Color(0f, 0f, 0f, 0.6f), 501f, 511f, -51f, -41f);
            _route.Box(Green, 502f, 510f, -50f, -42f);
        }

        /// <summary>state: 0 done, 1 active, 2 ahead.</summary>
        private void DrawLeg(Vector2[] pts, int state)
        {
            float w = Kind == Layout.Park ? 4f : 5f;
            if (state == 1)
            {
                _route.Line(new Color(0f, 0f, 0f, 0.55f), w + 4f, pts);
                _route.Line(Amber, w, pts);
            }
            else if (state == 2)
            {
                _route.Dashed(new Color(Amber.r, Amber.g, Amber.b, 0.75f), w - 1f, 10f, 7f, pts);
            }
            else
            {
                _route.Line(RouteDone, w - 1.5f, pts);
            }
        }

        // ---- markers ------------------------------------------------------------------------------------------

        private void BuildMarkers(RectTransform rt)
        {
            // The live markers move every frame: they get their own nested canvas so only they re-batch, not the
            // whole tablet (map meshes, terrain, ~40 labels).
            var mg = new GameObject("Markers", typeof(RectTransform), typeof(Canvas));
            RectTransform mrt = mg.GetComponent<RectTransform>();
            mrt.SetParent(rt, false);
            mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
            mrt.offsetMin = mrt.offsetMax = Vector2.zero;
            Canvas mc = mg.GetComponent<Canvas>();
            mc.overrideSorting = false;
            mc.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            _markers = mrt;

            _beacon = CretaceaUiFactory.CreateAnchoredPanel(mrt, "ObjectiveBeacon", Amber, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            _beacon.GetComponent<Image>().sprite = CretaceaUiFactory.GetRadialRingSprite();
            RectTransform core = CretaceaUiFactory.CreateAnchoredPanel(_beacon, "Core", Amber, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            core.GetComponent<Image>().sprite = CretaceaUiFactory.GetCircleSprite();

            _player = CretaceaUiFactory.CreateAnchoredPanel(mrt, "PlayerMarker", Color.clear, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            // heading wedge + dot, drawn by a tiny vector layer in a 2 x 2 "world"
            var arrowGo = new GameObject("Heading", typeof(RectTransform), typeof(CanvasRenderer), typeof(ParkMapGraphic));
            _playerArrow = arrowGo.GetComponent<RectTransform>();
            _playerArrow.SetParent(_player, false);
            _playerArrow.anchorMin = _playerArrow.anchorMax = new Vector2(0.5f, 0.5f);
            _playerArrow.sizeDelta = new Vector2(40f, 40f);
            ParkMapGraphic a = arrowGo.GetComponent<ParkMapGraphic>();
            a.raycastTarget = false;
            a.World = new Rect(-1f, -1f, 2f, 2f);
            a.Poly(new Color(0.93f, 0.90f, 0.84f, 0.6f), new Vector2(0f, 0f), new Vector2(-0.5f, 1f), new Vector2(0.5f, 1f));
            a.Dot(new Color(0.02f, 0.016f, 0.012f, 0.95f), Vector2.zero, 18f);   // ~2 px dark outline
            a.Dot(Ink, Vector2.zero, 13f);

            // "YOU" on a dark plate, so it reads over wall lines and labels
            _youPlate = CretaceaUiFactory.CreateAnchoredPanel(_player, "YouPlate", new Color(0f, 0f, 0f, 0.6f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 9f), new Vector2(46f, 21f));
            CretaceaUiFactory.StyleRounded(_youPlate, 3f);
            _youLabel = CretaceaUiFactory.CreateText(_youPlate, "You", "YOU", 17f, Ink, UiFont.Heading, TextAlignmentOptions.Center);
            _youLabel.characterSpacing = 3f;
            _youLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _youLabel.rectTransform.offsetMin = new Vector2(2f, 0f);
            _youLabel.rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>Places the player marker (with heading) and the pulsing objective beacon.</summary>
        public void UpdateMarkers(Vector2 playerXZ, float yawDeg, Vector2 targetXZ, bool showTarget)
        {
            if (_player == null) return;
            if (showTarget != _beacon.gameObject.activeSelf) _beacon.gameObject.SetActive(showTarget);
            if (showTarget)
            {
                // the pulse is the only per-frame change while the player stands still (it re-batches the marker canvas only)
                float pulse = 1f + 0.22f * Mathf.Sin(Time.unscaledTime * 4.5f);
                _beacon.localScale = Vector3.one * pulse;
            }
            Vector2 size = _rt.rect.size;
            bool moved = (playerXZ - _lastPlayer).sqrMagnitude > 0.04f || Mathf.Abs(Mathf.DeltaAngle(yawDeg, _lastYaw)) > 1f
                         || targetXZ != _lastTarget || showTarget != _lastShow || size != _lastMarkerSize;
            if (!moved) return;
            _lastPlayer = playerXZ; _lastYaw = yawDeg; _lastTarget = targetXZ; _lastShow = showTarget; _lastMarkerSize = size;

            Vector2 p = Clamp01Inset(_base.ToNormalized(playerXZ), 0.03f);
            _player.anchorMin = _player.anchorMax = p;
            _player.anchoredPosition = Vector2.zero;
            _playerArrow.localRotation = Quaternion.Euler(0f, 0f, -yawDeg);

            bool overlap = false;
            if (showTarget)
            {
                Vector2 o = Clamp01Inset(_base.ToNormalized(BeaconPoint(targetXZ)), 0.035f);
                _beacon.anchorMin = _beacon.anchorMax = o;
                _beacon.anchoredPosition = Vector2.zero;
                // "YOU" moves above the dot when it would sit on the beacon
                overlap = Vector2.Distance(o * size, p * size) < 44f;
            }
            bool inCorridor = Kind == Layout.VisitorCenter && StaffCorridor.Contains(playerXZ);
            if (inCorridor && !overlap)
            {
                // In the 3 m corridor a plate above or below the dot covers a wall line: put YOU beside the dot, on the
                // side away from the heading wedge.
                bool wedgeRight = Mathf.Sin(yawDeg * Mathf.Deg2Rad) > 0f;
                _youPlate.anchorMin = _youPlate.anchorMax = new Vector2(wedgeRight ? 0f : 1f, 0.5f);
                _youPlate.pivot = new Vector2(wedgeRight ? 1f : 0f, 0.5f);
                _youPlate.anchoredPosition = new Vector2(wedgeRight ? 6f : -6f, 0f);
            }
            else
            {
                _youPlate.anchorMin = _youPlate.anchorMax = new Vector2(0.5f, overlap ? 1f : 0f);
                _youPlate.pivot = new Vector2(0.5f, overlap ? 0f : 1f);
                _youPlate.anchoredPosition = new Vector2(0f, overlap ? -9f : 9f);
            }
            if (Kind == Layout.VisitorCenter && _corridorLabel != null && _corridorLabelIndex >= 0)
            {
                Vector2 at = CorridorLabelAt(playerXZ);
                if (_labelWorld[_corridorLabelIndex].Value != at)
                {
                    _labelWorld[_corridorLabelIndex] = new System.Collections.Generic.KeyValuePair<RectTransform, Vector2>(_corridorLabel.rectTransform, at);
                    Place(_corridorLabel.rectTransform, at);
                }
            }

            // map labels near the player dot step back, so the dot, wedge and YOU stay legible
            Vector2 pp = p * size;
            for (int i = 0; i < _labelWorld.Count; i++)
            {
                RectTransform lr = _labelWorld[i].Key;
                Vector2 c = _base.ToNormalized(_labelWorld[i].Value) * size;
                Vector2 pv = lr.pivot;
                // label box centre from its pivot (labels are 260 x 60 boxes; use the text's own bounds)
                TextMeshProUGUI t = _labelTexts[i];
                Vector2 tb = _labelExtents[i];
                if (tb == Vector2.zero) { tb = t.GetPreferredValues(t.text); _labelExtents[i] = tb; }
                Vector2 centre = c + new Vector2((0.5f - pv.x) * tb.x, (0.5f - pv.y) * tb.y);
                Vector2 d = new Vector2(Mathf.Max(0f, Mathf.Abs(pp.x - centre.x) - tb.x * 0.5f), Mathf.Max(0f, Mathf.Abs(pp.y - centre.y) - tb.y * 0.5f));
                bool near = d.magnitude < 30f;
                float want = near ? (i == _corridorLabelIndex ? 0f : 0.2f) : 1f;
                if (!Mathf.Approximately(t.alpha, want)) t.alpha = want;
            }
            if (Kind == Layout.VisitorCenter) HighlightRoomAt(showTarget ? targetXZ : new Vector2(9999f, 9999f));
        }

        /// <summary>On the floor plan the beacon sits in the lower half of a north-wing room, whose name is set
        /// high: the pulsing ring never touches the target's own label.</summary>
        private Vector2 BeaconPoint(Vector2 xz)
        {
            if (Kind != Layout.VisitorCenter) return xz;
            foreach (Room r in Rooms)
                if (!string.IsNullOrEmpty(r.Name) && r.R.yMin >= 21f && r.R.Contains(xz))
                    return new Vector2(xz.x, Mathf.Min(xz.y, r.R.yMin + 2.6f));
            return xz;
        }

        private RectTransform _markers, _youPlate;
        private Vector2 _lastPlayer = new Vector2(float.NaN, 0f), _lastTarget, _lastMarkerSize;
        private float _lastYaw;
        private bool _lastShow;

        private static Vector2 Clamp01Inset(Vector2 n, float inset) =>
            new Vector2(Mathf.Clamp(n.x, inset, 1f - inset), Mathf.Clamp(n.y, inset, 1f - inset));

        private Vector2 _hlFor = new Vector2(float.NaN, float.NaN);
        private void HighlightRoomAt(Vector2 xz)
        {
            if (_highlightRoom == null) return;
            if (_rt.rect.width < 2f) { _highlightTarget = xz; return; }
            if (xz == _hlFor) return;
            _hlFor = xz;
            foreach (Room r in Rooms)
            {
                if (string.IsNullOrEmpty(r.Name) || !r.R.Contains(xz)) continue;
                RectTransform h = _highlightRoom.rectTransform;
                h.anchorMin = _base.ToNormalized(r.R.min);
                h.anchorMax = _base.ToNormalized(r.R.max);
                h.offsetMin = h.offsetMax = Vector2.zero;
                _highlightRoom.enabled = true;
                SetHighlightLabel(_roomLabels.TryGetValue(r.Name, out TextMeshProUGUI hl) ? hl : null);
                return;
            }
            _highlightRoom.enabled = false;
            SetHighlightLabel(null);
        }

        private void SetHighlightLabel(TextMeshProUGUI label)
        {
            if (_hlLabel == label) return;
            if (_hlLabel != null) { _hlLabel.color = InkMuted; _hlLabel.fontStyle = FontStyles.Normal; }
            _hlLabel = label;
            if (_hlLabel != null) { _hlLabel.color = InkOnAmber; _hlLabel.fontStyle = FontStyles.Bold; }
        }

        // ---- compass and scale ---------------------------------------------------------------------------------

        private void BuildCompassAndScale(RectTransform rt, Layout layout)
        {
            RectTransform plate = CretaceaUiFactory.CreateAnchoredPanel(rt, "Compass", new Color(0f, 0f, 0f, 0.45f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(46f, 62f));
            CretaceaUiFactory.StyleRounded(plate, 4f);
            var cg = new GameObject("Needle", typeof(RectTransform), typeof(CanvasRenderer), typeof(ParkMapGraphic));
            RectTransform crt = cg.GetComponent<RectTransform>();
            crt.SetParent(plate, false);
            crt.anchorMin = new Vector2(0f, 0f); crt.anchorMax = new Vector2(1f, 0.7f);
            crt.offsetMin = new Vector2(6f, 6f); crt.offsetMax = new Vector2(-6f, -2f);
            ParkMapGraphic n = cg.GetComponent<ParkMapGraphic>();
            n.raycastTarget = false;
            n.World = new Rect(-1f, -1f, 2f, 2f);
            n.Poly(Amber, new Vector2(0f, 1f), new Vector2(-0.42f, -0.2f), new Vector2(0f, 0.05f));
            n.Poly(new Color(Amber.r * 0.7f, Amber.g * 0.7f, Amber.b * 0.7f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.05f), new Vector2(0.42f, -0.2f));
            n.Poly(InkMuted, new Vector2(0f, -1f), new Vector2(0.42f, -0.2f), new Vector2(-0.42f, -0.2f));
            TextMeshProUGUI nl = CretaceaUiFactory.CreateText(plate, "N", "N", 18f, Ink, UiFont.Heading, TextAlignmentOptions.Top);
            nl.rectTransform.offsetMax = new Vector2(0f, -2f);

            RectTransform sp = CretaceaUiFactory.CreateAnchoredPanel(rt, "Scale", new Color(0f, 0f, 0f, 0.45f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 14f), new Vector2(150f, 44f));
            CretaceaUiFactory.StyleRounded(sp, 4f);
            _scaleBar = CretaceaUiFactory.CreateAnchoredPanel(sp, "Bar", Ink, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 9f), new Vector2(100f, 4f));
            _scaleLabel = CretaceaUiFactory.CreateText(sp, "Label", "100 M", 18f, Ink, UiFont.Label, TextAlignmentOptions.TopRight);
            _scaleLabel.characterSpacing = 2f;
            _scaleLabel.rectTransform.offsetMin = new Vector2(8f, 0f);
            _scaleLabel.rectTransform.offsetMax = new Vector2(-12f, -3f);
        }

        private void UpdateScaleBar()
        {
            if (_scaleBar == null || _base == null) return;
            float ppm = _base.PixelsPerMetre;
            if (ppm <= 0.0001f) return;
            float metres = Kind == Layout.Park ? 100f : 10f;
            _scaleBar.sizeDelta = new Vector2(metres * ppm, 4f);
            _scaleLabel.text = metres.ToString("0") + " M";
            RectTransform plate = _scaleBar.parent as RectTransform;
            if (plate != null) plate.sizeDelta = new Vector2(Mathf.Max(96f, metres * ppm + 24f), 44f);
        }
    }

    /// <summary>The park's ground as a map texture: hillshade, contours, sea and lake from the island terrain.</summary>
    public static class ParkTerrainTexture
    {
        private static Texture2D s_Tex;

        public static void Release()
        {
            if (s_Tex == null) return;
            if (Application.isPlaying) Object.Destroy(s_Tex); else Object.DestroyImmediate(s_Tex);
            s_Tex = null;
        }

        /// <summary>Separable box blur of a heightfield (radius r texels), in place.</summary>
        private static void BoxBlur(float[] a, float[] tmp, int w, int h, int r)
        {
            float inv = 1f / (2 * r + 1);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float acc = 0f;
                    for (int k = -r; k <= r; k++) acc += a[y * w + Mathf.Clamp(x + k, 0, w - 1)];
                    tmp[y * w + x] = acc * inv;
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float acc = 0f;
                    for (int k = -r; k <= r; k++) acc += tmp[Mathf.Clamp(y + k, 0, h - 1) * w + x];
                    a[y * w + x] = acc * inv;
                }
        }

        public static Texture2D Get(Rect world)
        {
            if (s_Tex != null) return s_Tex;
            const int w = 800, h = 516;   // one texel per metre
            float[] H = new float[w * h];
            // one bulk heightmap read per terrain, then bilinear lookups (SampleHeight per texel took ~260 ms)
            Terrain[] terrains = Terrain.activeTerrains;
            var maps = new float[terrains.Length][,];
            for (int i = 0; i < terrains.Length; i++)
            {
                TerrainData td = terrains[i] != null ? terrains[i].terrainData : null;
                if (td == null) continue;
                Vector3 p = terrains[i].transform.position, sz = td.size;
                if (p.x > world.xMax || p.z > world.yMax || p.x + sz.x < world.xMin || p.z + sz.z < world.yMin) continue;
                maps[i] = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);
            }
            for (int y = 0; y < h; y++)
            {
                float z = world.yMin + (y + 0.5f) * world.height / h;
                for (int x = 0; x < w; x++)
                {
                    float wx = world.xMin + (x + 0.5f) * world.width / w;
                    H[y * w + x] = SampleHeight(terrains, maps, wx, z);
                }
            }

            // contours: the heights softened a little (5-tap box, twice)
            float[] tmp = new float[w * h];
            BoxBlur(H, tmp, w, h, 2);
            BoxBlur(H, tmp, w, h, 2);
            // hillshade: normals from a much wider blur (13-tap box, three passes), so the straight slope breaks where
            // the hand-built play area meets the generated terrain tiles do not show as seams or roads
            float[] S = (float[])H.Clone();
            for (int pass = 0; pass < 3; pass++) BoxBlur(S, tmp, w, h, 6);

            var px = new Color32[w * h];
            Vector3 light = new Vector3(-0.6f, 0.75f, 0.6f).normalized;   // from the north-west, 45 deg
            var rng = new System.Random(1993);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float c = H[y * w + x];
                    float l = H[y * w + Mathf.Max(0, x - 1)], r = H[y * w + Mathf.Min(w - 1, x + 1)];
                    float d = H[Mathf.Max(0, y - 1) * w + x], u = H[Mathf.Min(h - 1, y + 1) * w + x];
                    float sl = S[y * w + Mathf.Max(0, x - 1)], sr = S[y * w + Mathf.Min(w - 1, x + 1)];
                    float sd = S[Mathf.Max(0, y - 1) * w + x], su = S[Mathf.Min(h - 1, y + 1) * w + x];
                    Vector3 n = new Vector3(sl - sr, 2f, sd - su).normalized;
                    float shade = Mathf.Clamp(Vector3.Dot(n, light) / light.y, 0.55f, 1.45f);
                    // next to an artificial step (the play area cut into the island) the wide blur would paint a broad
                    // lit/shaded "tube": keep the ground flat-toned there and let the cliff line alone mark the edge
                    float dev = Mathf.Abs(c - S[y * w + x]);
                    shade = Mathf.Lerp(shade, 1f, Mathf.InverseLerp(0.4f, 2.0f, dev));
                    float wx = world.xMin + (x + 0.5f) * world.width / w, wz = world.yMin + (y + 0.5f) * world.height / h;

                    Color col;
                    bool sea = c < SeaLevelLocal;
                    bool lake = !sea && c < LakeLevelLocal && Vector2.Distance(new Vector2(wx, wz), LakeCentreLocal) < 75f;
                    if (sea || lake)
                    {
                        float depth = Mathf.Clamp01(((sea ? SeaLevelLocal : LakeLevelLocal) - c) / 12f);
                        col = Color.Lerp(new Color(0.16f, 0.24f, 0.29f), new Color(0.10f, 0.15f, 0.19f), depth);
                        // shoreline
                        bool shore = (sea ? Mathf.Max(Mathf.Max(l, r), Mathf.Max(d, u)) >= SeaLevelLocal : Mathf.Max(Mathf.Max(l, r), Mathf.Max(d, u)) >= LakeLevelLocal);
                        if (shore) col = new Color(0.36f, 0.50f, 0.56f);
                    }
                    else
                    {
                        // warm ground, a touch lighter on the hills; hillshade on top
                        float e = Mathf.InverseLerp(-10f, 45f, c);
                        Color low = new Color(0.115f, 0.10f, 0.075f), high = new Color(0.20f, 0.175f, 0.13f);
                        col = Color.Lerp(low, high, e) * shade;
                        // contours every 5 m, index contours every 25 m; where the hand-built play area was cut into
                        // the island (an 18-38 m step at x ~521) the bunched contours read as a pale road: draw a
                        // cliff as one dark edge instead
                        int band = Mathf.FloorToInt(c / 5f);
                        bool contour = band != Mathf.FloorToInt(r / 5f) || band != Mathf.FloorToInt(u / 5f);
                        float grad = Mathf.Max(Mathf.Abs(r - l), Mathf.Abs(u - d)) * 0.5f;
                        // a step (not a smooth slope) shows as a large gap between the heights and their wide blur
                        bool cliff = grad > 1.1f || dev > 2.0f;
                        if (cliff)
                            col = Color.Lerp(col, new Color(0.065f, 0.055f, 0.042f), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.2f, 2.2f, grad)) * 0.7f);
                        else if (contour)
                        {
                            bool index = Mathf.FloorToInt(c / 25f) != Mathf.FloorToInt(r / 25f) || Mathf.FloorToInt(c / 25f) != Mathf.FloorToInt(u / 25f);
                            col = Color.Lerp(col, new Color(0.42f, 0.37f, 0.28f), index ? 0.55f : 0.28f);
                        }
                    }
                    col.a = 1f;
                    // dither by +-0.75 of an 8-bit linear step: the overlay UI buffer is 8-bit linear and would band
                    Color lin = col.linear;
                    float dn = ((float)rng.NextDouble() - 0.5f) * (1.5f / 255f);
                    lin.r = Mathf.Max(0f, lin.r + dn); lin.g = Mathf.Max(0f, lin.g + dn); lin.b = Mathf.Max(0f, lin.b + dn);
                    px[y * w + x] = lin.gamma;
                }
            }
            s_Tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
            {
                name = "ParkMap_Terrain",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            s_Tex.SetPixels32(px);
            s_Tex.Apply(false, true);
            return s_Tex;
        }

        private const float SeaLevelLocal = -40f, LakeLevelLocal = -7.65f;
        private static readonly Vector2 LakeCentreLocal = new Vector2(-62f, -205f);

        private static float SampleHeight(Terrain[] terrains, float[][,] maps, float x, float z)
        {
            for (int i = 0; i < terrains.Length; i++)
            {
                float[,] m = maps[i];
                if (m == null) continue;
                Terrain t = terrains[i];
                Vector3 p = t.transform.position, s = t.terrainData.size;
                if (x < p.x || z < p.z || x > p.x + s.x || z > p.z + s.z) continue;
                int res = m.GetLength(0);
                float u = (x - p.x) / s.x * (res - 1), v = (z - p.z) / s.z * (res - 1);
                int x0 = Mathf.Clamp((int)u, 0, res - 2), z0 = Mathf.Clamp((int)v, 0, res - 2);
                float fu = u - x0, fv = v - z0;
                // GetHeights is [z, x], normalised to the terrain height
                float a = Mathf.Lerp(m[z0, x0], m[z0, x0 + 1], fu), b = Mathf.Lerp(m[z0 + 1, x0], m[z0 + 1, x0 + 1], fu);
                return Mathf.Lerp(a, b, fv) * s.y + p.y;
            }
            return 0f;
        }
    }
}
