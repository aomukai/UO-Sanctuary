#!/usr/bin/env python3
"""Bind automatic Wake firing/death, native precision regression and retained suites."""
import argparse,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/wake-fire-automatic'
TARGET=ROOT/'.local/class-skills/wake-fire-automatic-targeted'
PARENT='dabd726bf3c217a1618a3401f5017e4f3ab80af7'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/'pre-build-7.json',STAGE/'birth-inputs.json')
    inventory.STAGE=TARGET;focused=inventory.validate_inventory(TARGET/'birth-inputs.json');inventory.STAGE=STAGE
    assert tested==focused,'Focused/full must use exactly the same source,data,binaries and extra inputs'
    sys.path.insert(0,str(ROOT/'tests'))
    from check_wake_fire_automatic import configuration
    c=configuration();assert (len(c['extra_families']),len(c['extra_checks']),len(c['extra_target_input_commands']))==(20,10,12)
    log=(STAGE/'birth-native-checks.log').read_text();small=(TARGET/'birth-native-checks.log').read_text()
    for text,count in [(log,198),(small,27)]:
        assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==count
        assert text.count('Writing world save snapshot done')==text.count('Shutting down')==3
        assert text.count('wake-fire-automatic-live-check: passed')==text.count('wake-fire-automatic-live-result: passed')==3
        assert text.count('wake-fire-automatic-check: passed')==1 and text.count('wake-fire-automatic-reload-check: passed')==2
    for name in c['extra_families']:assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2,name
    for name in c['extra_checks']:assert log.count(name+': passed')==3,name
    for name in c['extra_target_input_commands']:
        assert log.count(name+'-check: passed')==log.count(name+'-result: passed')==3,name
        assert log.count(name+(': acknowledged' if name.endswith('left') else ': windup'))==3,name
    for name in ('sentry-retirement','sentry-learned','lightning-learned','inferno-learned','death-learned'):
        assert log.count(name+'-result: passed')==1 and log.count(name+'-reload-result: passed')==2,name
    for stage in (STAGE,TARGET):
        saved=json.loads((stage/'wake-fire-automatic-fixtures.json').read_text())
        assert saved['Generation']==2 and saved['Actor']['Corpse'] and saved['Actor']['Retirement'] is None
        assert saved['Actor']['Creation']['Ability']==206 and saved['Actor']['Creation']['Rank']==3
        control=saved['Actor']['WakeControl'];assert control['Mode']==0 and control['Dispatches']==control['Attempts']==1 and control['Ai']['RemainingShots']==4
        flights=saved['Pool']['Flights'];assert len(flights)==17 and len(saved['Pool']['Leases'])==0
        first=flights[0]['Id'];assert [f['Id'] for f in flights[:15]]==list(range(first,first+15)) and all(f['Id']>first+14 for f in flights[15:])
        assert all(f['Actor']==saved['Body'] and f['Owner']==saved['Owner'] and f['Rank']==3 and f['Fault'] is None for f in flights)
        assert any(not f['Wave'] for f in flights) and any(f['Wave'] for f in flights)
        assert json.loads((stage/'wake-fire-missile-fixtures.json').read_text())['Generation']==2
    audit=json.loads((STAGE/'audit-inputs.json').read_text())
    assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
    assert audit['sourceInputs']=={p:h for p,h in tested['sourceInputs'].items() if p.endswith('.cs')}
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==2
    for name,count in [('named-item-runtime-audit.json',916),('first-loop-affix-audit.json',21)]:
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in current['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    for kind in ('content','integration'):
        text=(STAGE/(kind+'-build-7.log')).read_text();assert all(w in text for w in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    text=(STAGE/'catalogue-checks.log').read_text()
    for word in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.','Current sentry monster/models:601 fields','Local sentry animation:11records pinned','Owned missile guide:5fields pinned'):assert word in text
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses']
    assert sum(s['implementation']=='handler-present' for s in status['skills'])==191
    assert sum(s['implementation']=='handler-present' and s['characterClass']=='Assassin' for s in status['skills'])==22
    row=next(s for s in status['skills'] if s['talentId']=='assassin.wakeoffire');assert row['implementation']=='blocked' and row['verification']=='partial-native-coverage' and not row['complete']
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    prior=json.loads((ROOT/'.local/class-skills/wake-fire-missiles/birth-inputs.json').read_text())
    for path in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][path]==tested['artifacts'][path]
    changed={p for p,h in tested['sourceInputs'].items() if p.startswith(('src/','shared/')) and prior['sourceInputs'].get(p)!=h}
    assert changed=={'src/Sanctuary.Content/'+n for n in ('AssassinSentryBody.cs','AssassinSentryRetirement.cs','AssassinTrapRoster.cs','AssassinWakeFireMissiles.cs','SkillHealth.cs')}
    assert {p for p,h in tested['copiedData'].items() if prior['copiedData'].get(p)!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    old=json.loads(subprocess.check_output(['git','show',PARENT+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT))
    assert [a['talentId'] for a,b in zip(old['skills'],status['skills']) if a!=b]==['assassin.wakeoffire']
    diagnostics={};earlier=set();health='src/Sanctuary.Content/SkillHealth.cs'
    for parent in (STAGE,TARGET):
        for folder in sorted(parent.glob('diagnostic-*')):
            captured=json.loads((folder/'birth-inputs.json').read_text())
            for kind in ('content','integration'):assert sha(folder/kind/('Sanctuary.'+kind.title()+'.dll'))==captured['artifacts']['Assemblies/Sanctuary.'+kind.title()+'.dll']
            for f in folder.glob('*.cs'):assert [h for path,h in captured['sourceInputs'].items() if Path(path).name==f.name]==[sha(f)]
            assert all(captured['sourceInputs'][p]==h for p,h in tested['sourceInputs'].items() if p.startswith(('src/','shared/')) and p!=health)
            assert captured['sourceInputs'][health]!=tested['sourceInputs'][health]
            earlier.add(sha(folder/'content/Sanctuary.Content.dll'))
            diagnostics[str(folder.relative_to(ROOT))]={str(f.relative_to(ROOT)):sha(f) for f in folder.rglob('*') if f.is_file()}
    assert len(diagnostics)==6 and len(earlier)==1 and sha(STAGE/'content/Sanctuary.Content.dll') not in earlier
    failure=(TARGET/'diagnostic-fractional-life-4/birth-native-checks.log').read_text();assert 'actorLife=13056' in failure and 'actor=True flights=True' in failure
    files=[Path(__file__).resolve(),ROOT/'tools/build_skill_roadmap.py',ROOT/'tests/check_wake_fire_automatic.py',ROOT/'tests/check_wake_fire_missiles.py',
        *(ROOT/'docs'/n for n in ('wake-fire-automatic-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-7.json','birth-inputs.json','birth-native-checks.log','native-run-3.log','content-build-7.log','integration-build-7.log','audit-inputs.json','audit-native-checks.log','audit-run-4.log','python-tests.log','catalogue-checks.log','wake-fire-automatic-fixtures.json','wake-fire-missile-fixtures.json','wake-fire-actor-fixtures.json')),
        *(TARGET/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-5.log','wake-fire-automatic-fixtures.json','wake-fire-missile-fixtures.json'))]
    files.extend(c['extra_inputs']);files.extend(c['extra_files'].values())
    data=dict(referenceVersion='3.3.93847',parent=PARENT,status='Automatic prepared Wake firing/E9 retained original death and fractional-life precision verified; learned route remains gated.',
        verified=dict(fullMarkers=198,targetedMarkers=27,retainedMarkers=189,bootsPerRun=3,reloadsPerRun=2,savesPerRun=3,wireRequests=36,successfulRightQuickCasts=24,rejectedLeftRequests=12,originalAutomaticFlightIdentities=15,finalAutomaticFlights=17,automaticReloadGenerations=2,auditMarkers=2,pythonTests=71,handlerCount=191,assassinHandlers=22,unchangedInstalledArtifacts=106),
        limits=['NPC placement-versus-do125 execution NextDelay identity remains uncertain. Prepared default firing explicitly uses one supplied execution per do125; learned placement/payment/input is gated.',
            'E9 death uses owned current animation/model metadata and reconstructed ordinary source scheduling. Exact modern corpse-pool insertion/callback/eviction order, historical fault/death upgrade and current full executable chronology remain unverified.',
            'Real cap replacement keeps its original death deadline, naturally completed before/on reload. Issued flight inspection clocks are explicitly held/rearmed; the earlier scene reconciles expired leases after foreign future stepping without changing its held flights.',
            'Source grid/room/geometry/activation/global-order/seed, zero-velocity, inherited sunder, complete owner/item/PvP/client and final artwork remain acceptance work. No installed deployment/save reset/class certificate.'],
        tested=tested,diagnostics=diagnostics,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference/wake-fire-automatic-native-evidence.json';encoded=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Automatic prepared Wake:198full/27focused markers;3boots/2reloads each;original E9/15flights and fractional wounds retained;191handlers;106installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
