#!/usr/bin/env python3
"""Record public Revive reset checks against unchanged accepted production."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import subprocess
import record_grizzly_body as inventory
from snapshot_skill_build import check as check_snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/revive-respec'
PREDECESSOR = '4aee67e'
sha = inventory.sha


def record(check=False):
    inventory.STAGE = STAGE
    current = inventory.validate_inventory(STAGE / 'respec-inputs.json')
    check_snapshot(STAGE / 'pre-build-attempt-5.json', STAGE / 'respec-inputs.json')
    receipt_path = 'tools/reference/revive-regeneration-evidence.json'
    raw = subprocess.check_output(['git', 'show', PREDECESSOR + ':' + receipt_path], cwd=ROOT)
    previous = json.loads(raw)
    assert (ROOT / receipt_path).read_bytes() == raw
    assert previous['verified']['fullClassMarkers'] == 1184 and previous['verified']['pythonTests'] == 71
    prior = previous['inputInventory']
    for name, value in prior['artifacts'].items():
        if name != 'Assemblies/Sanctuary.Integration.dll':
            assert current['artifacts'][name] == value, name
    assert current['copiedData'] == prior['copiedData']
    prior_cs = {p: h for p, h in prior['sourceInputs'].items() if p.endswith('.cs')}
    now_cs = {p: h for p, h in current['sourceInputs'].items() if p.endswith('.cs')}
    assert set(now_cs) - set(prior_cs) == {'tests/Sanctuary.Integration/ReviveRespecChecks.cs'}
    assert {p: h for p, h in now_cs.items() if p in prior_cs} == prior_cs
    snapshot = json.loads((STAGE / 'pre-build-attempt-5.json').read_text())
    for name, value in snapshot['inputs']['projectInputs'].items():
        raw_project = subprocess.check_output(['git', 'show', PREDECESSOR + ':' + name], cwd=ROOT)
        assert hashlib.sha256(raw_project).hexdigest() == value
    for path in (ROOT / 'tests').glob('test_*.py'):
        prior_test = subprocess.check_output(['git', 'show', PREDECESSOR + ':' + str(path.relative_to(ROOT))], cwd=ROOT)
        assert path.read_bytes() == prior_test
    log = (STAGE / 'respec-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed', log)) == Counter({
        'revive-respec-check': 1, 'revive-respec-result': 1, 'revive-respec-reload-check': 2,
        'revive-respec-live-check': 3, 'revive-respec-live-result': 3})
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    build = STAGE / 'integration-build-attempt-5.log'
    assert all(s in build.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    saved = json.loads((STAGE / 'isolated-revive-respec-fixtures.json').read_text())
    assert len({saved[n] for n in ['Owner', 'Actor', 'Corpse', 'Loot']}) == 4
    assert saved['AvailablePoints'] == 79 and saved['Gold'] == 1000
    installed_path = ROOT / 'tools/reference/creeper-frame-installation-evidence.json'
    installed = json.loads(installed_path.read_text())
    assert len(installed['installedArtifacts']) == 106
    for name, value in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / name) == value
    paths = [Path(__file__).resolve(), ROOT / receipt_path, installed_path,
             ROOT / 'tests/check_revive_respec.py', ROOT / 'tests/Sanctuary.Integration/ReviveRespecChecks.cs',
             build, STAGE / 'pre-build-attempt-5.json', STAGE / 'respec-inputs.json',
             STAGE / 'respec-native-checks.log', STAGE / 'isolated-revive-respec-fixtures.json', STAGE / 'run-attempt-5.log',
             STAGE / 'integration/Sanctuary.Integration.dll']
    result = dict(referenceVersion='3.3.93847', component='Public class reset of actual learned Revive actors',
        installed=False, goalComplete=False, completedClasses=[],
        verified=dict(markers=10, boots=3, actualReloads=2, saves=3, integrationBuildWarnings=0,
                      productionBinariesAndDataUnchanged=True, unchangedInstalledArtifacts=106),
        frozenPredecessor=dict(commit=PREDECESSOR, receipt=receipt_path, receiptSha256=hashlib.sha256(raw).hexdigest(),
            reusedFullClassMarkers=1184, reusedPythonTests=71,
            scope='Existing production acceptance is reused by exact content/native/data/source equality. The new integration assembly has its own10-check scope; no new full or Python run is claimed.'),
        inputInventory=current, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(paths))},
        established=['Actual learned casts revive the original NPC via automatic accepted-death capture. Parked and connected world-active originals are removed through public ClassRespec.Commit after the real combat exclusion.',
                     'A pending action refuses without charge or dismissal. Successful reset charges5000gold once, refunds trained ranks, preserves bounded current resources and leaves separate corpse loot intact. Replayed quotes refuse.',
                     'Two actual reloads retain the refund/payment/loot while the removed original and its activation/parked registrations do not return.'],
        remaining=['No class completion or deployment. Facet rescaling, independent item/stat/proc/sunder consumers and Summon Resist reference conflict remain open.',
                   'The ownerless fixture corpse was refused by shared availability and was replaced with the established player-kill path; no eligibility rule was changed or bypassed.',
                   'Final skill effects/audio and talent/hotkey icons remain separate identified placeholders.'])
    path = ROOT / 'tools/reference/revive-respec-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Revive public respec:10checks/3boots/2reloads;unchanged accepted production reused;scoped acceptance only.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
