var path = "Assets/Art/Weapons/Prefabs/BMA_Weapon_Rifle_Kenney.prefab";
var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
var inst = (UnityEngine.GameObject)UnityEngine.Object.Instantiate(go);
var sb = new System.Text.StringBuilder();
foreach (var r in inst.GetComponentsInChildren<UnityEngine.Renderer>(true))
{
    sb.Append(r.name).Append(" bounds=").Append(r.bounds.ToString("F3")).Append(" localScale=").Append(r.transform.lossyScale.ToString("F3")).Append('\n');
}
UnityEngine.Debug.Log("[rifle]\n" + sb);
UnityEngine.Object.DestroyImmediate(inst);
return "renderers=" + go.GetComponentsInChildren<UnityEngine.Renderer>(true).Length;
