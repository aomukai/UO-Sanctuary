#!/usr/bin/env python3
"""Bind current warp candidate and monster-placement comparisons and retained saved bodies."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/generic-pet-warp-candidate'
PARENT='7ec817fe446c49b81783f28bdbfad18a6ef74f8e'
sha=inventory.sha
def record(check=False):
    subprocess.run(['python3',str(ROOT/'tools/record_generic_pet_live_follow.py'),'--check'],check=True,stdout=subprocess.PIPE)
    subprocess.run(['python3',str(ROOT/'tools/record_generic_pet_warp_search_reference.py'),'--check'],check=True,stdout=subprocess.PIPE)
    inventory.STAGE=STAGE;native=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-build-1.json',STAGE/'birth-inputs.json')
    diagnostic=STAGE/'diagnostic-cookie-result-1'
    failed=json.loads((diagnostic/'birth-inputs.json').read_text())
    snapshot(diagnostic/'pre-build-1.json',diagnostic/'birth-inputs.json')
    assert failed['artifacts']==native['artifacts'] and failed['copiedData']==native['copiedData']
    assert {p for p,h in failed['sourceInputs'].items() if native['sourceInputs'].get(p)!=h}=={
        '.local/class-skills/generic-pet-warp-candidate-source-probe.json'}
    for kind in ('content','integration'):
        name='Sanctuary.'+kind.title()+'.dll'
        assert sha(diagnostic/kind/name)==sha(STAGE/kind/name)
    old_source=json.loads((diagnostic/'generic-pet-warp-candidate-source-evidence.json').read_text())
    assert sha(diagnostic/'probe_generic_pet_warp_candidate.py')==old_source['artifacts']['tools/probe_generic_pet_warp_candidate.py']
    assert sha(diagnostic/'generic-pet-warp-candidate-source-probe.json')==old_source['fixture']['sha256']
    assert failed['sourceInputs'][old_source['fixture']['path']]==old_source['fixture']['sha256']
    failed_log=(diagnostic/'birth-native-checks.log').read_text()
    assert 'generic-pet-warp-candidate-check: failed' in failed_log and 'candidate case 243:[1,' in failed_log
    source=json.loads((ROOT/'tools/reference/generic-pet-warp-candidate-source-evidence.json').read_text())
    assert source['cases']==8553 and source['offlineMachineCodeExecuted'] and not source['worldMovementVerified']
    for name,digest in source['artifacts'].items():assert sha(ROOT/name)==digest,name
    fixture=source['fixture'];assert sha(ROOT/fixture['path'])==fixture['sha256']
    assert len(json.loads((ROOT/fixture['path']).read_text()))==8553
    placement=json.loads((ROOT/'tools/reference/generic-pet-monster-placement-source-evidence.json').read_text())
    assert placement['cases']==5229 and placement['offlineMachineCodeExecuted'] and not placement['worldMovementVerified']
    for name,digest in placement['artifacts'].items():assert sha(ROOT/name)==digest,name
    assert sha(ROOT/placement['fixture']['path'])==placement['fixture']['sha256']
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==45
    assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    for name in ('generic-pet-warp-candidate','generic-pet-monster-placement','generic-pet-warp-primitives','generic-pet-warp','generic-pet-motion-services','generic-pet-follow-movement','generic-pet-follow-executor','generic-pet-decisions','generic-pet-movement','generic-pet-parameters'):
        assert log.count(name+'-check: passed')==3,name
    assert log.count('GenericPet warp candidate:8553complete current-code cases')==3
    assert log.count('GenericPet monster placement:5229complete current-code cases')==3
    for name in ('goatman-body','goatman-foundation'):
        assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2
    saved=json.loads((STAGE/'goatman-body-fixtures.json').read_text())
    assert len(saved)==2 and {s['Female'] for s in saved}=={False,True}
    for s in saved:assert all(s[k]>0 for k in ('Owner','Body','Helm','Mana','Maximum')) and s['Life']==s['Maximum']-257 and s['State']
    audit=json.loads((STAGE/'audit-inputs.json').read_text())
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {p:h for p,h in native['sourceInputs'].items() if p.endswith('.cs')}==audit['sourceInputs']
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==2
    for name,count in [('named-item-runtime-audit.json',930),('first-loop-affix-audit.json',21)]:
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
    previous=json.loads((ROOT/'.local/class-skills/generic-pet-warp-primitives/birth-inputs.json').read_text())
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==previous['artifacts'][name]
    assert native['copiedData']==previous['copiedData']
    assert {p for p,h in native['sourceInputs'].items() if p.startswith(('src/','shared/')) and previous['sourceInputs'].get(p)!=h}=={
        'src/Sanctuary.Content/GenericPetWarpCandidateCheck.cs','src/Sanctuary.Content/GenericPetMonsterPlacement.cs'}
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    files=[Path(__file__).resolve(),ROOT/'tests/check_generic_pet_warp_candidate.py',ROOT/'tools/probe_generic_pet_warp_candidate.py',
        ROOT/'tools/reference/generic-pet-warp-candidate-source-evidence.json',ROOT/'tools/reference/generic-pet-monster-placement-source-evidence.json',ROOT/'tools/probe_generic_pet_monster_placement.py',ROOT/'tools/reference/generic-pet-warp-search-reference-evidence.json',ROOT/'tools/record_generic_pet_warp_search_reference.py',ROOT/'tools/reference/generic-pet-live-follow-source-evidence.json',ROOT/'tools/record_generic_pet_live_follow.py',ROOT/'tools/capture_generic_pet_follow_services.py',
        *(ROOT/'docs'/name for name in ('generic-pet-warp-candidate-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in ('pre-build-1.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','content-build-1.log','integration-build-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','python-tests.log','catalogue-checks.log','candidate-source-check.log','placement-source-check.log','goatman-body-fixtures.json','goatman-foundation-fixtures.json')),
        *(p for p in sorted(diagnostic.rglob('*')) if p.is_file())]
    data=dict(referenceVersion='3.3.93847',parent=PARENT,status='Current validation-only landing candidate and monster relocation policy verified;actual geometry/path commit/world controller and learned summons remain unfinished.',
        verified=dict(sourceCases=8553,placementCases=5229,nativeMarkers=45,composedSearchCandidateCasesPerBoot=512,retainedPrimitiveCasesPerBoot=2352,retainedWarpCasesPerBoot=1360,retainedMotionServiceCasesPerBoot=12325,retainedFollowMovementCasesPerBoot=3856,retainedExecutorCasesPerBoot=5991,boots=3,reloads=2,saves=3,originalGoatmanBodies=2,retainedRootCasesPerBoot=12816,retainedSelectorCasesPerBoot=46936,retainedMovementCasesPerBoot=6144,retainedParameterCasesPerBoot=2016,auditMarkers=2,pythonTests=71,handlerCount=191,unchangedInstalledArtifacts=106),
        limits=['Actual metadata/geometry/collision,special point,nearby adjustment,path commit,source timing,cfg+0x70 producer and persistent world controller remain unfinished;explicit services preserve these boundaries.',
            'Complete candidate/search code executes for the actual immutable radius0/validation-only request;512compositions preserve shared room RNG. All6placement fragments execute for actual monster/last-arg0path with the real room-update body. Player/nonzero-last-arg and generic spawning modes are not exposed.',
            'Two original saved Goatman bodies retain held recovery deadlines; no automatic combat,cap enforcement,payment,learned cast or new handler is enabled.',
            'No fresh full1180class run,deployment,save reset,final asset or complete-class certification is claimed.'],
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    data['diagnostic']=dict(path=str(diagnostic.relative_to(ROOT)),sameProductionAndIntegrationBinaries=True,
        changedInput='Only the candidate source fixture changed;the normal-return cookie hook now preserves RAX,which the captured epilogue returns unchanged.',
        failedCase=243,acceptedBuild=1,acceptedRun=2)
    out=ROOT/'tools/reference/generic-pet-warp-candidate-native-evidence.json';encoded=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Current GenericPet warp candidate/placement:8553/5229source/native cases,45markers/3boots/2reloads,retained saved Goatman/decision/movement/item scopes;71Python;191handlers;106installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
