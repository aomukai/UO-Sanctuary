#!/usr/bin/env python3
"""Bind Creeper world clock and explicit moving-vine/contact capture acceptance."""
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
    assert log.count('creeper-fields-check: passed') == 1
    assert log.count('creeper-ground-check: passed') == 1
    assert log.count('creeper-ground-reload-check: passed') == 2
    assert log.count('creeper-fields-live-result: passed') == 3
    assert log.count('creeper-world-reload-check: passed') == 2
    assert log.count('creeper-world-live-check: passed') == 3
    assert log.count('creeper-world-live-result: passed') == 3
    assert log.count('spirit-wolf-world-check: passed') == 1
    assert log.count('spirit-wolf-world-reload-check: passed') == 2
    assert log.count('Shutting down') == 3
    fixture = json.loads((STAGE / 'world-fixtures.json').read_text())
    state = fixture['State']['Controller']
    assert state['Profile'] == 'source-ai-independent-field-contact-capture-v3'
    assert state['Mode'] == 1 and state['Fault'] is None and state['SkillEvents'] == 1
    assert state['GameBase'] == 1000 and state['Ai']['LastSkillFrame'] == 1025
    assert len(state['LastHeads']) == 3 and fixture['Life'] == 6007 + 2 * 9
    assert state['Fields']['Heads'] == state['Fields']['Mats'] == []
    assert state['LastFieldId'] == fixture['Ground']['Id'] and fixture['Ground']['Fault'] is None
    fields = fixture['Ground']['Fields']
    assert fields['Profile'] == 'charged-motion-current-range-contact-capture-v1'
    assert len(fields['Heads']) == 3 and len(fields['Mats']) == 6
    assert all(m['Total'] == 125 and m['Z'] == 100 for m in fields['Mats'])
    assert all(h['Velocity256'] == 1344 and h['Z'] == 100 for h in fields['Heads'])
    assert any(m['LastCollision'] != 0 for m in fields['Mats']) and fields['Contacts'] == 0
    orphan = json.loads((STAGE / 'ground-fixtures.json').read_text())
    ground = orphan['Ground']
    assert orphan['RemovedActor'] == ground['Actor'] and orphan['Owner'] == ground['Owner']
    assert ground['Fault'] is None and ground['Map'] == orphan['Map']
    assert len(ground['Fields']['Heads']) == 3 and len(ground['Fields']['Mats']) == 6
    assert all(h['Remaining'] == 4 for h in ground['Fields']['Heads'])
    assert any(m['LastCollision'] != 0 for m in ground['Fields']['Mats']) and ground['Fields']['Contacts'] == 0
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
        ('source/D2Common/src/Units/Units.cpp', ['UNITS_GetMeleeRange', 'UNITS_IsInMeleeRange', 'UNITS_GetDistanceToCoordinates']),
        ('source/D2Common/src/D2Skills.cpp', ['SKILLS_GetHighestLevelSkillFromSkillId', 'SKILLS_EvaluateSkillFormula']),
        ('source/D2Game/src/SKILLS/SkillSor.cpp', ['SKILLS_MissileInit_ChargedBolt']),
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvDo130_VineAttack']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['D2GAME_CreateMissile_6FD115E0']),
        ('source/D2Game/src/PLAYER/Player.cpp', ['sub_6FC7C900', 'sub_6FC7CA10']),
        ('source/D2Game/src/MISSILES/MissMode.cpp', ['MISSMODE_SrvDo26_Vines_PlagueVines', 'MISSMODE_HandleMissileCollision'])]:
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
        'src/Sanctuary.Content/DruidCreeperFields.cs', 'src/Sanctuary.Content/DruidEmitterPatterns.cs',
        'src/Sanctuary.Content/DruidCreeperGround.cs',
        'src/Sanctuary.Content/ChargedBolts.cs', 'src/Sanctuary.Content/SourceMissileSteps.cs',
        'src/Sanctuary.Content/AmazonSlowMissiles.cs', 'src/Sanctuary.Content/CombatHitboxes.cs',
        'src/Sanctuary.Content/PetGroundWorld.cs', 'src/Sanctuary.Content/DruidSpiritWolfWorld.cs',
        'src/Sanctuary.Content/SourcePetMovement.cs', 'src/Sanctuary.Content/PetOwnerRoutes.cs',
        'src/Sanctuary.Content/SourceActorPath.cs', 'src/Sanctuary.Content/SourceActorMotion.cs',
        'src/Sanctuary.Content/SourceAnimationEvents.cs', 'tools/build_class_skills.py',
        'tests/Sanctuary.Integration/CreeperWorldChecks.cs', 'tests/Sanctuary.Integration/SpiritWolfWorldChecks.cs',
        'tests/Sanctuary.Integration/CreeperFieldsChecks.cs',
        'tests/Sanctuary.Integration/CreeperGroundChecks.cs',
        'tests/check_creeper_stats.py', 'tools/record_creeper_world.py',
        'tools/reference/creeper-stats-native-evidence.json', 'tools/reference/creeper-ai-source-evidence.json',
        'tools/reference/creeper-preflight.json', 'data/Sanctuary/Balance/necromancer-summons.json']]
    paths += [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log',
        'world-fixtures.json', 'ground-fixtures.json', 'wolf-world-fixtures.json', 'python-tests.log', 'content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll']]
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], installed=False, learnedHandler=False,
        profile=state['Profile'], automaticWorldClock=True, actorConnected=True, liveVineDamage=False,
        headRequestsCapturedOnly=False, movingHeads=True, finiteMats=True, poisonContactsCapturedOnly=True,
        independentFieldPersistence=True, removedActorFieldsReloaded=True, embeddedFieldsMigrated=True,
        boots=3, actualReloads=2, connectedNaturalFollows=3, connectedNaturalFieldExpiries=3,
        passedMarkers=len(re.findall(': passed', log)), pythonTests=70, handlers=167, druidHandlers=11,
        fullClassRegression=False, currentExecutableParity=False, completedClasses=[], goalComplete=False,
        officialSynergyReference=dict(url='https://news.blizzard.com/en-gb/article/23788293/diablo-ii-resurrected-patch-2-4-ladder-now-live',
            scope='Released2.4notes announce Rabies adding10percent poison damage per level to Creeper, matching the current table. This corroborates intended synergy, not modern NPC transport or capture timing.'),
        sourceHashes=source_hashes, reviewedRoutineHashes=routines,
        hashes={str(p.relative_to(ROOT)): sha(p) for p in paths},
        established=[
            'Saved automatic40ms actor controller consumes the already-compared Vines outer AI and shared common/six-mode pet movement. Three temporary-account loopback sessions pass the logged-in transport gate and move the actual Creeper toward its owner under natural timers.',
            'Explicit world tests retain first think25, shared follow/warp service routing, scalar source escape away from an exact100poison-resist target, lazy mapped-NPC target scan and source melee-range2plus1threshold. Source reach/escape bodies are reviewed/hash-pinned; full room/target helpers are not represented as complete current-executable services.',
            'A mature supplied game base1000allows the initial melee skill attempt at actor frame25/remembered global1025. A supplied fresh base0retains strict early-game delay and attempts at a later AI decision. The default clock uses server-initialization elapsed age; actor base/remembered frame persist while inactive intervals discard actions/healing.',
            'Current K9S1kind2event launches actual prepared moving heads from the target coordinates. Flags3cardinal delta becomes absolute aim before charged initialization, whose seed uses aimXplus ordinal. Shared charged path, velocity and crossing helpers are reused. Head20frame life and remaining18/9trail emission produce six finite mats from three rank1heads. Mat creation precedes movement/decrement; new mats first advance next local tick. Stored paths, original Z, remaining lives and collider history are deep-copied and survive two real world reloads without replay. The missing-target branch remains without a separate native transition check.',
            'Current trail Range is evaluated by the existing formula evaluator: rank1/20/21/255give125/600/625/6475frames. Native controls cover20frame expiry, two emissions per unobstructed head, age15inactive/16active, early LastCollide update, alternating recent victim rather than a permanent hit set, refused allegiance contact, inactive age without movement/emission/contact, slow33percent producing443speed, terrain-service refusal and malformed/JSON roundtrip state. Damage-eligible contacts are counted only; poison is deliberately not delivered.',
            'Fields now have independent actor/player/facet identities, absolute next-frame UTC and deep-copied state in priority173persistence. Actor deletion preserves existing fields, missing actors refuse fresh child creation, and original-player death removes fields. A different owner facet does not relocate mats or prevent their hostile-NPC contact captures. Inactive intervals discard motion/emission/contact while lifetime runs. Original-player allegiance, coordinate-distance child limits and sector/terrain services remain explicit projections.',
            'A removed-actor group and a separate live-actor group survive two actual world reloads with exact saved paths, heights, remaining life and nonzero early collision history. First loaded advancement discards actions; later active steps resume without reconstructing the missing actor. Native embedded-state migration transfers fields exactly once without replaying launch or losing age/history. Duplicate and invalid-facet restores leave the existing pool unchanged.',
            'Three authenticated natural sessions now create fields after the initial follow, observe both emission batches, delete the actor and wait for the independent timer to expire the remaining mats. The always-running conductor continues advancing fields with no actor in its tracked set. The internal direct factory is exercised, not a learned cast or animation.',
            'Shared native terrain helper preserves the existing Wolf ground projection. Wolf world/cold-hit/slow-state controls and two actual reloads pass alongside Creeper. Retained Creeper life/buff/history/facet and numeric/policy controls remain checked. Main installed runtime/saves/config/client are unchanged.'],
        remaining=[
            'This is an internal moving-head/finite-mat/contact-capture profile, not a functioning learned Poison Creeper attack. Poison owner/Rabies/mastery/pierce transport, raw packet creation, resistance/duration delivery and kill credit must be connected next. No poison damage is claimed.',
            'The reviewed legacy formula wrapper preserves its supplied unit; the highest-skill lookup searches that unit only. Vine Attack supplies the Creeper as missile owner, while the current trail overrides damage skill to player222whose formula asks Rabies hard rank. These reviewed bodies do not establish modern pet-to-player synergy transport. Do not silently inherit all owner gear/mastery or assert a zero-synergy current-game result.',
            'Current Range expression evaluation is a declared adapter, not executable-parser equivalence. Local head/mat tick ordering, charged floating projection, native terrain-fit/sector-order/height and size3collision services are not exact D2R room/path/collision certificates. The declared original-player allegiance adapter preserves hostile-NPC filtering after actor removal; exact modern ownerless collision/friendly selection, child distance/room checks after source relocation and shared global event order remain unverified.',
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
    print('Creeper world:3boots/2reloads/3connected follows+field expiries; independent removed-actor fields/migration/contact capture pass; poison/learned casting remain pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
