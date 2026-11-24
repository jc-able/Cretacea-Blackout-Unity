// Edit-mode locomotion simulation: animator + root motion along +Z at speed v + DinoProceduralRig.LateUpdate.
// Reports stance-foot world slip speed (should be ~0) and min sole gap per species/speed.
using UnityEngine; using UnityEditor; using System.Linq; using System.Text; using System.Reflection;
var sb = new StringBuilder();
foreach (var sp in new[]{"Velociraptor","Tyrannosaurus","Triceratops","Brachiosaurus"}) {
  var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Dinosaurs/PF_{sp}.prefab");
  var go = (GameObject)Object.Instantiate(pf); go.hideFlags = HideFlags.DontSave;
  go.transform.position = new Vector3(0, -800, 0);
  var anim = go.GetComponent<Animator>(); var rig = go.GetComponent<Cretacea.AI.DinoProceduralRig>();
  var cfg = (Cretacea.AI.DinoConfig)new SerializedObject(rig).FindProperty("config").objectReferenceValue;
  // flat ground collider
  var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.hideFlags = HideFlags.DontSave; ground.transform.position = new Vector3(0,-800.5f,500); ground.transform.localScale = new Vector3(60,1,1200); Physics.SyncTransforms();
  var late = typeof(Cretacea.AI.DinoProceduralRig).GetMethod("Tick", BindingFlags.NonPublic|BindingFlags.Instance);
  var awake = typeof(Cretacea.AI.DinoProceduralRig).GetMethod("Awake", BindingFlags.NonPublic|BindingFlags.Instance);
  awake.Invoke(rig, null);
  var ov = cfg.rigOverrides;
  var feet = new[]{ov.leftFootBone, ov.rightFootBone, ov.leftFrontFootBone, ov.rightFrontFootBone}.Where(n=>!string.IsNullOrEmpty(n)).Select(n=>go.GetComponentsInChildren<Transform>().First(t=>t.name==n)).ToArray();
  foreach (float v in new[]{cfg.stalkSpeed, cfg.walkSpeed, (cfg.walkSpeed+cfg.runSpeed)*0.5f, cfg.runSpeed}) {
    anim.Rebind(); anim.Update(0f);
    go.transform.position = new Vector3(0,-800,0);
    float dt = 1f/60f; int N = 360;
    var prev = new Vector3[feet.Length]; var minY = new float[feet.Length];
    var hs = new float[N, feet.Length]; var sl = new float[N, feet.Length];
    for (int i = 0; i < N; i++) {
      float expected = cfg.EvaluateExpectedClipStrideSpeed(v);
      float mult = Mathf.Clamp(v / Mathf.Max(0.2f, expected), cfg.minAnimSpeedScale, cfg.maxAnimSpeedScale);
      anim.SetFloat("Speed", v); anim.SetFloat("LocomotionSpeedMult", mult);
      go.transform.position += Vector3.forward * v * dt;
      anim.Update(dt);
      Time.captureDeltaTime = dt;
      late.Invoke(rig, new object[]{dt});
      for (int k = 0; k < feet.Length; k++) { var p = feet[k].position; hs[i,k] = p.y + 800f; sl[i,k] = i > 0 ? new Vector2(p.x - prev[k].x, p.z - prev[k].z).magnitude / dt : 0; prev[k] = p; }
    }
    // stance = frames within 2 cm of each foot's min height (after warm-up)
    float slipSum = 0; int slipN = 0; float minH = 9;
    for (int k = 0; k < feet.Length; k++) { float mn = 9; for (int i = 120; i < N; i++) mn = Mathf.Min(mn, hs[i,k]); minH = Mathf.Min(minH, mn);
      for (int i = 121; i < N; i++) if (hs[i,k] - mn < 0.02f) { slipSum += sl[i,k]; slipN++; } }
    var stTime = anim.GetCurrentAnimatorStateInfo(0);
    sb.AppendLine($"{sp} v={v:F2} mult={Mathf.Clamp(v / Mathf.Max(0.2f, cfg.EvaluateExpectedClipStrideSpeed(v)), cfg.minAnimSpeedScale, cfg.maxAnimSpeedScale):F2} stanceSlip={(slipN>0?slipSum/slipN:0):F2} m/s ({(slipN>0?100*slipSum/slipN/v:0):F0}% of v) footMinY={minH:F3}");
  }
  Object.DestroyImmediate(go); Object.DestroyImmediate(ground);
}
return sb.ToString();
