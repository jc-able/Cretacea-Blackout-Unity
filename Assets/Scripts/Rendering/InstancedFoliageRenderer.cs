using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cretacea.Rendering
{
    /// <summary>
    /// Draws the hill jungle (tens of thousands of trees and plants nobody can reach) with Graphics.RenderMeshInstanced
    /// instead of one GameObject per plant. The instance list is baked by the world builder into a binary TextAsset;
    /// each prototype has its LOD meshes (last level = a crossed-quad impostor) with a maximum distance per level.
    /// Per camera: cells are frustum-culled; near cells choose a LOD per instance, far cells use one level for the whole
    /// cell; the lists are rebuilt only when the camera has moved or turned, otherwise last frame's lists are re-submitted.
    /// </summary>
    [ExecuteAlways]
    public sealed class InstancedFoliageRenderer : MonoBehaviour
    {
        [Serializable]
        public sealed class LodLevel
        {
            public Mesh mesh;
            public Material[] materials;
            public float maxDistance;          // at instance scale 1
            public bool castShadows;
            [Tooltip("Optional copy of the mesh without the impostor's horizontal crown cap, drawn where the camera does not look down on the crowns.")]
            public Mesh capless;
        }

        [Serializable]
        public sealed class Prototype
        {
            public string name;
            public LodLevel[] lods;
        }

        public Prototype[] prototypes = Array.Empty<Prototype>();
        /// <summary>Per instance: int32 prototype, float3 position, float4 rotation, float scale (36 bytes).</summary>
        public TextAsset instanceData;
        public float cellSize = 64f;
        public int renderLayer;
        [Tooltip("Cells closer than this choose a LOD per instance.")]
        public float perInstanceDistance = 260f;
        [Tooltip("Rebuild the draw lists when the camera moves this far (m) or turns this much (deg).")]
        public float moveThreshold = 1.0f, turnThreshold = 1.0f;
        [Tooltip("Crown caps are drawn only where the camera looks down on the crowns by more than this slope (tan of the angle); elsewhere the capless mesh (round 5: from eye level the caps read as a mosaic of lit plates on the hills).")]
        public float capElevation = 0.10f;
        [Tooltip("Hysteresis round capElevation: a cell switches to the cap above capElevation + this and back to the capless mesh below capElevation - this (no flicker while the camera height hovers at the threshold).")]
        public float capHysteresis = 0.02f;

        private const int RecordSize = 36;
        private const int MaxBatch = 1023;

        private sealed class Cell
        {
            public Bounds bounds;
            public int[] start;     // per prototype, into _matrices / _positions
            public int[] count;
            public float maxScale;
        }

        private sealed class CameraState
        {
            public Vector3 pos;
            public Quaternion rot;
            public float fov = -1f;
            public Matrix4x4[][][] lists;   // [proto][lod]
            public int[][] counts;
            // fix round: the cap / capless choice per far cell and per near instance, with hysteresis
            public bool[] cellCapless;
            public bool[] instCapless;
        }

        private Matrix4x4[] _matrices = Array.Empty<Matrix4x4>();
        private Vector3[] _positions = Array.Empty<Vector3>();
        private float[] _scales = Array.Empty<float>();
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly Dictionary<Camera, CameraState> _cams = new Dictionary<Camera, CameraState>();
        private readonly Plane[] _planes = new Plane[6];
        private float _maxDistance;
        private Bounds _worldBounds;
        private bool _ready;

        public int InstanceCount => _matrices.Length;

        private void OnEnable()
        {
            Decode();
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            _cams.Clear();
            _ready = false;
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled) { Decode(); _cams.Clear(); }
        }

        private TextAsset _decodedFrom;
        private int _decodedProtos = -1;

        private void Decode()
        {
            _ready = false;
            _decodedFrom = instanceData;
            _decodedProtos = prototypes != null ? prototypes.Length : 0;
            _cells.Clear();
            if (instanceData == null || prototypes == null || prototypes.Length == 0) return;
            byte[] b = instanceData.bytes;
            int n = b.Length / RecordSize;
            int pc = prototypes.Length;
            var proto = new int[n];
            var pos = new Vector3[n];
            var rot = new Quaternion[n];
            var scl = new float[n];
            var cellOf = new int[n];
            var cellIndex = new Dictionary<long, int>();
            for (int i = 0; i < n; i++)
            {
                int o = i * RecordSize;
                proto[i] = Mathf.Clamp(BitConverter.ToInt32(b, o), 0, pc - 1);
                pos[i] = new Vector3(BitConverter.ToSingle(b, o + 4), BitConverter.ToSingle(b, o + 8), BitConverter.ToSingle(b, o + 12));
                rot[i] = new Quaternion(BitConverter.ToSingle(b, o + 16), BitConverter.ToSingle(b, o + 20), BitConverter.ToSingle(b, o + 24), BitConverter.ToSingle(b, o + 28));
                scl[i] = BitConverter.ToSingle(b, o + 32);
                long key = ((long)Mathf.FloorToInt(pos[i].x / cellSize) << 32) ^ (uint)Mathf.FloorToInt(pos[i].z / cellSize);
                if (!cellIndex.TryGetValue(key, out int c))
                {
                    c = _cells.Count;
                    cellIndex[key] = c;
                    _cells.Add(new Cell { bounds = new Bounds(pos[i], Vector3.zero), start = new int[pc], count = new int[pc] });
                }
                cellOf[i] = c;
                _cells[c].count[proto[i]]++;
            }
            // counting sort by (cell, prototype)
            int run = 0;
            foreach (Cell c in _cells)
                for (int p = 0; p < pc; p++) { c.start[p] = run; run += c.count[p]; c.count[p] = 0; }
            _matrices = new Matrix4x4[n];
            _positions = new Vector3[n];
            _scales = new float[n];
            _worldBounds = new Bounds(n > 0 ? pos[0] : Vector3.zero, Vector3.zero);
            for (int i = 0; i < n; i++)
            {
                Cell c = _cells[cellOf[i]];
                int k = c.start[proto[i]] + c.count[proto[i]]++;
                _matrices[k] = Matrix4x4.TRS(pos[i], rot[i], Vector3.one * scl[i]);
                _positions[k] = pos[i];
                _scales[k] = scl[i];
                c.maxScale = Mathf.Max(c.maxScale, scl[i]);
                // generous bounds: trees are up to ~30 m tall and ~20 m wide
                c.bounds.Encapsulate(new Bounds(pos[i] + Vector3.up * 12f * scl[i], new Vector3(20f, 26f, 20f) * scl[i]));
                _worldBounds.Encapsulate(c.bounds);
            }
            _maxDistance = 0f;
            foreach (Prototype p in prototypes)
                if (p != null && p.lods != null)
                    foreach (LodLevel l in p.lods) _maxDistance = Mathf.Max(_maxDistance, l.maxDistance);
            _ready = n > 0;
        }

        private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            // fields assigned from editor code after OnEnable (world builder): decode lazily
            if (_decodedFrom != instanceData || _decodedProtos != (prototypes != null ? prototypes.Length : 0)) { Decode(); _cams.Clear(); }
            if (!_ready || cam == null) return;
            if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            if ((cam.cullingMask & (1 << renderLayer)) == 0) return;
            if (!_cams.TryGetValue(cam, out CameraState st))
            {
                st = new CameraState();
                int pc = prototypes.Length;
                st.lists = new Matrix4x4[pc][][];
                st.counts = new int[pc][];
                for (int p = 0; p < pc; p++)
                {
                    int lc = prototypes[p].lods != null ? prototypes[p].lods.Length : 0;
                    // slots lc..2lc-1: the same levels drawn with their capless mesh
                    st.lists[p] = new Matrix4x4[lc * 2][];
                    st.counts[p] = new int[lc * 2];
                    for (int l = 0; l < lc * 2; l++) st.lists[p][l] = new Matrix4x4[l < lc ? 256 : 16];
                }
                _cams[cam] = st;
                if (_cams.Count > 16) PruneCameras();
            }
            Transform t = cam.transform;
            if (st.fov < 0f || (t.position - st.pos).sqrMagnitude > moveThreshold * moveThreshold ||
                Quaternion.Angle(t.rotation, st.rot) > turnThreshold || Mathf.Abs(cam.fieldOfView - st.fov) > 0.5f)
            {
                st.pos = t.position; st.rot = t.rotation; st.fov = cam.fieldOfView;
                Rebuild(cam, st);
            }
            Submit(cam, st);
        }

        private void PruneCameras()
        {
            var dead = new List<Camera>();
            foreach (var kv in _cams) if (kv.Key == null) dead.Add(kv.Key);
            foreach (Camera c in dead) _cams.Remove(c);
        }

        // LOD distances are authored for a 70 deg vertical field of view; a narrower lens keeps detail further out
        private static float FovScale(Camera cam) => Mathf.Tan(35f * Mathf.Deg2Rad) / Mathf.Max(0.05f, Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));

        private void Rebuild(Camera cam, CameraState st)
        {
            for (int p = 0; p < st.counts.Length; p++)
                for (int l = 0; l < st.counts[p].Length; l++) st.counts[p][l] = 0;
            GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            Vector3 cp = cam.transform.position;
            float fovK = FovScale(cam) * QualitySettings.lodBias;
            float cull = _maxDistance * 1.7f * fovK;
            float near2 = perInstanceDistance * perInstanceDistance;
            int pc = prototypes.Length;
            if (st.cellCapless == null || st.cellCapless.Length != _cells.Count) st.cellCapless = new bool[_cells.Count];
            if (st.instCapless == null || st.instCapless.Length != _matrices.Length) st.instCapless = new bool[_matrices.Length];
            for (int ci = 0; ci < _cells.Count; ci++)
            {
                Cell c = _cells[ci];
                float d2 = c.bounds.SqrDistance(cp);
                if (d2 > cull * cull) continue;
                // near cells are kept even when off screen (their shadows can fall into view)
                if (d2 > 60f * 60f && !GeometryUtility.TestPlanesAABB(_planes, c.bounds)) continue;
                bool perInstance = d2 < near2;
                float cellD = Mathf.Sqrt(d2);
                for (int p = 0; p < pc; p++)
                {
                    int cnt = c.count[p];
                    if (cnt == 0) continue;
                    LodLevel[] lods = prototypes[p].lods;
                    if (lods == null || lods.Length == 0) continue;
                    int s0 = c.start[p];
                    if (!perInstance)
                    {
                        // whole cell on one level (by its nearest point and its largest plant)
                        int lod = PickLod(lods, cellD / (c.maxScale * fovK));
                        if (lod < 0) continue;
                        if (lods[lod].capless != null && Capless(ref st.cellCapless[ci], cp, c.bounds.center)) lod += lods.Length;
                        Append(st, p, lod, s0, cnt);
                        continue;
                    }
                    for (int i = s0; i < s0 + cnt; i++)
                    {
                        float d = Vector3.Distance(cp, _positions[i]) / (_scales[i] * fovK);
                        int lod = PickLod(lods, d);
                        if (lod < 0) continue;
                        if (lods[lod].capless != null && Capless(ref st.instCapless[i], cp, _positions[i] + Vector3.up * (12f * _scales[i]))) lod += lods.Length;
                        Append(st, p, lod, i, 1);
                    }
                }
            }
        }

        /// <summary>
        /// True where the capless mesh is drawn: the camera looks down on the crown centre by less than capElevation,
        /// with hysteresis (state keeps the last choice while the slope is inside capElevation +- capHysteresis).
        /// </summary>
        private bool Capless(ref bool state, Vector3 cam, Vector3 crown)
        {
            float dx = crown.x - cam.x, dz = crown.z - cam.z;
            float slope = (cam.y - crown.y) / Mathf.Max(Mathf.Sqrt(dx * dx + dz * dz), 1f);
            if (slope > capElevation + capHysteresis) state = false;
            else if (slope < capElevation - capHysteresis) state = true;
            return state;
        }

        private static int PickLod(LodLevel[] lods, float scaledDistance)
        {
            for (int l = 0; l < lods.Length; l++)
                if (scaledDistance <= lods[l].maxDistance) return lods[l].mesh != null ? l : -1;
            return -1;
        }

        private void Append(CameraState st, int p, int lod, int start, int count)
        {
            Matrix4x4[] list = st.lists[p][lod];
            int have = st.counts[p][lod];
            if (have + count > list.Length)
            {
                Array.Resize(ref list, Mathf.Max(list.Length * 2, have + count));
                st.lists[p][lod] = list;
            }
            Array.Copy(_matrices, start, list, have, count);
            st.counts[p][lod] = have + count;
        }

        private void Submit(Camera cam, CameraState st)
        {
            for (int p = 0; p < prototypes.Length; p++)
            {
                LodLevel[] lods = prototypes[p].lods;
                for (int l = 0; l < lods.Length * 2; l++)
                {
                    int count = st.counts[p][l];
                    LodLevel lv = lods[l % lods.Length];
                    bool caplessSlot = l >= lods.Length;
                    Mesh mesh = caplessSlot ? lv.capless : lv.mesh;
                    if (count == 0 || mesh == null || lv.materials == null) continue;
                    int subs = Mathf.Min(mesh.subMeshCount, lv.materials.Length);
                    for (int s = 0; s < subs; s++)
                    {
                        Material m = lv.materials[s];
                        if (m == null) continue;
                        var rp = new RenderParams(m)
                        {
                            camera = cam,
                            layer = renderLayer,
                            worldBounds = _worldBounds,
                            shadowCastingMode = lv.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                            receiveShadows = true,
                            motionVectorMode = MotionVectorGenerationMode.Camera,
                            lightProbeUsage = LightProbeUsage.Off,
                            reflectionProbeUsage = ReflectionProbeUsage.BlendProbes,
                        };
                        // fix round: the shadow of a capless crown is still the capped mesh's (shadows do not change
                        // with the camera height): the capless copy is drawn unshadowed, the capped one shadows-only
                        if (caplessSlot) rp.shadowCastingMode = ShadowCastingMode.Off;
                        for (int start = 0; start < count; start += MaxBatch)
                            Graphics.RenderMeshInstanced(rp, mesh, s, st.lists[p][l], Mathf.Min(MaxBatch, count - start), start);
                        if (caplessSlot && lv.castShadows && lv.mesh != null && s < lv.mesh.subMeshCount)
                        {
                            rp.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                            for (int start = 0; start < count; start += MaxBatch)
                                Graphics.RenderMeshInstanced(rp, lv.mesh, s, st.lists[p][l], Mathf.Min(MaxBatch, count - start), start);
                        }
                    }
                }
            }
        }
    }
}
