// PLAY-MODE frame sequences of the runtime rig (run with `unity command script eval --file` while in Play mode; the
// placeholders @SP@ @X@ @Z@ @YAW@ @SCEN@ are filled by playcap.py). Spawns the species' prefab with its AI and
// NavMeshAgent disabled, then steps Animator + DinoProceduralRig.Tick at 60 Hz in the real scene (real ground, foot
// IK/lock, jaw, tail, look-at, lighting) and renders a SIDE and a FRONT view every few frames (8 per clip) into
// Screenshots/handin/play/<species>_<clip>_<view>_NN.png. Locomotion moves the root over the ground at the scenario
// speed; "turn" walks and turns 180 deg at the species' turn rate (fixed 3/4 camera); "look" sweeps a look-at target.
using UnityEngine; using UnityEditor; using System.Linq; using System.Text; using System.Reflection; using System.IO;
using UnityEngine.Rendering.HighDefinition;
var sb = new StringBuilder();
string sp = "@SP@"; Vector3 at = new Vector3(@X@f, 0f, @Z@f); float yaw0 = @YAW@f; string[] scen = "@SCEN@".Split(',');
string outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots/handin/play"); Directory.CreateDirectory(outDir);
int gmask = Cretacea.AI.DinoLayerMasks.GroundMask;
System.Func<Vector3, float> groundY = p => Physics.Raycast(new Vector3(p.x, p.y + 30f, p.z), Vector3.down, out RaycastHit h, 80f, gmask, QueryTriggerInteraction.Ignore) ? h.point.y : p.y;
at.y = groundY(at);
var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Dinosaurs/PF_{sp}.prefab");
var holder = new GameObject("__playcap_holder"); holder.SetActive(false);
var go = (GameObject)Object.Instantiate(pf, at, Quaternion.Euler(0, yaw0, 0), holder.transform);
foreach (var b in go.GetComponents<Cretacea.AI.DinoControllerBase>()) b.enabled = false;
var nav = go.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (nav) nav.enabled = false;
go.transform.SetParent(null, true); Object.Destroy(holder); go.SetActive(true);
var anim = go.GetComponent<Animator>(); anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
var rig = go.GetComponent<Cretacea.AI.DinoProceduralRig>();
var cfg = (Cretacea.AI.DinoConfig)new SerializedObject(rig).FindProperty("config").objectReferenceValue;
var T = typeof(Cretacea.AI.DinoProceduralRig);
var tick = T.GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
float size = cfg.agentHeight; float D = Mathf.Clamp(size * 2.6f, 4.2f, 34f);
var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(); smr.updateWhenOffscreen = true; smr.forceMatrixRecalculationPerRender = true;   // skin per Camera.Render (no engine frames pass inside this call)
Camera MakeCam() { var g = new GameObject("__playcap_cam"); g.hideFlags = HideFlags.HideAndDontSave; var c = g.AddComponent<Camera>(); var hd = g.AddComponent<HDAdditionalCameraData>(); hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing; hd.volumeLayerMask = ~0; hd.customRenderingSettings = true; hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.MotionBlur] = true; hd.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.MotionBlur, false); c.fieldOfView = 38f; c.nearClipPlane = 0.05f; c.farClipPlane = 2000f; return c; }
var camS = MakeCam(); var camF = MakeCam();
var rt = new RenderTexture(new RenderTextureDescriptor(640, 360, RenderTextureFormat.ARGB32, 24) { sRGB = true }); rt.Create();
var tex = new Texture2D(640, 360, TextureFormat.RGB24, false, false);
void Shot(Camera c, string file, int renders) { c.targetTexture = rt; for (int i = 0; i < renders; i++) c.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); tex.Apply(); File.WriteAllBytes(Path.Combine(outDir, file), tex.EncodeToPNG()); c.targetTexture = null; RenderTexture.active = null; }
float clipLen(string state) { var ac = anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController; foreach (var s in ac.layers[0].stateMachine.states) if (s.state.name == state && s.state.motion is AnimationClip cl) return cl.length; return 2f; }
float dt = 1f / 60f;
bool warmed = false;
foreach (var sc in scen) {
  go.transform.position = at; go.transform.rotation = Quaternion.Euler(0, yaw0, 0);
  anim.Rebind(); anim.Update(0f);
  float v = 0f; string trig = null; string state = "Locomotion"; float len = 2.4f; bool turn = false; bool look = false; float spinIn = 0f;
  switch (sc) {
    case "idle": v = 0f; len = clipLen("Locomotion") > 0 ? 3f : 3f; break;
    case "stalk": v = cfg.stalkSpeed; break;
    case "walk": v = cfg.walkSpeed; break;
    case "run": v = cfg.runSpeed; len = 1.6f; break;
    case "turn": v = cfg.walkSpeed; turn = true; len = 0.6f + 180f / cfg.turnSpeedDegPerSec + 0.5f; break;
    case "look": v = 0f; look = true; len = 3.0f; break;
    case "roar": trig = "TriggerRoar"; state = "Roar"; break;
    case "bite": trig = "TriggerBite"; state = "Bite"; break;
    case "inspect": trig = "TriggerInspect"; state = "Inspect"; break;
    case "bash": trig = "TriggerBash"; state = "Bash"; break;
    case "turnleft": trig = "TurnLeft"; state = "Turn_Left"; spinIn = -1f; break;
  }
  if (trig != null) len = clipLen(state) + 0.3f;
  // warm-up: 1 s of the scenario's locomotion before sampling (feet settle, locks engage)
  int warm = 60, N = Mathf.CeilToInt(len / dt);
  Vector3 fixedCamPos = Vector3.zero, fixedLook = Vector3.zero, fixedUp = Vector3.forward;
  float turned = 0f;
  for (int i = -warm; i < N; i++) {
    float tt = i * dt;
    if (i == 0 && trig != null) anim.SetTrigger(trig);
    if (spinIn != 0f && i >= 0 && tt < len - 0.3f) go.transform.Rotate(0, spinIn * 90f / Mathf.Max(0.3f, len - 0.3f) * dt, 0);   // the AI turns the body ~90 deg over the clip
    if (turn && tt > 0.6f && turned < 180f) { float d = Mathf.Min(cfg.turnSpeedDegPerSec * dt, 180f - turned); turned += d; go.transform.Rotate(0, d, 0); }
    float mult = Mathf.Clamp(v / Mathf.Max(0.2f, cfg.EvaluateExpectedClipStrideSpeed(Mathf.Max(0.01f, v))), cfg.minAnimSpeedScale, cfg.maxAnimSpeedScale);
    anim.SetFloat("Speed", v); anim.SetFloat("LocomotionSpeedMult", v > 0.01f ? mult : 1f);
    Vector3 p = go.transform.position + go.transform.forward * v * dt; p.y = groundY(p); go.transform.position = p;
    if (i == -warm && v > 0f) { go.transform.position -= go.transform.forward * v * (warm * dt); p = go.transform.position; p.y = groundY(p); go.transform.position = p; }
    if (look) { float a = Mathf.Lerp(-150f, 150f, Mathf.Clamp01((tt + 0.2f) / (len * 0.9f))); Vector3 tgt = go.transform.position + Quaternion.Euler(0, a, 0) * go.transform.forward * D + Vector3.up * size * 0.8f; rig.SetLookTarget(tgt, 1f); }
    anim.Update(dt);
    tick.Invoke(rig, new object[] { dt });
    if (i < 0) continue;
    int every = Mathf.Max(1, N / 8);
    if (i % every != 0 || i / every >= 8) continue;
    int k = i / every;
    Vector3 root = go.transform.position; Vector3 fwd = go.transform.forward; Vector3 right = go.transform.right;
    float camH = Mathf.Max(0.8f, size * 0.55f);
    if (turn) {
      if (k == 0) { fixedLook = root + fwd * 0.5f * size + Vector3.up * size * 0.25f; fixedCamPos = fixedLook + (right * 0.8f + fwd * 0.6f).normalized * D * 1.5f + Vector3.up * size * 0.7f; fixedUp = fwd; }
      camS.transform.position = fixedCamPos; camS.transform.LookAt(fixedLook);
      camF.transform.position = fixedLook + Vector3.up * D * 2.0f; camF.transform.rotation = Quaternion.LookRotation(Vector3.down, fixedUp);   // top view: footprints
    } else {
      Vector3 c = root + Vector3.up * size * 0.5f;
      camS.transform.position = root + right * D + Vector3.up * camH; camS.transform.LookAt(c);
      camF.transform.position = root + fwd * D * 1.35f + Vector3.up * size * 0.75f; camF.transform.LookAt(c + Vector3.up * size * 0.15f);
    }
    int renders = warmed ? 2 : 5; warmed = true;
    Shot(camS, $"{sp}_{sc}_side_{k:00}.png", renders);
    Shot(camF, $"{sp}_{sc}_{(turn ? "top" : "front")}_{k:00}.png", renders);
  }
  float jaw = rig.CurrentJawOpenDegrees;
  sb.AppendLine($"{sp} {sc}: {N} frames, len {len:F2}s, jaw {jaw:F0} deg");
}
rt.Release(); Object.Destroy(camS.gameObject); Object.Destroy(camF.gameObject); Object.Destroy(go);
return sb.ToString();
