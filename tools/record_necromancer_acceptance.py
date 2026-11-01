#!/usr/bin/env python3
"""Bind the full combined Necromancer run, preserving its bounded acceptance scope."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess

import record_grizzly_body as inventory
from record_necromancer_mechanics import record as mechanics
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/necromancer-acceptance'
MECHANICS=ROOT/'.local/class-skills/necromancer-acceptance-mechanics'
sha=inventory.sha


def record(check=False):
    mechanics(check=True)
    inventory.STAGE=STAGE
    inputs={name:inventory.validate_inventory(STAGE/(name+'-inputs.json')) for name in ('birth','audit')}
    for name,row in inputs.items():
        snapshot(STAGE/'pre-build-10.json',STAGE/(name+'-inputs.json'))
        assert row['artifacts']==inputs['birth']['artifacts'] and row['copiedData']==inputs['birth']['copiedData']
    diagnostic=inventory.validate_inventory(MECHANICS/'birth-inputs.json')
    assert diagnostic['artifacts']==inputs['birth']['artifacts'] and diagnostic['copiedData']==inputs['birth']['copiedData']
    log=(STAGE/'birth-native-checks.log').read_text();counts=markers(STAGE/'birth-native-checks.log')
    assert sum(counts.values())==845 and ' ERR]' not in log and ': failed' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/'run-6.log').read_text()
    enchant_reloads=re.findall(r'Mage buff reload retains original Enchant payload/deadline;expired=(True|False)',log)
    assert len(enchant_reloads)==2
    # The diagnostic already binds the same compiled fixture bodies. Full run
    # must retain every diagnostic check and add actual live/input results.
    expected=markers(MECHANICS/'birth-native-checks.log')
    import sys
    sys.path.insert(0,str(ROOT/'tests'))
    from check_necromancer_acceptance import combined
    config=combined()
    assert len(config['extra_live_commands'])==34 and len(config['extra_target_input_commands'])==46
    for name in config['extra_live_commands']:expected[name+'-result']+=3
    for name in config['extra_target_input_commands']:
        expected[name+'-check']+=3;expected[name+'-result']+=3
    assert counts==expected
    prior_path=ROOT/'tools/reference/confuse-resume-native-evidence.json'
    prior=json.loads(prior_path.read_text());old_log=ROOT/'.local/class-skills/confuse-resume/birth-native-checks.log'
    assert sha(old_log)==prior['artifacts'][str(old_log.relative_to(ROOT))]
    old=markers(old_log);assert sum(old.values())==411
    old['necromancer-acceptance-save']=old.pop('fire-golem-death-save')
    assert not (old-counts)
    for name,size in (('named-item-runtime-audit.json',887),('first-loop-affix-audit.json',21)):
        audit=json.loads((STAGE/name).read_text())
        assert audit==json.loads((ROOT/'tools/reference'/name).read_text()) and len(audit['InputHashes'])==size
        assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in audit['InputHashes'].items():assert sha(ROOT/path)==digest
        previous=json.loads(subprocess.check_output(['git','show','2e46a03:tools/reference/'+name],cwd=ROOT))
        strip=lambda d:{k:v for k,v in d.items() if k not in ('ContentAssemblyPath','ContentAssemblySha256','InputHashes')}
        assert strip(audit)==strip(previous)
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text() and ': failed' not in (STAGE/'audit-native-checks.log').read_text()
    assert 'exported from the selected build' in (STAGE/'audit-run-1.log').read_text()
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text()
    assert 'Eight classes, 240 source skills' in (STAGE/'catalogue-check.log').read_text()
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers=[row for row in status['skills'] if row['implementation']=='handler-present']
    assert len(handlers)==186 and sum(row['characterClass']=='Necromancer' for row in handlers)==30 and not status['completedClasses']
    docs=('necromancer-acceptance-checkpoint','necromancer-mechanical-audit','class-skills-resume-checkpoint','class-skill-roadmap','class-skill-reuse-notes','blind-world-checkpoint','skill-effects','skill-icons')
    files=[prior_path,old_log,ROOT/'tools/reference/necromancer-mechanics-native-evidence.json',Path(__file__).resolve(),ROOT/'tests/check_necromancer_acceptance.py',ROOT/'tests/check_iron_golem_birth.py',*[ROOT/'docs'/(name+'.md') for name in docs]]
    files += [p for p in STAGE.iterdir() if p.is_file()]
    result=dict(referenceVersion='3.3.93847',component='Combined Necromancer spells,summons,AI and actual saved input acceptance',
        installed=False,productionChanged=False,goalComplete=False,completedClasses=[],build=10,run=6,auditRun=1,
        verified=dict(nativeMarkers=845,retainedMarkers=411,mechanicsMarkers=467,observerMarkers=18,confuseInputMarkers=24,confuseResumeMarkers=14,boots=3,actualReloads=2,saves=3,
            fixtureFamilies=113,sharedChecks=21,connectedLiveActions=102,realInputActions=138,pythonTests=71,
            enchantReloads=dict(active=enchant_reloads.count('False'),expired=enchant_reloads.count('True')),
            namedDefinitions=573,auditMarkers=4,auditFingerprints=[887,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={name:dict(path=str((STAGE/(name+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(name+'-inputs.json'))) for name in inputs},
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'All467mechanics/save diagnostic markers are retained with102actual connected world actions and138right/quick input actions. The earlier411cohort remains covered;its Fire-only save command is replaced by a single coordinator preserving both Fire retirement and barrier clock/maker/birth preparation.',
            'The earlier extended blind/spell cohort now runs with current pet/parking/curse behavior. Actual two reloads verify general class armor,blindness motion,minion and bindings rather than recreating that fixture. Field pet tests retain original progression,guard,friendly,XP,offline,facet and death assertions. Corpse Explosion,original Revive,Summon Resist and barrier source-clock checks share the same three saved worlds.',
            'Both Mage buff reloads require the exact raw saved Enchant caster/rank/damage/AR/absolute deadline,and the effective consumer follows the real reload time. Active-versus-expired observations are recorded separately;no temporary buff is extended or refreshed to suit the fixture.',
            'Production C#,runtime data,kernel and installed106remain unchanged. Fixture changes reconcile approved Clay damage and safe-area behavior, preserve original assertions, expose preparation without changing its bodies,and order short-lived reload checks before unrelated slow work. Four audits/71Python/573named/eight-class240pass with887/21fresh fingerprints and unchanged decisions.'],
        remaining=[
            'A passing combined cohort is not proof of every current-engine/host adapter. Barrier linked death/collision/removal and empty-ground acquisition,source event/global RNG/geometry/room/PvP/Sunder/credit/unmapped species and legacy reconstruction remain documented individually. No complete class or global goal certificate is inferred.',
            'Graphical-client and final assets remain separate:effects/audio and talent/hotkey icons use identified placeholders. No deployment or installed-save mutation occurred. Continue the class roadmap with explicit nonblocking parity questions and remaining mechanical work.'])
    output=ROOT/'tools/reference/necromancer-acceptance-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Necromancer combined:845markers/2reloads,102live+138input actions;467mechanics and411prior scopes retained;class gaps remain explicit.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
