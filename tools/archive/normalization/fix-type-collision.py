"""Complete the WeaponType rename while preserving serialized/network field names."""
from pathlib import Path
import json
import re

root=Path(__file__).resolve().parents[2]
for directory in (root/"Assets/Scripts",root/"Assets/Tests"):
    for path in directory.rglob("*.cs"):
        text=path.read_text(encoding="utf-8-sig")
        updated=re.sub(r"\bWeaponId\b(?!\s*(?:=|\.Value))","WeaponType",text)
        if updated!=text:path.write_text(updated,encoding="utf-8")
manifest=root/"Builds/Normalization/manifest.json"
data=json.loads(manifest.read_text(encoding="utf-8"))
data["Types"]["M3WeaponId"]="WeaponType"
manifest.write_text(json.dumps(data,indent=2),encoding="utf-8")
