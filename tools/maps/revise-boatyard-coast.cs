// One-time live-Editor layout revision of the first blockout, NOT an art pass or regeneration.
// Refuses dirty/already-revised scenes. Original layout remains in Git (96edf68/d003674).
if (UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before authoring.");
var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Boatyard.unity");
bool opened = !scene.IsValid() || !scene.isLoaded;
if (opened) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Boatyard.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
if (scene.isDirty) throw new System.InvalidOperationException("Save/review existing Boatyard edits before applying this revision.");
UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
try {
    var roots = scene.GetRootGameObjects();
    var arena = roots.Single(g => g.name == "Arena").transform;
    var pieces = arena.Cast<UnityEngine.Transform>().ToDictionary(t => t.name);
    var oldFloor = pieces["BMA_Map_Floor_Quay"];
    if (oldFloor.localScale != new UnityEngine.Vector3(40, .3f, 40) || roots.Any(g => g.name == "Backdrop"))
        throw new System.InvalidOperationException("Only the original rectangular blockout may be revised by this script. Edit later revisions in the Editor.");
    foreach (string name in new[] { "CliffBoundary", "SeaBoundary", "ServiceBoundary", "QuayBoundary" })
        if (!pieces.ContainsKey("BMA_Map_Solid_" + name)) throw new System.InvalidOperationException("Original perimeter has been edited; review instead of overwriting.");
    string dir = "Assets/Art/Maps/BoatyardBlockout";
    var ground = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dir + "/Ground.mat");
    var structure = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dir + "/Structure.mat");
    var equipment = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dir + "/Equipment.mat");
    var railing = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dir + "/Railing.mat");

    UnityEngine.GameObject Box(UnityEngine.Transform parent, string name, UnityEngine.Vector3 center, UnityEngine.Vector3 size, UnityEngine.Material material, bool collision = true) {
        var go = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = center; go.transform.localScale = size;
        go.GetComponent<UnityEngine.Renderer>().sharedMaterial = material;
        if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<UnityEngine.Collider>());
        return go;
    }
    void Move(string name, UnityEngine.Vector3 center, UnityEngine.Vector3 size) {
        var t = pieces[name]; t.localPosition = center; t.localScale = size;
    }
    void Solid(string name, UnityEngine.Vector3 center, UnityEngine.Vector3 size, UnityEngine.Material material) =>
        Box(arena, "BMA_Map_Solid_" + name, center, size, material);
    void Floor(string name, UnityEngine.Vector3 center, UnityEngine.Vector3 size) =>
        Box(arena, "BMA_Map_Floor_" + name, center, size, ground);

    // Only these known prototype boundary objects are removed. Match wiring, kit assets,
    // four ramp meshes, materials, accepted player rigs and scene/record GUIDs are preserved.
    foreach (string name in new[] { "CliffBoundary", "SeaBoundary", "ServiceBoundary", "QuayBoundary" })
        UnityEngine.Object.DestroyImmediate(pieces["BMA_Map_Solid_" + name].gameObject);
    UnityEngine.Object.DestroyImmediate(pieces["BMA_Map_Solid_MaintenanceDivider"].gameObject);

    // Three overlapping quay pads bend around a land spur. There is no continuous square floor.
    Move("BMA_Map_Floor_Quay", new UnityEngine.Vector3(-6.5f, -.15f, -8.5f), new UnityEngine.Vector3(9, .3f, 7));
    Floor("QuayBend", new UnityEngine.Vector3(1, -.15f, -12), new UnityEngine.Vector3(14, .3f, 6));
    Floor("QuayBerth", new UnityEngine.Vector3(12.5f, -.15f, -12), new UnityEngine.Vector3(9, .3f, 10));
    Move("BMA_Map_Platform_YardFoundation", new UnityEngine.Vector3(-2.5f, .6f, 2), new UnityEngine.Vector3(21, 1.2f, 14));
    Box(arena, "BMA_Map_Platform_ServiceApron", new UnityEngine.Vector3(9, .6f, 13.25f), new UnityEngine.Vector3(6, 1.2f, 6.5f), ground);

    // Natural/functional land-side limits, with different depths and heights, not an enclosure.
    Solid("CliffLower", new UnityEngine.Vector3(-17, 4, -9), new UnityEngine.Vector3(12, 8, 8), ground);
    Solid("CliffYard", new UnityEngine.Vector3(-18, 5, 2), new UnityEngine.Vector3(10, 10, 14), ground);
    Solid("CliffWorkshop", new UnityEngine.Vector3(-17, 7, 15), new UnityEngine.Vector3(10, 14, 12), ground);
    Solid("CoastSpur", new UnityEngine.Vector3(3, 4, -7), new UnityEngine.Vector3(10, 8, 4), ground);
    Solid("PumpHouse", new UnityEngine.Vector3(10, 3.3f, 1.5f), new UnityEngine.Vector3(4, 6.6f, 17), structure);
    Solid("PumpHouseReturn", new UnityEngine.Vector3(7, 3.3f, 9.5f), new UnityEngine.Vector3(2, 6.6f, 1), structure);
    Solid("ServiceStore", new UnityEngine.Vector3(9, 4, 20.25f), new UnityEngine.Vector3(6, 8, 7.5f), structure);
    Solid("ShorePumpTower", new UnityEngine.Vector3(16, 5.5f, 20), new UnityEngine.Vector3(8, 11, 8), structure);

    Move("BMA_Map_Cover_DryDockHull", new UnityEngine.Vector3(2.5f, 1.2f, -11.8f), new UnityEngine.Vector3(4, 2.4f, 3.2f));
    Move("BMA_Map_Cover_QuayWinch", new UnityEngine.Vector3(-10, .65f, -7.5f), new UnityEngine.Vector3(1.2f, 1.3f, 1.2f));
    Move("BMA_Map_Cover_SpareMotorRack", new UnityEngine.Vector3(4, 2.4f, 4), new UnityEngine.Vector3(2.2f, 2.4f, 3));

    // Open seaward guarding: real posts are close enough to block the body, but gaps remain
    // visible/shootable. Do not fake a transparent wall using one giant collider/AABB.
    void Guard(string name, UnityEngine.Vector3 a, UnityEngine.Vector3 b) {
        float length = UnityEngine.Vector3.Distance(a, b);
        int count = UnityEngine.Mathf.CeilToInt(length / .6f);
        for (int i = 0; i <= count; i++) {
            var p = UnityEngine.Vector3.Lerp(a, b, i / (float)count);
            float height = 2.4f;
            if (name == "BerthSeaEdge" && p.z > -10) height += (p.z + 10) / 5 * 1.2f;
            Solid(name + "_Post_" + i, p + UnityEngine.Vector3.up * (height * .5f), new UnityEngine.Vector3(.12f, height, .12f), railing);
        }
        var span = b - a;
        var size = new UnityEngine.Vector3(UnityEngine.Mathf.Abs(span.x) + .12f, .08f, UnityEngine.Mathf.Abs(span.z) + .12f);
        foreach (float y in new[] { .9f, 2.3f }) Solid(name + "_Rail_" + y, (a + b) * .5f + UnityEngine.Vector3.up * y, size, railing);
    }
    Guard("QuayWestEdge", new UnityEngine.Vector3(-11, 0, -12), new UnityEngine.Vector3(-6, 0, -12));
    Guard("QuayFirstReturn", new UnityEngine.Vector3(-6, 0, -12), new UnityEngine.Vector3(-6, 0, -15));
    Guard("QuayBendEdge", new UnityEngine.Vector3(-6, 0, -15), new UnityEngine.Vector3(8, 0, -15));
    Guard("QuaySecondReturn", new UnityEngine.Vector3(8, 0, -15), new UnityEngine.Vector3(8, 0, -17));
    Guard("BerthEdge", new UnityEngine.Vector3(8, 0, -17), new UnityEngine.Vector3(17, 0, -17));
    Guard("BerthSeaEdge", new UnityEngine.Vector3(17, 0, -17), new UnityEngine.Vector3(17, 0, -5));
    Guard("MaintenanceSeaEdge", new UnityEngine.Vector3(17, 1.2f, -5), new UnityEngine.Vector3(17, 1.2f, 16));
    for (int i = 0; i < 2; i++) {
        float y = i == 0 ? .9f : 2.3f;
        var a = new UnityEngine.Vector3(17, y, -10); var b = new UnityEngine.Vector3(17, y + 1.2f, -5);
        var beam = Box(arena, "BMA_Map_Solid_QuayRampGuard_Rail_" + i, (a + b) * .5f,
            new UnityEngine.Vector3(.12f, .08f, UnityEngine.Vector3.Distance(a, b)), railing);
        beam.transform.rotation = UnityEngine.Quaternion.FromToRotation(UnityEngine.Vector3.forward, (b - a).normalized);
    }

    // Visual-only world continuation is deliberately a separate scene root: it cannot become
    // walkable bot space or imaginary combat cover in MapSpawns.BuildCollision.
    var backdrop = new UnityEngine.GameObject("Backdrop").transform;
    var water = new UnityEngine.Material(ground) { name = "Blockout_WaterContext" };
    water.SetColor("_BaseColor", new UnityEngine.Color(.30f, .34f, .36f));
    UnityEditor.AssetDatabase.CreateAsset(water, dir + "/WaterContext.mat");
    var sea = Box(backdrop, "SeaContext_NotPlayable", new UnityEngine.Vector3(0, -2.6f, 0), new UnityEngine.Vector3(1400, .1f, 1400), water, false);
    sea.GetComponent<UnityEngine.Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    sea.GetComponent<UnityEngine.Renderer>().receiveShadows = false;

    void Mound(string name, UnityEngine.Vector3 center, float width, float depth, float height) {
        var ring = new[] { new UnityEngine.Vector2(-1, -.4f), new UnityEngine.Vector2(-.65f, -.9f),
            new UnityEngine.Vector2(.2f, -1), new UnityEngine.Vector2(.9f, -.55f), new UnityEngine.Vector2(1, .25f),
            new UnityEngine.Vector2(.45f, 1), new UnityEngine.Vector2(-.4f, .8f), new UnityEngine.Vector2(-.9f, .35f) };
        var vertices = new UnityEngine.Vector3[10]; vertices[0] = new UnityEngine.Vector3(-width * .12f, height, depth * .05f);
        for (int i = 0; i < 8; i++) vertices[i + 1] = new UnityEngine.Vector3(ring[i].x * width * .5f, 0, ring[i].y * depth * .5f);
        vertices[9] = new UnityEngine.Vector3(0, -4, 0);
        var triangles = new System.Collections.Generic.List<int>();
        for (int i = 0; i < 8; i++) {
            int a = i + 1, b = (i + 1) % 8 + 1;
            triangles.AddRange(new[] { 0, b, a, 9, a, b });
        }
        var mesh = new UnityEngine.Mesh { name = name, vertices = vertices, triangles = triangles.ToArray() };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        UnityEditor.AssetDatabase.CreateAsset(mesh, dir + "/" + name + ".asset");
        var go = new UnityEngine.GameObject(name, typeof(UnityEngine.MeshFilter), typeof(UnityEngine.MeshRenderer));
        go.transform.SetParent(backdrop, false); go.transform.localPosition = center;
        go.GetComponent<UnityEngine.MeshFilter>().sharedMesh = mesh; go.GetComponent<UnityEngine.Renderer>().sharedMaterial = ground;
    }
    Mound("UpcoastLand", new UnityEngine.Vector3(-65, 3, 55), 105, 110, 33);
    Mound("BeyondPumpHeadland", new UnityEngine.Vector3(50, -1.8f, 55), 75, 65, 22);
    Mound("DistantIsland", new UnityEngine.Vector3(125, -2, -65), 80, 70, 27);
    Box(backdrop, "UphillTerrace", new UnityEngine.Vector3(-37, 5, 32), new UnityEngine.Vector3(32, 10, 30), ground, false);
    Box(backdrop, "ServiceCourtBeyondWorkshop", new UnityEngine.Vector3(-1, 2.25f, 26), new UnityEngine.Vector3(22, .3f, 14), ground, false);
    Box(backdrop, "ServiceRoadContinues", new UnityEngine.Vector3(-3, 2.4f, 45), new UnityEngine.Vector3(5, .15f, 30), ground, false);
    foreach (var p in new[] { new UnityEngine.Vector3(-39, 13, 32), new UnityEngine.Vector3(-27, 12.5f, 38), new UnityEngine.Vector3(4, 5, 28) }) {
        var size = p.y > 10 ? new UnityEngine.Vector3(9, 5, 8) : new UnityEngine.Vector3(7, 5, 7);
        string label = "NeighborWorkshop_" + p.x;
        Box(backdrop, label, p, size, structure, false);
        Box(backdrop, label + "_Roof", p + UnityEngine.Vector3.up * (size.y * .5f + .2f), new UnityEngine.Vector3(size.x + .5f, .4f, size.z + .5f), equipment, false);
    }
    Box(backdrop, "RemoteBerth", new UnityEngine.Vector3(32, -.4f, -30), new UnityEngine.Vector3(5, .8f, 17), ground, false);
    Box(backdrop, "MooredBoatHull", new UnityEngine.Vector3(25, -1, -19), new UnityEngine.Vector3(4, 1.6f, 10), equipment, false);
    Box(backdrop, "MooredBoatCabin", new UnityEngine.Vector3(25, .25f, -18), new UnityEngine.Vector3(2.5f, .9f, 3), structure, false);

    var map = roots.SelectMany(g => g.GetComponentsInChildren<BeMyArms.Match.MapSpawns>()).Single();
    var record = UnityEditor.AssetDatabase.LoadAssetAtPath<BeMyArms.Client.MapDefinition>("Assets/Art/Maps/Boatyard.asset");
    record.CoverCount = arena.Cast<UnityEngine.Transform>().Count(t => t.name.StartsWith("BMA_Map_Cover"));
    float clearance = float.MaxValue;
    foreach (var s in map.Spawns) foreach (var renderer in arena.GetComponentsInChildren<UnityEngine.Renderer>()) {
        var b = renderer.bounds;
        if (b.max.y <= s.Position.y + .55f || b.min.y >= s.Position.y + 1.8f) continue;
        float dx = UnityEngine.Mathf.Max(0, UnityEngine.Mathf.Max(b.min.x - s.Position.x, s.Position.x - b.max.x));
        float dz = UnityEngine.Mathf.Max(0, UnityEngine.Mathf.Max(b.min.z - s.Position.z, s.Position.z - b.max.z));
        clearance = UnityEngine.Mathf.Min(clearance, UnityEngine.Mathf.Sqrt(dx * dx + dz * dz));
    }
    record.MinSpawnClearance = clearance;
    UnityEditor.EditorUtility.SetDirty(record);
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    UnityEditor.AssetDatabase.SaveAssets();
    return new { Scene = scene.path, ArenaPieces = arena.childCount, BackdropPieces = backdrop.childCount,
        QuayFloorArea = 225, SpawnClearance = clearance, Notes = "Bent coast, smaller connected pockets, visual-only world continuation. Not final art." };
}
finally {
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    if (opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
}
