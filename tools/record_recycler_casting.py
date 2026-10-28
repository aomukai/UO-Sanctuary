#!/usr/bin/env python3
"""Bind prepared recycler payment/replacement; public SC/input stay gated."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/recycler-casting'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    paths = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'run.log', 'python-tests.log',
        'recycler-casting-fixtures.json', 'recycler-world-fixtures.json', 'recycler-body-fixtures.json', 'recycler-recovery-fixtures.json',
        'body-fixtures.json', 'world-fixtures.json', 'ground-fixtures.json', 'poison-fixtures.json', 'casting-fixtures.json', 'wolf-world-fixtures.json']]
    for kind in ['content', 'integration']:
        p = STAGE / (kind + '-build.log')
        assert all(s in p.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        paths += [p, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 70
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    for name in ['recycler-model', 'recycler-body', 'recycler-recovery', 'recycler-world', 'recycler-casting']:
        assert log.count(name + '-check: passed') == 1, name
    for name in ['recycler-body', 'recycler-recovery', 'recycler-world', 'recycler-casting']:
        assert log.count(name + '-reload-check: passed') == 2, name
    for name in ['recycler-recovery-live', 'recycler-world-live', 'recycler-casting-live']:
        assert log.count(name + '-check: passed') == log.count(name + '-result: passed') == 3, name
    assert '10mana Carrion/14rank1Solar/15soft-rank2Solar' in log and 'three-vine replacement,independent recovery' in log
    assert 'public learned SC/input and exact surface windows remain gated' in (STAGE / 'run.log').read_text()
    saved = json.loads((STAGE / 'recycler-casting-fixtures.json').read_text())
    state = saved['State']
    assert saved['Body'] > 0 and state['Owner'] == saved['Owner'] and saved['Wolf'] > 0 and saved['Raven'] > 0
    assert state['Snapshot']['Birth']['Ability'] == 179 and state['Snapshot']['Birth']['Rank'] == 1
    assert state['Controller']['RecoveryModel'] == 1 and state['Controller']['Fault'] is None and saved['Mana'] > 0
    frozen_receipt('dc51130', 'tools/reference/recycler-world-native-evidence.json')
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        p = STAGE / name
        audit = json.loads(p.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name, digest in audit['InputHashes'].items():
            assert sha(ROOT / name) == digest, name
        paths.append(p)
        audits.append(audit)
    prior = json.loads(subprocess.check_output(['git', 'show', 'dc51130:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == prior['Definitions']
    py = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
    paths += [ROOT / name for name in ['tests/check_recycler_casting.py', 'tools/record_recycler_casting.py',
        'tools/reference/vine-exposure-observation.json', 'data/Sanctuary/Balance/class-skill-status.json',
        'data/Sanctuary/Balance/skill-effects.json', 'data/Sanctuary/Balance/skill-icons.json']]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [s for s in status['skills'] if s['implementation'] == 'handler-present']
    assert len(handlers) == 182 and sum(s['characterClass'] == 'Druid' for s in handlers) == 26 and status['completedClasses'] == []
    result = dict(referenceVersion='3.3.93847', component='Prepared Carrion/Solar do115 release payment, placement and vine replacement',
        installed=False, learnedHandlerEnabled=False, currentExecutableParity=False, goalComplete=False,
        handlers=182, druidHandlers=26, completedClasses=[],
        verified=dict(passMarkers=70, recyclerMarkers=31, boots=3, actualReloads=2, automaticPreparedCastFeedRuns=3,
            retainedAutomaticWorldRuns=3, retainedIndependentCredits=3, pythonTests=70, unchangedNamedDefinitions=573,
            inputFingerprints=[len(a['InputHashes']) for a in audits]),
        inputInventory=inputs, artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        frozenPredecessor=dict(commit='dc51130', receipt='tools/reference/recycler-world-native-evidence.json'),
        established=[
            'Both prepared do115 releases reuse existing zero-footprint vine destination and shared-category replacement. Actual payment is10mana for Carrion,14for rank1Solar and15for item-boosted rank2Solar. Allocation/payment failures retain the original injured vine and its resources; insufficient fractional mana does not allocate.',
            'Selected moving/deleted target destination, occupied ground, invalid location/clock/profile, frozen/paralyzed owner, actual Tarnhelm birth/payment capture and town casting pass. Replacement crosses all three vine species while preserving wolves/Ravens. An existing player-owned delayed heal survives the old vine being replaced.',
            'Paid actor/profile/fractional-resource state and coexisting summon references survive two actual reloads; each post-load replacement pays once. Three connected timer runs commit a prepared cast, automatically feed and heal while retaining one mana payment. These invoke the prepared release directly, not public learned SC/right/quick input.'],
        remaining=[
            'The user confirms underground vines cannot take damage and surfaced vines can. Exact exposure start/end frames and relation to K9control/client-only vine visuals remain unmeasured; current visible vulnerable bodies are identified preparation, not correct underground exposure.',
            'Public learned SC/FCR/right/quick, movement/stop/hit-recovery cancellation, unpaid restart, item-only permission and transformed casting remain unverified. Learned Carrion/Solar stay disabled. Solar current calc2versus older do33calc1and innate Battle Command transport remain explicitly gated.',
            'Current room/vision/geometry/global event timing, native source0x40vision projection, corpse-owner metadata and source AStar limits remain as in the frozen world evidence. No full-class regression or installation is claimed; main4c5d3a0and existing saves/client are preserved. Final effects/audio and separate icons remain placeholders.'])
    path = ROOT / 'tools/reference/recycler-casting-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Prepared recycler release:70checks/3boots/2reloads,3natural cast/feed/credit runs; public learned SC/input and exposure windows remain gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
