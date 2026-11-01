#!/usr/bin/env python3
"""Bind live warrior hard-point unlocks to shared affix/retaliation checks."""
import argparse
import json
import re
import subprocess
from collections import Counter
from pathlib import Path

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/skeleton-upgrades'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {name: inventory.validate_inventory(STAGE / f'{name}-inputs.json') for name in ('birth', 'audit')}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / f'{name}-inputs.json')
        assert value['artifacts'] == inputs['birth']['artifacts']
        assert value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/mage-historical-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed = {'src/Sanctuary.Content/' + n + '.cs' for n in (
        'EncounterProfiles', 'EncounterCreature', 'NpcEquipmentRetaliation',
        'SkeletonPets', 'RaisedSkeleton', 'SkeletonWorldBody')} | {
        'tests/Sanctuary.Integration/' + n + 'GolemWorldChecks.cs' for n in ('Fire', 'Blood', 'Clay')}
    added = {'src/Sanctuary.Content/SkeletonPetUpgrades.cs', 'tests/Sanctuary.Integration/SkeletonUpgradeChecks.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    assert inputs['birth']['copiedData'] == old['copiedData']
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/mage-historical/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    for family in ('skeleton-upgrades', 'iron-golem-retaliation', 'paladin-thorns'):
        expected[family + '-check'] += 1
        expected[family + '-reload-check'] += 2
    expected['skeleton-upgrades-live-result'] += 3
    expected['paladin-thorns-result'] += 1
    expected['paladin-thorns-reload-result'] += 2
    assert sum(expected.values()) == 289 and markers(STAGE / 'birth-native-checks.log') == expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('Skeleton upgrades:hard9+real soft1/19+soft1 locked') == 1
    assert log.count('9wild affix controls and real doubled source motion pass') == 1
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ('content', 'integration'):
        build_log = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in build_log for s in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    for name, count in (('named-item-runtime-audit.json', 877), ('first-loop-affix-audit.json', 21)):
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert len(audit['InputHashes']) == count
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for source, digest in audit['InputHashes'].items():
            assert sha(ROOT / source) == digest
        prior_audit = json.loads(subprocess.check_output(['git', 'show', '7bdcb14:tools/reference/' + name], cwd=ROOT))
        strip = lambda x: {k: v for k, v in x.items() if k not in ('ContentAssemblyPath', 'ContentAssemblySha256', 'InputHashes')}
        assert strip(audit) == strip(prior_audit)
    assert markers(STAGE / 'audit-native-checks.log') == Counter({
        'paladin-thorns-check': 1, 'paladin-thorns-result': 1, 'named-gear-check': 1, 'first-loop-catalogue-check': 1})
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
    assert len(handlers) == 186 and sum(x['characterClass'] == 'Necromancer' for x in handlers) == 30
    assert not status['completedClasses']
    # Unlocks add no saved fields or versions to the three supported actor types.
    for name in ('SkeletonPets', 'RaisedSkeleton', 'SkeletonWorldBody'):
        path = f'src/Sanctuary.Content/{name}.cs'
        original = subprocess.check_output(['git', 'show', '7bdcb14:' + path], cwd=ROOT, text=True)
        current = (ROOT / path).read_text()
        for pattern in (r'\[SerializableField\([^]]+\)\]\s*private[^;]+;', r'\[SerializationGenerator\([^]]+\)\]'):
            assert re.findall(pattern, original) == re.findall(pattern, current)
    saved = json.loads((STAGE / 'skeleton-upgrades-fixtures.json').read_text())
    assert saved['Generation'] == 2 and len({saved[k] for k in ('Body', 'Legacy', 'Raised')}) == 3
    docs = ('skeleton-upgrades-checkpoint', 'pet-adjustment-review', 'class-skill-reuse-notes',
            'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons')
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_skeleton_upgrades.py',
             *[ROOT / p for p in changed | added], *[ROOT / f'docs/{name}.md' for name in docs]]
    names = [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log',
             f'run-{run}.log', f'audit-run-{audit_run}.log', 'pre-build-1.json', 'content-build-1.log',
             'birth-inputs.json', 'birth-native-checks.log', 'audit-inputs.json', 'audit-native-checks.log',
             'python-tests.log', 'named-readiness.log', 'catalogue-check.log', 'skeleton-upgrades-fixtures.json',
             'named-item-runtime-audit.json', 'first-loop-affix-audit.json',
             'pre-build-2.json', 'content-build-2.log', 'integration-build-2.log', 'run-1.log', 'audit-run-1.log',
             'attempt-1-birth-inputs.json', 'attempt-1-birth-native-checks.log', 'attempt-1-audit-inputs.json',
             'attempt-1-audit-native-checks.log', 'attempt-1-named-item-runtime-audit.json',
             'attempt-1-first-loop-affix-audit.json', 'attempt-1-python-tests.log', 'attempt-1-named-readiness.log']
    names += ['pre-build-3.json', 'content-build-3.log', 'integration-build-3.log', 'run-2.log', 'audit-run-2.log',
              'pre-build-4.json', 'content-build-4.log']
    names += ['attempt-2-' + name for name in ('birth-inputs.json', 'birth-native-checks.log', 'audit-inputs.json',
              'audit-native-checks.log', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json')]
    files += [STAGE / name for name in names]
    result = dict(referenceVersion='3.3.93847', component='Skeleton Warrior live hard10 ATD34/hard20 existing Extra Fast',
        installed=False, goalComplete=False, completedClasses=[], build=build, run=run, auditRun=audit_run,
        verified=dict(nativeMarkers=289, retainedMagePlayerPetSentryMarkers=274, boots=3, actualReloads=2, saves=3,
            naturalUpgradedMovementSessions=3, originalSavedWarriorTypes=3, unchangedWildAffixPairs=9,
            auditMarkers=4, pythonTests=71, namedDefinitions=573, auditFingerprints=[877, 21],
            unchangedInstalledArtifacts=106, handlers=186, necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / f'{n}-inputs.json').relative_to(ROOT)),
            sha256=sha(STAGE / f'{n}-inputs.json')) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)},
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'Live owner hard points unlock34flat physical ATD at10 and the existing ExtraFast movement-only100percent at20. Real soft points do not unlock either;respec/relearn changes existing actors without birth recapture,HP write,identity/order/seed changes or a new timer/state schema.',
            'The flat amount aggregates in the existing NPC item stat and PaladinThorns melee-attempt/physical receiver. Actual native hit/miss,zero/duplicate/ranged/spell,resistance and real item8plus34controls pass. Existing Iron item/inherent/Barbs and Paladin aura scopes remain retained.',
            'Wild encounters and warriors share the extracted ExtraFast movement value and interval/floor arithmetic. Nine wild difficulty/grade pairs retain other stats and attack timing. Learned skeleton source motion doubles through the existing velocity adapter;attack12/19and760msremain unchanged.',
            'All three supported saved warrior types retain ownership,wounds,live unlocks and original types through two reloads. Three connected natural source-motion sessions preserve damage and attack clocks. All274 predecessor markers,71Python,4audits and named/catalogue checks pass;data,audit decisions,kernel and106installed hashes remain unchanged.'],
        remaining=[
            'Inherited retaliation HP/death/event order,current-executable equivalence,global RNG,geometry/PvP/Sunder and graphical/client boundaries retain their prior evidence limits. Existing Paladin/Iron shared-consumer controls are not new full chronological source proof for every aura/item combination.',
            'Retained Fire/Blood/Clay snapshot-only fixture holds use one hour rather than two minutes so the longer suite observes stable saved states;their separate natural unheld combat checks are retained. Saved candidate actors/layout declarations are not an actual installed old-DLL byte upgrade or concurrent/unheld-save acceptance. Source velocity and native profile timing are verified separately;final movement presentation still needs graphical playtesting.',
            'Mage replacement Sorceress curves/triples and recorded Skeleton Mastery mapping remain unfinished. Other approved golem/Druid upgrades,safe-area parking,barrier phases and remaining class skills stay queued.',
            'No class completion,deployment or new player binding. Existing body/movement/retaliation cues are placeholders;final effects/audio and Raise Skeleton talent/right/quick icons remain separate assets.'])
    output = ROOT / 'tools/reference/skeleton-upgrades-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Skeleton upgrades:289markers/3boots/2reloads,3natural movement sessions;274retained/4audits/71Python;installed unchanged. No class complete.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
