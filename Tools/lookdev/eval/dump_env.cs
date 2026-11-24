using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
var sb = new StringBuilder();
var ci = System.Globalization.CultureInfo.InvariantCulture;
// Volumes
foreach (var v in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
{
    var prof = v.sharedProfile;
    sb.AppendLine("VOLUME " + v.name + " global=" + v.isGlobal + " prio=" + v.priority + " weight=" + v.weight + " active=" + v.gameObject.activeInHierarchy + " profile=" + (prof != null ? UnityEditor.AssetDatabase.GetAssetPath(prof) : "null") + " pos=" + v.transform.position);
    if (prof == null) continue;
    foreach (var c in prof.components)
    {
        if (c == null) continue;
        sb.Append("   - " + c.GetType().Name + " active=" + c.active + " :");
        foreach (var p in c.parameters.Zip(c.GetType().GetFields().Where(f => typeof(VolumeParameter).IsAssignableFrom(f.FieldType)), (par, f) => (par, f)))
        {
            if (p.par.overrideState) { object val = null; try { val = p.par.GetType().GetProperty("value").GetValue(p.par); } catch { } sb.Append(" " + p.f.Name + "=" + (val is float fl ? fl.ToString("G5", ci) : (val != null ? val.ToString() : "null")) + ";"); }
        }
        sb.AppendLine();
    }
}
// default profile
var def = GraphicsSettings.GetRenderPipelineSettings<HDRPDefaultVolumeProfileSettings>();
if (def != null && def.volumeProfile != null)
{
    sb.AppendLine("DEFAULT PROFILE " + UnityEditor.AssetDatabase.GetAssetPath(def.volumeProfile));
    foreach (var c in def.volumeProfile.components)
    {
        if (c == null) continue;
        sb.Append("   - " + c.GetType().Name + " active=" + c.active + " :");
        foreach (var p in c.parameters.Zip(c.GetType().GetFields().Where(f => typeof(VolumeParameter).IsAssignableFrom(f.FieldType)), (par, f) => (par, f)))
        {
            if (p.par.overrideState) { object val = null; try { val = p.par.GetType().GetProperty("value").GetValue(p.par); } catch { } sb.Append(" " + p.f.Name + "=" + (val is float fl ? fl.ToString("G5", ci) : (val != null ? val.ToString() : "null")) + ";"); }
        }
        sb.AppendLine();
    }
}
// HDRP asset
var rp = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
sb.AppendLine("HDRP ASSET " + UnityEditor.AssetDatabase.GetAssetPath(rp) + " quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] + " colorSpace=" + QualitySettings.activeColorSpace);
var s = rp.currentPlatformRenderPipelineSettings;
sb.AppendLine("  litShaderMode=" + s.supportedLitShaderMode + " colorBuffer=" + s.colorBufferFormat + " SSR=" + s.supportSSR + " SSRtransp=" + s.supportSSRTransparent + " SSAO=" + s.supportSSAO + " SSGI=" + s.supportSSGI + " SSS=" + s.supportSubsurfaceScattering + " volum=" + s.supportVolumetrics + " volClouds=" + s.supportVolumetricClouds + " water=" + s.supportWater + " decals=" + s.supportDecals + " RT=" + s.supportRayTracing + " msaa=" + s.msaaSampleCount + " dynres=" + s.dynamicResolutionSettings.enabled + " terrainHoles=" + s.supportTerrainHole + " transpBackface=" + s.supportTransparentBackface + " distortion=" + s.supportDistortion + " customPass=" + s.supportCustomPass + " lightLayers=" + s.supportLightLayers + " shadowMask=" + s.supportShadowMask + " screenSpaceShadows=" + s.hdShadowInitParams.supportScreenSpaceShadows + " dirShadowRes=" + s.hdShadowInitParams.directionalShadowsDepthBits + " maxShadowReq=" + s.hdShadowInitParams.maxShadowRequests + " punctAtlas=" + s.hdShadowInitParams.punctualLightShadowAtlas.shadowAtlasResolution + " maxPunctOnScreen=" + s.lightLoopSettings.maxPunctualLightsOnScreen + " maxDecals=" + s.lightLoopSettings.maxDecalsOnScreen + " maxLightsPerCluster=" + s.lightLoopSettings.maxLightsPerClusterCell);
// Terrain
foreach (var t in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
{
    var td = t.terrainData;
    sb.AppendLine("TERRAIN " + t.name + " pos=" + t.transform.position + " size=" + td.size + " hmRes=" + td.heightmapResolution + " alphaRes=" + td.alphamapResolution + " layers=" + td.terrainLayers.Length + " trees=" + td.treeInstanceCount + " treeProtos=" + td.treePrototypes.Length + " detailProtos=" + td.detailPrototypes.Length + " detailRes=" + td.detailResolution + " mat=" + (t.materialTemplate != null ? t.materialTemplate.name + "/" + t.materialTemplate.shader.name : "null") + " drawInstanced=" + t.drawInstanced + " pixErr=" + t.heightmapPixelError + " baseMapDist=" + t.basemapDistance);
    for (int i = 0; i < td.terrainLayers.Length; i++) { var L = td.terrainLayers[i]; sb.AppendLine("   layer" + i + " " + L.name + " diffuse=" + (L.diffuseTexture != null ? L.diffuseTexture.name : "null") + " nrm=" + (L.normalMapTexture != null) + " mask=" + (L.maskMapTexture != null) + " tile=" + L.tileSize + " tint/spec=" + L.diffuseRemapMax + " sm=" + L.smoothness); }
    var am = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);
    var sums = new double[td.alphamapLayers];
    for (int y = 0; y < td.alphamapHeight; y += 2) for (int x = 0; x < td.alphamapWidth; x += 2) for (int l = 0; l < td.alphamapLayers; l++) sums[l] += am[y, x, l];
    double tot = sums.Sum();
    sb.AppendLine("   splat shares: " + string.Join(", ", sums.Select((v, i) => td.terrainLayers[i].name + "=" + (100.0 * v / tot).ToString("F1", ci) + "%")));
}
// scene roots & counts
foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
{
    sb.AppendLine("ROOT " + root.name + " children=" + root.transform.childCount + " totalTransforms=" + root.GetComponentsInChildren<Transform>(true).Length);
    if (root.transform.childCount < 30) foreach (Transform c in root.transform) sb.AppendLine("    " + c.name + " (" + c.GetComponentsInChildren<Transform>(true).Length + ")");
}
sb.AppendLine("reflectionProbes=" + UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include).Length + " planarProbes=" + UnityEngine.Object.FindObjectsByType<PlanarReflectionProbe>(FindObjectsInactive.Include).Length + " decalProjectors=" + UnityEngine.Object.FindObjectsByType<DecalProjector>(FindObjectsInactive.Include).Length + " particleSystems=" + UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include).Length + " lodGroups=" + UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Include).Length + " cameras=" + UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include).Length);
sb.AppendLine("ambientMode=" + RenderSettings.ambientMode + " lightmapCount=" + LightmapSettings.lightmaps.Length);
System.IO.File.WriteAllText("/private/tmp/claude-501/-Users-jc-able--claude-spares-spare-jxQboM/9d5b6ec1-be2e-4d6b-8f6a-8a96d2ed0c5e/scratchpad/unity/dump/env.txt", sb.ToString());
return "ok";
