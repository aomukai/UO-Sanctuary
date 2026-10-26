#!/usr/bin/env python3
"""Pin Volcano's current rows and reviewed older source before implementation."""
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
    skill = tables['skills']['244']
    assert skill['skill'] == 'Volcano' and skill['srvdofunc'] == 123 and skill['mana'] == 25 and skill['localdelay'] == 100
    names = ['volcano', 'volcano debris 2', 'volcano small fire', 'volcano overlay fire', 'volcano explosion']
    missiles = {name: tables['missiles'][name] for name in names}
    root, debris, field = [missiles[name] for name in names[:3]]
    assert [root[k] for k in ['*ID', 'pSrvDoFunc', 'Range', 'Param3', 'Param4', 'Param5', 'Collision', 'Size', 'NextDelay']] == [479, 28, 150, 2, 128, 30, 1, 3, 10]
    assert debris['*ID'] == 481 and debris['pSrvHitFunc'] == 51 and debris['Range'] == 16
    assert field['*ID'] == 483 and field['Range'] == 18 and field['LastCollide'] == 1 and field['NextDelay'] == 5 and field['Size'] == 2
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

    for relative, name in [('source/D2Game/src/SKILLS/SkillDruid.cpp', 'SKILLS_SrvDo123_Volcano'),
                           ('source/D2Game/src/SKILLS/Skills.cpp', 'sub_6FD15340'),
                           ('source/D2Game/src/MISSILES/MissMode.cpp', 'MISSMODE_SrvDo28_Volcano'),
                           ('source/D2Game/src/MISSILES/MissMode.cpp', 'MISSMODE_SrvHit51_VolcanoDebris'),
                           ('source/D2Common/src/Units/Missile.cpp', 'MISSILE_GetRemainingFrames')]:
        routine(relative, name)
    output = dict(referenceVersion='3.3.93847 — Reign of the Warlock', primaryDataCommit=manifest['commit'],
                  legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
                  status='Reviewed source/table preflight only; no compiled Volcano behavior or native implementation is claimed.',
                  pinnedInputHashes=pins, sourceHashes=hashes, reviewedRoutineHashes=routines, skill=skill, missiles=missiles,
                  skillDescription=next(r for r in tables['skilldesc'].values() if r.get('skilldesc') == 'volcano'),
                  reviewed=[
                      'Player do123 separately resolves target coordinates and applies the shared missile-size placement gate before drawing one caster random number. A flags1 stationary root request retains skill/rank; successful allocation stores the draw in TargetX and sends the client skill notification. Allocation failure follows the draw.',
                      'Root do28 uses current calc4=2 and aurarangecalc=12 when row delay/range are nonpositive. The remaining-frames helper was independently proven to mean elapsed frames. Strict2<elapsed<128 with even elapsed selects4,6,...126:62ordinary emission opportunities before the150-frame root expires.',
                      'Each eligible root callback resets missile low seed from stored TargetX, draws two inclusive25-way offsets[-12,+12], and stores the second low seed back into TargetX. Preserve this separate emitter memory instead of replacing it with Fissure/Blizzard square sampling or the collision RNG state.',
                      'Debris requests use flags1312, owner/origin/root rank, absolute target offsets, SubMissile1=481 and graphic/lob argument30. Full factory/trajectory/terminal timing is not established merely by the16-frame row.',
                      'Hit51 creates configured HitSubMissile children with inherited owner/origin/skill/rank, including a null-unit terminal invocation. Current inspected server rows name only small-fire483; client overlay480/explosion482 references do not establish extra server damage actors.',
                      'Root479 and small-fire483 both select the Volcano skill. Their collision size, LastCollide and hit-delay inputs differ: root Size3/noLastCollide/10frames; field Size2/LastCollide1/5frames. Damage/factory and exact modern grouping require dedicated proof.',
                      'Current physical synergy is16percent per hard Molten Boulder point; current fire synergy is12percent per hard Fissure and Armageddon point. Reuse the separately verified pre-HitShift physical and post-HitShift elemental arithmetic after complete creator/getter captures.'
                  ], reuse=[
                      'DruidGroundCasting/SkillCombat provide SC ground/live/deleted target handling and once-only release payment; source-specific placement/search/collision and100-frame local delay must remain explicit.',
                      'The source123 placement gate can reuse the Fissure generic gate proof with current Size3 inputs, after compiling the complete Volcano caller.',
                      'Existing source RNG, immutable saved actors, projectile lifetime, physical/fire mitigation, source block/knockback and retained collision steps are candidates for reuse where matched.',
                      'MissileFootprints must gain the real Collision1 Volcano root projection before exporting it; its fail-closed producer inventory currently guards this missing provider. Current small-fire483 has no Collision1 footprint.'
                  ], remaining=[
                      'Compile complete launch/gate/emitter/terminal child bodies and current-row generic factory. Trace graphics30/lob velocity/path/landing and flags1312 before choosing a native trajectory.',
                      'Capture complete root/debris/field physical/fire source stats, source-parent/origin ownership and fresh/captured rank/gear/hard-synergy behavior. Do not infer a damaging or harmless debris packet from presentation.',
                      'Implement saved root/debris/fields, correct collision/history/modern NHD boundaries, damage/defense/knockback, source timing, once-only25mana payment and100-frame local delay with actual input/reload/owner/facet acceptance.',
                      'Native source-size placement/search/room order, current emitter/cast grouping/RNG/event/client behavior and ordinary-client acceptance remain open; no unsupported rule is silently chosen.',
                      'Final volcano_base/volcano_Fire/volcano_rock/lava_pool/explosion, cast/contact/expiry/audio and separate talent/right/quick icons remain in their existing inventories. Identified placeholders may unblock mechanics; source leftskill0.'
                  ], next='Finish the frozen Twister full regression and exact-package installation first. Then compile Volcano source composition and implement its independent vent/debris/lava consumers; keep Arctic Blast/channel and Boulder terminal-current-build gaps individual.')
    text = json.dumps(output, indent=2) + '\n'
    path = ROOT / 'tools/reference/volcano-preflight.json'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Volcano source/table preflight pinned; separate vent/debris/lava routes documented; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
