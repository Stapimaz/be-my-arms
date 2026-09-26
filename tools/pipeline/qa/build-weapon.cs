var prefab = BeMyArms.M7.EditorTools.M7WeaponBuilder.Build();
var sb = new System.Text.StringBuilder();
if (prefab == null) sb.Append("null\n");
else
{
    var rends = prefab.GetComponentsInChildren<UnityEngine.Renderer>(true);
    var b = rends[0].bounds;
    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
    sb.Append("renderers=").Append(rends.Length).Append(" worldSize=").Append(b.size.ToString("F3")).Append('\n');
    void Find(UnityEngine.Transform t, string n)
    {
        foreach (var c in t.GetComponentsInChildren<UnityEngine.Transform>(true))
            if (c.name == n) sb.Append(n).Append(" local=").Append(c.localPosition.ToString("F3")).Append(" world=").Append(c.position.ToString("F3")).Append('\n');
    }
    Find(prefab.transform, "Grip_R");
    Find(prefab.transform, "Grip_L");
    Find(prefab.transform, "Muzzle");
    var model = prefab.transform.Find("Model");
    if (model != null) sb.Append("Model rot=").Append(model.localRotation.eulerAngles.ToString("F0")).Append(" scale=").Append(model.localScale.ToString("F4")).Append(" pos=").Append(model.localPosition.ToString("F3")).Append('\n');
}
UnityEngine.Debug.Log("[weaponbuild]\n" + sb);
return sb.ToString();
