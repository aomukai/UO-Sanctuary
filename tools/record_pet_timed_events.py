#!/usr/bin/env python3
"""Authenticate timed monster event production/removal and root binding."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-timed-events'
PARENT='219b02042685d34ca292a8b5641178e56310473c'

def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text()) if path.suffix=='.json' else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-timed-events-source-evidence.json')
    assert source['referenceVersion']=='3.3.93847' and source['machineCodeBytes']==1796 and source['sourceContexts']==31
    assert source['sourceCodeExecutedInOfflineEmulator'] and not source['processMemoryRead'] and not source['targetProcessCodeExecuted']
    for name,digest in source['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    captures=[pin(ROOT/'.local/class-skills/warlock-runtime-reference'/name)for name in
              ['callees-pet-ai-schedule-20261109.json','callees-pet-ai-event-20261109.json','callees-pet-ai-event-node-20261109.json']]
    assert sum(c['bytesRead']for c in captures)==4136
    for capture in captures:
        assert capture['directCallTargetsVerified'] and all(row['allUnwindFragmentsCaptured']for row in capture['records'])
        for name,digest in capture['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    for name in ['GenericPetInitialization.cs','GenericPetDecisions.cs','DeathMarkWorld.cs']:
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
                      'pet-command-target-check':1,'pet-command-target-reload-check':2,'pet-timed-events-check':3})
    log=e.cohort(STAGE,expected,15);assert log.count('Timed events:31current sequences/1796retained bytes')==3
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
    for name in ['docs/pet-timed-events-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
                 'docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_timed_events.py']:
        pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],learnedEnabled=False,automaticAiEnabled=False,
                currentTimedEventBytes=1796,sourceSequences=31,freshCaptureBytesRead=4136,oldLargeSourceMatricesRerun=0,
                nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,
                artifacts=born['artifacts'],preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
                verified=['Complete retained idle/remover and captured wrapper/scheduler/node factory execute31focused sequences with available source pool and declared null-callback validation. Native C# matches metadata,unit-list and bucket ordering,reuse,processing flags,neutral/deferred service order,selector refusals and post-service status.',
                          'Prepared event-store entries now supply root event types and removal for original three-species demons. Root removes both AI events while retaining its other event,finishes typed commands and publishes existing persistent controls. Original birth/wounds/commands/control values survive two native reloads;the event store is recreated,not persisted.',
                          'Existing initializer/root/DeathMark controller source files are text-identical to parent;no replacement AI or automatic dispatcher is introduced. Installed artifacts,saves,configuration and current client remain unchanged.'],
                limits=['Monster/null-callback events over explicit ordinary frame requests only. Non-null callback validation is an opaque declared service;raw pool-growth path,other unit groups,untimed sentinel,global dispatch cursor and dispatch completion are not certified.',
                        'No automatic tick/due-event execution,event persistence or host frame projection. Special stat-root inputs and post-service status remain explicit;no gameplay meaning or live default is inferred. Binding snapshots explicit root entries;the supported initializer does not mutate timed events.',
                        'Live player-pet store,birth/default AI-control values,remaining search/path/movement/combat/actions and learned summon/DeathMark admission remain unfinished. No class certificate or new assets.'],
                diagnostics=['Oracle review replaced free-list-head-derived snapshot count with actual factory-return high-water tracking before native comparison. Earlier script retained in probe-diagnostics-1;no native/build failure.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-timed-events-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Timed events:31source sequences,15native markers/two reloads;213handlers;dispatch and AI gated.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
