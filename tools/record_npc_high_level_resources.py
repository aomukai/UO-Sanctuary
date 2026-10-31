#!/usr/bin/env python3
"""Bind higher-level NPC resource checks to the complete regression build."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
import build_skill_roadmap

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/npc-high-level-resources'
FROZEN = '6b18590'
sha = inventory.sha


def markers(path):
    return Counter(re.findall(r'\b([a-z0-9-]+): passed\b', path.read_text()))


def record(build, run, check=False):
    inventory.STAGE = STAGE
    inputs = {scope: inventory.validate_inventory(STAGE / (scope + '-inputs.json'))
              for scope in ['birth', 'audit', 'class']}
    for scope, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (scope + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts']
        assert value['copiedData'] == inputs['birth']['copiedData']
    prior_path = ROOT / 'tools/reference/iron-golem-casting-evidence.json'
    prior = json.loads(prior_path.read_text())
    old = prior['inputInventory']
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(after) - set(before) == {'tests/Sanctuary.Integration/NpcHighLevelResourceChecks.cs'}
    assert not set(before) - set(after)
    assert {p for p in before if before[p] != after[p]} == {
        'src/Sanctuary.Content/NpcResourceValues.cs',
        'tests/Sanctuary.Integration/ClassSkillChecks.cs'}
    assert inputs['birth']['copiedData'] == old['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]

    source_path = ROOT / 'tools/reference/npc-high-level-resources-source-evidence.json'
    source = json.loads(source_path.read_text())
    assert source['cases'] == 768
    for name, digest in source['artifacts'].items():
        assert sha(ROOT / name) == digest, name
    assert not (ROOT / '.local/class-skills/npc-high-level-resources-source/sanitizer.log').read_text()
    baseline = ROOT / '.local/class-skills/iron-golem-casting/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    assert markers(STAGE / 'birth-native-checks.log') == markers(baseline) + Counter({'npc-high-level-resource-check': 3})
    assert sum(markers(STAGE / 'birth-native-checks.log').values()) == 97
    for scope in ['birth', 'class', 'audit']:
        text = (STAGE / (scope + '-native-checks.log')).read_text()
        assert ': failed' not in text and ' ERR]' not in text
        assert text.count('Shutting down') == (1 if scope == 'audit' else 3)
    full = (STAGE / 'class-native-checks.log').read_text()
    assert 'including two save/reload cycles.' in (STAGE / f'full-run-{run}.log').read_text()
    assert full.count('raven-body-check: passed') == 1
    assert full.count('raven-body-reload-check: passed') == 2
    assert full.count('class-skills-check: passed') == 1
    assert full.count('class-skills-reload-check: passed') == 2
    full_count = sum(markers(STAGE / 'class-native-checks.log').values())
    full_saves = full.count('Writing world save snapshot done')
    # This invocation leaves the two optional catalogue/named environment
    # switches off. Their first-boot export is verified in the separate audit
    # scope below; the three optional named save/reload markers are not claimed.
    assert full_count == 1180 and full_saves == 35
    assert not any(name.startswith(('named-gear-', 'first-loop-catalogue-'))
                   for name in markers(STAGE / 'class-native-checks.log'))
    assert markers(STAGE / 'audit-native-checks.log') == Counter({
        'paladin-thorns-check': 1, 'paladin-thorns-result': 1,
        'named-gear-check': 1, 'first-loop-catalogue-check': 1})
    for kind in ['content', 'integration']:
        text = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(t in text for t in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        previous = json.loads(subprocess.check_output(['git', 'show', FROZEN + ':tools/reference/' + name], cwd=ROOT))
        for key, value in previous.items():
            if key not in ['ContentAssemblyPath', 'ContentAssemblySha256', 'InputHashes']:
                assert audit[key] == value, key
        audits.append(audit)
    assert [len(a['InputHashes']) for a in audits] == [817, 21]
    assert len(audits[0]['Definitions']) == 573
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert status == build_skill_roadmap.build() and not status['completedClasses']
    handlers = [s for s in status['skills'] if s['implementation'] == 'handler-present']
    assert len(handlers) == 186 and sum(s['characterClass'] == 'Necromancer' for s in handlers) == 30
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    assert sha(STAGE / 'client/cuo.dll') == prior['clientSha256']
    artifacts = [Path(__file__).resolve(), prior_path, source_path, baseline]
    artifacts += [STAGE / name for name in [
        f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log',
        f'run-{run}.log', f'audit-run-{run}.log', f'full-run-{run}.log', 'python-tests.log',
        'birth-inputs.json', 'birth-native-checks.log', 'audit-inputs.json', 'audit-native-checks.log',
        'class-inputs.json', 'class-native-checks.log', 'iron-golem-cast-fixtures.json']]
    artifacts += [ROOT / name for name in [
        'tests/check_npc_high_level_resources.py', 'tests/check_npc_high_level_resources_audits.py',
        'tests/check_class_skills.py', 'docs/npc-high-level-resources-checkpoint.md',
        'docs/class-skills-resume-checkpoint.md', 'docs/class-skill-roadmap.md',
        'docs/skill-mechanical-reference.md', 'docs/skill-effects.md', 'docs/skill-icons.md',
        'tools/reference/named-item-runtime-audit.json', 'tools/reference/first-loop-affix-audit.json']]
    result = dict(referenceVersion='3.3.93847', component='NPC resource levels above the player cap',
        installed=False, goalComplete=False, completedClasses=[], build=build, run=run,
        verified=dict(sourceCases=768, focusedMarkers=97, fullRegressionMarkers=full_count,
            fullRegressionSaves=full_saves, fullOptionalCatalogueAndNamedChecks=False,
            bootsPerScope=3, actualReloadsPerScope=2,
            auditMarkers=4, pythonTests=71, namedDefinitions=573,
            auditFingerprints=[817, 21], unchangedInstalledArtifacts=106,
            handlers=186, necromancerHandlers=30, unchangedClient=True),
        inputInventories=inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts},
        established=[
            'Only NPC resource validation is widened; player progression remains capped at99. '
            'Raven birth can retain owner level plus effective ranks, including above99. '
            'The shared graph getter no longer fails during facet changes or Battle Orders capture.',
            '768 complete older-source resource/attachment routines match the native getter at '
            'levels100/104/255/352. The complete source prefix is unchanged and sanitizers are clean.',
            'The legacy class check now expects trained Iron Golem to be available. '
            'Focused Iron input/payment/original/reload checks and full regression are retained.'],
        remaining=[
            'Regression acceptance is not a full-class D2R parity certificate. '
            'Modern executable, graphical client and per-skill uncertainties remain documented.',
            'Optional named-item reload checks were not enabled in the full run. '
            'Current catalogue/named export and the retained timed Paladin check pass in a separate boot.',
            'Explicit Iron item/proc/Sunder/recipient refusals remain; this fix does not implement them.',
            'The staged candidate is uninstalled. Installed build/saves are unchanged. '
            'No assets added; final effects/audio and talent/hotkey icons remain separate.'])
    path = ROOT / 'tools/reference/npc-high-level-resources-native-evidence.json'
    text = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print(f'NPC resources:768source cases,97focused/{full_count}full markers,'
          f'{full_saves}full saves,71Python;186handlers/Necromancer30,no class complete.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.check)
