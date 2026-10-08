// Isolated development SERVER fixture; never save or run against a human playtest session.
// Runtime eval has no gameplay compilation references, so access loaded types via reflection.
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
object Field(object obj,string name)=>obj.GetType().GetField(name,flags).GetValue(obj);
void Set(object obj,string name,object value)=>obj.GetType().GetField(name,flags).SetValue(obj,value);
void Publish(object body,string name,object value)=>Field(body,name).GetType().GetProperty("Value").SetValue(Field(body,name),value);
var type=System.Type.GetType("BeMyArms.Match.NetworkBody, BeMyArms.Match");
var bodies=UnityEngine.Object.FindObjectsByType(type,UnityEngine.FindObjectsSortMode.None);
var source=bodies.Single(b=>(int)type.GetProperty("TeamIndex").GetValue(b)==0);
var target=bodies.Single(b=>(int)type.GetProperty("TeamIndex").GetValue(b)==1);
var shooterSim=Field(source,"_sim");var targetSim=Field(target,"_sim");
string mode=(string)System.AppDomain.CurrentDomain.GetData("CombatCase");
foreach(var body in bodies) {
    ((UnityEngine.Behaviour)body).enabled=false;
    Publish(body,"Alive",true);
    Set(body,"_spawnGraceRemaining",mode=="protected" && body==target ? 4f : 0f);
    Set(body,"_damageRemainder",0f);Set(body,"_botLiveTime",-10000f);
}
shooterSim.GetType().GetMethod("Initialize").Invoke(shooterSim,new object[]{0f,0f,0f,0f});
targetSim.GetType().GetMethod("Initialize").Invoke(targetSim,new object[]{0f,0f,10f,0f});
object pose=Field(targetSim,"State"),sourcePose=Field(shooterSim,"State");
Set(pose,"Health",100);
if(mode=="crouch" || mode=="crouch-cover" || mode=="historical") {
    Set(pose,"Crouching",true);Set(pose,"HitHeight",1.15f);Set(pose,"EyeHeight",1.05f);
}
if(mode=="elevated")Set(pose,"PosY",2f);
Set(sourcePose,"ControlEpoch",Field(source,"_controlEpoch"));
Set(sourcePose,"SimulationTick",Field(source,"_simulationTick"));
Set(pose,"ControlEpoch",Field(target,"_controlEpoch"));
Set(shooterSim,"State",sourcePose);Set(targetSim,"State",pose);
Publish(source,"State",sourcePose);Publish(target,"State",pose);
var collisionType=System.Type.GetType("BeMyArms.Networking.MovementCollision, BeMyArms.Networking");
var collision=System.Activator.CreateInstance(collisionType);
void Wall(float z,float height)=>collisionType.GetMethod("AddBox").Invoke(collision,new object[]{-2f,0f,z,2f,height,z+.2f});
if(mode=="wall")Wall(5,3);
if(mode=="behind-surface")Wall(9.9f,3);
if(mode=="head-cover" || mode=="crouch-cover")Wall(5,1.25f);
Set(shooterSim,"Collision",collision);
var loadout=type.GetMethod("ServerApplyLoadout");
loadout.Invoke(source,new[]{System.Enum.ToObject(loadout.GetParameters()[0].ParameterType,0)});
foreach(string name in new[]{"_weapon","_rifle"}) {var obj=Field(source,name);obj.GetType().GetMethod("Reset").Invoke(obj,null);}
Publish(source,"ShotsFired",0u);Publish(source,"Ammo",30);Publish(source,"Kills",0);
double now=(double)Field(source,"_serverTime");
// Box a separate copy for history so the current replicated crouch pose stays untouched.
pose=Field(targetSim,"State");
if(mode=="historical") {Set(pose,"PosY",2f);Set(pose,"Crouching",false);Set(pose,"HitHeight",1.8f);}
foreach(var history in (System.Array)Field(source,"_lag")){
    history.GetType().GetMethod("Clear").Invoke(history,null);
    history.GetType().GetMethods().Single(m=>m.Name=="Record" && m.GetParameters()[2].ParameterType.IsByRef)
        .Invoke(history,new object[]{now-.1,0f,pose,true});
}
Set(source,"LagRewindSeconds",.1f);
var aimHistory=Field(source,"_aimHistory");
aimHistory.GetType().GetMethod("Record").Invoke(aimHistory,new[]{Field(sourcePose,"SimulationTick"),(object)0f});
var geometry=System.Type.GetType("BeMyArms.Match.CombatHitGeometry, BeMyArms.Match");
float headY=((UnityEngine.Vector3)geometry.GetMethod("HeadCenter").Invoke(null,new[]{pose})).y;
float aimY=new[]{"head","head-cover","crouch","crouch-cover","historical"}.Contains(mode) ? headY : 1f;
return new {Pitch=-UnityEngine.Mathf.Atan2(aimY-1.45f,10f)*UnityEngine.Mathf.Rad2Deg,Epoch=Field(sourcePose,"ControlEpoch")};
