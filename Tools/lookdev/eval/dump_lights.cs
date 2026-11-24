using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
var sb = new StringBuilder();
var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
sb.AppendLine("count=" + lights.Length);
foreach (var l in lights.OrderBy(x => x.transform.position.x))
{
    var hd = l.GetComponent<HDAdditionalLightData>();
    string path = l.name; var t = l.transform.parent; int d = 0;
    while (t != null && d < 3) { path = t.name + "/" + path; t = t.parent; d++; }
    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
        "{0}|{1}|en={2}|act={3}|I={4:G5} {5}|col=({6:F2},{7:F2},{8:F2})|useT={9}|T={10:F0}|range={11:F1}|spot={12:F0}/{13:F0}|shadows={14}|vol={15:F2}|volEn={16}|pos=({17:F1},{18:F1},{19:F1})|cookie={20}|aff={21}",
        path, l.type, l.enabled, l.gameObject.activeInHierarchy, l.intensity, l.lightUnit, l.color.r, l.color.g, l.color.b, l.useColorTemperature, l.colorTemperature, l.range, l.spotAngle, l.innerSpotAngle, l.shadows,
        hd != null ? hd.volumetricDimmer : -1f, hd != null && hd.affectsVolumetric, l.transform.position.x, l.transform.position.y, l.transform.position.z, l.cookie != null ? l.cookie.name : "-", hd != null ? (hd.affectDiffuse ? "D" : "") + (hd.affectSpecular ? "S" : "") : "?"));
}
System.IO.File.WriteAllText("/private/tmp/claude-501/-Users-jc-able--claude-spares-spare-jxQboM/9d5b6ec1-be2e-4d6b-8f6a-8a96d2ed0c5e/scratchpad/unity/dump/lights.txt", sb.ToString());
return "lights=" + lights.Length;
