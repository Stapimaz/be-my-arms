"""Update the maintained lifecycle regression to the normalized launch/type interfaces."""
from pathlib import Path
import json
import re

root=Path(__file__).resolve().parents[2]
manifest=json.loads((root/"Builds/Normalization/manifest.json").read_text(encoding="utf-8"))
manifest["Types"]["M4MatchSlot"]="MatchmakingSlot"
(root/"Builds/Normalization/manifest.json").write_text(json.dumps(manifest,indent=2),encoding="utf-8")
path=root/"tools/qa/test-session-lifecycle.ps1"
text=path.read_text(encoding="utf-8-sig")
for old,new in sorted(manifest["Assemblies"].items(),key=lambda pair:-len(pair[0])):
    text=text.replace(old,new)
for old,new in sorted(manifest["Types"].items(),key=lambda pair:-len(pair[0])):
    text=re.sub(r"\b"+re.escape(old)+r"\b",new,text)
for old,new in sorted(manifest["Paths"].items(),key=lambda pair:-len(pair[0])):
    text=text.replace(old,new)
for old,new in {"M7MainMenu":"MainMenu","M7DuelArena":"DuelArena","M7TwoVsTwoArena":"TwoVsTwoArena","M7_":"Client_","-m3-":"-match-","-m4-":"-queue-","-m7-":"-client-","Builds/Pass1":"Builds/Windows","[M7]":"[Client]","m7_private_server.log":"private-server.log"}.items():
    text=text.replace(old,new)
path.write_text(text,encoding="utf-8")
