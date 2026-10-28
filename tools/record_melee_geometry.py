#!/usr/bin/env python3
"""Bind shared source melee geometry and its captured-contact consumer."""
import argparse
import hashlib
import json
import re
import subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/melee-geometry'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    _, prior = frozen_receipt('6e29a7a', 'tools/reference/monster-contact-native-evidence.json')
    assert prior['verified']['passMarkers'] == 199 and prior['verified']['fullClassMarkers'] is None
    artifacts = []
    for kind in ['content', 'integration']:
        p = STAGE / (kind + '-build.log')
        assert all(s in p.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [p, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 200
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    pattern = re.compile(r'\] ([A-Za-z0-9-]+): passed')
    before = Counter(pattern.findall((ROOT / '.local/class-skills/monster-contact/stats-native-checks.log').read_text()))
    after = Counter(pattern.findall(log))
    assert sum(before.values()) == 199 and not before - after and after - before == Counter({'melee-geometry-check': 1})
    assert log.count('Melee geometry:32400distance/endpoints,91464range and14400retained wolf distances;three current NPC profiles,native contact HP/free/retained-copy bridge pass;actual world ray/metadata remain explicit') == 1
    source_path = ROOT / 'tools/reference/melee-geometry-source-evidence.json'
    source = json.loads(source_path.read_text()); artifacts.append(source_path)
    assert not source['currentExecutableParity'] and len(source['compiledUnchangedRoutineHashes']) == 5
    assert source['verified']['geometryContexts'] == 32400 and source['verified']['rangeContexts'] == 91464
    assert source['verified']['flyingCollisionMask'] == 0x804 and source['verified']['tentacleBaseIds'] == [258, 261]
    profiles = source['verified']['currentNpcProfiles']
    assert profiles == [dict(Key='warbighead', BaseId=743, Size=3, RawRange=0), dict(Key='wargoatman', BaseId=742, Size=2, RawRange=2), dict(Key='warputriddefiler', BaseId=744, Size=2, RawRange=0)]
    assert json.loads((ROOT / 'data/Sanctuary/Balance/npc-melee-geometry.json').read_text()) == profiles
    for group in ['inputs', 'artifacts']:
        for name, digest in source[group].items():
            assert sha(ROOT / name) == digest, name
            artifacts.append(ROOT / name)
    assert not (ROOT / '.local/class-skills/melee-geometry-source/sanitizer.log').read_bytes()
    saved = json.loads((STAGE / 'focused-fixtures/monster-death-delay-fixtures.json').read_text())
    assert saved['Next'] == 2 and [len(b) for b in saved['Batches']] == [4, 4]
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        p = STAGE / name; audit = json.loads(p.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name2, digest in audit['InputHashes'].items():
            assert sha(ROOT / name2) == digest, name2
        audits.append(audit); artifacts += [p, ROOT / 'tools/reference' / name]
    old = json.loads(subprocess.check_output(['git', 'show', '6e29a7a:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == old['Definitions']
    assert [len(a['InputHashes']) for a in audits] == [721, 21]
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    main = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(main['installedArtifacts']) == 106
    for name, digest in main['installedArtifacts'].items():
        assert sha(Path(main['runtimeRoot']) / name) == digest, name
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert sum(s['implementation'] == 'handler-present' for s in status['skills']) == 183 and status['completedClasses'] == []
    artifacts += [STAGE / n for n in ['stats-inputs.json', 'stats-native-checks.log', 'run.log', 'python-tests.log']]
    artifacts += list((STAGE / 'focused-fixtures').glob('*fixtures.json'))
    artifacts += [ROOT / n for n in ['tools/record_melee_geometry.py', 'tests/check_melee_geometry.py', 'tools/build_skill_roadmap.py', 'data/Sanctuary/Balance/class-skill-status.json', 'data/Sanctuary/Balance/skill-effects.json', 'data/Sanctuary/Balance/skill-icons.json', 'tools/reference/creeper-frame-installation-evidence.json']]
    result = dict(referenceVersion='3.3.93847', component='Shared source NPC melee geometry and contact range consumer', installed=False, handlerEnabled=False, currentExecutableParity=False, fullActorAccepted=False, goalComplete=False, handlers=183, warlockHandlers=1, completedClasses=[],
        verified=dict(passMarkers=200, retainedMarkers=199, worldBoots=3, actualWorldReloads=2, sourceGeometryContexts=32400, sourceRangeContexts=91464, retainedWolfDistanceComparisons=14400, currentNpcProfiles=profiles, sourceRoutineCount=5, collisionMask=0x804, tentacleBaseIds=[258, 261], nativeContactHealthGate=True, nativeFixturePacketFreeAndNoReplay=True, missAndZeroDistanceShortCircuit=True, sharedWolfGeometry=True, fullClassMarkers=None, pythonTests=70, unchangedNamedDefinitions=573, inputFingerprints=[721, 21], unchangedMainInstallationArtifacts=106),
        inputInventory=inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))}, frozenPredecessor=dict(commit='6e29a7a', receipt='tools/reference/monster-contact-native-evidence.json'),
        established=[
            'Shared native distance/collision endpoints match32400complete-source contexts and exact ray requests. Native range matches91464composed source cases,including NPC missing rows,255weapon-class reach,strict tentacle reach,zero-distance bypass,roomless collision behavior,null units and source mask0x804. The ray body remains a declared service;only endpoint requests and supplied ray answers are certified.',
            'The existing wolf melee-distance wrapper delegates to the shared near-distance table/far-distance implementation. All14400previously supported size0..3controls match;its old size contract remains explicit. The new helper separately supports source sizes0..255. UnitDistance and full-size AI metrics remain distinct. Current owned tables pin Goatman size2/reach2,Tainted size3/reach0 and Defiler size2/reach0,with their resolved base IDs.',
            'GeometricMonsterContactServices composes source range with captured contact. Actual encounter bodies use their native positions on the existing two-subtile projection. Overlapping source footprints bypass the supplied blocking ray and commit17FP damage;the blocked in-range control requests the exact ray/mask and frees without HP loss;the far control refuses without a ray or HP loss. The fixture free service clears its captured slot,and an immediate second attempt cannot replay it. Other contact metadata and the ray answer are explicitly supplied.',
            'All199predecessor markers,including native health/Stun/death and both real saved deferred-death batches,remain exact. Both builds,70Python tests,573named-item decisions and106installed artifact hashes pass. Earlier full-class acceptance remains separately scoped to16ce299;no new full suite or installation is claimed. Source diagnostics retain an initial preflight with swapped door/barrier labels;the combined0x804mask was unchanged and the primary header corrected the labels before compilation.'
        ], remaining=[
            'The actual world ray,collision grid/door/barrier/native tile-height projection,facet/room lookup and complete source base-ID mapping remain unfinished. Native fixture ray answers are controlled services,not real wall collision acceptance. The existing two-subtile world projection remains provisional for final actual-client sign-off. Missing attacker room can make the source collision helper return false;outer world validation must still own invalid actor/room handling.',
            'Real contact hostility/killability/dead modes and packet-store lifecycle/persistence,actual mode/animation/FHR/block/knockback/network/AI scheduling,think rearming and deletion cleanup remain unfinished. Modern helper/dispatcher linkage,extended12-slot passives/event16/CrushingBlow/BloodOath/sunder,new Frenzy/Cleave and actual proc/cold/damage-credit/drain/absorb/regeneration still gate learned Goatman casting/body/AI. Player weapon reach is not supplied by this NPC adapter;current executable parity is not certified.',
            'Final NPC get-hit/Stun/Berserk/body effects/audio and separate talent/hotkey icons remain pending in independent registers. Existing presentation is an identified placeholder;this internal geometry policy creates no player hotkey identity. No class is complete.'
        ])
    path = ROOT / 'tools/reference/melee-geometry-native-evidence.json'; encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Melee geometry:200focused/3boots/2reloads;32400geometry/91464range/14400wolf controls;70Python;world ray remains explicit.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
