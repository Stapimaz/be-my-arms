using BeMyArms.M2;
using BeMyArms.M3;
using UnityEngine;

namespace BeMyArms.M7
{
    /// <summary>
    /// Presentation-only animation for the segment-rig characters: readable P1 locomotion (leg swing,
    /// hip bob, decoupled head look) and P2 aim (shoulder layer + weapon follow the authoritative aim,
    /// so the two-hand rest grip is maintained). It only reads simulation state; it never writes
    /// simulation, aim, transforms used by hit detection, or the contract anchors. A skinned-mesh
    /// animation pass can replace this later behind the same M5 rig contract.
    /// </summary>
    public class M7CharacterAnimator : MonoBehaviour
    {
        public M3DuelBody Body;
        public Transform P1Skin;
        public Transform P2Skin;
        public Transform Weapon;

        Transform _hips, _head;
        Transform _thighL, _thighR, _shinL, _shinR, _footL, _footR;
        Transform _p2Root;

        Quaternion _hipsRest = Quaternion.identity, _headRest = Quaternion.identity;
        Vector3 _hipsRestPosition;
        bool _hasHipsRest;
        Quaternion _thighRestL = Quaternion.identity, _thighRestR = Quaternion.identity;
        Quaternion _shinRestL = Quaternion.identity, _shinRestR = Quaternion.identity;
        Quaternion _footRestL = Quaternion.identity, _footRestR = Quaternion.identity;
        Quaternion _p2Rest = Quaternion.identity;
        Quaternion _weaponRest = Quaternion.identity;

        // The character's world up/forward expressed in the parent frame of the head/hips bones.
        // The P1 FBX imports its root rotated (270 deg X), so a naive local-Y "look" rotation would
        // roll the head instead of yawing it; these axes make the procedural layer skin-agnostic.
        Vector3 _headYawAxis = Vector3.up;
        Vector3 _hipsRollAxis = Vector3.forward;
        Vector3 _hipsUpLocal = Vector3.up;
        float _hipsBobScale = 1f;

        Vector3 _lastPosition;
        bool _hasLast;
        float _phase;
        float _recoil;
        int _lastAmmo = -1;
        float _nextMuzzle;

        void Start()
        {
            _hips = Find(P1Skin, "Hips");
            _head = Find(P1Skin, "Head");
            _thighL = Find(P1Skin, "Thigh_L"); _thighR = Find(P1Skin, "Thigh_R");
            _shinL = Find(P1Skin, "Shin_L"); _shinR = Find(P1Skin, "Shin_R");
            _footL = Find(P1Skin, "Foot_L"); _footR = Find(P1Skin, "Foot_R");
            _p2Root = Find(P2Skin, "P2Root");

            if (_hips != null) { _hipsRest = _hips.localRotation; _hipsRestPosition = _hips.localPosition; _hasHipsRest = true; if (_hips.parent != null) { _hipsRollAxis = _hips.parent.InverseTransformDirection(Vector3.forward); _hipsUpLocal = _hips.parent.InverseTransformDirection(Vector3.up).normalized; _hipsBobScale = _hips.parent.lossyScale.y; } }
            if (_head != null) { _headRest = _head.localRotation; if (_head.parent != null) _headYawAxis = _head.parent.InverseTransformDirection(Vector3.up); }
            if (_thighL != null) _thighRestL = _thighL.localRotation;
            if (_thighR != null) _thighRestR = _thighR.localRotation;
            if (_shinL != null) _shinRestL = _shinL.localRotation;
            if (_shinR != null) _shinRestR = _shinR.localRotation;
            if (_footL != null) _footRestL = _footL.localRotation;
            if (_footR != null) _footRestR = _footR.localRotation;
            if (_p2Root != null) _p2Rest = _p2Root.localRotation;
            if (Weapon != null) _weaponRest = Weapon.localRotation;

            if (Body != null) _lastPosition = new Vector3(Body.State.Value.PosX, 0f, Body.State.Value.PosZ);
        }

        void LateUpdate()
        {
            if (Body == null) return;
            M2BodyState state = Body.State.Value;
            Vector3 position = new Vector3(state.PosX, 0f, state.PosZ);

            float speed = 0f;
            if (_hasLast && Time.deltaTime > 0.0001f)
                speed = Vector3.Distance(position, _lastPosition) / Time.deltaTime;
            _lastPosition = position;
            _hasLast = true;

            float speed01 = Mathf.Clamp01(speed / 5f);
            AnimateLocomotion(speed01, state);
            AnimateAim(state);
            HandleCombat();
        }

        void AnimateLocomotion(float speed01, M2BodyState state)
        {
            _phase += Time.deltaTime * (4f + speed01 * 6f);
            float amp = 24f * speed01;
            float swingL = Mathf.Sin(_phase) * amp;
            float swingR = Mathf.Sin(_phase + Mathf.PI) * amp;

            SetX(_thighL, _thighRestL, swingL);
            SetX(_thighR, _thighRestR, swingR);
            SetX(_shinL, _shinRestL, -Mathf.Abs(Mathf.Sin(_phase)) * 30f * speed01 - 4f);
            SetX(_shinR, _shinRestR, -Mathf.Abs(Mathf.Sin(_phase + Mathf.PI)) * 30f * speed01 - 4f);
            SetX(_footL, _footRestL, Mathf.Abs(Mathf.Sin(_phase)) * 12f * speed01);
            SetX(_footR, _footRestR, Mathf.Abs(Mathf.Sin(_phase + Mathf.PI)) * 12f * speed01);

            if (_hips != null)
            {
                _hips.localRotation = Quaternion.AngleAxis(Mathf.Sin(_phase) * 4f * speed01, _hipsRollAxis) * _hipsRest;
                if (_hasHipsRest)
                {
                    // Bob is authored in world metres; convert to the skin's local units, which may
                    // be scaled (the P1 FBX root imports at x100).
                    float bob = Mathf.Abs(Mathf.Sin(_phase * 2f)) * 0.03f * speed01 / Mathf.Max(0.0001f, _hipsBobScale);
                    _hips.localPosition = _hipsRestPosition + _hipsUpLocal * bob;
                }
            }

            // Decoupled head look within the neck limit, yawed about the character's up axis.
            float lookOffset = M2BodySim.Normalize(state.LookYaw - state.BodyYaw);
            if (_head != null) _head.localRotation = Quaternion.AngleAxis(lookOffset, _headYawAxis) * _headRest;
        }

        void AnimateAim(M2BodyState state)
        {
            float aimLocalYaw = M2BodySim.Normalize(state.AimYaw - state.BodyYaw);
            float pitch = Mathf.Clamp(state.AimPitch, -80f, 80f);
            float kick = -_recoil * 6f;
            _recoil = Mathf.Max(0f, _recoil - Time.deltaTime * 6f);

            if (_p2Root != null) _p2Root.localRotation = _p2Rest * Quaternion.Euler(pitch, aimLocalYaw, 0f);
            if (Weapon != null) Weapon.localRotation = _weaponRest * Quaternion.Euler(pitch + kick, aimLocalYaw, 0f);
        }

        void HandleCombat()
        {
            if (Body == null) return;
            int ammo = Body.Ammo.Value;
            if (_lastAmmo < 0) { _lastAmmo = ammo; return; }

            // The shot is authoritative; presentation reacts to the magazine dropping.
            if (ammo < _lastAmmo && !Body.Reloading.Value)
            {
                _recoil = Mathf.Min(1f, _recoil + 0.5f);
                if (Time.time >= _nextMuzzle && M7VfxService.Instance != null && Weapon != null)
                {
                    _nextMuzzle = Time.time + 0.05f;
                    M7VfxService.Instance.Spawn(M7VfxId.MuzzleFlash, Weapon.position + Weapon.forward * 0.5f, Weapon.rotation);
                }
            }
            _lastAmmo = ammo;
        }

        static void SetX(Transform t, Quaternion rest, float degrees)
        {
            if (t != null) t.localRotation = rest * Quaternion.Euler(degrees, 0f, 0f);
        }

        static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }
    }
}
