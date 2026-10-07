using System;
using BeMyArms.M2;
using BeMyArms.M3;
using BeMyArms.M7;
using Unity.Pipeline.Commands;
using UnityEngine;

namespace BeMyArms.QA
{
    /// <summary>Optional development probe. Samples real frames/ticks so slow CLI RPCs cannot
    /// miss a kick's strike phase or mistake a multi-tick snapshot for a one-tick body snap.</summary>
    [DefaultExecutionOrder(300)]
    public class SoloCleanupProbe : MonoBehaviour
    {
        public int TurnSamples, StrikeSamples, RaySamples;
        public float MaxBotTurnRate, MaxWristBend, MinKickReach=10, MinKickAlignment=1, MaxShotDeviation, MaxSpread;
        public int MaxBurst;
        uint _tick,_epoch=uint.MaxValue,_shots;
        float _yaw;

        [CliCommand("qa_solo_probe", "Start/reset or read a real-frame solo bot/weapon/kick/grip probe.",
            MainThreadRequired=true, RuntimeOnly=true, Tags=new[]{"qa"})]
        public static object Probe([CliArg("reset", "Reset the measurements.")] bool reset=false)
        {
            var probe=FindAnyObjectByType<SoloCleanupProbe>();
            if(reset && probe!=null){DestroyImmediate(probe.gameObject);probe=null;}
            if(probe==null)probe=new GameObject("SoloCleanupProbe").AddComponent<SoloCleanupProbe>();
            return new {probe.TurnSamples,probe.MaxBotTurnRate,probe.StrikeSamples,probe.MinKickReach,
                probe.MinKickAlignment,probe.MaxWristBend,probe.RaySamples,probe.MaxShotDeviation,probe.MaxSpread,probe.MaxBurst};
        }

        void LateUpdate()
        {
            M3DuelBody body=null;
            var player=FindAnyObjectByType<M7LocalPlayer>();var client=player!=null ? player.LocalClient : null;
            if(client!=null)body=client.Body;
            else foreach(var candidate in FindObjectsByType<M3DuelBody>(FindObjectsSortMode.None))
                if(candidate.IsServer && candidate.TeamIndex==0){body=candidate;break;}
            if(body==null || !body.IsSpawned)return;
            var state=body.State.Value;
            if(_epoch!=state.ControlEpoch){_epoch=state.ControlEpoch;_tick=state.SimulationTick;_yaw=state.BodyYaw;_shots=body.ShotsFired.Value;}
            if(_tick!=state.SimulationTick)
            {
                if(body.P1Bot.Value)
                {
                    float seconds=(state.SimulationTick-_tick)/60f;
                    MaxBotTurnRate=Mathf.Max(MaxBotTurnRate,Mathf.Abs(M2BodySim.Normalize(state.BodyYaw-_yaw))/seconds);TurnSamples++;
                }
                _tick=state.SimulationTick;_yaw=state.BodyYaw;
            }
            if(body.IsServer && _shots!=body.ShotsFired.Value)
            {
                _shots=body.ShotsFired.Value;RaySamples++;
                var aim=Quaternion.Euler(state.AimPitch,state.AimYaw,0)*Vector3.forward;
                MaxShotDeviation=Mathf.Max(MaxShotDeviation,Vector3.Angle(aim,body.LastShotDirection));
                MaxSpread=Mathf.Max(MaxSpread,body.LastShotSpread);MaxBurst=Mathf.Max(MaxBurst,body.RifleBurst);
            }
            if(client==null)return;
            var driver=body.GetComponent<M7CharacterAnimator>();var view=player.Viewmodel;
            if(view!=null)
            {
                var pose=view.GetComponent<M7RiflePose>();
                if(pose.ReloadProgress==0)foreach(string s in new[]{"l"})
                {
                    var elbow=M7RiflePose.Find(pose.Model,"lowerarm_"+s);var hand=M7RiflePose.Find(pose.Model,"hand_"+s);var knuckle=M7RiflePose.Find(pose.Model,"middle_01_"+s);
                    MaxWristBend=Mathf.Max(MaxWristBend,Vector3.Angle(hand.position-elbow.position,knuckle.position-hand.position));
                }
            }
            var visual=client.ViewState;
            bool heavy=visual.MovementState==(byte)M2MovementState.KickHeavy;
            if(!heavy && visual.MovementState!=(byte)M2MovementState.KickLight)return;
            float phase=1-visual.ActionTimeLeft/(heavy ? .7f : .35f);
            if(phase<.33f || phase>.50f)return;
            var thigh=M7RiflePose.Find(driver.P1Animator.transform,"thigh_r");var foot=M7RiflePose.Find(driver.P1Animator.transform,"foot_r");
            var attack=new Vector3(visual.ActionDirX,0,visual.ActionDirZ);var extension=foot.position-thigh.position;
            MinKickReach=Mathf.Min(MinKickReach,Vector3.Dot(extension,attack));extension.y=0;
            MinKickAlignment=Mathf.Min(MinKickAlignment,Vector3.Dot(extension.normalized,attack));StrikeSamples++;
            if(StrikeSamples==1)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath,"..","QA","runtime-kick-strike.png"));
        }
    }
}
