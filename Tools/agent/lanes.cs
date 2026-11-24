// Capsule-casts (r 0.3 m) each stroller lane of ParkVisitorCrowd and each standing spot (overlap) against the built scene.
var sb = new System.Text.StringBuilder();
float[][] lanes = { new[]{-56f,-45.1f,8f}, new[]{18f,-38.3f,-35f}, new[]{-58f,-37.6f,-92f} };
int mask = ~((1 << 2) | (1 << UnityEngine.LayerMask.NameToLayer("Player")));
foreach (var l in lanes)
{
    float x0 = l[0], z = l[1], x1 = l[2];
    UnityEngine.Vector3 a = new UnityEngine.Vector3(x0, 0f, z), b = new UnityEngine.Vector3(x1, 0f, z);
    float gy = 0f; UnityEngine.RaycastHit g;
    if (UnityEngine.Physics.Raycast(a + UnityEngine.Vector3.up * 5f, UnityEngine.Vector3.down, out g, 10f, mask, UnityEngine.QueryTriggerInteraction.Ignore)) gy = g.point.y;
    UnityEngine.Vector3 p1 = new UnityEngine.Vector3(x0, gy + 0.45f, z), p2 = new UnityEngine.Vector3(x0, gy + 1.5f, z);
    UnityEngine.Vector3 dir = (b - a).normalized; float len = UnityEngine.Mathf.Abs(x1 - x0);
    var hits = UnityEngine.Physics.CapsuleCastAll(p1, p2, 0.3f, dir, len, mask, UnityEngine.QueryTriggerInteraction.Ignore);
    sb.Append($"lane z{z} {x0}->{x1} gy{gy:F2}: {hits.Length} hits");
    foreach (var h in hits) sb.Append($" [{h.collider.name} @x{h.point.x:F1} y{h.point.y:F1}]");
    // also a downward ray every 2 m for steps/props without colliders
    sb.Append("\n");
}
return sb.ToString();
