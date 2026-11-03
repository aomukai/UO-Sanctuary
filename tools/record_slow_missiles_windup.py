#!/usr/bin/env python3
"""Bind prepared Slow Missiles windup while retaining public/producer gates."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/slow-missiles-windup';FOCUS=ROOT/'.local/class-skills/slow-missiles-windup-focus'
PARENT='78a87208b02eb8348906ea7837748b577e10d3ac';sha=inventory.sha

def record(compare=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json');focused=inventory.validate_inventory(FOCUS/'birth-inputs.json')
    for path in (STAGE/'birth-inputs.json',STAGE/'audit-inputs.json',FOCUS/'birth-inputs.json'):snapshot(STAGE/'pre-build-3.json',path)
    assert native['artifacts']==audit['artifacts']==focused['artifacts'] and native['copiedData']==audit['copiedData']==focused['copiedData']
    assert {n:h for n,h in native['sourceInputs'].items() if n.endswith('.cs')}==audit['sourceInputs']
    prior=json.loads((ROOT/'.local/class-skills/necromancer-cast-clock/birth-inputs.json').read_text())
    assert {n for n in set(prior['sourceInputs'])|set(native['sourceInputs']) if n.endswith('.cs') and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}=={
        'src/Sanctuary.Content/AmazonSlowMissilesCast.cs','src/Sanctuary.Content/ClassCombat.cs',
        'src/Sanctuary.Content/AmazonSlowMissilesCasting.cs','tests/Sanctuary.Integration/SlowMissilesWindupChecks.cs'}
    assert {n for n in set(prior['copiedData'])|set(native['copiedData']) if prior['copiedData'].get(n)!=native['copiedData'].get(n)}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert native['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    baseline=ROOT/'.local/class-skills/necromancer-cast-clock/birth-native-checks.log'
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/necromancer-cast-clock-native-evidence.json'],cwd=ROOT))
    assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)
    for family in ('slow-missiles-windup','slow-missiles-cast'):
        expected[family+'-check']+=1;expected[family+'-reload-check']+=2
    expected['slow-missiles-restore-check']+=3;expected['slow-missiles-windup-live-result']+=3
    assert sum(expected.values())==207 and markers(STAGE/'birth-native-checks.log')==expected
    for folder,count in ((STAGE,207),(FOCUS,15)):
        log=(folder/'birth-native-checks.log').read_text();assert log.count(': passed')==count and ': failed' not in log and ' ERR]' not in log
        assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
        assert log.count('slow-missiles-windup-live-result: passed')==3
        assert log.count('Slow Missiles windup:source SC/self target,unpaid start,once-only live-rank release,stop/move/mana/frozen/weapon cancellation,8captured bolts and saved transient pass;')==1
        for generation in (1,2):assert log.count(f'Slow Missiles windup:generation{generation},same paid recipient/resources;unpaid context transient')==1
        saved=json.loads((folder/'slow-missiles-windup-fixtures.json').read_text())
        assert saved['Generation']==2 and saved['State']['Rank']==2 and saved['Mana']==12817
    saved=json.loads((STAGE/'slow-missiles-cast-fixtures.json').read_text());assert saved['Generation']==2 and saved['State']['Rank']==1
    reconnect=json.loads((STAGE/'pet-reconnect-fixture.json').read_text());assert reconnect['Generation']==2 and reconnect['TotalDraws']==10 and reconnect['Life']==69247 and reconnect['Mana']==54272
    world=json.loads((STAGE/'pet-placement-world-fixtures.json').read_text());assert world['Generation']==2 and json.loads(world['State'])['Revision']==11
    for name,count in (('named-item-runtime-audit.json',958),('first-loop-affix-audit.json',21)):
        new=json.loads((STAGE/name).read_text());assert new==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(new['InputHashes'])==count and new['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,h in new['InputHashes'].items():assert sha(ROOT/path)==h,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in new.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    a=(STAGE/'audit-native-checks.log').read_text();assert a.count(': passed')==2 and ': failed' not in a and ' ERR]' not in a
    for kind in ('content','integration'):assert all(t in (STAGE/f'{kind}-build-3.log').read_text() for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for attempt in (1,2):
        diagnostic=STAGE/f'diagnostic-reload-{attempt}'
        dll=diagnostic/('content/Sanctuary.Content.dll' if attempt==1 else 'Sanctuary.Content.dll')
        assert sha(dll)==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        assert 'slow-missiles-windup-reload-check: failed' in (diagnostic/'birth-native-checks.log').read_text()
    failure=(STAGE/'diagnostic-reload-2/birth-native-checks.log').read_text()
    assert 'mana=13073/12817,pending=None,prepared=False' in failure
    assert json.loads((STAGE/'diagnostic-reload-2/diagnosis.json').read_text())['productionChanged'] is False
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    for name,text in (('named-readiness.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in (STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for name,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==h,name
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    slow=next(s for s in status['skills'] if s['talentId']=='amazon.slowmissiles');assert slow['implementation']=='blocked' and slow['verification']=='partial-native-coverage' and not slow['complete']
    review=ROOT/'tools/reference/cast-frame-role-review.json';reviewData=json.loads(review.read_text())
    for name,h in reviewData['sources'].items():assert sha(ROOT/name)==h,name
    files=[Path(__file__).resolve(),review,ROOT/'tests/check_slow_missiles_windup.py',ROOT/'tests/check_slow_missiles_windup_focus.py',ROOT/'tools/build_skill_roadmap.py',baseline,
        *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-2.log','slow-missiles-windup-fixtures.json')),
        *(ROOT/'docs'/n for n in ('slow-missiles-windup-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-3.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-3.log','audit-native-checks.log','audit-run-3.log','python-tests.log','named-readiness.log','catalogue-check.log','roadmap-check.log','content-build-3.log','integration-build-3.log','slow-missiles-windup-fixtures.json','slow-missiles-cast-fixtures.json','amazon-cast-clock-fixtures.json','grim-ward-transport-fixtures.json','pet-placement-world-fixtures.json','pet-reconnect-fixture.json')),
        *(STAGE/f'diagnostic-reload-{attempt}'/n for attempt in (1,2) for n in ('birth-native-checks.log','birth-inputs.json','SlowMissilesWindupChecks.cs')),
        STAGE/'diagnostic-reload-1/content/Sanctuary.Content.dll',STAGE/'diagnostic-reload-1/integration/Sanctuary.Integration.dll',
        STAGE/'diagnostic-reload-2/Sanctuary.Content.dll',STAGE/'diagnostic-reload-2/Sanctuary.Integration.dll',
        STAGE/'diagnostic-reload-2/diagnosis.json']
    result=dict(referenceVersion='3.3.93847',parent=PARENT,goalComplete=False,completedClasses=[],component='Prepared shared Slow Missiles SC/session windup to existing do6/actual missile capture',
        verified=dict(focusedNativeMarkers=15,nativeMarkers=207,retainedNativeMarkers=195,boots=3,reloads=2,saves=3,naturalPreparedCasts=3,
            pythonTests=71,namedDecisions=573,auditFingerprints=[958,21],handlers=191,protectedInstalledArtifactsUnchanged=106),
        limits=['Internal prepared entry only. Public availability/bindings/ordinary Perform remain disabled. Supported encounter missiles capture state;unmapped/native ranged/spell/breath/child contexts are not established.',
            'Cast frame-role review is manual legacy evidence,not current client/recast or executable proof. Do not interpret the scheduled server epoch as a frame-position counter.',
            'Held paid-recipient/unpaid inspection clocks are explicit. No full class certificate,current factory/damage order,new final assets,deployment or installed-save write.'],
        diagnostics=dict(failedReloadRuns=2,productionDllUnchanged=True,cause='Test owner mana timer was constructed before fixture rename; first normal interval regenerated one mana. Creation-scoped inspection freeze fixes the fixture without changing skill behavior.'),
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    path=ROOT/'tools/reference/slow-missiles-windup-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if compare:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Prepared Slow Missiles:207retained/15focused markers across3boots/2reloads;SC/payment/cancel/live-rank/actual missiles;public producer gate remains.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
