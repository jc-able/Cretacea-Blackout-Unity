// Edit-mode turn simulation (run with `unity command script eval --file`): walk/run straight 1.5 s, turn 180 deg at the
// species' turn rate (and x1.35 for NavMesh angularSpeed headroom) while moving, then straight 1.5 s, 60 Hz, flat ground.
// For every frame in which the CLIP says a foot is planted (rig AnimLift < swingLo in this and the previous frame) it
// measures the foot's world displacement: a locked foot moves 0, a leashed foot eases, a release/re-lock POP shows as a
// one-frame jump. Reports the largest jump and the mean planted-foot slide speed during the turn.
using UnityEngine; using UnityEditor; using System.Linq; using System.Text; using System.Reflection;
var sb = new StringBuilder();
var T = typeof(Cretacea.AI.DinoProceduralRig);
foreach (var sp in new[]{"Velociraptor","Tyrannosaurus","Triceratops","Brachiosaurus"}) {
  var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Dinosaurs/PF_{sp}.prefab");
  var go = (GameObject)Object.Instantiate(pf); go.hideFlags = HideFlags.DontSave;
  foreach (var b in go.GetComponents<MonoBehaviour>()) if (!(b is Cretacea.AI.DinoProceduralRig)) b.enabled = false;
  var nav = go.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (nav) nav.enabled = false;
  go.transform.position = new Vector3(0, -800, 0);
  var anim = go.GetComponent<Animator>(); var rig = go.GetComponent<Cretacea.AI.DinoProceduralRig>();
  var cfg = (Cretacea.AI.DinoConfig)new SerializedObject(rig).FindProperty("config").objectReferenceValue;
  var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.hideFlags = HideFlags.DontSave; ground.transform.position = new Vector3(0,-800.5f,0); ground.transform.localScale = new Vector3(400,1,400); Physics.SyncTransforms();
  var tick = T.GetMethod("Tick", BindingFlags.NonPublic|BindingFlags.Instance);
  T.GetMethod("Awake", BindingFlags.NonPublic|BindingFlags.Instance).Invoke(rig, null);
  var limbsF = T.GetField("_limbs", BindingFlags.NonPublic|BindingFlags.Instance);
  float scale = Mathf.Max(0.2f, cfg.agentHeight / 1.67f); float swingLo = 0.05f * scale;
  foreach (float v in new[]{cfg.walkSpeed, cfg.runSpeed}) foreach (float rateMul in new[]{0f, 1f, 1.35f}) {
    anim.Rebind(); anim.Update(0f);
    go.transform.position = new Vector3(0,-800,0); go.transform.rotation = Quaternion.identity;
    float rate = cfg.turnSpeedDegPerSec * rateMul;
    float dt = 1f/60f; float turnT = rateMul > 0 ? 180f / rate : 1f; int N = Mathf.CeilToInt((3f + turnT) / dt);
    int nf = ((System.Collections.IList)limbsF.GetValue(rig)).Count;
    var prevP = new Vector3[nf]; var prevPl = new bool[nf]; var prevD = new float[nf];
    float pop = 0; string popAt = ""; float slip = 0; int slipN = 0; float turned = 0;
    for (int i = 0; i < N; i++) {
      float t = i * dt;
      if (rateMul > 0 && t > 1.5f && turned < 180f) { float d = Mathf.Min(rate * dt, 180f - turned); turned += d; go.transform.Rotate(0, d, 0); }
      float mult = Mathf.Clamp(v / Mathf.Max(0.2f, cfg.EvaluateExpectedClipStrideSpeed(v)), cfg.minAnimSpeedScale, cfg.maxAnimSpeedScale);
      anim.SetFloat("Speed", v); anim.SetFloat("LocomotionSpeedMult", mult);
      go.transform.position += go.transform.forward * v * dt;
      anim.Update(dt);
      tick.Invoke(rig, new object[]{dt});
      var list = (System.Collections.IList)limbsF.GetValue(rig);
      for (int k = 0; k < nf; k++) {
        var L = list[k]; var LT = L.GetType();
        var foot = (Transform)LT.GetField("Foot").GetValue(L);
        bool pl = (float)LT.GetField("AnimLift").GetValue(L) < swingLo;
        Vector3 p = foot.position;
        float d = new Vector2(p.x - prevP[k].x, p.z - prevP[k].z).magnitude;
        if (i > 40 && pl && prevPl[k]) {
          float jump = d - prevD[k];                       // sudden change of the foot's step (a pop), not a steady slide
          if (jump > pop) { pop = jump; popAt = $"{foot.name}@{t:F2}s d={d*100:F1}cm"; }
          if (t >= 1.5f && t <= 1.5f + turnT + 0.5f) { slip += d / dt; slipN++; }
        }
        prevP[k] = p; prevPl[k] = pl; prevD[k] = (pl && prevPl[k]) ? d : d;
      }
    }
    sb.AppendLine($"{sp} v={v:F2} turn={rate:F0}deg/s: largest one-frame jump of a planted foot={pop*100:F1} cm ({popAt}); planted-foot slide during turn={(slipN>0?slip/slipN:0):F2} m/s");
  }
  Object.DestroyImmediate(go); Object.DestroyImmediate(ground);
}
return sb.ToString();
