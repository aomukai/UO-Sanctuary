#!/usr/bin/env python3
"""Bind Ward periodic actual-area composition with its unresolved controller inputs."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
from build_grim_ward_bodies import build as body_rows
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/grim-ward-world-pulse'
PARENT='d378db71349686fe3ef385134b3ef66bdb0cd564';sha=inventory.sha

def record(compare=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ('birth','audit'):snapshot(STAGE/'pre-build-1.json',STAGE/(name+'-inputs.json'))
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {n:h for n,h in native['sourceInputs'].items() if n.endswith('.cs')}==audit['sourceInputs']
    prior=json.loads((ROOT/'.local/class-skills/grim-ward-transport/birth-inputs.json').read_text())
    assert {n for n in set(prior['sourceInputs'])|set(native['sourceInputs']) if n.endswith('.cs') and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}=={
        'src/Sanctuary.Content/GrimWardCurseTransport.cs','src/Sanctuary.Content/GrimWardWorldPulse.cs','tests/Sanctuary.Integration/GrimWardWorldPulseChecks.cs'}
    assert {n for n in set(prior['copiedData'])|set(native['copiedData']) if prior['copiedData'].get(n)!=native['copiedData'].get(n)}=={
        'Data/Sanctuary/Balance/class-skill-status.json','Data/Sanctuary/Balance/grim-ward-bodies.json'}
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert native['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    assert json.loads((ROOT/'data/Sanctuary/Balance/grim-ward-bodies.json').read_text())==body_rows()
    baseline=ROOT/'.local/class-skills/grim-ward-transport/birth-native-checks.log'
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/grim-ward-transport-native-evidence.json'],cwd=ROOT))
    assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);expected['grim-ward-world-pulse-check']+=3
    assert sum(expected.values())==161 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Ward world pulses:3body kinds,real area recipients,elapsed0/1/6,one explicit formula evaluation per pulse,retained/new primary deltas,creator/radius/floor and dispatch-before-collision pass;')==3
    for generation in range(3):assert log.count(f'Ward transport:generation{generation},same saved monster/missile creator/four deltas/AI;')==1
    saved=json.loads((STAGE/'grim-ward-transport-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Creator']==saved['Curse']['Source'] and saved['Creator']>=0x40000000
    assert saved['Curse']['Ward']==dict(Velocity=-27,Attack=-27,Other=-27,FullPhysical=-120) and saved['Curse']['PhysicalDelta']==-24
    reconnect=json.loads((STAGE/'pet-reconnect-fixture.json').read_text())
    assert reconnect['Generation']==2 and reconnect['TotalDraws']==10 and reconnect['Life']==69247 and reconnect['Mana']==54272
    world=json.loads((STAGE/'pet-placement-world-fixtures.json').read_text())
    assert world['Generation']==2 and json.loads(world['State'])['Revision']==11
    for name,count in (('named-item-runtime-audit.json',952),('first-loop-affix-audit.json',21)):
        new=json.loads((STAGE/name).read_text());assert new==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(new['InputHashes'])==count and new['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,h in new['InputHashes'].items():assert sha(ROOT/path)==h,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT))
        skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in new.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    logAudit=(STAGE/'audit-native-checks.log').read_text()
    assert logAudit.count(': passed')==2 and ': failed' not in logAudit and ' ERR]' not in logAudit
    for kind in ('content','integration'):
        assert all(t in (STAGE/f'{kind}-build-1.log').read_text() for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    for name,text in (('body-data-check.log','Three pinned Ward body rows retain period6/dispatch30'),('named-readiness.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):
        assert text in (STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for name,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==h,name
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    ward=next(s for s in status['skills'] if s['talentId']=='barbarian.grimward')
    assert ward['implementation']=='blocked' and ward['verification']=='partial-native-coverage' and not ward['complete']
    review=ROOT/'tools/reference/other-animation-dispatch-review.json';reviewData=json.loads(review.read_text())
    assert sha(ROOT/reviewData['sourcePath'])==reviewData['sourceSha256'] and reviewData['gitBlob']=='cac33ac7614f167989129849dedd7e6b9451569d'
    files=[Path(__file__).resolve(),ROOT/'tools/build_grim_ward_bodies.py',ROOT/'tests/check_grim_ward_world_pulse.py',baseline,
        ROOT/'tools/reference/barbarian-grim-ward-preflight.json',review,
        *(ROOT/'docs'/n for n in ('grim-ward-world-pulse-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-1.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-1.log','audit-native-checks.log','audit-run-1.log','python-tests.log','body-data-check.log','named-readiness.log','catalogue-check.log','roadmap-check.log','content-build-1.log','integration-build-1.log','grim-ward-transport-fixtures.json','pet-placement-world-fixtures.json','pet-reconnect-fixture.json'))]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,goalComplete=False,completedClasses=[],
        component='Prepared periodic body do14/do30 world-area and evaluated-packet recipient composition',
        verified=dict(nativeMarkers=161,retainedNativeMarkers=158,boots=3,reloads=2,saves=3,bodyKinds=3,worldScenes=9,
            legacyCurseCasesPerBoot=31134,lifecycleCasesPerBoot=3144,retainedLiveEscapeExpiryRuns=3,pythonTests=71,
            namedDecisions=573,auditFingerprints=[952,21],handlers=191,protectedInstalledArtifactsUnchanged=106),
        limits=['Caller supplies total/current frames,formula lookup and collision;no automatic controller,timer,child insertion,global clock or current owner-versus-snapshot policy is selected.',
            'Actual native search uses existing radius/floor/serial-order projections and mapped recipient AIs. Exact source room traversal,ties,client scale and absent/dead/off-facet owner lifecycle remain unfinished.',
            'Other animation review is manual legacy source inspection;no new compiled/current-executable parity. Nonattack native animation consumers remain open.',
            'No learned cast,class completion,final assets,deployment,installed-save write or fresh full-class regression.'],
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    path=ROOT/'tools/reference/grim-ward-world-pulse-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if compare:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Ward world pulse:161markers/3boots/2reloads;all3body kinds,one captured packet per pulse and actual area effects;controller context/clock and learned cast remain open.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
