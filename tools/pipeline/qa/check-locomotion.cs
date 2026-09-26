var sb = new System.Text.StringBuilder();

// 1. controller structure
var ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Art/Characters/Quaternius/Controllers/M7_P1_Locomotion.controller");
sb.Append("layers=").Append(ctrl.layers.Length).Append('\n');
foreach (var layer in ctrl.layers)
    foreach (var st in layer.stateMachine.states)
        if (st.state.name == "Locomotion" && st.state.motion is UnityEditor.Animations.BlendTree bt)
        {
            sb.Append("blend ").Append(bt.blendType).Append(" x=").Append(bt.blendParameter).Append(" y=").Append(bt.blendParameterY).Append('\n');
            foreach (var ch in bt.children)
                sb.Append("  ").Append(ch.motion != null ? ch.motion.name : "NULL")
                  .Append(" @(").Append(ch.position.x.ToString("F2")).Append(',').Append(ch.position.y.ToString("F2")).Append(")\n");
        }
sb.Append("params=");
foreach (var p in ctrl.parameters) sb.Append(p.name).Append(' ');
sb.Append('\n');

// 2/3. clips + avatars
int bodyClips = 0, human = 0;
foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies/Q_P1_Body.fbx"))
    if (a is UnityEngine.AnimationClip c && !c.name.StartsWith("__preview__")) { bodyClips++; if (c.isHumanMotion) human++; }
sb.Append("bodyClips=").Append(bodyClips).Append(" humanClips=").Append(human).Append('\n');
int kaykit = 0, kaykitHuman = 0;
foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/KayKitCharacterAnimations/Rig_Medium_MovementAdvanced.fbx"))
    if (a is UnityEngine.AnimationClip c && !c.name.StartsWith("__preview__")) { kaykit++; if (c.isHumanMotion) kaykitHuman++; }
sb.Append("kaykitClips=").Append(kaykit).Append(" humanClips=").Append(kaykitHuman).Append('\n');

// 4. skin animator avatar
var skin = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Art/Characters/Quaternius/Prefabs/BMA_P1_Clay.prefab");
var anim = skin != null ? skin.GetComponentInChildren<UnityEngine.Animator>(true) : null;
sb.Append("skinAnimator=").Append(anim != null).Append(" ctrl=").Append(anim != null && anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "null")
  .Append(" avatar=").Append(anim != null && anim.avatar != null ? anim.avatar.name + " human=" + anim.avatar.isHuman : "null").Append('\n');
UnityEngine.Debug.Log("[checkloco]\n" + sb);
return sb.ToString();
