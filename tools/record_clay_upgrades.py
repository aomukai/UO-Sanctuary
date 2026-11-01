#!/usr/bin/env python3
"""Bind approved Clay damage and defensive affixes to the shared runtime consumers."""
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
STAGE = ROOT / '.local/class-skills/clay-upgrades'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {name: inventory.validate_inventory(STAGE / f'{name}-inputs.json') for name in ('birth', 'audit')}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / f'{name}-inputs.json')
        assert value['artifacts'] == inputs['birth']['artifacts']
        assert value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/skeleton-upgrades-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed = {'src/Sanctuary.Content/' + n + '.cs' for n in (
        'EncounterProfiles', 'EncounterCreature', 'NecromancerSummons', 'ClayGolemBody')} | {
        'tests/Sanctuary.Integration/' + n + '.cs' for n in ('ClayGolemCombatChecks', 'ClayGolemWorldChecks')}
    added = {'src/Sanctuary.Content/ClayGolemUpgrades.cs', 'tests/Sanctuary.Integration/ClayGolemUpgradeChecks.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    assert inputs['birth']['copiedData'] == old['copiedData']
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/skeleton-upgrades/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    expected['clay-upgrades-check'] += 1
    expected['clay-upgrades-reload-check'] += 2
    expected['clay-upgrades-live-result'] += 3
    assert sum(expected.values()) == 295 and markers(STAGE / 'birth-native-checks.log') == expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('Clay upgrades:real hard9+soft1/19+soft1 gates') == 1
    assert log.count('Clay combat: FP273..546,5percent intrinsic') == 1
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ('content', 'integration'):
        build_log = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in build_log for s in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    for name, count in (('named-item-runtime-audit.json', 878), ('first-loop-affix-audit.json', 21)):
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert len(audit['InputHashes']) == count
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for source, digest in audit['InputHashes'].items():
            assert sha(ROOT / source) == digest
        prior_audit = json.loads(subprocess.check_output(['git', 'show', 'bfab5bb:tools/reference/' + name], cwd=ROOT))
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
    # The replacement adds no actor saved fields or versions.
    for name in ('NecromancerSummons', 'ClayGolemBody', 'ClayGolemWorldBody'):
        path = f'src/Sanctuary.Content/{name}.cs'
        original = subprocess.check_output(['git', 'show', 'bfab5bb:' + path], cwd=ROOT, text=True)
        current = (ROOT / path).read_text()
        for pattern in (r'\[SerializableField\([^]]+\)\]\s*private[^;]+;', r'\[SerializationGenerator\([^]]+\)\]'):
            assert re.findall(pattern, original) == re.findall(pattern, current)
    saved = json.loads((STAGE / 'clay-upgrades-fixtures.json').read_text())
    assert saved['Generation'] == 2 and saved['Body'] != saved['Legacy'] and saved['Rank'] == 20
    docs = ('clay-upgrades-checkpoint', 'pet-adjustment-review', 'class-skill-reuse-notes',
            'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons')
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_clay_upgrades.py',
             *[ROOT / p for p in changed | added], *[ROOT / f'docs/{name}.md' for name in docs]]
    names = [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log',
             f'run-{run}.log', f'audit-run-{audit_run}.log', 'pre-build-1.json', 'content-build-1.log',
             'pre-build-2.json', 'content-build-2.log', 'birth-inputs.json', 'birth-native-checks.log',
             'audit-inputs.json', 'audit-native-checks.log', 'python-tests.log', 'named-readiness.log',
             'catalogue-check.log', 'clay-upgrades-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json']
    files += [STAGE / name for name in names]
    result = dict(referenceVersion='3.3.93847', component='Approved Clay Skeleton curve/Golem Mastery and live Stone Skin/Magic Resistant',
        installed=False, goalComplete=False, completedClasses=[], build=build, run=run, auditRun=audit_run,
        verified=dict(nativeMarkers=295, retainedWarriorMagePlayerPetSentryMarkers=289, boots=3, actualReloads=2, saves=3,
            naturalUpgradedClayContacts=3, originalSavedClayTypes=2, unchangedWildStoneSkinPairs=6, actualFacetTransitions=3,
            normalRank4NoMasteryMinimum256=273, normalRank4NoMasteryExclusiveMaximum256=547,
            auditMarkers=4, pythonTests=71, namedDefinitions=573, auditFingerprints=[878, 21],
            unchangedInstalledArtifacts=106, handlers=186, necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / f'{n}-inputs.json').relative_to(ROOT)),
            sha256=sha(STAGE / f'{n}-inputs.json')) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)},
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'Clay raw damage and whole-number native display/fallback share SkeletonStats.WeaponBase with captured Clay rank and Golem Mastery. Skeleton Mastery and old innate Clay/passive Fire synergy damage do not enter that replacement. Exact bodies retain the shared fractional NPC damage,critical,prepared contact,item,aura,Enchant and debuff consumers.',
            'Hard10StoneSkin and hard20MagicResistant read live owner Clay hard points. Defense uses the extracted unchanged encounter double;native physical/fire/lightning/cold/poison seeds plus existing equipment contributions enter the same ordered resistance helper,including already-immune and two-immunity restrictions. Magic resistance is a context input,not a new bonus channel.',
            'Real soft-point boundaries,training/respec/relearn,actual incoming physical/fire packets,unchanged captured damage/resources/order/seed/attack clock and three owner-facet transitions pass. Six wild Unique/Minion difficulty pairs retain their defense inheritance and other stats.',
            'Modern exact and original plain Clay actors retain their original types,ownership and wounds through two reloads. Exact actor packet/maximum/defense/resistances/ranks/order/stream/10action18end clock survive. Three natural learned contacts release the approved captured physical packet. All289 predecessor marker scopes remain;the retained rank4combat numeric expectation is intentionally updated from1049..2623to273..546FP for this approved formula.',
            'Both clean builds,71Python,4audits,573named and eight-class240checks pass. Decision payloads,data,kernel and106installed hashes remain unchanged;878/21fingerprints are fresh. No saved actor field/version or new player binding is added.'],
        remaining=[
            'The original plain Minion keeps its older native melee/life/controller path;updating its curve/affixes and preserving its type is not a full source-frame/FP packet migration. Exact existing Clay subtypes use the reviewed packet/controller path. Actual installed old-DLL upgrades and concurrent/unheld save acceptance remain separate.',
            'Shared hit/result/health/death/event order,modern world/geometry/global RNG/PvP/Sunder/client boundaries retain their prior evidence limits. Affix resistance order uses current host native seeds/equipment followed by the unchanged approved encounter policy;this is an explicit Sanctuary composition,not new D2R source proof.',
            'Blood/Iron/Fire replacement damage and hard-point upgrades remain queued. Mage replacement Sorceress curves/triples and Skeleton Mastery mapping,safe-area parking,barrier phases,Druid upgrades and remaining class skills stay unfinished.',
            'No deployment or class completion. Clay body/cast/contact/slow/defensive cues remain placeholders;final effects/audio and Clay Golem talent/right/quick icons stay in separate registers.'])
    output = ROOT / 'tools/reference/clay-upgrades-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Clay upgrades:295markers/3boots/2reloads,3natural learned contacts;289retained/4audits/71Python;installed unchanged. No class complete.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
