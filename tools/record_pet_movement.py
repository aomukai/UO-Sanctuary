#!/usr/bin/env python3
"""Record native common-pet policy comparisons; live wolf AI is separate."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'spirit-wolf'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log
    assert log.count('pet-movement-check: passed') == 1
    assert '8312complete direct and39936complete common contexts, 367734ordered service requests match' in log
    assert 'Spirit Wolf legacy policy:1802complete source contexts/ordered requests match' in log
    assert 'Spirit Wolf corrected policy:6366complete source contexts/ordered requests match' in log
    assert 'suspect nonnull-monstats attack/chase branch is not approved live AI' in log
    body = json.loads((ROOT / 'tools/reference/spirit-wolf-body-native-evidence.json').read_text())
    assert body['boots'] == 3 and body['actualReloads'] == 2 and body['pythonTests'] == 70
    for path, digest in body['hashes'].items():
        assert sha(ROOT / path) == digest, path
    inventory = json.loads((STAGE / 'stats-inputs.json').read_text())
    for path, digest in inventory['sourceInputs'].items():
        assert sha(ROOT / path) == digest, path
    sources = []
    for name in ['pet-movement', 'spirit-wolf-ai']:
        source = json.loads((ROOT / 'tools/reference' / (name + '-source-evidence.json')).read_text())
        for path, digest in source['sourceHashes'].items():
            assert sha(BASE / 'd2moo-pinned-source' / path) == digest, path
        if name == 'pet-movement':
            fixture = source['fixture']
            assert sha(ROOT / fixture['path']) == fixture['sha256']
            assert fixture['direct'] == 8312 and fixture['common'] == 39936
            assert sha(BASE / 'pet-movement-source-probe.cpp') == source['generatedSourceSha256']
            assert (BASE / 'pet-movement-sanitizer.log').read_text() == ''
        else:
            assert source['fixture']['cases'] == 1802
            assert sha(ROOT / source['fixture']['path']) == source['fixture']['sha256']
            assert sha(BASE / 'spirit-wolf-ai-source-probe.cpp') == source['generatedSourceSha256']
        sources.append(source)
    corrected = json.loads((ROOT / 'tools/reference/spirit-wolf-corrected-ai-source-evidence.json').read_text())
    assert corrected['supplementaryCommit'] == '7f6d773f31060e97124241e6bd30d606bcab6d4e'
    assert corrected['fixture']['cases'] == 6366
    assert sha(ROOT / corrected['sourcePath']) == corrected['sourceSha256']
    assert sha(ROOT / corrected['fixture']['path']) == corrected['fixture']['sha256']
    assert sha(BASE / 'spirit-wolf-corrected-ai-source-probe.cpp') == corrected['generatedSourceSha256']
    assert sha(BASE / 'spirit-wolf-ai-source-probe.cpp') == corrected['reusedServiceSourceSha256']
    assert (BASE / 'spirit-wolf-corrected-ai-sanitizer.log').read_text() == ''
    paths = [ROOT / name for name in ['src/Sanctuary.Content/SourcePetMovement.cs', 'src/Sanctuary.Content/DruidSpiritWolfAi.cs',
        'tests/Sanctuary.Integration/PetMovementChecks.cs', 'tests/check_spirit_wolf_body.py', 'tools/probe_pet_movement_reference.py',
        'tools/probe_spirit_wolf_ai_reference.py', 'tools/probe_spirit_wolf_corrected_ai_reference.py', 'tools/record_pet_movement.py', 'tools/reference/pet-movement-source-evidence.json',
        'tools/reference/spirit-wolf-ai-source-evidence.json', 'tools/reference/spirit-wolf-corrected-ai-source-evidence.json', 'tools/reference/spirit-wolf-body-native-evidence.json']]
    paths += [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log',
        'content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll']]
    evidence = dict(referenceVersion='3.3.93847', installed=False, preparedPolicies=True,
        scope='Pure policy comparisons with declared services; world-controller composition is recorded separately in spirit-wolf-world-native-evidence.json.',
        learnedHandler=True, fullClassRegression=False,
        completeDirectMovementCases=8312, completeCommonMovementCases=39936, orderedMovementServiceRequests=367734,
        completeLegacySpiritWolfCases=1802, completeCorrectedSpiritWolfCases=6366, sourceAndNativeCases=56416,
        boots=3, actualReloads=2, pythonTests=70, buildWarnings=0, buildErrors=0,
        retainedHandlers=167, druidHandlers=11, completedClasses=[], goalComplete=False,
        hashes={str(p.relative_to(ROOT)): sha(p) for p in paths},
        established=[
            'Native shared policy matches complete unchanged PetMove modes0..5 and complete common-pet composition in48248source contexts, including367734ordered action/query/random/placement service requests, return values, movement-attempt counts and teleported flag writes.',
            'Full seven-offset and midpoint/fallback follow paths, reverse20entry owner trail, skipped zero/near entries, first-success ordinals, random40..79velocity, one AI-parameter fallback, crowd/escape/owner-walk branches, strict distance boundaries, owner-path AND test, total-pet leash cap36 and placement/room/commit failures are retained. The newer source names player words40/41as the last-teleport point; native context uses that explicit name.',
            'Native explicitly named LegacyThink matches1802full SpiritWolf request contexts: independent primary target precedence, literal negated potential visibility, preserved combat input, duplicate failed follow requests, mode-change delays4/6, idle10/8 and approach/stall actions. Common/PetMove outputs are supplied services in that separate outer-AI fixture; it does not execute the shared movement code through a live actor.',
            'Separately pinned corrected source7f6d773/blob89e229d changes only the attack/chase condition after four symbol renames in the complete SpiritWolf routine. Native CorrectedThink matches6366complete contexts, retaining A1only in combat, owner-target distance20and owner distance10idle/walk-radius boundaries, run-to-target and velocity ratios including zero base velocity. The obsolete pointer branch stays in LegacyThink for comparison.',
            'The retained actual saved wolf life/ownership slice passes3boots/2reloads along with prior numeric and shared health/defense/wardrobe/Decoy consumers. All70Python tests and fresh native audits pass; no additional handler or installation is claimed.'
        ], remaining=[
            'The old pointer-versus-combat reconstruction defect has a separately pinned source correction; do not wire LegacyThink into a live controller. The corrected older source still does not certify current D2R executable behavior. Its opposite potential-target LOS polarity remains an independent uncertainty.',
            'Actual saved owner history/teleport markers, movement/individual targets, natural clock and bounded cold melee now have separate acceptance in spirit-wolf-world-native-evidence.json. Complete path repair and modern event/global-RNG/geometry/physical-floor equivalence remain pending. The newer source crowd callback uses the current pet rather than the old null prior-target pointer.',
            'The skeleton-template placement request in shared mode3 is retained as a declared source service; it is separate from Spirit Wolf Teleport2/do98. No generic teleport-to-owner shortcut or whole-flock retargeting is inferred.',
            'Learned do119payment/FIFO/coexistence/input has separate acceptance in spirit-wolf-casting-native-evidence.json. Exact placement, full regression/installation and final effects/audio/talent/hotkey icons remain pending. Current installed build, saves and client stay preserved.'
        ])
    path = ROOT / 'tools/reference/pet-movement-native-evidence.json'; encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Pet policies:48248shared movement/367734ordered requests,1802legacy and6366corrected Spirit contexts; world-controller composition recorded separately.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
