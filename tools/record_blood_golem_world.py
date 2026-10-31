#!/usr/bin/env python3
"""Bind learned Blood world/healing to frozen inputs and retained summon checks."""
import argparse
import json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/blood-golem-world'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {n: inventory.validate_inventory(STAGE / (n + '-inputs.json')) for n in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts'] and value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/clay-golem-world-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed = {'src/Sanctuary.Content/NecromancerSummons.cs'} | {'tests/Sanctuary.Integration/' + n + '.cs' for n in ['BloodGolemBirthChecks', 'BloodGolemCombatChecks', 'BloodGolemContactChecks']}
    added = {'src/Sanctuary.Content/BloodGolemWorldBody.cs', 'tests/Sanctuary.Integration/BloodGolemWorldChecks.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    assert inputs['birth']['copiedData'] == old['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/clay-golem-world/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    for family in ['blood-golem-world', 'blood-golem-birth', 'blood-golem-combat', 'blood-golem-contact']:
        expected[family + '-check'] += 1
        expected[family + '-reload-check'] += 2
    for name in ['blood-golem-world-live-result', 'blood-golem-combat-live-result']:
        expected[name] += 3
    for name in ['blood-golem-birth-right', 'blood-golem-birth-quick']:
        expected[name + '-check'] += 3
        expected[name + '-result'] += 3
    assert sum(expected.values()) == 129 and markers(STAGE / 'birth-native-checks.log') == expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('AI67:1598 complete source ordered-service/state/draw comparisons') == 3
    assert 'Blood world:shared AI67,G2action10/end16,exact owner/pet pre-HP healing including lethal257FP clamp,no double healing' in log
    assert 'modern startup parity remains open' in log
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ['content', 'integration']:
        log = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in log for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    for name, count in [('named-item-runtime-audit.json', 866), ('first-loop-affix-audit.json', 21)]:
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
    saved = json.loads((STAGE / 'blood-golem-world-fixtures.json').read_text())
    assert saved['Generation'] == 2 and saved['OwnerLife'] > 0 and saved['Life'] > 0 and saved['TargetLife'] == 5120000 and saved['Seed'] > 0
    assert len({saved[n] for n in ['Owner', 'Body', 'Target', 'Historical']}) == 4
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 186 and sum(x['characterClass'] == 'Necromancer' for x in handlers) == 30 and not status['completedClasses']
    docs = ['blood-golem-world-checkpoint', 'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons']
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_blood_golem_world.py', *[ROOT / p for p in changed | added], *[ROOT / f'docs/{n}.md' for n in docs]]
    names = [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log', f'run-{run}.log', f'audit-run-{audit_run}.log', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'audit-native-checks.log', 'python-tests.log', 'named-readiness.log', 'catalogue-check.log', 'blood-golem-world-fixtures.json', 'blood-golem-birth-fixtures.json', 'blood-golem-contact-fixtures.json', 'blood-golem-combat-fixtures.json', 'clay-golem-world-fixtures.json', 'skeleton-world-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json']
    files += [STAGE / name for name in names]
    result = dict(referenceVersion='3.3.93847', component='Learned Blood Golem shared world and pre-HP healing', installed=False, goalComplete=False, completedClasses=[], build=build, run=run, auditRun=audit_run,
        verified=dict(nativeMarkers=129, retainedClaySkeletonIronMarkers=99, boots=3, actualReloads=2, saves=3, learnedBloodRightQuickCasts=6, learnedBloodNaturalSessions=3, retainedHistoricalBloodSessions=3, healingSourceContexts=15559, incomingSourceContexts=3456, birthSourceContexts=16065, auditMarkers=4, pythonTests=71, namedDefinitions=573, auditFingerprints=[866, 21], unchangedInstalledArtifacts=106, handlers=186, necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / (n + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (n + '-inputs.json'))) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)}, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'New learned Blood casts select a saved BloodGolemWorldBody. Existing birth preflight, once-only mana, candidate-before-shared-cap replacement and native/difficulty resources are retained. Six real right/quick casts assert the new type.',
            'The unchanged UnarmedPetWorld/Iron conductor supplies owner/state/contact/motion/load behavior. Blood supplies G2A1speed256/action10/end16, velocity9/WL184 and captured Mastery movement. All99 predecessor Clay/skeleton/Iron markers are retained.',
            'Existing BloodGolemHealing.Dealt runs in shared contact before target HP; no new after-contact heal exists. Actual learned world attacks verify exact fractional owner/pet shares, overkill limited to257FP remaining target life, captured mitigation/no early healing/no reroll/no double healing and owner kill credit. Natural learned sessions verify the same path on the unheld clock.',
            'Interruption/offline/load cancel transient packets without damage/heal/RNG replay. Both reloads retain wounded owner/pet, target life, seed and an actual historical BloodGolemBody. Snapshot staging alone holds the inspected cohort next observation120seconds; unheld natural sessions are separate.',
            'Source birth/healing/incoming comparisons, historical direct native contacts, typed saves, Mastery movement/follow/regeneration, malformed-state refusal and actual Dog reported pre-packet fallback pass. Runtime audit payloads, copied data, native kernel and installed106 remain unchanged.'],
        remaining=[
            'Current executable callback11034/11033 versus table dm rounding remains unresolved; existing complete older-callback policy is retained, not certified as modern parity. Param5zero incoming sharing remains distinct; changed nonzero parameters are refused.',
            'Startup S1end26/first-think25 eligibility/mode/event order remains unverified. Exact owner/seed/world/room/scan/ties/LOS/fit, live mid-action rate changes, full HP-death/proc/Sunder/PvP/special-recipient and native integer-fallback equivalence remain unsupported or uncertain.',
            'Transient contact cancellation is not saved in-flight resumption. Unheld concurrent snapshots, actual old-binary upgrade and graphical client acceptance remain untested. No deployment, complete class, approved pet-enhancement or safe-area parking claim.',
            'Body14/hue0x21, Blood Golem placeholder name/native A1/emergence/healing overlay151/audio and existing talent/right/quick icon requirements remain separately pending final assets.'])
    output = ROOT / 'tools/reference/blood-golem-world-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Learned Blood world:129markers/3boots/2reloads,6real casts/3auto healing sessions;99Clay/skeleton/Iron retained,4audits/71Python/installed unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
