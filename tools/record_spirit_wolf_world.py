#!/usr/bin/env python3
"""Bind bounded saved wolf movement acceptance; ordinary hit/casting remain pending."""
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/spirit-wolf'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    body = json.loads((ROOT / 'tools/reference/spirit-wolf-body-native-evidence.json').read_text())
    policies = json.loads((ROOT / 'tools/reference/pet-movement-native-evidence.json').read_text())
    for evidence in [body, policies]:
        for path, digest in evidence['hashes'].items():
            assert sha(ROOT / path) == digest, path
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log
    assert log.count('spirit-wolf-world-check: passed') == 1
    assert log.count('spirit-wolf-world-reload-check: passed') == 2
    assert log.count('spirit-wolf-world-live-check: passed') == 3
    assert log.count('spirit-wolf-world-live-result: passed') == 3
    assert log.count('Writing world save snapshot done') == 3
    assert log.count('Shutting down') == 3
    fixture = json.loads((STAGE / 'world-fixtures.json').read_text())
    controller = fixture['State']['Controller']
    assert controller['Profile'] == 'source-ai-native-ground-fit-pending-hit-v1'
    assert controller['Mode'] == 1 and controller['Motion']['Points'] == []
    assert controller['Fault'] is None and controller['StrikeRequests'] == 0
    assert fixture['Route']['Owner'] == fixture['Owner']
    assert len(fixture['Route']['Trail']) == 20
    assert fixture['Life'] == 100 * 256 + 17 + 2 * 32
    paths = [ROOT / name for name in [
        'src/Sanctuary.Content/DruidSpiritWolfWorld.cs', 'src/Sanctuary.Content/DruidWolfGeometry.cs',
        'src/Sanctuary.Content/PetOwnerRoutes.cs', 'src/Sanctuary.Content/DruidSpiritWolfBody.cs',
        'src/Sanctuary.Content/SourcePetMovement.cs', 'src/Sanctuary.Content/DruidSpiritWolfAi.cs',
        'src/Sanctuary.Content/SourceActorMotion.cs', 'src/Sanctuary.Content/SourceActorPath.cs',
        'src/Sanctuary.Content/ClassMovement.cs', 'src/Sanctuary.Content/Teleportation.cs',
        'src/Sanctuary.Content/SkeletonPets.cs', 'tools/build_class_skills.py',
        'data/Sanctuary/Balance/necromancer-summons.json', 'tests/check_spirit_wolf_body.py',
        'tests/Sanctuary.Integration/SpiritWolfWorldChecks.cs', 'tools/record_spirit_wolf_world.py',
        'tools/reference/spirit-wolf-body-native-evidence.json', 'tools/reference/pet-movement-native-evidence.json',
        'tools/reference/druid-beasts-preflight.json', 'tools/reference/spirit-wolf-corrected-ai-source-evidence.json']]
    paths += [STAGE / name for name in [
        'stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'world-fixtures.json',
        'python-tests.log', 'content-build.log', 'integration-build.log',
        'content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll']]
    evidence = dict(referenceVersion='3.3.93847', supplementaryAiCommit='7f6d773f31060e97124241e6bd30d606bcab6d4e',
        scope='Prepared world-controller factory and retained life/policy consumers, with declared native terrain/input services. Learned cast and ordinary hit delivery remain unavailable.',
        profile=controller['Profile'], installed=False, actorConnected=True, automaticWolfClock=True,
        ordinaryAttack=False, learnedHandler=False, fullClassRegression=False, currentExecutableParity=False,
        boots=3, actualReloads=2, connectedNaturalFollows=3, passedMarkers=len(re.findall(r': passed', log)),
        pythonTests=70, buildWarnings=0, buildErrors=0, handlers=166, druidHandlers=10,
        completedClasses=[], goalComplete=False,
        hashes={str(path.relative_to(ROOT)): sha(path) for path in paths},
        established=[
            'A saved world controller now calls CorrectedThink and SourcePetMovement through real actor services. Three authenticated loopback sessions exercise the natural40ms timer: initial25-frame think, source paths and position updates bring a distant wolf toward its owner. This tests server movement, not graphical client animation or a learned cast.',
            'Explicit world frames retain individual targets when another wolf is created, approach an occupied target footprint and request A1only in melee. A1action records a pending strike without silently applying invented damage. The shared hit-recovery route cancels its queued action, stops movement and preserves RNG/strike count.',
            'Delayed Teleport2request is distinct from common-pet placement: event+4moves to its captured legal destination; +3does not. Source S1/A1/WL/RN/GH metadata and animation event plans are exported separately from current-executable timing claims.',
            'Saved owner history has20points, strict25ms/displacement45gates, wrap, unconditional successful teleport append and50-frame last-teleport expiration. Snapshots do not alias caller arrays; malformed owner history/world state restoration is atomic.',
            'Offline and wardrobe states stop actions, RNG and healing while discarding elapsed frames. Two actual world reloads retain the parked actor owner/birth/pose/seed/clock/fractional injury and owner history; one subsequent active frame resumes placement and adds32FP recovery. Body-only health, buffs, facet and other retained consumers remain separately checked.',
            'Three boots/two reloads retain policy, numeric, Raven/path/Valkyrie, named gear, buffs, histories, wardrobe, Inner Sight and three actual Decoy casts/expiries/disconnect checks. Fresh audits retain573named definitions and all70Python checks pass. The installed main build and original saves/configuration/client are unchanged.'
        ], remaining=[
            'Ordinary hit accuracy/block/critical/cold/chill and supported equipment delivery are next. The user expects100percent cold damage; the tiny physical floor in the older ordinary-hit source remains unverified in3.3.93847. No physical damage branch is inferred from that uncertainty. Nonzero cold sunder remains explicitly unsupported by this prepared factory.',
            'Learned do119placement/payment/FIFO-cap/coexistence, right/quick input, full-class regression, matching graphical client acceptance and installation remain pending. No additional handler or class completion is claimed.',
            'Ground fit/LOS, accepted native tile steps and one-step owner route, facet-wide room projection, nearest mapped-NPC scan, size/grid conversions and scoped per-actor RNG are explicit adapters. Complete source skeleton placement, AStar/type1and straight/type7close-obstacle fallback, moving-unit path repair and exact modern crowd/room/geometry/global ordering are unfinished. Unsupported AStar requests fault the prepared controller explicitly.',
            'The corrected source resolves the old pointer-versus-combat defect but does not certify current D2R. Negated potential-target visibility remains uncertain. S1initial think/end composition, Teleport2mode-change versus animation callbacks, exact recovery/repath chronology and current-executable event equivalence still need reference work.',
            'The named silent0xE1wolf is a placeholder. Final body/emergence/movement/bite/cold/teleport/hurt/death/audio and separate talent/right/quick icons remain in their independent asset inventories.'
        ])
    path = ROOT / 'tools/reference/spirit-wolf-world-native-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Prepared world wolf:3boots/2reloads/3natural follows, saved individual movement/history/teleport and recovery cancellation; ordinary hit and learned casting pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
