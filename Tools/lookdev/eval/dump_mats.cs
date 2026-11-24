using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
var sb = new StringBuilder();
var use = new Dictionary<Material, int>();
var rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
foreach (var r in rends) foreach (var m in r.sharedMaterials) { if (m == null) continue; use.TryGetValue(m, out int c); use[m] = c + 1; }
sb.AppendLine("renderers=" + rends.Length + " materials=" + use.Count);
string F(Color c) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:F2},{1:F2},{2:F2},{3:F2})", c.r, c.g, c.b, c.a);
foreach (var kv in use.OrderByDescending(k => k.Value))
{
    var m = kv.Key;
    string path = UnityEditor.AssetDatabase.GetAssetPath(m);
    var line = new StringBuilder();
    line.Append(kv.Value + "x|" + m.name + "|" + m.shader.name + "|" + path);
    if (m.HasProperty("_BaseColor")) line.Append("|base=" + F(m.GetColor("_BaseColor")));
    if (m.HasProperty("_BaseColorMap")) { var t = m.GetTexture("_BaseColorMap"); line.Append("|map=" + (t != null ? t.name : "NONE")); var s = m.GetTextureScale("_BaseColorMap"); line.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, "|tile=({0:F2},{1:F2})", s.x, s.y)); }
    if (m.HasProperty("_NormalMap")) { var t = m.GetTexture("_NormalMap"); line.Append("|nrm=" + (t != null ? "y" : "n")); }
    if (m.HasProperty("_MaskMap")) { var t = m.GetTexture("_MaskMap"); line.Append("|mask=" + (t != null ? "y" : "n")); }
    if (m.HasProperty("_Smoothness")) line.Append("|sm=" + m.GetFloat("_Smoothness").ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
    if (m.HasProperty("_Metallic")) line.Append("|met=" + m.GetFloat("_Metallic").ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
    if (m.HasProperty("_EmissiveColor")) { var e = m.GetColor("_EmissiveColor"); if (e.maxColorComponent > 0.0001f) line.Append("|emis=" + F(e)); }
    if (m.HasProperty("_SurfaceType")) line.Append("|surf=" + m.GetFloat("_SurfaceType"));
    if (m.HasProperty("_MaterialID")) line.Append("|matid=" + m.GetFloat("_MaterialID"));
    if (m.HasProperty("_UVBase")) line.Append("|uv=" + m.GetFloat("_UVBase"));
    sb.AppendLine(line.ToString());
}
var dumpDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath), "Logs", "dump");
System.IO.Directory.CreateDirectory(dumpDir);
System.IO.File.WriteAllText(System.IO.Path.Combine(dumpDir, "materials.txt"), sb.ToString());
return "mats=" + use.Count;
