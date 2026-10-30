#!/usr/bin/env python3
"""Bind native world-controller transfer and real reload acceptance."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_snapshot
ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/revive-world-transfer'
PREDECESSOR = '49a5539'
SNAPSHOT = STAGE / 'pre-build-attempt-4.json'
sha = inventory.sha

def markers(log):
    return Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed', log))

def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    check_snapshot(SNAPSHOT, STAGE / 'stats-inputs.json')
    _, prior = frozen_receipt(PREDECESSOR, 'tools/reference/revive-native-melee-evidence.json')
    assert prior['verified']['nativePatchCount'] == 70 and prior['verified']['fullClassMarkers'] == 1184
    reference = json.loads((STAGE / 'native-reference.json').read_text())
    assert reference['frozenCommit'] == PREDECESSOR
    artifacts = [Path(__file__).resolve(), SNAPSHOT, STAGE / 'native-reference.json']
    for name, value in reference['artifacts'].items():
        path = Path(reference['nativeDistribution']) / name
        assert sha(path) == value == inputs['artifacts'][name] == prior['inputInventory']['artifacts'][name]
        artifacts.append(path)
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build-attempt-4.log')
        assert all(s in path.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and sum(markers(log).values()) == 335
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    before = markers((ROOT / '.local/class-skills/revive-native-melee/stats-native-checks.log').read_text())
    assert sum(before.values()) == 332 and not before - markers(log)
    assert markers(log) - before == Counter({'revive-world-transfer-check':1, 'revive-world-transfer-reload-check':2})
    assert log.count('Revive world transfer:actual native spawns/deaths free dictionary and entry rosters,respawn/delete preserve original and loot,ownership clears home/waypoint;injected post-move failure restores controllers/lease/loot and retry works;active/pending originals and replacements survive actual reloads') == 3
    saved = json.loads((STAGE / 'revive-world-transfer-fixtures.json').read_text())
    assert saved['Active'] != saved['Pending'] and saved['ActiveCorpse'] != saved['PendingCorpse']
    assert all(len(saved[k]) == len(set(saved[k])) == 2 for k in ['Spawners','Replacements','Waypoints'])
    assert len(saved['ActiveHome']) == 3 and saved['ActiveHome'] != [0,0,0]
    audits = []
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for source, value in audit['InputHashes'].items():
            assert sha(ROOT / source) == value, source
        artifacts += [path, ROOT / 'tools/reference' / name]
        audits.append(audit)
    old = json.loads(subprocess.check_output(['git','show',PREDECESSOR+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert audits[0]['Definitions'] == old['Definitions'] and len(audits[0]['Definitions']) == 573
    fingerprints = [len(a['InputHashes']) for a in audits]
    assert fingerprints == [780,21]
    tests = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
    main_path = ROOT / 'tools/reference/creeper-frame-installation-evidence.json'
    main = json.loads(main_path.read_text())
    assert len(main['installedArtifacts']) == 106
    for name,value in main['installedArtifacts'].items():
        assert sha(Path(main['runtimeRoot']) / name) == value, name
    for path in (ROOT / 'data/Sanctuary').rglob('*'):
        if path.is_file():
            assert path.read_bytes() == subprocess.check_output(['git','show',PREDECESSOR+':'+str(path.relative_to(ROOT))],cwd=ROOT),path
    status = json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert sum(s['implementation']=='handler-present' for s in status['skills']) == 184 and status['completedClasses'] == []
    artifacts += [main_path, ROOT/'tests/check_revive_world_transfer.py', ROOT/'tools/build_skill_roadmap.py',
        ROOT/'docs/class-skill-roadmap.md', ROOT/'docs/class-skills-resume-checkpoint.md', ROOT/'docs/revive-native-behavior-review.md']
    artifacts += [STAGE/name for name in ['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','revive-world-transfer-fixtures.json']]
    artifacts += [p for p in STAGE.glob('attempt-*/**/*') if p.is_file()]
    artifacts += list(STAGE.glob('pre-build-*.json'))
    result = dict(referenceVersion='3.3.93847',component='Retained Revive original world spawner/home/waypoint transfer',
        installed=False,handlerEnabled=False,automaticRetentionEnabled=False,goalComplete=False,handlers=184,completedClasses=[],processMemoryRead=False,
        verified=dict(passMarkers=335,retainedMarkers=332,newWorldTransferMarkers=3,worldBoots=3,actualReloads=2,saveSnapshots=3,
            nativeSpawnerDeaths=3,nativeRespawnReplacementCases=3,formerSpawnerDeletionCase=1,injectedPostMoveRollbackRetryCases=3,
            unsupportedCustomDefragRefusals=3,savedActiveOriginals=1,savedPendingOriginals=1,savedFormerSpawners=2,savedReplacements=2,
            nativePatchCount=70,nativeBinariesReused=True,freshFullClassRun=False,frozenPredecessorFullClassMarkers=1184,
            pythonTests=71,inputFingerprints=fingerprints,unchangedNamedDefinitions=573,unchangedMainInstallationArtifacts=106),
        frozenPredecessor=dict(commit=PREDECESSOR,receipt='tools/reference/revive-native-melee-evidence.json'),
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},
        established=[
            'Accepted death retention now unlinks supported native BaseSpawner controllers through their ordinary Remove contract before parking the actual dead actor. Both global and entry rosters release it; three native respawns produce distinct replacements, and deletion of a former controller cannot delete its retained original or loot. This is standard world lifecycle cleanup, not a replacement NPC.',
            'Native ownership binding may clear the former Home/WayPoint. Class parking creates its own new Stay anchor; tests distinguish that from the old spawn home rather than demanding zero while parked. One active and one inactive original, two former spawners/replacements/waypoints and separate corpse loot survive both actual reloads.',
            'An injected post-move exception on each boot exercises real rollback: original ownership/resources/storage and old Home/HomeMap/WayPoint are restored, the corpse remains unconsumed with loot, and retry activates the same actor successfully. This is additional to preflight refusal checks.',
            'Controllers overriding native defrag or implementing unknown unlink behavior refuse explicitly without mutating the controller link. Guild and lingering-spawner activation contexts remain gates. The optional fixture types have explicit serialization constructors/methods because the native type table retains even deleted type names.',
            'Clean content/integration builds reuse precisely the accepted70-patch native products. All332prior focused markers remain within335checks/three boots/two reloads;71Python checks and fresh780/21audits pass. No fresh full-class run is claimed for this still-gated Revive-only change. The predecessor1184full-class scope remains separately frozen;573named decisions and106installed artifacts are unchanged.'],
        remaining=[
            'Unknown/custom defrag controllers and guild ownership semantics need individual adapters. The runtime learned Revive route and automatic retention remain gated; supported world transfer alone does not compose real start/release/status/mode/placement/payment.',
            'Source cadence/selection/critical/special attacks, original item/stat/proc effects, Summon Resist/events, regeneration, resource policy, facet replanning and respec remain unfinished. Preserve the approved behavior policy and no-expiration lifetime. Final effects/audio and talent/hotkey icons stay separate placeholders. No class or full goal completion is claimed.'])
    output=ROOT/'tools/reference/revive-world-transfer-evidence.json'
    encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Revive world transfer:335checks/3boots/2reloads/71Python;real spawner death/respawn/deletion,original identity/loot,home/waypoint rollback and retry verified;learned Revive remains gated.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
