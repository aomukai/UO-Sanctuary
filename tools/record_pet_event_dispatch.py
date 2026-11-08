#!/usr/bin/env python3
"""Authenticate current monster timed dispatch/router and original root cancellation."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-event-dispatch'
PARENT='668c29a154a8a47be522c943434a1d7b7008c63e'

def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text()) if path.suffix=='.json' else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-event-dispatch-source-evidence.json')
    assert source['referenceVersion']=='3.3.93847' and source['newDispatcherBytes']==722 and source['newRouterBytes']==324 and source['totalLoadedBytes']==2842 and source['sourceSequences']==36 and source['callbackTrampolineCacheInvalidated']
    assert source['sourceCodeExecutedInOfflineEmulator'] and not source['processMemoryRead'] and not source['targetProcessCodeExecuted']
    for name,digest in source['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    captures=[pin(ROOT/'.local/class-skills/warlock-runtime-reference'/name)for name in
              ['pet-event-dispatch-candidate-20261109.json','pet-monster-dispatch-candidate-20261109.json','callees-pet-ai-event-router-20261109.json','pet-ai-event-entry-20261109.json']]
    assert sum(c['bytesRead']for c in captures)==6814
    for capture in captures:
        assert all(row['allUnwindFragmentsCaptured']for row in capture['records'])
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
                      'pet-command-target-check':1,'pet-command-target-reload-check':2,'pet-event-dispatch-check':3})
    log=e.cohort(STAGE,expected,15);assert log.count('Event dispatch:36current sequences;722-byte executor+324-byte typed router')==3
    pin(STAGE/'birth-native-checks.log');saved=pin(STAGE/'pet-command-target-fixtures.json')
    assert saved['Generation']==2 and len(saved['Entries'])==3 and len(saved['Owners'])==2
    assert log.count('AI control:explicit three-word registration')==3
    for i,entry in enumerate(saved['Entries']):
        control=entry['Command']['Control']
        assert control==dict(Value0=-2147483648+i+6,Value1=-1-i-42,Value2=2147483647-i)
    assert log.count('Timed event root:original demon callback')==9
    for run,diagnostic in [(1,'Current dispatch/callback order differs'),(2,"The given key '1' was not present")]:
        failed=STAGE/f'failed-native-{run}'
        assert diagnostic in pin(failed/'birth-native-checks.log').decode()
        pin(failed/'birth-inputs.json');pin(failed/'source-oracle/probe_pet_event_dispatch.py')
        pin(failed/'source-oracle/cases.json');pin(failed/'source-oracle/pet-event-dispatch-source-evidence.json')
        for kind in ['content','integration']:
            assert e.sha(failed/kind/f'Sanctuary.{kind.title()}.dll')==e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')
    old=pin(STAGE/'failed-native-1/source-oracle/probe_pet_event_dispatch.py').decode()
    assert 'ctl_remove_cache' not in old and 'ctl_remove_cache' in (ROOT/'tools/probe_pet_event_dispatch.py').read_text()
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
    for name in ['docs/pet-event-dispatch-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
                 'docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_event_dispatch.py']:
        pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],learnedEnabled=False,automaticAiEnabled=False,
                dispatcherBytes=722,routerBytes=324,loadedSourceBytes=2842,sourceSequences=36,freshCaptureBytesRead=6814,capturedAiEntryBytes=1096,oldLargeSourceMatricesRerun=0,
                nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=3,
                artifacts=born['artifacts'],preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
                verified=['Current722-byte executor and324-byte typed router match36native comparisons over explicit monster chains,with actual producer/remover/idle during callback mutations. Includes exact due checks,both processor branches,cursor repair,self/upcoming cancellation,allocation,reuse,all15table slots and raw state-gate boundaries.',
                          'Original three-species demon root callback consumes the same store containing its processing AI event,an upcoming same-bucket AI event and a non-AI entry. Current event removal defers;upcoming removal repairs cursor;completion releases current. Births,wounds,typed commands and six control updates per actor survive two actual reloads.',
                          'Pinned router table links event2 to full1096-byte43b0c0 entry,captured for subsequent integration. Rejected145-byte candidate is queue cleanup,not dispatcher. Existing initializer/root/controller source files are identical to parent;no learned/automatic route bypasses AI entry.'],
                limits=['Supplied monster/null-callback chains with returning declared typed handlers only. Global starting-chain/frame selection,real host tick,event persistence,suspension and full AI-entry semantics remain unqualified. Prepared native root is an explicit fixture service.',
                        'Nested dispatch,throwing handlers,raw global allocator/custom callback validation and other unit groups are outside supported scope. Router raw stat-root metadata remains explicit;no missing native status is silently defaulted.',
                        'Birth/default control producer,live player-pet roster and remaining world/search/path/movement/combat/actions and learned summon/DeathMark admission remain unfinished. No class certificate or new assets.'],
                diagnostics=['Initial probe fixture constructor duplicate keyword fixed before native1. Native1found stale translated callback trampoline;cache is explicitly invalidated after rewriting each trampoline.',
                             'Native2found router fixtures where separate scheduler stat-root conditions prevented allocation. Fixtures now isolate router mask and assert allocated/live chain start. Exact failed oracles/inputs/sources/logs/DLLs archived. Accepted native3 uses identical product and integration binaries.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-event-dispatch-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Event dispatch:36source/native comparisons,15markers/two reloads;213handlers;automatic AI gated.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
