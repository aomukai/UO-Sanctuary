#!/usr/bin/env python3
"""Compile Creeper's complete source damage/creation helpers with declared services."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
REF = ROOT / 'tools/reference/d2r-named-equipment'


def build(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines, generated, pins = {}, {}, {}, {}

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
        body = text[match.start():end]
        return body

    def verify(cpp, relative, names):
        original = source(relative)
        for name in names:
            body = routine(original, name)
            assert routine(cpp, name) == body, name
            routines[name] = hashlib.sha256(body.encode()).hexdigest()

    def run(stem, cpp):
        path = STAGE / (stem + '-source-probe.cpp'); path.write_text(cpp)
        binary = path.with_suffix('')
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
        result = subprocess.run([str(binary)], capture_output=True)
        (STAGE / (stem + '-sanitizer.log')).write_bytes(result.stderr)
        assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-3000:]
        generated[str(path.relative_to(ROOT))] = hashlib.sha256(path.read_bytes()).hexdigest()
        return json.loads(result.stdout)

    tables = {}
    manifest = json.loads((REF / 'sources.json').read_text())
    for name in ['skills', 'missiles', 'monstats', 'monstats2', 'pettype', 'itemstatcost']:
        path = REF / 'json' / (name + '.json')
        pins[name] = hashlib.sha256(path.read_bytes()).hexdigest()
        assert pins[name] == manifest['files']['json/' + name + '.json']['sha256']
        tables[name] = json.loads(path.read_text())
    skill = tables['skills']['222']; monster = tables['monstats']['plaguepoppy']
    assert skill['EDmgSymPerCalc'] == "skill('Rabies'.blvl)*par8" and skill['Param8'] == 10
    assert monster['*hcIdx'] == 425 and monster['Align'] == monster['noRatio'] == 1
    assert not any(v for k, v in tables['itemstatcost']['item_pierce_poison_immunity'].items() if k.lower().startswith('itemevent'))
    trail = tables['missiles']['plague vines trail']
    assert trail['Skill'] == 'Plague Poppy' and not any(trail.get(k) for k in ['MissileSkill', 'SrcDamage', 'SrcMissDmg'])
    # The complete poison creator/getter/writer harness is already independently
    # pinned. Alter only the ABI/formula/table service and scenario driver.
    cpp = (STAGE / 'poison-javelin-damage-source-probe.cpp').read_text().split('int main()', 1)[0]
    cpp = cpp.replace('}skill;', '}skill,actorSkill;', 1)
    cpp = cpp.replace('D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){return &skill;}',
                      'int selectedSkill=0;D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){selectedSkill=id;return id==222?&skill:id==294?&actorSkill:nullptr;}')
    assert 'return hard*12;' in cpp
    cpp = cpp.replace('return hard*12;', 'return hard*10;', 1)
    verify(cpp, 'source/D2Common/src/Units/Missile.cpp', ['MISSILE_AddStatsToDamage', 'MISSILE_CalculateDamageData', 'MISSILE_CalculateFinalDamage', 'MISSILE_SetDamageStats'])
    verify(cpp, 'source/D2Common/src/D2Skills.cpp', ['SKILLS_CalculateDamageBonusByLevel', 'SKILLS_CalculateMasteryBonus', 'SKILLS_GetMinElemDamage', 'SKILLS_GetMaxElemDamage', 'SKILLS_GetElementalLength'])
    fields = json.loads((STAGE / 'bow-launch-source-probe.json').read_text())['fields']
    cpp += r'''
bool first=true;
void emit(int rank,int points,int mastery,int noise,int hasOrigin,int overrideSkill){
 owner={};weapon={};missile={};skill={};actorSkill={};row={};missileData={};D2UnitStrc origin;
 owner.dwUnitType=UNIT_MONSTER;owner.pInventory=nullptr;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=475;missileData.nSkill=294;missileData.nLevel=rank;
 row.nSkill=222;row.nSrcDamage=row.nSrcMissDmg=0;row.dwMissileFlags=overrideSkill?gdwBitMasks[MISSILESFLAGINDEX_MISSILESKILL]:0;
 skill.nSrcDam=0;skill.nEType=ELEMTYPE_POIS;skill.dwELen=100;for(int&v:skill.dwELevLen)v=0;
 actorSkill.nSrcDam=0;actorSkill.nEType=ELEMTYPE_NONE;actorSkill.dwEMin=actorSkill.dwEMax=actorSkill.dwELen=0;for(int&v:actorSkill.dwEMinLev)v=0;for(int&v:actorSkill.dwEMaxLev)v=0;for(int&v:actorSkill.dwELevLen)v=0;
'''
    for field, name in [('dwEMin', 'EMin'), ('dwEMax', 'EMax'), ('nToHitShift', 'HitShift')]:
        cpp += f'skill.{field}={skill[name]};\n'
    for i in range(5):
        cpp += f'skill.dwEMinLev[{i}]={skill["EMinLev"+str(i+1)]};skill.dwEMaxLev[{i}]={skill["EMaxLev"+str(i+1)]};\n'
    cpp += r'''
 hard=points;critical=true;critCalls=0;skillMin=skillMax=0;selectedSkill=0;
 for(int&v:owner.stats)v=noise;for(int&v:origin.stats)v=777;for(int&v:weapon.stats)v=999;
 owner.stats[STAT_PASSIVE_POIS_MASTERY]=mastery;
 D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,hasOrigin?&origin:nullptr,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&d,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,[",first?"":",",rank,points,mastery,noise,hasOrigin,overrideSkill);first=false;
'''
    for i, field in enumerate(fields):
        cpp += f'printf("{"," if i else ""}%d",d.{field});\n'
    cpp += r'''
 printf("],%d,%d,%d,%d,%d,%d]",missile.stats[STAT_POISONMINDAM],missile.stats[STAT_POISONLENGTH],missile.stats[STAT_POISON_COUNT],missile.stats[STAT_PASSIVE_POIS_MASTERY],critCalls,selectedSkill);
}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");
for(int rank:{1,3,8,9,16,17,20,22,23,28,40,255})for(int hard:{0,1,20})for(int mastery:{-50,0,25,100})for(int noise:{0,99999})for(int origin:{0,1})for(int over:{0,1})emit(rank,hard,mastery,noise,origin,over);
for(int rank=1;rank<=255;rank++)for(int hard:{0,1,20})for(int mastery:{-50,0,25,100})for(int over:{0,1})emit(rank,hard,mastery,0,1,over);
printf("]\n");}
'''
    damage = run('creeper-damage', cpp)
    assert len(damage) == 7272
    def percentage(value, percent):
        return abs(value * percent) // 100 * (-1 if value * percent < 0 else 1)
    for rank, hard, mastery, noise, origin, over, values, rate, length, count, stored_mastery, critical, selected in damage:
        expected = []
        for lane in ['EMin', 'EMax']:
            value, remaining = skill[lane], rank - 1
            for i, width in enumerate([7, 8, 6, 6, 2**30], 1):
                amount = min(remaining, width); value += amount * skill[lane + 'Lev' + str(i)]; remaining -= amount
            value <<= skill['HitShift']; value += percentage(value, hard * 10); value += percentage(value, mastery)
            expected.append(0 if over else value)
        actual = dict(zip(fields, values))
        allowed = ['nPoisonMinDamage', 'nPoisonMaxDamage', 'nPoisonLength']
        assert [actual[k] for k in allowed] == expected + [0 if over else 100]
        assert all(v == 0 for k, v in actual.items() if k not in allowed)
        assert (rate, length, count, stored_mastery, critical, selected) == (expected[0], 0 if over else 100, 0, 0, 0, 294 if over else 222)
    # Reuse the complete base/passive ABI. The explicit-level base helper is
    # present but intentionally unused: do115 never calls it.
    cpp = (STAGE / 'valkyrie-creation-source-probe.cpp').read_text().split('int main()', 1)[0]
    cpp = cpp.replace('int dwUnitId=17,dwUnitType=1,stats', 'int dwFlags=0,dwNodeIndex=123,dwUnitId=17,dwUnitType=1,stats', 1)
    cpp = cpp.replace('nStatesTxtRecordCount=100', 'nPetTypeTxtRecordCount=32,nStatesTxtRecordCount=256', 1)
    cpp = cpp.replace('struct D2SkillsTxt{', 'struct D2SkillsTxt{int nPetType=14;unsigned dwPetMax=20;int dwCalcExtra=0;', 1)
    cpp = cpp.replace('dwCalc[1]={11}', 'dwCalc[2]={11,21}', 1)
    cpp = cpp.replace('}skill;', '}skill,actorSkill;', 1)
    cpp = cpp.replace('D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return id>=0?&skill:nullptr;}',
                      'bool validSkill=true;D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return id==222?(validSkill?&skill:nullptr):id==294?&actorSkill:nullptr;}')
    old = re.search(r'int SKILLS_EvaluateSkillFormula\([^\n]+', cpp)[0]
    cpp = cpp.replace(old, 'int SKILLS_EvaluateSkillFormula(D2UnitStrc*p,int f,int,int){return f==0?pierce:f==11?50*(rank-1):f==12?rank:f==20?1:f==21?p->stats[STAT_LEVEL]*3/4+rank:0;}')
    cpp = cpp.replace('void STATES_ToggleState(D2UnitStrc*,int,int){}', 'int stateCalls=0;void STATES_ToggleState(D2UnitStrc*,int s,int on){if(s==150&&on==1)stateCalls++;}')
    cpp = cpp.replace('void D2GAME_SetSkills_6FD14C60(D2UnitStrc*,int s,int n,int){skills[s-100]=n;}',
                      'int grantedRank=0;void D2GAME_SetSkills_6FD14C60(D2UnitStrc*,int s,int n,int){if(s==294)grantedRank=n;}')
    verify(cpp, 'source/D2Game/src/SKILLS/SkillNec.cpp', ['D2GAME_SetSummonPassiveStats_6FD0C530'])
    verify(cpp, 'source/D2Game/src/MONSTER/MonsterUnique.cpp', ['MONSTERUNIQUE_CalculatePercentage'])
    cpp += r'''
constexpr int AISPECIALSTATE_NONE=0,UNITFLAG_SKSRVDOFUNC=0x40,STATE_VINE_BEAST=150;
struct D2SummonArgStrc{int nPetType=0,dwFlags=0,nHcIdx=0,nAiSpecialState=0,nMonMode=0,nPetMax=0;D2UnitStrc*pOwner=nullptr;}arg;
int spawnId=425,spawnCalls=0,aiNode=-1,baseLife=12800;bool spawnSuccess=true;
int D2GAME_GetSummonIdFromSkill_6FD15580(D2UnitStrc*,int,int,int,int*mode,int,int){*mode=99;return spawnId;}
D2UnitStrc* D2GAME_SummonPet_6FD14430(D2GameStrc*,D2SummonArgStrc*p){spawnCalls++;arg=*p;if(!spawnSuccess)return nullptr;pet.stats[STAT_MAXHP]=baseLife;pet.stats[STAT_HITPOINTS]=baseLife/2;pet.stats[STAT_ARMORCLASS]=25;pet.stats[20]=9;return &pet;}
void D2GAME_UpdateSummonAI_6FC401F0(D2GameStrc*,D2UnitStrc*,int,int n){aiNode=n;}
'''
    body = routine(source('source/D2Game/src/SKILLS/SkillDruid.cpp'), 'SKILLS_SrvDo115_Vines')
    routines['SKILLS_SrvDo115_Vines'] = hashlib.sha256(body.encode()).hexdigest()
    cpp += body + r'''
bool first=true;
void emit(int level,int r,int difficulty,int pi,int delta,int failure){
 owner={};pet={};skill={};actorSkill={};arg={};rank=r;pierce=pi;game.nDifficulty=difficulty;owner.stats[STAT_LEVEL]=level;
 validSkill=failure!=1;spawnId=failure==2?-1:425;spawnSuccess=failure!=5;spawnCalls=stateCalls=grantedRank=equipmentLevel=0;aiNode=-1;baseLife=12800+delta;
 for(int&x:skill.nPassiveStat)x=-1;skill.nPassiveStat[0]=7;skill.dwPassiveCalc[0]=0;for(int&x:skill.wAuraStat)x=-1;
 skill.nAuraState=150;for(int&x:skill.wSumSkill)x=-1;skill.wSumSkill[0]=294;skill.dwSumSkCalc[0]=12;skill.nPetType=failure==3?-1:failure==4?32:14;
 int result=SKILLS_SrvDo115_Vines(&game,&owner,222,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",level,r,difficulty,pi,delta,failure,result,pet.stats[STAT_LEVEL],pet.stats[STAT_MAXHP],pet.stats[STAT_HITPOINTS],pet.stats[STAT_ARMORCLASS],pet.stats[STAT_TOHIT],pet.stats[20],pet.stats[7],grantedRank,equipmentLevel,stateCalls,spawnCalls,arg.nMonMode,arg.nPetMax,aiNode,owner.dwFlags);first=false;
}
int main(){printf("[");for(int l:{1,30,99})for(int r=1;r<=255;r++)for(int d:{0,1,2})for(int pi:{0,17})for(int delta:{0,17})emit(l,r,d,pi,delta,0);
for(int f=1;f<=5;f++)emit(99,1,0,17,0,f);printf("]\n");}
'''
    birth = run('creeper-birth', cpp)
    assert len(birth) == 9185
    for level, rank, diff, pierce, delta, failure, result, pet_level, maximum, life, defense, attack, regen, inherited, grant, equipment, state_calls, spawn_calls, mode, cap, node, flags in birth:
        if not failure:
            total = 12800 + delta + (12800 + delta) * 50 * (rank - 1) // 100
            assert [result, pet_level, maximum, life, defense, attack, regen, inherited, grant, equipment, state_calls, spawn_calls, mode, cap, node, flags] == [1, max(1, level * 3 // 4 + rank), total, total, 25, 0, 9, pierce, rank, min(3 * rank, level), 2, 1, 8, 1, 123, 64]
        else:
            assert result == 0 and maximum == life == grant == state_calls == 0
            assert spawn_calls == (failure == 5) and flags == (64 if failure == 5 else 0)
    fixture = STAGE / 'creeper-components-source-probe.json'
    fixture.write_text(json.dumps(dict(fields=fields, damage=damage, birth=birth), separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
                    sourceHashes=hashes, unchangedRoutineHashes=routines, pinnedTableHashes=pins, generatedSourceHashes=generated,
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), damage=len(damage), birth=len(birth)),
                    sanitizers='Both ASan/UBSan binaries exit0 with empty runtime stderr.',
                    established=[
                        'Complete explicit-Skill222 poison creator/getters/writer select Creeper endpoints despite stored Vine Attack294; override controls select the damage-free actor row. All255ranks, hard Rabies0/1/20, negative/positive mastery, owner/weapon/origin noise, count/length exclusion and no-critical/source-zero routes are captured.',
                        'The complete do115/passive/percentage composition retains explicit ownerLevel*3/4+rank above99, scales existing base HP by50percent per rank above1, fills current life, preserves spawn defense25/AR0/regen9, copies accrued poison sunder and requests inherited Vine Attack at summon rank. Both direct and passive VINE_BEAST toggles occur. Equipment dispatch is recorded as a request only.',
                        'Missing skill/summon, invalid pet category and failed allocation return0 without stat/grant application. Valid lookup followed by allocation failure retains the old do-function owner flag. Shared factory layout/placement/spawn and formula services are declared; this is not a live summon.'
                    ], limits=[
                        'Current actor-to-owner Rabies/mastery/pierce transport and capture timing are not established by a declared formula/stat service. Do not claim these arithmetic cases verify that transport.',
                        'Spawn base50HP/25defense/0AR/9regen is supplied by the reviewed noRatio route; complete current monster initialization, spawn flags, difficulty/health scaling and state attackability require separate composition.',
                        'The old five-slot passive loop suffices for the current single Creeper passive. Stat/event/layout, AI/node, item-equipment and world/placement services are bounded shims, not a current executable ABI.',
                        'Trail Range formula, head/trail factory/motion/collision/window, poison application, current NPC timing/state, saved actors/input and native acceptance remain pending. No handler is enabled.'
                    ])
    path = ROOT / 'tools/reference/creeper-components-source-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Creeper:7272complete poison creator/getter/writer and9185complete do115/passive birth cases; native actor and stat transport remain separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
