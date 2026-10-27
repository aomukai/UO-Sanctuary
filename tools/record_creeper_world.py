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
    assert log.count('creeper-poison-check: passed') == 1
    assert log.count('creeper-poison-reload-check: passed') == 2
    assert log.count('creeper-poison-live-result: passed') == 3
    assert log.count('creeper-casting-check: passed') == 1
    assert log.count('creeper-casting-reload-check: passed') == 2
    assert log.count('creeper-world-reload-check: passed') == 2
    assert log.count('creeper-world-live-check: passed') == 3
    assert log.count('creeper-world-live-result: passed') == 3
    assert log.count('spirit-wolf-world-check: passed') == 1
    assert log.count('spirit-wolf-world-reload-check: passed') == 2
    assert log.count('Shutting down') == 3
    fixture = json.loads((STAGE / 'world-fixtures.json').read_text())
    state = fixture['State']['Controller']
    assert state['Profile'] == 'source-ai-independent-vine-poison-provisional-v4'
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
    context = fixture['Ground']['PoisonContext']
    assert context['Profile'] == 'birth-owner-rabies-no-player-gear-provisional-v1'
    assert context['OwnerLevel'] == 99 and context['RabiesHardRank'] == 0
    for mat in fields['Mats']:
        assert mat['Poison']['Damage'] == dict(Minimum256=32, Maximum256=48, Frames=100, Count=0)
        assert mat['Poison']['SlowDamagePercent'] == 100
    orphan = json.loads((STAGE / 'ground-fixtures.json').read_text())
    ground = orphan['Ground']
    assert orphan['RemovedActor'] == ground['Actor'] and orphan['Owner'] == ground['Owner']
    assert ground['Fault'] is None and ground['Map'] == orphan['Map']
    assert len(ground['Fields']['Heads']) == 3 and len(ground['Fields']['Mats']) == 6
    assert all(h['Remaining'] == 4 for h in ground['Fields']['Heads'])
    assert any(m['LastCollision'] != 0 for m in ground['Fields']['Mats']) and ground['Fields']['Contacts'] == 0
    assert all(m['Poison'] is not None for m in ground['Fields']['Mats'])
    poison = json.loads((STAGE / 'poison-fixtures.json').read_text())
    assert poison['Context']['RabiesHardRank'] == 20
    assert poison['Mat']['Poison']['Damage'] == dict(Minimum256=96, Maximum256=144, Frames=100, Count=0)
    assert poison['Poison']['PoisonSource'] == poison['Owner'] and poison['Poison']['Owner'] == poison['Target']
    assert poison['Poison']['PoisonMs'] == 3920 and 96 <= poison['Poison']['PoisonClock']['Rate256'] < 144
    casting = json.loads((STAGE / 'casting-fixtures.json').read_text())
    assert casting['State']['Owner'] == casting['Owner']
    assert casting['State']['Snapshot']['Birth']['Rank'] == 1 and casting['State']['Controller']['Mode'] == 1
    assert casting['State']['Controller']['SkillEvents'] == 0 and casting['State']['Controller']['Fault'] is None
    assert casting['Body'] not in [casting['Wolf'], casting['Raven']] and casting['Mana'] >= 0
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
        ('source/D2Common/src/D2States.cpp', ['STATES_ToggleState']),
        ('source/D2Common/src/D2StatList.cpp', ['D2Common_STATES_ToggleState_6FDB8900']),
        ('source/D2Common/src/Path/Path.cpp', ['PATH_GetCollisionPatternFromMonStats2Txt']),
        ('source/D2Game/src/GAME/Targets.cpp', ['D2GAME_UpdateSummonAI_6FC401F0']),
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
        'src/Sanctuary.Content/DruidCreeperPoison.cs', 'src/Sanctuary.Content/PoisonPackets.cs',
        'src/Sanctuary.Content/DruidCreeperCasting.cs', 'src/Sanctuary.Content/DruidRavenCasting.cs',
        'src/Sanctuary.Content/PoisonStates.cs', 'src/Sanctuary.Content/TimedEffects.cs',
        'src/Sanctuary.Content/ChargedBolts.cs', 'src/Sanctuary.Content/SourceMissileSteps.cs',
        'src/Sanctuary.Content/AmazonSlowMissiles.cs', 'src/Sanctuary.Content/CombatHitboxes.cs',
        'src/Sanctuary.Content/PetGroundWorld.cs', 'src/Sanctuary.Content/DruidSpiritWolfWorld.cs',
        'src/Sanctuary.Content/SourcePetMovement.cs', 'src/Sanctuary.Content/PetOwnerRoutes.cs',
        'src/Sanctuary.Content/SourceActorPath.cs', 'src/Sanctuary.Content/SourceActorMotion.cs',
        'src/Sanctuary.Content/SourceAnimationEvents.cs', 'tools/build_class_skills.py',
        'tests/Sanctuary.Integration/CreeperWorldChecks.cs', 'tests/Sanctuary.Integration/SpiritWolfWorldChecks.cs',
        'tests/Sanctuary.Integration/CreeperFieldsChecks.cs',
        'tests/Sanctuary.Integration/CreeperGroundChecks.cs',
        'tests/Sanctuary.Integration/CreeperPoisonChecks.cs', 'tests/Sanctuary.Integration/CreeperStatsChecks.cs',
        'tests/Sanctuary.Integration/CreeperCastingChecks.cs',
        'tests/check_creeper_stats.py', 'tools/record_creeper_world.py',
        'tools/reference/creeper-stats-native-evidence.json', 'tools/reference/creeper-ai-source-evidence.json',
        'tools/reference/creeper-preflight.json', 'data/Sanctuary/Balance/necromancer-summons.json']]
    paths += [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log',
        'world-fixtures.json', 'ground-fixtures.json', 'poison-fixtures.json', 'casting-fixtures.json', 'wolf-world-fixtures.json', 'python-tests.log', 'content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll']]
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], installed=False, learnedHandler=False,
        profile=state['Profile'], automaticWorldClock=True, actorConnected=True, liveVineDamage=True,
        headRequestsCapturedOnly=False, movingHeads=True, finiteMats=True, poisonContactsCapturedOnly=False,
        provisionalPoisonCapture=True, poisonCaptureProfile=context['Profile'],
        preparedPaymentPlacement=True, learnedInputConnected=False, zeroFootprintMobilePlacement=True,
        independentFieldPersistence=True, removedActorFieldsReloaded=True, embeddedFieldsMigrated=True,
        boots=3, actualReloads=2, connectedNaturalFollows=3, connectedNaturalFieldExpiries=3, connectedNaturalPoisonRuns=3,
        passedMarkers=len(re.findall(': passed', log)), pythonTests=70, handlers=167, druidHandlers=11,
        fullClassRegression=False, currentExecutableParity=False, completedClasses=[], goalComplete=False,
        officialSynergyReference=dict(url='https://news.blizzard.com/en-gb/article/23788293/diablo-ii-resurrected-patch-2-4-ladder-now-live',
            scope='Released2.4notes announce Rabies adding10percent poison damage per level to Creeper, matching the current table. This corroborates intended synergy, not modern NPC transport or capture timing.'),
        exposureReview=dict(implemented=False, stateId=150, currentSetFunc=11, currentClientActiveFunc=89,
            scope='Reviewed state toggle changes state bits/disguise and refreshes the unit; summon-node helper links owner targeting nodes without setting damage protection. Source size0selects collision patternNONE. Current isAtt0supports filtered exclusion, not a complete global invulnerability policy. Exact surface/underground timing and incoming routes remain open.',
            supplementaryGuide='https://www.theamazonbasin.com/wiki/index.php/Poison_Creeper',
            guideScope='Guide reports no damage underground but does not establish this pinned build or expose a timing boundary. Its3+rank second mat duration also differs from the pinned current expression100+25*rank. It is supplementary, not a current-executable certificate.'),
        sourceHashes=source_hashes, reviewedRoutineHashes=routines,
        hashes={str(p.relative_to(ROOT)): sha(p) for p in paths},
        established=[
            'Saved automatic40ms actor controller consumes the already-compared Vines outer AI and shared common/six-mode pet movement. Three temporary-account loopback sessions pass the logged-in transport gate and move the actual Creeper toward its owner under natural timers.',
            'Explicit world tests retain first think25, shared follow/warp service routing, scalar source escape away from an exact100poison-resist target, lazy mapped-NPC target scan and source melee-range2plus1threshold. Source reach/escape bodies are reviewed/hash-pinned; full room/target helpers are not represented as complete current-executable services.',
            'A mature supplied game base1000allows the initial melee skill attempt at actor frame25/remembered global1025. A supplied fresh base0retains strict early-game delay and attempts at a later AI decision. The default clock uses server-initialization elapsed age; actor base/remembered frame persist while inactive intervals discard actions/healing.',
            'Current K9S1kind2event launches actual prepared moving heads from the target coordinates. Flags3cardinal delta becomes absolute aim before charged initialization, whose seed uses aimXplus ordinal. Shared charged path, velocity and crossing helpers are reused. Head20frame life and remaining18/9trail emission produce six finite mats from three rank1heads. Mat creation precedes movement/decrement; new mats first advance next local tick. Stored paths, original Z, remaining lives and collider history are deep-copied and survive two real world reloads without replay. The missing-target branch remains without a separate native transition check.',
            'Current trail Range is evaluated by the existing formula evaluator: rank1/20/21/255give125/600/625/6475frames. Native controls cover20frame expiry, two emissions per unobstructed head, age15inactive/16active, early LastCollide update, alternating recent victim rather than a permanent hit set, refused allegiance contact, inactive age without movement/emission/contact, slow33percent producing443speed, terrain-service refusal and malformed/JSON roundtrip state. New fields now deliver raw poison packets at damage-eligible contacts; old capture-only groups retain null packets and remain harmless.',
            'Fields now have independent actor/player/facet identities, absolute next-frame UTC and deep-copied state in priority173persistence. Actor deletion preserves existing fields, missing actors refuse fresh child creation, and original-player death removes fields. A different owner facet does not relocate mats or prevent their hostile-NPC contact captures. Inactive intervals discard motion/emission/contact while lifetime runs. Original-player allegiance, coordinate-distance child limits and sector/terrain services remain explicit projections.',
            'A removed-actor group and a separate live-actor group survive two actual world reloads with exact saved paths, heights, remaining life and nonzero early collision history. First loaded advancement discards actions; later active steps resume without reconstructing the missing actor. Native embedded-state migration transfers fields exactly once without replaying launch or losing age/history. Duplicate and invalid-facet restores leave the existing pool unchanged.',
            'Three authenticated natural sessions create fields after the initial follow, observe both emission batches, delete the actor and introduce two stationary hostile monsters after the inactive contact window. Actual monster life falls from poison and the independent timer expires remaining mats. The always-running conductor continues with no actor in its tracked set. The internal direct factory is exercised, not a learned cast or animation.',
            'New mats capture raw rates,100frame poison length, slow damage scalar and per-mat RNG. Rank1/no-synergy gives32/48rates and hard20Rabies gives96/144; slow33percent gives10/15without shortening poison. The mapped creator helper also matches all zero-mastery cases in the retained complete source endpoint fixture. Ground pools preserve packet/rank/context/seed across two reloads; missing packets and packet/context disagreement are atomically refused.',
            'The bridge reuses recipient resistance/curse/aura pools, independent poison rate/length mitigation, exclusive-maximum rolls and the source40msnegative-regeneration clock. Native controls check no immediate damage,39/40msboundary,50percent and negative resistance, immunity, weaker/stronger replacement, four-second expiry, lethal NPC poison and original-player damage attribution. Landed poison survives two actual reloads and resumes one exact frame with unchanged source/rate. Its inspection UpdatedUtc is held two days; ordinary four-second expiry is checked separately.',
            'Prepared do115release transaction uses current8mana/InTown1and current effective rank, allocates before payment and replaces existing source-category vines only after successful payment. Native controls cover selected and deleted target coordinates, occupied zero-footprint placement, failed allocation, lost/fractional mana, successful replacement, frozen/invalid-point refusal, town and unchanged Wolf/Raven families. Both actual reloads retain paid state and mana; a new paid commit after each reload replaces the previous vine. This is an internal release transaction, not SC/FCR windup, learned dispatch or client input acceptance.',
            'Shared native terrain helper preserves the existing Wolf ground projection. Wolf world/cold-hit/slow-state controls and two actual reloads pass alongside Creeper. Retained Creeper life/buff/history/facet and numeric/policy controls remain checked. Main installed runtime/saves/config/client are unchanged.'],
        remaining=[
            'This is an internal functional-poison profile with explicitly provisional capture timing, not a learned or current-executable parity certificate. Modern pet/owner stat transport, full mastery/pierce/sunder, current credit/event ordering and graphical acceptance remain open.',
            'The named birth-owner-rabies-no-player-gear-provisional-v1model uses the already-captured summon-birth hard Rabies context and inherited head rank, then creates the raw packet/slow scalar at each mat birth. It honors the current intended synergy and does not copy player weapon/gear mastery or pierce into the NPC. Released notes/tables establish the synergy, not this timing. Modern live-versus-birth Rabies and equipment transport must still be corroborated; this is not an approved Sanctuary deviation. Original-player poison attribution is an explicit durable source-credit adapter after NPC removal.',
            'Current Range expression evaluation is a declared adapter, not executable-parser equivalence. Local head/mat tick ordering, charged floating projection, native terrain-fit/sector-order/height and size3collision services are not exact D2R room/path/collision certificates. The declared original-player allegiance adapter preserves hostile-NPC filtering after actor removal; exact modern ownerless collision/friendly selection, child distance/room checks after source relocation and shared global event order remain unverified.',
            'VINE_BEAST surface/underground visibility and general melee/projectile targeting remain unverified. Reviewed server state/target-node/size routines do not prove the client state effect or exposure window; no invulnerability timing is invented. isAtt0offensive aura exclusion remains intact, while the named silent0x8actor is visibly vulnerable preparation only.',
            'Server-start age and saved per-actor source-frame offset are explicit clock adapters. Exact modern NPC skill-rate/mode-end/AI scheduling, game epoch, shared actor ordering, source scan alternatives/list/ties/LOS, special target reach and global RNG equivalence are not certified.',
            'Zero-footprint native terrain ignores mobile occupancy but uses ordinary native16height/surface checks. Facet-wide room projection, source-grid conversion, owner one-step movement and nearest mapped target are adapters. AStar/type1, complete moving-target/obstacle path repair and full source collision geometry remain unsupported/unverified.',
            'Nonzero inherited poison sunder and innate-skill Battle Command transport remain explicitly unsupported. Prepared release requires a learned hard point; staffmod-only summon permission, SC/FCR windup/cancellation, right/quick input, full-class regression, graphical client and installation remain pending. Carrion/Solar source-category keys are recognized, but their actual future actors/casts are not exercised. Native ground16height/LOS/range/two-tile retry remain adapters, not exact source placement or room-search certificates. Separate effects/audio and talent/hotkey icons remain placeholders.'])
    target = ROOT / 'tools/reference/creeper-world-native-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Creeper world:3boots/2reloads/3connected poison runs; raw packets and actual damage/expiry/credit persist; capture timing provisional and learned casting pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
