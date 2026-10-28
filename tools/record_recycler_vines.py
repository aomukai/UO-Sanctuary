#!/usr/bin/env python3
"""Bind prepared recycler components; learned controller/casting remain separate."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

import record_grizzly_body as inventory

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/recycler-vines'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    paths = [STAGE / name for name in [
        'stats-inputs.json', 'stats-native-checks.log', 'run.log', 'python-tests.log',
        'recycler-body-fixtures.json', 'recycler-recovery-fixtures.json',
        'body-fixtures.json', 'world-fixtures.json', 'ground-fixtures.json',
        'poison-fixtures.json', 'casting-fixtures.json', 'wolf-world-fixtures.json']]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        assert all(s in path.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        paths += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 52
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    for name, count in [('recycler-model-check', 1), ('recycler-body-check', 1),
                        ('recycler-recovery-check', 1), ('recycler-body-reload-check', 2),
                        ('recycler-recovery-reload-check', 2), ('recycler-recovery-live-check', 3),
                        ('recycler-recovery-live-result', 3)]:
        assert log.count(name + ': passed') == count, name
    assert '9180complete shared birth cases,4705supported AI111decisions and1140valid delayed-resource callbacks pass' in log
    assert 'natural delayed credit' in (STAGE / 'run.log').read_text()
    body = json.loads((STAGE / 'recycler-body-fixtures.json').read_text())
    for name, life, regen in [('Carrion', 24320, 17), ('Solar', 42240, 30)]:
        state = body[name + 'State']
        assert state['Owner'] == body['Owner'] and state['Profile'] == 'prepared-recycler-life-owner-v1'
        assert state['Snapshot']['Birth']['Life256'] == life
        assert state['Snapshot']['Birth']['Regeneration256'] == regen
    assert body['CarrionLife'] == 12007 + 3 * 17 and body['SolarLife'] == 21007 + 3 * 30
    pending = json.loads((STAGE / 'recycler-recovery-fixtures.json').read_text())
    state = pending['State']
    assert state['Owner'] == pending['Owner'] and state['Actor'] == pending['RemovedActor']
    assert state['Id'] == pending['Effect'] and state['Remaining'] == 10 and not state['Triggered'] and state['Model'] == 0
    for name, count in [('recycler-vines-birth', 9190), ('recycler-vines-ai', 7009), ('recycler-vines', 3318)]:
        path = ROOT / ('tools/reference/' + name + '-source-evidence.json')
        proof = json.loads(path.read_text())
        assert proof['referenceVersion'] == '3.3.93847' and proof['contexts'] == count and proof['sanitizerClean']
        paths.append(path)
        for name, digest in proof['artifacts'].items():
            assert sha(ROOT / name) == digest, name
            paths.append(ROOT / name)
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name, digest in audit['InputHashes'].items():
            assert sha(ROOT / name) == digest, name
        paths.append(path)
        audits.append(audit)
    previous = json.loads(subprocess.check_output(['git', 'show', '4c5d3a0:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == previous['Definitions']
    py = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
    paths += [ROOT / name for name in ['tests/check_recycler_vines.py', 'tools/record_recycler_vines.py',
        'tools/reference/recycler-vines-preflight.json', 'data/Sanctuary/Balance/class-skill-status.json',
        'data/Sanctuary/Balance/skill-effects.json', 'data/Sanctuary/Balance/skill-icons.json']]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [s for s in status['skills'] if s['implementation'] == 'handler-present']
    assert len(handlers) == 182 and sum(s['characterClass'] == 'Druid' for s in handlers) == 26 and status['completedClasses'] == []
    result = dict(referenceVersion='3.3.93847', component='Prepared Carrion Vine/Solar Creeper birth, AI policy, saved body and independent recovery',
        installed=False, learnedHandlerEnabled=False, currentExecutableParity=False, goalComplete=False,
        handlers=182, druidHandlers=26, completedClasses=[],
        verified=dict(passMarkers=52, recyclerMarkers=13, boots=3, actualReloads=2, automaticCredits=3,
            sourceBirthComparisons=9180, supportedAiDecisions=4705, sourceResourceCallbacks=1140,
            explicitProvisionalSolarRankControls=255, pythonTests=70, unchangedNamedDefinitions=573,
            inputFingerprints=[len(a['InputHashes']) for a in audits]),
        inputInventory=inputs, artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        established=[
            'Both noRatio bodies preserve exact fractional life, separate birth/innate ranks, live effective-rank capture for new summons, owner/history/facet injury and atomic save state. Two actual reloads retain those bodies and pending actor-independent recovery.',
            'Actual monster corpses reserve once and retain loot. Failed allocation keeps reservation. Player-owned delayed credit survives actor removal, uses elapsed age45/47callbacks, writes once and expires. Offline and first-loaded callbacks skip elapsed actions without backfilling. Three connected automatic timer runs credit actual life.',
            'Legacy Solar calc1/zero recovery remains a source differential control. CurrentTableSolarProvisional deliberately reads current calc2; its all-rank arithmetic is tested separately and is not current-executable verification.'],
        remaining=[
            'Automatic corpse seeking, movement, skill-mode entry and prepared/learned casting/input are not connected. Native component acceptance does not certify complete AI dispatch, modern timing/room/geometry/global event ordering or underground VINE_BEAST exposure.',
            'Solar current executable formula-slot behavior remains unresolved. The next-native-tick callback phase is a declared adapter. No learned route or underground policy is inferred, and no full class regression/installation is claimed.',
            'Final K9 body, underground/surface/feed, recycler vines/explosion, recovery overlays and audio remain identified placeholders; talent/right/quick icons are recorded separately. Main stays on the preserved 4c5d3a0 installation.'])
    path = ROOT / 'tools/reference/recycler-vines-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Recycler components:52 checks/3boots/2reloads;9180birth/4705AI/1140callback comparisons; learned routes disabled, main preserved.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
