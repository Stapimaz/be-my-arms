using UnityEngine;

namespace BeMyArms.Gameplay
{
    /// <summary>
    /// P2 first-person camera. Rides the standardized shoulder anchor but uses the Gameplay aim rig's
    /// world-space rotation, independent of body yaw (visual half of the Model C coupling).
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M1", "BeMyArms.M1", "M1P2Camera")]
    public class LocalP2Camera : MonoBehaviour
    {
        public Transform eye;
        public P2AimRig aim;

        void LateUpdate()
        {
            if (eye == null || aim == null) return;
            transform.SetPositionAndRotation(eye.position, aim.AimRotation);
        }
    }
}
