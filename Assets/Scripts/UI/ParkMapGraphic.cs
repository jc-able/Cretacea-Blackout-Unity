using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cretacea.UI
{
    /// <summary>
    /// Vector map layer: lines (solid or dashed), filled rectangles, polygons and dots given in world XZ metres,
    /// drawn into this RectTransform through a uniform world-to-map fit. Line widths and dot sizes are in reference
    /// pixels. Edges get a one-pixel alpha fringe so thin lines stay smooth at 720p.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class ParkMapGraphic : MaskableGraphic
    {
        private enum Kind { Line, Rect, Poly, Dot }

        private struct Shape
        {
            public Kind Kind;
            public Vector2[] Pts;     // world XZ
            public float Width;       // ref px (line width / dot diameter)
            public float Dash, Gap;   // ref px; 0 = solid
            public Color Color;
        }

        private readonly List<Shape> _shapes = new List<Shape>();
        private Rect _world = new Rect(0f, 0f, 100f, 100f);

        /// <summary>The world XZ rectangle this map shows (fits inside the rect, centred).</summary>
        public Rect World
        {
            get => _world;
            set { _world = value; SetVerticesDirty(); }
        }

        public void Clear() { _shapes.Clear(); SetVerticesDirty(); }

        public void Line(Color c, float width, params Vector2[] pts) => Add(Kind.Line, pts, width, 0f, 0f, c);
        public void Dashed(Color c, float width, float dash, float gap, params Vector2[] pts) => Add(Kind.Line, pts, width, dash, gap, c);
        public void Poly(Color c, params Vector2[] pts) => Add(Kind.Poly, pts, 0f, 0f, 0f, c);
        public void Dot(Color c, Vector2 p, float diameter) => Add(Kind.Dot, new[] { p }, diameter, 0f, 0f, c);

        public void Box(Color fill, float xMin, float xMax, float zMin, float zMax) =>
            Add(Kind.Rect, new[] { new Vector2(xMin, zMin), new Vector2(xMax, zMax) }, 0f, 0f, 0f, fill);

        public void Outline(Color c, float width, float xMin, float xMax, float zMin, float zMax, float dash = 0f, float gap = 0f) =>
            Add(Kind.Line, new[] { new Vector2(xMin, zMin), new Vector2(xMax, zMin), new Vector2(xMax, zMax), new Vector2(xMin, zMax), new Vector2(xMin, zMin) }, width, dash, gap, c);

        private void Add(Kind k, Vector2[] pts, float w, float dash, float gap, Color c)
        {
            if (pts == null || pts.Length == 0) return;
            _shapes.Add(new Shape { Kind = k, Pts = pts, Width = w, Dash = dash, Gap = gap, Color = c });
            SetVerticesDirty();
        }

        // ---- world <-> map ------------------------------------------------------------------------------

        private void Fit(out float scale, out Vector2 origin)
        {
            Rect r = rectTransform.rect;
            scale = Mathf.Min(r.width / Mathf.Max(0.01f, _world.width), r.height / Mathf.Max(0.01f, _world.height));
            Vector2 size = new Vector2(_world.width, _world.height) * scale;
            origin = r.center - size * 0.5f;
        }

        /// <summary>World XZ to local position inside this RectTransform.</summary>
        public Vector2 ToLocal(Vector2 xz)
        {
            Fit(out float s, out Vector2 o);
            return o + new Vector2(xz.x - _world.xMin, xz.y - _world.yMin) * s;
        }

        /// <summary>World XZ to normalised (0..1) coordinates of this RectTransform (anchor space).</summary>
        public Vector2 ToNormalized(Vector2 xz)
        {
            Rect r = rectTransform.rect;
            Vector2 l = ToLocal(xz);
            return new Vector2((l.x - r.xMin) / Mathf.Max(1f, r.width), (l.y - r.yMin) / Mathf.Max(1f, r.height));
        }

        /// <summary>Reference pixels per world metre at the current size.</summary>
        public float PixelsPerMetre { get { Fit(out float s, out _); return s; } }

        // ---- mesh -----------------------------------------------------------------------------------------

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_shapes.Count == 0) return;
            Fit(out float s, out Vector2 o);
            foreach (Shape sh in _shapes)
            {
                Color32 col = sh.Color * color;
                switch (sh.Kind)
                {
                    case Kind.Rect:
                    {
                        Vector2 a = o + (sh.Pts[0] - _world.min) * s, b = o + (sh.Pts[1] - _world.min) * s;
                        Quad(vh, new Vector2(a.x, a.y), new Vector2(b.x, a.y), new Vector2(b.x, b.y), new Vector2(a.x, b.y), col);
                        break;
                    }
                    case Kind.Poly:
                    {
                        int start = vh.currentVertCount;
                        for (int i = 0; i < sh.Pts.Length; i++) vh.AddVert(o + (sh.Pts[i] - _world.min) * s, col, Vector4.zero);
                        for (int i = 1; i < sh.Pts.Length - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
                        break;
                    }
                    case Kind.Dot:
                        Disc(vh, o + (sh.Pts[0] - _world.min) * s, sh.Width * 0.5f, col);
                        break;
                    case Kind.Line:
                    {
                        var p = new Vector2[sh.Pts.Length];
                        for (int i = 0; i < p.Length; i++) p[i] = o + (sh.Pts[i] - _world.min) * s;
                        if (sh.Dash > 0f) DashedLine(vh, p, sh.Width, sh.Dash, sh.Gap, col);
                        else for (int i = 0; i < p.Length - 1; i++) Segment(vh, p[i], p[i + 1], sh.Width, col, i > 0);
                        break;
                    }
                }
            }
        }

        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 col)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, col, Vector4.zero); vh.AddVert(b, col, Vector4.zero);
            vh.AddVert(c, col, Vector4.zero); vh.AddVert(d, col, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }

        /// <summary>A line segment with a 1 px soft fringe; joins get a round cap so polylines have no notches.</summary>
        private static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float w, Color32 col, bool capStart)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) return;
            Vector2 n = new Vector2(-d.y, d.x) / len;
            float h = Mathf.Max(0.35f, w * 0.5f - 0.5f);
            Color32 clear = new Color32(col.r, col.g, col.b, 0);
            int i = vh.currentVertCount;
            Vector2 no = n * (h + 1f), ni = n * h;
            vh.AddVert(a + no, clear, Vector4.zero); vh.AddVert(a + ni, col, Vector4.zero); vh.AddVert(a - ni, col, Vector4.zero); vh.AddVert(a - no, clear, Vector4.zero);
            vh.AddVert(b + no, clear, Vector4.zero); vh.AddVert(b + ni, col, Vector4.zero); vh.AddVert(b - ni, col, Vector4.zero); vh.AddVert(b - no, clear, Vector4.zero);
            for (int k = 0; k < 3; k++) { vh.AddTriangle(i + k, i + 4 + k, i + 5 + k); vh.AddTriangle(i + k, i + 5 + k, i + 1 + k); }
            if (capStart && w >= 2.5f) Disc(vh, a, w * 0.5f, col);
        }

        private static void DashedLine(VertexHelper vh, Vector2[] p, float w, float dash, float gap, Color32 col)
        {
            float phase = 0f;   // distance into the current dash+gap period, carried across corners
            float period = dash + gap;
            for (int i = 0; i < p.Length - 1; i++)
            {
                Vector2 a = p[i], b = p[i + 1];
                float len = Vector2.Distance(a, b);
                if (len < 0.01f) continue;
                Vector2 dir = (b - a) / len;
                float t = 0f;
                while (t < len)
                {
                    float inPeriod = phase % period;
                    if (inPeriod < dash)
                    {
                        float run = Mathf.Min(dash - inPeriod, len - t);
                        Segment(vh, a + dir * t, a + dir * (t + run), w, col, false);
                        t += run; phase += run;
                    }
                    else
                    {
                        float run = Mathf.Min(period - inPeriod, len - t);
                        t += run; phase += run;
                    }
                }
            }
        }

        private static void Disc(VertexHelper vh, Vector2 c, float r, Color32 col)
        {
            int seg = Mathf.Clamp(Mathf.CeilToInt(r * 2.2f), 10, 40);
            Color32 clear = new Color32(col.r, col.g, col.b, 0);
            int centre = vh.currentVertCount;
            vh.AddVert(c, col, Vector4.zero);
            float ri = Mathf.Max(0.2f, r - 0.5f), ro = r + 0.5f;
            for (int k = 0; k <= seg; k++)
            {
                float a = k * Mathf.PI * 2f / seg;
                Vector2 u = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(c + u * ri, col, Vector4.zero);
                vh.AddVert(c + u * ro, clear, Vector4.zero);
            }
            for (int k = 0; k < seg; k++)
            {
                int i0 = centre + 1 + k * 2, i1 = i0 + 2;
                vh.AddTriangle(centre, i0, i1);
                vh.AddTriangle(i0, i0 + 1, i1 + 1);
                vh.AddTriangle(i0, i1 + 1, i1);
            }
        }
    }
}
