#!/usr/bin/env python3
"""Bind real post-load Confuse combat to the retained native cohort."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/confuse-resume'
sha = inventory.sha


def record(check=False):
    inventory.STAGE = STAGE
    inputs = {name: inventory.validate_inventory(STAGE / (name + '-inputs.json')) for name in ('birth', 'audit')}
    for name, row in inputs.items():
        snapshot(STAGE / 'pre-build-1.json', STAGE / (name + '-inputs.json'))
        assert row['artifacts'] == inputs['birth']['artifacts']
        assert row['copiedData'] == inputs['birth']['copiedData']
    parent_path = ROOT / 'tools/reference/parking-boundaries-native-evidence.json'
    parent = json.loads(parent_path.read_text())
    prior_ref = parent['inputInventories']['birth']
    assert sha(ROOT / prior_ref['path']) == prior_ref['sha256']
    prior = json.loads((ROOT / prior_ref['path']).read_text())
    before = {p: h for p, h in prior['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    added = {'tests/Sanctuary.Integration/ConfuseResumeChecks.cs'}
    changed = {'tests/Sanctuary.Integration/ConfuseCastChecks.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    old_fixture = subprocess.check_output(['git', 'show', '6ec3725:tests/Sanctuary.Integration/ConfuseCastChecks.cs'], cwd=ROOT, text=True)
    expected_fixture = old_fixture.replace('private static PlayerMobile Owner(', 'internal static PlayerMobile Owner(').replace('private static GraveyardSkeleton Enemy(', 'internal static GraveyardSkeleton Enemy(')
    assert (ROOT / next(iter(changed))).read_text() == expected_fixture
    assert {p: h for p, h in before.items() if p.startswith('src/')} == {p: h for p, h in after.items() if p.startswith('src/')}
    assert inputs['birth']['copiedData'] == prior['copiedData']
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == prior['artifacts'][name]
    baseline = ROOT / '.local/class-skills/parking-boundaries/birth-native-checks.log'
    assert sha(baseline) == parent['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    assert sum(expected.values()) == 406
    expected.update({'confuse-resume-check': 1, 'confuse-resume-reload-check': 2, 'confuse-resume-reload-result': 2})
    assert sum(expected.values()) == 411 and markers(STAGE / 'birth-native-checks.log') == expected
    focused = ROOT / '.local/class-skills/confuse-resume-targeted'
    focused_counts = markers(focused / 'birth-native-checks.log')
    assert sum(focused_counts.values()) == 14 and focused_counts['confuse-resume-reload-result'] == 2
    for stage in (STAGE, focused):
        text = (stage / 'birth-native-checks.log').read_text()
        assert ': failed' not in text and ' ERR]' not in text
        assert text.count('Shutting down') == text.count('Writing world save snapshot done') == 3
        assert text.count('Confuse resume: generation=1, natural draws=') == 1
        assert text.count('Confuse resume: generation=2, natural draws=') == 1
        saved = json.loads((stage / 'confuse-resume-fixtures.json').read_text())
        assert saved['Generation'] == 2 and saved['Mind']['Mode'] == 3 and saved['Mind']['Draws'] >= 2
        assert saved['Curse']['UntilUtc'] == saved['Mind']['UntilUtc']
        assert len({saved['Owner'], saved['Actor'], saved['Victim']}) == 3
    for name in ('run-1.log', 'targeted-run-1.log'):
        assert 'pass across3boots/2reloads' in (STAGE / name).read_text()
    for kind in ('content', 'integration'):
        text = (STAGE / (kind + '-build-1.log')).read_text()
        assert all(word in text for word in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    for name, count in (('named-item-runtime-audit.json', 888), ('first-loop-affix-audit.json', 21)):
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert len(audit['InputHashes']) == count and audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest
        old = json.loads(subprocess.check_output(['git', 'show', '6ec3725:tools/reference/' + name], cwd=ROOT))
        strip = lambda d: {k: v for k, v in d.items() if k not in ('ContentAssemblyPath', 'ContentAssemblySha256', 'InputHashes')}
        assert strip(audit) == strip(old)
    assert markers(STAGE / 'audit-native-checks.log') == Counter({'paladin-thorns-check': 1, 'paladin-thorns-result': 1, 'named-gear-check': 1, 'first-loop-catalogue-check': 1})
    assert 'exported from the selected build' in (STAGE / 'audit-run-1.log').read_text()
    assert ': failed' not in (STAGE / 'audit-native-checks.log').read_text() and ' ERR]' not in (STAGE / 'audit-native-checks.log').read_text()
    assert 'Ran 71 tests' in (STAGE / 'python-tests.log').read_text() and '\nOK\n' in (STAGE / 'python-tests.log').read_text()
    assert '573 definitions accounted for' in (STAGE / 'named-readiness.log').read_text()
    assert 'Eight classes, 240 source skills' in (STAGE / 'catalogue-check.log').read_text()
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [row for row in status['skills'] if row['implementation'] == 'handler-present']
    assert len(handlers) == 186 and sum(row['characterClass'] == 'Necromancer' for row in handlers) == 30 and not status['completedClasses']
    docs = ['confuse-resume-checkpoint', 'confuse-cast-checkpoint', 'class-skills-resume-checkpoint', 'class-skill-roadmap', 'class-skill-reuse-notes', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons']
    files = [parent_path, baseline, Path(__file__).resolve(), ROOT / 'tests/check_confuse_resume.py',
             *[ROOT / p for p in added | changed], *[ROOT / 'docs' / (name + '.md') for name in docs]]
    files += [p for p in STAGE.iterdir() if p.is_file()]
    files += [focused / name for name in ('birth-native-checks.log', 'birth-inputs.json', 'confuse-resume-fixtures.json')]
    result = dict(referenceVersion='3.3.93847', component='Actual autonomous Confuse acquisition and combat after two native reloads',
        productionChanged=False, installed=False, goalComplete=False, completedClasses=[], build=1, run=1, auditRun=1,
        verified=dict(nativeMarkers=411, retainedMarkers=406, focusedNativeMarkers=14, actualReloads=2, boots=3, saves=3,
                      connectedPostLoadFights=4, pythonTests=71, namedDefinitions=573, auditMarkers=4, auditFingerprints=[888, 21],
                      unchangedInstalledArtifacts=106, handlers=186, necromancerHandlers=30),
        inputInventories={name: dict(path=str((STAGE / (name + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (name + '-inputs.json'))) for name in inputs},
        changedSourceHashes={p: after[p] for p in sorted(added | changed)},
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'Connected actors resume normal source-algorithm faction selection and ordinary species combat after both actual reloads in focused and full cohorts. The fixture does not assign a combat target, manually invoke Think, rewrite the seed or advance draws on load. Saved RNG advances independently checked against every observed natural draw.',
            'Exact saved curse, override, deadlines, fractional resources, rank and binding survive. Real player or monster HP loss is required before saving the next generation. Neutral attackability remains; no pet ownership, friendly immunity, owner mana debit or held-expiry reset is granted.',
            '411 native markers retain all406 accepted parking/pet/curse scopes across3boots/2reloads/3saves. Focused14markers and four shared audits pass.71Python/573named/eight-class240 and clean builds pass.888/21fingerprints preserve decisions. Production C#,data,kernel and installed106are unchanged.'],
        remaining=[
            'The long retained regression explicitly holds this inspection fixture expiry before its first save; this does not replace existing real ten-second Confuse expiry/input tests. The fixture still uses native scan/LOS/tie/acquisition-clock and species-action adapters, not full current-executable/source-node/global-RNG parity.',
            'Unmapped alignments, arbitrary minion/conversion/room/credit/PvP/Sunder and legacy bare-override reconstruction remain explicit. Barrier death/collision/group behavior is independently provisional. No class completion, deployment or new art/binding is claimed;effects/audio and talent/hotkey icons remain separate.'])
    output = ROOT / 'tools/reference/confuse-resume-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Confuse resume:411markers retaining406;4connected post-load fights/2reloads;production and installed unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
