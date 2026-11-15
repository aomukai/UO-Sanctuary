#!/usr/bin/env python3
"""Stage the exact accepted client plus the single Bind channel whitelist patch."""
import hashlib,json,shutil,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/bind-demon-client';PARENT=ROOT/'.local/class-skills/hex-siphon-cast'
PATCH='patches/ClassicUO/0044-bind-demon-held-input.patch'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def prepare():
    old=json.loads((PARENT/'client-inputs-1.json').read_text());source=PARENT/'client-root/upstream/ClassicUO';assert len(old['patchHashes'])==43
    for name,h in old['patchHashes'].items():assert sha(ROOT/name)==h,name
    for name,h in old['clientInputs'].items():assert sha(source/name)==h,name
    for name,h in old['sharedInputs'].items():assert sha(ROOT/name)==sha(PARENT/'client-root'/name)==h,name
    assert not(STAGE/'client-root').exists();STAGE.mkdir(parents=True,exist_ok=True)
    shutil.copytree(PARENT/'client-root',STAGE/'client-root',ignore=shutil.ignore_patterns('.git'))
    client=STAGE/'client-root/upstream/ClassicUO';subprocess.run(['git','apply','--check',str(ROOT/PATCH)],cwd=client,check=True);subprocess.run(['git','apply',str(ROOT/PATCH)],cwd=client,check=True)
    changed=[name for name,h in old['clientInputs'].items()if sha(client/name)!=h];assert changed==['src/ClassicUO.Client/Game/ClassCombatChannelClient.cs']
    manifest=dict(old,baseReceipt='tools/reference/hex-siphon-cast-client-evidence.json',baseReceiptSha256=sha(ROOT/'tools/reference/hex-siphon-cast-client-evidence.json'),patches=44,
        addedPatches=[PATCH],intentionalSharedChanges=[],clientInputs={n:sha(client/n)for n in old['clientInputs']},patchHashes={**old['patchHashes'],PATCH:sha(ROOT/PATCH)})
    (STAGE/'client-inputs-1.json').write_text(json.dumps(manifest,indent=2)+'\n');print('Isolated44patch client prepared;only Bind channel whitelist differs;shared sources unchanged.')
if __name__=='__main__':prepare()
