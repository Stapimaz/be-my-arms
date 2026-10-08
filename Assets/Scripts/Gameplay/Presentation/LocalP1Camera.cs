using UnityEngine;

namespace BeMyArms.Gameplay
{
    /// <summary>
    /// P1 third-person camera. Follows the decoupled LOOK yaw (head/camera), not BodyYaw, so P1 can
    /// look around while the body keeps facing elsewhere. Mouse Y pitches the camera only.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M1", "BeMyArms.M1", "M1P1Camera")]
    public class LocalP1Camera : MonoBehaviour
    {
        public Transform target;
        public P1LookController look;
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
            if (target == null || look == null) return;

            Quaternion rotation = Quaternion.Euler(_pitch, look.LookYaw, 0f);
            Vector3 position = target.position + Vector3.up * height - rotation * Vector3.forward * distance;
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
