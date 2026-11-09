#!/usr/bin/env python3
"""Qualify do158 ordering without treating protected helpers as implemented."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bind-demon-cast';PARENT='813df9fd5c5f0d183d1aaef16a7e13d071d1ce73'
def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_bytes()
    source=pin(ROOT/'tools/reference/bind-demon-cast-source-evidence.json');assert(source['sourceContexts'],source['machineCodeBytes'],source['skillId'],source['doIndex'])==(69,709,382,158)and source['dispatcherIndexLinked']
    for name,h in source['artifacts'].items():assert e.sha(ROOT/name)==h,name
    capture=pin(ROOT/'.local/class-skills/warlock-runtime-reference/callees-bind-demon-core-20261109.json')
    assert capture['bytesRead']==560 and capture['directCallTargetsVerified'] and [r['bytes']for r in capture['records']]==[356,1943]
    assert all(not r['allUnwindFragmentsCaptured']and not any(f['captured']for f in r['fragments'])for r in capture['records'])
    for name,h in capture['artifacts'].items():assert e.sha(ROOT/name)==h,name
    parent=pin(ROOT/capture['parent'])
    for name,h in parent['artifacts'].items():assert e.sha(ROOT/name)==h,name
    for name in ['WarlockCombat.cs','GenericPetStartupProposal.cs','GenericPetTraversal.cs','GenericPetAiEntry.cs','GenericPetOwnerLoss.cs','GenericPetOwnerRemove.cs','GenericPetOwnerDetach.cs','GenericPetWorldIdentity.cs','DeathMarkWorld.cs','RingOfFireWorld.cs']:
        path='src/Sanctuary.Content/'+name;assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    assert 'internal const bool Approved=false;'in (ROOT/'src/Sanctuary.Content/GenericPetStartupProposal.cs').read_text()
    prior=pin(STAGE/'source-snapshot-1.json')['inputs'];current=pin(STAGE/'source-snapshot-2.json')['inputs']
    assert prior['sourceInputs']==current['sourceInputs']and prior['projectInputs']==current['projectInputs']
    assert [n for n in prior['copiedData']if prior['copiedData'][n]!=current['copiedData'][n]]==['Data/Sanctuary/Balance/class-skill-status.json']
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ['content','integration']:
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
        log=pin(STAGE/f'{kind}-build-1.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'bind-demon-cast-check':3})
    log=e.cohort(STAGE,expected,12);assert log.count('Bind Demon:69current contexts/709bytes')==3
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    report=pin(ROOT/'data/Sanctuary/Balance/class-skill-status.json');rows=report['skills'];bind=next(r for r in rows if r['talentId']=='warlock.binddemon');assert bind['implementation']=='blocked'and bind['verification']=='partial-native-coverage'and not bind['complete']
    assert len([r for r in rows if r['implementation']=='handler-present'])==213
    protected=e.protection('tools/reference/ring-cast-client-evidence.json',43)
    for name in ['tools/skill_checkpoint_evidence.py','tools/reference/creeper-frame-installation-evidence.json','.local/class-skills/creeper-frame-installation.json','tools/reference/ring-cast-client-evidence.json']:
        value=pin(ROOT/name)
        if name.endswith('ring-cast-client-evidence.json'):pin(ROOT/value['inputManifest']['path'])
    for name in ['docs/bind-demon-cast-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tools/build_skill_roadmap.py','tests/check_bind_demon_cast.py']:pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],reviewOnly=True,learnedEnabled=False,sourceContexts=69,machineCodeBytes=709,newReferenceReadBytes=560,protectedHelperBytes=2299,oldLargeOfflineMatricesRerun=0,
        nativeMarkers=12,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=2,nativeRun=1,newTimers=0,newSavedFormats=0,artifacts=born['artifacts'],preservation={**protected,'installed':False},
        verified=['Complete dispatcher-linked do158 agrees with69declared-service source/native cases. Current target refresh,state checks,AI branches,cached counter,linked event release,typed owner lookup,current caster context and source flags retained.',
            'Eligibility refusal returns1before later effects;binding refusal returns1after preceding mutations. Neither is silently treated as rollback or a payment rule. Two helpers were checked once and remain protected;only560validation anchor bytes read.',
            'Twelve native component/baseline checks pass across three boots/two reloads/three saves without allocating native Bind actors or opening learned route. Content inputs remain identical across source snapshots;only copied roadmap report changes.'],
        limits=['Eligibility44b240,binding44b3b0,transfer/AI-stop/alignment/allocator/room services are supplied;actual native bind/controller/passive/cap lifecycle not certified.',
            'Start64,channel/missile timing,input/payment and protected helper behavior remain unresolved. No guessed Conversion/Revive substitute,learned admission,class certificate,save migration,deployment or new art. Normal demon startup approval remains pending.'],inputs=inputs)
    path=ROOT/'tools/reference/bind-demon-cast-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Bind Demon:69current contexts/709bytes,12native markers/two reloads;protected gate/binding explicit,no learned admission.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
