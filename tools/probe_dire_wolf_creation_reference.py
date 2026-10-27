#!/usr/bin/env python3
"""Reuse the existing wolf creation ABI with all current Dire Wolf passives."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    preflight = ROOT / 'tools/reference/dire-wolf-preflight.json'
    current = json.loads(preflight.read_text())
    row = current['currentRows']['skills']['Summon Fenris']
    assert [row['passivestat' + str(i)] for i in range(1, 5)] == ['item_armor_percent', 'tohit', 'damagepercent', 'item_pierce_damage_immunity']
    assert 'passivestat5' not in row
    parent = STAGE / 'spirit-wolf-birth-source-probe.cpp'
    prior = json.loads((ROOT / 'tools/reference/spirit-wolf-creation-source-evidence.json').read_text())
    assert sha(parent) == prior['generatedSourceHashes'][str(parent.relative_to(ROOT))]
    cpp = parent.read_text().split('bool first=true;', 1)[0]
    cpp = cpp.replace('id==227?', 'id==237?', 1)
    old = re.search(r'int dire=0,bear=0,coldMin[^\n]+', cpp)[0]
    cpp = cpp.replace(old, 'int spiritRank=0,grizzlyRank=0,sunder=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*p,int f,int,int){return f==0?4*rank+10*spiritRank:f==1?25*spiritRank:f==2?(grizzlyRank>0?25+10*(grizzlyRank-1):0):f==3?sunder:f>=5&&f<=8?std::min(5*rank,85):f==11?10*rank+50+15*(rank-1):f==20?std::min(rank,3):f==21?p->stats[STAT_LEVEL]:0;}')
    assert 'pet.stats[STAT_ARMORCLASS]=67;pet.stats[20]=32;' in cpp
    cpp = cpp.replace('pet.stats[STAT_ARMORCLASS]=67;pet.stats[20]=32;', 'pet.stats[STAT_ARMORCLASS]=116;pet.stats[20]=54;')
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == current['legacyCommit']
    hashes, routines = {}, {}
    for relative, names in [
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvDo119_DruidSummon']),
        ('source/D2Game/src/SKILLS/SkillNec.cpp', ['D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10', 'D2GAME_SetSummonPassiveStats_6FD0C530']),
        ('source/D2Game/src/MONSTER/MonsterUnique.cpp', ['MONSTERUNIQUE_CalculatePercentage']),
    ]:
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        for name in names:
            body = function(raw.decode(), name)
            assert cpp.count(body) == 1, name
            routines[name] = hashlib.sha256(body.encode()).hexdigest()
    cpp += r'''
bool first=true;
void emit(int level,int r,int diff,int expansion,int s,int b,int inherited,int delta,int failure){
owner={};pet={};skill={};arg={};rank=r;spiritRank=s;grizzlyRank=b;sunder=inherited;owner.stats[STAT_LEVEL]=level;game.nDifficulty=diff;game.nGameType=expansion;game.dwGameType=0;
validSkill=failure!=1;spawnId=failure==2?-1:421;spawnSuccess=failure!=6;hasTarget=failure!=5;spawnCalls=grantedRank=equipmentLevel=0;aiNode=-1;baseLife=216*256+delta;
skill.nPetType=failure==3?-1:failure==4?32:12;skill.nAuraState=0;
skill.nPassiveStat[0]=5;skill.nPassiveStat[1]=STAT_TOHIT;skill.nPassiveStat[2]=6;skill.nPassiveStat[3]=7;skill.nPassiveStat[4]=-1;
for(int i=0;i<5;i++){skill.dwPassiveCalc[i]=i;skill.wSumSkill[i]=-1;}
for(int i=0;i<6;i++){skill.wAuraStat[i]=i<4?12+i:-1;skill.dwAuraStatCalc[i]=5+i;}
int result=SKILLS_SrvDo119_DruidSummon(&game,&owner,237,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",level,r,diff,expansion,s,b,inherited,delta,failure,result);first=false;
for(int i=0;i<21;i++)printf("%s%d",i?",":"",pet.stats[i]);printf("],%d,%d,%d,%d,%d,%d]",equipmentLevel,aiNode,owner.dwFlags,spawnCalls,arg.nMonMode,arg.nPetMax);
}
int main(){
'''
    levels_path = ROOT / 'data/Sanctuary/Balance/summon-level-stats.json'
    levels = json.loads(levels_path.read_text())
    for level in levels:
        for field, member in [('defense', 'dwAC'), ('attack', 'dwTH'), ('expansionDefense', 'dwLAC'), ('expansionAttack', 'dwLTH')]:
            for d, value in enumerate(level[field]):
                cpp += f'tables.pMonLvlTxt[{level["level"]}].{member}[{d}]={value};\n'
    cpp += r'''
printf("[");int pairs[6][2]={{0,0},{1,0},{0,1},{20,20},{28,28},{40,28}};
for(int level:{1,30,99})for(int rank=1;rank<=255;rank++)for(int diff:{0,1,2})for(int expansion:{0,1})for(auto& pair:pairs)for(int delta:{0,17})emit(level,rank,diff,expansion,pair[0],pair[1],0,delta,0);
for(int rank:{1,20,255})for(int diff:{0,1,2})for(int expansion:{0,1})for(int inherited:{1,95})for(int other:{0,28})emit(99,rank,diff,expansion,other,other,inherited,0,0);
for(int failure=1;failure<=6;failure++)emit(99,1,0,1,0,0,0,0,failure);printf("]\n");}
'''
    path = STAGE / 'dire-wolf-creation-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    process = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'dire-wolf-creation-sanitizer.log').write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-3000:]
    cases = json.loads(process.stdout)
    assert len(cases) == 55158
    for level, rank, difficulty, expansion, spirit, grizzly, inherited, delta, failure, success, stats, equipment, node, flags, spawns, mode, cap in cases:
        if failure:
            assert success == 0 and not any(stats) and equipment == 0 and node == -1
            assert flags == (64 if failure in [5, 6] else 0) and spawns == int(failure == 6)
            continue
        base = levels[level]
        life = 216 * 256 + delta
        life += life * (10 * rank + 50 + 15 * (rank - 1)) // 100
        expected = [0] * 21
        expected[0] = level
        expected[1] = 116 + base['expansionDefense' if expansion else 'defense'][difficulty]
        expected[2] = base['expansionAttack' if expansion else 'attack'][difficulty] + 25 * spirit
        expected[3] = expected[4] = life
        expected[5] = 4 * rank + 10 * spirit
        expected[6] = 25 + 10 * (grizzly - 1) if grizzly else 0
        expected[7] = inherited
        expected[12:16] = [min(5 * rank, 85)] * 4
        expected[20] = 54
        assert stats == expected, (level, rank, difficulty, expansion, spirit, grizzly, inherited)
        assert [success, equipment, node, flags, spawns, mode, cap] == [1, min(3 * rank, level), 123, 64, 1, 8, min(rank, 3)]
    fixture = STAGE / 'dire-wolf-creation-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
        preflightSha256=sha(preflight), reusedCreationAbiSha256=sha(parent), levelDataSha256=sha(levels_path), sourceHashes=hashes, unchangedRoutineHashes=routines,
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture), cases=len(cases), successfulBirths=55152, failureBranches=6, generatedSourceSha256=sha(path), sanitizers='ASan+UBSan'),
        established=[
            'Complete do119/base/passive/percentage composition executes55,152successful births and six failures across255ranks,three owner levels/difficulties,two game contexts,independent Spirit/Grizzly ranks,fractional life and inherited-sunder controls.',
            'Current noRatio spawn service supplies216life/116defense/zero initial AR and54FP regeneration. Explicit-level base adds monlvl defense/AR; all four current passives and four capped resistance stats are copied. Life uses10*rank+50+15*(rank-1),capmin(rank,3),source S1mode8 and equipment requestmin(3*rank,owner level).',
            'Spirit Wolf effective rank contributes separate armor/tohit passives; effective Grizzly rank contributes copied damagepercent,zero when absent. Current four-slot physical sunder is transported exactly as supplied,not silently discarded.',
        ], boundedServices=[
            'Spawn/base layout,formula/stat/level/target/AI/equipment services remain explicit. This is not full current monster initialization or native body acceptance. The source initializer flags7 and later attack-mode table statistics are separate.',
            'Live owner-derived physical endpoints/tohit,attack-mode stats,rage,event/geometry/seed ordering,owner/save/input and current executable equivalence remain unfinished. Nonzero inherited physical-sunder delivery is not established by copying that stat.',
        ])
    output = ROOT / 'tools/reference/dire-wolf-creation-engine-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Dire Wolf:55,152complete successful births/six failures pass ASan/UBSan; actual body/attack-mode/learned entry remain separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
