// Only the smoke script's disposable server. Intentionally advances/resets its match; never
// run this against a human session. Uses the ordinary MatchState events and server round reset.
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
object Field(object obj, string name) => obj.GetType().GetField(name, flags).GetValue(obj);
object Value(object obj, string name) => Field(obj, name).GetType().GetProperty("Value").GetValue(Field(obj, name));
void Assert(bool ok, string message) { if (!ok) throw new System.Exception(message); }
var type = System.Type.GetType("BeMyArms.Match.NetworkBody, BeMyArms.Match");
var bodies = UnityEngine.Object.FindObjectsByType(type).OrderBy(b => (int)type.GetProperty("TeamIndex").GetValue(b)).ToArray();
Assert(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Boatyard", "Expected Boatyard server");
Assert(bodies.Length == 2 && (bool)type.GetProperty("IsServer").GetValue(bodies[0]), "Disposable Duel server required");
var director = Field(bodies[0], "_director");
((UnityEngine.Behaviour)director).enabled = false;
foreach (var body in bodies) ((UnityEngine.Behaviour)body).enabled = false;
var owners = bodies.Select(b => new[] { (ulong)Field(b, "_ownerP1"), (ulong)Field(b, "_ownerP2") }).ToArray();
var slots = bodies.Select(b => new[] { (int)type.GetProperty("SlotP1").GetValue(b), (int)type.GetProperty("SlotP2").GetValue(b) }).ToArray();
UnityEngine.Vector3 Position(object b) {
    var s = Value(b, "State"); return new UnityEngine.Vector3((float)Field(s, "PosX"), (float)Field(s, "PosY"), (float)Field(s, "PosZ"));
}
var match = Field(director, "_match"); var matchType = match.GetType();
matchType.GetMethod("StartMatch").Invoke(match, null);
var initial = bodies.Select(Position).ToArray();
Assert(initial[0] == new UnityEngine.Vector3(-8, 2.4f, 16) && initial[1] == new UnityEngine.Vector3(10, 0, -16), "First round uses authored starts");
var reports = new System.Collections.Generic.List<object>();
for (int round = 1; round <= 3; round++) {
    for (int team = 0; team < 2; team++) {
        Assert(Position(bodies[team]) == initial[round % 2 == 0 ? 1 - team : team], "Actual round reset did not exchange sides");
        Assert((int)type.GetProperty("TeamIndex").GetValue(bodies[team]) == team, "Round changed team identity");
        Assert((ulong)Field(bodies[team], "_ownerP1") == owners[team][0] && (ulong)Field(bodies[team], "_ownerP2") == owners[team][1], "Round changed role owners");
        Assert((int)type.GetProperty("SlotP1").GetValue(bodies[team]) == slots[team][0] && (int)type.GetProperty("SlotP2").GetValue(bodies[team]) == slots[team][1], "Round changed role slots");
    }
    var a = Position(bodies[0]); var b = Position(bodies[1]);
    // Vector3 has recursively nested properties; use plain arrays for Pipeline JSON results.
    reports.Add(new { Round = round, TeamA = new[] { a.x, a.y, a.z }, TeamB = new[] { b.x, b.y, b.z } });
    if (round == 3) break;
    matchType.GetMethod("Tick").Invoke(match, new object[] { 1000f }); // Buy -> Live
    matchType.GetMethod("ReportTeamEliminated").Invoke(match, new object[] { round % 2 });
    matchType.GetMethod("Tick").Invoke(match, new object[] { 1000f }); // RoundEnd -> next Buy
}
Assert((int)matchType.GetProperty("TeamAWins").GetValue(match) == 1 && (int)matchType.GetProperty("TeamBWins").GetValue(match) == 1, "Side changes must not exchange or reset team scores");
return new { Success = true, Rounds = reports, OwnersAndScoresPreserved = true };
