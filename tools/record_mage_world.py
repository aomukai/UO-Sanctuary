#!/usr/bin/env python3
"""Bind learned Mage world and retained shared pet/sentry regressions."""
import argparse
import json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/mage-world'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {n: inventory.validate_inventory(STAGE / (n + '-inputs.json')) for n in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts'] and value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/necromancer-mage-targets-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed = {'src/Sanctuary.Content/'+n+'.cs' for n in ['AssassinSentryWorldTargets','IronGolemWorld','NecromancerMageMissiles','NecromancerSummons','UnarmedPetWorld']} | {'tests/Sanctuary.Integration/MageBirthChecks.cs'}
    added = {'src/Sanctuary.Content/'+n+'.cs' for n in ['NecromancerGroundWorld','NpcRangedTargets','MageWorldBody']} | {'tests/Sanctuary.Integration/MageWorldChecks.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    assert inputs['birth']['copiedData'] == old['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/necromancer-mage-targets/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    for family in ['mage-world','mage-birth','mage-missile','sentry-unit-path']:
        expected[family+'-check'] += 1
        expected[family+'-reload-check'] += 2
    for name in ['sentry-target-check','mage-world-live-result','mage-missile-live-result','mage-world-right-check','mage-world-right-result','mage-world-quick-check','mage-world-quick-result']:
        expected[name] += 3
    assert sum(expected.values()) == 210 and markers(STAGE / 'birth-native-checks.log') == expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('AI67:1598 complete source ordered-service/state/draw comparisons') == 3
    assert log.count('Mage target services:actual learned actor,distinct supplied ranged primary,15/16and19/20boundaries') == 3
    assert log.count('Mage world:ranged AI1,S1end24/think25,A1kind2action35/end45') == 1
    assert log.count('4096 complete selector and2160 complete shared charged-bolt') == 3
    assert log.count('4032 complete source compositions') == 3
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ['content', 'integration']:
        log = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in log for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    for name, count in [('named-item-runtime-audit.json', 870), ('first-loop-affix-audit.json', 21)]:
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
    docs = ['mage-world-checkpoint', 'class-skill-reuse-notes', 'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons']
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_mage_world.py', *[ROOT / p for p in changed | added], *[ROOT / f'docs/{n}.md' for n in docs], ROOT / 'tools/reference/sentry-target-source-evidence.json']
    names = [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log', f'run-{run}.log', f'audit-run-{audit_run}.log', 'attempt-1-birth-inputs.json', 'attempt-1-birth-native-checks.log', 'pre-build-1.json', 'content-build-1.log', 'integration-build-1.log', 'run-1.log', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'audit-native-checks.log', 'python-tests.log', 'named-readiness.log', 'catalogue-check.log', 'fire-golem-world-fixtures.json', 'fire-golem-death-fixtures.json', 'blood-golem-world-fixtures.json', 'clay-golem-world-fixtures.json', 'skeleton-world-fixtures.json', 'mage-world-fixtures.json', 'sentry-unit-path-fixture.json', 'mage-birth-fixtures.json', 'mage-missile-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json']
    files += [STAGE / name for name in names]
    result = dict(referenceVersion='3.3.93847', component='Learned Mage shared ranged AI67 world, saved owner frame and existing elemental missiles', installed=False, goalComplete=False, completedClasses=[], build=build, run=run, auditRun=audit_run,
        verified=dict(nativeMarkers=210, retainedSummonMarkers=177, boots=3, actualReloads=2, saves=3, learnedMageInputCasts=6, connectedMageWorldSessions=3, policyCasesPerBoot=1598, targetSourceCasesPerBoot=4096, unitPathSourceCasesPerBoot=4032, auditMarkers=4, pythonTests=71, namedDefinitions=573, auditFingerprints=[870, 21], unchangedInstalledArtifacts=106, handlers=186, necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / (n + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (n + '-inputs.json'))) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)}, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'Shared NecromancerGroundWorld owns state/owner/frame/load transport without changing the existing melee actors serialized field or JSON layout. New learned MageWorldBody uses ranged AI parameter0=1 and NPC338/mode4, suppresses native AI and shares the existing global pet frame conductor.',
            'NpcRangedTargets extracts the original sentry native enumeration/LOS/threat adapter around unchanged AssassinSentryTargets.Select. Both callers reuse the existing stationary zero-lead unit path only in stopped think context; cached velocity need not be zero. Existing4096selector/2160charged-path and4032unit-path source comparisons and saved sentry checks pass.',
            'Actual learned corpse/payment, S1end24/think25 and kind2A1 action35/end45 launch, delayed traced collision, shared actor AI/intrinsic-critical seed, owner-follow/regeneration, recovery/offline/load cancellation and strict ranged state pass. Three connected world sessions and six real right/quick casts pass; historical plain Mage save type remains.',
            'All177 predecessor markers retain Fire/Blood/Clay/skeleton/Iron/query coverage; old Mage birth/missile controls also pass. Fresh audit payloads, runtime data, native kernel and installed106 are unchanged.'],
        remaining=[
            'This is a native adapter integration backed by existing complete legacy components, not a complete modern executable/world proof. Room ordering, map projection, remembered-target service, seed initialization and mixed actor/missile event priority remain explicit. Missing threat is refused; missing native footprint uses the previously identified provisional size2 with a visible report.',
            'Movement paths/reposition and moving-target path-cache equivalence remain open. Stationary zero-lead probe does not establish moving actor initializer parity. Mid-animation rate changes, initial S1/initializer/equipment/shield generation, PvP/special/Sunder/client and unsupported recipients remain separate.',
            'Successful-launch diagnostics count emitted missiles, not guaranteed hits. Existing projectile-after-caster-removal behavior is retained without a modern parity claim; poison aggregation and modern critical chronology remain open.',
            'Inspected future-held snapshot cohorts prove cancellation and wounds/seed/type transport across2reloads, not unheld concurrent-save behavior or actual old installed-build upgrades.',
            'Approved three-element Mage upgrades, other pet adjustments and safe-area parking remain queued. Baseline four elements remain here; no final effects/audio/icons, class completion or deployment.'])
    output = ROOT / 'tools/reference/mage-world-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Learned Mage world:210markers/3boots/2reloads,6inputs/3automatic sessions;177retained,4audits/71Python/installed unchanged. No class complete.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
