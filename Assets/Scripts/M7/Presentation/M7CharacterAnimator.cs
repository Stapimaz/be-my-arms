using BeMyArms.M2;
using BeMyArms.M3;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace BeMyArms.M7
{
    /// <summary>
    /// Presentation-only Mecanim driver for the Quaternius-derived shared body.
    ///
    /// Native baked cardinal locomotion blended through diagonals, with rifle posing after animation.
    ///
    /// It only reads simulation/replicated state and never writes simulation, hitboxes, aim or the
    /// contract anchors.
    /// </summary>
    public class M7CharacterAnimator : MonoBehaviour
    {
        public M3DuelBody Body;
        public M3DuelClient Client;
        public Transform P1Skin;
        public Transform P2Skin;
        public Animator P1Animator;
        public Animator P2Animator;
        public Transform AimPivot;
        public Transform Weapon;
        public Transform Muzzle;
        public TwoBoneIKConstraint ArmIkL;
        public TwoBoneIKConstraint ArmIkR;

        [Header("Stance")]
        public float StandingChestLocalY = -.165f;
        public float CrouchBlendSpeed = 10f;

        /// <summary>Diagnostic: the current crouch blend 0..1.</summary>
        public float CrouchWeight => _crouch01;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        static readonly int AliveId = Animator.StringToHash("Alive");
        static readonly int ShootId = Animator.StringToHash("Shoot");
        static readonly int ReloadId = Animator.StringToHash("Reload");

        Vector3 _aimPivotBase;
        float _crouch01;
        float _recoil;
        int _lastAmmo = -1;
        float _nextMuzzle;
        bool _weaponVisible = true;
        bool _wasGrounded;
        byte _lastAction;
        uint _epoch = uint.MaxValue;
        float _stepDistance;
        Transform _kickThigh, _kickShin;
        Transform _upperChest;
        Transform _head;
        Quaternion _headBasis;

        void Start()
        {
            if (Body == null) Body = GetComponent<M3DuelBody>();
            if (Client == null) Client = GetComponent<M3DuelClient>();
            if (Application.isBatchMode)
            {
                if (P1Animator != null) P1Animator.enabled = false;
                if (P2Animator != null) P2Animator.enabled = false;
                return;
            }
            _lastAmmo = Body != null ? Body.Ammo.Value : -1;
            if (AimPivot != null) _aimPivotBase = AimPivot.localPosition;
            if (P1Animator != null)
            {
                _kickThigh = M7RiflePose.Find(P1Animator.transform, "DEF-thigh.R");
                _kickShin = M7RiflePose.Find(P1Animator.transform, "DEF-shin.R");
                _head = M7RiflePose.Find(P1Animator.transform, "Head");
                if (_head != null) _headBasis = Quaternion.Inverse(P1Animator.transform.rotation) * _head.rotation;
            }
        }

        void Update()
        {
            if (Body == null || !Body.IsSpawned || Application.isBatchMode) return;
            M2BodyState state = Client != null ? Client.ViewState : Body.State.Value;
            bool alive = Body.Alive.Value && state.Health > 0;
            if (P1Animator != null)
            {
                float yaw = state.MovementState==(byte)M2MovementState.Slide
                    ? Mathf.Atan2(state.ActionDirX,state.ActionDirZ)*Mathf.Rad2Deg-state.BodyYaw : 0f;
                // Only the cosmetic skeleton faces a fixed action trajectory. Root motion and
                // this transform have no authority over the network body or firing sector.
                P1Animator.transform.localRotation=Quaternion.Euler(0,yaw,0);
            }
            if (_epoch != state.ControlEpoch)
            {
                _epoch=state.ControlEpoch; _lastAmmo=Body.Ammo.Value; _recoil=_crouch01=_stepDistance=0;
                _wasGrounded=state.Grounded; _lastAction=state.MovementState;
                if(P1Animator!=null) P1Animator.Rebind();
                if(P2Animator!=null && P2Animator!=P1Animator) P2Animator.Rebind();
            }
            ApplyAnimator(P1Animator,state,alive);
            if(P2Animator!=P1Animator) ApplyAnimator(P2Animator,state,alive);
            UpdateMovementFeedback(state,alive,Time.deltaTime);
        }

        void LateUpdate()
        {
            if (Body == null || !Body.IsSpawned || Application.isBatchMode) return;

            M2BodyState state = Client != null ? Client.ViewState : Body.State.Value;
            float dt = Mathf.Max(1e-4f, Time.deltaTime);

            bool alive = Body.Alive.Value && state.Health > 0;
            UpdateStance(state, alive, dt);
            UpdateAim(state, alive);
            if (alive) SetHeadLook(state.LookYaw,state.LookPitch);
            UpdateWeaponVisibility(alive);
            UpdateCombat(dt);
            if (alive && (state.MovementState == (byte)M2MovementState.KickLight || state.MovementState == (byte)M2MovementState.KickHeavy))
            {
                bool heavy=state.MovementState==(byte)M2MovementState.KickHeavy;
                ApplyKickPose(Mathf.Clamp01(1-state.ActionTimeLeft/(heavy ? .7f : .35f)),heavy);
            }
        }

        /// <summary>P1's desired look, bounded relative to the shared chest so opposing P2 aim
        /// cannot twist the neck through the back. Pure presentation; the camera still owns look.</summary>
        public void SetHeadLook(float worldYaw,float pitch)
        {
            if(P1Animator==null)return;
            if(_head==null)
            {
                _head=M7RiflePose.Find(P1Animator.transform,"Head");
                if(_head==null)return;
                _headBasis=Quaternion.Inverse(P1Animator.transform.rotation)*_head.rotation;
            }
            if(_upperChest==null)_upperChest=M7RiflePose.Find(P1Animator.transform,"spine_03");
            Quaternion chest=_upperChest!=null ? _upperChest.rotation : P1Animator.transform.rotation;
            Quaternion desired=Quaternion.Euler(pitch*.65f,worldYaw,0);
            Vector3 relative=(Quaternion.Inverse(chest)*desired).eulerAngles;
            _head.rotation=chest*Quaternion.Euler(
                Mathf.Clamp(M2BodySim.Normalize(relative.x),-35f,35f),
                Mathf.Clamp(M2BodySim.Normalize(relative.y),-50f,50f),
                Mathf.Clamp(M2BodySim.Normalize(relative.z),-15f,15f))*_headBasis;
        }

        /// <summary>Short chamber, fast extension, then recovery. Rotate about body-right rather
        /// than imported bone-local X, which can only roll the leg on this source skeleton.</summary>
        public void ApplyKickPose(float phase,bool heavy)
        {
            if(P1Animator==null)return;
            if(_kickThigh==null)_kickThigh=M7RiflePose.Find(P1Animator.transform,"thigh_r");
            if(_kickShin==null)_kickShin=M7RiflePose.Find(P1Animator.transform,"calf_r");
            if(_kickThigh==null || _kickShin==null)return;
            Vector2 chamber=new Vector2(50f,95f),strike=new Vector2(heavy ? 85f : 75f,5f);
            Vector2 pose=phase<.22f ? Vector2.Lerp(Vector2.zero,chamber,Mathf.SmoothStep(0,1,phase/.22f))
                : phase<.34f ? Vector2.Lerp(chamber,strike,Mathf.SmoothStep(0,1,(phase-.22f)/.12f))
                : phase<.42f ? strike
                : phase<.60f ? Vector2.Lerp(strike,chamber,Mathf.SmoothStep(0,1,(phase-.42f)/.18f))
                : Vector2.Lerp(chamber,Vector2.zero,Mathf.SmoothStep(0,1,(phase-.60f)/.40f));
            _kickThigh.rotation=Quaternion.AngleAxis(-pose.x,transform.right)*_kickThigh.rotation;
            _kickShin.rotation=Quaternion.AngleAxis(pose.y,transform.right)*_kickShin.rotation;
        }

        /// <summary>
        /// The animated body falls on death but the world rifle lives under the non-animated
        /// WeaponAnchor/AimPivot, so it would otherwise float in place. Hide it while dead and restore
        /// it when the round respawns the body. No dropped-weapon behaviour yet.
        /// </summary>
        void UpdateWeaponVisibility(bool alive)
        {
            if (Weapon == null || _weaponVisible == alive) return;
            _weaponVisible = alive;
            Weapon.gameObject.SetActive(alive);
        }

        void ApplyAnimator(Animator animator, M2BodyState state, bool alive)
        {
            if (animator == null) return;
            float magnitude=state.PlanarSpeed < .1f ? 0 : Mathf.Min(1.55f,state.PlanarSpeed/(state.Crouching ? 2.5f : 4.5f));
            animator.SetFloat("MoveX",state.MoveRight*magnitude,.055f,Time.deltaTime);
            animator.SetFloat("MoveZ",state.MoveForward*magnitude,.055f,Time.deltaTime);
            animator.SetFloat("Playback",state.PlanarSpeed < .1f ? 1 : Mathf.Clamp(state.PlanarSpeed/(state.Crouching ? 2.5f : 4.5f),.7f,1.55f));
            animator.SetInteger("Action",state.MovementState==(byte)M2MovementState.Slide ? 1 : state.MovementState==(byte)M2MovementState.Dodge ? 3 : 0);
            animator.SetBool(GroundedId, state.Grounded);
            animator.SetFloat(VerticalSpeedId, state.VerticalVelocity);
            animator.SetBool(AliveId, alive);
        }

        /// <summary>Blends the crouch override layer and drops the weapon mount with the stance.</summary>
        void UpdateStance(M2BodyState state, bool alive, float dt)
        {
            float target = alive && state.Crouching && state.MovementState!=(byte)M2MovementState.Slide ? 1f : 0f;
            _crouch01 = Mathf.Lerp(_crouch01, target, 1f - Mathf.Exp(-CrouchBlendSpeed * dt));
            if (P1Animator != null) P1Animator.SetFloat("Crouch", _crouch01);
        }

        void UpdateAim(M2BodyState state, bool alive)
        {
            if (AimPivot == null) return;

            bool localP2 = Client != null && Client.IsLocalOwnBody && Client.LocalRoleIndex == 1;
            float yaw = localP2 ? Client.LocalAimYaw : state.AimYaw;
            float pitch = localP2 ? Client.LocalAimPitch : state.AimPitch;
            float bodyYaw = Client != null ? Client.VisualYaw : state.BodyYaw;

            float kick = _recoil * 4f;
            AimPivot.rotation = Quaternion.Euler(pitch - kick, yaw, 0f);
            if (alive) SetUpperBodyAim(M2BodySim.Normalize(yaw-bodyYaw),pitch);
            if (_upperChest != null) AimPivot.position = _upperChest.position + AimPivot.rotation * new Vector3(.08f, -.04f, .04f);

            // The arm IK is only meaningful while the body is alive and holding the rifle.
            float ikWeight = alive ? 1f : 0f;
            if (ArmIkL != null) ArmIkL.weight = ikWeight;
            if (ArmIkR != null) ArmIkR.weight = ikWeight;
        }

        /// <summary>Rotate the P2 rig around its animated chest, including low stance, rather than
        /// around the standing contract socket. Prevents crouch + extreme pitch moving shoulders away from grips.</summary>
        public void SetUpperBodyAim(float offset,float pitch)
        {
            if(P1Animator==null) return;
            if(_upperChest==null) _upperChest=M7RiflePose.Find(P1Animator.transform,"DEF-spine.003");
            if(_upperChest==null) return;
            // Aim the shared chest bone, not an entire second skeleton around an unrelated socket.
            float bodyYaw=Client!=null && Client.IsSpawned ? Client.VisualYaw : transform.eulerAngles.y;
            _upperChest.rotation=Quaternion.Euler(pitch,bodyYaw+offset,0);
        }

        public float AnimatedChestDrop()
        {
            if(P1Animator==null) return 0;
            if(_upperChest==null) _upperChest=M7RiflePose.Find(P1Animator.transform,"DEF-spine.003");
            return _upperChest!=null ? Mathf.Clamp(1.31f-transform.InverseTransformPoint(_upperChest.position).y,0,.65f) : 0;
        }

        void UpdateCombat(float dt)
        {
            _recoil = Mathf.Max(0f, _recoil - dt * 8f);

            int ammo = Body.Ammo.Value;
            if (_lastAmmo < 0) { _lastAmmo = ammo; return; }
            if (ammo < _lastAmmo && !Body.Reloading.Value)
            {
                _recoil = Mathf.Min(1f, _recoil + 0.7f);
                bool ownP2=Client!=null && Client.IsLocalOwnBody && Client.LocalRoleIndex==1;
                if (!ownP2 && M7AudioService.Instance!=null && Weapon!=null) M7AudioService.Instance.PlayAt(M7AudioId.RifleShot,Weapon.position);
                if (!ownP2 && Time.time >= _nextMuzzle && M7VfxService.Instance != null && Weapon != null)
                {
                    _nextMuzzle = Time.time + 0.045f;
                    Vector3 point = Muzzle != null ? Muzzle.position : Weapon.position + Weapon.forward * 0.45f;
                    Quaternion rotation = Muzzle != null ? Muzzle.rotation : Weapon.rotation;
                    M7VfxService.Instance.Spawn(M7VfxId.MuzzleFlash, point, rotation);
                }
            }
            _lastAmmo = ammo;
        }

        void UpdateMovementFeedback(M2BodyState state,bool alive,float dt)
        {
            if(!alive) { _wasGrounded=state.Grounded; _stepDistance=0; return; }
            var audio=M7AudioService.Instance;
            Vector3 point=Client!=null ? Client.VisualPosition : transform.position;
            if(!_wasGrounded && state.Grounded)
            { audio?.PlayAt(M7AudioId.Land,point); M7VfxService.Instance?.Spawn(M7VfxId.FootstepDust,point,Quaternion.identity); }
            if(_wasGrounded && !state.Grounded && state.VerticalVelocity>1) audio?.PlayAt(M7AudioId.Jump,point);
            if(_lastAction!=state.MovementState)
            {
                if(state.MovementState==(byte)M2MovementState.Slide) audio?.PlayAt(M7AudioId.Slide,point);
                else if(state.MovementState==(byte)M2MovementState.Dodge) audio?.PlayAt(M7AudioId.Jump,point);
                else if(state.MovementState==(byte)M2MovementState.KickLight || state.MovementState==(byte)M2MovementState.KickHeavy) audio?.PlayAt(M7AudioId.Kick,point);
            }
            bool walking=state.Grounded && state.PlanarSpeed>.4f && state.MovementState!=(byte)M2MovementState.Slide;
            if(walking)
            {
                _stepDistance+=state.PlanarSpeed*dt;
                float stride=state.Crouching ? .6f : 1.05f;
                if(_stepDistance>=stride)
                {
                    _stepDistance%=stride;
                    audio?.PlayAt(M7AudioId.Footstep,point); audio?.PlayAt(M7AudioId.Gear,point);
                    if(!state.Crouching) M7VfxService.Instance?.Spawn(M7VfxId.FootstepDust,point,Quaternion.identity);
                }
            }
            else _stepDistance=0;
            _wasGrounded=state.Grounded; _lastAction=state.MovementState;
        }
    }
}
