var c = new UnityEngine.Vector3(-172f, 1.5f, -247f);
var counts = new System.Collections.Generic.Dictionary<string, int>();
foreach (var r in UnityEngine.Object.FindObjectsByType<UnityEngine.Renderer>(UnityEngine.FindObjectsInactive.Exclude))
{
    if ((r.bounds.center - c).magnitude > 16f) continue;
    string n = r.transform.root.name + "/" + (r.transform.parent != null && r.transform.parent.parent != null ? r.transform.parent.parent.name : "") + "/" + System.Text.RegularExpressions.Regex.Replace(r.name, "[0-9_]+$", "");
    counts[n] = counts.TryGetValue(n, out int v) ? v + 1 : 1;
}
var sb = new System.Text.StringBuilder();
foreach (var kv in counts) sb.Append(kv.Value + " x " + kv.Key + "\n");
foreach (var t in UnityEngine.Terrain.activeTerrains)
{
    var td = t.terrainData; var p = t.transform.position;
    if (c.x < p.x || c.x > p.x + td.size.x || c.z < p.z || c.z > p.z + td.size.z) continue;
    int dx = (int)((c.x - p.x) / td.size.x * td.detailWidth), dz = (int)((c.z - p.z) / td.size.z * td.detailHeight);
    sb.Append("terrain " + t.name + " details=" + td.detailPrototypes.Length + " patchRes=" + td.detailResolution + "\n");
    for (int i = 0; i < td.detailPrototypes.Length; i++)
    {
        int[,] m = td.GetDetailLayer(System.Math.Max(0, dx - 8), System.Math.Max(0, dz - 8), 16, 16, i);
        int sum = 0; foreach (int v in m) sum += v;
        var dp = td.detailPrototypes[i];
        sb.Append("  detail " + i + " " + (dp.prototype != null ? dp.prototype.name : (dp.prototypeTexture != null ? dp.prototypeTexture.name : "?")) + " sum16x16=" + sum + " h=" + dp.minHeight + "-" + dp.maxHeight + " w=" + dp.minWidth + "-" + dp.maxWidth + "\n");
    }
    sb.Append("  ground y=" + (t.SampleHeight(c) + p.y).ToString("F2") + "\n");
}
return sb.ToString();
