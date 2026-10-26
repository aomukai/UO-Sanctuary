#!/usr/bin/env python3
"""Execute complete Spirit Wolf cold getters and do119/base/passive creation."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
REF = ROOT / 'tools/reference/d2r-named-equipment'


def build(check=False):
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, blocks, generated, pins = {}, {}, {}, {}

    def routine(relative, name):
        raw = (BASE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest(); source = raw.decode()
        match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', source, re.M); assert match, name
        end, depth = source.index('{', match.start()) + 1, 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}'); end += 1
        body = source[match.start():end]; blocks[name] = hashlib.sha256(body.encode()).hexdigest()
        return body

    def verify(cpp, relative, names):
        for name in names:
            body = routine(relative, name)
            assert cpp.count(body) == 1, name

    def run(stem, cpp):
        path = BASE / (stem + '-source-probe.cpp'); path.write_text(cpp); binary = path.with_suffix('')
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
        result = subprocess.run([str(binary)], capture_output=True)
        (BASE / (stem + '-sanitizer.log')).write_bytes(result.stderr)
        assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-4000:]
        generated[str(path.relative_to(ROOT))] = hashlib.sha256(path.read_bytes()).hexdigest()
        return json.loads(result.stdout)

    manifest = json.loads((REF / 'sources.json').read_text()); tables = {}
    for name in ['skills', 'monstats', 'monlvl', 'itemstatcost']:
        path = REF / 'json' / (name + '.json'); pins[name] = hashlib.sha256(path.read_bytes()).hexdigest()
        assert pins[name] == manifest['files']['json/' + name + '.json']['sha256']; tables[name] = json.loads(path.read_text())
    row = tables['skills']['227']; bear = tables['skills']['247']; dire = tables['skills']['237']
    assert row['srvdofunc'] == 119 and row['EType'] == 'cold' and row['EDmgSymPerCalc'] == "skill('Summon Grizzly'.ln12)"
    assert [bear['Param1'], bear['Param2'], dire['Param2'], row['Param7'], row['Param8']] == [25, 10, 15, 10, 5]
    assert [row['passivecalc' + str(i)] for i in range(1, 6)] == ['(par4 * lvl) + ln56', 'ln12', 'edmn', 'edmx', 'edln']
    assert row['calc1'] == "(par7 * lvl) + skill('Summon Fenris'.par2) * skill('Summon Fenris'.lvl)" and row['calc2'] == 'ulvl'
    assert row['passivestat6'] == 'item_pierce_cold_immunity' and row['passivecalc6'] == "stat('item_pierce_cold_immunity'.accr)"
    for name in [row['passivestat' + str(i)] for i in range(1, 7)] + [row['aurastat' + str(i)] for i in range(1, 5)]:
        assert not any(v for k, v in tables['itemstatcost'][name].items() if k.lower().startswith('itemevent'))
    cpp = (BASE / 'poison-javelin-damage-source-probe.cpp').read_text().split('int main()', 1)[0]
    old = 'int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return hard*12;}'
    assert cpp.count(old) == 1
    cpp = cpp.replace(old, 'int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int f,int,int){return f==2?25:hard>0?25+(hard-1)*10:0;}')
    verify(cpp, 'source/D2Common/src/D2Skills.cpp', ['SKILLS_CalculateDamageBonusByLevel', 'SKILLS_CalculateMasteryBonus', 'SKILLS_GetMinElemDamage', 'SKILLS_GetMaxElemDamage', 'SKILLS_GetElementalLength'])
    cpp += '''
int main(){printf("[");bool first=true;
for(int rank=1;rank<=255;rank++)for(int bear:{0,1,20,28,40})for(int mastery:{-50,0,100})for(int applied:{0,1}){
owner={};skill={};hard=bear;skill.nEType=ELEMTYPE_COLD;skill.nToHitShift=8;skill.dwEDmgSymPerCalc=1;skill.dwELenSymPerCalc=2;
'''
    for field in ['EMin', 'EMax', 'ELen']:
        cpp += f'skill.dw{field}={row[field]};\n'
    for i in range(1, 6):
        cpp += f'skill.dwEMinLev[{i-1}]={row["EMinLev"+str(i)]};skill.dwEMaxLev[{i-1}]={row["EMaxLev"+str(i)]};\n'
    for i in range(1, 4):
        cpp += f'skill.dwELevLen[{i-1}]={row["ELevLen"+str(i)]};\n'
    cpp += r'''
owner.stats[STAT_PASSIVE_COLD_MASTERY]=mastery;
int low=SKILLS_GetMinElemDamage(&owner,227,rank,applied),high=SKILLS_GetMaxElemDamage(&owner,227,rank,applied),length=SKILLS_GetElementalLength(&owner,227,rank,applied);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,bear,mastery,applied,low,high,length,low>>8,high>>8);first=false;
}printf("]\n");}
'''
    cold = run('spirit-wolf-cold', cpp); assert len(cold) == 7650
    cold_map = {}
    for rank, bear_rank, mastery, applied, minimum, maximum, frames, whole_min, whole_max in cold:
        expected = []
        for field in ['EMin', 'EMax']:
            value, left = row[field], rank - 1
            for i, width in enumerate([7, 8, 6, 6, 2**30], 1):
                amount = min(left, width); value += amount * row[field + 'Lev' + str(i)]; left -= amount
            value <<= row['HitShift']; value += value * (25 + (bear_rank - 1) * 10 if bear_rank > 0 else 0) // 100
            if applied:
                value += abs(value * mastery) // 100 * (-1 if mastery < 0 else 1)
            expected.append(value)
        assert [minimum, maximum] == expected and [whole_min, whole_max] == [v >> 8 for v in expected]
        assert frames == (25 * rank * 125 // 100)
        if applied == 0 and mastery == 0:
            cold_map[rank, bear_rank] = (whole_min, whole_max, frames)
    # Reuse the full creation ABI, retaining the exact old five-slot helper.
    cpp = (BASE / 'creeper-birth-source-probe.cpp').read_text().split('bool first=true;', 1)[0]
    cpp = cpp.replace('id==222?', 'id==227?', 1)
    old = re.search(r'int SKILLS_EvaluateSkillFormula\([^\n]+', cpp)[0]
    cpp = cpp.replace(old, 'int dire=0,bear=0,coldMin[256][41]={},coldMax[256][41]={},coldFrames[256][41]={};int SKILLS_EvaluateSkillFormula(D2UnitStrc*p,int f,int,int){return f==0?7*rank+50+(rank-1)*10:f==1?50+(rank-1)*25:f==2?coldMin[rank][bear]:f==3?coldMax[rank][bear]:f==4?coldFrames[rank][bear]:f>=5&&f<=8?std::min(5*rank,85):f==11?10*rank+15*dire:f==20?std::min(rank,5):f==21?p->stats[STAT_LEVEL]:0;}')
    cpp = cpp.replace('*mode=99;', '*mode=8;', 1)
    cpp = cpp.replace('pet.stats[STAT_ARMORCLASS]=25;pet.stats[20]=9;', 'pet.stats[STAT_ARMORCLASS]=67;pet.stats[20]=32;', 1)
    verify(cpp, 'source/D2Game/src/SKILLS/SkillNec.cpp', ['D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10', 'D2GAME_SetSummonPassiveStats_6FD0C530'])
    verify(cpp, 'source/D2Game/src/MONSTER/MonsterUnique.cpp', ['MONSTERUNIQUE_CalculatePercentage'])
    cpp += r'''
bool hasTarget=true;
int D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int*x,int*y){*x=40;*y=70;return hasTarget;}
'''
    cpp += routine('source/D2Game/src/SKILLS/SkillDruid.cpp', 'SKILLS_SrvDo119_DruidSummon') + r'''
bool first=true;
void emit(int level,int r,int diff,int expansion,int d,int b,int delta,int failure){
owner={};pet={};skill={};arg={};rank=r;dire=d;bear=b;owner.stats[STAT_LEVEL]=level;game.nDifficulty=diff;game.nGameType=expansion;game.dwGameType=0;
validSkill=failure!=1;spawnId=failure==2?-1:420;spawnSuccess=failure!=6;hasTarget=failure!=5;spawnCalls=grantedRank=equipmentLevel=0;aiNode=-1;baseLife=130*256+delta;
skill.nPetType=failure==3?-1:failure==4?32:11;skill.nAuraState=0;
skill.nPassiveStat[0]=5;skill.nPassiveStat[1]=STAT_TOHIT;skill.nPassiveStat[2]=6;skill.nPassiveStat[3]=7;skill.nPassiveStat[4]=8;
for(int i=0;i<5;i++){skill.dwPassiveCalc[i]=i;skill.wSumSkill[i]=-1;}
for(int i=0;i<6;i++){skill.wAuraStat[i]=i<4?12+i:-1;skill.dwAuraStatCalc[i]=5+i;}
int result=SKILLS_SrvDo119_DruidSummon(&game,&owner,227,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",level,r,diff,expansion,d,b,delta,failure,result);first=false;
for(int i=0;i<21;i++)printf("%s%d",i?",":"",pet.stats[i]);printf("],%d,%d,%d,%d,%d,%d]",equipmentLevel,aiNode,owner.dwFlags,spawnCalls,arg.nMonMode,arg.nPetMax);
}
int main(){
'''
    for level in range(111):
        row_level = tables['monlvl'][str(level)]
        for member, field in [('dwAC', 'AC'), ('dwTH', 'TH'), ('dwLAC', 'L-AC'), ('dwLTH', 'L-TH')]:
            for difficulty, suffix in enumerate(['', '(N)', '(H)']):
                cpp += f'tables.pMonLvlTxt[{level}].{member}[{difficulty}]={row_level[field+suffix]};\n'
    for (rank, bear_rank), (minimum, maximum, frames) in cold_map.items():
        cpp += f'coldMin[{rank}][{bear_rank}]={minimum};coldMax[{rank}][{bear_rank}]={maximum};coldFrames[{rank}][{bear_rank}]={frames};\n'
    cpp += r'''
printf("[");for(int level:{1,30,99})for(int rank=1;rank<=255;rank++)for(int diff:{0,1,2})for(int expansion:{0,1})for(int synergy:{0,1,20,28,40})for(int delta:{0,17})emit(level,rank,diff,expansion,synergy,synergy,delta,0);
for(int failure=1;failure<=6;failure++)emit(99,1,0,1,0,0,0,failure);printf("]\n");}
'''
    birth = run('spirit-wolf-birth', cpp); assert len(birth) == 45906
    for level, rank, difficulty, expansion, dire_rank, bear_rank, delta, failure, result, stats, equipment, node, flags, spawns, mode, cap in birth:
        if failure:
            assert result == 0 and not any(stats) and equipment == 0 and node == -1
            assert flags == (64 if failure in [5, 6] else 0) and spawns == int(failure == 6)
            continue
        level_row = tables['monlvl'][str(level)]; suffix = ['', '(N)', '(H)'][difficulty]
        life = 130 * 256 + delta; life += life * (10 * rank + 15 * dire_rank) // 100
        expected = [0] * 21; expected[0] = level
        expected[1] = 67 + level_row[('L-AC' if expansion else 'AC') + suffix]
        expected[2] = level_row[('L-TH' if expansion else 'TH') + suffix] + 50 + 25 * (rank - 1)
        expected[3] = expected[4] = life; expected[5] = 7 * rank + 50 + 10 * (rank - 1)
        expected[6:9] = cold_map[rank, bear_rank]; expected[12:16] = [min(5 * rank, 85)] * 4; expected[20] = 32
        assert stats == expected
        assert [result, equipment, node, flags, spawns, mode, cap] == [1, min(3 * rank, level), 123, 64, 1, 8, min(rank, 5)]
    assert cold_map[28, 28][:2] == (343, 359)
    assert 130 * (100 + 10 * 28 + 15 * 28) // 100 == 1040
    assert row['mana'] == 15 and row['Param3'] == 5
    assert row['Param1'] + 27 * row['Param2'] == 725 and row['Param5'] + 27 * row['Param6'] == 320
    fixture = BASE / 'spirit-wolf-creation-source-probe.json'
    fixture.write_text(json.dumps(dict(cold=cold, birth=birth), separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
                    sourceHashes=hashes, unchangedRoutineHashes=blocks, pinnedTableHashes=pins, generatedSourceHashes=generated,
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cold=7650, birth=45906),
                    userObservation=dict(date='2026-10-27', referenceVersion='3.3.93847', spiritEffectiveRank=28, spiritHardRank=20,
                                         direEffectiveRank=28, direHardRank=20, grizzlyEffectiveRank=28, grizzlyHardRank=20,
                                         mana=15, life=1040, coldMinimum=343, coldMaximum=359, maximumWolves=5,
                                         tooltipAttackRatingPercent=725, tooltipDefensePercent=320,
                                         scope='User tooltip observation matches life, cold, mana, cap and displayed linear bonus formulas. It does not establish runtime AR/defense consumers. The copied armor passive also contains the separate7*rank term.'),
                    sanitizers='Both ASan/UBSan binaries exit0 with empty runtime stderr.',
                    established=[
                        'Complete cold getters use effective Grizzly ln12, zero when absent, and return unmastered edmn/edmx whole-life values for the passive copy. Independent applied-mastery controls distinguish a4=0 from1. Current25percent ELenSym produces31frames at rank1. Getter round-down before passive storage is retained.',
                        'Complete do119/base/passive composition uses explicit owner level, difficulty/game-context monlvl additions, current effective Dire life bonus, first five additive passives and four capped resistance auras. HP starts from supplied130base plus optional fractional control, is scaled and filled; supplied pre-passive regen32 is preserved. All255ranks and independent owner/difficulty/game/synergy contexts execute.',
                        'Missing row/summon, invalid category, missing target and failed allocation have distinct return/owner-flag/allocation boundaries. Successful calls request source S1mode8, capmin(rank,5), AI owner node and clamped3*rank equipment dispatch. No actual equipment is inferred.'
                    ], limits=[
                        'Formula/table/stat/target/AI/allocation services are declared. Cold formula service uses the separately executed full getter capture, not a full VM. Spawn HP130/defense67/AR0/regen32 are supplied noRatio initialization controls, not proof of actual current monster initialization.',
                        'The old passive helper covers only five slots. Current slot6 cold sunder is data only and requires a separate native/consumer policy. Raw defense plus armor-percent and raw AR plus additive tohit are captured, not the complete downstream defense/attack/damage consumers.',
                        'Native Spirit Wolf actor, movement/AI/teleport, captured/live stat timing, current executable equivalence, ordinary attack/cold delivery, input/payment/cap/lifecycle/persistence and final effects/icons remain pending. No learned handler is enabled.'
                    ])
    out = ROOT / 'tools/reference/spirit-wolf-creation-source-evidence.json'; encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == encoded
    else:
        out.write_text(encoded)
    print('Spirit Wolf:7650 complete cold getter and45906 complete do119/base/passive cases; sixth slot and actual actor remain separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
