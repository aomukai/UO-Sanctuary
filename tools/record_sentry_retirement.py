#!/usr/bin/env python3
"""Record natural CBS retirement with bounded native/source acceptance."""
import argparse,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sentry-retirement'
PARENT='71f234d86888458867f350d1a1bb010e1dc9899f'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-build-2.json',STAGE/'birth-inputs.json')
    sys.path.insert(0,str(ROOT/'tests'))
    from check_sentry_retirement import configuration
    config=configuration();assert len(config['extra_families'])==7 and len(config['extra_checks'])==2
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==39
    assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    for name in config['extra_families']:
        assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2,name
    for name in config['extra_checks']:assert log.count(name+': passed')==3,name
    assert log.count('sentry-retirement-result: passed')==1 and log.count('sentry-retirement-reload-result: passed')==2
    saved=json.loads((STAGE/'sentry-retirement-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['NaturalState']['Corpse'] and saved['ReplacementState']['Corpse']
    assert saved['NaturalState']['Retirement'] is None and saved['ReplacementState']['Retirement'] is None
    assert saved['NaturalState']['Control']['Dispatches']==5 and saved['NaturalState']['Control']['Ai']['RemainingShots']==0
    assert saved['Bolt']['Actor']==saved['Replacement'] and saved['Bolt']['Owner']==saved['Owner'] and saved['Bolt']['Frame']>=4
    assert saved['NaturalState']['Owner']==saved['ReplacementState']['Owner']==saved['Owner']
    audit=json.loads((STAGE/'audit-inputs.json').read_text())
    assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
    assert audit['sourceInputs']=={p:h for p,h in tested['sourceInputs'].items() if p.endswith('.cs')}
    text=(STAGE/'audit-native-checks.log').read_text()
    assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==2
    for name,count in [('named-item-runtime-audit.json',915),('first-loop-affix-audit.json',21)]:
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in current['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in ignore}=={k:v for k,v in old.items() if k not in ignore}
    for kind in ('content','integration'):
        text=(STAGE/(kind+'-build-2.log')).read_text()
        assert all(word in text for word in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    text=(STAGE/'catalogue-checks.log').read_text()
    for word in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert word in text
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==187
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    prior=json.loads((ROOT/'.local/class-skills/limited-corpses/birth-inputs.json').read_text())
    for path in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][path]==tested['artifacts'][path]
    assert tested['copiedData']==prior['copiedData']
    changed={p for p,h in tested['sourceInputs'].items() if p.startswith(('src/','shared/')) and prior['sourceInputs'].get(p)!=h}
    assert changed=={'src/Sanctuary.Content/'+name for name in ('AssassinSentryBody.cs','AssassinSentryBolts.cs','AssassinSentryRetirement.cs','AssassinTrapRoster.cs')}
    files=[Path(__file__).resolve(),ROOT/'tests/check_sentry_retirement.py',ROOT/'tests/check_limited_corpses.py',
        *(ROOT/'docs'/name for name in ('sentry-retirement-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(ROOT/'tools/reference'/name for name in ('sentry-death-clock-source-evidence.json','d2r-local-sentry-animation-evidence.json','d2r-local-dataguide-evidence.json')),
        *(STAGE/name for name in ('pre-build-2.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','sentry-retirement-fixtures.json','content-build-2.log','integration-build-2.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','python-tests.log','catalogue-checks.log'))]
    files.extend(config['extra_files'].values())
    data=dict(referenceVersion='3.3.93847',parent=PARENT,
        status='Natural CBS exhaustion and cap replacement use the identified completed-death profile; public learned input remains gated.',
        verified=dict(nativeCommandMarkers=39,boots=3,reloads=2,saves=3,actualNaturalVolleys=5,deathFrames=23,originalFlightResumes=2,sourceClockContexts=90,auditMarkers=2,pythonTests=71,handlerCount=187,unchangedInstalledArtifacts=106),
        limits=['Pool registration uses the prepared completed-death boundary. Exact modern insertion phase, same-frame callback/eviction order and room-unload behavior are unverified.',
        'Current owned animation metadata and reconstructed source composition supply the timing profile; the complete current mode/rate/executable engine is not executed.',
        'Natural connected exhaustion and damage during death use ordinary timers. Reload flight inspection clocks are held/rearmed; no uninterrupted flight-lifetime-through-downtime claim.',
        'Only Charged Bolt Sentry gains this natural death adapter. Other trap/summon families retain their separate retirement paths.',
        'Learned input, spawn-search fallback and occupied size-zero placement, full modern motion/owner/item/event/PvP/client behavior and final collapse art/audio remain acceptance work.',
        'No class completion, new public handler, installed deployment, save reset or final icons.'],
        tested=tested,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference/sentry-retirement-native-evidence.json';text=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==text
    else:out.write_text(text)
    print('Natural CBS retirement:39 native markers/3 boots/2 reloads;5 volleys/23 death frames;71 Python;106 installed artifacts unchanged.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
