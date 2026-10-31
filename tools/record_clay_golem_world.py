#!/usr/bin/env python3
"""Bind learned Clay world delivery to retained skeleton/Iron behavior and saves."""
import argparse
import json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/clay-golem-world'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {n: inventory.validate_inventory(STAGE / (n + '-inputs.json')) for n in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts']
        assert value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/skeleton-world-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed = {'src/Sanctuary.Content/' + n + '.cs' for n in ['IronGolemWorld', 'NecromancerSummons', 'SkeletonWorldBody', 'ClayGolemContact']}
    changed |= {'tests/Sanctuary.Integration/' + n + '.cs' for n in ['ClayGolemBirthChecks', 'ClayGolemCombatChecks', 'ClayGolemContactChecks']}
    added = {'src/Sanctuary.Content/UnarmedPetWorld.cs', 'src/Sanctuary.Content/ClayGolemWorldBody.cs', 'tests/Sanctuary.Integration/ClayGolemWorldChecks.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    assert inputs['birth']['copiedData'] == old['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/skeleton-world/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    for family in ['clay-golem-world', 'clay-golem-contact', 'clay-golem-combat', 'clay-golem-slow', 'clay-golem-birth', 'golem-roster']:
        expected[family + '-check'] += 1
        expected[family + '-reload-check'] += 2
    for name in ['clay-golem-world-live-result', 'clay-golem-combat-live-result']:
        expected[name] += 3
    for name in ['clay-golem-world-right', 'clay-golem-world-quick']:
        expected[name + '-check'] += 3
        expected[name + '-result'] += 3
    assert sum(expected.values()) == 99 and markers(STAGE / 'birth-native-checks.log') == expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('AI67:1598 complete source ordered-service/state/draw comparisons') == 3
    assert 'Clay world:shared AI67,G1action10/end18' in log and 'startup mode/event parity remains open' in log
    assert '10377safe complete-source matches/1098unsafe refusals' in log
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ['content', 'integration']:
        log = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in log for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    for name, count in [('named-item-runtime-audit.json', 865), ('first-loop-affix-audit.json', 21)]:
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert len(audit['InputHashes']) == count
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
    saved = json.loads((STAGE / 'clay-golem-world-fixtures.json').read_text())
    assert saved['Generation'] == 2 and saved['Life'] == 262143 and saved['TargetLife'] == 5120000
    assert len({saved[n] for n in ['Owner', 'Body', 'Target', 'Historical']}) == 4
    assert 'Stale named runtime evidence' in (STAGE / 'python-stale-audit-run-1.log').read_text()
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 186 and sum(x['characterClass'] == 'Necromancer' for x in handlers) == 30 and not status['completedClasses']
    docs = ['clay-golem-world-checkpoint', 'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-mechanical-reference', 'skill-effects', 'skill-icons']
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_clay_golem_world.py', *[ROOT / p for p in changed | added], *[ROOT / f'docs/{n}.md' for n in docs]]
    names = [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log', f'run-{run}.log', f'audit-run-{audit_run}.log', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'audit-native-checks.log', 'python-tests.log', 'python-stale-audit-run-1.log', 'named-readiness.log', 'catalogue-check.log', 'clay-golem-world-fixtures.json', 'clay-golem-birth-fixtures.json', 'clay-golem-contact-fixtures.json', 'clay-golem-combat-fixtures.json', 'clay-golem-slow-fixtures.json', 'golem-roster-fixtures.json', 'skeleton-world-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json']
    files += [STAGE / name for name in names]
    result = dict(referenceVersion='3.3.93847', component='Learned Clay Golem shared unarmed world controller', installed=False, goalComplete=False, completedClasses=[], build=build, run=run, auditRun=audit_run,
        verified=dict(nativeMarkers=99, retainedSkeletonIronMarkers=63, boots=3, actualReloads=2, saves=3, learnedClayRightQuickCasts=6, learnedClayNaturalSessions=3, retainedHistoricalClaySessions=3, auditMarkers=4, pythonTests=71, namedDefinitions=573, auditFingerprints=[865, 21], unchangedInstalledArtifacts=106, handlers=186, necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / (n + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (n + '-inputs.json'))) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)}, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'New learned Clay casts create a saved ClayGolemWorldBody after existing birth preflight. Candidate setup precedes shared golem replacement; once-only mana, actual right/quick input, four-type roster and difficulty/resource checks pass.',
            'UnarmedPetWorld extracts the existing skeleton owner/state/contact lifecycle, preserving its JSON shape and profile. Skeleton and Clay supply species attack clocks and movement independently; Iron original-item clocks remain separate. All63 predecessor markers are retained.',
            'Clay uses captured Golem Mastery movement, current velocity8 and G1A1speed232/action10/end18. The existing first-think25 convention yields action35/end43 in the supplied unblocked cohort. Captured mitigation/no early HP/no reroll/no replay, outgoing positive-whole-life-loss slow, follow, regeneration, interruption and offline suspension pass.',
            'Actual native Dog recipients retain the explicitly reported pre-packet compatibility bridge. There is no fallback after mapped packet RNG/allocation. Historical Clay direct/native hooks remain separately exercised and historical typed saves reload alongside the new type.',
            'Two reloads preserve new actor ownership, wounds and seed while canceling transient contact. Snapshot staging alone holds the next observation for120seconds; unheld natural combat is separate. Fresh audit payloads match prior decisions; the initial stale-audit Python run is archived and all71 tests pass after metadata refresh.'],
        remaining=[
            'Modern startup S1end32/first-think25 overlap and mode/event eligibility remain unverified; this rollout retains the existing conductor convention without claiming full modern startup parity. Exact world/owner/seed/room/scan/ties/LOS/fit and live mid-action rate rescheduling remain open.',
            'Clay outgoing item19 applicability remains a historical-source discrepancy; the established positive-whole-life-loss slow adapter is retained. Full proc/Sunder/PvP/special-recipient/HP-death and native-fallback equivalence remain unsupported or uncertain.',
            'Contact cancellation on suspension/reload is not saved in-flight resumption. The fixture-only held snapshot is not an unheld concurrent-save certificate. Actual old-binary upgrade and graphical client acceptance remain untested.',
            'No deployment, complete skill/class, approved pet-upgrade or safe-area parking claim. Clay body14/hue0x96D, named placeholder/native A1/emergence particles/audio and talent/right/quick art remain separate missing assets.'])
    output = ROOT / 'tools/reference/clay-golem-world-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Learned Clay world:99markers/3boots/2reloads,6real casts/3auto sessions;63skeleton/Iron retained,4audits/71Python/installed unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
