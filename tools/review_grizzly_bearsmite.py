#!/usr/bin/env python3
"""Record BearSmite source composition and unresolved modern semantics without enabling it."""
import argparse
import hashlib
import json
from pathlib import Path
import re
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
NEWER_COMMIT = '7f6d773f31060e97124241e6bd30d606bcab6d4e'
NEWER_BLOB = '8b7877862bee8c9bdb14324b8ba1006e3bb616f7'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, bodies = {}, {}
    def original(relative, names=()):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        for name in names:
            bodies[name] = function(raw.decode(), name)
        return raw.decode()
    start_name = 'SKILLS_SrvSt32_Conversion_Bash_Stun_Concentrate_BearSmite'
    do_name = 'SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws'
    original('source/D2Game/src/SKILLS/SkillBar.cpp', [start_name])
    original('source/D2Game/src/SKILLS/Skills.cpp', [do_name])
    original('source/D2Game/src/UNIT/SUnitDmg.cpp', ['SUNITDMG_AllocCombat', 'SUNITDMG_DrainItemDurability', 'SUNITDMG_ExecuteEvents'])
    original('source/D2Game/src/AI/AiTactics.cpp', ['AITACTICS_UseSequenceSkill'])
    modes_source = original('source/D2Common/include/D2Monsters.h')
    original('source/D2Game/include/UNIT/SUnitDmg.h')
    modes = {name:int(value,16) for name,value in re.findall(r'(MONMODE_\w+),\s*//0x([0-9A-Fa-f]+)', modes_source)}
    assert [modes[k] for k in ['MONMODE_GETHIT', 'MONMODE_BLOCK', 'MONMODE_DEAD', 'MONMODE_SEQUENCE']] == [3, 6, 12, 14]
    original('source/D2Game/src/MONSTER/MonsterMode.cpp', ['sub_6FC627B0'])
    assert 'nMode == MONMODE_BLOCK || nMode == MONMODE_CAST || nMode == MONMODE_SKILL1' in bodies['sub_6FC627B0']
    start = bodies[start_name]
    assert start.index('SUNITDMG_AllocCombat(') < start.index('pDamage->dwPhysDamage +=')
    assert 'pSkillsTxtRecord->dwCalc[1]' in start and 'dwStunLen' not in start
    drain = bodies['SUNITDMG_DrainItemDurability']
    assert 'SUNITDMG_ExecuteEvents(pGame, pAttacker, pDefender, 0, &damageCopy);' in drain
    execute = bodies['SUNITDMG_ExecuteEvents']
    assert execute.count('SUNITDMG_CalculateTotalDamage(') == 1
    assert 'if (bMissile)\n\t{\n\t\tSUNITDMG_CalculateTotalDamage(' in execute
    assert 'pDamage->dwDmgTotal' in execute and 'pDamage->dwStunLen' in execute
    newer = BASE / 'grizzly-newer-SkillBar.cpp'
    raw = newer.read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == NEWER_BLOB
    assert function(raw.decode(), start_name) == start
    preflight_path = ROOT / 'tools/reference/grizzly-preflight.json'
    preflight = json.loads(preflight_path.read_text())
    row = preflight['currentRows']['skills']['BearSmite']
    assert [row[k] for k in ['*Id', 'srvstfunc', 'srvdofunc', 'SrcDam', 'calc1', 'calc2', '*calc2 desc', 'Param1', 'Param2', 'Param3', 'Param4', 'ResultFlags']] == ['326', '32', '2', '128', 'ln34', 'max(250,ln12)', 'Stun Length', '15', '5', '15', '15', '8']
    assert [preflight['currentRows']['monstats']['druidbear'][k] for k in ['Skill1', 'Sk1mode', 'Sk1lvl']] == ['BearSmite', 'seq_bearsmite', '1']
    observation_path = ROOT / 'tools/reference/grizzly-bearsmite-user-observations.json'
    observation = json.loads(observation_path.read_text())
    assert observation['referenceVersion'] == '3.3.93847' and observation['observed']['verbatim'] == "i've only seen a knockback, no stuns"
    result = dict(userObservation=dict(path=str(observation_path.relative_to(ROOT)),sha256=sha(observation_path)),referenceVersion='3.3.93847', legacyCommit=tree['sha'], scope='Complete-body read/review; not a compiled packet proof or native BearSmite implementation',
        handlerEnabled=False, sourceHashes=hashes, reviewedRoutineHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in bodies.items()},
        preflightSha256=sha(preflight_path), sourceMonsterModes=modes,
        supplementaryStart=dict(commit=NEWER_COMMIT, blob=NEWER_BLOB, path=str(newer.relative_to(ROOT)), sha256=sha(newer),
            url=f'https://github.com/ThePhrozenKeep/D2MOO/blob/{NEWER_COMMIT}/source/D2Game/src/SKILLS/SkillBar.cpp', sameCompleteRoutine=True),
        established=[
            'Actual monster mode constants are GETHIT3,BLOCK6,DEAD12,SEQUENCE14. AI UseSequenceSkill asks for MONMODE_SEQUENCE14. Existing sentry mode14sequence primitives are reuse candidates;12must not be described as BearSmite sequence.',
            'User observation in pinned3.3.93847reports only knockback,no stuns. Keep BearSmite free of an invented stun; this does not measure extra damage or knockback distance.',
            'NPC326is granted at innate rank1,distinct from owner Grizzly rank. Its current calc1ln34gives15damagepercent at1; calc2max(250,ln12)gives250at1and is described as Stun Length; source ResultFlags8is KNOCKBACK. Effective innate NPC bonuses/Battle Command must be composed rather than treating owner rank as326rank.',
            'Complete supplementary start32prepares hit flags/percent/elements,allocates combat,and only then adds calc2<<8to dwPhysDamage. It does not write dwStunLen. Separately pinned newer7f6d773routine is byte-for-byte identical,so that reconstruction does not resolve this semantic mismatch.',
            'Complete do2calls the shared DrainItemDurability wrapper. The complete wrapper copies its allocated packet,checks melee range with monster bonus3,and calls ExecuteEvents with bMissile0. Complete ExecuteEvents recalculates total in its bMissile branch only,commits dwDmgTotal to health and reads dwStunLen separately. Ordinary event/item callbacks remain separate consumers,not proof that this suspicious post-allocation field changes modern health/stun.',
        ], unresolved=[
            'User-observed knockback without stun resolves the immediate stun decision:do not add one. The formula label versus reconstructed physical addition/stale total remains a hidden-packet reference gap;do not invent250extra live damage. Exact modern packet/health timing and knockback distance remain separate.',
            'The prior ordinary-mode service ABI declared BLOCK3instead of6; ordinary compared4/5/7/8cases are unaffected,while unused helper mapping3and unexercised6need correction before extending the full Grizzly mode stream. Keep frozen earlier packages/receipts unchanged; add header-backed controls in a new candidate.',
            'Sequence14clock/entry/start/action/durability/event/tie composition,ordinary versus skill mode-stat timing,block/miss,knockback immunity,actual AI112world and saved cancellation remain to implement and verify. The current body preparation has no automatic movement/combat or learned handler.',
        ])
    path = ROOT / 'tools/reference/grizzly-bearsmite-flow-review.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('BearSmite complete-body review:sequence14/header modes pinned; calc2stun-label/physical-field mismatch remains explicit,not invented.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
