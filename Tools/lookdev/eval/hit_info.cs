var cam = UnityEngine.Camera.main.transform;
UnityEngine.RaycastHit h;
if (!UnityEngine.Physics.Raycast(cam.position, cam.forward, out h, 30f, Cretacea.Core.GameLayers.EnvironmentAndDoorMask, UnityEngine.QueryTriggerInteraction.Ignore)) return "no hit";
var c = h.collider;
var sb = new System.Text.StringBuilder();
sb.Append("collider=" + c.name + " (" + c.GetType().Name + ") dist=" + h.distance.ToString("F2"));
sb.Append(" ownRenderer=" + (c.GetComponent<UnityEngine.Renderer>() != null));
sb.Append(" childRenderer=" + (c.GetComponentInChildren<UnityEngine.Renderer>() != null));
var p = c.transform.parent;
sb.Append(" parent=" + (p != null ? p.name : "-"));
if (p != null) { var pr = p.GetComponentsInChildren<UnityEngine.Renderer>(); sb.Append(" parentRenderers=" + pr.Length); for (int i = 0; i < pr.Length && i < 6; i++) sb.Append(" [" + pr[i].name + ":" + (pr[i].sharedMaterial != null ? pr[i].sharedMaterial.name : "null") + "]"); }
var own = c.GetComponent<UnityEngine.Renderer>();
if (own != null && own.sharedMaterial != null) { var m = own.sharedMaterial; sb.Append(" mat=" + m.name + " shader=" + m.shader.name + " base=" + (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "-") + " tex=" + (m.HasProperty("_BaseColorMap") && m.GetTexture("_BaseColorMap") != null ? m.GetTexture("_BaseColorMap").name : "none")); }
return sb.ToString();
