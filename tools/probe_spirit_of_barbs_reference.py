#!/usr/bin/env python3
"""Reuse Heart's bounded creation ABI with Barbs data and unchanged pinned routines."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    preflight = ROOT / 'tools/reference/spirit-of-barbs-preflight.json'
    current = json.loads(preflight.read_text())['currentRows']
    body = current['monstats']['spiritofbarbs']
    assert [body[k] for k in ['*hcIdx', 'minHP', 'AC', 'DamageRegen']] == ['422', '213', '196', '3']
    summon = current['skills']['Spirit of Barbs']
    assert summon['Param1'] == '25' and summon['calc1'] == '(lvl-1)*par1'
    prior = json.loads((ROOT / 'tools/reference/heart-of-wolverine-engine-evidence.json').read_text())
    parent = STAGE / 'heart-of-wolverine-source-probe.cpp'
    assert sha(parent) == prior['generatedSourceSha256']
    cpp = parent.read_text()
    # Only named ABI services and driver inputs change. Independently verify
    # every complete compiled source function below after these substitutions.
    changes = {
        'id==236?': 'id==246?', 'id==297?': 'id==296?',
        'if(s==297)': 'if(s==296)', 'skill.wSumSkill[0]=297;': 'skill.wSumSkill[0]=296;',
        'spawnId=423,': 'spawnId=422,', 'failure==2?-1:423;': 'failure==2?-1:422;',
        'baseLife=34816': 'baseLife=54528',
        'pet.stats[STAT_ARMORCLASS]=123;': 'pet.stats[STAT_ARMORCLASS]=196;',
        'pet.stats[20]=25;': 'pet.stats[20]=39;',
        'SKILLS_SrvDo119_DruidSummon(&game,&owner,236,rank)': 'SKILLS_SrvDo119_DruidSummon(&game,&owner,246,rank)',
    }
    for before, after in changes.items():
        assert before in cpp, before
        cpp = cpp.replace(before, after)
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == prior['legacyCommit']
    sources, routines = {}, {}
    for relative, names in [
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvDo119_DruidSummon']),
        ('source/D2Game/src/SKILLS/SkillNec.cpp', ['D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10', 'D2GAME_SetSummonPassiveStats_6FD0C530']),
        ('source/D2Game/src/MONSTER/MonsterUnique.cpp', ['MONSTERUNIQUE_CalculatePercentage']),
    ]:
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        sources[relative] = hashlib.sha256(raw).hexdigest()
        for name in names:
            original = function(raw.decode(), name)
            assert function(cpp, name) == original, name
            routines[name] = hashlib.sha256(original.encode()).hexdigest()
    path = STAGE / 'spirit-of-barbs-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True, check=True)
    assert not result.stderr, result.stderr.decode()
    cases = json.loads(result.stdout)
    assert len(cases) == 186
    levels_path = ROOT / 'data/Sanctuary/Balance/summon-level-stats.json'
    levels = json.loads(levels_path.read_text())
    for row in cases:
        level, rank, difficulty, expansion, delta, failure, success, pet_level, maximum, life, defense, attack, regen, poison, maxpoison, grant, assigned, equipment, spawns, mode, cap, flags = row
        if failure:
            assert success == 0 and maximum == life == grant == assigned == 0
            assert spawns == (failure == 5) and flags == (64 if failure in [5, 6] else 0)
        else:
            total = 54528 + delta + (54528 + delta) * 25 * (rank - 1) // 100
            stats = levels[level]
            assert row[6:] == [1, level, total, total, 196 + stats['expansionDefense' if expansion else 'defense'][difficulty], stats['expansionAttack' if expansion else 'attack'][difficulty], 39, 100, 100, rank, 1, min(3 * rank, level), 1, 1, 1, 64]
    fixture = STAGE / 'spirit-of-barbs-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
        preflightSha256=sha(preflight), reusedCreationAbiSha256=sha(parent), sourceHashes=sources, unchangedRoutineHashes=routines,
        generatedSourceSha256=sha(path), levelDataSha256=sha(levels_path),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture), cases=len(cases), sanitizers='ASan+UBSan'),
        established=[
            '180 complete unchanged do119/base/passive/percentage compositions and six failure branches pass with Barbs owner level, difficulty/game-context level additions, fractional life, poison passives, aura assignment and equipment request.',
            'The existing Heart creation ABI is reused. Current spawn service supplies213base life,196defense,zeroAR and39FP regeneration. Birth life adds25percent per rank above1; regeneration remains seeded before that multiplier.',
        ], remaining=[
            'Full monster initialization, actual actor/motion, aura stat-list/recipient/flat aggregation, learned cast/payment and native/world/client acceptance are not exercised by this probe.',
            'Formula/stat/level-table/equipment/allocation/target/node services are bounded. Complete modern event/HP/death/global RNG and geometry equivalence remain unverified.',
        ])
    output = ROOT / 'tools/reference/spirit-of-barbs-engine-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Barbs:180 complete source creation compositions and six failures pass ASan/UBSan; no actor or learned handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
