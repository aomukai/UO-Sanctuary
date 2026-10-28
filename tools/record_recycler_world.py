#!/usr/bin/env python3
"""Bind prepared recycler automatic world acceptance and retained components."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/recycler-world'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    paths = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'run.log', 'python-tests.log',
        'recycler-world-fixtures.json', 'recycler-body-fixtures.json', 'recycler-recovery-fixtures.json',
        'body-fixtures.json', 'world-fixtures.json', 'ground-fixtures.json', 'poison-fixtures.json', 'casting-fixtures.json', 'wolf-world-fixtures.json']]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        assert all(s in path.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        paths += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 61
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    for name, count in [('recycler-model-check', 1), ('recycler-body-check', 1), ('recycler-recovery-check', 1),
                        ('recycler-world-check', 1), ('recycler-body-reload-check', 2), ('recycler-recovery-reload-check', 2),
                        ('recycler-world-reload-check', 2), ('recycler-recovery-live-check', 3), ('recycler-recovery-live-result', 3),
                        ('recycler-world-live-check', 3), ('recycler-world-live-result', 3)]:
        assert log.count(name + ': passed') == count, name
    assert '800complete-source walk flag comparisons' in log
    assert 'natural seek/move/feed/credit' in (STAGE / 'run.log').read_text()
    saved = json.loads((STAGE / 'recycler-world-fixtures.json').read_text())
    state = saved['State']
    assert state['Owner'] == saved['Owner'] and state['Snapshot']['Birth']['Life256'] == 24320
    assert state['Controller']['Profile'] == 'prepared-ai111-mode-entry-recovery-v1'
    assert state['Controller']['Fault'] is None and state['Controller']['Mode'] == 1
    assert state['Controller']['SkillAttempts'] == 0 and saved['Life'] == 12007 + 2 * 17
    review = ROOT / 'tools/reference/recycler-world-source-review.json'
    assert json.loads(review.read_text())['completeRoutines'] == 10
    paths.append(review)
    proof = ROOT / 'tools/reference/pet-walk-flags-source-evidence.json'
    data = json.loads(proof.read_text())
    assert data['contexts'] == 12800 and data['sanitizerClean'] and data['completeRoutine'] == 'AITACTICS_MoveToTarget'
    paths.append(proof)
    for name, digest in data['artifacts'].items():
        assert sha(ROOT / name) == digest, name
        paths.append(ROOT / name)
    frozen_receipt('b42d729', 'tools/reference/recycler-vines-native-evidence.json')
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
    prior = json.loads(subprocess.check_output(['git', 'show', 'b42d729:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == prior['Definitions']
    py = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
    paths += [ROOT / name for name in ['tests/check_recycler_world.py', 'tools/record_recycler_world.py',
        'tools/review_recycler_world_reference.py', 'data/Sanctuary/Balance/class-skill-status.json',
        'data/Sanctuary/Balance/skill-effects.json', 'data/Sanctuary/Balance/skill-icons.json']]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [s for s in status['skills'] if s['implementation'] == 'handler-present']
    assert len(handlers) == 182 and sum(s['characterClass'] == 'Druid' for s in handlers) == 26 and status['completedClasses'] == []
    result = dict(referenceVersion='3.3.93847', component='Prepared Carrion/Solar automatic corpse seeking, movement and feeding',
        installed=False, learnedHandlerEnabled=False, currentExecutableParity=False, goalComplete=False,
        handlers=182, druidHandlers=26, completedClasses=[],
        verified=dict(passMarkers=61, recyclerMarkers=22, boots=3, actualReloads=2, automaticWorldRuns=3, retainedAutomaticCredits=3,
            sourceWalkTacticContexts=12800, nativeOrderedWalkFlagComparisons=800, retainedBirthComparisons=9180,
            retainedAiDecisions=4705, retainedResourceCallbacks=1140, pythonTests=70, unchangedNamedDefinitions=573,
            inputFingerprints=[len(a['InputHashes']) for a in audits]),
        inputInventory=inputs, artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        frozenPredecessor=dict(commit='b42d729', receipt='tools/reference/recycler-vines-native-evidence.json'),
        established=[
            'Automatic world controller uses checked common pet movement/path/motion, source AI111 and owner trails. Corpse scan preserves native item-list order with mapped corpse sizes and existing loot/LOS/access guards. Full owner resources suppress feeding while corpse approach remains active.',
            'Source-mode entry reserves and allocates recovery immediately; animation end does not repeat start63. Failed allocation retains corpse reservation and attempt delay. Independent player-owned credit continues after actor removal. Mature/early strict-delay, offline skip/resume, atomic invalid state and actual parked-controller reloads pass.',
            'Walk request7 is failure flags rather than path type. Shared ordered fallback deletes think, marks vision reset and chooses close-walk4 or idle10. Native callback policy matches800complete-source cases; complete old walk tactic has12800ASan/UBSan-clean service contexts. Three connected timer runs seek, approach, reserve and heal without manual stepping.'],
        remaining=[
            'Learned and prepared casting/input are not connected. Underground VINE_BEAST exposure, innate Battle Command transport and modern Solar formula-slot behavior remain unresolved; Solar recovery profile is always explicit.',
            'Source AI-control0x40vision reset is projected to a fresh native hostility scan. Current exact vision/room/geometry/item order/global event timing and special tentacle reach remain unverified. Historical corpses lacking mapped owner/size metadata are skipped, and source AStar fallback is explicitly refused with a saved fault.',
            'No full-class regression or installation is claimed. Main stays on4c5d3a0with existing saves/client preserved. Final K9body/feed/recycler/recovery/audio and separate talent/right/quick icons remain identified placeholders.'])
    path = ROOT / 'tools/reference/recycler-world-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Recycler world:61checks/3boots/2reloads,3natural seek/move/feed/credit runs; learned routes disabled, main preserved.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
