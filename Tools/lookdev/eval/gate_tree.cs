var root = UnityEngine.GameObject.Find("Door_e0_gate_leaves");
if (root == null) return "no root";
var sb = new System.Text.StringBuilder();
var stack = new System.Collections.Generic.Stack<(UnityEngine.Transform, int)>();
stack.Push((root.transform, 0));
int n = 0;
while (stack.Count > 0 && n < 60)
{
    var (t, d) = stack.Pop(); n++;
    var r = t.GetComponent<UnityEngine.Renderer>();
    sb.Append(new string(' ', d * 2) + t.name + " lp=" + t.localPosition.ToString("F2") + " ls=" + t.localScale.ToString("F2") + (r != null ? " mat=" + (r.sharedMaterial != null ? r.sharedMaterial.name : "null") + " b=" + r.bounds.size.ToString("F2") + " c=" + r.bounds.center.ToString("F2") : "") + "\n");
    if (d < 3) for (int i = t.childCount - 1; i >= 0; i--) stack.Push((t.GetChild(i), d + 1));
}
return sb.ToString();
