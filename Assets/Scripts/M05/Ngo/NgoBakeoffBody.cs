using BeMyArms.M0;
using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.M05.Ngo
{
    /// <summary>
    /// One combined input packet carrying both role domains. The bake-off measures the cost of
    /// binding two role-tagged input streams to one authoritative entity on each stack.
    /// </summary>
    public struct BakeoffInput : INetworkSerializable
    {
        public float MoveX;
        public float MoveZ;
        public float YawDelta;      // P1
        public float AimYawDelta;   // P2
        public float AimPitchDelta; // P2
        public bool Sprint;         // P1
        public bool Fire;           // P2

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MoveX);
            serializer.SerializeValue(ref MoveZ);
            serializer.SerializeValue(ref YawDelta);
            serializer.SerializeValue(ref AimYawDelta);
            serializer.SerializeValue(ref AimPitchDelta);
            serializer.SerializeValue(ref Sprint);
            serializer.SerializeValue(ref Fire);
        }
    }

    /// <summary>
    /// Server-authoritative shared body for the NGO candidate. P1 owns BodyYaw + movement;
    /// P2 owns the sector-clamped world aim and fire. NGO provides no prediction, so the client
    /// sends raw input and sees the server result (this absence is one of the bake-off findings).
    /// </summary>
    public class NgoBakeoffBody : NetworkBehaviour
    {
        public float MoveSpeed = 5f;
        public float SprintMultiplier = 1.5f;
        public float Gravity = -20f;
        public float SectorHalf = 70f;
        public float MaxPitch = 80f;
        public float FireRate = 8f;
        public float Damage = 18f;
        public float HeadshotMultiplier = 2.5f;
        public float Range = 150f;
        public float MaxHealth = 100f;
        public Transform Eye;

        public NetworkVariable<float> BodyYaw = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> AimYaw = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> AimPitch = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> Health = new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> Hits = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        CharacterController _controller;
        float _verticalVelocity;
        float _desiredAimYaw;
        float _nextFireTime;

        public override void OnNetworkSpawn()
        {
            _controller = GetComponent<CharacterController>();
            if (IsServer)
            {
                _desiredAimYaw = transform.eulerAngles.y;
                BodyYaw.Value = _desiredAimYaw;
                AimYaw.Value = _desiredAimYaw;
                Health.Value = MaxHealth;
            }
        }

        /// <summary>Client -> server input. Ownership is not required: the body belongs to the server.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void SubmitInputServerRpc(BakeoffInput input)
        {
            float dt = Time.deltaTime;

            // P1 domain: body yaw + locomotion.
            BodyYaw.Value = AimSector.NormalizeAngle(BodyYaw.Value + input.YawDelta);
            transform.rotation = Quaternion.Euler(0f, BodyYaw.Value, 0f);

            Vector3 move = new Vector3(input.MoveX, 0f, input.MoveZ);
            if (move.sqrMagnitude > 1f) move.Normalize();
            move = transform.rotation * move;
            float speed = MoveSpeed * (input.Sprint ? SprintMultiplier : 1f);
            if (_controller != null)
            {
                if (_controller.isGrounded) _verticalVelocity = -2f;
                else _verticalVelocity += Gravity * dt;
                _controller.Move((move * speed + Vector3.up * _verticalVelocity) * dt);
            }

            // P2 domain: sector-clamped world aim (Model C) + pitch.
            _desiredAimYaw = AimSector.ApplyInput(_desiredAimYaw, input.AimYawDelta, BodyYaw.Value, SectorHalf);
            AimYaw.Value = _desiredAimYaw;
            AimPitch.Value = Mathf.Clamp(AimPitch.Value + input.AimPitchDelta, -MaxPitch, MaxPitch);

            if (input.Fire) ServerFire();
        }

        void ServerFire()
        {
            if (Time.time < _nextFireTime) return;
            _nextFireTime = Time.time + 1f / Mathf.Max(0.01f, FireRate);

            Vector3 origin = Eye != null ? Eye.position : transform.position + Vector3.up * 1.45f;
            Vector3 direction = Quaternion.Euler(AimPitch.Value, AimYaw.Value, 0f) * Vector3.forward;

            RaycastHit[] hits = Physics.RaycastAll(origin, direction, Range, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<NetworkObject>() == NetworkObject) continue;

                HitboxRegion region = hit.collider.GetComponentInParent<HitboxRegion>();
                if (region == null || region.owner == null)
                {
                    Debug.Log("[M05-NGO] shot hit geometry (no damageable)");
                    return;
                }

                float amount = region.region == HitboxRegion.Region.Head ? Damage * HeadshotMultiplier : Damage;
                region.owner.ApplyDamage(amount, region.region);
                Hits.Value += 1;
                Debug.Log($"[M05-NGO] hit {region.region} for {amount:0} -> targetHealth={region.owner.Health:0}");
                return;
            }
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            style.normal.textColor = Color.white;
            GUI.Box(new Rect(8f, 8f, 430f, 84f), GUIContent.none);
            GUI.Label(new Rect(16f, 12f, 420f, 78f),
                $"[M05 NGO] role={(IsServer ? "server" : "client")}\n" +
                $"BodyYaw {BodyYaw.Value:0.0}  AimYaw {AimYaw.Value:0.0}  Pitch {AimPitch.Value:0.0}\n" +
                $"HP {Health.Value:0}  hits {Hits.Value}",
                style);
        }
    }
}
