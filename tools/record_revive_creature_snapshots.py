#!/usr/bin/env python3
"""Bind original creature descriptor persistence to the tested candidate."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess

import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_snapshot
from probe_warlock_runtime import ROOT, digest

STAGE = ROOT / '.local/class-skills/revive-creature-snapshots'
PREDECESSOR = 'f0dc651'


def sha(path):
    return digest(path.read_bytes())


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    check_snapshot(STAGE / 'pre-build-inputs.json', STAGE / 'stats-inputs.json')
    _, prior = frozen_receipt(PREDECESSOR, 'tools/reference/revive-source-policy-native-evidence.json')
    assert prior['verified']['passMarkers'] == 299
    artifacts = [Path(__file__).resolve()]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        assert all(s in path.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 302
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    pattern = r'\] ([A-Za-z0-9-]+): passed'
    before = Counter(re.findall(pattern, (ROOT / '.local/class-skills/revive-source-policy/stats-native-checks.log').read_text()))
    after = Counter(re.findall(pattern, log))
    assert sum(before.values()) == 299 and not before - after
    assert after - before == Counter({'revive-creature-snapshot-check': 1, 'revive-creature-snapshot-reload-check': 2})
    assert log.count('Revive creature snapshots:9actual deaths across5native types/3source species/3difficulties retain concrete type,AI/combat descriptors,raw fields and loot;unmapped capture is explicit,independent decoded profiles,6invalid payloads per corpse and old-v0 compatibility pass;native ability state,in-place reconstruction and full actor lifecycle remain gated') == 3
    saved = json.loads((STAGE / 'revive-creature-snapshot-fixtures.json').read_text())
    rows = saved['Rows']
    assert len(rows) == len(saved['Loot']) == 9
    assert len({row['ReviveCreature']['RuntimeType'] for row in rows}) == 5
    assert len({row['Source'] for row in rows}) == 3
    assert len({json.loads(row['ReviveCreature']['ActiveCombatJson'])['Difficulty'] for row in rows}) == 3
    assert all(not row['ReviveCreature']['NativeAbilityStateCaptured'] and row['ReviveSnapshotIssue'] is None for row in rows)
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name_, hash_ in audit['InputHashes'].items():
            assert sha(ROOT / name_) == hash_, name_
        audits.append(audit)
        artifacts += [path, ROOT / 'tools/reference' / name]
    old = json.loads(subprocess.check_output(['git', 'show', PREDECESSOR + ':tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert audits[0]['Definitions'] == old['Definitions'] and len(audits[0]['Definitions']) == 573
    fingerprints = [len(a['InputHashes']) for a in audits]
    assert fingerprints == [767, 21]
    tests = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
    main_path = ROOT / 'tools/reference/creeper-frame-installation-evidence.json'
    main = json.loads(main_path.read_text())
    assert len(main['installedArtifacts']) == 106
    for name, hash_ in main['installedArtifacts'].items():
        assert sha(Path(main['runtimeRoot']) / name) == hash_, name
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert sum(s['implementation'] == 'handler-present' for s in status['skills']) == 184
    # This increment changes source and persistence, not balance, assets or runtime data.
    for path in (ROOT / 'data/Sanctuary').rglob('*'):
        if path.is_file():
            assert path.read_bytes() == subprocess.check_output(['git', 'show', PREDECESSOR + ':' + str(path.relative_to(ROOT))], cwd=ROOT), path
    diagnostic = STAGE / 'diagnostics/attempt1'
    assert 'Historical Revive identity/combat descriptor disagrees with its corpse' in (diagnostic / 'stats-native-checks.log').read_text()
    artifacts += [p for p in diagnostic.rglob('*') if p.is_file()]
    for name in ['pre-build-inputs.json', 'stats-inputs.json', 'stats-native-checks.log', 'run.log', 'python-tests.log', 'revive-creature-snapshot-fixtures.json']:
        artifacts.append(STAGE / name)
    artifacts += [main_path, ROOT / 'tests/check_revive_creature_snapshots.py', ROOT / 'tools/build_skill_roadmap.py', ROOT / 'tools/build_property_integration_backlog.py', ROOT / 'tools/reference/item-property-integration-backlog.json', ROOT / 'tools/reference/uo-monster-strength-classification-policy.json']
    result = dict(referenceVersion='3.3.93847', component='Optional original native creature descriptors on corpse records', installed=False, handlerEnabled=False, reviveActorImplemented=False, goalComplete=False, handlers=184, completedClasses=[], processMemoryRead=False,
        verified=dict(passMarkers=302, retainedMarkers=299, worldBoots=3, actualWorldReloads=2, actualDeaths=9, nativeTypes=5, sourceSpecies=3, difficulties=3, invalidPayloadChecksPerBoot=54, oldV0Compatible=True, preBuildAndNativeSourceDataMatch=True, pythonTests=71, unchangedNamedDefinitions=573, inputFingerprints=fingerprints, unchangedMainInstallationArtifacts=106, newFullClassRun=False),
        frozenPredecessor=dict(commit=PREDECESSOR, receipt='tools/reference/revive-source-policy-native-evidence.json'), inputInventory=inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))},
        established=[
            'Actual deaths save concrete runtime type/assembly, AI/FightMode identifiers, body/hue/raw attributes, native or actively applied damage endpoints and a detached active combat/profile descriptor. Proposed disabled variants are not substituted for live values. No native object reference is retained.',
            'Nine real deaths across five concrete native types, three source species and three difficulties retain descriptors and loot through two actual reloads. Independently decoded profiles do not share mutable dictionaries. Six malformed payloads per corpse are rejected; old v0 records without either new field remain loadable and report unavailable reconstruction explicitly.',
            'Special-ability state remains explicitly incomplete. Saved types are not resolved or instantiated during world load. Unsupported descriptor capture can record an issue without preventing other corpse mechanics. The unmapped lich is explicitly rejected; no guessed D2 counterpart is assigned.',
            'The first native attempt used an unmapped lich in a mapped-source fixture and failed before any save. Its inputs, binaries, sources and logs are preserved. The corrected matrix uses mapped dragon/drake/native skeleton cases and separately verifies unmapped capture refusal.',
            'The approved relative-strength policy for native UO creatures without direct D2 counterparts is documented separately. Tier criteria and assignments remain pending. All runtime balance and asset data, named eligibility decisions and installed artifacts are unchanged.'],
        remaining=[
            'No reconstructed Revive actor or learned handler is enabled. Native special-ability restoration, complete state/passive/sunder/event transfer, AI permission, historical GUID reservation and owner/death/replacement/facet/logout/respec/save lifecycle remain unfinished.',
            'AI/FightMode and combat descriptors are not proof of native ability state or modern executable equivalence. Different combat consumers still require explicit integration. Effects/audio and talent/hotkey icons remain separate placeholders. No class is complete.'])
    output = ROOT / 'tools/reference/revive-creature-snapshot-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Revive descriptors:302 native/71 Python;9 real deaths,5 native types,2 reloads;actor and abilities remain unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
