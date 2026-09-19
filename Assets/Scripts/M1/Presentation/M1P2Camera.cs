using UnityEngine;

namespace BeMyArms.M1
{
    /// <summary>
    /// P2 first-person camera. Rides the standardized shoulder anchor but uses the M1 aim rig's
    /// world-space rotation, independent of body yaw (visual half of the Model C coupling).
    /// </summary>
    public class M1P2Camera : MonoBehaviour
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
