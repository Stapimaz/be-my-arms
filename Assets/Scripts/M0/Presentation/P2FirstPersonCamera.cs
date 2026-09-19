using UnityEngine;

namespace BeMyArms.M0
{
    /// <summary>
    /// P2 first-person camera. It rides the standardized shoulder/upper-chest anchor (which
    /// moves with the body) but its rotation is the WORLD-space aim, independent of body
    /// yaw. This is the visual half of Aim Model C.
    /// </summary>
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
