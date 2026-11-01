#!/usr/bin/env python3
"""Bind approved Mage Meteor and shared warning/field consumers checks."""
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
STAGE = ROOT / '.local/class-skills/mage-meteor'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {n: inventory.validate_inventory(STAGE / (n + '-inputs.json')) for n in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts'] and value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/mage-glacial-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ['FireGround','ImmolationArrows','MageOffensiveSkills','MartialPhoenixPackets','NecromancerMageMissiles']}
    added={'src/Sanctuary.Content/MageMeteors.cs','tests/Sanctuary.Integration/MageMeteorChecks.cs'}
    assert before.keys()<=after.keys() and after.keys()-before.keys()==added
    assert {p for p in before if before[p]!=after[p]}==changed
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/mage-glacial/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    expected['mage-meteor-check']+=1;expected['mage-meteor-reload-check']+=2
    expected['fire-ground-check']+=1;expected['fire-ground-reload-check']+=2
    expected['fire-ground-result']+=1;expected['fire-ground-reload-result']+=2
    expected['mage-meteor-live-result']+=3
    assert sum(expected.values())==262 and markers(STAGE/'birth-native-checks.log')==expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('AI67:1598 complete source ordered-service/state/draw comparisons') == 3
    assert log.count('Mage target services:actual learned actor,distinct supplied ranged primary,15/16and19/20boundaries') == 3
    assert log.count('Mage Meteor:hard9+soft1 refusal/hard10 fixed10')==1
    assert log.count('Mage Meteor:320 complete normal-center source expiry/hit14/area/field contexts and shared Phoenix offset consumer pass')==3
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
    for name, count in [('named-item-runtime-audit.json', 875), ('first-loop-affix-audit.json', 21)]:
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
    docs = ['mage-meteor-checkpoint', 'skeletal-mage-upgrade-plan', 'pet-adjustment-review', 'class-skill-reuse-notes', 'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons']
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_mage_meteor.py', *[ROOT / p for p in changed | added], *[ROOT / f'docs/{n}.md' for n in docs], ROOT / 'tools/reference/sentry-target-source-evidence.json', ROOT/'tools/reference/tornado-pulse-source-evidence.json', ROOT/'tools/build_class_skills.py', ROOT/'data/Sanctuary/Balance/necromancer-summons.json']
    names=[f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{audit_run}.log','pre-build-2.json','content-build-2.log','integration-build-2.log','run-2.log','audit-run-2.log','attempt-2-birth-inputs.json','attempt-2-birth-native-checks.log','attempt-2-audit-inputs.json','attempt-2-audit-native-checks.log','pre-build-1.json','content-build-1.log','integration-build-1.log','run-1.log','audit-run-1.log','attempt-1-birth-inputs.json','attempt-1-birth-native-checks.log','attempt-1-audit-inputs.json','attempt-1-audit-native-checks.log','birth-inputs.json','birth-native-checks.log','audit-inputs.json','audit-native-checks.log','python-tests.log','named-readiness.log','catalogue-check.log','mage-meteor-fixtures.json','fire-ground-checks.json','named-item-runtime-audit.json','first-loop-affix-audit.json']
    files += [STAGE/name for name in names]
    source=ROOT/'tools/reference/mage-meteor-source-evidence.json';source_proof=json.loads(source.read_text());fixture=ROOT/source_proof['fixture']['path']
    assert source_proof['fixture']['cases']==320 and sha(fixture)==source_proof['fixture']['sha256']
    assert inputs['birth']['sourceInputs'][str(fixture.relative_to(ROOT))]==sha(fixture)
    files += [source,fixture,ROOT/'tools/probe_mage_meteor_reference.py',ROOT/'tools/reference/phoenix-meteor-source-evidence.json',ROOT/'.local/class-skills/mage-meteor-source.log',ROOT/'.local/class-skills/mage-meteor-source-preflight.log',ROOT/'.local/class-skills/mage-meteor-source-recheck.log']
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';reviewed={}
    for rel,name in [('source/D2Game/src/MISSILES/MissMode.cpp','MISSMODE_SetDamageFlags'),('source/D2Game/src/SKILLS/Skills.cpp','sub_6FD10140'),('source/D2Game/src/MONSTER/MonsterMode.cpp','D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70')]:
        path=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=path.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
        reviewed[name]=hashlib.sha256(function(raw.decode(),name).encode()).hexdigest();files.append(path)
    result=dict(referenceVersion='3.3.93847',legacyCommit='0c0bbd1e6bef9181a3da1c046523af622a925416',reviewedNotExecuted=reviewed,compiledSourceEvidence=dict(path=str(source.relative_to(ROOT)),sha256=sha(source)),component='Approved fixed10 Mage Meteor through shared saved fire-ground warning/field/cooldown controller',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=262,retainedMageGlacialChainStaticBuffPlayerPetSentryMarkers=250,boots=3,actualReloads=2,saves=3,completeMeteorSourceContexts=320,naturalMageMeteorSessions=3,playerFireGroundAsyncMarkers=3,mageProcPolicyContexts=44800,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[875,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / (n + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (n + '-inputs.json'))) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)}, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'Fire Mage hard10 gains original five-percent fixed10 Meteor. Actual hard9+real soft1 refuses;cooldown-unavailable slots fall back to basic without another actor draw. One proc replaces the basic,spends no owner/NPC mana and records a caster-specific30frame local delay. Owner and other Mage delays remain independent.',
            'Existing FireGround dictionaries,timer,persistence v0 and cooldown service hold NPC warning/fields. Optional NpcCaster/Seed defaults preserve player JSON. Real owner supplies allegiance;Mage supplies caster/damage identity. Fixed10 packets have no owner Sorceress mastery/synergy/potion. No new controller or proxy actor.',
            'Normal Meteor60frame warning uses captured ground location and one shared raw area packet through existing per-recipient Mage impact/critical consumer. Shared18offset producer now serves player Meteor/Phoenix/NPC. Fixed10 fire fields use existing missile formula,165frame lifetime,40ms cadence,size2/DamageRate41 and one native collider per field.',
            'Fractional field damage,fire immunity,field seed/SOFTHIT and separate actor critical draws/no recovery,offline no-damage/no-RNG expiry,moving-target ground snapshot,atomic shared restore and historical optional defaults pass. Three natural learned Mage sessions create warning,impact and subsequent ground damage. All250 predecessor markers plus full retained player fire-ground async/save controls pass.',
            'Held warning/fields/source/packet/cooldown/seed and fractional wounds survive two reloads. Copied warning/field resumption leaves inspected originals unchanged.320 complete legacy normal-center expiry/router/hit14/area/producer contexts pass ASan/UBSan,including absent owner/skill and partial field failures. Data,audit decisions,kernel and106installed hashes remain unchanged.'],
        remaining=[
            'Exact modern room/path/area-filter/target enumeration/size/global seed/event/NHD,source after death,special/PvP/Sunder and client behavior remain open. Native field fit/collision/first-recipient projection is an adapter. Source do5 animation/collision-mask RNG and03SOFTHIT result/event refresh are not complete native parity;the transported draw does not certify those subscribers.',
            'Inherited NPC area intrinsic critical and field avoidance/critical/mitigation receiver behavior are reused contracts,not independent modern skill56 proof. Warning/field host seeds use the established supplier;exact source allocation/snapshot timing remains unresolved.',
            'Held warning/field/cooldown cohorts and controlled copied resumption do not prove unheld concurrent saves or an installed old-build upgrade. Existing player Meteor live child/field values and player lifecycle retain their prior contracts;NPC integration does not certify all Sorceress mechanics.',
            'All approved fixed-rank Mage secondaries/buffs are now bound,but three-element creation/saved poison-to-ice migration,historical plain Mage AI rollout,basic Sorceress spells/hard5triples and recorded Skeleton Mastery mapping remain required. Other approved pets,safe parking and barrier phases remain queued.',
            'No final effects/audio/icons,new player binding,deployment or completed class. Warning0x3709/impact0x36BD/ground0x398C hue0x489 remain identified shared placeholders.'])
    output = ROOT / 'tools/reference/mage-meteor-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Mage Meteor:262markers/3boots/2reloads,3natural sessions,250retained/player fire-ground,320source/4audits/71Python;installed unchanged. Package remains unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
