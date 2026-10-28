#!/usr/bin/env python3
"""Bind supplied-intent SQ18/SQ6 sequence composition; public input stays open."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/player-inferno-sequence'


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
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 74
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('player-inferno-sequence-check: passed') == 1 and log.count('player-inferno-sequence-reload-check: passed') == 2
    assert log.count('4608complete-source contexts/188928frames plus4224explicit current-cost controls/173184frames,17664JSON continuations pass') == 3
    assert log.count('finite rank1control16debits/14attempts and early-reentry no-side-effect guards pass') == 3
    assert log.count('player-inferno-check: passed') == 1
    for name in ['recycler-recovery-live', 'recycler-world-live', 'recycler-casting-live']:
        assert log.count(name + '-result: passed') == 3, name
    assert 'modern input/FCR/payment frequency and cold flight remain open' in (STAGE / 'run.log').read_text()
    proof = ROOT / 'tools/reference/player-inferno-sequence-source-evidence.json'
    source = json.loads(proof.read_text())
    assert source['referenceVersion'] == '3.3.93847' and source['contexts'] == 4608 and source['frames'] == 188928 and source['sanitizerClean']
    paths.append(proof)
    for name, digest in source['artifacts'].items():
        assert sha(ROOT / name) == digest, name
        paths.append(ROOT / name)
    frozen_receipt('c3e2cc4', 'tools/reference/player-inferno-channel-native-evidence.json')
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
    previous = json.loads(subprocess.check_output(['git', 'show', 'c3e2cc4:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == previous['Definitions']
    py = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
    paths += [ROOT / name for name in ['tests/check_player_inferno_sequence.py', 'tools/record_player_inferno_sequence.py',
        'tools/reference/arctic-blast-tooltip-observation.json', 'data/Sanctuary/Balance/class-skill-status.json',
        'data/Sanctuary/Balance/skill-effects.json', 'data/Sanctuary/Balance/skill-icons.json']]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [s for s in status['skills'] if s['implementation'] == 'handler-present']
    assert len(handlers) == 182 and sum(s['characterClass'] == 'Druid' for s in handlers) == 26 and status['completedClasses'] == []
    result = dict(referenceVersion='3.3.93847', component='Prepared supplied-intent player SQ18/SQ6 mode/action/end/channel composition',
        installed=False, learnedHandlerEnabled=False, currentExecutableParity=False, goalComplete=False,
        handlers=182, druidHandlers=26, completedClasses=[],
        verified=dict(passMarkers=74, retainedMarkers=71, retainedWorldBoots=3, actualRetainedWorldReloads=2,
            sourceContexts=4608, sourceFrames=188928, legacyContextsPerBoot=4608, legacyFramesPerBoot=188928,
            currentCostControlsPerBoot=4224, currentCostFramesPerBoot=173184, jsonContinuationsPerBoot=17664,
            finiteRankOneDebits=16, finiteRankOneEmissionAttempts=14, earlyReentryGuardsBeforeServices=True,
            retainedAutomaticRecyclerRuns=9, pythonTests=70, unchangedNamedDefinitions=573,
            inputFingerprints=[len(a['InputHashes']) for a in audits]),
        inputInventory=inputs, artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        frozenPredecessor=dict(commit='c3e2cc4', receipt='tools/reference/player-inferno-channel-native-evidence.json'),
        established=[
            'Complete ordinary XY/ID mode entry,initial/action/end/rewind scheduling,source st11/do19/resources and bit/list/stop helpers execute with both complete15-point arrays. Native bridge reuses SourceAnimationEvents and PlayerInfernoResources rather than NPC or Blade Fury channel policy.',
            'Fresh entry clears flag64. Existing-list start rewinds before posting its six-frame expiry,then successful nested do rewinds again. FIFO expiry/action ties,stale/duplicate removal callbacks,stop-before-start-before-events and active-sequence mode cancellation match4608source contexts over188928frames per boot.',
            'Explicit current-table cost controls match legacy frames where corrected rank1debit does not change later eligibility. The1536mana/rank1cohort is deliberately excluded from legacy frame equivalence; a separate current-cost finite control pays16times/emits14attempts,then stops and expires with no free channel. Neither test schedule defines modern input cadence.',
            'Two JSON continuations per context preserve future events and subsequent source comparison. Early reentry that could address negative sequence points is rejected before resource,allocation,aim or emission callbacks. This is component serialization,not a production active-channel world-save claim.'],
        remaining=[
            'Normal256sequence rate and accepted input intents are explicit services. Current D2R FCR/rate,client held/reentry/stop acceptance,full cross-owner game queue and modern payment frequency remain unverified. Very early double-rewind and fresh mode entry while a common Inferno lease already exists remain unsupported.',
            'No learned player channel,production persistence,input/cancellation/lifecycle integration or actual cold stream is enabled. Source current cold endpoints,mastery/pierce/chill and Size1/LastCollide/DamageRate41charged flight remain next. Final effects/audio and talent/left/right/quick icons stay separate.',
            'Main4c5d3a0and existing saves/client remain preserved. This is not full-class regression or installation; no class complete. Vine exposure timing,Solar formula/Command and earlier class/Warlock work remain in scope.'])
    path = ROOT / 'tools/reference/player-inferno-sequence-native-evidence.json'
    text = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Player Inferno/Arctic sequence:74checks/3boots/2retained-world reloads;188928source frames +173184current-cost controls per boot; public channel remains gated.')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--check', action='store_true')
    record(p.parse_args().check)
