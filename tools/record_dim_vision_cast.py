#!/usr/bin/env python3
"""Bind learned Dim Vision casting acceptance without certifying unfinished AI."""
import argparse
from collections import Counter
import json
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/dim-vision-cast'
sha = inventory.sha


def record(build, run, check=False):
    inventory.STAGE = STAGE
    inputs = {name: inventory.validate_inventory(STAGE / (name + '-inputs.json')) for name in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts'] and value['copiedData'] == inputs['birth']['copiedData']
    previous = ROOT / 'tools/reference/life-tap-cast-native-evidence.json'
    prior = json.loads(previous.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before) <= set(after)
    added = set(after) - set(before)
    changed = {p for p in before if before[p] != after[p]}
    assert added == {'tests/Sanctuary.Integration/DimVisionCastChecks.cs'}
    assert changed == {'src/Sanctuary.Content/NecromancerCurseCasts.cs'}
    assert inputs['birth']['copiedData'] == old['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/life-tap-cast/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline) + Counter({'dim-vision-cast-check': 1, 'dim-vision-cast-reload-check': 2,
        'dim-vision-cast-right-check': 3, 'dim-vision-cast-right-result': 3,
        'dim-vision-cast-quick-check': 3, 'dim-vision-cast-quick-result': 3,
        'class-skills-ai-check': 3, 'class-skills-ai-result': 3})
    assert markers(STAGE / 'birth-native-checks.log') == expected and sum(expected.values()) == 512 and sum(markers(baseline).values()) == 491
    text = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in text and ' ERR]' not in text
    assert text.count('Shutting down') == text.count('Writing world save snapshot done') == 3
    for kind in ['content', 'integration']:
        text = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(x in text for x in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    for name, count in [('named-item-runtime-audit.json', 849), ('first-loop-affix-audit.json', 21)]:
        a = json.loads((STAGE / name).read_text())
        b = json.loads((ROOT / '.local/class-skills/life-tap-cast' / name).read_text())
        assert a == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert all(a[k] == v for k, v in b.items() if k not in ['ContentAssemblyPath', 'ContentAssemblySha256', 'InputHashes'])
        assert len(a['InputHashes']) == count and a['ContentAssemblySha256'] == inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path, digest in a['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
    assert markers(STAGE / 'audit-native-checks.log') == Counter({'paladin-thorns-check': 1, 'paladin-thorns-result': 1, 'named-gear-check': 1, 'first-loop-catalogue-check': 1})
    text = (STAGE / 'audit-native-checks.log').read_text()
    assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down') == 1
    text = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in text and '\nOK\n' in text
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    source_path = ROOT / 'tools/reference/necromancer-ai-curse-source-evidence.json'
    source = json.loads(source_path.read_text())
    assert source['contexts'] == 12000 and source['difficultyDivisors'] == dict(Normal=1, Nightmare=2, Hell=4)
    for group in ['inputs', 'artifacts']:
        for path, digest in source[group].items():
            assert sha(ROOT / path) == digest, path
    for key in ['reusedReference', 'reusedReceiverCpp']:
        assert sha(ROOT / source[key]['path']) == source[key]['sha256']
    saved = json.loads((STAGE / 'dim-vision-cast-fixtures.json').read_text())
    assert saved['Generation'] == 2 and saved['Curse']['Rank'] == 21 and saved['Curse']['Motion']['Target'] == saved['Owner'] and saved['Old']['Rank'] == 1
    assert all('PhysicalDelta' not in saved[k] and 'ElementDeltas' not in saved[k] for k in ['Curse', 'Old'])
    assert json.loads((STAGE / 'life-tap-cast-fixtures.json').read_text())['HealerLife'] == 2817
    docs = ['docs/dim-vision-cast-checkpoint.md', 'docs/necromancer-ai-curse-source-review.md', 'docs/class-skill-roadmap.md',
        'docs/class-skills-resume-checkpoint.md', 'docs/necromancer-mechanical-audit.md', 'docs/skill-mechanical-reference.md',
        'docs/skill-effects.md', 'docs/skill-icons.md', 'docs/pet-adjustment-review.md']
    files = [previous, source_path, Path(__file__), *[ROOT / p for p in added | changed], *[ROOT / p for p in docs],
        ROOT / 'tools/probe_necromancer_ai_curse.py', ROOT / 'tests/check_dim_vision_cast.py', ROOT / 'tests/check_iron_golem_birth.py',
        ROOT / 'tools/reference/named-item-runtime-audit.json', ROOT / 'tools/reference/first-loop-affix-audit.json']
    files += [STAGE / p for p in [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log',
        f'run-{run}.log', f'audit-run-{run}.log', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'audit-native-checks.log', 'python-tests.log']]
    files += [STAGE / (name + '-fixtures.json') for name in ['dim-vision-cast', 'life-tap-cast', 'iron-maiden', 'lower-resist', 'decrepify', 'weaken', 'amplify-damage', 'cloak']]
    result = dict(referenceVersion='3.3.93847', component='Dim Vision learned casting, facet duration and no-stat receiver',
        installed=False, goalComplete=False, completedClasses=[], build=build, run=run,
        verified=dict(nativeMarkers=512, retainedPredecessorMarkers=491, boots=3, actualReloads=2, saves=3, auditMarkers=4,
            pythonTests=71, namedDefinitions=573, auditFingerprints=[849, 21], unchangedInstalledArtifacts=106,
            sourceDo30ReceiverContexts=12000, nativeDimVisionScalarArbitrationComparisons=3600, connectedRightQuickCasts=6,
            naturalExpiryChecks=6, naturalRank1Frames=175, mana256=2304, handlers=186, necromancerHandlers=30),
        inputInventories={name: dict(path=str((STAGE / (name + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (name + '-inputs.json'))) for name in inputs},
        changedSourceHashes={p: after[p] for p in sorted(added | changed)}, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'Learned Dim Vision uses shared do30/filter2 no-stat casting/eligibility/rank/creator arbitration. Cost9mana,radius4+(rank-1),base175+50*(rank-1)frames;configured ownerfacet divisor1/2/4precedes curse resistance and integer frame truncation. Mismatched NPCspawn difficulty no longer controls this learned spell.',
            'Weaker same-skill ranks reject;equal retains creator/motion and refreshes expiry;higher replaces. Learned blind can replace Confuse without inheriting item-blind refusal. Attract remains protected. Existing pending chase/flee adapter and source metadata persist without a new save field.',
            'Native encounter grade adapters,rank/caster deletion,facet-vs-spawn,geometry/LOS,payment/cancel/insufficient mana,real Wormskull rank21,legacy shapes and two actual pending-chase reload resumes pass. Six real right/quick inputs retain normal7second expiry. Retained real chase/obstruction/adjacent acquisition/special suppression/expiry/flee checks run on every boot.',
            '512native markers retain491predecessor markers across3boots/2reloads/3saves. Four audits,71Python,573unchanged named decisions and clean builds pass. Runtime data,staged kernel and106installed artifacts are unchanged.'],
        remaining=[
            'The3600native differential covers supported difficulty/scalar/receiver arbitration with source AI-permission/alignment/Attract gates as declared inputs;it does not independently execute every native AI eligibility path. The12000complete source pipeline also declares AI dispatch,formula VM,stat storage and radial traversal.',
            'The blind special-state20percent wander/10frame idle tick,cadence,seed/path/event integration and special-state exit callbacks remain unfinished. Existing pending-motion behavior remains an explicit native adapter;this receipt does not finish the AI state or skill/class.',
            'Source uninterruptible/exact alignment/room/species/flags/geometry and full source host/current executable/PvP/client parity remain open. Unmapped species and the Imp attachment branch are logged/refused;enemy-controlled/summoned/hireling and bard coexistence lack a verified adapter. Native encounters are not certified D2species identities.',
            'No new Mobile/save field/runtime data/handler/class completion/deployment. Final effects/audio and tree/right/quick icons remain separate placeholders;approved pet upgrades and safe-area parking are design-only.'])
    destination = ROOT / 'tools/reference/dim-vision-cast-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert destination.read_text() == rendered
    else:
        destination.write_text(rendered)
    print('Dim Vision casting:512native markers/3boots/2reloads,6real7second casts;4audits/71Python and installed preservation verified. Blind tick remains unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.check)
