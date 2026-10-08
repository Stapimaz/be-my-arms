using UnityEngine;

namespace BeMyArms.Core
{
    /// <summary>
    /// P1 third-person camera. Core assumption: it follows BodyYaw exactly, so there is no
    /// free-look. Pitch is camera-only and does not change BodyYaw.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M0", "BeMyArms.M0", "P1ThirdPersonCamera")]
    public class P1ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 4.5f;
        public float height = 1.6f;
        public float minPitch = -35f;
        public float maxPitch = 65f;

        float _pitch = 12f;

        public void AddPitch(float deltaDegrees)
        {
            _pitch = Mathf.Clamp(_pitch + deltaDegrees, minPitch, maxPitch);
        }

        void LateUpdate()
        {
            if (target == null) return;

            float bodyYaw = target.eulerAngles.y;
            Quaternion rotation = Quaternion.Euler(_pitch, bodyYaw, 0f);
            Vector3 position = target.position + Vector3.up * height - rotation * Vector3.forward * distance;
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
