var sb = new System.Text.StringBuilder();
var cam = UnityEngine.Camera.main;
sb.Append("cam=" + (cam != null ? cam.transform.position.ToString("F1") : "null") + " t=" + UnityEngine.Time.time.ToString("F1") + "\n");
foreach (var l in UnityEngine.Object.FindObjectsByType<UnityEngine.Light>(UnityEngine.FindObjectsInactive.Exclude))
{
    if (!l.enabled) continue;
    var hd = l.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
    float dim = hd != null ? hd.lightDimmer : 1f;
    float eff = l.intensity * dim;
    bool dir = l.type == UnityEngine.LightType.Directional;
    float d = cam != null ? UnityEngine.Vector3.Distance(l.transform.position, new UnityEngine.Vector3(-65f, 1.5f, 28.5f)) : 0f;
    if (dir || (d < 16f && eff > 900f))
        sb.Append(l.name + " " + l.type + " I=" + l.intensity.ToString("F0") + " dim=" + dim.ToString("F2") + " pos=" + l.transform.position.ToString("F1") + " sh=" + l.shadows + " layer=" + l.renderingLayerMask + "\n");
}
return sb.ToString();
