var p = "Assets/Art/Characters/Quaternius/Prefabs/BMA_P1_Clay.prefab";
var go = (UnityEngine.GameObject)UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(p));
UnityEngine.Transform F(string n) { UnityEngine.Transform[] all = go.GetComponentsInChildren<UnityEngine.Transform>(true); foreach (var t in all) if (t.name == n) return t; return null; }
var hips = F("DEF-hips"); var toeL = F("DEF-toe.L"); var head = F("DEF-head"); var handL = F("DEF-hand.L"); var footL = F("DEF-foot.L");
UnityEngine.Debug.Log("[face] hips=" + hips.position.ToString("F3") + " head=" + head.position.ToString("F3") + " toeL=" + toeL.position.ToString("F3") + " footL=" + footL.position.ToString("F3") + " handL=" + handL.position.ToString("F3"));
var smr = go.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
UnityEngine.Debug.Log("[face] bounds=" + smr.bounds.ToString("F3"));
UnityEngine.Object.DestroyImmediate(go);
return "face-check";
