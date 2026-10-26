#!/usr/bin/env python3
"""Pin Tornado's distinct periodic-area and direct-contact routes before implementation."""
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    primary = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((primary / 'sources.json').read_text())
    assert manifest['commit'] == 'fa1286949266498b294c40c2942521a7c0d7207a'
    tables, pins = {}, {}
    for name in ['skills', 'missiles', 'skilldesc']:
        raw = (primary / f'json/{name}.json').read_bytes()
        pins[name] = hashlib.sha256(raw).hexdigest()
        assert pins[name] == manifest['files'][f'json/{name}.json']['sha256']
        tables[name] = json.loads(raw)
    skill, missile = tables['skills']['245'], tables['missiles']['tornado']
    assert skill['skill'] == 'Tornado' and skill['srvdofunc'] == 118 and skill['calc1'] == 1 and skill['mana'] == 10
    assert [skill[k] for k in ['aurafilter', 'Param1', 'Param2', 'Param8', 'HitShift']] == [42371, 15, 3, 9, 8]
    assert [missile[k] for k in ['*ID', 'pSrvDoFunc', 'Range', 'Vel', 'Size', 'CollideType', 'LastCollide', 'NextDelay', 'ResultFlags', 'HitFlags']] == [478, 27, 75, 8, 3, 3, 1, 25, 5, 2]
    assert not any(missile.get(k) for k in ['pSrvHitFunc', 'pSrvDmgFunc', 'Collision', 'SrcDamage', 'SrcMissDmg', 'ToHit', 'CollideKill'])
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = {}, {}
    for relative, name in [
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', 'SKILLS_SrvDo118_Twister_Tornado'),
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', 'sub_6FCFE4C0'),
        ('source/D2Game/src/SKILLS/SkillSor.cpp', 'SKILLS_MissileInit_ChargedBolt'),
        ('source/D2Game/src/MISSILES/MissMode.cpp', 'MISSMODE_SrvDo27_Tornado'),
        ('source/D2Game/src/MISSILES/MissMode.cpp', 'MISSMODE_HandleMissileCollision'),
        ('source/D2Game/src/SKILLS/Skills.cpp', 'sub_6FD10200'),
        ('source/D2Game/src/SKILLS/Skills.cpp', 'sub_6FD10140'),
        ('source/D2Game/src/SKILLS/SkillAss.cpp', 'sub_6FCF5DE0'),
        ('source/D2Common/src/Units/Missile.cpp', 'MISSILE_GetRemainingFrames')]:
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        text = raw.decode()
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end, depth = text.index('{', match.start()) + 1, 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        routines[name] = hashlib.sha256(text[match.start():end].encode()).hexdigest()
    evidence = dict(referenceVersion='3.3.93847 — Reign of the Warlock', primaryDataCommit=manifest['commit'], legacyCommit=tree['sha'],
                    handlerEnabled=False, installed=False, status='Reviewed source/table preflight; no compiled Tornado execution or native handler is claimed here.',
                    pinnedInputHashes=pins, sourceHashes=hashes, reviewedRoutineHashes=routines, skill=skill, missile=missile,
                    skillDescription=next(row for row in tables['skilldesc'].values() if row.get('skilldesc') == 'tornado'),
                    reviewed=[
                        'Do118/shared launcher selects count1 and the Charged Bolt initializer. Current75-frame lifetime remains below the77-frame cap. Reuse actual charged path/seed/retained-step infrastructure only after current-row launch/factory/path composition.',
                        'Do27 resolves periodic delay from positive row Param1 or max(skill calc4,1), then tests elapsed modulo delay before calling generic movement/collision. Current delay15 gives ordinary elapsed0,15,30,45,60 over75body calls. The elapsed75 control is a distinct post-lifetime input, not another ordinary pulse.',
                        'Each periodic event fills one raw packet with null defender, applies row HitFlags2/ResultFlags5, resolves radius from positive row Param2 or max(skill aurarangecalc,1), and dispatches filter42371=0xA583 at request coordinates0,0 around the missile. Complete area geometry/zero-coordinate resolution/filter semantics require separate composition.',
                        'The copied-area callback copies that packet per target. Its shared impact wrapper resolves original owner, calls defense with arguments1/0, handles flags/events and commits. It does not call the generic missile NextHit/LastCollide router or SetDamageFlags for periodic damage.',
                        'Generic collision remains separate: moving Size3 missile, LastCollide1, NextHit1/25frames, GetHit1/KnockBack1, no attack-rating check and no kill-on-contact. Distinguish periodic recipients/defenses/history from direct contacts; do not silently route both through Twister contact leases.',
                        'Physical hard Cyclone Armor+Twister+Hurricane9percent applies before HitShift8. No elemental/weapon source is specified. Capture effective Tornado rank and hard synergies through the existing physical supplier after full creator/getter proof.'
                    ], reuse=['DruidGroundCasting/SkillCombat SC release payment and L/R/quick bindings;10mana and no localdelay.',
                              'ChargedBolts, DruidEmitterPatterns, SourceRandom, SourceMissileSteps and physical/potion/block/recovery consumers where source routes match.',
                              'Existing copied-area/recipient source proofs and common defense consumers, with separate Tornado filter/radius/packet/history acceptance.'],
                    remaining=['Compile complete do27/elapsed/copied-area/impact and generic update composition with owner/row/formula guards, frame order, per-target copies/defenses and direct-history controls.',
                               'Compose current count1/75-frame launch, generic factory, charged path/final seed and damage creator across supported ranks before native actors.',
                               'Implement saved periodic clock and charged flight, distinct direct-history/modern NHD, physical area/direct delivery, learned timing/resources/input, natural timers, lifecycle and actual reloads.',
                               'Exact modern periodic/direct overlap, native source geometry/room/order/RNG/events/current-executable/client and unsupported item/target branches remain explicit acceptance limits.',
                               'Final travelling tornado, cast/contact/periodic/expiry/audio and separate talent/L/R/quick icons remain in existing inventories.'],
                    next='Finish the independently running Volcano regression/installation first; then implement Tornado using its distinct periodic-area path alongside direct contacts.')
    out = ROOT / 'tools/reference/tornado-preflight.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Tornado preflight pinned: periodic area before movement and separate direct contacts; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
