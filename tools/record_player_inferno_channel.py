#!/usr/bin/env python3
"""Bind prepared shared player st11/do19; modern channel timing stays open."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/player-inferno-channel'


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
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 71
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('player-inferno-check: passed') == 1
    assert '19200complete-source legacy comparisons and19200explicit current-cost controls,510rank costs and actual player96FPdebit pass' in log
    for name in ['recycler-recovery-live', 'recycler-world-live', 'recycler-casting-live']:
        assert log.count(name + '-result: passed') == 3, name
    for name in ['recycler-body', 'recycler-recovery', 'recycler-world', 'recycler-casting']:
        assert log.count(name + '-reload-check: passed') == 2, name
    assert 'modern scheduling/payment frequency/input and cold flight remain open' in (STAGE / 'run.log').read_text()
    proof = ROOT / 'tools/reference/player-inferno-channel-source-evidence.json'
    data = json.loads(proof.read_text())
    assert data['referenceVersion'] == '3.3.93847' and data['contexts'] == 19200 and data['sanitizerClean']
    paths.append(proof)
    for name, digest in data['artifacts'].items():
        assert sha(ROOT / name) == digest, name
        paths.append(ROOT / name)
    frozen_receipt('300c5f3', 'tools/reference/recycler-casting-native-evidence.json')
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
    prior = json.loads(subprocess.check_output(['git', 'show', '300c5f3:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == prior['Definitions']
    py = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
    paths += [ROOT / name for name in ['tests/check_player_inferno_channel.py', 'tools/record_player_inferno_channel.py',
        'tools/reference/arctic-blast-tooltip-observation.json', 'data/Sanctuary/Balance/class-skill-status.json',
        'data/Sanctuary/Balance/skill-effects.json', 'data/Sanctuary/Balance/skill-icons.json']]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [s for s in status['skills'] if s['implementation'] == 'handler-present']
    assert len(handlers) == 182 and sum(s['characterClass'] == 'Druid' for s in handlers) == 26 and status['completedClasses'] == []
    result = dict(referenceVersion='3.3.93847', component='Prepared shared player Inferno/Arctic st11/do19 and ordinary resource helpers',
        installed=False, learnedHandlerEnabled=False, currentExecutableParity=False, goalComplete=False,
        handlers=182, druidHandlers=26, completedClasses=[],
        verified=dict(passMarkers=71, retainedMarkers=70, boots=3, actualReloads=2, sourceChannelContexts=19200,
            nativeLegacyComparisons=19200, explicitCurrentCostControls=19200, allRankCostChecks=510,
            actualPlayerRankOneDebit256=96, retainedNaturalRecyclerRuns=9, pythonTests=70, unchangedNamedDefinitions=573,
            inputFingerprints=[len(a['InputHashes']) for a in audits]),
        inputInventory=inputs, artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        frozenPredecessor=dict(commit='300c5f3', receipt='tools/reference/recycler-casting-native-evidence.json'),
        established=[
            'Shared st11/StartInferno/do19/DoInferno player branch has source/native state/resource comparisons for both skill identities. Six mana is a start threshold; fresh list20frames,existing-list renewal6frames,first-do emission suppression and ignored factory failure remain distinct from ordinary source start payment.',
            'Active bit and stat-list lease are separate. Released player do still rewinds unconditionally, unlike Blade Fury. Existing-list start rewinds once before renewal and again after successful nested do; nested aim failure does not change its successful start result. NPC timeout/duration branches are not substituted.',
            'Actual prepared player resources debit96FP at rank1under the explicit current-table per-invocation profile,with below-threshold refusal and no extra do charge. Legacy rank1zero is retained as a differential control; all255ranks per skill have independent cost checks. Neither profile establishes modern callback frequency.',
            'Arctic and Inferno use distinct range increments2and3. The first native comparison caught the shared-range fixture error; its diagnostic is archived separately. The corrected source fixture and current native skill-row formulas now agree.'],
        remaining=[
            'No production player channel,held/reentry/stop packet cadence,full SQ18/SQ6/action/end scheduling,FCR/rate composition,unpaid restart,cancellation or current channel save/input behavior is enabled or certified.',
            'User rank1Arctic tooltip:cost4,minimum6mana,range5.3yards,cold length4seconds,damage12–24. Fractional drain and synergy/gear context were not measured. Do not calibrate damage or infer exact payment frequency from those rounded readings.',
            'Current cold endpoint/mastery/pierce/chill,complete missile factory/charged flight/Size1/LastCollide/DamageRate41,world/lifecycle and graphical acceptance remain to implement and verify. Learned Arctic and Inferno remain disabled; final effects/audio and talent/left/right/quick icons remain separately listed.',
            'No full-class regression or installation is claimed. Main4c5d3a0and existing saves/client remain preserved. Vine exposure timing,Solar formula/Command and earlier class/Warlock gaps stay in scope.'])
    path = ROOT / 'tools/reference/player-inferno-channel-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Player Inferno/Arctic helpers:19200legacy +19200current-cost comparisons;71checks/70retained/3boots/2reloads; production channel remains gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
