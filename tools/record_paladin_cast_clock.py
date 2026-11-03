#!/usr/bin/env python3
"""Bind the shared SC clock and all admitted Paladin cast routes."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/paladin-cast-clock';FOCUS=ROOT/'.local/class-skills/paladin-cast-clock-focus'
PARENT='5522b8a728f0d36dbbe04d92071fb0f2fcec939b';sha=inventory.sha

def record(compare=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json');focused=inventory.validate_inventory(FOCUS/'birth-inputs.json')
    for inputs in (STAGE/'birth-inputs.json',STAGE/'audit-inputs.json',FOCUS/'birth-inputs.json'):snapshot(STAGE/'pre-build-1.json',inputs)
    assert native['artifacts']==audit['artifacts']==focused['artifacts'] and native['copiedData']==audit['copiedData']==focused['copiedData']
    assert {n:h for n,h in native['sourceInputs'].items() if n.endswith('.cs')}==audit['sourceInputs']
    prior=json.loads((ROOT/'.local/class-skills/slow-missiles-windup/birth-inputs.json').read_text())
    assert {n for n in set(prior['sourceInputs'])|set(native['sourceInputs']) if n.endswith('.cs') and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}=={
        'src/Sanctuary.Content/ActionTiming.cs',
        'src/Sanctuary.Content/PaladinCastClock.cs','tests/Sanctuary.Integration/PaladinCastClockChecks.cs'}
    assert {n for n in set(prior['copiedData'])|set(native['copiedData']) if prior['copiedData'].get(n)!=native['copiedData'].get(n)}=={'Data/Sanctuary/Balance/paladin-cast-clock.json'}
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert native['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    sourcePath=ROOT/'tools/reference/paladin-cast-clock-engine-evidence.json';source=json.loads(sourcePath.read_text())
    assert source['contexts']==2072 and source['referenceVersion']=='3.3.93847' and not source['currentExecutableParity']
    for name,h in source['artifacts'].items():assert sha(ROOT/name)==h,name
    assert all((r['Frames'],r['Speed'],r['Action'],r['Flag'])==(16,256,9,2) for r in source['animations'])
    assert len(source['animations'])==8 and len(json.loads((ROOT/source['fixture']['path']).read_text()))==2072
    assert (ROOT/'.local/class-skills/paladin-cast-clock-source/sanitizer.log').read_text()==''
    baseline=ROOT/'.local/class-skills/slow-missiles-windup/birth-native-checks.log'
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/slow-missiles-windup-native-evidence.json'],cwd=ROOT))
    assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);expected['paladin-cast-clock-check']+=3
    expected['holy-shield-cast-check']+=1;expected['holy-shield-cast-reload-check']+=2;expected['holy-shield-cast-save']+=3
    for label in ('paladin-cast-clock-right','paladin-cast-clock-quick','holy-shield-cast-right','holy-shield-cast-quick'):
        expected[label+'-check']+=3;expected[label+'-result']+=3
    assert sum(expected.values())==240 and markers(STAGE/'birth-native-checks.log')==expected
    message='Paladin SC clock:2072source comparisons,1036static clocks,8actual owner routes/gear/effect casts;Holy Shield pays on start,other3on release;4cancellation controls and weapon/aura exclusions pass'
    for folder,count in ((STAGE,240),(FOCUS,42)):
        log=(folder/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==count
        assert log.count('Shutting down')==log.count('Writing world save snapshot done')==log.count(message)==3
        saved=json.loads((folder/'holy-shield-cast-checks.json').read_text())
        assert saved['Reloads']==2 and saved['SaveStarts']==3 and saved['PendingMana']==saved['Mana']==17
        for label in ('paladin-cast-clock-right','paladin-cast-clock-quick','holy-shield-cast-right','holy-shield-cast-quick'):
            assert log.count(label+'-result: passed')==3
    log=(STAGE/'birth-native-checks.log').read_text()
    assert log.count('Amazon SC clock:2072source comparisons,32actual weapon/FCR/learned-skill casts;generation0 paid Decoy cohort retained')==1
    saved=json.loads((STAGE/'amazon-cast-clock-fixtures.json').read_text());assert saved['Generation']==2 and saved['Life']>0
    reconnect=json.loads((STAGE/'pet-reconnect-fixture.json').read_text());assert reconnect['Generation']==2 and reconnect['TotalDraws']==10 and reconnect['Life']==69247 and reconnect['Mana']==54272
    world=json.loads((STAGE/'pet-placement-world-fixtures.json').read_text());assert world['Generation']==2 and json.loads(world['State'])['Revision']==11
    for name,count in (('named-item-runtime-audit.json',960),('first-loop-affix-audit.json',21)):
        new=json.loads((STAGE/name).read_text());assert new==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(new['InputHashes'])==count and new['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,h in new['InputHashes'].items():assert sha(ROOT/path)==h,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in new.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    auditlog=(STAGE/'audit-native-checks.log').read_text();assert auditlog.count(': passed')==2 and ': failed' not in auditlog and ' ERR]' not in auditlog
    for kind in ('content','integration'):assert all(t in (STAGE/f'{kind}-build-1.log').read_text() for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    for name,text in (('source-check.log','2072cases verified read-only'),('named-readiness.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in (STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for name,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==h,name
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    review=ROOT/'tools/reference/player-sc-start-review.json';reviewData=json.loads(review.read_text());assert sha(ROOT/reviewData['sourcePath'])==reviewData['sha256']
    files=[Path(__file__).resolve(),sourcePath,review,ROOT/'tests/check_paladin_cast_clock.py',ROOT/'tests/check_paladin_cast_clock_focus.py',baseline,
        *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','holy-shield-cast-checks.json')),
        *(ROOT/'docs'/n for n in ('paladin-cast-clock-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-1.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-1.log','audit-native-checks.log','audit-run-1.log','python-tests.log','source-check.log','named-readiness.log','catalogue-check.log','roadmap-check.log','content-build-1.log','integration-build-1.log','amazon-cast-clock-fixtures.json','inner-sight-fixtures.json','holy-shield-cast-checks.json','slow-missiles-windup-fixtures.json','grim-ward-transport-fixtures.json','pet-placement-world-fixtures.json','pet-reconnect-fixture.json'))]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,goalComplete=False,completedClasses=[],component='Shared literal SC/FCR clock and all four admitted Paladin SC routes',
        verified=dict(focusedNativeMarkers=42,nativeMarkers=240,retainedNativeMarkers=207,boots=3,reloads=2,saves=3,sourceComparisonsPerBoot=2072,
            staticRouteClocksPerBoot=1036,ownerRoutesPerBoot=8,learnedGearEffectCastsPerBoot=8,realInputCasts=12,holyShieldSourceComponentsPerBoot=120,retainedAmazonSourceCasesPerBoot=2072,
            pythonTests=71,namedDecisions=573,auditFingerprints=[960,21],handlers=191,protectedInstalledArtifactsUnchanged=106),
        limits=['Literal legacy scheduler offsets with owned animation rows;no new current executable/client or first/last-frame/repeat-input parity.',
            'Selected conversion/metadata/event services remain explicit;manual player XY/ID start review is separate from compiled controls. Active-cast FCR and generic source-event callbacks remain open.',
            'Only admitted Paladin SC routes use the central override;auras/weapon attacks,off-class skills and transformed appearances are excluded. Remaining pet/AI/initializer/combat mechanics are not certified.',
            'No full class certificate,fresh full-class regression,new final assets,deployment or installed-save write.'],tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    path=ROOT/'tools/reference/paladin-cast-clock-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if compare:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Paladin SC:240 retained/42 focused checks;four spells,2072 source cases,gear/input and original paid Holy Shield reloads pass;191 handlers,no class completion.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
