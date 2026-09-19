using UnityEngine;

namespace BeMyArms.M5
{
    /// <summary>
    /// Describes a cosmetic skin: which half of the body it dresses, its stable id and the rig
    /// contract version it was authored against. Skins are pure presentation layers.
    /// </summary>
    public class M5SkinDescriptor : MonoBehaviour
    {
        public M5RigRole Role = M5RigRole.P1;
        public string SkinId = "";
        public string RigId = "default";
    }
}
