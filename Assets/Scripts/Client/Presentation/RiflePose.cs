using System.Collections.Generic;
using BeMyArms.Match;
using UnityEngine;

namespace BeMyArms.Client
{
    /// <summary>Authored rifle grips, elbow poles, finger pose and magazine/bolt reload choreography.
    /// Runs after Mecanim and camera composition. Entirely cosmetic; never moves logical aim.</summary>
    [DefaultExecutionOrder(150)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M7", "BeMyArms.M7", "M7RiflePose")]
    public class RiflePose : MonoBehaviour
    {
        public Transform Model;
        public Transform Weapon;
        public NetworkBody Body;
        public bool FirstPerson;
        public Vector3 FirstPersonSupportShoulderPosition = new Vector3(-.22f,-.30f,.24f);
        public Renderer[] FirstPersonReloadOnlyRenderers = System.Array.Empty<Renderer>();
        [System.Serializable]
        public struct FingerRotation { public Transform Bone; public Quaternion Rotation; }
        public FingerRotation[] AuthoredFingers = System.Array.Empty<FingerRotation>();
        public float ReloadProgress { get; private set; }
        public float LeftGripError { get; private set; }
        public float RightGripError { get; private set; }
        Transform _upperL, _lowerL, _handL, _upperR, _lowerR, _handR, _gripL, _gripR, _magazine, _bolt;
        Vector3 _weaponBase, _magBase, _boltBase;
        Quaternion _weaponRotation;
        readonly List<(Transform bone, Quaternion rest, float curl)> _fingers = new();
        bool _ready, _reloading;
        float _reloadStarted;
        uint _epoch = uint.MaxValue;
        int _reloadStage;

        public void Initialize()
        {
            if (_ready || Model == null || Weapon == null) return;
            _upperL=Find(Model,"DEF-upper_arm.L"); _lowerL=Find(Model,"DEF-forearm.L"); _handL=Find(Model,"DEF-hand.L");
            _upperR=Find(Model,"DEF-upper_arm.R"); _lowerR=Find(Model,"DEF-forearm.R"); _handR=Find(Model,"DEF-hand.R");
            _gripL=Find(Weapon,"Grip_L"); _gripR=Find(Weapon,"Grip_R");
            _magazine=Find(Weapon,"Magazine"); _bolt=Find(Weapon,"Bolt");
            _weaponBase=Weapon.localPosition; _weaponRotation=Weapon.localRotation;
            if(_magazine!=null) _magBase=_magazine.localPosition;
            if(_bolt!=null) _boltBase=_bolt.localPosition;
            foreach(var t in Model.GetComponentsInChildren<Transform>(true))
                if(t.name.StartsWith("DEF-f_") || t.name.StartsWith("DEF-thumb."))
                {
                    bool index=t.name.Contains("index") && t.name.EndsWith(".R");
                    float curl=t.name.Contains("thumb") ? 30 : index && t.name.Contains(".01.") ? 25 : t.name.Contains(".01.") ? 62 : 78;
                    _fingers.Add((t,t.localRotation,curl));
                }
            _ready=_upperL!=null && _upperR!=null && _handL!=null && _handR!=null && _gripL!=null && _gripR!=null;
        }

        void LateUpdate()
        {
            if(Application.isBatchMode) return;
            if(FirstPerson && Body==null)
            {
                var local=FindAnyObjectByType<LocalPlayer>();
                if(local!=null && local.LocalClient!=null) Body=local.LocalClient.Body;
            }
            if(Body==null || !Body.IsSpawned) return;
            Initialize();
            if(!_ready) return;
            if(_epoch!=Body.State.Value.ControlEpoch)
            { _epoch=Body.State.Value.ControlEpoch; ResetPresentation(); }
            if(!Body.Alive.Value) { ResetPresentation(); return; }
            Pose(Body.Reloading.Value, Time.time);
        }

        public void ResetPresentation()
        {
            _reloading=false; ReloadProgress=0; _reloadStage=0;
            if(Weapon!=null && _ready){ Weapon.localPosition=_weaponBase; Weapon.localRotation=_weaponRotation; }
            if(_magazine!=null){ _magazine.localPosition=_magBase; _magazine.gameObject.SetActive(true); }
            if(_bolt!=null) _bolt.localPosition=_boltBase;
            if(FirstPerson)foreach(var r in FirstPersonReloadOnlyRenderers)if(r!=null)r.enabled=false;
        }

        /// <summary>Also used by the editor correctness fixture to inspect deterministic reload poses.</summary>
        public void Pose(bool reloading,float now)
        {
            Initialize(); if(!_ready) return;
            if(reloading && !_reloading){ _reloadStarted=now; _reloadStage=0; }
            _reloading=reloading;
            if(FirstPerson)foreach(var r in FirstPersonReloadOnlyRenderers)if(r!=null)r.enabled=reloading;
            float seconds=Body!=null ? Loadouts.Stats((WeaponType)Body.WeaponId.Value).ReloadSeconds : 2.2f;
            ReloadProgress=reloading ? Mathf.Clamp01((now-_reloadStarted)/Mathf.Max(.1f,seconds)) : 0;
            float t=ReloadProgress;
            float lift=reloading ? Mathf.SmoothStep(0,1,Mathf.Min(t/.16f,(1-t)/.15f)) : 0;
            Weapon.localPosition=_weaponBase+new Vector3(0,-.035f,-.045f)*lift;
            Weapon.localRotation=_weaponRotation*Quaternion.Euler(-12*lift,-8*lift,-18*lift);
            Vector3 left=_gripL.position;
            Quaternion leftRot=_gripL.rotation;
            if(reloading)
            {
                // Reach magazine, remove below the frame, reinsert, reach bolt, recover foregrip.
                Vector3 mag=Weapon.TransformPoint(new Vector3(-.032f,-.13f,.16f));
                Vector3 low=Weapon.TransformPoint(new Vector3(-.11f,-.29f,.09f));
                Vector3 bolt=Weapon.TransformPoint(new Vector3(.08f,.10f,.12f));
                if(t<.20f) left=Vector3.Lerp(left,mag,Mathf.SmoothStep(0,1,t/.20f));
                else if(t<.40f) left=Vector3.Lerp(mag,low,Mathf.SmoothStep(0,1,(t-.20f)/.20f));
                else if(t<.65f) left=Vector3.Lerp(low,mag,Mathf.SmoothStep(0,1,(t-.40f)/.25f));
                else if(t<.78f) left=Vector3.Lerp(mag,bolt,Mathf.SmoothStep(0,1,(t-.65f)/.13f));
                else if(t<.86f) left=bolt;
                else left=Vector3.Lerp(bolt,left,Mathf.SmoothStep(0,1,(t-.86f)/.14f));
                leftRot=Weapon.rotation*Quaternion.LookRotation(Vector3.forward,Vector3.right);
                if(_magazine!=null)
                {
                    float drop=t<.2f || t>.65f ? 0 : t<.4f ? (t-.2f)/.2f : 1-(t-.4f)/.25f;
                    _magazine.position=Weapon.TransformPoint(new Vector3(0,-.05f,.16f))+Weapon.up*(-.26f*drop);
                    _magazine.gameObject.SetActive(t<.38f || t>.43f);
                }
                if(_bolt!=null) _bolt.localPosition=_boltBase+_bolt.parent.InverseTransformVector(Weapon.forward)*(-.07f*Mathf.Sin(Mathf.Clamp01((t-.78f)/.08f)*Mathf.PI));
                int stage=t>=.80f ? 3 : t>=.65f ? 2 : t>=.20f ? 1 : 0;
                if(stage>_reloadStage)
                {
                    _reloadStage=stage;
                    var audio=AudioService.Instance;
                    var client=Body!=null ? Body.GetComponent<NetworkBodyClient>() : null;
                    bool audible=FirstPerson || client==null || !client.IsLocalOwnBody || client.LocalRoleIndex!=1;
                    if(audio!=null && audible) audio.PlayAt(stage==1 ? AudioId.MagazineOut : stage==2 ? AudioId.MagazineIn : AudioId.Bolt,Weapon.position);
                }
            }
            else
            {
                if(_magazine!=null){ _magazine.localPosition=_magBase; _magazine.gameObject.SetActive(true); }
                if(_bolt!=null) _bolt.localPosition=_boltBase;
            }
            Transform reference=FirstPerson ? transform : Weapon.parent!=null ? Weapon.parent : transform;
            // Fixed camera-local shoulder plus saved weapon sockets: one stable support hand in
            // rifle hold. An absolute position is repeatable even with the POV Animator disabled.
            if(FirstPerson)_upperL.position=transform.TransformPoint(FirstPersonSupportShoulderPosition);
            Solve(_upperR,_lowerR,_handR,_gripR.position,_gripR.rotation,reference.TransformDirection(FirstPerson ? new Vector3(.45f,-.65f,-.65f) : new Vector3(.6f,-.8f,-.2f)),FirstPerson);
            Solve(_upperL,_lowerL,_handL,left,leftRot,reference.TransformDirection(FirstPerson ? new Vector3(-.65f,-.25f,-.5f) : new Vector3(-.65f,-.8f,0)),FirstPerson);
            foreach(var f in _fingers) f.bone.localRotation=f.rest*Quaternion.Euler(f.curl,0,0);
            foreach(var f in AuthoredFingers) if(f.Bone!=null) f.Bone.localRotation=f.Rotation;
            LeftGripError=Vector3.Distance(_handL.position,left); RightGripError=Vector3.Distance(_handR.position,_gripR.position);
        }

        public static void Solve(Transform upper,Transform lower,Transform hand,Vector3 target,Quaternion rotation,Vector3 pole,bool solveForearmTwist=false)
        {
            Vector3 start=upper.position; float a=Vector3.Distance(start,lower.position), b=Vector3.Distance(lower.position,hand.position);
            Vector3 offset=target-start; float d=Mathf.Clamp(offset.magnitude,.01f,a+b-.001f); Vector3 n=offset.normalized;
            Vector3 bend=Vector3.ProjectOnPlane(pole,n).normalized;
            if(bend.sqrMagnitude<.1f) bend=Vector3.ProjectOnPlane(Vector3.up,n).normalized;
            float x=(a*a-b*b+d*d)/(2*d);
            Vector3 elbow=start+n*x+bend*Mathf.Sqrt(Mathf.Max(0,a*a-x*x));
            upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
            if(solveForearmTwist)
            {
                // Solve pronation at the forearm, where it belongs. Leaving all roll correction
                // to the hand collapses weighted wrist vertices even with a reachable grip target.
                Vector3 axis=(hand.position-lower.position).normalized;
                Quaternion delta=rotation*Quaternion.Inverse(hand.rotation);
                Vector3 projected=Vector3.Project(new Vector3(delta.x,delta.y,delta.z),axis);
                Quaternion twist=new Quaternion(projected.x,projected.y,projected.z,delta.w);
                if(Quaternion.Dot(twist,twist)>.0001f)lower.rotation=twist.normalized*lower.rotation;
            }
            hand.rotation=rotation;
        }

        public static Transform Find(Transform root,string name)
        {
            string modern=name;
            if(name=="DEF-spine.003") modern="spine_03";
            else if(name=="DEF-head") modern="Head";
            else if(name.StartsWith("DEF-"))
            {
                modern=name.Substring(4).Replace("upper_arm","upperarm").Replace("forearm","lowerarm").Replace("shin","calf").Replace("shoulder","clavicle");
                modern=modern.Replace(".L","_l").Replace(".R","_r");
            }
            foreach(var t in root.GetComponentsInChildren<Transform>(true)) if(t.name==name || t.name==modern) return t;
            return null;
        }
    }
}
