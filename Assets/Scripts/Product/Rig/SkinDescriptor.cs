using UnityEngine;

namespace BeMyArms.Product
{
    /// <summary>
    /// Describes a cosmetic skin: which half of the body it dresses, its stable id and the rig
    /// contract version it was authored against. Skins are pure presentation layers.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M5", "BeMyArms.M5", "M5SkinDescriptor")]
    public class SkinDescriptor : MonoBehaviour
    {
        public RigRole Role = RigRole.P1;
        public string SkinId = "";
        public string RigId = "default";
    }
}
