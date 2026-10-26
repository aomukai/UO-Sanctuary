#!/usr/bin/env python3
"""Pin shared weather state setup and distinct Hurricane/Armageddon callbacks."""
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
    for name in ['skills', 'missiles', 'states', 'skilldesc']:
        raw = (primary / f'json/{name}.json').read_bytes()
        pins[name] = hashlib.sha256(raw).hexdigest()
        assert pins[name] == manifest['files'][f'json/{name}.json']['sha256']
        tables[name] = json.loads(raw)
    skills = {name: tables['skills'][str(sid)] for sid, name in [(249, 'Armageddon'), (250, 'Hurricane')]}
    states = {name: next(r for r in tables['states'].values() if r['state'] == name.lower()) for name in skills}
    for name, state_id, callback, mana, radius, delay in [('Hurricane', 144, 145, 30, 9, 20), ('Armageddon', 145, 146, 35, 8, 6)]:
        skill = skills[name]
        assert skill['skill'] == name and skill['srvdofunc'] == 124
        assert [skill[k] for k in ['mana', 'Param1', 'Param2', 'Param3', 'Param4', 'Param7']] == [mana, 250, 0, radius, delay, 50]
        assert [states[name][k] for k in ['*ID', 'srvactivefunc']] == [state_id, callback]
        assert skill['anim'] == 'SC' and not skill.get('localdelay') and not skill.get('intown')
        assert skill.get('leftskill', 0) == 0 and skill['rightskill'] == 1
    assert skills['Hurricane']['aurafilter'] == 42883
    assert skills['Armageddon']['srvmissilea'] == 'armageddoncontrol'
    missiles = {name: tables['missiles'][name] for name in ['armageddoncontrol', 'armageddonfire', 'armageddonrock', 'armageddontail', 'armageddonexplosion']}
    control, fire = missiles['armageddoncontrol'], missiles['armageddonfire']
    assert [control[k] for k in ['*ID', 'pSrvDoFunc', 'pSrvHitFunc', 'Range', 'sHitPar1']] == [577, 1, 56, 25, 4]
    assert control['HitSubMissile1'] == 'armageddonfire' and not control.get('CollideType')
    assert [fire[k] for k in ['*ID', 'Range', 'CollideType', 'Collision', 'Size', 'HitShift', 'DamageRate']] == [623, 37, 8, 1, 2, 2, 41]
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = {}, {}

    def routine(relative, name):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        source = raw.decode()
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', source, re.M)
        assert match, name
        end, depth = source.index('{', match.start()) + 1, 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}')
            end += 1
        routines[name] = hashlib.sha256(source[match.start():end].encode()).hexdigest()

    for relative, names in [
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvDo124_Armageddon_Hurricane', 'SKILLS_SrvDo145_Unused', 'SKILLS_SrvDo146_Unused', 'sub_6FCFE0E0']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['D2GAME_MONSTERS_AiFunction06_6FD14370', 'sub_6FD14400', 'sub_6FD10E50', 'sub_6FD10200', 'sub_6FD10140', 'sub_6FD0FE80', 'sub_6FD0FA00']),
        ('source/D2Game/src/SKILLS/SkillAss.cpp', ['SKILLS_GetProgressiveSkillMissileId', 'sub_6FCF5DE0']),
        ('source/D2Game/src/MISSILES/MissMode.cpp', ['MISSMODE_SrvHit56_ArmageddonControl', 'MISSMODE_HandleMissileCollision'])]:
        for name in names:
            routine(relative, name)
    evidence = dict(referenceVersion='3.3.93847 — Reign of the Warlock', primaryDataCommit=manifest['commit'], legacyCommit=tree['sha'],
                    handlerEnabled=False, installed=False, status='Reviewed source/table preflight only; no weather handler or compiled behavioral proof is claimed.',
                    pinnedInputHashes=pins, sourceHashes=hashes, reviewedRoutineHashes=routines, skills=skills, states=states, missiles=missiles,
                    skillDescriptions={name: next(r for r in tables['skilldesc'].values() if r.get('skilldesc') == name.lower()) for name in skills},
                    reviewed=[
                        'Both learned skills use do124. It validates aura state and matching used skill, draws a uint16 caster seed before allocation, clamps duration to at least1, creates or refreshes the same state, records skill/rank, replaces its active-state event at now+Param4 and replaces the stored seed. Recast timing is restart-from-cast, not an independent stacked aura.',
                        'State rows map Hurricane144 to server callback145 and Armageddon145 to146. The legacy names say Unused, but the state dispatcher reads srvactivefunc and invokes them; do124 alone is not the complete mechanic.',
                        'Hurricane callback rejects absent state/invalid rank/range, removes state in town, then schedules another active event before requiring an outdoor room. It rolls physical and elemental damage fresh at event rank, adds skill flags1/2 and copies one packet through area dispatcher42883=0xA783 at owner coordinates0,0.',
                        'Hurricane radius9, interval20,250base frames+50per hard Cyclone Armor point and hard Twister/Tornado9percent cold synergy come from current rows. Cold length50 requires source cold-length/target difficulty/resistance/mitigation proof. Unlike Tornado0xA583, filter0xA783 requests the source LOS barrier check; both area wrappers omit optional NoAura filtering.',
                        'Armageddon callback validates progressive missile id, current highest learned skill and positive range, reschedules at6frames and requires an outdoor room. It resets low seed from stored skill Param1, stores the next draw modulo1000000, then makes at most5inclusive17x17 offset attempts. A candidate must pass radial barrier, room resolution and mask1 before create+client notification; failed allocation still returns success after a valid site.',
                        'Armageddon duration250+50per hard Fissure/Eruption point and radius8 are distinct from the delayed impact radius4. Control577 has25frames/no unit collision, hit56 copies its captured physical/fire packet at terminal coordinates, adds flags5/2, dispatches an owner-sourced area request at impact coordinates with filter0 (shared default0x8583), and creates fire623. Do not mistake visual rock/tail/explosion rows for additional server damage packets.',
                        'Armageddon physical18percent per hard Volcano point, fire14percent per hard Boulder/Firestorm point and separate ground-fire own row7percent synergy/HitShift2 require complete factory/stat captures. Fire623 has Collision1/Size2/CollideType8/37frames/DamageRate41; reusing ordinary contact fields without this route proof would silently change damage.'
                    ], reuse=[
                        'Existing self SC/cancellation/release payment and right/quick binding infrastructure, with no localdelay and separate30/35mana costs.',
                        'Tornado copied-area/defense/radius/source-roll proofs and shared hostility/mitigation consumers, adding Hurricane LOS/owner origin/cold and live-stat event captures explicitly.',
                        'Saved timed state infrastructure, source RNG and Druid placement/terminal/field infrastructure where callback/factory routes agree; independent weather states may coexist.'
                    ], remaining=[
                        'Compile unchanged shared setup and complete145/146callbacks with allocation/recast/town/state/rank/room/delay/seed/placement controls; verify dispatcher and state removal/event ordering.',
                        'Compile complete cold damage/length and Armageddon physical/fire/control/ground-field factory/stat/terminal captures using current rows, effective rank and hard synergies.',
                        'Implement saved player-bound weather, once-only release payment, natural timers/live stat changes, mitigation/cold, position-following, offline/facet/death/class/town cleanup and actual input/reloads. No handler is enabled by this preflight.',
                        'Exact modern state expiry/event ordering, geometry/LOS/room/list/RNG/global scheduling, cold difficulty/PvP/unmapped/item/event/client behavior remain unsupported or unverified until separately tested.',
                        'Final Hurricane wind/debris/cast/pulse/end audio and Armageddon falling rock/tail/blast/crater/cast/end audio remain effects placeholders; talent/right/quick icons remain separately inventoried.'
                    ], next='Finish Tornado full regression and exact-package installation first; then Hurricane source/native acceptance, followed by Armageddon using the shared state setup and its distinct emitter/impact/field routes.')
    out = ROOT / 'tools/reference/druid-weather-preflight.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Druid weather preflight pinned: shared do124, state callbacks145/146, distinct area/emitter routes; no handlers enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
