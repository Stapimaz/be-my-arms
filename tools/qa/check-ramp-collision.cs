// Disposable Boatyard development SERVER only. Drives its actual loaded collision/simulation;
// resets its body and freezes the session. Never run this in a human playtest.
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
object Field(object obj, string name) => obj.GetType().GetField(name, flags).GetValue(obj);
void Assert(bool ok, string message) { if (!ok) throw new System.Exception(message); }
var type = System.Type.GetType("BeMyArms.Match.NetworkBody, BeMyArms.Match");
var bodies = UnityEngine.Object.FindObjectsByType(type);
var body = bodies.Single(b => (int)type.GetProperty("TeamIndex").GetValue(b) == 0);
Assert((bool)type.GetProperty("IsServer").GetValue(body), "Disposable server required");
Assert(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Boatyard", "Expected authored Boatyard geometry");
foreach (var b in bodies) ((UnityEngine.Behaviour)b).enabled = false;
((UnityEngine.Behaviour)Field(body, "_director")).enabled = false;
var sim = Field(body, "_sim"); var map = Field(sim, "Collision");
var move = sim.GetType().GetMethod("ApplyP1"); var inputType = move.GetParameters()[0].ParameterType.GetElementType();
void Walk(int frames, float forward) {
    var input = System.Activator.CreateInstance(inputType);
    inputType.GetField("MoveZ").SetValue(input, forward);
    for (int i = 0; i < frames; i++) move.Invoke(sim, new object[] { input, 1f / 60f });
}
float Coordinate(string name) => (float)Field(Field(sim, "State"), name);
void Reset(float x, float y, float z, float yaw) => type.GetMethod("ServerResetRound").Invoke(body, new object[] { x, y, z, yaw });
Reset(-9.6f, 0, -6, 90); Walk(90, 1);
Assert(Coordinate("PosX") <= -9.399f && Coordinate("PosY") == 0, "High side swallowed the server body");
Reset(-6, 0, -11.5f, 0); Walk(110, 1);
Assert(Coordinate("PosZ") > -4 && System.Math.Abs(Coordinate("PosY") - 1.2f) < .025f, "Ordinary ascent is obstructed");
Walk(110, -1);
Assert(System.Math.Abs(Coordinate("PosY")) < .025f, "Ordinary descent is obstructed");
var ray = map.GetType().GetMethod("RaycastSolids");
var rayArgs = new object[] { -10f, .5f, -6f, 1f, 0f, 0f, 4f, 0f };
Assert((bool)ray.Invoke(map, rayArgs) && System.Math.Abs((float)rayArgs[7] - 1) < .001f, "Rifle ray did not hit the actual solid side");
rayArgs[2] = -10f;
Assert(!(bool)ray.Invoke(map, rayArgs), "Ramp AABB incorrectly obstructs empty air above the low slope");
return new { Success = true, Checks = 5, SolidSide = true, Ascent = true, Descent = true, ExactRayEntry = true, ClearAir = true };
