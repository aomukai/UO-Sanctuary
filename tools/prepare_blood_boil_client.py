#!/usr/bin/env python3
"""Reuse qualified44patch client;only Blood Boil self-target metadata changes."""
import json,shutil,tarfile
from pathlib import Path
import skill_checkpoint_evidence as e
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/blood-boil-learned';PARENT=ROOT/'.local/class-skills/slow-missiles-learned'
def prepare():
    old=e.load(PARENT/'client-inputs-1.json');source=PARENT/'client-root'
    for n,h in old['patchHashes'].items():assert e.sha(ROOT/n)==h,n
    for n,h in old['clientInputs'].items():assert e.sha(source/'upstream/ClassicUO'/n)==h,n
    for n,h in old['sharedInputs'].items():assert e.sha(source/n)==h,n
    changed=[n for n,h in old['sharedInputs'].items() if e.sha(ROOT/n)!=h];assert changed==['shared/ClassAbilities.cs']
    before=(source/changed[0]).read_text();after=(ROOT/changed[0]).read_text()
    assert after==before.replace('SelfTarget(CombatAbility value) => value is','SelfTarget(CombatAbility value) => value is CombatAbility.BloodBoil or',1)
    destination=Path('/dev/shm/sanctuary-blood-boil-client-revive-respec');assert not destination.exists()
    shutil.copytree(source,destination,ignore=shutil.ignore_patterns('.git'))
    (STAGE/'client-root').symlink_to(destination,target_is_directory=True);shutil.copy2(ROOT/changed[0],destination/changed[0])
    manifest={**old,'baseReceipt':'tools/reference/slow-missiles-learned-client-evidence.json','baseReceiptSha256':e.sha(ROOT/'tools/reference/slow-missiles-learned-client-evidence.json'),'addedPatches':[],'intentionalSharedChanges':changed,'sharedInputs':{n:e.sha(ROOT/n) for n in old['sharedInputs']}}
    (STAGE/'client-inputs-1.json').write_text(json.dumps(manifest,indent=2)+'\n')
    archive=STAGE/'content/../client-source-1.tar.gz'
    with tarfile.open(archive,'w:gz',dereference=True) as t:t.add(destination,arcname='client-root')
    (STAGE/'client-source-1.tar.gz').symlink_to(archive.resolve())
    print('Qualified44patch client reused;one shared SelfTarget change;RAM source archived durably.')
if __name__=='__main__':prepare()
