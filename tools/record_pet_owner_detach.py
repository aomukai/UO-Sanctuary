#!/usr/bin/env python3
"""Authenticate current detach/store composition and original-roster checks."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-owner-detach';PARENT='2605ecd9e13a3377eddb30193a0fca291785496b'
def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-owner-detach-source-evidence.json');assert source['sourceContexts']==84 and source['machineCodeBytes']==1371
    for name,h in source['artifacts'].items():assert e.sha(ROOT/name)==h,name
    captures=[pin(ROOT/'.local/class-skills/warlock-runtime-reference'/name)for name in ['callees-pet-detach-store-20261109.json','callees-pet-detach-packed-20261109.json']]
    assert [c['bytesRead']for c in captures]==[1988,768]and all(r['allUnwindFragmentsCaptured']for c in captures for r in c['records'])
    for c in captures:
        for name,h in c['artifacts'].items():assert e.sha(ROOT/name)==h,name
    policy=pin(ROOT/'src/Sanctuary.Content/GenericPetOwnerDetach.cs').decode();assert 'found.Flags|=1'in policy and 'GenericPetOwnedNode View()'in policy and 'Detach35c970='in policy
    entry='src/Sanctuary.Content/GenericPetAiEntry.cs';before=subprocess.check_output(['git','show',PARENT+':'+entry],cwd=ROOT).decode()
    assert pin(ROOT/entry).decode()==before.replace('unit is{Kind:1,HasData:true}&&(unit.Flags30&4)!=0?unit.Snapshot8c:game.Snapshot(stackTail)','GenericPetPackedContext.Read(game,unit,stackTail)')
    for name in ['GenericPetOwnerIterator.cs','GenericPetOwnerLoss.cs','GenericPetLinkedUnits.cs','GenericPetWorldIdentity.cs','GenericPetTimedEvents.cs','GenericPetUninterruptible.cs','WarlockCombat.cs','DeathMarkWorld.cs','GenericPetStartupProposal.cs']:
        path='src/Sanctuary.Content/'+name;assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    assert 'internal const bool Approved=false;'in (ROOT/'src/Sanctuary.Content/GenericPetStartupProposal.cs').read_text()
    pre=pin(STAGE/'source-snapshot-2.json')['inputs'];now=pin(STAGE/'source-snapshot-3.json')['inputs']
    assert pre['projectInputs']==now['projectInputs']and pre['copiedData']==now['copiedData']
    assert [n for n in pre['sourceInputs']if pre['sourceInputs'][n]!=now['sourceInputs'][n]]==['tests/Sanctuary.Integration/GenericPetOwnerDetachChecks.cs']
    for name in ['GenericPetOwnerDetach.cs','GenericPetOwnerDetachChecks.cs','source-snapshot-1.json','content-build-1.log','integration-build-1.log','native-run-1.log','birth-native-checks.log','birth-inputs.json','pet-owner-detach-native-evidence.json','Sanctuary.Content.dll','Sanctuary.Integration.dll']:pin(STAGE/'pre-review-1'/name)
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-3.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ['content','integration']:
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
        log=pin(STAGE/f'{kind}-build-2.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'pet-owner-detach-check':3,'pet-command-target-check':1,'pet-command-target-reload-check':2,'pet-native-status-check':1,'pet-native-status-reload-check':2})
    log=e.cohort(STAGE,expected,18)
    assert log.count('Owner iterator binding:callback changes player store')==3
    assert log.count('Owner detach:84current contexts/1371bytes')==3 and log.count('Owner detach original:actual original GUIDs/typed registry share full-width node backing')==9
    for message in ['Linked units original:actual NPC/player direct masters','Uninterruptible original:actual saved mode provider drives scheduling guard','Native pet status original:freeze suppresses AI delivery']:
        assert log.count(message)==9
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    statuses=pin(STAGE/'pet-native-status-fixtures.json');original=pin(STAGE/'pet-command-target-fixtures.json')
    assert statuses['Generation']==original['Generation']==2 and len(statuses['Entries'])==len(original['Entries'])==3
    for i,entry in enumerate(statuses['Entries']):assert entry['Command']['Control']==dict(Value0=-2147483648+i+3,Value1=-1-i-21,Value2=(2147483647-i)^0x5a5a5a5a)
    for i,entry in enumerate(original['Entries']):assert entry['Command']['Control']==dict(Value0=-2147483648+i+6,Value1=-1-i-42,Value2=2147483647-i)
    protected=e.protection('tools/reference/ring-cast-client-evidence.json',43)
    for name in ['tools/skill_checkpoint_evidence.py','tools/reference/creeper-frame-installation-evidence.json','.local/class-skills/creeper-frame-installation.json','tools/reference/ring-cast-client-evidence.json']:
        value=pin(ROOT/name)
        if name.endswith('ring-cast-client-evidence.json'):pin(ROOT/value['inputManifest']['path'])
    for name in ['docs/pet-owner-detach-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_owner_detach.py']:pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],reviewOnly=True,automaticAiEnabled=False,learnedEnabled=False,sourceContexts=84,machineCodeBytes=1371,newReferenceReadBytes=2756,oldLargeOfflineMatricesRerun=0,
        originalRosterChecks=9,newTimers=0,newSavedFormats=0,nativeMarkers=18,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=2,sourceSnapshot=3,nativeRun=2,artifacts=born['artifacts'],preservation={**protected,'installed':False},
        verified=['Current detach/type/data/bucket/packed helpers compose1371bytes in84cases. Full flags,first-match inactive lookup,selector/count guards,type7exception,changed GUID/store data,packed snapshot/fallback/tail and ordered status/client/broadcast calls match. Null/fatal boundaries refuse without a return.',
            'Mutable full-width source nodes feed existing iterator views;actual original GUIDs/typed world registry enumerate before inactive marking and exclude only selected original afterward. First-match lookup still finds that inactive entry;upper flags remain. Nine original checks preserve actor/master/wounds/mana/controller through two reloads.',
            'AI entry reuses the captured packed getter;existing entry/mode/linked/freeze/stun and control/command reload cohorts pass. No second traversal algorithm,timer or save format.'],
        diagnostics=['First C1/I1/snapshot1/native1passed. Review then made iterator backing an explicit captured handle,matching retained source35e6d0 instead of rereading changed player store. Original sources/logs/snapshot/inputs/DLLs/receipt retainedpre-review-1. Focused callback-mutation control added:test-onlysnapshot2to3diff;C2reusedforI2. AcceptedC2/I2/snapshot3/native2.'],
        limits=['Store population/order,external count/status/client/broadcast services and native death effects remain mandatory explicit bindings. Structured notice is not sent as a D2 packet to a UO client.',
            'Unwritten packed tail remains explicit;no meaning inferred for unrelated context bytes. Native component uses a prepared roster over actual identities,not a qualified production store/birth/persistence producer.',
            'No automatic pump/startup admission,save migration,learned skill,class certificate,deployment or new assets. Startup proposal still pending;protected paths not retried.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-owner-detach-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Pet owner detach:84current contexts/1371bytes,9original roster checks,18markers/two reloads;production AI gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
