#!/usr/bin/env python3
"""Bind Amazon source SC clocks to actual learned casts and retained world checks."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/amazon-cast-clock'
PARENT='1a2b7a4ba7e906a15639e7d3e576ad5530b2a922';sha=inventory.sha

def record(compare=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ('birth','audit'):snapshot(STAGE/'pre-build-6.json',STAGE/(name+'-inputs.json'))
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {n:h for n,h in native['sourceInputs'].items() if n.endswith('.cs')}==audit['sourceInputs']
    prior=json.loads((ROOT/'.local/class-skills/grim-ward-world-pulse/birth-inputs.json').read_text())
    assert {n for n in set(prior['sourceInputs'])|set(native['sourceInputs']) if n.endswith('.cs') and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}=={
        'src/Sanctuary.Content/AmazonCastClock.cs','src/Sanctuary.Content/AmazonInnerSight.cs','src/Sanctuary.Content/AmazonDecoys.cs','tests/Sanctuary.Integration/AmazonCastClockChecks.cs'}
    assert {n for n in set(prior['copiedData'])|set(native['copiedData']) if prior['copiedData'].get(n)!=native['copiedData'].get(n)}=={'Data/Sanctuary/Balance/amazon-cast-clock.json'}
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert native['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    sourcePath=ROOT/'tools/reference/amazon-cast-clock-engine-evidence.json';source=json.loads(sourcePath.read_text())
    assert source['contexts']==2072 and source['referenceVersion']=='3.3.93847' and not source['currentExecutableParity']
    for name,h in source['artifacts'].items():assert sha(ROOT/name)==h,name
    assert len(json.loads((ROOT/source['fixture']['path']).read_text()))==2072 and len(source['animations'])==8
    assert (ROOT/'.local/class-skills/amazon-cast-clock-source/sanitizer.log').read_text()==''
    focus=ROOT/'.local/class-skills/amazon-cast-clock-focus'
    focused=inventory.validate_inventory(focus/'birth-inputs.json')
    assert focused['artifacts']==native['artifacts'] and focused['copiedData']==native['copiedData']
    focusedLog=(focus/'birth-native-checks.log').read_text()
    assert focusedLog.count(': passed')==24 and ': failed' not in focusedLog and ' ERR]' not in focusedLog
    assert focusedLog.count('Shutting down')==focusedLog.count('Writing world save snapshot done')==3
    baseline=ROOT/'.local/class-skills/grim-ward-world-pulse/birth-native-checks.log'
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/grim-ward-world-pulse-native-evidence.json'],cwd=ROOT))
    assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);expected['amazon-cast-clock-check']+=1;expected['amazon-cast-clock-reload-check']+=2
    expected['inner-sight-check']+=1;expected['inner-sight-reload-check']+=2;expected['inner-sight-result']+=1
    for label in ('amazon-cast-clock-right','amazon-cast-clock-quick'):
        expected[label+'-check']+=3;expected[label+'-result']+=3
    assert sum(expected.values())==180 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Amazon SC clock:2072source comparisons,32actual weapon/FCR/learned-skill casts;generation0 paid Decoy cohort retained')==1
    for generation in (1,2):assert log.count(f'Amazon SC clock:generation{generation},same paid Decoy/gear/wounds/mana;unpaid windup transient')==1
    saved=json.loads((STAGE/'amazon-cast-clock-fixtures.json').read_text());assert saved['Generation']==2 and saved['Life']>0
    reconnect=json.loads((STAGE/'pet-reconnect-fixture.json').read_text())
    assert reconnect['Generation']==2 and reconnect['TotalDraws']==10 and reconnect['Life']==69247 and reconnect['Mana']==54272
    world=json.loads((STAGE/'pet-placement-world-fixtures.json').read_text());assert world['Generation']==2 and json.loads(world['State'])['Revision']==11
    for name,count in (('named-item-runtime-audit.json',954),('first-loop-affix-audit.json',21)):
        new=json.loads((STAGE/name).read_text());assert new==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(new['InputHashes'])==count and new['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,h in new['InputHashes'].items():assert sha(ROOT/path)==h,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in new.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    auditlog=(STAGE/'audit-native-checks.log').read_text();assert auditlog.count(': passed')==2 and ': failed' not in auditlog and ' ERR]' not in auditlog
    for kind in ('content','integration'):
        assert all(t in (STAGE/f'{kind}-build-6.log').read_text() for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    for name,text in (('source-check.log','2072cases verified read-only'),('named-readiness.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in (STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for name,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==h,name
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    slow=next(s for s in status['skills'] if s['talentId']=='amazon.slowmissiles');assert slow['implementation']=='blocked' and not slow['complete']
    for name,fragment in (('diagnostic-weapon-1','Actual equipped source class differs: 1HS'),('diagnostic-named-2','That named definition is not available.'),('diagnostic-ready-3','amazon-cast-clock-right-result: failed'),('diagnostic-mana-4','Original paid Decoy/gear/wounds/mana changed across world reload'),('diagnostic-owner-5','Reload unpaid windup')):
        assert fragment in (STAGE/name/'birth-native-checks.log').read_text()
    assert sha(STAGE/'diagnostic-weapon-1/content/Sanctuary.Content.dll')==native['artifacts']['Assemblies/Sanctuary.Content.dll']
    assert sha(STAGE/'diagnostic-weapon-1/AmazonCastClock.cs')==sha(ROOT/'src/Sanctuary.Content/AmazonCastClock.cs')
    files=[Path(__file__).resolve(),sourcePath,ROOT/'tests/check_amazon_cast_clock.py',ROOT/'tests/check_amazon_cast_clock_focus.py',baseline,*(focus/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','amazon-cast-clock-fixtures.json')),
        *(ROOT/'docs'/n for n in ('amazon-cast-clock-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-1.json','pre-build-2.json','pre-build-3.json','pre-build-4.json','pre-build-5.json','pre-build-6.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-6.log','audit-native-checks.log','audit-run-6.log','python-tests.log','source-check.log','named-readiness.log','catalogue-check.log','roadmap-check.log','content-build-6.log','integration-build-6.log','amazon-cast-clock-fixtures.json','inner-sight-fixtures.json','grim-ward-transport-fixtures.json','pet-placement-world-fixtures.json','pet-reconnect-fixture.json')),
        *(p for directory in ('diagnostic-weapon-1','diagnostic-named-2','diagnostic-ready-3','diagnostic-mana-4','diagnostic-owner-5') for p in (STAGE/directory).rglob('*') if p.is_file())]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,goalComplete=False,completedClasses=[],
        component='Source weapon-specific Amazon SC/FCR action and end clocks for learned Inner Sight/Decoy',
        verified=dict(focusedNativeMarkers=24,nativeMarkers=180,retainedNativeMarkers=161,boots=3,reloads=2,saves=3,sourceComparisonsPerBoot=2072,
            actualGearCasts=32,realInputCasts=6,pythonTests=71,namedDecisions=573,auditFingerprints=[954,21],handlers=191,protectedInstalledArtifactsUnchanged=106),
        limits=['Seven unchanged legacy scheduling routines with declared class/skill/conversion/metadata/event services;owned animation rows do not prove current executable/client or first/last-frame origin parity.',
            'Only admitted Inner Sight and Decoy begin routes use the exact clock. Generic UI timing projections,active-cast FCR change and unknown native weapon classes remain separate.',
            'Saved inspection pet/timers are explicitly held. No downtime-regeneration or generic source-event callback parity claim.',
            'Slow Missiles remains disabled;hostile producer,target and modern damage/context gaps remain. No full class certificate,new final assets,deployment or installed-save write.'],
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    path=ROOT/'tools/reference/amazon-cast-clock-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if compare:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Amazon SC clock:180markers/3boots/2reloads;2072source cases per boot,32actual gear casts,6real inputs;191handlers and installed state preserved.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
