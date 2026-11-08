#!/usr/bin/env python3
"""Authenticate persistent AI control in original demon controllers."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-control'
PARENT='a4286dc1885dcdbbb27449c78227e3b846d8ff13'

def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text()) if path.suffix=='.json' else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-owner-iterator-source-evidence.json')
    assert source['referenceVersion']=='3.3.93847' and source['sourceBytes']==832 and source['sourceContexts']==33
    assert source['assertionContexts']==7 and source['nullGameDereferenceContexts']==1 and source['growthContexts']==1
    assert source['sourceCodeExecutedInOfflineEmulator'] and not source['processMemoryRead'] and not source['targetProcessCodeExecuted']
    for name,digest in source['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    initial=pin(ROOT/'tools/reference/pet-initialization-source-evidence.json')
    root=pin(ROOT/'tools/reference/generic-pet-decisions-source-evidence.json')
    assert initial['composedBytes']==2522 and initial['sourceContexts']==29 and root['rootBytes']==781
    for receipt in [initial,root]:
        assert receipt['referenceVersion']=='3.3.93847' and not receipt['processMemoryRead']
        for name,digest in receipt['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    for name in ['GenericPetInitialization.cs','GenericPetDecisions.cs','GenericPetOwnerIterator.cs']:
        path='src/Sanctuary.Content/'+name
        assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    born=pin(STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-1.json')
    snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for name,digest in born['sourceInputs'].items():assert e.sha(ROOT/name)==digest,name
    for kind in ['content','integration']:
        path=STAGE/kind/f'Sanctuary.{kind.title()}.dll';assert e.sha(path)==born['artifacts']['Assemblies/'+path.name]
        log=pin(STAGE/f'{kind}-build-1.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
                      'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
                      'pet-command-target-check':1,'pet-command-target-reload-check':2})
    log=e.cohort(STAGE,expected,12);assert log.count('Owner iterator:33complete-current controls/832bytes')==1
    pin(STAGE/'birth-native-checks.log');saved=pin(STAGE/'pet-command-target-fixtures.json')
    assert saved['Generation']==2 and len(saved['Entries'])==3 and len(saved['Owners'])==2
    assert log.count('AI control:explicit three-word registration')==3
    for i,entry in enumerate(saved['Entries']):
        control=entry['Command']['Control']
        assert control==dict(Value0=-2147483648+i+3,Value1=-1-i-21,Value2=(2147483647-i)^0x5a5a5a5a)
    installed=pin(ROOT/'tools/reference/creeper-frame-installation-evidence.json')
    assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert e.sha(Path(installed['runtimeRoot'])/name)==digest,name
    protected=pin(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for kind,relative,count in [('saves','Saves',38),('configuration','Configuration',20)]:
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative
        assert len(protected[kind])==count
        assert {str(p.relative_to(folder)):e.sha(p)for p in folder.rglob('*')if p.is_file()}==protected[kind]
    client=pin(ROOT/'tools/reference/ring-cast-client-evidence.json');clientInputs=pin(ROOT/client['inputManifest']['path'])
    assert e.sha(ROOT/client['inputManifest']['path'])==client['inputManifest']['sha256']
    assert client['patches']==len(clientInputs['patchHashes'])==43
    for group in ['patchHashes','sharedInputs']:
        for name,digest in clientInputs[group].items():assert e.sha(ROOT/name)==digest,name
    for name in ['docs/pet-control-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
                 'docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_control.py']:
        pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],learnedEnabled=False,automaticAiEnabled=False,
                retainedInitializerBytes=2522,retainedRootBytes=781,retainedIteratorContexts=33,newProcessReads=0,oldLargeSourceMatricesRerun=0,
                nativeMarkers=12,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,
                artifacts=born['artifacts'],preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
                verified=['Existing registered original demon controller now retains exactly three signed AI-control words separately from command/temporary target. Unknown legacy entries remain null;qualified explicit registration is one-time. No birth defaults invented.',
                          'Actual original demon initializer snapshots registered control. Existing current root publishes updated words only after finishing;frame edits do not alias persistent state. Existing initializer/root/iterator policies are text-identical to parent checkpoint.',
                          'Original three-species Warlock/Witch birth snapshots,fractional wounds,typed commands and control words survive two actual reloads. Three source-root executions per actor produce exactly the expected signed boundary values. Legacy seven-field JSON omission/roundtrip and early refusal are checked in memory.'],
                limits=['Birth/default control producer is still unknown and must supply all three words explicitly. Legacy-format refusal/roundtrip is in memory;actual native reloads cover supplied control entries. No claim of deriving authentic startup words.',
                        'Actual native player-pet store and timed event/scheduling producers,remaining search/path/movement/combat/actions and learned summons/DeathMark admission remain unfinished. BindControlPersistence overrides only the explicit root publication service.',
                        'Registered control publication re-resolves actor and refuses missing/deleted/ineligible controllers;that guard does not certify full concurrency/reentrancy or atomic rollback of unrelated root services.',
                        'No new assets/deployment;effects/audio and talent/hotkey icons remain separate.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-control-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('AI control:registered words->initializer->root->save/load;12markers/two reloads;213handlers;AI gated.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
