var counts = new System.Collections.Generic.Dictionary<string, int>();
foreach (var c in UnityEngine.Object.FindObjectsByType<UnityEngine.Collider>())
{
    if (c.isTrigger || c is UnityEngine.TerrainCollider || c is UnityEngine.CharacterController) continue;
    if (c.GetComponent<UnityEngine.Renderer>() != null || c.GetComponentInChildren<UnityEngine.Renderer>() != null) continue;
    string n = System.Text.RegularExpressions.Regex.Replace(c.name, "[0-9]+", "#");
    string p = c.transform.parent != null ? System.Text.RegularExpressions.Regex.Replace(c.transform.parent.name, "[0-9]+", "#") : "-";
    if (p.Length > 18) p = p.Substring(0, 18);
    string k = c.GetType().Name.Replace("Collider", "") + ":" + n + " <" + p + ">";
    counts.TryGetValue(k, out int v); counts[k] = v + 1;
}
var list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(counts);
list.Sort((a, b) => b.Value.CompareTo(a.Value));
var sb = new System.Text.StringBuilder();
for (int i = 0; i < list.Count && i < 45; i++) sb.Append(list[i].Value + "x " + list[i].Key + " | ");
var merged = new System.Collections.Generic.Dictionary<string, int>();
foreach (var mr in UnityEngine.Object.FindObjectsByType<UnityEngine.MeshRenderer>())
{
    if (mr.GetComponentInParent<UnityEngine.Collider>() != null || mr.GetComponentInChildren<UnityEngine.Collider>() != null) continue;
    var mf = mr.GetComponent<UnityEngine.MeshFilter>(); if (mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount < 400) continue;
    string k = System.Text.RegularExpressions.Regex.Replace(mr.name, "[0-9]+", "#"); if (k.Length > 26) k = k.Substring(0, 26);
    merged.TryGetValue(k, out int v); merged[k] = v + 1;
}
var l2 = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(merged);
l2.Sort((a, b) => b.Value.CompareTo(a.Value));
sb.Append(" ### BIG RENDERERS WITHOUT COLLIDER: ");
for (int i = 0; i < l2.Count && i < 25; i++) sb.Append(l2[i].Value + "x " + l2[i].Key + " | ");
return sb.ToString();
