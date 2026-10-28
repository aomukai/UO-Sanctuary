#!/usr/bin/env python3
"""Compose Rabies's full older start/do/payment and separate ordinary/poison packets."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    pinpath = ROOT / 'tools/reference/druid-rabies-preflight.json'
    pin = json.loads(pinpath.read_text())
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == pin['legacyCommit']
    hashes, routines, enums = {}, {}, {}

    def source(relative):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = sha(path)
        return raw.decode()

    def routine(relative, name):
        body = function(source(relative), name)
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        if name in pin['completeRoutineHashes']:
            assert routines[name] == pin['completeRoutineHashes'][name]
        return body + '\n'

    def enum(relative, name):
        body = re.search(r'\benum\s+' + name + r'\s*\{.*?\};', source(relative), re.S).group()
        enums[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    cpp = r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <initializer_list>
#include <iterator>
#include <vector>
#define __fastcall
#define __stdcall
#define D2_MAYBE_UNUSED(x) (void)(x)
using BOOL=int;
'''
    for relative, name in [
        ('source/D2Common/include/D2Combat.h', 'D2C_ElemTypes'),
        ('source/D2Common/include/D2StatList.h', 'D2C_ItemStats'),
        ('source/D2Common/include/D2States.h', 'D2C_States'),
        ('source/D2Game/include/UNIT/SUnitDmg.h', 'D2DamageResultFlags'),
        ('source/D2Game/include/UNIT/SUnitDmg.h', 'D2DamageHitFlags'),
        ('source/D2Common/include/DataTbls/SkillsTbls.h', 'D2C_SkillsTxtFlagIndices'),
        ('source/D2Common/include/DataTbls/SkillsTbls.h', 'D2C_SkillsTxtLineOfSight'),
    ]:
        cpp += enum(relative, name)
    cpp += r'''
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_ITEM=4,UNITFLAG_SKSRVDOFUNC=64;
constexpr int EVENTTYPE_REMOVESTATE=10,EVENTTYPE_PERIODICSKILLS=2,PLRMODE_TOWNNEUTRAL=5,PLRMODE_SEQUENCE=18;
constexpr int COLLIDE_MISSILE_BARRIER=0,COLLIDE_MASK_PLAYER_PATH=0,COLLIDE_PLAYER=0,COLLIDE_MONSTER=0,COLLIDE_DOOR=0,COLLIDE_WALL=0;
struct Seed{};
struct D2GameStrc{int dwGameFrame=1000;void* pMemoryPool=nullptr;}game;
struct D2DamageStrc{uint32_t wResultFlags=0,dwHitFlags=0,dwHitClass=0;int dwEnDmgPct=0,dwPhysDamage=0,dwFireDamage=0,dwLtngDamage=0,dwMagDamage=0,dwColdDamage=0,dwPoisDamage=0,dwLifeLeech=0,dwManaLeech=0,dwStamLeech=0,dwColdLen=0,dwPoisLen=0,dwStunLen=0,dwBurnLen=0,dwBurnDamage=0,dwFrzLen=0,dwDmgTotal=0;};
struct D2CombatStrc{D2GameStrc* pGame=nullptr;int dwAttackerType=0,dwAttackerId=0,dwDefenderType=0,dwDefenderId=0;D2DamageStrc tDamage;D2CombatStrc* pNext=nullptr;};
struct D2UnitStrc{int dwUnitType=0,dwUnitId=1,dwClassId=5,dwFlags=0,dwSeqFrame=0,hp=16000000,mana=0;void* pMemoryPool=nullptr;void* pDynamicPath=nullptr;D2CombatStrc* pCombat=nullptr;Seed pSeed;}owner,target,plague;
struct D2SkillsTxt{int dwEMin=6,dwEMax=14,dwEMinLev[5]={4,5,7,11,16},dwEMaxLev[5]={4,5,7,11,16},nToHitShift=3,dwEDmgSymPerCalc=1002,nEType=ELEMTYPE_POIS,dwELen=100,dwELevLen[3]={10,10,10},dwELenSymPerCalc=-1;uint32_t dwToHitCalc=uint32_t(-1);int dwToHit=50,dwLevToHit=10,wMana=10,wLevelMana=0,nManaShift=8,wMinMana=1,wSrvStartFunc=57,wSrvDoFunc=121,nLineOfSight=0,wItemEffect=0,wSrvMissile=-1,nAnim=15;uint32_t wResultFlags=1,dwHitFlags=2,dwHitClass=0,dwCalc[4]={},dwDelay=0,dwFlags[2]={};uint8_t nSrcDam=0;int wAuraTargetState=112,nAuraState=-1;}row,wolf;
struct D2SkillStrc{int id=238,rank=1,param=9,nCharges=0;}usedSkill;
struct D2StatListStrc{int state=112,deadline=6000;};D2StatListStrc marker,wolfList;
struct D2StatesTxt{}wolfState;
struct D2MissileStrc{int nMissile=0,nRange=0,dwFlags=0,nSkill=0,nSkillLevel=0,nX=0,nY=0,nTargetX=0,nTargetY=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;};
int transformStates[]={139};
struct Tables{int nStatesTxtRecordCount=200,nTransformStates=1;int* pTransformStates=transformStates;int nMissilesTxtRecordCount=0,pMissilesTxt[1]={},nShiftedStuff=63,nStuff=6;}tables;auto*sgptDataTables=&tables;
struct D2ActiveRoomStrc{}room;
using SkillStartFunc=int32_t(*)(D2GameStrc*,D2UnitStrc*,int32_t,int32_t);using SkillDoFunc=SkillStartFunc;
SkillStartFunc gpSkillSrvStartFnTable_6FD408B0[58]={};SkillDoFunc gpSkillSrvDoFnTable_6FD40A20[122]={};unsigned gdwBitMasks[32];
int scene=0,hard=0,mastery=0,upper=0,critical=0,startFlags=1,weaponFlags=1,resultCalls=0,fills=0,calculations=0,allocations=0,drains=0,delivered=0,draws=0,paid=0,markerAllocations=0,markerPosts=0,plagues=0,plagueRange=0;
bool selected=true,used=true,marked=false,releaseContact=true;std::vector<int> attackPercents;std::vector<D2DamageStrc> packets;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){if(id==223)return &wolf;assert(id==238);return scene==1?nullptr:&row;}
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){return SKILLS_GetSkillsTxtRecord(id);}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return used?&usedSkill:nullptr;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecordFromSkill(D2SkillStrc* skill){return SKILLS_GetSkillsTxtRecord(skill->id);}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc* skill,const char*,int){return skill->id;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc* skill,int){return skill->rank;}
int SKILLS_GetOwnerGUIDFromSkill(D2SkillStrc*){return -1;}
int SKILLS_GetParam1(D2SkillStrc* skill){return skill->param;}void SKILLS_SetParam1(D2SkillStrc* skill,int value){skill->param=value;}
unsigned SKILLS_GetFlags(D2SkillStrc*){return 0;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return nullptr;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int id,int){assert(id==238);if(formula==0)return 0;if(formula==1002)return hard*20;std::abort();}
int STATLIST_UnitGetStatValue(D2UnitStrc* unit,int stat,int){if(unit==&target&&stat==STAT_HITPOINTS)return target.hp;if(unit==&owner&&stat==STAT_MANA)return owner.mana;assert(unit==&owner&&stat==STAT_PASSIVE_POIS_MASTERY);return mastery;}
void STATLIST_AddUnitStat(D2UnitStrc* unit,int stat,int value,int){assert(unit==&owner&&stat==STAT_MANA);owner.mana+=value;paid++;}
int DATATBLS_ApplyRatio(int amount,int percent,int divisor){return int(int64_t(amount)*percent/divisor);}
int ITEMS_RollLimitedRandomNumber(Seed*,int bound){assert(bound>0);draws++;return upper?bound-1:0;}
int ITEMS_RollRandomNumber(Seed*){std::abort();}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return selected?&target:nullptr;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int ar,int){attackPercents.push_back(ar);if(resultCalls++==0){if(scene==12)owner.mana=0;return startFlags;}return releaseContact?weaponFlags:0;}
bool STATES_CheckState(D2UnitStrc* unit,int state){return unit==&owner&&state==STATE_WOLF;}
bool STATES_IsUnitShapeShifted(D2UnitStrc*){return true;}
D2StatesTxt* SKILLS_GetStatesTxtRecord(int state){assert(state==139);return &wolfState;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* unit,int state){if(unit==&owner&&state==139)return &wolfList;assert(unit==&target&&state==112);return marked?&marker:nullptr;}
int STATLIST_GetStatValue(D2StatListStrc* list,int stat,int){assert(list==&wolfList&&stat==STAT_MODIFIERLIST_SKILL);return 223;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned flags,int deadline,int type,int id){assert(flags==2&&type==1&&id==2);markerAllocations++;if(scene==7)return nullptr;marker.deadline=deadline;return &marker;}
void STATLIST_SetState(D2StatListStrc* list,int state){assert(list==&marker&&state==112);list->state=state;}
void sub_6FD10E50(){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)()){}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc* unit,D2StatListStrc* list,int value){assert(unit==&target&&list==&marker&&value==1);marked=true;markerPosts++;}
void STATES_ToggleState(D2UnitStrc* unit,int state,int value){assert(unit==&target&&state==112&&value==1);}
void D2COMMON_10476(D2StatListStrc* list,int frame){list->deadline=frame;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc* unit,int kind,int frame,int,int){assert(unit==&target&&kind==EVENTTYPE_REMOVESTATE&&frame==marker.deadline);}
void sub_6FCFE0E0(D2UnitStrc* unit,D2StatListStrc* list,D2SkillsTxt* skill,int id,int rank){assert(unit==&owner&&list==&marker&&skill==&row&&id==238&&rank==usedSkill.rank);}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int id){assert(id==238);return scene==8?0:515;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* args){assert(args->nMissile==515&&args->dwFlags==32768&&args->pOwner==&target&&args->pOrigin==&target&&args->nSkill==238&&args->nSkillLevel==usedSkill.rank);if(scene==9)return nullptr;plagues++;plagueRange=args->nRange;return &plague;}
void MISSILE_SetTargetX(D2UnitStrc* unit,int value){assert(unit==&plague&&value==0);}
void MISSILE_SetTargetY(D2UnitStrc* unit,int value){assert(unit==&plague&&value==1);}
void SUNITDMG_FillDamageValues(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* damage,int,uint8_t src){assert(src==128);fills++;damage->dwPhysDamage=(40*256+37)*(critical?2:1);damage->dwFireDamage=11*256+13;damage->dwPoisDamage=777;damage->dwPoisLen=321;damage->dwLifeLeech=7;damage->dwManaLeech=9;damage->dwHitFlags|=DAMAGEHITFLAG_32;if(critical)damage->wResultFlags|=DAMAGERESULTFLAG_CRITICALSTRIKE;}
void SUNITDMG_CalculateTotalDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* damage){calculations++;damage->dwDmgTotal=damage->dwPhysDamage+damage->dwFireDamage;}
template<class T>T* allocate(void*){allocations++;return new T;}
#define D2_ALLOC_STRC_POOL(pool,T) allocate<T>(pool)
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc* unit,D2UnitStrc*,int value){assert(unit==&owner&&value==0);drains++;auto*combat=owner.pCombat;assert(combat);packets.push_back(combat->tDamage);if(releaseContact&&(combat->tDamage.wResultFlags&1))delivered++;owner.pCombat=combat->pNext;delete combat;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return false;}
void UNITS_SetUsedSkill(D2UnitStrc*,D2SkillStrc*){std::abort();}void sub_6FC817D0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int){std::abort();}
int CLIENTS_GetUnitX(D2UnitStrc*){std::abort();}int CLIENTS_GetUnitY(D2UnitStrc*){std::abort();}
int D2COMMON_10175_PathGetFirstPointX(void*){std::abort();}int D2COMMON_10176_PathGetFirstPointY(void*){std::abort();}
bool D2Common_11026(int,int,D2UnitStrc*,uint32_t){std::abort();}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){std::abort();}
int sub_6FD118C0(D2GameStrc*,D2UnitStrc*){std::abort();}
void sub_6FD11710(D2GameStrc*,int,D2UnitStrc*,int,int,int,int,int,int,int){std::abort();}void sub_6FD11420(D2GameStrc*,int,D2UnitStrc*,int,int,int,int,int,int,int){std::abort();}
void D2GAME_SKILLS_SetDelay_6FD11C00(D2GameStrc*,D2UnitStrc*,int){std::abort();}
int D2GAME_SKILLS_BloodMana_6FD025E0(D2UnitStrc*,int){std::abort();}
D2UnitStrc* SUNIT_GetServerUnit(D2GameStrc*,int,int){std::abort();}
bool ITEMMODS_GetItemCharges(D2UnitStrc*,int,int,int*,D2StatListStrc**){std::abort();}
void STATLIST_SetBaseStat2(D2StatListStrc*,int,int,int,D2UnitStrc*){std::abort();}
void* SUNIT_GetClientFromPlayer(D2UnitStrc*,const char*,int){std::abort();}
void D2GAME_PACKETS_SendPacket0x3E_6FC3EC20(void*,D2UnitStrc*,int,int,int,int){std::abort();}
'''
    common = 'source/D2Common/src/D2Skills.cpp'
    skills = 'source/D2Game/src/SKILLS/Skills.cpp'
    druid = 'source/D2Game/src/SKILLS/SkillDruid.cpp'
    for name in ['SKILLS_CalculateDamageBonusByLevel', 'SKILLS_CalculateMasteryBonus', 'SKILLS_GetMinElemDamage', 'SKILLS_GetMaxElemDamage', 'SKILLS_GetElementalLength', 'SKILLS_GetToHitFactor', 'SKILLS_GetManaCosts']:
        cpp += routine(common, name)
    for name in ['sub_6FD155E0', 'sub_6FD11E40', 'D2GAME_RollElementalDamage_6FD14DD0', 'sub_6FD11D90']:
        cpp += routine(skills, name)
    cpp += routine('source/D2Game/src/UNIT/SUnitDmg.cpp', 'SUNITDMG_AllocCombat')
    for name in ['SKILLS_SrvSt57_Rabies', 'sub_6FD00370', 'sub_6FCFF2E0', 'SKILLS_SrvDo121_Rabies']:
        cpp += routine(druid, name)
    for name in ['sub_6FD10CE0', 'D2GAME_SKILLMANA_Consume_6FD10A50', 'sub_6FD12950', 'D2GAME_SKILLS_Handler_6FD12BA0']:
        cpp += routine(skills, name)
    cpp += r'''
void emitPacket(const D2DamageStrc& p){printf("[%d,%d,%d,%d,%d,%d,%u,%u,%u]",p.dwPhysDamage,p.dwFireDamage,p.dwPoisDamage,p.dwPoisLen,p.dwLifeLeech,p.dwManaLeech,p.wResultFlags,p.dwHitFlags,p.dwHitClass);}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;
 gpSkillSrvStartFnTable_6FD408B0[57]=SKILLS_SrvSt57_Rabies;gpSkillSrvDoFnTable_6FD40A20[121]=SKILLS_SrvDo121_Rabies;
 printf("{\"scalars\":[");bool first=true;
 for(int rank=1;rank<=255;rank++)for(int points:{0,1,20})for(int master:{0,25,100}){
  scene=0;row={};hard=points;mastery=master;
  int lo=SKILLS_GetMinElemDamage(&owner,238,rank,1),hi=SKILLS_GetMaxElemDamage(&owner,238,rank,1),length=SKILLS_GetElementalLength(&owner,238,rank,1);
  printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,points,master,lo,hi,length,SKILLS_GetManaCosts(238,rank),SKILLS_GetToHitFactor(&owner,238,rank));first=false;
 }
 printf("],\"bites\":[");first=true;
 for(int rank:{1,3,20,28,60,255})for(int points:{0,20})for(int master:{0,25})for(int roll:{0,1})for(int crit:{0,1})
 for(int st:{0,1,4})for(int wep:{0,1,4})for(int mode=0;mode<13;mode++)for(int mana:{2559,2560}){
  scene=mode;row={};wolf={};wolf.dwToHit=50;wolf.dwLevToHit=15;wolf.nSrcDam=128;wolf.nEType=ELEMTYPE_NONE;
  owner={};target={};target.dwUnitType=1;target.dwUnitId=2;usedSkill={};usedSkill.rank=rank;owner.mana=mana;marker={};marked=scene==6;
  selected=scene!=3;used=scene!=2;releaseContact=true;hard=points;mastery=master;upper=roll;critical=crit;startFlags=st;weaponFlags=wep;
  resultCalls=fills=calculations=allocations=drains=delivered=draws=paid=markerAllocations=markerPosts=plagues=plagueRange=0;packets.clear();attackPercents.clear();
  int start=sub_6FD12950(&game,&owner,238,rank,0),startMana=owner.mana;int effect=-1;
  if(start){if(scene==4)selected=false;if(scene==5)used=false;if(scene==10)releaseContact=false;if(scene==11)owner.mana=0;effect=D2GAME_SKILLS_Handler_6FD12BA0(&game,&owner,238,rank,1,0,0);}
  printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",rank,points,master,roll,crit,st,wep,scene,mana,start,effect,startMana,owner.mana,paid,usedSkill.param,resultCalls,fills,calculations,allocations,drains,delivered,draws,markerAllocations,markerPosts,plagues,plagueRange);first=false;
  for(size_t i=0;i<attackPercents.size();i++)printf("%s%d",i?",":"",attackPercents[i]);printf("],[");for(size_t i=0;i<packets.size();i++){if(i)printf(",");emitPacket(packets[i]);}printf("],%d,%d]",int(marked),marker.deadline);assert(!owner.pCombat);
 }printf("]}\n");}
'''
    generated = BASE / 'druid-rabies-bite-source-probe.cpp'
    generated.write_text(cpp)
    compiler = BASE / 'druid-rabies-bite-compiler.log'
    with compiler.open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(generated.with_suffix(''))], stdout=log, stderr=subprocess.STDOUT, check=True)
    process = subprocess.run([str(generated.with_suffix(''))], capture_output=True)
    sanitizer = BASE / 'druid-rabies-bite-sanitizer.log'
    sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-2000:]
    data = json.loads(process.stdout)
    assert len(data['scalars']) == 2295 and len(data['bites']) == 22464
    raw = pin['currentRows']['skills']['Rabies']

    def endpoint(rank, hard, mastery, maximum=False):
        key = 'EMax' if maximum else 'EMin'
        value, left = int(raw[key]), rank - 1
        for index, count in enumerate([7, 8, 6, 6, 10000], 1):
            take = min(count, left)
            value += take * int(raw[key + 'Lev' + str(index)])
            left -= take
        value <<= 3
        value += value * hard * 20 // 100
        value += value * mastery // 100
        return value

    for rank, hard, mastery, lo, hi, length, mana, ar in data['scalars']:
        assert (lo, hi, length, mana, ar) == (endpoint(rank, hard, mastery), endpoint(rank, hard, mastery, True), 100 + 10 * (rank - 1), 2560, 50 + 10 * (rank - 1))
    for case in data['bites']:
        rank, hard, mastery, upper, critical, st, wep, scene, mana, start, effect, start_mana, end_mana, paid, param, rolls, fills, calcs, allocs, drains, delivered, draws, marker_allocs, posts, plagues, timeout, ars, packets, marked, deadline = case
        attempted = scene not in [1, 2, 3] and mana >= 2560
        success = attempted and st == 1
        cost = 256 if rank == 1 else 2560
        paid_once = success and scene != 12
        expected_start_mana = 0 if attempted and scene == 12 else mana - cost if paid_once else mana
        active = success and scene not in [4, 5]
        assert (start, effect, start_mana, end_mana, paid) == (int(success), int(active) if success else -1, expected_start_mana, 0 if success and scene == 11 else expected_start_mana, int(paid_once)), case
        assert param == (0 if attempted and not success or active else 1 if success else 9), case
        ordinary = active and wep == 1 and scene != 10
        assert (rolls, fills, calcs, allocs, drains, draws) == (int(attempted) + int(active), int(ordinary), int(ordinary) + int(active), 2 * int(active), 2 * int(active), int(active)), case
        assert delivered == (int(ordinary) + int(active and scene != 10))
        assert ars == ([50 + 10 * (rank - 1)] if attempted else []) + ([50 + 15 * (rank - 1)] if active else [])
        new_marker = active and scene != 6
        posted = new_marker and scene != 7
        emitted = posted and scene not in [8, 9]
        length = 100 + 10 * (rank - 1)
        assert (marker_allocs, posts, plagues, timeout, marked, deadline) == (int(new_marker), int(posted), int(emitted), length if emitted else 0, int(posted or scene == 6), 1000 + length if posted else 6000)
        assert len(packets) == 2 * int(active)
        if active:
            flags = wep if scene != 10 else 0
            expected_weapon = [(40 * 256 + 37) * (2 if critical else 1), 11 * 256 + 13, 777, 321, 7, 9, 1 | (8192 if critical else 0), 32, 1] if ordinary else [0, 0, 0, 0, 0, 0, flags, 0, 1]
            lo, hi = endpoint(rank, hard, mastery), endpoint(rank, hard, mastery, True)
            poison = lo + (hi - lo - 1 if upper else 0)
            assert packets == [expected_weapon, [0, 0, poison, length, 0, 0, 1, 2, 0x50]], case
    fixture = BASE / 'druid-rabies-bite-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', sourcePreparation=True, nativeAccepted=False, currentExecutableParity=False,
        legacyCommit=tree['sha'], preflightSha256=sha(pinpath), scalarContexts=2295, biteContexts=22464,
        completeRoutineHashes=routines, pinnedEnumHashes=enums, sourceHashes=hashes,
        generatedSourceSha256=sha(generated), compilerLogSha256=sha(compiler), sanitizerLogSha256=sha(sanitizer),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)),
        scalarColumns=['rank', 'hardCreeper', 'poisonMastery', 'minRate256', 'maxRate256', 'poisonFrames', 'tableMana256', 'skillAttackPercent'],
        biteColumns=['rank', 'hardCreeper', 'mastery', 'upperRoll', 'declaredCritical', 'initialResultFlags', 'ordinaryResultFlags', 'scene', 'initialMana256', 'start', 'do', 'manaAfterStart', 'manaAfterDo', 'debits', 'skillParam', 'resultRolls', 'weaponFills', 'calculations', 'combatAllocations', 'drainCalls', 'declaredDeliveredPackets', 'poisonRolls', 'markerAllocations', 'markerPosts', 'plagues', 'plagueFrames', 'attackPercentArguments', 'drainedPackets', 'marked', 'markerDeadline'],
        packetColumns=['physical256', 'fire256', 'poisonRate256', 'poisonFrames', 'lifeDrain', 'manaDrain', 'resultFlags', 'hitFlags', 'hitClass'],
        established=[
            'Twenty complete older routines compose2295all-rank scalar contexts and22464bounded start/do contacts under ASan/UBSan,using seven original header enums. Full getters apply HitShift3,hard Creeper20percent synergy,poison mastery and100+10per-rank duration. Separate low/high legal poison rolls retain an exclusive max-minus-min span.',
            'Successful start57stores its hit gate before the outer start wrapper pays. Failed start accuracy/block pays nothing in this older source; the ordinary effect handler does not debit again because start57exists and current usemanaondo is blank. The legacy rank1payer uses minmana1despite the current10-mana table; eligibility still requires10. A declared reentrant mana loss also exposes the older ignored-debit-failure path. These are explicit older-source observations,not modern payment acceptance.',
            'Do121clears its success parameter,allocates/drains an ordinary transformed weapon packet,then creates/checks the independent infection marker/emitter,then allocates/drains a separate skill-only poison packet. Current ResultFlags1forces the poison result and HitFlags2prevents a second weapon fill. Weapon physical critical/item poison/length/drains remain solely in the first packet. The complete elemental writer assigns poison hit-class0x50to the second packet; the initial oracle omitted that writer result and was corrected without changing the source body. Ordinary miss/block after a successful start does not suppress the marker or poison packet.',
            'Current source passes Rabies rank to the older transformed helper extra-AR path,which supplies50+15per-rank in the declared wolf row,distinct from initial Rabies50+10per-rank. This does not establish that modern extra AR should be added to the existing native captured-form AR. Missing target/used skill after successful start keeps the paid start but stops the effect.',
            'Existing markers are not refreshed by another initial bite,while its new poison packet still gets full skill length. Failed marker/plague creation does not prevent the poison packet. A declared lost-range drain skips delivery but complete do121still creates the marker/emitter; native cancellation must not silently copy Hunger if the modern rule remains unresolved.',
        ], declaredServices=[
            'Accuracy/block outcomes,weapon/critical fill,damage calculation,HP/mitigation/leech/events/durability and range-sensitive combat drain are explicit services. The complete allocator and two-packet ordering execute around them; actual drain/combat/HP and lethal first-hit/reentrant event behavior are not executed. The statically pinned full drain has a melee-range guard; this probe does not certify a whole modern lost-contact path.',
            'Formula handles/hard-point lookup,mastery stat projection,ratio service,minimal structures,state-list events,room/LOS/missile creation and selected RNG values are declared boundaries. No Fog/real seed/world path/ABI is executed. Unselected item-charge,Blood Mana,delay,quantity and other handler-table branches abort. The matrix models do only after a successful outer start; event scheduling/reentry remains separate.',
        ], remaining=[
            'Current10-mana payment takes reference precedence over the legacy rank1quirk. Modern initial-miss/block payment,ignored debit,extra transformed AR,target-loss attachment and release-rank/gear capture need corroboration or explicit unsupported handling. Do not silently port the legacy oddities or collapse both packets into one generic weapon strike.',
            'Execute actual skill/child missile damage creation and relative-origin/zero-aim factory/path semantics. Then connect the native independent marker/emitter,existing poison-state/mitigation/replacement/cure consumers,and true input/resource/cancellation/save/reload behavior. Owner/history/logout/facet policy and graphical/final effects/icons remain separate. No Rabies handler or installation is enabled here; the installed Hunger build remains preserved.',
        ])
    path = ROOT / 'tools/reference/druid-rabies-bite-engine-evidence.json'
    text = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Rabies bite:2295scalar/22464start-do contexts,20complete routines; distinct weapon/poison packets and legacy payment prepared, no native handler.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
