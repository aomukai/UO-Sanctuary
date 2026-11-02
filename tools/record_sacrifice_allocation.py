#!/usr/bin/env python3
"""Bind Sacrifice start allocation and retained mechanics to immutable native evidence."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import sys
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sacrifice-allocation'
PARENT='d56ee02ecddfc7ba3c69ce86016c59af7bcdbe39'
sha=inventory.sha

def historical(name):
    return subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT)

def successful(path,total,boots):
    log=path.read_text();counts=markers(path)
    assert sum(counts.values())==total and ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==boots
    return log,counts

def record(check=False):
    inventory.STAGE=STAGE
    tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-build-3.json',STAGE/'birth-inputs.json')
    log,counts=successful(STAGE/'birth-native-checks.log',806,3)
    assert 'pass across3boots/2reloads' in (STAGE/'full-run-3.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'))
    from check_sacrifice_allocation import configuration
    cfg=configuration(True)
    assert len(cfg['extra_families'])==69 and len(cfg['extra_checks'])==11 and len(cfg['extra_target_input_commands'])==69
    for name in cfg['extra_families']:
        assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
    for name in cfg['extra_checks']:assert counts[name]==3,name
    for name in cfg['extra_target_input_commands']:
        assert counts[name+'-check']==counts[name+'-result']==3,name
        assert log.count(' '+name+': windup')==3,name
    assert 'out-of-range cost;movement/weapon/stop cancellation;held target identity and once consumption' in log
    focused=STAGE/'focused-accepted-3'
    prior=inventory.validate_inventory(focused/'birth-inputs.json')
    assert prior['artifacts']==tested['artifacts'] and prior['copiedData']==tested['copiedData']
    successful(focused/'birth-native-checks.log',33,3)
    for part in ('content','integration'):
        name=part+'/Sanctuary.'+part.title()+'.dll'
        assert sha(STAGE/name)==sha(focused/name)
        text=(STAGE/(part+'-build-3.log')).read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
    negative=STAGE/'negative-release-time'
    text=(negative/'birth-native-checks.log').read_text()
    assert 'sacrifice-allocation-check: failed' in text
    assert 'Sacrifice must allocate hit and physical damage at start without committing either life' in text
    ni=json.loads((negative/'birth-inputs.json').read_text())
    for name in ('PaladinSacrifice.cs','ClassCombat.cs','PaladinCombat.cs'):
        assert (negative/name).read_bytes()==historical('src/Sanctuary.Content/'+name)
        assert sha(negative/name)==ni['sourceInputs']['src/Sanctuary.Content/'+name]
    assert sha(negative/'content/Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
    def snapshot_body(path):
        text=path.read_text();return text[text.index('    private static void Snapshot()'):text.index('    private static void LiveCap()')]
    assert snapshot_body(negative/'SacrificeAllocationChecks.cs')==snapshot_body(ROOT/'tests/Sanctuary.Integration/SacrificeAllocationChecks.cs')
    source=ROOT/'.local/class-skills/SkillPal.cpp.reference'
    assert sha(source)=='c421857a633b5dd1f441eded1c5f41cbd88b375ba9dd771c3fa802e39591bdf0'
    probe=ROOT/'.local/class-skills/sacrifice-source-probe.json'
    old=json.loads((ROOT/'tools/reference/sacrifice-engine-evidence.json').read_text())
    assert json.loads(probe.read_text())==old['probe']['data']
    assert sha(STAGE/'native/Distribution/Server.dll')==tested['artifacts']['Server.dll']
    for path in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        prior=json.loads((ROOT/'.local/class-skills/paladin-baseline/birth-inputs.json').read_text())
        assert prior['artifacts'][path]==tested['artifacts'][path]
    current=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    previous=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
    changed=[]
    for now,before in zip(current['skills'],previous['skills'],strict=True):
        if now!=before:
            assert now['talentId']=='paladin.sacrifice'
            changed.append(now['talentId']);before['remaining']=now['remaining']
    assert changed==['paladin.sacrifice'] and current==previous and current['completedClasses']==[]
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json')
    assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text()
    fingerprints=[]
    for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        report=json.loads((STAGE/name).read_text())
        assert report==json.loads((ROOT/'tools/reference'/name).read_text())
        assert report['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in report['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(historical('tools/reference/'+name));ignored={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in report.items() if k not in ignored}=={k:v for k,v in old.items() if k not in ignored}
        fingerprints.append(len(report['InputHashes']))
    assert fingerprints==[905,21]
    text=(STAGE/'python-tests.log').read_text()
    assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
    text=(STAGE/'catalogue-checks.log').read_text()
    for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):
        assert message in text
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest,name
    files=[Path(__file__).resolve(),ROOT/'tests/check_sacrifice_allocation.py',source,probe,
        *(ROOT/'docs'/name for name in ('paladin-sacrifice-allocation-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in ('pre-build-3.json','birth-inputs.json','birth-native-checks.log','full-run-3.log','sacrifice-checks.json','audit-inputs.json','audit-native-checks.log','audit-run-3.log','python-tests.log','catalogue-checks.log','content-build-3.log','integration-build-3.log')),
        *(focused/name for name in ('birth-inputs.json','birth-native-checks.log','focused-run-3.log','sacrifice-checks.json')),
        *(negative/name for name in ('birth-inputs.json','birth-native-checks.log','PaladinSacrifice.cs','ClassCombat.cs','PaladinCombat.cs','SacrificeAllocationChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
        *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json','paladin-baseline-native-evidence.json'))]
    result=dict(referenceVersion='3.3.93847',component='Sacrifice learned start allocation and impact life cap',installed=False,graphicalClientVerified=False,completedClasses=[],
        verified=dict(nativeMarkers=806,focusedMarkers=33,boots=3,reloads=2,saves=3,newServerWireActions=9,retainedServerWireActions=198,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Previous content DLL fails the unchanged start-allocation regression. Learned windup now captures hit/miss and shared prepared weapon damage;release does not reroll or re-mitigate after resistance changes.',
        'Self-cost reads current fractional target life at impact,after allocation and before contact recheck. Miss,cancellation,self-death,held target identity and once consumption are covered. Original dedicated Sacrifice resource/weapon/rank/absorb/save tests survive both reloads.',
        'Nine actual learned left/right/quick wire actions pass with zero mana and exact once-only target/self-life changes. Combined predecessor checks and audits retain their original scope;no other skill completion or deployment is inferred.'],
        remaining=['Current target delivery still uses the compatibility GearEffects.Strike path. Migrate contact/notification/HP/kill/attempt/return through shared ordered player-to-NPC services;native immediate death currently precedes some return events.',
        'Supplementary older start29/do64 source is not proof of modern executable parity. Source geometry,RNG,item registration order,complete curse/leech/LifeTap/retaliation modes,PvP/charged/oskill,unheld pending-action saves and old-DLL migration remain separate acceptance work.',
        'Final blood/contact/audio and talent/hotkey assets remain separately tracked placeholders.106installed artifacts and existing saves remain unchanged.'])
    path=ROOT/'tools/reference/sacrifice-allocation-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Sacrifice allocation:806combined/33focused markers,9new/198retained wire actions,2reloads,4audits/71Python;106installed unchanged;ordered target delivery remains open.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
