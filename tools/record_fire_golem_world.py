#!/usr/bin/env python3
"""Bind learned Fire world/contact/periodic/retirement acceptance to frozen inputs."""
import argparse
import json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/fire-golem-world'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {n: inventory.validate_inventory(STAGE / (n + '-inputs.json')) for n in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts'] and value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/blood-golem-world-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed = {'src/Sanctuary.Content/' + n + '.cs' for n in ['IronGolemWorld', 'UnarmedPetWorld', 'NecromancerSummons']}
    changed |= {'tests/Sanctuary.Integration/' + n + '.cs' for n in ['FireGolemBirthChecks', 'FireGolemCombatChecks']}
    added = {'src/Sanctuary.Content/FireGolemWorldBody.cs', 'tests/Sanctuary.Integration/FireGolemWorldChecks.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    assert inputs['birth']['copiedData'] == old['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/blood-golem-world/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    for family in ['fire-golem-world', 'fire-golem-birth', 'fire-golem-combat', 'fire-golem-absorption', 'fire-golem-aura', 'fire-golem-death']:
        expected[family + '-check'] += 1
        expected[family + '-reload-check'] += 2
    for name in ['fire-golem-world-live-result', 'fire-golem-combat-live-result', 'fire-golem-aura-live-result', 'fire-golem-death-live-result']:
        expected[name] += 3
    for name in ['fire-golem-birth-right', 'fire-golem-birth-quick']:
        expected[name + '-check'] += 3
        expected[name + '-result'] += 3
    expected['fire-golem-death-save'] += 3
    assert sum(expected.values()) == 174 and markers(STAGE / 'birth-native-checks.log') == expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('AI67:1598 complete source ordered-service/state/draw comparisons') == 3
    assert 'Fire world:tied S1end25/think25,G3action10/end17' in log
    assert 'retirement during Thorns stops catchup/regen' in log
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ['content', 'integration']:
        log = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in log for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    for name, count in [('named-item-runtime-audit.json', 867), ('first-loop-affix-audit.json', 21)]:
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
    saved = json.loads((STAGE / 'fire-golem-world-fixtures.json').read_text())
    assert saved['Generation'] == 2 and saved['Life'] > 0 and saved['TargetLife'] == 5120000 and saved['Seed'] > 0 and saved['AuraFrame'] == 50
    assert len({saved[n] for n in ['Owner', 'Body', 'Target']}) == 3
    assert 'Build FAILED.' in (STAGE / 'content-build-1.log').read_text()
    assert 'No independent fixture corridor near 7140,3650' in (STAGE / 'birth-native-checks-attempt-1.log').read_text()
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 186 and sum(x['characterClass'] == 'Necromancer' for x in handlers) == 30 and not status['completedClasses']
    docs = ['fire-golem-world-checkpoint', 'fire-golem-world-preflight', 'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons']
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_fire_golem_world.py', *[ROOT / p for p in changed | added], *[ROOT / f'docs/{n}.md' for n in docs], ROOT / 'tools/reference/periodic-skill-clock-source-evidence.json', ROOT / 'tools/reference/fire-golem-aura-source-evidence.json']
    names = [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log', f'run-{run}.log', f'audit-run-{audit_run}.log', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'audit-native-checks.log', 'python-tests.log', 'named-readiness.log', 'catalogue-check.log', 'fire-golem-world-fixtures.json', 'fire-golem-birth-fixtures.json', 'fire-golem-combat-fixtures.json', 'fire-golem-absorption-fixtures.json', 'fire-golem-aura-fixtures.json', 'fire-golem-death-fixtures.json', 'blood-golem-world-fixtures.json', 'clay-golem-world-fixtures.json', 'skeleton-world-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json', 'content-build-1.log', 'birth-native-checks-attempt-1.log', 'run-1.log']
    files += [STAGE / name for name in names]
    result = dict(referenceVersion='3.3.93847', component='Learned Fire Golem ground, independent aura and retirement integration', installed=False, goalComplete=False, completedClasses=[], build=build, run=run, auditRun=audit_run,
        verified=dict(nativeMarkers=174, retainedBloodClaySkeletonIronMarkers=129, boots=3, actualReloads=2, saves=3, learnedFireRightQuickCasts=6, learnedFireNaturalMixedContactSessions=3, learnedFireFrozenPeriodicSessions=3, retainedHistoricalFireSessions=3, retainedFireAuraSessions=3, retainedFireDeathSessions=3, auditMarkers=4, pythonTests=71, namedDefinitions=573, auditFingerprints=[867, 21], unchangedInstalledArtifacts=106, handlers=186, necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / (n + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (n + '-inputs.json'))) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)}, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'New learned Fire casts select a separately saved FireGolemWorldBody inheriting the accepted FireGolemDeathBody. Existing birth, fractional resources, absorption, shared golem cap, payment and actual right/quick inputs remain verified.',
            'Shared ground motion/contact/lifecycle is reused with G3A1speed256/action10/end17, velocity10/WL192 and Mastery. Equality at S1end25/first-think25 now preserves the due AI event. Mapped mixed physical/fire packets capture once and release at35/end42 without mitigation or RNG recomputation.',
            'An optional periodic-frame interface keeps Holy Fire independent of frozen/ordinary AI and stores its next deadline separately. Existing PulseHolyFire/PaladinElements supply damage and self weapon state. Natural frozen zero-mana sessions check first50/next100 and offline skip to150 without catch-up damage/RNG.',
            'Death detaches ordinary ground scheduling and cancels pending contact; the original source/seed/owner remain for the existing160ms retirement timer. Thorns death during contact stops further requested catch-up frames, regeneration and aura work. Existing natural/replacement/blocked death and saved offline retirement checks remain.',
            'Two reloads preserve a wounded new actor/seed/aura phase while canceling transient contact. Historical Fire actors remain on their own type/controller paths; direct native-hook tests are explicitly historical. All129 predecessor markers and fresh audit payload decisions are retained.'],
        remaining=[
            'The initial50frame pulse preserves the prior two-second native adapter. Modern initial aura activation and periodic-versus-AI/animation/regen priority remain unverified; current hook placement is not source equivalence. Relative actor/world/UTC projection, scan/room/LOS/fit/ties and host seed transport remain adapters.',
            'Optional monstats elemental/noRatio output and mode-entry RNG, modern proximity arithmetic, full event/HP-death/proc/Sunder/PvP/special-recipient/native-fallback equivalence and live mid-animation rate changes remain unsupported or uncertain.',
            'Packets remain transient on suspension/load. The inspected snapshot cohort alone holds its next observation120seconds; unheld concurrent snapshots, actual old-binary upgrade and graphical client acceptance remain untested. No deployment, complete skill/class, approved pet-upgrade or safe-area parking claim.',
            'Build1 failed a generated-property collision before native launch. Attempt1 stopped at an out-of-map fixture corridor; final accepted scope uses the corrected test location. These diagnostic failures are archived, not acceptance.',
            'Fire Golem placeholder name/body14/hue0x489/native A1/emergence/pulse/contact/retirement explosion/audio and existing talent/right/quick icons remain separately pending final assets.'])
    output = ROOT / 'tools/reference/fire-golem-world-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Learned Fire world:174markers/3boots/2reloads,6real casts/3mixed hits/3frozen pulses;129retained,4audits/71Python/installed unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
