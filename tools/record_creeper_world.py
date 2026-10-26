#!/usr/bin/env python3
"""Bind Creeper world movement/clock and explicit vine-launch capture acceptance."""
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/creeper'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    component = json.loads((ROOT / 'tools/reference/creeper-stats-native-evidence.json').read_text())
    for name, digest in component['hashes'].items():
        assert sha(ROOT / name) == digest, name
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log and 'send buffer exhausted' not in log
    assert log.count('creeper-world-check: passed') == 1
    assert log.count('creeper-world-reload-check: passed') == 2
    assert log.count('creeper-world-live-check: passed') == 3
    assert log.count('creeper-world-live-result: passed') == 3
    assert log.count('spirit-wolf-world-check: passed') == 1
    assert log.count('spirit-wolf-world-reload-check: passed') == 2
    assert log.count('Shutting down') == 3
    fixture = json.loads((STAGE / 'world-fixtures.json').read_text())
    state = fixture['State']['Controller']
    assert state['Profile'] == 'source-ai-native-ground-vine-launch-capture-v1'
    assert state['Mode'] == 1 and state['Fault'] is None and state['SkillEvents'] == 1
    assert state['GameBase'] == 1000 and state['Ai']['LastSkillFrame'] == 1025
    assert len(state['LastHeads']) == 3 and fixture['Life'] == 6007 + 2 * 9
    wolf = json.loads((STAGE / 'wolf-world-fixtures.json').read_text())
    assert wolf['State']['Controller']['Fault'] is None
    assert wolf['State']['Controller']['AttackSpeed'] == 88 and wolf['State']['Controller']['DeferredThinks'] > 0
    assert wolf['Life'] == 100 * 256 + 17 + 2 * 32
    # Reviewed complete source routines ground the melee threshold and escape
    # destination. Their services are still native projections in this record.
    tree = json.loads((ROOT / '.local/class-skills/d2moo-source-tree.json').read_text())
    source_hashes, routines = {}, {}
    for relative, names in [
        ('source/D2Game/src/AI/AiTactics.cpp', ['D2GAME_AICORE_Escape_6FCD0560']),
        ('source/D2Common/src/Units/Units.cpp', ['UNITS_GetMeleeRange', 'UNITS_IsInMeleeRange'])]:
        raw = (ROOT / '.local/class-skills/d2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        source_hashes[relative] = hashlib.sha256(raw).hexdigest()
        text = raw.decode()
        for name in names:
            found = re.search(r'^.*\b' + name + r'\([^;\n]+\)\s*\n\{', text, re.M)
            assert found, name
            end, depth = text.index('{', found.start()) + 1, 1
            while depth:
                depth += (text[end] == '{') - (text[end] == '}')
                end += 1
            routines[name] = hashlib.sha256(text[found.start():end].encode()).hexdigest()
    paths = [ROOT / name for name in [
        'src/Sanctuary.Content/DruidCreeperWorld.cs', 'src/Sanctuary.Content/DruidCreeperBody.cs',
        'src/Sanctuary.Content/DruidCreeperAi.cs', 'src/Sanctuary.Content/DruidCreeperVines.cs',
        'src/Sanctuary.Content/PetGroundWorld.cs', 'src/Sanctuary.Content/DruidSpiritWolfWorld.cs',
        'src/Sanctuary.Content/SourcePetMovement.cs', 'src/Sanctuary.Content/PetOwnerRoutes.cs',
        'src/Sanctuary.Content/SourceActorPath.cs', 'src/Sanctuary.Content/SourceActorMotion.cs',
        'src/Sanctuary.Content/SourceAnimationEvents.cs', 'tools/build_class_skills.py',
        'tests/Sanctuary.Integration/CreeperWorldChecks.cs', 'tests/Sanctuary.Integration/SpiritWolfWorldChecks.cs',
        'tests/check_creeper_stats.py', 'tools/record_creeper_world.py',
        'tools/reference/creeper-stats-native-evidence.json', 'tools/reference/creeper-ai-source-evidence.json',
        'tools/reference/creeper-preflight.json', 'data/Sanctuary/Balance/necromancer-summons.json']]
    paths += [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log',
        'world-fixtures.json', 'wolf-world-fixtures.json', 'python-tests.log', 'content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll']]
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], installed=False, learnedHandler=False,
        profile=state['Profile'], automaticWorldClock=True, actorConnected=True, liveVineDamage=False,
        headRequestsCapturedOnly=True, boots=3, actualReloads=2, connectedNaturalFollows=3,
        passedMarkers=len(re.findall(': passed', log)), pythonTests=70, handlers=167, druidHandlers=11,
        fullClassRegression=False, currentExecutableParity=False, completedClasses=[], goalComplete=False,
        sourceHashes=source_hashes, reviewedRoutineHashes=routines,
        hashes={str(p.relative_to(ROOT)): sha(p) for p in paths},
        established=[
            'Saved automatic40ms actor controller consumes the already-compared Vines outer AI and shared common/six-mode pet movement. Three temporary-account loopback sessions pass the logged-in transport gate and move the actual Creeper toward its owner under natural timers.',
            'Explicit world tests retain first think25, shared follow/warp service routing, scalar source escape away from an exact100poison-resist target, lazy mapped-NPC target scan and source melee-range2plus1threshold. Source reach/escape bodies are reviewed/hash-pinned; full room/target helpers are not represented as complete current-executable services.',
            'A mature supplied game base1000allows the initial melee skill attempt at actor frame25/remembered global1025. A supplied fresh base0retains strict early-game delay and attempts at a later AI decision. The default clock uses server-initialization elapsed age; actor base/remembered frame persist while inactive intervals discard actions/healing.',
            'Current K9S1kind2event is planned through the existing source animation component. The action captures three source head requests at the target coordinates, without poison, actual fields or invented damage. Heads are copied across API boundaries. The missing-target branch is implemented but has no separate native transition check yet. Last head requests and event count survive two actual world reloads without replay.',
            'Shared native terrain helper preserves the existing Wolf ground projection. Wolf world/cold-hit/slow-state controls and two actual reloads pass alongside Creeper. Retained Creeper life/buff/history/facet and numeric/policy controls remain checked. Main installed runtime/saves/config/client are unchanged.'],
        remaining=[
            'This is an internal head-request capture profile, not a functioning learned Poison Creeper attack. Actual head/charged initialization/trail factories, current formula-driven lifetime, poison owner/Rabies/mastery/pierce transport, delivery and LastCollide/world collision must be connected next.',
            'VINE_BEAST surface/underground visibility and general melee/projectile targeting remain unverified. isAtt0offensive aura exclusion remains intact, while the named silent0x8actor is visibly vulnerable preparation only.',
            'Server-start age and saved per-actor source-frame offset are explicit clock adapters. Exact modern NPC skill-rate/mode-end/AI scheduling, game epoch, shared actor ordering, source scan alternatives/list/ties/LOS, special target reach and global RNG equivalence are not certified.',
            'Zero-footprint native terrain ignores mobile occupancy but uses ordinary native16height/surface checks. Facet-wide room projection, source-grid conversion, owner one-step movement and nearest mapped target are adapters. AStar/type1, complete moving-target/obstacle path repair and full source collision geometry remain unsupported/unverified.',
            'Nonzero inherited poison sunder and innate-skill Battle Command transport remain explicitly unsupported. Learned placement/payment/shared-vine cap/right/quick input, full-class regression, graphical client and installation are pending. Separate effects/audio and talent/hotkey icons remain placeholders.'])
    target = ROOT / 'tools/reference/creeper-world-native-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Creeper world:3boots/2reloads/3connected natural follows, source kind2head requests captured; live vines/poison/learned casting remain pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
