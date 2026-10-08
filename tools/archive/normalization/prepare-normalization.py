"""One-time migration plan. Unity applies asset moves, preserving their .meta GUIDs.

Writes a reviewable manifest and staged source rewrites to ignored Builds/Normalization.
Does not edit Unity assets or execute a build.
"""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Builds/Normalization"
OUT.mkdir(parents=True, exist_ok=True)
MODULES = {
    "M0": "Core", "M1": "Gameplay", "M2": "Networking", "M3": "Match",
    "M4": "Matchmaking", "M5": "Product", "M6": "Content", "M7": "Client",
}
OVERRIDES = {
    "M0Tuning": "SharedControlTuning", "M0BodyRoot": "SharedControlSampleBody",
    "M0SceneBuilder": "SharedControlSceneBuilder", "M0DebugHud": "SharedControlHud",
    "M0SceneWiringTests": "SharedControlSceneWiringTests", "M0PlayModeSmokeTests": "SharedControlSmokeTests",
    "M1Tuning": "LocalCombatTuning", "M1BodyRoot": "LocalCombatBody",
    "M1SceneBuilder": "LocalCombatSceneBuilder", "M1Hud": "LocalCombatHud",
    "M1P1Camera": "LocalP1Camera", "M1P2Camera": "LocalP2Camera",
    "M1InputMode": "LocalInputMode", "M1Commands": "LocalCombatCommands",
    "M1P1DeviceInputSource": "LocalP1DeviceInputSource", "M1P2DeviceInputSource": "LocalP2DeviceInputSource",
    "M1ScriptedInputSource": "LocalScriptedInputSource",
    "M2Config": "PredictionHarnessConfig", "M2Bootstrap": "PredictionHarnessBootstrap",
    "M2NetworkBody": "PredictionHarnessBody", "M2RoleService": "PredictionHarnessRoleService",
    "M2ClientPredictor": "PredictionHarnessClient", "M2Role": "PredictionHarnessRole",
    "M2SceneBuilder": "PredictionHarnessSceneBuilder", "M2Build": "PredictionHarnessBuild",
    "M2MovementState": "BodyMovementState", "M2RoleRegistry": "BodyRoleRegistry",
    "M3Config": "MatchConfig", "M3DuelRole": "MatchProcessRole",
    "M3DuelBootstrap": "MatchBootstrap", "M3DuelDirector": "MatchDirector",
    "M3DuelBody": "NetworkBody", "M3DuelClient": "NetworkBodyClient",
    "M3DuelRoleService": "MatchRoleService", "M3DuelRoster": "MatchRoster",
    "M3DuelSlots": "MatchSlots", "M3DuelSlot": "MatchSlot",
    "M3DuelHud": "MatchDebugHud", "M3DuelSceneBuilder": "MatchSampleSceneBuilder",
    "M3DuelBuild": "MatchSampleBuild", "M3QueueEntry": "RoleQueueEntry",
    "M3SlotAssignment": "RoleSlotAssignment", "M3Phase": "RoundPhase",
    "M3WeaponId": "WeaponType",
    "M3MapSpawn": "BodySpawn", "M4QueueEntry": "MatchmakingQueueEntry",
    "M4SlotAssignment": "MatchSlotAssignment", "M4Rating": "RoleRating",
    "M4MatchSlot": "MatchmakingSlot",
    "M4MatchHost": "MatchmakingHost", "M4MatchBridge": "MatchmakingBridge",
    "M4PlayerProfile": "MatchmakingPlayerProfile", "M6WeaponKind": "AssetWeaponKind",
    "M6PipelineCommands": "AssetPipeline", "M7PipelineCommands": "ContentPipeline",
    "M6PrefabBuilders": "AssetPrefabBuilders", "M6Descriptors": "WeaponDescriptor",
    "M7Spawn": "MapSpawn", "M05NgoSceneBuilder": "NetcodeComparisonSceneBuilder",
    "SoloCleanupProbe": "HandlingProbe",
}
texts = {p: p.read_text(encoding="utf-8-sig") for p in (ROOT / "Assets").rglob("*.cs")}
symbols = set()
for text in texts.values():
    symbols.update(re.findall(r"\bI?M(?:0[5]?|[1-7])[A-Z]\w*\b", text))
symbols.update(p.stem for p in texts if re.match(r"M\d", p.stem))
types = {symbol: OVERRIDES.get(symbol, re.sub(r"^(I?)M(?:0[5]?|[1-7])", r"\1", symbol)) for symbol in symbols}
types.update(OVERRIDES)
assemblies = {"BeMyArms."+old: "BeMyArms."+new for old, new in MODULES.items()}
assemblies["BeMyArms.M05.Ngo"] = "BeMyArms.Diagnostics.NetcodeComparison"
assemblies["BeMyArms.NetProbe"] = "BeMyArms.Diagnostics.NetworkingProbe"
moves = []
def move(old, new):
    moves.append({"From": old, "To": new})
for old, new in MODULES.items():
    move("Assets/Scripts/"+old, "Assets/Scripts/"+new)
move("Assets/Scripts/M05", "Assets/Scripts/Diagnostics/NetcodeComparison")
move("Assets/Scripts/NetProbe", "Assets/Scripts/Diagnostics/NetworkingProbe")
for mode in ("EditMode", "PlayMode"):
    for old, new in MODULES.items():
        if (ROOT / f"Assets/Tests/{mode}/{old}").is_dir():
            move(f"Assets/Tests/{mode}/{old}", f"Assets/Tests/{mode}/{new}")
for directory, new in {
    "Assets/Scripts/Networking/Net": "Assets/Scripts/Networking/Samples/PredictionHarness",
    "Assets/Scripts/Networking/Editor": "Assets/Scripts/Networking/Samples/PredictionHarness/Editor",
    "Assets/Scripts/Networking/Sim": "Assets/Scripts/Networking/Simulation",
    "Assets/Scripts/Match/Net": "Assets/Scripts/Match/Networking",
    "Assets/Scripts/Match/Sim": "Assets/Scripts/Match/Rules",
    "Assets/Scripts/Matchmaking/Sim": "Assets/Scripts/Matchmaking/Rules",
    "Assets/Scripts/Product/Sim": "Assets/Scripts/Product/Services",
    "Assets/Scripts/Client/Sim": "Assets/Scripts/Client/Content",
}.items():
    move(directory, new)
move("Assets/Art/Characters/Pass2Redo", "Assets/Art/Characters/SharedRig")
move("Assets/Art/Characters/Pass2", "Assets/Art/Characters/Samples/SegmentRig")
move("Assets/Art/Characters/Quaternius", "Assets/Art/Characters/Samples/SkeletalStudies")
for old, new in {
    "M7MainMenu": "MainMenu", "M7DuelArena": "DuelArena", "M7TwoVsTwoArena": "TwoVsTwoArena",
    "M0SharedBody": "Samples/SharedControl", "M05Ngo": "Samples/NetcodeComparison",
    "M1VerticalSlice": "Samples/LocalCombat", "M2NetworkingSpike": "Samples/PredictionHarness",
    "M3Duel": "Samples/MatchHarness", "M6ProductionShowcase": "Samples/AssetShowcase",
    "NetProbe": "Samples/NetworkingProbe", "SampleScene": "Samples/URPTemplate",
}.items():
    move(f"Assets/Scenes/{old}.unity", f"Assets/Scenes/{new}.unity")

def destination(path):
    for entry in moves:
        old, new = entry["From"], entry["To"]
        if path == old or path.startswith(old+"/"):
            path = new + path[len(old):]
    return path

# Keep script class filenames and asmdef names in agreement with the new type names.
asset_paths = [p.relative_to(ROOT).as_posix() for p in (ROOT/"Assets").rglob("*") if p.is_file() and p.suffix!=".meta"]
for path in asset_paths:
    current = destination(path)
    p = Path(current)
    name = p.name
    if p.suffix in (".cs", ".asmdef"):
        stem = p.stem
        stem = types.get(stem, stem)
        for old, new in sorted(assemblies.items(), key=lambda pair: -len(pair[0])):
            stem = stem.replace(old, new)
        name = stem+p.suffix
    elif re.match(r"M\d", name):
        fallback = re.sub(r"^M\d+_?", "", p.stem)
        if name.startswith("M0_"):
            fallback = "Core_"+name[3:-len(p.suffix)]
        name = types.get(p.stem, fallback)+p.suffix
    elif name == "BMA_Weapon_Rifle_Pass2.prefab":
        name = "BMA_Weapon_Rifle_View.prefab"
    if name != p.name:
        move(current, p.with_name(name).as_posix())

# Organize rules by their actual domain without changing the assembly graph.
for path in asset_paths:
    current = destination(path)
    p = Path(current)
    if current.startswith("Assets/Scripts/Match/Rules/") and p.suffix==".cs":
        stem = p.stem
        category = "Bots" if stem.startswith("Bot") else "Combat" if stem in {"RifleHandling","Loadouts","UtilitySystem","AimHistory","CombatEvents"} else "Sessions" if stem in {"MatchRoster","RoleQueue","InputStream","P1CommandStream"} else None
        if category:
            move(current, "Assets/Scripts/Match/"+category+"/"+p.name)

paths = {path: destination(path) for path in asset_paths if path != destination(path)}
token_pattern = re.compile(r"\b(?:"+"|".join(re.escape(s) for s in sorted(types, key=len, reverse=True))+r")\b")
def rewrite(text):
    # Full asset paths first, then shared directory literals and identifiers.
    for old, new in sorted(paths.items(), key=lambda pair: -len(pair[0])):
        text = text.replace(old, new)
    for entry in moves:
        text = text.replace(entry["From"], entry["To"])
    for old, new in sorted(assemblies.items(), key=lambda pair: -len(pair[0])):
        text = text.replace(old, new)
    text = token_pattern.sub(lambda m: types[m.group()], text)
    for old, new in {"M7MainMenu":"MainMenu", "M7DuelArena":"DuelArena", "M7TwoVsTwoArena":"TwoVsTwoArena", "M7_P2ArmsViewmodel":"P2ArmsViewmodel", "M7PlayerBody":"PlayerBody", "M7PlayerDirector":"PlayerDirector", "-m3-":"-match-", "-m4-":"-queue-", "-m7-":"-client-", "-m2-":"-prediction-"}.items():
        text = text.replace(old,new)
    for old, new in MODULES.items():
        text = text.replace("Be My Arms/"+old+"/", "Be My Arms/"+new+"/")
        text = text.replace("["+old, "["+new)
        text = text.replace(old+"_", new+"_")
    text = text.replace("Builds/M7/", "Builds/Windows/").replace("Builds/Pass2Redo/", "Builds/Windows/")
    text = re.sub(r"\bM(?:0[5]?|[1-7])\b",lambda m:MODULES.get(m.group(),"NetcodeComparison"),text)
    return text

rewrites = []
for path, text in texts.items():
    old_path = path.relative_to(ROOT).as_posix()
    transformed = rewrite(text)
    old_namespace = re.search(r"namespace\s+([\w.]+)", text)
    # Migration metadata for serialized component / asset types. .meta GUIDs remain unchanged.
    if old_namespace and "/Editor/" not in old_path and "/Tests/" not in old_path:
        namespace = old_namespace.group(1)
        assembly = next((a for a in sorted(assemblies,key=len,reverse=True) if namespace==a or namespace.startswith(a+".")), None)
        if assembly:
            for match in re.finditer(r"(?m)^(\s*)(public\s+(?:(?:sealed|abstract)\s+)?class\s+(\w+)\s*:\s*(?:MonoBehaviour|NetworkBehaviour|ScriptableObject)\b)",text):
                old_type = match.group(3)
                new_declaration = rewrite(match.group(2))
                attribute = f'[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "{namespace}", "{assembly}", "{old_type}")]'
                transformed = transformed.replace(new_declaration, attribute+"\n"+match.group(1)+new_declaration,1)
    staged = OUT / "rewrites" / destination(old_path)
    staged.parent.mkdir(parents=True,exist_ok=True)
    staged.write_text(transformed,encoding="utf-8")
    rewrites.append({"Path":destination(old_path), "Source":staged.relative_to(ROOT).as_posix()})
for path in (ROOT/"Assets").rglob("*.asmdef"):
    original = path.relative_to(ROOT).as_posix()
    staged = OUT/"rewrites"/destination(original)
    staged.parent.mkdir(parents=True,exist_ok=True)
    staged.write_text(rewrite(path.read_text(encoding="utf-8-sig")),encoding="utf-8")
    rewrites.append({"Path":destination(original),"Source":staged.relative_to(ROOT).as_posix()})

# Resources keys and serialized scene-name fields need the same rename as source strings.
strings = dict(paths)
strings.update({Path(old).stem:Path(new).stem for old,new in paths.items() if Path(old).suffix not in (".cs",".asmdef")})
strings.update({"Assets/Art/Characters/Pass2Redo":"Assets/Art/Characters/SharedRig", "Assets/Art/Characters/Pass2":"Assets/Art/Characters/Samples/SegmentRig"})
manifest = {"Moves":moves, "Rewrites":rewrites, "Paths":paths, "Strings":strings, "Types":types, "Assemblies":assemblies}
(OUT/"manifest.json").write_text(json.dumps(manifest,indent=2),encoding="utf-8")
print(f"Prepared {len(moves)} GUID-preserving moves and {len(rewrites)} source/asmdef rewrites")
