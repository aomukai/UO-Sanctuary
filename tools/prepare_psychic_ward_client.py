#!/usr/bin/env python3
"""Reuse the accepted43patch client;change only linked Ward self-target metadata."""
import hashlib,json,shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/psychic-ward-cast';PARENT=ROOT/'.local/class-skills/ring-cast'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def prepare():
    old=json.loads((PARENT/'client-inputs-2.json').read_text());assert len(old['patchHashes'])==43
    source=PARENT/'client-root/upstream/ClassicUO'
    for name,h in old['patchHashes'].items():assert sha(ROOT/name)==h,name
    for name,h in old['clientInputs'].items():assert sha(source/name)==h,name
    for name,h in old['sharedInputs'].items():assert sha(PARENT/'client-root'/name)==h,name
    assert not(STAGE/'client-root').exists();shutil.copytree(PARENT/'client-root',STAGE/'client-root',ignore=shutil.ignore_patterns('.git'))
    shared={str(p.relative_to(ROOT)):sha(p)for p in sorted((ROOT/'shared').glob('*.cs'))}
    assert [name for name,h in old['sharedInputs'].items()if shared[name]!=h]==['shared/ClassAbilities.cs']
    shutil.copy2(ROOT/'shared/ClassAbilities.cs',STAGE/'client-root/shared/ClassAbilities.cs')
    manifest=dict(old,baseReceipt='tools/reference/ring-cast-client-evidence.json',baseReceiptSha256=sha(ROOT/'tools/reference/ring-cast-client-evidence.json'),sharedInputs=shared,intentionalSharedChanges=['shared/ClassAbilities.cs'])
    (STAGE/'client-inputs-1.json').write_text(json.dumps(manifest,indent=2)+'\n');print('Isolated43patch client staged;only linked Ward self-target metadata differs.')
if __name__=='__main__':prepare()
