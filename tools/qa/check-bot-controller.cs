// Disposable, isolated development server only. Runs the real ServerTick/authority/sim path
// against controlled sight/target changes; never run in a human playtest or save this fixture.
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
var type=System.Type.GetType("BeMyArms.Match.NetworkBody, BeMyArms.Match");
var bodies=UnityEngine.Object.FindObjectsByType(type);
var source=bodies.Single(b=>(int)type.GetProperty("TeamIndex").GetValue(b)==0);
var target=bodies.Single(b=>(int)type.GetProperty("TeamIndex").GetValue(b)==1);
if(!(bool)type.GetProperty("IsServer").GetValue(source))throw new System.Exception("Isolated server required.");
object Field(object obj,string name)=>obj.GetType().GetField(name,flags).GetValue(obj);
void Set(object obj,string name,object value)=>obj.GetType().GetField(name,flags).SetValue(obj,value);
object Value(object obj,string name)=>Field(obj,name).GetType().GetProperty("Value").GetValue(Field(obj,name));
void Publish(object obj,string name,object value)=>Field(obj,name).GetType().GetProperty("Value").SetValue(Field(obj,name),value);
void Assert(bool ok,string message){if(!ok)throw new System.Exception(message);}
Assert((bool)Value(source,"P1Bot") && (bool)Value(source,"P2Bot"),"Both roles must be bot-owned in this isolated fixture.");
foreach(var body in bodies)((UnityEngine.Behaviour)body).enabled=false;
var director=Field(source,"_director");((UnityEngine.Behaviour)director).enabled=false;
var collisionType=System.Type.GetType("BeMyArms.Networking.MovementCollision, BeMyArms.Networking");
var config=System.Type.GetType("BeMyArms.Match.MatchConfig, BeMyArms.Match");
var reports=new System.Collections.Generic.List<object>();
foreach(int difficulty in new[]{0,1}){
    var difficultyField=config.GetField("BotDifficulty");difficultyField.SetValue(null,System.Enum.ToObject(difficultyField.FieldType,difficulty));
    type.GetMethod("ServerResetRound").Invoke(source,new object[]{0f,0f,0f,0f});
    type.GetMethod("ServerResetRound").Invoke(target,new object[]{10f,0f,10f,180f});
    type.GetMethod("ServerAutoBuyBotLoadout").Invoke(source,null);
    Set(source,"_botReaction",0f);Set(source,"_botLiveTime",20f);Set(source,"EasyBotAccuracy",1f);
    var map=System.Activator.CreateInstance(collisionType);
    Set(Field(source,"_sim"),"Collision",map);
    Set(source,"LagRewindSeconds",0f);Set(target,"_spawnGraceRemaining",100f);
    var tick=type.GetMethod("ServerTick",flags);
    int samples=0,turns=0,maxTickFrame=0;float maxAimRate=0,maxBodyRate=0;double maxTickMs=0;
    var tickDurations=new System.Collections.Generic.List<double>();
    for(int frame=0;frame<600;frame++){
        // Crossing the aim direction during live tracking must not bypass the velocity bound.
        if(frame==180 || frame==360){
            var sim=Field(target,"_sim");var pose=Field(sim,"State");
            Set(pose,"PosX",frame==180 ? -10f : 8f);Set(sim,"State",pose);Publish(target,"State",pose);
        }
        var before=Value(source,"State");float yaw=(float)Field(before,"AimYaw"),pitch=(float)Field(before,"AimPitch"),bodyYaw=(float)Field(before,"BodyYaw");
        var began=System.DateTime.UtcNow;tick.Invoke(source,new object[]{1f/60f});
        double milliseconds=(System.DateTime.UtcNow-began).TotalMilliseconds;tickDurations.Add(milliseconds);
        if(milliseconds>maxTickMs){maxTickMs=milliseconds;maxTickFrame=frame;}
        var after=Value(source,"State");
        float Wrap(float a){a%=360;return a>180 ? a-360 : a<=-180 ? a+360 : a;}
        float dy=Wrap((float)Field(after,"AimYaw")-yaw),dp=(float)Field(after,"AimPitch")-pitch;
        maxAimRate=UnityEngine.Mathf.Max(maxAimRate,UnityEngine.Mathf.Sqrt(dy*dy+dp*dp)*60);
        maxBodyRate=UnityEngine.Mathf.Max(maxBodyRate,UnityEngine.Mathf.Abs(Wrap((float)Field(after,"BodyYaw")-bodyYaw))*60);
        if(UnityEngine.Mathf.Abs(dy)+UnityEngine.Mathf.Abs(dp)>.001f)turns++;
        samples++;
    }
    uint shots=(uint)Value(source,"ShotsFired");
    Assert(shots>0,"Bot never acquired and fired on an unobstructed target.");
    Assert(turns>30,"Bot aim must visibly travel, not only update on firing ticks.");
    Assert(maxBodyRate<=90.05f,"P1 body turn exceeded the accepted bot steering bound.");
    // Aim speed plus a moving sector wall: P1 may legitimately push P2 when the sector moves.
    Assert(maxAimRate<=(difficulty==0 ? 191f : 251f),"P2 aim snapped during acquisition, burst or target crossing.");
    float rememberedX=(float)Field(Field(source,"_botTargetPose"),"PosX");
    collisionType.GetMethod("AddBox").Invoke(map,new object[]{-50f,0f,4f,50f,4f,4.5f});
    var hiddenSim=Field(target,"_sim");var hiddenPose=Field(hiddenSim,"State");
    Set(hiddenPose,"PosX",17f);Set(hiddenSim,"State",hiddenPose);Publish(target,"State",hiddenPose);Set(source,"_botSenseAt",0d);
    for(int i=0;i<120;i++)tick.Invoke(source,new object[]{1f/60f});
    Assert((uint)Value(source,"ShotsFired")==shots,"Bot fired at an occluded target.");
    Assert((float)Field(Field(source,"_botTargetPose"),"PosX")==rememberedX,"Bot tracked live target position through the wall.");
    type.GetMethod("ServerResetRound").Invoke(source,new object[]{0f,0f,0f,0f});
    Assert(Field(source,"_botTarget")==null && !(bool)Field(source,"_botVisible"),"Reset retained old bot perception.");
    tickDurations.Sort();
    reports.Add(new {Difficulty=difficulty==0 ? "Easy" : "Hard",Samples=samples,MovingAimSamples=turns,Shots=shots,
        MaxAimDegreesPerSecond=maxAimRate,MaxBodyDegreesPerSecond=maxBodyRate,MaxTickMilliseconds=maxTickMs,
        MaxTickFrame=maxTickFrame,P95TickMilliseconds=tickDurations[(int)(tickDurations.Count*.95)],
        HiddenTargetSuppressed=true,ResetClearedPerception=true});
}
return new {Success=true,Reports=reports};
