#!/usr/bin/env python3
"""Bind approved Mage Chain Lightning and shared saved player/NPC controller checks."""
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
STAGE = ROOT / '.local/class-skills/mage-chain'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {n: inventory.validate_inventory(STAGE / (n + '-inputs.json')) for n in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts'] and value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/mage-static-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ['ChainLightning','MageOffensiveSkills','NecromancerMageMissiles']}|{'tests/Sanctuary.Integration/MageStaticFieldChecks.cs'}
    added={'tests/Sanctuary.Integration/MageChainChecks.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    assert inputs['birth']['copiedData'] == old['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/mage-static/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    for family in ['mage-chain','chain-lightning']:
        expected[family+'-check']+=1;expected[family+'-reload-check']+=2
    expected['mage-chain-live-result']+=3;expected['chain-lightning-result']+=1;expected['chain-lightning-reload-result']+=2
    assert sum(expected.values()) == 238 and markers(STAGE / 'birth-native-checks.log') == expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('AI67:1598 complete source ordered-service/state/draw comparisons') == 3
    assert log.count('Mage target services:actual learned actor,distinct supplied ranged primary,15/16and19/20boundaries') == 3
    assert log.count('Mage Chain:hard9+real soft1 refusal/hard10 fixed10')==1
    assert log.count('Mage Static:44800 one-draw/exclusive five-percent policy contexts')==1
    assert log.count('Mage buffs:hard19+real soft1 refuses;live hard20 unlock/respec/relearn')==1
    assert log.count('Mage world:ranged AI1,S1end24/think25,A1kind2action35/end45') == 1
    assert log.count('4096 complete selector and2160 complete shared charged-bolt') == 3
    assert log.count('4032 complete source compositions') == 3
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ['content', 'integration']:
        log = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in log for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    for name, count in [('named-item-runtime-audit.json', 873), ('first-loop-affix-audit.json', 21)]:
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
    docs = ['mage-chain-checkpoint', 'skeletal-mage-upgrade-plan', 'pet-adjustment-review', 'class-skill-reuse-notes', 'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons']
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_mage_chain.py', *[ROOT / p for p in changed | added], *[ROOT / f'docs/{n}.md' for n in docs], ROOT / 'tools/reference/sentry-target-source-evidence.json']
    names = [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log', f'run-{run}.log', f'audit-run-{audit_run}.log', 'attempt-1-birth-inputs.json','attempt-1-birth-native-checks.log','pre-build-1.json','content-build-1.log','integration-build-1.log','run-1.log','attempt-2-birth-inputs.json','attempt-2-birth-native-checks.log','pre-build-2.json','content-build-2.log','integration-build-2.log','run-2.log','birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'audit-native-checks.log', 'python-tests.log', 'named-readiness.log', 'catalogue-check.log', 'fire-golem-world-fixtures.json', 'fire-golem-death-fixtures.json', 'blood-golem-world-fixtures.json', 'clay-golem-world-fixtures.json', 'skeleton-world-fixtures.json', 'mage-chain-fixtures.json','chain-lightning-checks.json','mage-static-fixtures.json','mage-pet-buff-fixtures.json','sorceress-skills-checks.json','enchant-checks.json','mage-world-fixtures.json', 'sentry-unit-path-fixture.json', 'mage-birth-fixtures.json', 'mage-missile-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json']
    files += [STAGE / name for name in names]
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    reviewed={}
    for relative,name in [('source/D2Game/src/SKILLS/SkillSor.cpp','SKILLS_SrvDo026_ChainLightning'),('source/D2Game/src/MISSILES/MissMode.cpp','MISSMODE_SrvHit12_ChainLightning_LightningStrike')]:
        source=ROOT/'.local/class-skills/d2moo-pinned-source'/relative;raw=source.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        reviewed[name]=hashlib.sha256(function(raw.decode(),name).encode()).hexdigest();files.append(source)
    files.append(ROOT/'tools/reference/chain-lightning-engine-evidence.json')
    result = dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],reviewedNotExecuted=reviewed,component='Approved Mage fixed10 Chain Lightning proc through shared saved player/NPC flight controller', installed=False, goalComplete=False, completedClasses=[], build=build, run=run, auditRun=audit_run,
        verified=dict(nativeMarkers=238,retainedMageStaticBuffPlayerPetSentryMarkers=226,boots=3,actualReloads=2,saves=3,mageProcPolicyContexts=44800,naturalMageChainSessions=3,naturalMageStaticSessions=3,playerChainAsyncMarkers=3,learnedMageInputCasts=6,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[873,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / (n + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (n + '-inputs.json'))) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)}, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'ChainFlight adds optional NPC caster,independent segment damage seed and slow payload while preserving player DTO defaults and persistence v0. Player launch/live-child synergy/mastery,source identity hopping and cast-local delay remain in the same controller. Restore validates into a temporary map before replacing live state.',
            'Approved Lightning Mage hard10 selects fixed10 Chain in the original five-percent slot,without extra mana or basic missile. Actual hard9+real soft1 refuses. NPC root/children use fixed Sorceress values,no owner mastery/synergy/potion,own caster LOS and current per-child slow state. Shared Mage ImpactSpell retains mitigation/recovery and existing actor intrinsic-critical transport.',
            'Actual delayed impacts,cyclic identity hops,finite count,one proc draw,one shared actor critical draw at first impact,captured Slow Missiles/live child refresh,offline no-travel/no-damage expiry,atomic invalid restore and historical player JSON defaults pass. Held NPC flights preserve packet/source/seed/delay/phase/wounds across2reloads; a copied flight resumes real collision without advancing its inspected original.',
            'Three natural learned AI sessions launch at the existing action35 and damage multiple targets through travel/hops. Existing full player Chain controls and all226 predecessor markers pass. Static hard19 now legitimately consumes its one Chain-eligible policy draw while its separate Static slot stays locked. Audit decisions,data,kernel and installed106 remain unchanged.'],
        remaining=[
            'Exact modern Size2/source-cell collision,path/room/aura-filter ordering,NHD confirmation,special/PvP/Sunder,event/rate/client and source-after-death behavior remain open. Shared legacy-derived controller acceptance is not a full current executable proof.',
            'NPC segment seeds use the existing host seed allocator and captured Slow Missiles adapter; exact modern allocation/global event chronology remains unresolved. Native NPC impacts reuse the existing intrinsic-critical consumer,not a new modern skill53 critical proof. Player Slow Missiles behavior is unchanged and remains separate.',
            'Historical plain Mage AI rollout,three-element creation/poison-to-ice migration,basic Sorceress spells/hard5triples,the recorded Skeleton Mastery mapping question and fixed Meteor/Glacial bindings remain required. Static and Chain are enabled; unbound slots remain unavailable.',
            'Held NPC/player flight cohorts prove serialized transport and controlled resumption,not unheld concurrent saves or an actual installed old-build byte upgrade. Normal NPC segment lifetime25/next-hit4 remains unchanged; fixed Chain has no local cooldown.',
            'No final art,new player binding,deployment or completed class. Other approved pets,safe-area parking,barrier phases and remaining Static reference gaps remain queued.'])
    output = ROOT / 'tools/reference/mage-chain-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Mage Chain:238markers/3boots/2reloads,3natural Chain/Static sessions;226retained,player Chain,4audits/71Python/installed unchanged. Package remains unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
