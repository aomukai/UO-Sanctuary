#!/usr/bin/env python3
"""Bind approved Mage Glacial Spike and shared flight/freeze consumers checks."""
import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/mage-glacial'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {n: inventory.validate_inventory(STAGE / (n + '-inputs.json')) for n in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts'] and value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/mage-chain-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ['MageOffensiveSkills','NecromancerMageMissiles','SkillCombat','SorceressCombat']}|{'tests/Sanctuary.Integration/'+n+'.cs' for n in ['MageWorldChecks','MageMissileChecks']}
    added={'tests/Sanctuary.Integration/MageGlacialChecks.cs','src/Sanctuary.Content/GlacialSpikes.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    data='Data/Sanctuary/Balance/necromancer-summons.json'
    assert {p for p in old['copiedData'] if old['copiedData'][p]!=inputs['birth']['copiedData'][p]}=={data}
    assert inputs['birth']['copiedData'].keys()==old['copiedData'].keys()
    # Only append the exact pinned missile row; preserve every prior row.
    import subprocess
    prior_data=json.loads(subprocess.check_output(['git','show','f50cd9a:data/Sanctuary/Balance/necromancer-summons.json'],cwd=ROOT))
    current_data=json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text())
    assert current_data[:-1]==prior_data and current_data[-1]['key']=='glacialspike'
    row=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/missiles.json').read_text())['glacialspike']
    assert current_data[-1]['fields']=={k:v for k,v in row.items() if isinstance(v,(int,str)) and not k.startswith('*') and k not in ['lineNumber','Missile']}
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/mage-chain/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    expected['mage-glacial-check']+=1;expected['mage-glacial-reload-check']+=2
    expected['freeze-state-check']+=3
    expected['mage-glacial-live-result']+=3;expected['glacial-player-live-result']+=3
    assert sum(expected.values()) == 250 and markers(STAGE / 'birth-native-checks.log') == expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('AI67:1598 complete source ordered-service/state/draw comparisons') == 3
    assert log.count('Mage target services:actual learned actor,distinct supplied ranged primary,15/16and19/20boundaries') == 3
    assert log.count('Mage Glacial:hard9+real soft1 refusal/hard10 fixed10')==1
    assert log.count('Freeze state:12096 application,1296 removal,100 callback and224 length source/native cases passed')==3
    assert log.count('Mage Static:44800 one-draw/exclusive five-percent policy contexts')==1
    assert log.count('Mage buffs:hard19+real soft1 refuses;live hard20 unlock/respec/relearn')==1
    assert log.count('Mage world:ranged AI1,S1end24/think25,A1kind2action35/end45') == 1
    assert log.count('4096 complete selector and2160 complete shared charged-bolt') == 3
    assert log.count('4032 complete source compositions') == 3
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ['content', 'integration']:
        log = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in log for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    for name, count in [('named-item-runtime-audit.json', 874), ('first-loop-affix-audit.json', 21)]:
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text()) and len(audit['InputHashes']) == count
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for source, digest in audit['InputHashes'].items():
            assert sha(ROOT / source) == digest
    assert markers(STAGE / 'audit-native-checks.log') == Counter({'paladin-thorns-check': 1, 'paladin-thorns-result': 1, 'named-gear-check': 1, 'first-loop-catalogue-check': 1})
    log = (STAGE / 'audit-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down') == 1
    log = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in log and '\nOK\n' in log
    assert '573 definitions accounted for' in (STAGE / 'named-readiness.log').read_text()
    assert 'Eight classes, 240 source skills' in (STAGE / 'catalogue-check.log').read_text()
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 186 and sum(x['characterClass'] == 'Necromancer' for x in handlers) == 30 and not status['completedClasses']
    docs = ['mage-glacial-checkpoint', 'skeletal-mage-upgrade-plan', 'pet-adjustment-review', 'class-skill-reuse-notes', 'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons']
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_mage_glacial.py', *[ROOT / p for p in changed | added], *[ROOT / f'docs/{n}.md' for n in docs], ROOT / 'tools/reference/sentry-target-source-evidence.json', ROOT/'tools/reference/tornado-pulse-source-evidence.json', ROOT/'tools/build_class_skills.py', ROOT/'data/Sanctuary/Balance/necromancer-summons.json']
    names = [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log', f'run-{run}.log', f'audit-run-{audit_run}.log', 'pre-build-3.json','content-build-3.log','integration-build-3.log','run-3.log','audit-run-3.log','attempt-3-birth-inputs.json','attempt-3-birth-native-checks.log','attempt-3-audit-inputs.json','attempt-3-audit-native-checks.log','pre-build-2.json','content-build-2.log','integration-build-2.log','run-2.log','audit-run-2.log','attempt-2-birth-inputs.json','attempt-2-birth-native-checks.log','attempt-2-audit-inputs.json','attempt-2-audit-native-checks.log','attempt-1-birth-inputs.json','attempt-1-birth-native-checks.log','attempt-1-audit-inputs.json','attempt-1-audit-native-checks.log','audit-run-1.log','pre-build-1.json','content-build-1.log','integration-build-1.log','run-1.log','birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'audit-native-checks.log', 'python-tests.log', 'named-readiness.log', 'catalogue-check.log', 'fire-golem-world-fixtures.json', 'fire-golem-death-fixtures.json', 'blood-golem-world-fixtures.json', 'clay-golem-world-fixtures.json', 'skeleton-world-fixtures.json', 'mage-glacial-fixtures.json','chain-lightning-checks.json','mage-chain-fixtures.json','mage-static-fixtures.json','mage-pet-buff-fixtures.json','sorceress-skills-checks.json','enchant-checks.json','mage-world-fixtures.json', 'sentry-unit-path-fixture.json', 'mage-birth-fixtures.json', 'mage-missile-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json']
    files += [STAGE / name for name in names]
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    reviewed={}
    for relative,name in [('source/D2Game/src/MISSILES/MissMode.cpp','MISSMODE_SrvHit13_GlacialSpike_HellMeteorDown')]:
        source=ROOT/'.local/class-skills/d2moo-pinned-source'/relative;raw=source.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        reviewed[name]=hashlib.sha256(function(raw.decode(),name).encode()).hexdigest();files.append(source)
    files.append(ROOT/'.local/class-skills/freeze-state-source-probe.json')
    result = dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],reviewedNotExecuted=reviewed,component='Approved Mage fixed10 Glacial Spike proc through shared saved Mage flight and NPC/player freeze-before-damage consumers', installed=False, goalComplete=False, completedClasses=[], build=build, run=run, auditRun=audit_run,
        verified=dict(nativeMarkers=250,retainedMageChainStaticBuffPlayerPetSentryMarkers=238,boots=3,actualReloads=2,saves=3,mageProcPolicyContexts=44800,naturalMageGlacialSessions=3,naturalPlayerGlacialSessions=3,naturalMageChainSessions=3,naturalMageStaticSessions=3,playerChainAsyncMarkers=3,learnedMageInputCasts=6,freezeApplicationSourceCasesPerBoot=12096,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[874,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / (n + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (n + '-inputs.json'))) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)}, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'Ice Mage hard10 gains its original five-percent fixed10 Glacial slot,one actor proc draw,no basic bolt and unchanged owner/NPC mana. Actual hard9+Wormskull soft1 refuses. Fixed NPC Sorceress values have no owner mastery,synergy or potion enhancement.',
            'Existing saved Mage do1 controller,source-step/collision,Slow Missiles,independent missile packet and actor critical streams are reused. Optional Spell defaults to None for historical/basic JSON; Glacial uses pinned Vel16/range40/freeze77. Restore validates temporary maps before replacing all flights/seeds.',
            'Glacial prepares one raw area packet and reuses per-recipient Mage avoidance/critical/mitigation/recovery. Freeze is applied before HP in both NPC and player consumers so existing corpse rules see lethal freeze. Player ordinary hit area now shares one raw packet; its existing private flight remains separate.',
            'Direct delayed area/recipient-copy tests,active terminal expiry/wall removal and native occlusion,lethal NPC/player freeze ordering,captured Slow Missiles,offline no-impact/no-RNG expiry and atomic invalid spell restore pass. Held packets,phase,owner/actor/seed and wounds survive two reloads; copied collision resumes without advancing the inspected original.',
            'Three natural Mage and three natural Sorceress releases damage/freeze multiple targets through actual travel. All238 predecessor markers plus full freeze controls pass. The only runtime-data delta is the exact appended pinned glacialspike row. Audit decisions,kernel and106installed hashes remain unchanged.'],
        remaining=[
            'Exact modern source path/InitSteps1/room/size/target enumeration/filter/seed/event/NHD,special/PvP/Sunder and caster-removal/client behavior remain open. Legacy hit13 is statically reviewed,not a newly executed current engine proof. Intrinsic Mage critical transport is inherited,not newly certified for modern skill55.',
            'NPC terminal wall/expiry routes use the shared explosion. Active expiry damages/freezes; wall removal/native area LOS occlusion pass. Native center-inside-wall rejects recipients; exact source room/LOS remains open. The player private SkillField still stops without exploding at wall/expiry and is not saved/source-exact; ordinary-hit consumer checks do not certify that flight or full player casting input.',
            'Historical plain Mage AI rollout,three-element/poison-to-ice migration,basic Sorceress spells/hard5triples,the recorded Skeleton Mastery mapping and fixed Meteor/cooldown binding remain required. Static,Chain and Glacial are available; unbound Meteor remains unavailable.',
            'Held state/controlled copied resumption does not prove unheld concurrent saves or actual installed old-build upgrade. Corpse tests check lethal absence-or-shatter marker,not complete loot/corpse render behavior. Other approved pets,safe parking and barrier/reference phases remain queued.',
            'No final effects/audio/icons,new player binding,deployment or completed class. Identified travel/explosion placeholders share eventual Sorceress Glacial assets.'])
    output = ROOT / 'tools/reference/mage-glacial-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Mage Glacial:250markers/3boots/2reloads,3natural NPC/player sessions;238retained,4audits/71Python/installed unchanged. Package remains unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
