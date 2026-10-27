#!/usr/bin/env python3
"""Compile complete pinned do119/base/passive helpers with explicit bounded services."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    preflight = ROOT / 'tools/reference/heart-of-wolverine-preflight.json'
    current = json.loads(preflight.read_text())['currentRows']
    assert current['skills']['Heart of Wolverine']['calc1'] == '(lvl-1)*par1' and current['skills']['Heart of Wolverine']['Param1'] == '25'
    assert [current['monstats']['heartofwolverine'][k] for k in ['minHP', 'AC', 'DamageRegen']] == ['136', '123', '3']
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = {}, {}

    def source(relative):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        return raw.decode()

    def routine(text, name):
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end, depth = text.index('{', match.start()) + 1, 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}'); end += 1
        return text[match.start():end]

    # Reuse the established bounded ABI; independently check every compiled
    # source routine against its Git-blob-verified original, not that adapter.
    cpp = (STAGE / 'valkyrie-creation-source-probe.cpp').read_text().split('int main()', 1)[0]
    cpp = cpp.replace('int dwUnitId=17,dwUnitType=1,stats', 'int dwFlags=0,dwNodeIndex=123,dwUnitId=17,dwUnitType=1,stats', 1)
    cpp = cpp.replace('nStatesTxtRecordCount=100', 'nPetTypeTxtRecordCount=32,nStatesTxtRecordCount=256', 1)
    cpp = cpp.replace('struct D2SkillsTxt{', 'struct D2SkillsTxt{int nPetType=14;unsigned dwPetMax=20;', 1)
    cpp = cpp.replace('dwCalc[1]={11}', 'dwCalc[2]={11,21}', 1).replace('}skill;', '}skill,auraSkill;', 1)
    cpp = cpp.replace('D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return id>=0?&skill:nullptr;}',
        'bool validSkill=true;D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return id==236?(validSkill?&skill:nullptr):id==297?&auraSkill:nullptr;}')
    old = re.search(r'int SKILLS_EvaluateSkillFormula\([^\n]+', cpp)[0]
    cpp = cpp.replace(old, 'int SKILLS_EvaluateSkillFormula(D2UnitStrc*p,int f,int,int){return f==0||f==1?100:f==11?25*(rank-1):f==12?rank:f==20?1:f==21?p->stats[STAT_LEVEL]:0;}')
    cpp = cpp.replace('void D2GAME_SetSkills_6FD14C60(D2UnitStrc*,int s,int n,int){skills[s-100]=n;}',
        'int grantedRank=0;void D2GAME_SetSkills_6FD14C60(D2UnitStrc*,int s,int n,int){if(s==297)grantedRank=n;}')
    cpp = cpp.replace('void D2GAME_AssignSkill_6FD13800(D2UnitStrc*,int,int,int){}',
        'int assigned=0;void D2GAME_AssignSkill_6FD13800(D2UnitStrc*,int,int s,int){if(s==297)assigned++;}')
    for relative, names in [
        ('source/D2Game/src/SKILLS/SkillNec.cpp', ['D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10', 'D2GAME_SetSummonPassiveStats_6FD0C530']),
        ('source/D2Game/src/MONSTER/MonsterUnique.cpp', ['MONSTERUNIQUE_CalculatePercentage'])]:
        text = source(relative)
        for name in names:
            body = routine(text, name)
            assert routine(cpp, name) == body
            routines[name] = hashlib.sha256(body.encode()).hexdigest()
    cpp += r'''
constexpr int AISPECIALSTATE_NONE=0,UNITFLAG_SKSRVDOFUNC=0x40;
struct D2SummonArgStrc{int nPetType=0,dwFlags=0,nHcIdx=0,nAiSpecialState=0,nMonMode=0,nPetMax=0;D2UnitStrc*pOwner=nullptr;}arg;
int spawnId=423,spawnCalls=0,aiNode=-1,baseLife=34816;bool spawnSuccess=true,targetSuccess=true;
int D2GAME_GetSummonIdFromSkill_6FD15580(D2UnitStrc*,int,int,int,int*mode,int,int){*mode=1;return spawnId;}
bool D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int*x,int*y){*x=40;*y=70;return targetSuccess;}
D2UnitStrc* D2GAME_SummonPet_6FD14430(D2GameStrc*,D2SummonArgStrc*p){spawnCalls++;arg=*p;if(!spawnSuccess)return nullptr;pet.stats[STAT_MAXHP]=baseLife;pet.stats[STAT_HITPOINTS]=baseLife/2;pet.stats[STAT_ARMORCLASS]=123;pet.stats[20]=25;return &pet;}
void D2GAME_UpdateSummonAI_6FC401F0(D2GameStrc*,D2UnitStrc*,int,int n){aiNode=n;}
'''
    body = routine(source('source/D2Game/src/SKILLS/SkillDruid.cpp'), 'SKILLS_SrvDo119_DruidSummon')
    routines['SKILLS_SrvDo119_DruidSummon'] = hashlib.sha256(body.encode()).hexdigest()
    cpp += body + '\nint main(){\n'
    levels_path = ROOT / 'data/Sanctuary/Balance/summon-level-stats.json'
    levels = json.loads(levels_path.read_text())
    for row in levels:
        for key, member in [('defense', 'dwAC'), ('attack', 'dwTH'), ('expansionDefense', 'dwLAC'), ('expansionAttack', 'dwLTH')]:
            for i, value in enumerate(row[key]):
                cpp += f'tables.pMonLvlTxt[{row["level"]}].{member}[{i}]={value};\n'
    cpp += r'''
bool first=true;printf("[");
for(int failure=0;failure<=6;failure++)for(int level:{1,30,99})for(int r:{1,2,20,21,255})for(int difficulty:{0,1,2})for(int expansion:{0,1})for(int delta:{0,17}){
if(failure && (level!=99||r!=1||difficulty||!expansion||delta))continue;
owner={};pet={};skill={};auraSkill={};arg={};rank=r;owner.stats[STAT_LEVEL]=level;game.nDifficulty=difficulty;game.nGameType=expansion;game.dwGameType=0;
validSkill=failure!=1;spawnId=failure==2?-1:423;spawnSuccess=failure!=5;targetSuccess=failure!=6;spawnCalls=grantedRank=assigned=equipmentLevel=0;aiNode=-1;baseLife=34816+delta;
for(int&x:skill.nPassiveStat)x=-1;skill.nPassiveStat[0]=7;skill.nPassiveStat[1]=8;skill.dwPassiveCalc[0]=0;skill.dwPassiveCalc[1]=1;
for(int&x:skill.wAuraStat)x=-1;for(int&x:skill.wSumSkill)x=-1;skill.wSumSkill[0]=297;skill.dwSumSkCalc[0]=12;skill.nPetType=failure==3?-1:failure==4?32:14;auraSkill.dwFlags[0]=1;
int result=SKILLS_SrvDo119_DruidSummon(&game,&owner,236,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",level,r,difficulty,expansion,delta,failure,result,pet.stats[STAT_LEVEL],pet.stats[STAT_MAXHP],pet.stats[STAT_HITPOINTS],pet.stats[STAT_ARMORCLASS],pet.stats[STAT_TOHIT],pet.stats[20],pet.stats[7],pet.stats[8],grantedRank,assigned,equipmentLevel,spawnCalls,arg.nMonMode,arg.nPetMax,owner.dwFlags);first=false;
}printf("]\n");}
'''
    path = STAGE / 'heart-of-wolverine-source-probe.cpp'; path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True, check=True)
    assert not result.stderr, result.stderr.decode()
    cases = json.loads(result.stdout)
    assert len(cases) == 186
    for level, rank, difficulty, expansion, delta, failure, result, pet_level, maximum, life, defense, attack, regen, poison, maxpoison, grant, assigned, equipment, spawn_calls, mode, cap, flags in cases:
        if failure:
            assert result == 0 and maximum == life == grant == assigned == 0
            assert spawn_calls == (failure == 5) and flags == (64 if failure in [5, 6] else 0)
        else:
            total = 34816 + delta + (34816 + delta) * 25 * (rank - 1) // 100
            row = levels[level]
            assert [result, pet_level, maximum, life, defense, attack, regen, poison, maxpoison, grant, assigned, equipment, spawn_calls, mode, cap, flags] == [1, level, total, total, 123 + row['expansionDefense' if expansion else 'defense'][difficulty], row['expansionAttack' if expansion else 'attack'][difficulty], 25, 100, 100, rank, 1, min(3 * rank, level), 1, 1, 1, 64]
    fixture = STAGE / 'heart-of-wolverine-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
        preflightSha256=hashlib.sha256(preflight.read_bytes()).hexdigest(), sourceHashes=hashes, unchangedRoutineHashes=routines,
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(cases)),
        generatedSourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
        levelDataSha256=hashlib.sha256(levels_path.read_bytes()).hexdigest(),
        established=[
            '180complete do119/base/passive/percentage compositions and6failure branches execute unchanged older routines with ASan/UBSan. Explicit owner level, difficulty/game-context monlvl addition, fractional birth life, two poison passives, aura grant/right assignment and equipment request are compared.',
            'Do119fails for missing row,negative summon,invalid pet category,missing target coordinates or failed allocation. The latter two paths set the server-do flag before failure. This is an internal route comparison, not a tested learned release or resource rollback.'
        ], boundedServices=[
            'Spawn supplies current noRatio136life/123defense/zero AR and25FP regeneration; this probe does not compile the full monster initializer. Fractional+17FP is an independent percentage control.',
            'Formula callbacks use the pinned current row; stat and level-table layout is bounded; equipment creation,node/AI binding,target coordinates and aura grant dispatch are recorded services. No aura recipient, stat lease or world entity is instantiated.',
            'The complete older passive loop has five slots; current Heart has two, both covered. Current executable event/RNG/geometry/timing equivalence is unverified.'
        ])
    output = ROOT / 'tools/reference/heart-of-wolverine-engine-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Heart of Wolverine:180complete creation compositions and6failure branches pass ASan/UBSan; learned actor/aura remain disabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
