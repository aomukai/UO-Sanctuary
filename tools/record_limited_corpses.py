#!/usr/bin/env python3
"""Bind prepared completed-death Sentry ownership to exact native test inputs."""
import argparse
import json
import subprocess
import sys
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/limited-corpses'
PARENT='cdbaa9a81bfbb12bc5c03b5fdf345a269f8b7a83'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE
    tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-build-5.json',STAGE/'birth-inputs.json')
    sys.path.insert(0,str(ROOT/'tests'))
    from check_limited_corpses import configuration
    config=configuration()
    assert len(config['extra_families'])==7 and len(config['extra_checks'])==2
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==39
    assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    for name in config['extra_families']:
        assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2,name
    for name in config['extra_checks']:
        assert log.count(name+': passed')==3,name
    assert log.count('limited-corpse-result: passed')==1 and log.count('limited-corpse-reload-result: passed')==2
    saved=json.loads((STAGE/'limited-corpse-fixtures.json').read_text())
    assert saved['Generation']==2 and len(saved['Pool']['Entries'])==50
    assert sum(row['Actor']==saved['Actor'] for row in saved['Pool']['Entries'])==1
    assert saved['Body']['Corpse'] and saved['Body']['Control']['Mode']==0
    assert saved['Body']['Control']['Sequence'] is None
    assert saved['Bolt']['Actor']==saved['Actor'] and saved['Bolt']['Owner']==saved['Owner'] and saved['Bolt']['Frame']>=4
    audit=json.loads((STAGE/'audit-inputs.json').read_text())
    assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
    assert audit['sourceInputs']=={p:h for p,h in tested['sourceInputs'].items() if p.endswith('.cs')}
    audit_log=(STAGE/'audit-native-checks.log').read_text()
    assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count(': passed')==2
    for name,count in [('named-item-runtime-audit.json',914),('first-loop-affix-audit.json',21)]:
        current=json.loads((STAGE/name).read_text())
        assert current==json.loads((ROOT/'tools/reference'/name).read_text()) and len(current['InputHashes'])==count
        assert current['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in current['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT))
        ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in ignore}=={k:v for k,v in old.items() if k not in ignore}
    for kind in ('content','integration'):
        text=(STAGE/(kind+'-build-5.log')).read_text()
        assert all(word in text for word in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    diagnostic=STAGE/'diagnostic-flight-clock-1'
    old=(diagnostic/'birth-native-checks.log').read_text()
    assert 'Already-issued corpse-owned bolts did not damage' in old
    assert sha(diagnostic/'content/Sanctuary.Content.dll')==sha(STAGE/'content/Sanctuary.Content.dll')
    prior=json.loads((diagnostic/'birth-inputs.json').read_text())
    assert sha(diagnostic/'LimitedCorpseChecks.cs')==prior['sourceInputs']['tests/Sanctuary.Integration/LimitedCorpseChecks.cs']
    assert sha(diagnostic/'integration/Sanctuary.Integration.dll')==prior['artifacts']['Assemblies/Sanctuary.Integration.dll']
    text=(STAGE/'python-tests.log').read_text()
    assert 'Ran 71 tests' in text and '\nOK\n' in text
    text=(STAGE/'catalogue-checks.log').read_text()
    for word in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):
        assert word in text
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==187
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    previous=json.loads((ROOT/'.local/class-skills/blade-cast/birth-inputs.json').read_text())
    for path in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert previous['artifacts'][path]==tested['artifacts'][path]
    changed={p for p,h in tested['sourceInputs'].items() if p.startswith(('src/','shared/')) and previous['sourceInputs'].get(p)!=h}
    assert changed=={'src/Sanctuary.Content/'+n for n in ('LimitedSummonCorpses.cs','AssassinSentryBody.cs','AssassinSentryBolts.cs')}
    assert {p for p,h in tested['copiedData'].items() if previous['copiedData'].get(p)!=h}=={'Data/Sanctuary/Balance/limited-summon-corpses.json'}
    files=[Path(__file__).resolve(),ROOT/'tests/check_limited_corpses.py',ROOT/'tools/corroborate_local_dataguide.py',
        *(ROOT/'tools/reference'/name for name in ('d2r-local-dataguide-evidence.json','sentry-death-entry-source-evidence.json','sentry-dead-damage-source-evidence.json')),
        *(ROOT/'docs'/name for name in ('sentry-corpse-pool-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in ('pre-build-5.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','limited-corpse-fixtures.json','content-build-5.log','integration-build-5.log','audit-inputs.json','audit-native-checks.log','audit-run-2.log','python-tests.log','python-tests-stale-evidence.log','catalogue-checks.log'))]
    files.extend(diagnostic/name for name in ('LimitedCorpseChecks.cs','birth-inputs.json','birth-native-checks.log','native-run-1.log','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll'))
    files.extend(config['extra_files'].values())
    data=dict(referenceVersion='3.3.93847',parent=PARENT,
        status='Prepared completed-death ownership only; public Charged Bolt Sentry input remains unavailable.',
        verified=dict(nativeCommandMarkers=39,boots=3,reloads=2,saves=3,originalFlightResumes=2,corpseCapacity=50,flaggedSpecies=15,auditMarkers=2,pythonTests=71,handlerCount=187,unchangedInstalledArtifacts=106),
        limits=['The native test supplies completed-death context. Natural exhaustion/replacement, DT/DD dispatch, exact modern corpse callback/eviction ordering and room-unload behavior are not established.',
        'Other flagged families have rules but no automatic death integration. Synthetic inert fixture actors test shared ordering; this does not certify their real death paths.',
        'Reload flight clocks are held and explicitly rearmed. Current charged-bolt motion, placement, full owner/item/event/PvP and client presentation remain separate acceptance work.',
        'The failed first flight observation used a future fixture clock; the correction leaves production content byte-identical. The initial Python run rejected stale evidence; fresh exports retain every audit decision.',
        'No new public handler, completed class, installed deployment, save reset or final artwork.'],
        tested=tested,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference/limited-corpse-native-evidence.json'
    text=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==text
    else:out.write_text(text)
    print('Prepared dead Sentry ownership:39 markers/3 boots/2 reloads;shared50 pool;71 Python;106 installed artifacts unchanged.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
