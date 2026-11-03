#!/usr/bin/env python3
"""Bind prepared real Wake maker/waves without claiming learned casting."""
import argparse,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/wake-fire-missiles'
PARENT='ff9c0f353f681336fc8c3dedaa1308dbbf0b7cd8'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-build-6.json',STAGE/'birth-inputs.json')
    sys.path.insert(0,str(ROOT/'tests'))
    from check_wake_fire_missiles import configuration
    config=configuration();assert (len(config['extra_families']),len(config['extra_checks']),len(config['extra_target_input_commands']))==(19,9,12)
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==189
    assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    for name in config['extra_families']:
        assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2,name
    for name in config['extra_checks']:assert log.count(name+': passed')==3,name
    for name in config['extra_target_input_commands']:
        assert log.count(name+'-check: passed')==log.count(name+'-result: passed')==3,name
        assert log.count(name+(': acknowledged' if name.endswith('left') else ': windup'))==3,name
    for name in ('sentry-retirement','sentry-learned','lightning-learned','inferno-learned','death-learned'):
        assert log.count(name+'-result: passed')==1 and log.count(name+'-reload-result: passed')==2,name
    assert log.count('corpse-base-result: passed')==3
    saved=json.loads((STAGE/'wake-fire-missile-fixtures.json').read_text())
    assert saved['Generation']==2 and len(saved['Pool']['Flights'])==12 and len(saved['Pool']['Leases'])==1
    assert saved['Pool']['NextId']==12 and [f['Id'] for f in saved['Pool']['Flights']]==list(range(1,13))
    assert saved['Actor']['Creation']['Ability']==206 and saved['Actor']['Creation']['Rank']==3
    assert all(f['Actor']==saved['Body'] and f['Owner']==saved['Owner'] and f['Rank']==3 for f in saved['Pool']['Flights'])
    assert any(f['Wave'] and f['LastCollision']==saved['Target'] for f in saved['Pool']['Flights'])
    assert any(not f['Wave'] for f in saved['Pool']['Flights'])
    for name in ('sentry-learned','lightning-learned','inferno-learned','death-learned'):
        old=json.loads((STAGE/(name+'-fixtures.json')).read_text())
        assert old['Generation']==2 and old['Actor']['Creation']['Rank']==21 and old['Bolt']['Actor']==old['Body']
    audit=json.loads((STAGE/'audit-inputs.json').read_text())
    assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
    assert audit['sourceInputs']=={p:h for p,h in tested['sourceInputs'].items() if p.endswith('.cs')}
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==2
    for name,count in [('named-item-runtime-audit.json',916),('first-loop-affix-audit.json',21)]:
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in current['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT))
        skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    for kind in ('content','integration'):
        text=(STAGE/(kind+'-build-6.log')).read_text();assert all(w in text for w in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    text=(STAGE/'catalogue-checks.log').read_text()
    for word in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.','Current sentry monster/models:601 fields','Local sentry animation:11records pinned','Owned missile guide:5fields pinned'):
        assert word in text
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses']
    assert sum(s['implementation']=='handler-present' for s in status['skills'])==191
    assert sum(s['implementation']=='handler-present' and s['characterClass']=='Assassin' for s in status['skills'])==22
    assert next(s for s in status['skills'] if s['talentId']=='assassin.wakeoffire')['implementation']=='blocked'
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    prior=json.loads((ROOT/'.local/class-skills/death-learned/birth-inputs.json').read_text())
    assert prior['copiedData']==tested['copiedData']
    for path in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][path]==tested['artifacts'][path]
    changed={p for p,h in tested['sourceInputs'].items() if p.startswith(('src/','shared/')) and prior['sourceInputs'].get(p)!=h}
    assert changed=={'src/Sanctuary.Content/'+n for n in ('AssassinWakeFireMissiles.cs','AssassinSentryBolts.cs','AssassinTrapClock.cs')}
    guide=json.loads((ROOT/'tools/reference/d2r-local-missile-guide-evidence.json').read_text());assert sha(ROOT/guide['file']['path'])==guide['file']['sha256']
    assert 'same skill cast' in guide['fields']['NextDelay']
    diagnostic=STAGE/'diagnostic-corridor-1';d=json.loads((diagnostic/'birth-inputs.json').read_text())
    for kind in ('content','integration'):assert sha(diagnostic/kind/('Sanctuary.'+kind.title()+'.dll'))==d['artifacts']['Assemblies/Sanctuary.'+kind.title()+'.dll']
    assert sha(diagnostic/'WakeFireMissileChecks.cs')==d['sourceInputs']['tests/Sanctuary.Integration/WakeFireMissileChecks.cs']
    assert 'No independent fixture corridor near 7300,2600' in (diagnostic/'birth-native-checks.log').read_text()
    files=[Path(__file__).resolve(),ROOT/'tests/check_wake_fire_missiles.py',ROOT/'tools/corroborate_local_missile_guide.py',ROOT/'tools/reference/d2r-local-missile-guide-evidence.json',
        *(ROOT/'docs'/n for n in ('wake-fire-missiles-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-6.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','content-build-6.log','integration-build-6.log','audit-inputs.json','audit-native-checks.log','audit-run-2.log','python-tests.log','python-tests-stale-audit.log','catalogue-checks.log','wake-fire-missile-fixtures.json','wake-fire-actor-fixtures.json','wake-fire-prepared-fixtures.json'))]
    files.extend(config['extra_files'].values());files.extend(config['extra_inputs']);files.extend(f for f in diagnostic.rglob('*') if f.is_file())
    data=dict(referenceVersion='3.3.93847',parent=PARENT,status='Real prepared Wake maker/waves and supplied execution leases verified; learned/default dispatch remains gated; no class certificate.',
        verified=dict(nativeCommandMarkers=189,retainedLearnedSentryMarkers=180,boots=3,reloads=2,saves=3,wireRequests=36,successfulRightQuickCasts=24,rejectedLeftRequests=12,originalWakeFlights=12,wakeLeaseCount=1,wakeResumeGenerations=2,auditMarkers=2,pythonTests=71,handlerCount=191,assassinHandlers=22,unchangedInstalledArtifacts=106),
        limits=['The current shipped guide establishes same-skill-cast NextDelay. Exact NPC placement versus individual do125 execution identity remains unverified; the backend supplies explicit execution groups and does not enable learned casting.',
            'Automatic actor dispatch and E9 retirement/released-source damage remain unconnected. Existing prepared actor terminal faults are retained; no invented dead-owner lifetime is added.',
            'Native Q12/source-cell/terrain adapters retain current executable grid/room/enumeration/activation/seed/client acceptance gaps. Zero-velocity paths, inherited sunder, complete owner/item/PvP and historical-state migration remain explicit.',
            'Inspection holds original flight/actor clocks in the future and steps them explicitly across both reloads. This is not uninterrupted short flight lifetime through real downtime.',
            'One outside-map fixture diagnostic and stale-audit Python failures are preserved; only the final build6/run2/audit2 is accepted. No installed deployment, save reset or final effects/audio/icons.'],
        tested=tested,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference/wake-fire-missiles-native-evidence.json';encoded=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Prepared Wake:189markers/3boots/2reloads;12original flights/lease persist;actual shared/independent contact and offline expiry;191handlers;106installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
