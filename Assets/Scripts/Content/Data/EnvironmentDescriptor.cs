using UnityEngine;

namespace BeMyArms.Content
{
    /// <summary>Marks an environment-kit piece and its kit category. No gameplay behavior.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M6", "BeMyArms.M6", "M6EnvironmentDescriptor")]
    public class EnvironmentDescriptor : MonoBehaviour
    {
        public string AssetId = "";
        public string KitCategory = "blockout";
    }
}
