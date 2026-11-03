#!/usr/bin/env python3
"""Pin current NPC422/missile734 and distinguish reviewed source from adapters."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]


def review(check=False):
    inputs = {}
    def pin(path):
        raw = path.read_bytes()
        inputs[str(path.relative_to(ROOT))] = hashlib.sha256(raw).hexdigest()
        return raw.decode('utf-8-sig')
    tree = json.loads(pin(ROOT / '.local/class-skills/d2moo-source-tree.json'))
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    routines = {}
    for relative, names in [
        ('source/D2Game/src/SKILLS/Skills.cpp', ['sub_6FD11420', 'sub_6FD0FA00', 'sub_6FD0FE80', 'sub_6FD10140', 'sub_6FD10200']),
        ('source/D2Game/src/SKILLS/SkillAss.cpp', ['sub_6FCF5DE0']),
        ('source/D2Game/src/MISSILES/MissMode.cpp', ['MISSMODE_SrvDo01_BasicMissile', 'MISSMODE_SrvHit01_Fireball_ExplodingArrow_FreezingArrowExplosion']),
        ('source/D2Game/src/MISSILES/Missiles.cpp', ['MISSILES_CreateMissileFromParams']),
        ('source/D2Game/src/UNIT/SUnitDmg.cpp', ['SUNITDMG_ApplyBlockOrDodge', 'SUNITDMG_ApplyDodge'])]:
        path = ROOT / '.local/class-skills/d2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(r['sha'] for r in tree['tree'] if r['path'] == relative)
        text = pin(path)
        for name in names:
            routines[name] = hashlib.sha256(function(text, name).encode()).hexdigest()
    evidence = json.loads(pin(ROOT / 'tools/reference/d2r-local-missile-data-evidence.json'))
    path = ROOT / evidence['local']['path']
    assert hashlib.sha256(path.read_bytes()).hexdigest() == evidence['local']['sha256']
    row = next(r for r in csv.DictReader(pin(path).splitlines(), delimiter='\t') if r['Missile'] == 'taintedfireball')
    route = json.loads(pin(ROOT / 'data/Sanctuary/Balance/tainted-fireball-flight.json'))
    assert len(route) == 1 and all(int(row.get(key) or 0) == value for key, value in route[0].items())
    parent = json.loads(pin(ROOT / 'tools/reference/tainted-foundation-source-evidence.json'))
    assert parent['referenceVersion'] == evidence['referenceVersion'] == '3.3.93847'
    for name in ('tainted-body-source-review.json', 'tornado-area-source-evidence.json', 'npc-equipment-critical-source-evidence.json', 'missile-velocity-evidence.json'):
        pin(ROOT / 'tools/reference' / name)
    pin(Path(__file__).resolve())
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], inputs=inputs,
        reviewedCompleteRoutineHashes=routines, newSourceRoutinesExecuted=False, currentExecutableParity=False, handlerEnabled=False,
        established=[
            'Owned NPC422 has the default srvmissile route, not a new custom do function. The reviewed ordinary sub_6FD11420 supplies flags33, original NPC origin and target coordinates. Missile734 has do1/hit1, velocity20, range50, size1, collision-kill and init-step1; its Collision layer is absent.',
            'Reuse BasicMissileUpdate, SourceMissileSteps, Q12 MissileVectors, the launch-time SlowMissilePayload and SourceDamageAreas. Full factory/first-update/world path/terrain/global-phase/current client parity is not implied by these kernels.',
            'Hit01 obtains one missile damage roll then default0x8583 area callbacks. sub_6FCF5DE0 copies the packet for each recipient; defense uses1/0 (avoid on, ordinary shield block off) and intrinsic critical is per recipient. The ordinary non-pet NPC projection has no class avoidance; PvP, hirelings and class/summoned/equipped passive recipients refuse explicitly.',
            'Reuse the persisted NPC zero-item actor stream for intrinsic5percent, without falsely implementing Tainted unarmed melee. The legacy serialized Unarmed flag keeps its schema and denotes the no-item binding. Initial world/projectile seed allocation and native room-list order remain host adapters.',
            'Use the checked qualified NPC fire receiver for resistance, fractional life, events, return/death and original-NPC/owner credit. Source class gear and player potion damage are not inherited.'],
        remaining=[
            'Actual GenericPet mode/owner/skill lookup and automatic selection/release, NPC420 aura, Blood Boil, incoming combat, shared cap/payment and learned summon remain unfinished.',
            'Exact modern source disappearance/death/offline/facet behavior is unverified. Host policy ages inactive flights without movement, damage, RNG or replay; it is not asserted as exact D2R source-removal behavior.',
            'Native source-cell/footprint/height/terrain and room/enumeration projections, callback-mutated targets, reflective missile recipients and full graphical/audio acceptance remain separate. Generic travel/explosion markers are identified placeholders; no player icon is added for internal NPC422.',
            'This review hashes complete source bodies and reuses existing source evidence. It does not claim a newly executed complete current or older factory/area/defense pipeline.'])
    target = ROOT / 'tools/reference/tainted-fireball-flight-source-review.json'
    text = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == text
    else:
        target.write_text(text)
    print('Tainted Fire Ball:owned734 flight fields;default33/do1/hit1/copied8583/defense1,0 reviewed;shared kernels and explicit adapters.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    review(parser.parse_args().check)
