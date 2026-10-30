#!/usr/bin/env python3
"""Execute complete pinned summon birth helpers with Iron Golem's current row.

The post-equipment stat getter is an input, not a fabricated equipment engine.
MAXHP output is the source base write, not a final accrued maximum.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'iron-golem-birth-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(exist_ok=True)
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs = {str(Path(__file__).relative_to(ROOT)): sha(Path(__file__))}
    routines = {}

    def body(relative, name):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        inputs[str(path.relative_to(ROOT))] = sha(path)
        text = function(raw.decode(), name)
        routines[name] = hashlib.sha256(text.encode()).hexdigest()
        return text + '\n'

    ref = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((ref / 'sources.json').read_text())['files']
    tables = {}
    for name in ['skills', 'monlvl', 'itemstatcost']:
        path = ref / 'json' / (name + '.json')
        assert sha(path) == manifest['json/' + name + '.json']['sha256']
        inputs[str(path.relative_to(ROOT))] = sha(path)
        tables[name] = json.loads(path.read_text())
    row = tables['skills']['90']
    assert row['skill'] == 'IronGolem' and row['srvdofunc'] == 57
    assert row['calc1'] == "skill('Golem Mastery'.ln12) + (skill('BloodGolem'.blvl)*skill('BloodGolem'.par8))"
    assert [row['passivestat' + str(i)] for i in range(1, 5)] == ['velocitypercent', 'tohit', 'armorclass', 'damagepercent']
    assert [row['passivecalc' + str(i)] for i in range(1, 5)] == ["skill('Golem Mastery'.dm34)", "skill('Golem Mastery'.ln56)+skill('Clay Golem'.blvl)*skill('Clay Golem'.par8)", 'lvl*par8', "(skill('FireGolem'.blvl)*skill('FireGolem'.par8))"]
    assert row['Param8'] == 35 and row['aurastatcalc1'] == 'edmn' and row['aurastatcalc2'] == 16
    assert [tables['skills']['79']['Param' + str(i)] for i in range(1, 7)] == [20, 20, 0, 40, 25, 25]
    assert [tables['skills'][n]['Param8'] for n in ['75', '85', '94']] == [20, 5, 6]
    for i in range(5, 11):
        assert row['passivecalc' + str(i)] == "stat('" + row['passivestat' + str(i)] + "'.accr)"
    ids = {name: data['*ID'] for name, data in tables['itemstatcost'].items()}
    assert [ids[row['passivestat' + str(i)]] for i in range(1, 6)] == [67, 19, 31, 25, 187]
    assert [ids[row['aurastat' + str(i)]] for i in range(1, 3)] == [78, 181]
    assert [ids[name] for name in ['level', 'armorclass', 'tohit', 'maxhp', 'hitpoints', 'fireresist', 'lightresist', 'coldresist', 'poisonresist']] == [12, 31, 19, 7, 6, 39, 41, 43, 45]
    assert row['HitShift'] == 8 and not row.get('EDmgSymPerCalc')
    thorns = tables['itemstatcost']['item_attackertakesdamage']
    assert thorns['itemevent1'] == 'attackedinmelee' and thorns['itemeventfunc1'] == 6
    cpp = r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
using D2C_UnitEventTypes=int;
constexpr int STAT_LEVEL=12,STAT_ARMORCLASS=31,STAT_TOHIT=19,STAT_MAXHP=7,STAT_HITPOINTS=6,
STAT_PASSIVE_SUMMON_RESIST=357,STAT_ITEM_ABSORBFIRE_PERCENT=142,STAT_ITEM_ABSORBLIGHT_PERCENT=144,
STAT_ITEM_ABSORBCOLD_PERCENT=148,STAT_FIRERESIST=39,STAT_LIGHTRESIST=41,STAT_COLDRESIST=43,STAT_POISONRESIST=45,SKILLSFLAGINDEX_AURA=0;
constexpr uint32_t gdwBitMasks[]={1};
struct D2UnitStrc{int dwUnitId=17,dwUnitType=1;void*pMemoryPool=nullptr;int stats[512]={};}owner,pet;
struct D2GameStrc{void*pMemoryPool=nullptr;uint8_t nDifficulty=0,nGameType=0;int dwGameType=0;}game;
struct D2MonLvlTxt{int dwLAC[3],dwAC[3],dwLTH[3],dwTH[3];};
struct Tables{int nMonLvlTxtRecordCount=111,nStatesTxtRecordCount=200,nOverlayTxtRecordCount=500;D2MonLvlTxt pMonLvlTxt[111];}tables;auto*sgptDataTables=&tables;
struct D2SkillsTxt{int nPassiveStat[5]={67,19,31,25,187},dwPassiveCalc[5]={0,1,2,3,4},wAuraStat[6]={78,181,-1,-1,-1,-1},dwAuraStatCalc[6]={5,6,0,0,0,0};
int nAuraState=29,dwCalc[1]={11},wSumSkill[5]={-1,-1,-1,-1,-1},dwSumSkCalc[5]={},dwFlags[1]={},wAuraEvent[3]={-1,-1,-1},wAuraEventFunc[3]={},wSumUMod=0,wSumOverlay=0;}skill;
struct D2ItemStatCostTxt{int wItemEvent[2]={},wItemEventFunc[2]={};}costs[512];
struct D2StatListStrc{}list;
int rank=1,gm=0,clay=0,blood=0,fire=0,thorns=0,equipmentLevel=0,events=0,state=0,resMask=0;
int STATLIST_UnitGetStatValue(D2UnitStrc*p,int s,int){return p->stats[s];}
void STATLIST_SetUnitStat(D2UnitStrc*p,int s,int n,int){p->stats[s]=n;}
void STATLIST_AddUnitStat(D2UnitStrc*p,int s,int n,int){p->stats[s]+=n;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*p){return p->stats[STAT_MAXHP];}
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){if(id<0){return nullptr;}assert(id==90);return &skill;}
D2ItemStatCostTxt*SKILLS_GetItemStatCostTxtRecord(int id){assert(id>=0&&id<512);return &costs[id];}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int f,int id,int r){assert(id==90&&r==rank);switch(f){
case 0:return gm>0?40*std::min(100,110*gm/(gm+6))/100:0;case 1:return 25*gm+20*clay;
case 2:return rank*35;case 3:return 6*fire;case 4:return 0;case 5:return thorns;case 6:return 16;
case 11:return 20*gm+5*blood;default:assert(false);return 0;}}
bool SUNITEVENT_GetEvent(D2GameStrc*,D2UnitStrc*,int,int,int){return false;}
void sub_6FD156A0(D2GameStrc*,D2UnitStrc*,int event,int layer,int zero,int func,int type,int guid){assert(event==1&&layer==(78<<16)&&zero==0&&func==6&&type==2&&guid==17);events++;}
D2StatListStrc*STATLIST_AllocStatList(void*,int,int,int,int){return &list;}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){}
void STATLIST_SetStatIfListIsValid(D2StatListStrc*,int s,int n,int){pet.stats[s]=n;if(s==39)resMask|=1;if(s==41)resMask|=2;if(s==43)resMask|=4;if(s==45)resMask|=8;}
void STATES_ToggleState(D2UnitStrc*,int n,int enabled){assert(n==29&&enabled==1);state=n;}
void STATLIST_SetState(D2StatListStrc*,int n){assert(n==29);}
void D2GAME_SetSkills_6FD14C60(D2UnitStrc*,int,int,int){assert(false);}
void D2GAME_AssignSkill_6FD13800(D2UnitStrc*,int,int,int){assert(false);}
void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int,int){assert(false);}
void D2GAME_BOSSES_AssignUMod_6FC6FF10(D2GameStrc*,D2UnitStrc*,int,int){assert(false);}
void UNITS_SetOverlay(D2UnitStrc*,int,int){assert(false);}
void sub_6FCF9580(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int,int level,int){equipmentLevel=level;}
int D2Clamp(int value,int minimum,int maximum){return std::clamp(value,minimum,maximum);}
'''
    # Resolve stat IDs from the pinned table instead of trusting the ABI labels.
    for symbol, name in [('STAT_PASSIVE_SUMMON_RESIST', 'passive_summon_resist'), ('STAT_ITEM_ABSORBFIRE_PERCENT', 'item_absorbfire_percent'), ('STAT_ITEM_ABSORBLIGHT_PERCENT', 'item_absorblight_percent'), ('STAT_ITEM_ABSORBCOLD_PERCENT', 'item_absorbcold_percent')]:
        cpp = re.sub(r'\b' + symbol + r'=\d+', symbol + '=' + str(ids[name]), cpp)
    nec = 'source/D2Game/src/SKILLS/SkillNec.cpp'
    cpp += body('source/D2Game/src/MONSTER/MonsterUnique.cpp', 'MONSTERUNIQUE_CalculatePercentage')
    for name in ['D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10', 'D2GAME_SetSummonPassiveStats_6FD0C530', 'D2GAME_SetSummonResistance_6FD0C2E0']:
        cpp += body(nec, name)
    cpp += '\nint main(){\n'
    for level in range(111):
        r = tables['monlvl'][str(level)]
        for member, field in [('dwAC', 'AC'), ('dwTH', 'TH'), ('dwLAC', 'L-AC'), ('dwLTH', 'L-TH')]:
            for diff, suffix in enumerate(['', '(N)', '(H)']):
                cpp += f'tables.pMonLvlTxt[{level}].{member}[{diff}]={r[field+suffix]};\n'
    # EDmn is an explicit formula service. Derive all tiers from the pinned row.
    cpp += 'int reflected[256]={};\n'
    for rank in range(1, 256):
        damage, remaining = row['EMin'], rank - 1
        for i, width in enumerate([7, 8, 6, 6, 2**30], 1):
            count = min(remaining, width)
            damage += count * row['EMinLev' + str(i)]
            remaining -= count
        cpp += f'reflected[{rank}]={damage};\n'
    cpp += r'''costs[78].wItemEvent[0]=1;costs[78].wItemEventFunc[0]=6;bool first=true;
printf("{\"birth\":[");
for(int level:{1,24,99})for(int r=1;r<=255;r++)for(int diff=0;diff<3;diff++)for(int expansion:{0,1})for(int synergy=0;synergy<4;synergy++)for(int delta:{17,15377}){
owner={};pet={};rank=r;gm=synergy==0?0:synergy==1?1:synergy==2?20:28;clay=blood=fire=synergy==0?0:synergy==1?1:20;thorns=reflected[r];
int hp=(diff==0?306:diff==1?595:980)*256+delta;pet.stats[STAT_MAXHP]=hp;pet.stats[STAT_HITPOINTS]=1;
pet.stats[STAT_ARMORCLASS]=157;pet.stats[STAT_TOHIT]=13;owner.stats[STAT_LEVEL]=level;game.nDifficulty=diff;game.nGameType=expansion;events=state=equipmentLevel=resMask=0;
int base=D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10(&game,&owner,&pet,0,r);
int passive=D2GAME_SetSummonPassiveStats_6FD0C530(&game,&owner,&pet,90,r,0);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",level,r,diff,expansion,gm,clay,blood,fire,hp,base,passive,pet.stats[STAT_LEVEL],pet.stats[STAT_MAXHP],pet.stats[STAT_HITPOINTS],pet.stats[STAT_ARMORCLASS],pet.stats[STAT_TOHIT],pet.stats[67],pet.stats[25],pet.stats[78],pet.stats[181],equipmentLevel,events,state);first=false;
}printf("],\"resistance\":[");first=true;
for(int bonus:{-5,0,52})for(int fire:{-1,0,10})for(int light:{-1,0,10})for(int cold:{-1,0,10}){
owner={};pet={};owner.stats[STAT_PASSIVE_SUMMON_RESIST]=bonus;pet.stats[STAT_ITEM_ABSORBFIRE_PERCENT]=fire;pet.stats[STAT_ITEM_ABSORBLIGHT_PERCENT]=light;pet.stats[STAT_ITEM_ABSORBCOLD_PERCENT]=cold;resMask=0;
D2GAME_SetSummonResistance_6FD0C2E0(&owner,&pet);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",bonus,fire,light,cold,resMask,pet.stats[39],pet.stats[41],pet.stats[43],pet.stats[45]);first=false;
}printf("]}\n");}
'''
    for name, digest in routines.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest
    path = STAGE / 'source.cpp'
    path.write_text(cpp)
    with (STAGE / 'compiler.log').open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(STAGE / 'probe')], stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(STAGE / 'probe')], capture_output=True)
    (STAGE / 'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    cases = json.loads(run.stdout)
    assert len(cases['birth']) == 36720 and len(cases['resistance']) == 81
    path = STAGE / 'cases.json'
    path.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    receipt = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], compiledUnchangedRoutines=routines,
        inputs=inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in [STAGE / n for n in ['source.cpp', 'cases.json', 'compiler.log', 'sanitizer.log']]},
        birthCases=36720, resistanceCases=81, currentExecutableParity=False, handlerEnabled=False,
        services=['Supplied post-equip getter values, simple additive/base stat storage, successful statlist allocation, exact asserted row formulas, event/state recording and equipment-level request. The original formula VM and complete accrued stat dependency graph are not executed.',
            'All ten current passive slots are retained as data by the native component; the unchanged older helper executes five. Its fifth cold-sunder service is zero. Modern nonzero-sunder delivery is not certified.',
            'Thorns registers attacked-in-melee event function6 in the source controls. This preparation component does not register or deliver that event to a real actor.'],
        established=['Every rank1–255, owner levels1/24/99, three difficulties, two game contexts, four mastery/synergy contexts and two fractional post-equip life inputs exercise complete base/passive helpers.',
            'The resulting maximum-life value is a base-stat WRITE after reading equipped accrued life. It is not a certificate for the final accrued maximum while attached items remain. Current life receives the same write.',
            'Complete resistance helper accepts nonpositive item absorption, suppresses its elemental bonus for positive absorption, always adds poison for nonzero bonus, and does nothing for zero bonus.'],
        remaining=['Actual item-equipped graph writes/callbacks and final accrued HP, full birth/spawn, retaliation/event consumers, modern sunder behavior, cast/payment/replacement/placement, actor AI and saved lifecycle. No learned handler, installation or full-class parity is claimed.'])
    target = ROOT / 'tools/reference/iron-golem-birth-source-evidence.json'
    encoded = json.dumps(receipt, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Iron Golem birth:36,720 complete base/passive contexts and81 resistance contexts;post-equip getter/base-write boundary explicit.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
