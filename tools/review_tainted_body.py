#!/usr/bin/env python3
"""Bound the Tainted body's source identity and explicitly qualified fire receiver."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def review(check=False):
    inputs = {}
    def pin(path):
        inputs[str(path.relative_to(ROOT))] = sha(path)
        return path.read_text()
    tree = json.loads(pin(ROOT / '.local/class-skills/d2moo-source-tree.json'))
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    routines = {}
    for relative, names in [
        ('source/D2Game/src/MISSILES/MissMode.cpp', ['MISSMODE_SrvHit01_Fireball_ExplodingArrow_FreezingArrowExplosion']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['sub_6FD10140', 'sub_6FD10200']),
        ('source/D2Game/src/MONSTER/MonsterMode.cpp', ['D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70']),
        ('source/D2Game/src/UNIT/SUnitDmg.cpp', ['SUNITDMG_SetHitClass'])]:
        path = ROOT / '.local/class-skills/d2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(r['sha'] for r in tree['tree'] if r['path'] == relative)
        text = pin(path)
        for name in names:
            routines[name] = hashlib.sha256(function(text, name).encode()).hexdigest()
    critical = json.loads(pin(ROOT / 'tools/reference/npc-equipment-critical-source-evidence.json'))
    for name in ('D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70', 'SUNITDMG_SetHitClass'):
        assert routines[name] == critical['compiledUnchangedRoutineHashes'][name]
    missiles_proof = json.loads(pin(ROOT / 'tools/reference/d2r-local-missile-data-evidence.json'))
    owned_missiles = ROOT / missiles_proof['local']['path']
    assert sha(owned_missiles) == missiles_proof['local']['sha256']; pin(owned_missiles)
    with owned_missiles.open(encoding='utf-8-sig', newline='') as f:
        row = next(r for r in csv.DictReader(f, delimiter='\t') if r['Missile'] == 'taintedfireball')
    assert [row[k] for k in ('pSrvHitFunc', 'ResultFlags', 'HitFlags', 'sHitPar1', 'Skill')] == ['1', '5', '2', '0', 'Tainted Fire Ball']
    route = json.loads(pin(ROOT / 'data/Sanctuary/Balance/tainted-fireball-hit.json'))
    assert route == [dict(Skill=422, Missile=734, HitFunction=1, ResultFlags=5, HitFlags=2, Radius=4)]
    foundation = json.loads(pin(ROOT / 'tools/reference/tainted-foundation-source-evidence.json'))
    assert foundation['referenceVersion'] == missiles_proof['referenceVersion'] == '3.3.93847'
    assert (foundation['formulaCases'], foundation['packetCases'], foundation['ownedMonsterFieldComparisons']) == (3570, 6120, 114)
    pin(Path(__file__).resolve())
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], component='Tainted body and qualified fire-hit source review',
        currentExecutableParity=False, newSourceRoutinesExecuted=False, handlerEnabled=False,
        inputs=inputs, reviewedCompleteRoutineHashes=routines,
        established=[
            'Owned missile734 uses hit01, result5, hitflags2 and source NPC skill422. Its zero explicit hit radius selects the NPC skill calc1/Param1=4. The prior packet evidence bounds its own damage/mastery/synergy suppliers.',
            'The reviewed copied-area callback calls block/dodge and NPC intrinsic critical independently per recipient before successful-event/health dispatch. This increment accepts only explicitly supplied successful copies and critical decisions; it does not implement those finder/defense/RNG operations.',
            'The existing compiled NPC intrinsic helper doubles fire and sets an initially empty hit-class high nibble to0x10. The source-zero Fire Ball copy needs no critical overlay54. Native tests supply the critical decision instead of pretending to run the source5percent roll.',
            'Existing Hydra ordered NPC notification/typed mitigation/fractional health/return/death/credit services and Goatman explicit recovery are extracted for reuse. Retained native actor/flight/reload tests must verify these consumers separately.'],
        remaining=[
            'No current GenericPet controller, source skill selection/release/mode/mana transaction, NPC aura, area enumeration, projectile factory/travel or automatic intrinsic RNG is established by this review.',
            'Source room/terrain/event/stat-list ownership, actor-removal behavior, modern critical/RNG chronology and complete graphical acceptance remain separate. No learned Tainted cast is enabled.',
            'Only the two critical routines have a preexisting compiled proof referenced here; this review hashes the other complete bodies without claiming new execution or current-executable parity.'])
    path = ROOT / 'tools/reference/tainted-body-source-review.json'; encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Tainted source review:owned hit01/5/2/NPC422, prior6120packet controls and existing intrinsic helper;finder/defense/RNG/release remain explicit.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    review(parser.parse_args().check)
