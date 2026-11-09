#!/usr/bin/env python3
"""Verify composed normal pet-node removal without admitting native effects."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-owner-remove';PARENT='6a7e7b5913d27f879e7582f1775681d13120a201'
def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-owner-remove-source-evidence.json');assert source['sourceContexts']==52 and source['machineCodeBytes']==1949 and source['newRoutineBytes']==578
    for name,h in source['artifacts'].items():assert e.sha(ROOT/name)==h,name
    capture=pin(ROOT/'.local/class-skills/warlock-runtime-reference/callees-pet-detach-effects-20261109.json')
    assert capture['bytesRead']==2136 and [r['bytes']for r in capture['records']]==[578,210]and all(r['allUnwindFragmentsCaptured']for r in capture['records'])
    for name,h in capture['artifacts'].items():assert e.sha(ROOT/name)==h,name
    types=pin(ROOT/'tools/reference/d2r-named-equipment/json/pettype.json');assert types['hireable']['lineNumber']==7
    policy=pin(ROOT/'src/Sanctuary.Content/GenericPetOwnerRemove.cs').decode();assert 'GenericPetOwnerStore.Type(owner,id'in policy and 'RemoveType='in policy and 'bucket.Shared.Count=unchecked'in policy
    for name in ['GenericPetAiEntry.cs','GenericPetOwnerIterator.cs','GenericPetOwnerLoss.cs','GenericPetLinkedUnits.cs','GenericPetWorldIdentity.cs','GenericPetTimedEvents.cs','GenericPetUninterruptible.cs','WarlockCombat.cs','DeathMarkWorld.cs','GenericPetStartupProposal.cs']:
        path='src/Sanctuary.Content/'+name;assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    assert 'internal const bool Approved=false;'in (ROOT/'src/Sanctuary.Content/GenericPetStartupProposal.cs').read_text()
    path='src/Sanctuary.Content/GenericPetOwnerDetach.cs';before=subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT).decode()
    replacement='internal sealed class GenericPetOwnerBucket(int first,int count=0)\n{\n    internal int First=first,Count=count;\n    internal GenericPetOwnerBucket Shared;\n}'
    assert pin(ROOT/path).decode()==before.replace('internal sealed class GenericPetOwnerBucket(int first){internal int First=first;}',replacement)
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-1.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ['content','integration']:
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
        log=pin(STAGE/f'{kind}-build-1.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'pet-owner-remove-check':3,'pet-command-target-check':1,'pet-command-target-reload-check':2,'pet-native-status-check':1,'pet-native-status-reload-check':2})
    log=e.cohort(STAGE,expected,18)
    assert log.count('Owner removal:52current contexts/1949composed bytes')==3 and log.count('Owner removal original:composed normal detach unlinks only actual selected GUID')==9
    for message in ['Owner detach original:actual original GUIDs/typed registry','Linked units original:actual NPC/player direct masters','Uninterruptible original:actual saved mode provider','Native pet status original:freeze suppresses AI delivery']:
        assert log.count(message)==9
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    statuses=pin(STAGE/'pet-native-status-fixtures.json');original=pin(STAGE/'pet-command-target-fixtures.json')
    assert statuses['Generation']==original['Generation']==2 and len(statuses['Entries'])==len(original['Entries'])==3
    for i,entry in enumerate(statuses['Entries']):assert entry['Command']['Control']==dict(Value0=-2147483648+i+3,Value1=-1-i-21,Value2=(2147483647-i)^0x5a5a5a5a)
    for i,entry in enumerate(original['Entries']):assert entry['Command']['Control']==dict(Value0=-2147483648+i+6,Value1=-1-i-42,Value2=2147483647-i)
    protected=e.protection('tools/reference/ring-cast-client-evidence.json',43)
    for name in ['tools/skill_checkpoint_evidence.py','tools/reference/creeper-frame-installation-evidence.json','.local/class-skills/creeper-frame-installation.json','tools/reference/ring-cast-client-evidence.json']:
        value=pin(ROOT/name)
        if name.endswith('ring-cast-client-evidence.json'):pin(ROOT/value['inputManifest']['path'])
    for name in ['docs/pet-owner-remove-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_owner_remove.py']:pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],reviewOnly=True,automaticAiEnabled=False,learnedEnabled=False,sourceContexts=52,machineCodeBytes=1949,newRoutineBytes=578,newReferenceReadBytes=2136,capturedButUnported=dict(hireableCalculationBytes=210),oldLargeOfflineMatricesRerun=0,
        originalRosterChecks=9,newTimers=0,newSavedFormats=0,nativeMarkers=18,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,artifacts=born['artifacts'],preservation={**protected,'installed':False},
        verified=['Complete current578byte callback composes with existing store/detach helpers in52source/native cases. First match unlinks before notification/registry/flag or special branch/free/counters. Shared aliases and unchecked counter/negative assertion chronology match;repeated broadcasts retained.',
            'Pinned type7is hireable. Normal unique-pet outer detach no longer enters inactive/cost branch after real unlink callback. Duplicate nodes preserve source first-match behavior. Hireable210byte calculation retained separately,not invented as a class-pet status effect.',
            'Nine original roster checks compose full normal unlink over actual GUIDs/typed registry. One logical release,local/shared count changes,source flags mask,three expected notifications and no hireable calculation;existing iterator sees remaining originals. Supplied native effects leave actual actor/master/wounds/mana/controller intact through reloads.'],
        limits=['Source registry bucket,logical allocator release,raw special35b450,broadcast/client/hireable services remain declared. Native flags/free/broadcast and actual roster birth/persistence are not admitted by this component check.',
            'No native death/automatic pump/startup admission,save migration,learned skill,class certificate,deployment or new assets. Startup proposal still pending;protected paths not retried.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-owner-remove-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Normal pet removal:52current cases/1949bytes,9original checks,18markers/two reloads;native effects gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
