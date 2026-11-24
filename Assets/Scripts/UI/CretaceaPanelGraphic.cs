using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cretacea.UI
{
    public enum UiPanelStyle
    {
        /// <summary>Hard-edged rectangle (backdrops, stripes, fades) — the plain uGUI Image.</summary>
        Flat,
        /// <summary>Rounded corners only (bars, tracks, fills).</summary>
        Rounded,
        /// <summary>Rounded, 1 px warm border and a soft drop shadow (HUD boxes, cards, modals).</summary>
        Card
    }

    /// <summary>
    /// An Image that draws a panel as up to three nine-sliced layers from small procedural textures made at the
    /// exact radius and border width asked for: a soft shadow that spills outside the rect, the rounded fill
    /// (tinted by <see cref="Graphic.color"/>) and a thin border ring (<see cref="borderColor"/>). When a sprite is
    /// assigned, or the style is Flat, it behaves exactly like a plain Image, so existing code that swaps sprites or
    /// fill modes keeps working.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class CretaceaPanelGraphic : Image
    {
        public UiPanelStyle style = UiPanelStyle.Flat;
        public float radius = 4f;              // reference px (1080p)
        public Color borderColor = new Color(0f, 0f, 0f, 0f);
        public float borderWidth = 1f;         // reference px
        public float shadowAlpha;              // 0 = no shadow
        public float shadowSpread = 14f;       // reference px outside the rect
        public Vector2 shadowOffset = new Vector2(0f, -4f);

        private bool PanelMode => sprite == null && style != UiPanelStyle.Flat && type == Type.Simple;

        public override Texture mainTexture => PanelMode ? UiShapeTextures.Get(radius, borderWidth, shadowSpread).Texture : base.mainTexture;

        public void SetStyle(UiPanelStyle newStyle, float newRadius, Color border, float border01Width, float shadow)
        {
            style = newStyle;
            radius = Mathf.Max(0.5f, newRadius);
            borderColor = border;
            borderWidth = Mathf.Max(0.5f, border01Width);
            shadowAlpha = shadow;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        public void SetBorder(Color border, float width)
        {
            if (borderColor == border && Mathf.Approximately(borderWidth, width)) return;
            bool texChange = !Mathf.Approximately(borderWidth, width);
            borderColor = border;
            borderWidth = width;
            if (texChange) SetMaterialDirty();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if (!PanelMode)
            {
                base.OnPopulateMesh(vh);
                return;
            }

            vh.Clear();
            UiShapeTextures.Entry e = UiShapeTextures.Get(radius, borderWidth, shadowSpread);
            Rect r = GetPixelAdjustedRect();
            float rr = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f);
            float k = UiShapeTextures.TexPerRef;

            if (shadowAlpha > 0.001f && style == UiPanelStyle.Card)
            {
                Rect sr = new Rect(r.x - shadowSpread + shadowOffset.x, r.y - shadowSpread + shadowOffset.y,
                    r.width + shadowSpread * 2f, r.height + shadowSpread * 2f);
                AddSliced(vh, sr, e.ShadowUv, e.ShadowSlice / k, e.ShadowSlice, e.Texture, new Color(0f, 0f, 0f, shadowAlpha * color.a));
            }

            AddSliced(vh, r, e.FillUv, e.Slice / k * (rr / radius), e.Slice * (rr / radius), e.Texture, color);

            if (borderColor.a > 0.001f && style == UiPanelStyle.Card)
            {
                AddSliced(vh, r, e.RingUv, e.Slice / k * (rr / radius), e.Slice * (rr / radius), e.Texture, borderColor);
            }
        }

        /// <summary>Nine-slice quad grid. borderRef = slice size on screen (ref px), borderTex = slice size in texels.</summary>
        private static void AddSliced(VertexHelper vh, Rect r, Rect uv, float borderRef, float borderTex, Texture tex, Color c)
        {
            float bx = Mathf.Min(borderRef, r.width * 0.5f);
            float by = Mathf.Min(borderRef, r.height * 0.5f);
            float ux = borderTex / tex.width * (borderRef > 0.0001f ? bx / borderRef : 0f);
            float uy = borderTex / tex.height * (borderRef > 0.0001f ? by / borderRef : 0f);

            float[] xs = { r.xMin, r.xMin + bx, r.xMax - bx, r.xMax };
            float[] ys = { r.yMin, r.yMin + by, r.yMax - by, r.yMax };
            float[] us = { uv.xMin, uv.xMin + ux, uv.xMax - ux, uv.xMax };
            float[] vs = { uv.yMin, uv.yMin + uy, uv.yMax - uy, uv.yMax };

            Color32 c32 = c;
            int start = vh.currentVertCount;
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    vh.AddVert(new Vector3(xs[x], ys[y], 0f), c32, new Vector4(us[x], vs[y], 0f, 0f));

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    int i = start + y * 4 + x;
                    vh.AddTriangle(i, i + 4, i + 5);
                    vh.AddTriangle(i, i + 5, i + 1);
                }
            }
        }
    }

    /// <summary>
    /// Procedural, cached textures for rounded panels: one texture per (radius, border width, shadow spread) with a
    /// fill region, a border-ring region and a soft-shadow region, drawn at 2 texels per reference pixel.
    /// </summary>
    public static class UiShapeTextures
    {
        public const float TexPerRef = 2f;

        public sealed class Entry
        {
            public Texture2D Texture;
            public Rect FillUv, RingUv, ShadowUv;
            public float Slice;        // corner slice of fill/ring in texels
            public float ShadowSlice;  // corner slice of the shadow in texels
        }

        private static readonly Dictionary<int, Entry> s_Cache = new Dictionary<int, Entry>();

        public static Entry Get(float radiusRef, float borderRef, float spreadRef)
        {
            int rq = Mathf.Clamp(Mathf.RoundToInt(radiusRef * 2f), 1, 400);
            int bq = Mathf.Clamp(Mathf.RoundToInt(borderRef * 4f), 1, 40);
            int sq = Mathf.Clamp(Mathf.RoundToInt(spreadRef), 1, 64);
            int key = (rq * 64 + bq) * 128 + sq;
            if (s_Cache.TryGetValue(key, out Entry cached) && cached.Texture != null) return cached;

            float R = rq * 0.5f * TexPerRef;          // corner radius in texels
            float T = bq * 0.25f * TexPerRef;         // ring thickness in texels
            float S = sq * TexPerRef;                 // shadow spread in texels
            int slice = Mathf.CeilToInt(R) + 1;
            int n = slice * 2 + 2;                    // fill / ring region size
            int shadowSlice = slice + Mathf.CeilToInt(S);
            int m = shadowSlice * 2 + 2;              // shadow region size
            const int gap = 4;
            int w = n + gap + n + gap + m;
            int h = Mathf.Max(n, m);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
            {
                name = $"UI_Panel_r{rq}_b{bq}_s{sq}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 0);

            int fillX = 0, ringX = n + gap, shadowX = n + gap + n + gap;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float sd = RoundedRectSd(x + 0.5f, y + 0.5f, 0.5f, 0.5f, n - 0.5f, n - 0.5f, R);
                    float outer = Mathf.Clamp01(0.5f - sd);
                    float inner = Mathf.Clamp01(0.5f - (sd + T));
                    px[y * w + fillX + x].a = (byte)Mathf.RoundToInt(outer * 255f);
                    px[y * w + ringX + x].a = (byte)Mathf.RoundToInt(Mathf.Clamp01(outer - inner) * 255f);
                }
            }
            for (int y = 0; y < m; y++)
            {
                for (int x = 0; x < m; x++)
                {
                    float sd = RoundedRectSd(x + 0.5f, y + 0.5f, 0.5f + S, 0.5f + S, m - 0.5f - S, m - 0.5f - S, R);
                    float a = sd <= 0f ? 1f : Mathf.Pow(1f - Mathf.SmoothStep(0f, 1f, sd / Mathf.Max(1f, S)), 2.2f);
                    px[y * w + shadowX + x].a = (byte)Mathf.RoundToInt(a * 255f);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);

            var e = new Entry
            {
                Texture = tex,
                Slice = slice,
                ShadowSlice = shadowSlice,
                FillUv = UvRect(fillX, 0, n, n, w, h),
                RingUv = UvRect(ringX, 0, n, n, w, h),
                ShadowUv = UvRect(shadowX, 0, m, m, w, h)
            };
            s_Cache[key] = e;
            return e;
        }

        private static Rect UvRect(int x, int y, int rw, int rh, int w, int h)
        {
            // inset by half a texel so bilinear filtering never reads the neighbouring region
            return Rect.MinMaxRect((x + 0.5f) / w, (y + 0.5f) / h, (x + rw - 0.5f) / w, (y + rh - 0.5f) / h);
        }

        /// <summary>Signed distance from (px,py) to a rounded rectangle [x0..x1]x[y0..y1] with corner radius r.</summary>
        private static float RoundedRectSd(float px, float py, float x0, float y0, float x1, float y1, float r)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
            float hx = (x1 - x0) * 0.5f - r, hy = (y1 - y0) * 0.5f - r;
            float qx = Mathf.Abs(px - cx) - hx, qy = Mathf.Abs(py - cy) - hy;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }
    }
}
