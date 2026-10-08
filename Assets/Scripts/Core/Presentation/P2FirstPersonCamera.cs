using UnityEngine;

namespace BeMyArms.Core
{
    /// <summary>
    /// P2 first-person camera. It rides the standardized shoulder/upper-chest anchor (which
    /// moves with the body) but its rotation is the WORLD-space aim, independent of body
    /// yaw. This is the visual half of Aim Model C.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M0", "BeMyArms.M0", "P2FirstPersonCamera")]
    public class P2FirstPersonCamera : MonoBehaviour
    {
        public Transform eye;
        public P2AimController aim;

        void LateUpdate()
        {
            if (eye == null || aim == null) return;
            transform.SetPositionAndRotation(eye.position, aim.AimRotation);
        }
    }
}
