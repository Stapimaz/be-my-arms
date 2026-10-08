"""Check GUID/dependency preservation against the pre-migration Editor audit."""
from pathlib import Path
import json

root=Path(__file__).resolve().parents[2]
out=root/"Builds/Normalization"
before=json.loads((out/"references-before.json").read_text(encoding="utf-8-sig"))
after=json.loads((out/"reference-audit.json").read_text(encoding="utf-8-sig"))
lost={guid:path for guid,path in before["GuidPaths"].items() if guid not in after["GuidPaths"]}
old_assets={a["Guid"]:a for a in before["Assets"]}
new_assets={a["Guid"]:a for a in after["Assets"]}
dependency_losses=[]
dependency_additions=[]
removed_legacy_ssao={
    "0849e84e3d62649e8882e9d6f056a017", "3302065f671a8450b82c9ddf07426f3a",
    "36f118343fc974119bee3d09e2111500", "4b7b083e6b6734e8bb2838b0b50a0bc8",
    "56a77a3e8d64f47b6afe9e3c95cb57d5", "c06cc21c692f94f5fb5206247191eeee",
    "cb76dd40fa7654f9587f6a344f125c9a", "e32226222ff144b24bf3a5a451de54bc",
}
reviewed_reserialization=[]
for guid,asset in old_assets.items():
    current=new_assets[guid]
    missing=set(asset["Dependencies"])-set(current["Dependencies"])
    added=set(current["Dependencies"])-set(asset["Dependencies"])
    if current["Path"]=="Assets/Settings/PC_Renderer.asset":
        expected=missing & removed_legacy_ssao
        if expected:reviewed_reserialization.append({"Path":current["Path"],"Guids":sorted(expected),
            "Reason":"URP 17.4 no longer serializes SSAO shader/noise fields; resources come from GraphicsSettings."})
        missing-=expected
    if missing:dependency_losses.append({"Path":current["Path"],"Guids":sorted(missing)})
    if added:dependency_additions.append({"Path":current["Path"],"Guids":sorted(added)})
legacy=set(before["Errors"])
new_errors=[error for error in after["Errors"] if error not in legacy]
report={"OriginalGuids":len(before["GuidPaths"]),"LostGuids":lost,"DependencyLosses":dependency_losses,
        "ReviewedLegacyReserialization":reviewed_reserialization,
        "DependencyAdditions":dependency_additions,"NewReferenceErrors":new_errors,
        "RetainedPreExistingSerializationWarnings":after["Errors"]}
(out/"migration-verification.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
print(json.dumps(report,indent=2))
if lost or dependency_losses or new_errors:raise SystemExit(1)
