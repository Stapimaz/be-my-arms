using UnityEngine;

namespace BeMyArms.Client
{
    /// <summary>Scene link so runtime match setup can find the map record for this arena.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M7", "BeMyArms.M7", "M7MapSceneLink")]
    public class MapSceneLink : MonoBehaviour
    {
        public MapDefinition Map;
    }
}
