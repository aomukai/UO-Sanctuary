#!/usr/bin/env python3
"""Bind current six-way follow executor comparisons and retained saved bodies."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/generic-pet-follow-executor'
PARENT='59c3cd0b448ac46e66fb551079eccc23f5b903a0'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE;native=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-build-1.json',STAGE/'birth-inputs.json')
    source=json.loads((ROOT/'tools/reference/generic-pet-follow-executor-source-evidence.json').read_text())
    assert source['cases']==5991 and source['offlineMachineCodeExecuted'] and not source['currentWorldMovementVerified']
    for name,digest in source['artifacts'].items():assert sha(ROOT/name)==digest,name
    fixture=source['fixture'];assert sha(ROOT/fixture['path'])==fixture['sha256']
    assert len(json.loads((ROOT/fixture['path']).read_text()))==5991
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==27
    assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    for name in ('generic-pet-follow-executor','generic-pet-decisions','generic-pet-movement','generic-pet-parameters'):
        assert log.count(name+'-check: passed')==3,name
    assert log.count('GenericPet follow executor:5991complete captured-current cases')==3
    for name in ('goatman-body','goatman-foundation'):
        assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2
    saved=json.loads((STAGE/'goatman-body-fixtures.json').read_text())
    assert len(saved)==2 and {s['Female'] for s in saved}=={False,True}
    for s in saved:assert all(s[k]>0 for k in ('Owner','Body','Helm','Mana','Maximum')) and s['Life']==s['Maximum']-257 and s['State']
    audit=json.loads((STAGE/'audit-inputs.json').read_text())
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {p:h for p,h in native['sourceInputs'].items() if p.endswith('.cs')}==audit['sourceInputs']
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==2
    for name,count in [('named-item-runtime-audit.json',923),('first-loop-affix-audit.json',21)]:
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in current['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    for kind in ('content','integration'):
        text=(STAGE/(kind+'-build-1.log')).read_text();assert all(word in text for word in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    text=(STAGE/'catalogue-checks.log').read_text()
    for word in ('573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert word in text
    previous=json.loads((ROOT/'.local/class-skills/goatman-body/birth-inputs.json').read_text())
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==previous['artifacts'][name]
    assert native['copiedData']==previous['copiedData']
    assert {p for p,h in native['sourceInputs'].items() if p.startswith(('src/','shared/')) and previous['sourceInputs'].get(p)!=h}=={
        'src/Sanctuary.Content/GenericPetDecisions.cs','src/Sanctuary.Content/GenericPetFollowExecutor.cs'}
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    files=[Path(__file__).resolve(),ROOT/'tests/check_generic_pet_follow_executor.py',ROOT/'tools/probe_generic_pet_follow_executor.py',
        ROOT/'tools/reference/generic-pet-follow-executor-source-evidence.json',
        *(ROOT/'docs'/name for name in ('generic-pet-follow-executor-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in ('pre-build-1.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','content-build-1.log','integration-build-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','python-before-audit-refresh.log','python-tests.log','catalogue-checks.log','source-check.log','goatman-body-fixtures.json','goatman-foundation-fixtures.json'))]
    data=dict(referenceVersion='3.3.93847',parent=PARENT,status='Complete current follow executor policy verified with explicit leaf services; automatic summon world and learned skills remain unfinished.',
        verified=dict(sourceCases=5991,nativeMarkers=27,boots=3,reloads=2,saves=3,originalGoatmanBodies=2,retainedRootCasesPerBoot=12816,retainedSelectorCasesPerBoot=46936,retainedMovementCasesPerBoot=6144,retainedParameterCasesPerBoot=2016,auditMarkers=2,pythonTests=71,handlerCount=191,unchangedInstalledArtifacts=106),
        limits=['Actual native world/path/warp execution and the producer of cfg+0x70 remain unverified; neutral fields and explicit services preserve uncertainty.',
            'All six current executor arms,raw wrapper byte results,directional ring,fallback ordering,unsigned midpoint arithmetic,region branches and inline RNG compare against offline captured machine code; this does not certify leaf world effects.',
            'Two original saved Goatman bodies retain held recovery deadlines; no automatic combat,cap enforcement,payment,learned cast or new handler is enabled.',
            'No fresh full1180class run,deployment,save reset,final asset or complete-class certification is claimed.'],
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference/generic-pet-follow-executor-native-evidence.json';encoded=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Current GenericPet follow executor:5991source/native cases,27markers/3boots/2reloads,retained saved Goatman/decision/movement/item scopes;71Python;191handlers;106installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
