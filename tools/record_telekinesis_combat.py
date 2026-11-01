#!/usr/bin/env python3
"""Bind Telekinesis ordered combat, saved motion and shared-consumer regression."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import sys
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/sorceress-telekinesis-combat';BUILD=9;PARENT='75fe38d';sha=inventory.sha


def historical(name):return subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT)


def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
    counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(counts.values())==392 and ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'));from check_telekinesis_combat import configuration
    config=configuration(True);assert len(config['extra_families'])==32 and len(config['extra_checks'])==9
    for name in config['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
    for name in config['extra_checks']:assert counts[name]==3
    for name in config['extra_target_input_commands']:
        assert counts[name+'-check']==counts[name+'-result']==3
        assert log.count(' '+name+': windup')==3,name
    assert counts['telekinesis-combat-reload-result']==2 and counts['monster-return-result']==1
    for text in ('TK source/native:rolls=7680 deliveries=2560 deaths=864 pushes=848','Ordered monster release:1616native complete-source compositions','NPC return:60488complete-source policies/exact traces','Poison Dagger contact:'):
        assert text in log,text
    saved=json.loads((STAGE/'telekinesis-combat-fixtures.json').read_text());assert saved['Generation']==2 and saved['Motion'] is None and saved['Mana']==55552 and saved['Life']==12799180
    for kind in ('content','integration'):
        text=(STAGE/f'{kind}-build-{BUILD}.log').read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
    focused=json.loads((STAGE/'diagnostic-9/birth-inputs.json').read_text());focused_log=(STAGE/'diagnostic-9/birth-native-checks.log').read_text()
    assert focused['artifacts']==tested['artifacts'] and sum(markers(STAGE/'diagnostic-9/birth-native-checks.log').values())==71
    assert focused_log.count('Shutting down')==focused_log.count('Writing world save snapshot done')==3 and ' ERR]' not in focused_log and ': failed' not in focused_log
    for path,digest in focused['sourceInputs'].items():assert sha(ROOT/path)==digest,path
    prior=json.loads((ROOT/'.local/class-skills/sorceress-telekinesis-world/birth-inputs.json').read_text());assert focused['copiedData']==tested['copiedData']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][name]==prior['artifacts'][name]
    assert tested['copiedData'].keys()==prior['copiedData'].keys()
    assert {p for p in tested['copiedData'] if tested['copiedData'][p]!=prior['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    sources=[]
    for name,expected in (('telekinesis-combat-source-evidence.json',{'cases':7680}),('telekinesis-motion-source-evidence.json',{'modeCases':192,'eventCases':68})):
        p=ROOT/'tools/reference'/name;r=json.loads(p.read_text());sources.append(p)
        assert r['referenceVersion']=='3.3.93847' and r['sanitizers']=='ASan/UBSan clean'
        for key,value in expected.items():assert r[key]==value
        for path,digest in r['artifacts'].items():assert sha(ROOT/path)==digest,path
        for path,digest in r['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/path)==digest,path
    old_source=ROOT/'tools/reference/monster-return-source-evidence.json';old=json.loads(old_source.read_text());assert old_source.read_bytes()==historical(str(old_source.relative_to(ROOT)))
    for variant in ('classic','ubers'):assert sha(STAGE/'source-inputs'/(variant+'.json'))==old['artifacts']['.local/class-skills/monster-return/'+variant+'.json']
    parent=ROOT/'tools/reference/telekinesis-world-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
    now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());before=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
    changed=next(x for x in now['skills'] if x['talentId']=='sorceress.telekinesis');previous=next(x for x in before['skills'] if x['talentId']=='sorceress.telekinesis');assert changed['verification']==previous['verification']=='partial-native-coverage';previous['remaining']=changed['remaining'];assert now==before and not now['completedClasses']
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json');assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text()
    fingerprints=[]
    for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        audit=json.loads((STAGE/name).read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text());assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in audit['InputHashes'].items():assert sha(ROOT/path)==digest,path
        previous=json.loads(historical('tools/reference/'+name));ignore=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
        assert {k:v for k,v in audit.items() if k not in ignore}=={k:v for k,v in previous.items() if k not in ignore};fingerprints.append(len(audit['InputHashes']))
    assert fingerprints==[891,21]
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
    text=(STAGE/f'checks-{BUILD}.log').read_text()
    for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert message in text
    assert 'Traceback' not in text
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    files=[Path(__file__).resolve(),ROOT/'tests/check_telekinesis_combat.py',ROOT/'tools/probe_telekinesis_combat.py',ROOT/'tools/probe_telekinesis_motion.py',old_source,parent,*sources,ROOT/'tools/build_skill_roadmap.py',
           *(ROOT/'docs'/name for name in ('sorceress-telekinesis-combat-checkpoint.md','sorceress-enchant-preflight.md','sorceress-telekinesis-world-checkpoint.md','sorceress-telekinesis-preflight.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
           *(STAGE/name for name in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','telekinesis-combat-fixtures.json','diagnostic-9/birth-inputs.json','diagnostic-9/birth-native-checks.log','diagnostic-run-9.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
           *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Telekinesis ordered NPC combat and saved knockback',installed=False,graphicalClientVerified=False,completedClasses=[],
                verified=dict(nativeMarkers=392,focusedMarkers=71,boots=3,reloads=2,saves=3,newServerWireActions=6,retainedServerWireActions=84,nativeSourceRollComparisons=7680,nativeNpcCompositions=2560,nativeDeaths=864,nativeKnockbackRequests=848,rankGoldens=255,sourceCompositions=7680,sourceMotionCases=192,sourceEventDispatchCases=68,sharedNpcReturnPolicies=60488,sharedOrderedMeleeCompositions=1616,fixtureFamilies=32,sharedChecks=9,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
                markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
                established=['Complete older do21/roll/typed/mitigation/event-health/NPC-return bodies compose in7680cases;192mode entry/completion and68frozen event cases pass sanitizers. Native comparisons include7680rolls/2560real HP/return deliveries/864deaths/848KBrequests,plus255independent rank goldens.',
                             'The shared prepared kernel adds explicit missile delivery with mitigation after attacker bookkeeping and no melee presentation/attempt/Thorns; default melee retains its path. Source NPC return,defender thresholds/RNG,no timed stun,immune KB,mastery/real gear/typed potion and no factory Slow Missiles payload are scoped.',
                             'Prepared trajectories reuse source motion/terrain/FHR with optional V0 freeze-suspension policy and ordinary post-KB class160 GH. Historical records retain their policy. Two actual saved-recipient resumes and natural recovery expiry preserve wounds/resources; the second path comes from another real paid cast without rewinding or restoring.',
                             'Actual right/quick input,world interaction regression,60488shared NPC return policies,1616ordered melee compositions and real Poison Dagger/native-notification checks pass in one cohort. Four fresh audits/71Python/573named/240catalogue checks preserve decisions and106installed hashes. Earlier fixture diagnostics/binaries are retained;no deployment/class certificate.'],
                remaining=['Player return,uninterruptible/death-delay,Sand Leaper AI completion,zero-vector KB and unadapted incoming native modifiers explicitly refuse before payment;they do not silently fall back to generic damage or a one-tile push.',
                           'Full modern/global actor seed allocation,initial/path/freeze/animation cadence,source regen rescheduling,arbitrary kill/absorb/missile subscribers,special/PvP/Sunder recipients,graphical input and actual old-DLL/unheld concurrent saves remain unverified.',
                           'Portals/gates/waypoints/shrines still need explicit world/travel mappings;effects/audio and tree/hotkey icons stay separately pending. Continue remaining Sorceress contracts;Inferno learned channel input/payment remains individually unfinished.'])
    output=ROOT/'tools/reference/telekinesis-combat-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Telekinesis combat:392combined/71focused markers,three boots/two reloads,7680roll/2560native packet compositions,six real inputs;shared melee retained,four audits/71Python;installed artifacts preserved;remaining recipients/world/modern/client/assets explicit.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
