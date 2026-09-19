using BeMyArms.M0;
using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.M05.Ngo
{
    /// <summary>
    /// Client-side input for the bake-off. Sends both role domains to the server at 60 Hz.
    /// Auto-drive synthesizes P1 movement/rotation and P2 aim/fire so the loop can be verified
    /// without a human (and in a headless server + editor client run).
    /// </summary>
    public class NgoBakeoffInputDriver : MonoBehaviour
    {
        public NgoBakeoffBody Body;
        public bool AutoDrive = true;
        public Transform AutoAimTarget;
        public bool FireWhenAligned = true;

        P1DeviceInputSource _p1;
        P2GamepadInputSource _p2;
        float _nextSendTime;

        void Awake()
        {
            if (Body == null) Body = GetComponent<NgoBakeoffBody>();
            _p1 = GetComponent<P1DeviceInputSource>();
            _p2 = GetComponent<P2GamepadInputSource>();
        }

        void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsClient || Body == null) return;
            if (Time.time < _nextSendTime) return;
            _nextSendTime = Time.time + 1f / 60f;

            BakeoffInput input = AutoDrive ? BuildAuto() : BuildDevice();
            Body.SubmitInputServerRpc(input);
        }

        BakeoffInput BuildDevice()
        {
            var input = default(BakeoffInput);
            if (_p1 != null)
            {
                P1Command c1 = _p1.Read(Time.deltaTime);
                input.MoveX = c1.Move.x;
                input.MoveZ = c1.Move.y;
                input.YawDelta = c1.LookYawDelta;
                input.Sprint = c1.Sprint;
            }
            if (_p2 != null)
            {
                P2Command c2 = _p2.Read(Time.deltaTime);
                input.AimYawDelta = c2.YawDelta;
                input.AimPitchDelta = c2.PitchDelta;
                input.Fire = c2.Fire;
            }
            return input;
        }

        BakeoffInput BuildAuto()
        {
            var input = default(BakeoffInput);

            // P1: advance and rotate the body so the target moves in and out of the sector.
            input.MoveZ = 1f;
            input.YawDelta = 25f * Time.deltaTime;

            // P2: track the target, fire when aligned.
            if (AutoAimTarget != null)
            {
                Vector3 origin = Body != null && Body.Eye != null ? Body.Eye.position : transform.position;
                Vector3 to = AutoAimTarget.position - origin;
                var flat = new Vector3(to.x, 0f, to.z);
                if (flat.sqrMagnitude > 0.0001f)
                {
                    float targetYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    float targetPitch = -Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg;
                    float error = AimSector.NormalizeAngle(targetYaw - Body.AimYaw.Value);
                    input.AimYawDelta = error;
                    input.AimPitchDelta = targetPitch - Body.AimPitch.Value;
                    input.Fire = FireWhenAligned && Mathf.Abs(error) <= 2.5f;
                }
            }

            return input;
        }
    }
}
