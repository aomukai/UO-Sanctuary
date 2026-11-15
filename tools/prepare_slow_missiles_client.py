#!/usr/bin/env python3
"""Reuse accepted44patch client;only shared self-target classification changes."""
import json,shutil
from pathlib import Path
import skill_checkpoint_evidence as e
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/slow-missiles-learned';PARENT=ROOT/'.local/class-skills/eldritch-blast-cast'
def prepare():
    old=e.load(PARENT/'client-inputs-1.json');source=PARENT/'client-root/upstream/ClassicUO'
    for n,h in old['patchHashes'].items():assert e.sha(ROOT/n)==h,n
    for n,h in old['clientInputs'].items():assert e.sha(source/n)==h,n
    changed=[n for n,h in old['sharedInputs'].items()if e.sha(ROOT/n)!=h]
    assert changed==['shared/ClassAbilities.cs']
    for n,h in old['sharedInputs'].items():assert e.sha(PARENT/'client-root'/n)==h,n
    before=(PARENT/'client-root/shared/ClassAbilities.cs').read_text();after=(ROOT/'shared/ClassAbilities.cs').read_text()
    assert after==before.replace('or CombatAbility.InnerSight or CombatAbility.BurstOfSpeed','or CombatAbility.InnerSight or CombatAbility.SlowMissiles or CombatAbility.BurstOfSpeed',1)
    assert not(STAGE/'client-root').exists();STAGE.mkdir(parents=True,exist_ok=True)
    shutil.copytree(PARENT/'client-root',STAGE/'client-root',ignore=shutil.ignore_patterns('.git'))
    shutil.copy2(ROOT/changed[0],STAGE/'client-root'/changed[0])
    manifest={**old,'baseReceipt':'tools/reference/eldritch-blast-cast-client-evidence.json','baseReceiptSha256':e.sha(ROOT/'tools/reference/eldritch-blast-cast-client-evidence.json'),
        'addedPatches':[],'intentionalSharedChanges':changed,'sharedInputs':{n:e.sha(ROOT/n)for n in old['sharedInputs']}}
    (STAGE/'client-inputs-1.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print('Accepted44patch client reused;only shared Slow Missiles self-target classification changes.')
if __name__=='__main__':prepare()
