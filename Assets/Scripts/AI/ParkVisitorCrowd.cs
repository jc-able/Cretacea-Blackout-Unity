using System.Collections.Generic;
using UnityEngine;

namespace Cretacea.AI
{
    /// <summary>
    /// The prologue crowd (review 2: "the park looks nearly empty"): about fifty people in groups —
    ///  - along the Visitor Center facade (shot 2): a queue between the entrance belts, a family at the park map, buyers
    ///    at both ticket windows, a tour group around the guide and strollers on lanes that clear every kiosk and rail;
    ///  - at the south viewing rail (shot 3): groups along the rail, just outside the lens so the brachiosaurus has the frame;
    ///  - in the Viewing Gallery (shot 4): a dozen pressed against the T. rex glass, a few in a second row, the middle
    ///    pane (z -61..-58) kept clear for the roar.
    /// Repeats are broken up per person by height (about +/-6 %), a clothing recolour (three per model, material assets in
    /// <c>Resources/VisitorVariants</c>, made by <c>Tools/agent/visitor_variants.py</c>: only the clothes change colour,
    /// and each variant stays SRP-Batcher compatible), the animation phase and rate.
    /// When the park closes a handful of strollers keep walking for a few seconds (<see cref="BeginClosing"/>).
    /// Lives on <c>Resources/PF_OpeningCrowd</c> (built by <c>cretacea build_character_prefabs</c>): StoryDirector
    /// instantiates it when the opening starts; the crowd removes itself as soon as the park closes (the atmosphere
    /// leaves GoldenDay: the "Closing time" cut, a skip or a chapter jump) or the cutscene ends. The people are its children.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParkVisitorCrowd : MonoBehaviour
    {
        public const string ResourcePath = "PF_OpeningCrowd";

        [SerializeField] private GameObject parkGuide;
        [SerializeField] private GameObject visitorMan;
        [SerializeField] private GameObject visitorWoman;
        [SerializeField] private GameObject visitorTeen;
        [SerializeField] private GameObject visitorChild;
        [SerializeField] private int seed = 1993;

        private enum Who { Guide, Man, Woman, Teen, Child }

        private struct Spot
        {
            public Who Who;
            public Vector3 Pos;
            public ParkVisitor.Activity Activity;
            public Vector3 LookAt;
            public Vector3 StrollTo;
            public float Speed;
        }

        private readonly List<ParkVisitor> _people = new List<ParkVisitor>(56);
        public IReadOnlyList<ParkVisitor> People => _people;
        private readonly List<ParkVisitor> _stragglers = new List<ParkVisitor>(8);
        // Each group draws and animates only while the camera is near it (shot 4's gallery is 100 m from the facade):
        // saves ~1 ms of CPU skinning per shot. Animators keep their state while disabled.
        private enum Group { Facade, Rail, Gallery }
        private readonly List<Group> _groups = new List<Group>(56);
        private readonly bool[] _groupOn = { true, true, true };
        private bool _sawCutscene;
        private float _nextCheck;
        private float _closeAt = -1f;

        private void Update()
        {
            if (_closeAt >= 0f && Time.time >= _closeAt) { Destroy(gameObject); return; }
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.1f;
            var atmosphere = Cretacea.Core.AtmosphereController.Instance;
            bool closed = atmosphere != null && atmosphere.CurrentPreset != Cretacea.Core.AtmospherePreset.GoldenDay;
            var gs = Cretacea.Core.GameState.Instance;
            if (gs != null && gs.InCutscene) _sawCutscene = true;
            bool cutsceneOver = _sawCutscene && gs != null && !gs.InCutscene;
            if (cutsceneOver || (closed && !(gs != null && gs.InCutscene))) { Destroy(gameObject); return; }
            if (closed && _closeAt < 0f) BeginClosing(3.5f);
            UpdateGroupVisibility();
        }

        private void UpdateGroupVisibility()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 c = cam.transform.position;
            bool gallery = c.x > 35f && c.z > -90f && c.z < -25f;
            bool rail = !gallery && (new Vector2(c.x + 48f, c.z + 92f)).sqrMagnitude < 40f * 40f;
            SetGroup(Group.Facade, !gallery);
            SetGroup(Group.Rail, rail);
            SetGroup(Group.Gallery, gallery);
        }

        private void SetGroup(Group g, bool on)
        {
            if (_groupOn[(int)g] == on) return;
            _groupOn[(int)g] = on;
            for (int i = 0; i < _people.Count; i++)
            {
                if (_groups[i] != g || _people[i] == null) continue;
                foreach (Renderer r in _people[i].GetComponentsInChildren<Renderer>()) r.enabled = on;
                foreach (Animator a in _people[i].GetComponentsInChildren<Animator>()) a.enabled = on;
            }
        }

        /// <summary>The park closes: everyone but the strollers leaves at once; the strollers keep walking toward the
        /// plaza exits for <paramref name="seconds"/> (the first seconds of the "Closing time" aerial), then the crowd goes.</summary>
        public void BeginClosing(float seconds)
        {
            if (_closeAt >= 0f) return;
            _closeAt = Time.time + Mathf.Max(0f, seconds);
            for (int i = _people.Count - 1; i >= 0; i--)
            {
                ParkVisitor v = _people[i];
                if (v == null || _stragglers.Contains(v)) continue;
                Destroy(v.gameObject);
                _people.RemoveAt(i);
                _groups.RemoveAt(i);
            }
        }

        private static readonly string[] VariantModels = { null, "man", "woman", "teen", "child" };   // indexed by Who
        private const int VariantsPerModel = 3;

        private void Start()
        {
            var rng = new System.Random(seed);
            // per model: the original look + three clothing recolours, dealt in turn from a random start so neighbours differ
            var variants = new Material[VariantModels.Length][];
            var nextVariant = new int[VariantModels.Length];
            for (int w = 0; w < VariantModels.Length; w++)
            {
                nextVariant[w] = rng.Next(VariantsPerModel + 1);
                if (VariantModels[w] == null) continue;
                variants[w] = new Material[VariantsPerModel + 1];
                for (int i = 1; i <= VariantsPerModel; i++)
                    variants[w][i] = Resources.Load<Material>($"VisitorVariants/MAT_visitor_{VariantModels[w]}_v{i}");
            }
            foreach (Spot s in Layout())
            {
                GameObject pf = PrefabFor(s.Who);
                if (pf == null) continue;
                Vector3 p = GroundAt(s.Pos);
                GameObject go = Instantiate(pf, p, Quaternion.identity, transform);
                go.name = $"{pf.name.Replace("PF_", "")}_{_people.Count:00}";
                ParkVisitor v = go.GetComponent<ParkVisitor>();
                if (v == null) continue;

                // height variation (ParkVisitor scales the walk speed with it so the feet still do not skate)
                float scale = 1f + ((float)rng.NextDouble() * 2f - 1f) * (s.Who == Who.Child ? 0.08f : 0.06f);
                go.transform.localScale = pf.transform.localScale * scale;
                int who = (int)s.Who;
                if (variants[who] != null)
                {
                    Material look = variants[who][nextVariant[who]];
                    nextVariant[who] = (nextVariant[who] + 1) % (VariantsPerModel + 1);
                    string stem = $"MAT_visitor_{VariantModels[who]}";
                    if (look != null)
                        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                            if (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith(stem, System.StringComparison.Ordinal))
                                r.sharedMaterial = look;
                }

                float clipOffset = (float)rng.NextDouble();
                if (s.Activity == ParkVisitor.Activity.Walk)
                {
                    v.Stroll(p, GroundAt(s.StrollTo), s.Speed, 0f, clipOffset);
                    _stragglers.Add(v);
                }
                else
                    v.Stand(s.Activity, s.LookAt, clipOffset, 0.88f + 0.24f * (float)rng.NextDouble());
                foreach (Animator a in go.GetComponentsInChildren<Animator>()) a.keepAnimatorStateOnDisable = true;
                _people.Add(v);
                _groups.Add(s.Pos.x > 40f ? Group.Gallery : s.Pos.z < -80f ? Group.Rail : Group.Facade);
            }
        }

        private GameObject PrefabFor(Who w)
        {
            switch (w)
            {
                case Who.Guide: return parkGuide;
                case Who.Man: return visitorMan;
                case Who.Woman: return visitorWoman;
                case Who.Teen: return visitorTeen;
                default: return visitorChild;
            }
        }

        private static Vector3 GroundAt(Vector3 p)
        {
            // from just above head height: under the gallery roof, above any floor
            if (Physics.Raycast(new Vector3(p.x, p.y + 2.6f, p.z), Vector3.down, out RaycastHit hit, 8f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                p.y = hit.point.y;
            return p;
        }

        private static IEnumerable<Spot> Layout()
        {
            const ParkVisitor.Activity Idle = ParkVisitor.Activity.Idle, Look = ParkVisitor.Activity.LookAround,
                Phone = ParkVisitor.Activity.Phone, Clap = ParkVisitor.Activity.Clap, Cheer = ParkVisitor.Activity.Cheer,
                Wave = ParkVisitor.Activity.Wave;

            // ---- Facade (shot 2 looks west-north-west along the facade from x -8..-24, z -51..-47.5) ----
            // Queue between the entrance belts (belts at z -36.5/-37.9/-39.3/-40.7, x -42.4..-38.4), lanes snake
            Vector3 east = new Vector3(-30f, 1.6f, -38f), west = new Vector3(-50f, 1.6f, -38f);
            yield return Stand(Who.Woman, -41.6f, -40.0f, Idle, east);
            yield return Stand(Who.Man,   -40.7f, -40.05f, Phone, east);
            yield return Stand(Who.Child, -39.9f, -39.95f, Look, east);
            yield return Stand(Who.Teen,  -39.1f, -38.6f, Phone, west);
            yield return Stand(Who.Woman, -40.1f, -38.65f, Look, west);
            yield return Stand(Who.Man,   -41.3f, -37.2f, Idle, east);
            yield return Stand(Who.Teen,  -40.2f, -37.25f, Look, east);
            // Family reading the park map board (-44, -38.5)
            Vector3 board = new Vector3(-44f, 1.5f, -38.4f);
            yield return Stand(Who.Man,   -44.7f, -39.7f, Look, board);
            yield return Stand(Who.Child, -43.9f, -39.5f, Wave, board);
            yield return Stand(Who.Woman, -43.2f, -40.2f, Idle, board);
            // Ticket windows. TicketKiosk_0 (-50.5, -42, yaw 90) has its window on its WEST side (x -51.6), TicketKiosk_1
            // (-13.5, -42, yaw -90) on its EAST side (x -12.4): the buyers queue there, facing the counter.
            yield return Stand(Who.Man,   -52.4f, -41.7f, Idle,  new Vector3(-51.5f, 1.4f, -41.9f));
            yield return Stand(Who.Woman, -52.9f, -42.7f, Phone, new Vector3(-51.5f, 1.4f, -42.1f));
            yield return Stand(Who.Teen,  -11.6f, -41.6f, Idle,  new Vector3(-12.5f, 1.4f, -41.9f));
            yield return Stand(Who.Woman, -11.0f, -42.6f, Look,  new Vector3(-12.5f, 1.4f, -42.1f));
            // Tour group around the guide, mid-ground left of the shot-2 axis
            Vector3 guide = new Vector3(-44.6f, 0f, -47.0f);
            yield return Stand(Who.Guide, guide.x, guide.z, Wave, new Vector3(-47.5f, 1.6f, -46.6f));
            yield return Stand(Who.Man,   -47.3f, -45.8f, Look, guide);
            yield return Stand(Who.Woman, -47.9f, -46.8f, Clap, guide);
            yield return Stand(Who.Teen,  -47.5f, -47.9f, Phone, guide);
            yield return Stand(Who.Child, -46.6f, -46.4f, Cheer, guide);
            yield return Stand(Who.Man,   -48.6f, -45.9f, Idle, guide);
            yield return Stand(Who.Woman, -46.5f, -48.4f, Look, guide);
            // Strollers (they are also the stragglers still walking out in the first seconds of the "Closing time" aerial).
            // One direction per lane, starts >= 6 m apart, the walker in front the fastest, every segment longer than the
            // ~41 s they live (no turn-arounds on screen). Lanes: z -45.1 (1.1 m south of the bollard lights at z -44,
            // 1.8 m south of the kiosks), z -38.3 east of the queue rails (x -35..18), z -37.6 west of the cafe (clear of
            // the cafe chairs at z -39.5). Checked with a 0.3 m capsule cast against the built scene (Tools/agent/lanes.cs).
            yield return Walk(Who.Woman, -32f, -45.1f,  8f, 1.10f);
            yield return Walk(Who.Child, -46f, -45.1f,  8f, 1.05f);
            yield return Walk(Who.Teen,  -56f, -45.1f,  8f, 0.95f);
            yield return Walk(Who.Man,    -2f, -38.3f, -35f, 1.00f);
            yield return Walk(Who.Woman,  12f, -38.3f, -35f, 0.95f);
            yield return Walk(Who.Teen,   18f, -38.3f, -35f, 0.90f);
            yield return Walk(Who.Man,   -58f, -37.6f, -92f, 0.85f);

            // ---- South viewing rail (shot 3: the lens glides x -49.0 -> -48.0 at z -90.3..-90.5 looking east-south-east
            // over the rail). Two small groups stand at the rail 15-19 m down the lens, whole figures in the lower left
            // third, small against the brachiosaurus (heads cut by the frame edge read as an accident, review 2).
            Vector3 meadow = new Vector3(-20f, 6f, -106f);
            yield return Stand(Who.Teen,  -34.5f, RailZ,        Look, meadow);
            yield return Stand(Who.Woman, -33.7f, RailZ + 0.2f, Phone, meadow);
            yield return Stand(Who.Man,   -31.2f, RailZ + 0.1f, Phone, meadow);
            yield return Stand(Who.Child, -30.5f, RailZ + 0.2f, Cheer, meadow);
            yield return Stand(Who.Woman, -29.8f, RailZ,        Look, meadow);

            // ---- Viewing Gallery (glass inner face x 55.86). Small groups 0.4-1.6 m off the glass with 2-3 m gaps, a few
            // people turned to each other; the middle pane (z -61..-58) stays clear for the roar, and nobody stands in the
            // shot-4b lens's lower frame (x 49.6..50.6 looking down z -59.5: the people nearest it are adults).
            Vector3 rex = new Vector3(66.8f, 3.5f, -59.5f);
            // group A, south of the clear pane
            yield return Stand(Who.Man,   55.2f, -61.5f, Clap, rex);
            yield return Stand(Who.Woman, 54.5f, -62.3f, Look, rex);
            yield return Stand(Who.Child, 55.3f, -63.4f, Cheer, rex);
            yield return Stand(Who.Teen,  54.3f, -64.3f, Phone, new Vector3(54.5f, 1.5f, -62.3f));
            // group B, north of the clear pane
            yield return Stand(Who.Woman, 55.2f, -57.6f, Clap, rex);
            yield return Stand(Who.Man,   54.4f, -56.8f, Idle, rex);
            yield return Stand(Who.Child, 55.3f, -56.1f, Wave, rex);
            // group C
            yield return Stand(Who.Teen,  55.0f, -53.4f, Look, rex);
            yield return Stand(Who.Woman, 54.0f, -52.7f, Phone, new Vector3(55.0f, 1.5f, -53.4f));
            yield return Stand(Who.Man,   55.2f, -51.9f, Clap, rex);
            // group D: the guide talking to a couple
            yield return Stand(Who.Guide, 54.0f, -49.3f, Idle, new Vector3(55.0f, 1.6f, -48.2f));
            yield return Stand(Who.Woman, 55.2f, -48.5f, Look, rex);
            yield return Stand(Who.Man,   54.6f, -47.6f, Look, new Vector3(54.0f, 1.6f, -49.3f));
            // second row, clear of the shot-4b lens
            yield return Stand(Who.Man,   53.0f, -63.0f, Look, rex);
            yield return Stand(Who.Teen,  53.2f, -54.6f, Cheer, rex);
        }

        // z of the people at the viewing rail (just in front of the glass balustrade at z -95.6)
        private const float RailZ = -94.9f;

        private static Spot Walk(Who w, float x, float z, float toX, float speed) => new Spot
        {
            Who = w, Pos = new Vector3(x, 0f, z), Activity = ParkVisitor.Activity.Walk, StrollTo = new Vector3(toX, 0f, z), Speed = speed
        };

        private static Spot Stand(Who w, float x, float z, ParkVisitor.Activity a, Vector3 lookAt) => new Spot
        {
            Who = w, Pos = new Vector3(x, 0f, z), Activity = a, LookAt = lookAt
        };
    }
}
