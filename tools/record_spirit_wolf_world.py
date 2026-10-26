#!/usr/bin/env python3
"""Bind bounded saved wolf movement/cold-melee acceptance; learned casting acceptance is separate."""
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
    assert log.count('spirit-wolf-hit-check: passed') == 1
    assert log.count('spirit-wolf-hit-live-check: passed') == 3
    assert log.count('spirit-wolf-hit-live-result: passed') == 3
    assert '384complete source cold packet/RNG comparisons' in log
    assert log.count('spirit-wolf-rate-check: passed') == 1
    assert log.count('spirit-wolf-rate-live-check: passed') == 3
    assert log.count('spirit-wolf-rate-live-result: passed') == 3
    assert '576complete source attack writer/event compositions' in log
    assert log.count('spirit-wolf-slow-live-check: passed') == 3
    assert log.count('spirit-wolf-slow-live-result: passed') == 3
    assert log.count('Writing world save snapshot done') == 3
    assert log.count('Shutting down') == 3
    fixture = json.loads((STAGE / 'world-fixtures.json').read_text())
    controller = fixture['State']['Controller']
    assert controller['Profile'] == 'source-ai-native-ground-fit-cold-hit-v2'
    assert controller['Mode'] == 1 and controller['Motion']['Points'] == []
    assert controller['Fault'] is None and controller['StrikeRequests'] > 0 and controller['LastHit'] is not None
    assert controller['AttackSpeed'] == 88 and controller['DeferredThinks'] > 0
    assert controller['AttackThinkPolicy'] == 'observed-bite-continuation-neutral-delay-v1'
    assert fixture['Route']['Owner'] == fixture['Owner']
    assert len(fixture['Route']['Trail']) == 20
    assert fixture['Life'] == 100 * 256 + 17 + 2 * 32
    paths = [ROOT / name for name in [
        'src/Sanctuary.Content/DruidSpiritWolfWorld.cs', 'src/Sanctuary.Content/DruidWolfGeometry.cs',
        'src/Sanctuary.Content/PetOwnerRoutes.cs', 'src/Sanctuary.Content/DruidSpiritWolfBody.cs',
        'src/Sanctuary.Content/SourcePetMovement.cs', 'src/Sanctuary.Content/DruidSpiritWolfAi.cs',
        'src/Sanctuary.Content/SourceActorMotion.cs', 'src/Sanctuary.Content/SourceActorPath.cs',
        'src/Sanctuary.Content/ClassMovement.cs', 'src/Sanctuary.Content/Teleportation.cs',
        'src/Sanctuary.Content/SkeletonPets.cs', 'src/Sanctuary.Content/DruidSpiritWolfHit.cs',
        'src/Sanctuary.Content/AssassinTrapAttack.cs', 'src/Sanctuary.Content/SourceBlocking.cs',
        'src/Sanctuary.Content/ColdStates.cs', 'src/Sanctuary.Content/SkillHealth.cs', 'src/Sanctuary.Content/SkillCombat.cs', 'tools/build_class_skills.py',
        'data/Sanctuary/Balance/necromancer-summons.json', 'tests/check_spirit_wolf_body.py',
        'tests/Sanctuary.Integration/SpiritWolfWorldChecks.cs', 'tests/Sanctuary.Integration/SpiritWolfHitChecks.cs', 'tools/record_spirit_wolf_world.py',
        'tools/reference/spirit-wolf-body-native-evidence.json', 'tools/reference/pet-movement-native-evidence.json',
        'tools/reference/druid-beasts-preflight.json', 'tools/reference/spirit-wolf-corrected-ai-source-evidence.json',
        'tools/reference/spirit-wolf-hit-source-evidence.json', 'tools/probe_spirit_wolf_hit_reference.py']]
    paths += [ROOT / name for name in ['src/Sanctuary.Content/SourceAttackRate.cs',
        'tests/Sanctuary.Integration/SpiritWolfRateChecks.cs', 'tools/probe_spirit_wolf_rate_reference.py',
        'tools/reference/spirit-wolf-rate-source-evidence.json', 'src/Sanctuary.Content/GearEffects.cs',
        'tools/reference/druid-beasts-user-observations.json', 'tools/reference/spirit-wolf-equipment-scope.json',
        'tools/record_spirit_wolf_equipment_scope.py']]
    paths += [STAGE / name for name in [
        'stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'world-fixtures.json',
        'python-tests.log', 'content-build.log', 'integration-build.log',
        'content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll']]
    evidence = dict(referenceVersion='3.3.93847', supplementaryAiCommit='7f6d773f31060e97124241e6bd30d606bcab6d4e',
        scope='Prepared world-controller/cold-melee factory and retained life/policy consumers, with declared native terrain/input services. Learned casting acceptance is recorded separately; the omitted legacy physical floor remains provisional.',
        profile=controller['Profile'], installed=False, actorConnected=True, automaticWolfClock=True,
        boundedColdMelee=True, ordinaryAttackFullParity=False, learnedHandler=True, fullClassRegression=False, currentExecutableParity=False,
        sourceColdPacketComparisons=384, sourceAttackRateComparisons=576, boots=3, actualReloads=2,
        connectedNaturalFollows=3, connectedNaturalAttacks=3, connectedNaturalHasteAttacks=3, connectedNaturalRepeatedSlowAttacks=3, attackThinkPolicy=controller['AttackThinkPolicy'], passedMarkers=len(re.findall(r': passed', log)),
        pythonTests=70, buildWarnings=0, buildErrors=0, handlers=167, druidHandlers=11,
        completedClasses=[], goalComplete=False,
        hashes={str(path.relative_to(ROOT)): sha(path) for path in paths},
        established=[
            'A saved world controller now calls CorrectedThink and SourcePetMovement through real actor services. Three authenticated loopback sessions exercise the natural40ms timer: initial25-frame think, source paths and position updates bring a distant wolf toward its owner. This tests server movement, not graphical client animation or a learned cast.',
            'Explicit world frames retain individual targets when another wolf is created, approach an occupied target footprint and request A1only in melee. A1action now resolves a recorded outcome and saved seed/hit count. Shared hit recovery cancels queued actions. Three additional connected natural clocks commit actual cold damage and chill.',
            'The cold-only packet matches384complete older-source skip-physical contexts, retaining FP256/exclusive upper bounds, copied whole endpoints, actor mastery controls, equal-endpoint draw suppression and raw Crit5all-channel doubling without length change. Fire/lightning/cold order is retained. The shared integer nonplayer hit helper uses copied flat AR plus live actor percent; native skeleton Dexterity and player-only owner modifiers do not leak.',
            'Actual mapped-NPC checks cover misses, shield blocking before cold fill, fractional cold damage/chill,100percent cold immunity without guessed physical damage, intrinsic critical, live actor Enchant fire/AR, ordinary release reach distinct from AI melee, invalid-range no-RNG and lethal killer identity. Older movement-only profiles migrate without replaying a pending strike. Saved LastHit/outcome/counters survive two actual reloads.',
            'Attack-entry rate now uses the source base100plus actor haste and distinct cold/item/Decrepify/HolyFreeze/Impale slow fields before the final15..175clamp. Cold-stat injection is a component control; both ordinary incoming-chill paths now refuse Spirit Wolf per the user observation while preserving its cold damage resistance.576complete source writer/scheduler comparisons pass. Native Fanaticism50sets264speed/action+9/end+13; compound haste50/cold50/Decrepify50sets88/action+27/end+38; an added item slow reaches26/action+89/end+128. New attacks refresh the live rate; queued events retain their entry capture. Three additional connected natural haste attacks land at actor frame34. Saved speed88and a consumed busy-think count survive the parked slowed fixture and two actual reloads.',
            'The pinned45-row equipment table has no Spirit Wolf entry. The complete legacy loader leaves its equipment index negative and the already-compiled dispatcher guard returns without item creation. Generated equipment/IAS transport is not a Spirit Wolf dependency under this declared current-table/legacy-dispatch model; other equipped summons retain their gaps.',
            'The user confirms continuing bites under Decrepify without Fanaticism. Explicitly provisional observed-bite-continuation-neutral-delay-v1keeps A1active, consumes an overlapping decision and uses the existing mode-end neutral delay for the next think. Native Decrepify-only controls land at52and105with88speed and two consumed busy decisions. Three additional authenticated natural sessions deliver at least two slowed cold hits each. Known old prepared overlap-refusal faults migrate to parked, unfaulted state without replaying a hit.',
            'Delayed Teleport2request is distinct from common-pet placement: event+4moves to its captured legal destination; +3does not. Source S1/A1/WL/RN/GH metadata and animation event plans are exported separately from current-executable timing claims.',
            'Saved owner history has20points, strict25ms/displacement45gates, wrap, unconditional successful teleport append and50-frame last-teleport expiration. Snapshots do not alias caller arrays; malformed owner history/world state restoration is atomic.',
            'Offline and wardrobe states stop actions, RNG and healing while discarding elapsed frames. Two actual world reloads retain the parked actor owner/birth/pose/seed/clock/fractional injury and owner history; one subsequent active frame resumes placement and adds32FP recovery. Body-only health, buffs, facet and other retained consumers remain separately checked.',
            'Three boots/two reloads retain policy, numeric, Raven/path/Valkyrie, named gear, buffs, histories, wardrobe, Inner Sight and three actual Decoy casts/expiries/disconnect checks. Fresh audits retain573named definitions and all70Python checks pass. The installed main build and original saves/configuration/client are unchanged.'
        ], remaining=[
            'The busy-A1continuation policy is an explicit inference from observed continuing bites plus the existing neutral-delay path. The user establishes no Fanaticism and continued attacks, not the exact mode-end/AI cadence. Modern mid-swing cancellation/re-entry, post-contact overlapping AI decisions, owner-movement/target-loss priorities and exact current dispatch still need corroboration; do not label the inferred policy exact D2R parity. Mid-swing current-executable rescheduling and generic melee-only morph/dual-wield transport remain unverified; the pinned Spirit Wolf equipment path creates no items. Recipient HTH-defense additions/passive avoidance and exact block-mode timing need broader acceptance.',
            'The cold-only ordinary hit is a clearly provisional current-data profile: the user expects100percent cold damage, but the tiny physical floor in older source remains unverified in3.3.93847. Item events beyond the pinned no-equipment scope, passive target avoidance, PvP/unmapped targets, exact retaliation/event order and nonzero cold sunder remain unsupported or unverified. Holy elemental-aura channel composition is wired but not separately established by the Enchant checks. Actor Dexterity0is an explicit default projection pending complete current monster initialization acceptance.',
            'Learned do119payment/FIFO-cap/coexistence/right/quick input has separate acceptance in spirit-wolf-casting-native-evidence.json. Exact placement/search, full-class regression, matching graphical client acceptance and installation remain pending. One candidate handler is added; no class completion is claimed.',
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
    print('Prepared world wolf:3boots/2reloads/3natural follows/3natural cold attacks/3natural haste attacks/3natural repeated slow attacks,384packet and576rate comparisons; exact busy cadence provisional, separate learned-casting acceptance.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
