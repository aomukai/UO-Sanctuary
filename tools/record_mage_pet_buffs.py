#!/usr/bin/env python3
"""Bind approved Mage hard20 buffs and retained shared player/pet regressions."""
import argparse
import json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/mage-pet-buffs'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {n: inventory.validate_inventory(STAGE / (n + '-inputs.json')) for n in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts'] and value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/mage-world-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ['ClassStats','IronGolemWorld','MageWorldBody','SorceressEffects']}
    added={'src/Sanctuary.Content/MagePetBuffs.cs','tests/Sanctuary.Integration/MagePetBuffChecks.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    assert inputs['birth']['copiedData'] == old['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/mage-world/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    for family in ['mage-pet-buff','sorceress-skills','enchant']:
        expected[family+'-check']+=1;expected[family+'-reload-check']+=2
    expected['sorceress-skills-result']+=1
    assert sum(expected.values()) == 220 and markers(STAGE / 'birth-native-checks.log') == expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('AI67:1598 complete source ordered-service/state/draw comparisons') == 3
    assert log.count('Mage target services:actual learned actor,distinct supplied ranged primary,15/16and19/20boundaries') == 3
    assert log.count('Mage buffs:hard19+real soft1 refuses;live hard20 unlock/respec/relearn')==1
    assert log.count('Mage world:ranged AI1,S1end24/think25,A1kind2action35/end45') == 1
    assert log.count('4096 complete selector and2160 complete shared charged-bolt') == 3
    assert log.count('4032 complete source compositions') == 3
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ['content', 'integration']:
        log = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in log for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    for name, count in [('named-item-runtime-audit.json', 871), ('first-loop-affix-audit.json', 21)]:
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
    docs = ['mage-pet-buffs-checkpoint', 'skeletal-mage-upgrade-plan', 'pet-adjustment-review', 'class-skill-reuse-notes', 'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons']
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_mage_pet_buffs.py', *[ROOT / p for p in changed | added], *[ROOT / f'docs/{n}.md' for n in docs], ROOT / 'tools/reference/sentry-target-source-evidence.json']
    names = [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log', f'run-{run}.log', f'audit-run-{audit_run}.log', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'audit-native-checks.log', 'python-tests.log', 'named-readiness.log', 'catalogue-check.log', 'fire-golem-world-fixtures.json', 'fire-golem-death-fixtures.json', 'blood-golem-world-fixtures.json', 'clay-golem-world-fixtures.json', 'skeleton-world-fixtures.json', 'mage-pet-buff-fixtures.json','sorceress-skills-checks.json','enchant-checks.json','mage-world-fixtures.json', 'sentry-unit-path-fixture.json', 'mage-birth-fixtures.json', 'mage-missile-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json']
    files += [STAGE / name for name in names]
    result = dict(referenceVersion='3.3.93847', component='Approved Mage hard20 automatic Enchant and intrinsic Frozen Armor', installed=False, goalComplete=False, completedClasses=[], build=build, run=run, auditRun=audit_run,
        verified=dict(nativeMarkers=220,retainedMagePetSentryMarkers=210,boots=3,actualReloads=2,saves=3,learnedMageInputCasts=6,connectedMageWorldSessions=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[871,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / (n + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (n + '-inputs.json'))) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)}, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'Approved Fire hard20 combat-start Enchant20 runs from the actual Mage AI skill request, without an extra action or mana cost. A combat latch prevents continuous scans; existing enchantments retain their original snapshot/timer and fresh allies are reconsidered at a new fight.',
            'Approved Ice hard20 FrozenArmor20 is intrinsic, follows live hard ranks on existing bodies and has no refresh timer. Existing Sorceress cold armor construction, NPC defense consumer and source FreezeStates are shared. Real soft gear does not unlock hard20; training, respec, relearn, source ownership and exact wounds pass.',
            'Fixed NPC skill values use the Sorceress row with only the requested effective rank and no learned synergy context. NPC Enchant bypasses player mastery/potion enhancement. The shared player Enchant construction and timed armor route retain their own enhancement/duration behavior.',
            'Three boots/two reloads preserve intrinsic unlocks, captured birth rank, wounded actor and absolute ally Enchant snapshot/duration. Existing player armor/shield/Enchant and all210 predecessor world/pet/sentry markers pass. Audit decisions,data,kernel and installed106 remain unchanged.'],
        remaining=[
            'This implements two approved Mage hard20 buffs, not the whole approved package or class. Fire combat-start integration currently belongs to learned MageWorldBody; historical plain actors keep their prior AI route. Full old-save spell rollout,three-element creation/poison-to-ice migration,Sorceress basic missiles,hard5triples and offensive secondaries/cooldowns remain required.',
            'Approved basic rank equals effective Raise Skeletal Mage, while normal Skeleton Mastery currently contributes to a different derived missile-rank curve. The exact mapping of that benefit onto replacement Sorceress endpoints is not specified. Keep it a nonblocking design question; do not invent a mastery percentage or silently drop it.',
            'The freeze fixture exercises the existing central weapon-hit consumer, not a complete new native attack/event-order or modern executable proof. Existing cold immunity/duration/shatter rules remain; no blanket corpse shattering.',
            'Future-held controller save cohorts inspect state/absolute buff duration, not naturally elapsed unheld concurrent snapshots or an actual old installed-build upgrade. Modern acquisition/seed/event/rate, PvP/special/Sunder and unsupported recipients remain separate.',
            'No final art, new player binding, deployment or completed class. Barrier death/search gaps, other approved pets and safe-area parking remain queued.'])
    output = ROOT / 'tools/reference/mage-pet-buffs-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Mage hard20 buffs:220markers/3boots/2reloads;210retained,player armor/Enchant,4audits/71Python/installed unchanged. Package remains unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
