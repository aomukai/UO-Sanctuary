#!/usr/bin/env python3
"""Execute complete beast owner-damage, attack-mode and noRatio table composition."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
REFERENCE = ROOT / 'tools/reference/d2r-named-equipment'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    manifest = json.loads((REFERENCE / 'sources.json').read_text())['files']
    tables = {}
    for name in ['monstats', 'skills']:
        path = REFERENCE / f'json/{name}.json'
        assert sha(path) == manifest[f'json/{name}.json']['sha256']
        tables[name] = json.loads(path.read_text())
    sources, routines = {}, {}

    def original(relative, name):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        sources[relative] = hashlib.sha256(raw).hexdigest()
        body = function(raw.decode(), name)
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    cpp = r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <initializer_list>
#include <iterator>
#define __fastcall
#define __stdcall
using BOOL=int;constexpr int TRUE=1,FALSE=0,UNIT_PLAYER=0,UNIT_MONSTER=1;
constexpr int MONMODE_ATTACK1=4,MONMODE_ATTACK2=5,MONMODE_BLOCK=3,MONMODE_CAST=7,MONMODE_SKILL1=8;
constexpr int DIFFMODE_NORMAL=0,MONALIGN_GOOD=1,UNIT_ALIGNMENT_EVIL=0,MONSTATSFLAGINDEX_NORATIO=0,SKILLSFLAGINDEX_KICK=1;
enum{STAT_LEVEL,STAT_DEXTERITY,STAT_STRENGTH,STAT_MINDAMAGE,STAT_MAXDAMAGE,STAT_TOHIT,STAT_MONSTER_PLAYERCOUNT,
STAT_FIREMINDAM,STAT_FIREMAXDAM,STAT_LIGHTMINDAM,STAT_LIGHTMAXDAM,STAT_MAGICMINDAM,STAT_MAGICMAXDAM,
STAT_COLDMINDAM,STAT_COLDMAXDAM,STAT_COLDLENGTH,STAT_POISONMINDAM,STAT_POISONMAXDAM,STAT_POISONLENGTH,
STAT_LIFEDRAINMINDAM,STAT_LIFEDRAINMAXDAM,STAT_MANADRAINMINDAM,STAT_MANADRAINMAXDAM,STAT_STAMDRAINMINDAM,STAT_STAMDRAINMAXDAM,
STAT_STUNLENGTH,STAT_BURNINGMIN,STAT_BURNINGMAX,STAT_FIRELENGTH};
enum{ELEMTYPE_RAND,ELEMTYPE_FIRE,ELEMTYPE_LTNG,ELEMTYPE_MAGIC,ELEMTYPE_COLD,ELEMTYPE_POIS,ELEMTYPE_LIFE,ELEMTYPE_MANA,ELEMTYPE_STAMINA,ELEMTYPE_STUN,ELEMTYPE_BURN};
unsigned gdwBitMasks[32];
struct D2MonStatsTxt{unsigned dwMonStatsFlags=1;int nAlign=1,wSkillDamage=237;
int nMinHP[3]={},nMaxHP[3]={},nAC[3]={},nExp[3]={},nA1TH[3]={},nA1MinD[3]={},nA1MaxD[3]={},nA2TH[3]={},nA2MinD[3]={},nA2MaxD[3]={},nS1TH[3]={},nS1MinD[3]={},nS1MaxD[3]={},nElMinD[3][3]={},nElMaxD[3][3]={},nElDur[3][3]={};
uint8_t nElMode[3]={},nElType[3]={},nElPct[3][3]={};}mon;
struct D2MonLvlTxt{int dwHP[6]={},dwAC[6]={},dwXP[6]={},dwTH[6]={},dwDM[6]={};};
struct Tables{int nSkillsTxtRecordCount=548,nMonLvlTxtRecordCount=111;D2MonLvlTxt pMonLvlTxt[111];}tables;auto*sgptDataTables=&tables;
struct D2MonStatsInitStrc{int nMinHP=0,nMaxHP=0,nAC=0,nExp=0,nTH=0,nA1MinD=0,nA1MaxD=0,nA2MinD=0,nA2MaxD=0,nS1MinD=0,nS1MaxD=0,nElMinD=0,nElMaxD=0,nElDur=0;};
struct D2MonsterDataStrc{D2MonStatsTxt*pMonstatsTxt=&mon;}monsterData;
struct D2UnitStrc{int dwUnitType=UNIT_MONSTER,dwClassId=421,rank=1,stats[64]={},pSeed=0;void*pInventory=nullptr;D2MonsterDataStrc*pMonsterData=&monsterData;}pet,owner;
struct D2MonSkillInfoStrc{int nMinDamage=0,nMaxDamage=0,nToHit=0;};
struct D2SkillsTxt{unsigned dwFlags[2]={},dwToHitCalc=unsigned(-1);int dwToHit=0,dwLevToHit=0,nSrcDam=0,dwMinDam=0,dwMaxDam=0,dwMinLvlDam[5]={},dwMaxLvlDam[5]={},dwDmgSymPerCalc=-1,nToHitShift=8;}skill;
struct D2StatListStrc{int stats[64]={};}list;
struct D2GameStrc{int nGameType=0,dwGameType=0,nDifficulty=0,bExpansion=1;}game;
bool ownerPresent=true,listPresent=true,skillPresent=true;int removed=0,writes=0,ownerQueries=0,rankQueries=0;
D2UnitStrc*AIGENERAL_GetMinionOwner(D2UnitStrc*){ownerQueries++;return ownerPresent?&owner:nullptr;}
D2MonStatsTxt*MONSTERMODE_GetMonStatsTxtRecord(int){return &mon;}
D2MonStatsTxt*DATATBLS_GetMonStatsTxtRecord(int){return &mon;}
D2SkillsTxt*DATATBLS_GetSkillsTxtRecord(int id){return skillPresent&&id==mon.wSkillDamage?&skill:nullptr;}
int SKILLS_GetHighestSkillLevelById(D2UnitStrc*p,int){rankQueries++;return p->rank;}
int STATLIST_UnitGetStatValue(D2UnitStrc*p,int stat,int){return p?p->stats[stat]:0;}
D2UnitStrc*D2Common_10434(D2UnitStrc*,int){__builtin_trap();}
int INVENTORY_GetWieldType(D2UnitStrc*,void*){__builtin_trap();}
int STATLIST_GetMinDamageFromUnit(D2UnitStrc*,int){__builtin_trap();}
int STATLIST_GetMaxDamageFromUnit(D2UnitStrc*,int){__builtin_trap();}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){__builtin_trap();}
int DATATBLS_ApplyRatio(int,int,int){__builtin_trap();}
D2StatListStrc*STATLIST_GetStatListFromUnitAndFlag(D2UnitStrc*,int flag){assert(flag==1);return listPresent?&list:nullptr;}
void STATLIST_RemoveAllStats(D2StatListStrc*p){*p={};removed++;}
D2GameStrc*SUNIT_GetGameFromUnit(D2UnitStrc*){return &game;}
int STATLIST_GetUnitAlignment(D2UnitStrc*){return 1;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc*p,int stat,int value,int){if(p){p->stats[stat]=value;writes++;}}
unsigned ITEMS_RollRandomNumber(int*){__builtin_trap();}
'''
    for relative, names in [
        ('source/D2Common/src/D2Skills.cpp', ['SKILLS_CalculateDamageBonusByLevel', 'SKILLS_GetMinPhysDamage', 'SKILLS_GetMaxPhysDamage', 'SKILLS_GetToHitFactor']),
        ('source/D2Common/src/DataTbls/MonsterTbls.cpp', ['DATATBLS_CalculateMonsterStatsByLevel']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['sub_6FD14D20']),
        ('source/D2Game/src/MONSTER/MonsterMode.cpp', ['sub_6FC627B0']),
    ]:
        for name in names:
            cpp += original(relative, name)
    cpp += '\nvoid prepare(int species){mon={};skill={};\n'
    for species, (name, key) in enumerate([('spiritwolf', 'Summon Spirit Wolf'), ('fenris', 'Summon Fenris')]):
        row = next(x for x in tables['monstats'].values() if x.get('Id') == name)
        s = next(x for x in tables['skills'].values() if x.get('skill') == key)
        assert row['noRatio'] == row['Align'] == 1 and row['SkillDamage'] == key
        assert not any(row.get(f'El{i}Mode', '') for i in range(1, 4))
        assert not any(s.get(k, '') for k in ['SrcDam', 'DmgSymPerCalc', 'ToHitCalc', 'kick'])
        cpp += f'if(species=={species}){{mon.wSkillDamage={s["*Id"]};\n'
        for member, field in [('nA1TH', 'A1TH'), ('nA1MinD', 'A1MinD'), ('nA1MaxD', 'A1MaxD'), ('nA2TH', 'A2TH'), ('nA2MinD', 'A2MinD'), ('nA2MaxD', 'A2MaxD'), ('nS1TH', 'S1TH'), ('nS1MinD', 'S1MinD'), ('nS1MaxD', 'S1MaxD')]:
            for d, suffix in enumerate(['', '(N)', '(H)']):
                cpp += f'mon.{member}[{d}]={row.get(field+suffix, 0)};\n'
        for member, field in [('dwToHit', 'ToHit'), ('dwLevToHit', 'LevToHit'), ('dwMinDam', 'MinDam'), ('dwMaxDam', 'MaxDam'), ('nToHitShift', 'HitShift')]:
            cpp += f'skill.{member}={s.get(field, 0)};\n'
        for side in ['Min', 'Max']:
            for i in range(5):
                cpp += f'skill.dw{side}LvlDam[{i}]={s.get(side+"LevDam"+str(i+1), 0)};\n'
        cpp += '}\n'
    cpp += r'''}
bool first=true;
void emit(int species,int rank,int diff,int expansion,int mode,int control){
prepare(species);pet={};owner={};monsterData={};owner.dwUnitType=UNIT_PLAYER;owner.rank=rank;pet.stats[STAT_LEVEL]=99;
game.nDifficulty=diff;game.nGameType=expansion;game.dwGameType=0;game.bExpansion=expansion;
list={};list.stats[STAT_MINDAMAGE]=123;list.stats[STAT_MAXDAMAGE]=456;list.stats[STAT_TOHIT]=789;
removed=writes=ownerQueries=rankQueries=0;listPresent=control!=5;ownerPresent=control!=6;skillPresent=control!=7;
if(control==2)pet.dwUnitType=UNIT_PLAYER;if(control==3)pet.pMonsterData=nullptr;if(control==4)monsterData.pMonstatsTxt=nullptr;
sub_6FC627B0(control==1?nullptr:&pet,mode);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",species,rank,diff,expansion,mode,control,list.stats[STAT_MINDAMAGE],list.stats[STAT_MAXDAMAGE],list.stats[STAT_TOHIT],removed,writes,ownerQueries,rankQueries);first=false;
}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");
for(int species:{0,1})for(int rank=0;rank<=255;rank++)for(int diff:{0,1,2})for(int expansion:{0,1})for(int mode:{4,5,7,8})emit(species,rank,diff,expansion,mode,0);
for(int species:{0,1})for(int rank:{1,28})for(int diff:{0,2})for(int expansion:{0,1})for(int mode:{4,5,7,8})for(int control=1;control<=7;control++)emit(species,rank,diff,expansion,mode,control);
printf("]\n");}
'''
    path = STAGE / 'druid-beast-attack-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    process = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'druid-beast-attack-sanitizer.log').write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-3000:]
    cases = json.loads(process.stdout)
    assert len(cases) == 12736
    for species, rank, difficulty, expansion, mode, control, minimum, maximum, ar, cleared, writes, owners, ranks in cases:
        if control in [1, 2, 3, 4, 5]:
            assert [minimum, maximum, ar, cleared, writes] == [123, 456, 789, 0, 0]
            assert owners == ranks == int(control == 5)
            continue
        base_ar = (50 if species == 0 else 150) if mode == 4 else 0
        tohit = (75 if species == 0 else 150) + 20 * (rank - 1) if rank else 0
        increments = [[2, 4, 8, 10, 12], [3, 5, 9, 11, 13]]
        def tier(side):
            return [7, 12][side] + sum(increments[side][0 if i <= 8 else 1 if i <= 16 else 2 if i <= 22 else 3 if i <= 28 else 4] for i in range(2, rank + 1))
        expected = [tier(0), tier(1)] if species == 1 and control not in [6, 7] else [0, 0]
        assert [minimum, maximum, ar, cleared, writes] == [*expected, base_ar + (tohit if control not in [6, 7] else 0), 1, 3], (species, rank, mode, control)
        assert owners == 1 and ranks == int(control != 6)
    fixture = STAGE / 'druid-beast-attack-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
        sourceHashes=sources, unchangedRoutineHashes=routines,
        tableHashes={name: sha(REFERENCE / f'json/{name}.json') for name in ['monstats', 'skills']},
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture), cases=len(cases), ordinaryContexts=12288, guardContexts=448, generatedSourceSha256=sha(path), sanitizers='ASan+UBSan'),
        established=[
            'Complete owner-derived damage/AR hook, physical tier/min/max and to-hit getters, complete monster attack-mode writer and noRatio table helper execute together. Current owner rank0..255 is sampled on every mode entry, not copied into birth.',
            'A1 adds literal monster AR50 for Spirit Wolf or150 for Fenris to live skill AR75/150+20 per rank after the first. Owner rank zero yields no skill AR. The separate birth monlvl AR and copied passive AR do not replace this flag1 mode list.',
            'Spirit Wolf mode physical endpoints are zero; Fenris gets current live rank physical tiers7..12 and no weapon or skill-synergy contribution here. Copied Grizzly damagepercent and rage are later damage consumers. A2/cast/S1 use distinct empty table lanes. Old mode stats are cleared before three new writes.',
        ], limits=[
            'Current table rows plus supplementary old engine composition are not a modern executable equivalence certificate. Native world A1 entry, body, hit/damage/stat aggregation and mode refresh timing need separate acceptance. Spirit Wolf ordinary melee physical-floor handling is downstream and remains uncertain.',
            'Owner/rank/statlist/game/layout services are explicit shims. Current friendly/noRatio/no-element species do not execute evil player-count, hostile classic scaling, ratio or elemental mode branches; unexpected service use traps. Null/nonmonster/metadata/list/owner/skill-row controls are retained.',
        ])
    target = ROOT / 'tools/reference/druid-beast-attack-engine-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert target.read_text() == rendered
    else:
        target.write_text(rendered)
    print('Beast attack-mode composition:12288ordinary/448guard contexts pass ASan+UBSan; native consumers remain separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
