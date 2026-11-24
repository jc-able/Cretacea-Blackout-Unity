using UnityEngine; using UnityEditor; using System.IO; using System.Linq;
string outDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "Screenshots/_work/mesh");
string r = "";
foreach (var s in new[]{"Velociraptor","Tyrannosaurus","Triceratops","Brachiosaurus"}) {
  var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Dinosaurs/PF_{s}.prefab");
  var smr = pf.GetComponentInChildren<SkinnedMeshRenderer>();
  var m = smr.sharedMesh;
  var v = m.vertices; var uv = m.uv; var bw = m.boneWeights; var tri = m.triangles; var nrm = m.normals;
  using (var w = new BinaryWriter(File.Create($"{outDir}/{s}.bin"))) {
    w.Write(v.Length); w.Write(tri.Length);
    foreach (var x in v) { w.Write(x.x); w.Write(x.y); w.Write(x.z); }
    foreach (var x in nrm) { w.Write(x.x); w.Write(x.y); w.Write(x.z); }
    foreach (var x in uv) { w.Write(x.x); w.Write(x.y); }
    foreach (var x in bw) { w.Write(x.boneIndex0); w.Write(x.weight0); w.Write(x.boneIndex1); w.Write(x.weight1); }
    foreach (var t in tri) w.Write(t);
  }
  File.WriteAllText($"{outDir}/{s}_bones.txt", string.Join("\n", smr.bones.Select(b => b.name)));
  r += $"{s} v={v.Length} t={tri.Length/3} subMeshes={m.subMeshCount} uvChannels2={m.uv2?.Length}\n";
}
return r;
