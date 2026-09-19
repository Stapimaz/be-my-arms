using UnityEngine;

namespace BeMyArms.M0
{
    /// <summary>
    /// P1-owned body: BodyYaw plus simple kinematic locomotion.
    /// P1 is the only source of BodyYaw. Nothing here ever reads P2 input, which is what
    /// guarantees the body never chases P2's aim.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SharedBodyController : MonoBehaviour
    {
        public float MoveSpeed = 5f;
        public float SprintMultiplier = 1.5f;
        public float Gravity = -20f;

        public float BodyYaw { get; private set; }

        CharacterController _controller;
        float _verticalVelocity;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            BodyYaw = transform.eulerAngles.y;
        }

        public void Step(in P1Command cmd, float deltaTime)
        {
            // BodyYaw is P1-only. This is the single place body orientation changes in M0.
            BodyYaw = AimSector.NormalizeAngle(BodyYaw + cmd.LookYawDelta);
            transform.rotation = Quaternion.Euler(0f, BodyYaw, 0f);

            Vector3 move = new Vector3(cmd.Move.x, 0f, cmd.Move.y);
            if (move.sqrMagnitude > 1f) move.Normalize();
            move = transform.rotation * move;

            float speed = MoveSpeed * (cmd.Sprint ? SprintMultiplier : 1f);

            if (_controller.isGrounded) _verticalVelocity = -2f;
            else _verticalVelocity += Gravity * deltaTime;

            Vector3 velocity = move * speed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * deltaTime);
        }
    }
}
