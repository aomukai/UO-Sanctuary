#!/usr/bin/env python3
"""Compose complete NPC326 start/allocation/fill/mitigation/action/health routines."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    sources,bodies={},{}
    def source(relative,names):
        raw=(BASE/'d2moo-pinned-source'/relative).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        sources[relative]=hashlib.sha256(raw).hexdigest()
        for name in names:bodies[name]=function(raw.decode(),name)
    start='SKILLS_SrvSt32_Conversion_Bash_Stun_Concentrate_BearSmite'
    action='SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws'
    unit='source/D2Game/src/UNIT/SUnitDmg.cpp'
    source(unit,['SUNITDMG_SetHitClass','SUNITDMG_ApplyDamageBonuses','SUNITDMG_RollDamageValueInRange','SUNITDMG_FillDamageValues','SUNITDMG_ApplyResistancesAndAbsorb','SUNITDMG_CalculateTotalDamage','SUNITDMG_AllocCombat','SUNITDMG_GetDamageFromUnits','SUNITDMG_ExecuteEvents','SUNITDMG_DrainItemDurability'])
    source('source/D2Game/src/MONSTER/MonsterMode.cpp',['D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70'])
    source('source/D2Game/src/MONSTER/MonsterUnique.cpp',['MONSTERUNIQUE_CalculatePercentage'])
    source('source/D2Game/src/SKILLS/SkillBar.cpp',[start])
    source('source/D2Game/src/SKILLS/Skills.cpp',[action,'D2GAME_RollElementalDamage_6FD14DD0'])
    for header in ['source/D2Common/include/Units/Units.h','source/D2Common/include/D2Seed.h','source/D2Common/include/DataTbls/MonsterTbls.h']:source(header,[])
    preflight_path=ROOT/'tools/reference/grizzly-preflight.json'
    preflight=json.loads(preflight_path.read_text());npc=preflight['currentRows']['skills']['BearSmite']
    assert [npc[k] for k in ['*Id','srvstfunc','srvdofunc','SrcDam','ResultFlags','calc1','calc2','Param1','Param2','Param3','Param4']]==['326','32','2','128','8','ln34','max(250,ln12)','15','5','15','15']
    assert not any(npc.get(k) for k in ['EType','EMin','EMax','aurastate','auratargetstate','srvoverlay','ToHit','LevToHit'])
    assert preflight['currentRows']['monstats']['druidbear']['Crit']=='5' and preflight['currentRows']['monstats2']['druidbear']['HitClass']=='3'
    seed_header=(BASE/'d2moo-pinned-source/source/D2Common/include/D2Seed.h').read_text()
    assert 'if (nMax > 0)' in seed_header and 'return 0;' in seed_header
    parent=BASE/'spirit-wolf-hit-source-probe.cpp'
    prior=json.loads((ROOT/'tools/reference/spirit-wolf-hit-source-evidence.json').read_text())
    assert sha(parent)==prior['generatedSourceSha256']
    cpp=parent.read_text().split('int main()',1)[0]
    for name in ['SUNITDMG_ApplyDamageBonuses','SUNITDMG_RollDamageValueInRange','SUNITDMG_FillDamageValues','D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70','MONSTERUNIQUE_CalculatePercentage']:
        assert cpp.count(bodies[name])==1,name
    def replace(old,new):
        nonlocal cpp
        assert cpp.count(old)==1,old[:100]
        cpp=cpp.replace(old,new,1)
    replace('#include <vector>','#include <vector>\n#include <cstring>\n#include <cstddef>\n#define __stdcall\n#define D2_MAYBE_UNUSED(x) (void)(x)\n#define D2_ASSERT(x) assert(x)')
    replace('UNIT_ALIGNMENT_GOOD=1','UNIT_ALIGNMENT_GOOD=2')
    replace('struct D2GameStrc{};struct D2UnitStrc{int dwUnitType=0,dwClassId=0,pSeed=0;',
        'struct D2CombatStrc;struct D2GameStrc{int nDifficulty=0,dwGameFrame=1000;bool bExpansion=true;void*pMemoryPool=nullptr;};struct D2UnitStrc{int dwUnitType=0,dwClassId=0,pSeed=0,dwUnitId=0,dwAnimMode=14;uint32_t dwFlags=0;D2GameStrc*pGame=nullptr;D2CombatStrc*pCombat=nullptr;')
    replace('int wResultFlags=0;};','int wResultFlags=0,dwHitClass=0,dwDmgTotal=0,dwAbsLife=0,dwPiercePct=0,nOverlay=0,nHitClassActiveSet=0;};')
    replace('struct D2MonStatsTxt{int wMonType=0,nCrit=5;}', 'struct D2MonStatsTxt{int wMonType=0,nCrit=5,nVelocity=5,nDrain[3]={100,100,100};uint32_t dwMonStatsFlags=0xffffffff;}')
    replace('int ITEMS_RollLimitedRandomNumber(int*,int bound){bounds.push_back(bound);draws++;return high&&bound>0?bound-1:0;}',
        'int ITEMS_RollLimitedRandomNumber(int*,int bound){bounds.push_back(bound);if(bound<=0)return 0;draws++;return high?bound-1:0;}')
    replace('int SUNITDMG_SetHitClass(D2DamageStrc*,unsigned n){assert(n==0x10);hitclasses++;return hitClassResult;}',bodies['SUNITDMG_SetHitClass'])
    all_bodies='\n'.join(bodies.values())
    constants=sorted(set(re.findall(r'\b(?:STAT_|STATE_|UNITEVENT_)\w+',all_bodies))-set(re.findall(r'\b(?:STAT_|STATE_|UNITEVENT_)\w+',cpp)))
    extra='\n'.join(f'constexpr int {name}={1000+i};' for i,name in enumerate(constants))
    declarations=r'''
constexpr int UNIT_OBJECT=2,MONSTATSFLAGINDEX_INTOWN=10,MONSTATSFLAGINDEX_KILLABLE=15,DIFFMODE_NIGHTMARE=1,DIFFMODE_HELL=2;
constexpr uint32_t UNITFLAG_SKSRVDOFUNC=0x40,UNITFLAG_ISREVIVE=0x80000000;
constexpr int EVENTTYPE_REMOVESTATE=1,EVENTTYPE_STATREGEN=2,ITEMTYPE_ANY_ARMOR=3;
constexpr int BODYLOC_HEAD=1,BODYLOC_TORSO=3,BODYLOC_RARM=4,BODYLOC_LARM=5,BODYLOC_BELT=8,BODYLOC_FEET=6,BODYLOC_GLOVES=7;
struct Trace{int kind,a=0,b=0;};std::vector<Trace>trace;
'''
    cpp=cpp.replace('struct D2DamageStrc{',extra+'\n'+declarations+'\nstruct D2DamageStrc{',1)
    replace('void SUNITEVENT_Trigger(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){events++;}',
        'void SUNITEVENT_Trigger(D2GameStrc*,int event,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*d){events++;trace.push_back({20,event,d->dwPhysDamage});}')
    replace('void STATLIST_SetUnitStat(D2UnitStrc* u,int stat,int value,int){u->stats[stat]=value;}',
        'void STATLIST_SetUnitStat(D2UnitStrc* u,int stat,int value,int){u->stats[stat]=value;if(stat==STAT_HITPOINTS)trace.push_back({21,u->dwUnitId,value});}')
    cpp+=r'''
enum D2DamageReductionType{DAMAGE_REDUCTION_NONE=0,DAMAGE_REDUCTION_PHYSICAL=1,DAMAGE_REDUCTION_MAGICAL=2};
struct D2DamageStatTableStrc{int nOffsetInDamageStrc,nResStatId,nMaxResStatId,nPierceStatId,nAbsorbPctStatId,nAbsorbStatId;D2DamageReductionType nDamageReductionType;int unk0x1C,unk0x20;const char*szName;uint8_t unk0x28,pad0x29,pad0x2A,pad0x2B;};
struct D2DifficultyLevelsTxt{int dwResistPenalty=0,dwHireableBossDmgPercent=25,dwLifeStealDiv=1,dwManaStealDiv=1;};
struct D2DamageInfoStrc{D2GameStrc*pGame;D2DifficultyLevelsTxt*pDifficultyLevelsTxt;D2UnitStrc*pAttacker;D2UnitStrc*pDefender;int bAttackerIsMonster,bDefenderIsMonster;D2DamageStrc*pDamage;int nDamageReduction[3];};
struct D2CombatStrc{D2GameStrc*pGame;int dwAttackerType,dwAttackerId,dwDefenderType,dwDefenderId;D2DamageStrc tDamage;D2CombatStrc*pNext;};
struct D2SkillsTxt{int dwCalc[4]={0,1,2,3},wResultFlags=8,dwHitFlags=0,dwHitClass=0,nEType=0,nAuraState=-1,wAuraTargetState=-1,wSrvOverlay=0,dwAuraLenCalc=0;uint8_t nSrcDam=128;}skill;
struct D2StatListStrc{};
struct D2DurabilityLossStrc{int nBodyLoc,nWeight;};
struct Table{int nStatesTxtRecordCount=200,nOverlayTxtRecordCount=400;}tables;auto*sgptDataTables=&tables;
D2DifficultyLevelsTxt difficulty;D2CombatStrc allocated;D2UnitStrc attacker,defender;D2GameStrc game;unsigned gdwBitMasks[32];
struct D2ActiveRoomStrc{}room;bool hasRow=true,hasTarget=true,town=false,hasRoom=true,interact=true,inRange=true;int resultFlags=1,npcRank=1,rangeBonus=-1,modeCalls=0,thornsCalls=0,freeCalls=0;D2DamageStrc dispatched;
#define D2_ALLOC_STRC_POOL(pool,type) (&allocated)
template<class T>T D2Clamp(T x,T lo,T hi){return std::clamp(x,lo,hi);}
bool MONSTERS_IsBoss(D2MonStatsTxt*,D2UnitStrc*){return false;}
bool MONSTERS_IsPrimeEvil(D2UnitStrc*){return false;}
bool MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc*,unsigned){return false;}
bool STATES_CheckState(D2UnitStrc*,int){return false;}
D2DifficultyLevelsTxt*DATATBLS_GetDifficultyLevelsTxtRecord(int){return &difficulty;}
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){return hasRow&&id==326?&skill:nullptr;}
D2UnitStrc*SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return hasTarget?&defender:nullptr;}
D2ActiveRoomStrc*UNITS_GetRoom(D2UnitStrc*){return hasRoom?&room:nullptr;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return town;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int rank){return calc==0?15*rank:calc==1?std::max(250,15+5*(rank-1)):0;}
int SKILLS_GetToHitFactor(D2UnitStrc*,int,int){return 0;}
void sub_6FD15470(D2UnitStrc*,int value){assert(value==0);trace.push_back({1,value});}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int ar,int){assert(ar==0);trace.push_back({2,resultFlags});return resultFlags;}
int SKILLS_GetMinElemDamage(D2UnitStrc*,int,int,int){return 0;}
int SKILLS_GetMaxElemDamage(D2UnitStrc*,int,int,int){return 0;}
int SKILLS_GetElementalLength(D2UnitStrc*,int,int,int){return 0;}
void sub_6FD11E40(D2UnitStrc*,D2DamageStrc*,int type,int damage,int length,int,int){assert(type==0&&damage==0&&length==0);}
D2StatListStrc*STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return nullptr;}
D2StatListStrc*STATLIST_AllocStatList(void*,unsigned,int,int,int){__builtin_trap();}
void STATLIST_SetState(D2StatListStrc*,int){__builtin_trap();}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){__builtin_trap();}
void sub_6FD10E50(D2UnitStrc*,int,D2StatListStrc*){__builtin_trap();}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)(D2UnitStrc*,int,D2StatListStrc*)){__builtin_trap();}
void STATLIST_SetExpireFrame(D2StatListStrc*,int){__builtin_trap();}
void STATES_ToggleState(D2UnitStrc*,int,int){__builtin_trap();}
void sub_6FCFE0E0(D2UnitStrc*,D2StatListStrc*,D2SkillsTxt*,int,int){__builtin_trap();}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){__builtin_trap();}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){__builtin_trap();}
bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return interact;}
bool SUNIT_IsDead(D2UnitStrc*u){return !u||u->stats[STAT_HITPOINTS]<=0;}
void UNITS_StoreLastAttacker(D2UnitStrc*,D2UnitStrc*){trace.push_back({3});}
D2MonStatsTxt*SUNITDMG_GetMonStatsTxtRecordFromUnit(D2UnitStrc*){return &mon;}
void D2COMMON_11013_ConvertMode(D2UnitStrc*,int*,int*,int*,const char*,int){__builtin_trap();}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*){return 10000000;}
void STATLIST_AddUnitStat(D2UnitStrc*u,int stat,int n,int){u->stats[stat]+=n;}
void SUNITDMG_AddLeechedLife(D2UnitStrc*,int){__builtin_trap();}
void SUNITDMG_AddLeechedMana(D2UnitStrc*,int){__builtin_trap();}
void sub_6FC6E890(D2GameStrc*,D2UnitStrc*){trace.push_back({4});}
void sub_6FCF8DD0(D2UnitStrc*){trace.push_back({5});}
void SUNITDMG_ApplyColdState(D2UnitStrc*,D2UnitStrc*,int n){trace.push_back({6,n});}
void SUNITDMG_ApplyFreezeState(D2UnitStrc*,D2UnitStrc*,int n){assert(n==0);}
void SUNITDMG_ApplyPoisonDamage(D2UnitStrc*,D2UnitStrc*,int rate,int length){assert(rate==0&&length==0);}
void SUNITDMG_ApplyBurnDamage(D2UnitStrc*,D2UnitStrc*,int rate,int length){assert(rate==0&&length==0);}
bool UNITS_IsInMeleeRange(D2UnitStrc*,D2UnitStrc*,int bonus){rangeBonus=bonus;return inRange;}
int UNITS_GetHitClass(D2UnitStrc*){return 3;}
void ITEMS_UpdateDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){__builtin_trap();}
D2UnitStrc*INVENTORY_GetItemFromBodyLoc(void*,int){__builtin_trap();}
void SKILLS_ApplyThornsDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){thornsCalls++;trace.push_back({7});}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*d){modeCalls++;dispatched=*d;trace.push_back({8,d->wResultFlags,d->dwHitClass});}
void SUNITDMG_FreeAttackerDefenderCombatList(D2GameStrc*,D2UnitStrc*a,D2UnitStrc*){a->pCombat=nullptr;freeCalls++;trace.push_back({9});}
'''
    for name in ['SUNITDMG_ApplyResistancesAndAbsorb','SUNITDMG_CalculateTotalDamage','SUNITDMG_AllocCombat','SUNITDMG_GetDamageFromUnits','D2GAME_RollElementalDamage_6FD14DD0',start,'SUNITDMG_ExecuteEvents','SUNITDMG_DrainItemDurability',action]:cpp+='\n'+bodies[name]+'\n'
    cpp+=r'''
bool first=true;
void emit(int r,int minimum,int maximum,int percent,int typed,int hi,int crit,int resistance,int control,int flags){
game={};attacker={};defender={};allocated={};skill={};npcRank=r;resultFlags=flags;
attacker.dwUnitType=defender.dwUnitType=UNIT_MONSTER;attacker.dwClassId=428;defender.dwClassId=28;attacker.dwUnitId=17;defender.dwUnitId=29;
attacker.pGame=defender.pGame=&game;attacker.pInventory=defender.pInventory=nullptr;
attacker.stats[STAT_MINDAMAGE]=minimum;attacker.stats[STAT_MAXDAMAGE]=maximum;attacker.stats[STAT_DAMAGEPERCENT]=percent;
attacker.stats[STAT_HITPOINTS]=defender.stats[STAT_HITPOINTS]=10000000;
if(typed){attacker.stats[STAT_FIREMINDAM]=2;attacker.stats[STAT_FIREMAXDAM]=6;attacker.stats[STAT_LIGHTMINDAM]=3;attacker.stats[STAT_LIGHTMAXDAM]=7;attacker.stats[STAT_COLDMINDAM]=5;attacker.stats[STAT_COLDMAXDAM]=8;attacker.stats[STAT_COLDLENGTH]=31;}
for(int stat:{STAT_DAMAGERESIST,STAT_FIRERESIST,STAT_LIGHTRESIST,STAT_COLDRESIST})defender.stats[stat]=resistance;
high=hi;criticalRoll=crit;draws=events=modeCalls=thornsCalls=freeCalls=0;rangeBonus=-1;dispatched={};bounds.clear();trace.clear();hasRow=control!=6;hasTarget=control!=7;town=control==8;hasRoom=interact=inRange=true;
int start=SKILLS_SrvSt32_Conversion_Bash_Stun_Concentrate_BearSmite(&game,&attacker,326,r);
D2DamageStrc saved=attacker.pCombat?attacker.pCombat->tDamage:D2DamageStrc{};
int afterStart=defender.stats[STAT_HITPOINTS],startDraws=draws;
if(control==1)inRange=false;if(control==2)attacker.dwAnimMode=0;if(control==3)hasTarget=false;if(control==4)hasRoom=false;if(control==5)interact=false;
if(control==9)defender.stats[STAT_HITPOINTS]=100*256;
if(control==10)defender.stats[STAT_HITPOINTS]=0;
// Different release stats/rank cannot rewrite an already allocated packet.
attacker.stats[STAT_DAMAGEPERCENT]=900;defender.stats[STAT_DAMAGERESIST]=0;defender.stats[STAT_FIRERESIST]=0;defender.stats[STAT_LIGHTRESIST]=0;defender.stats[STAT_COLDRESIST]=0;
int action=SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws(&game,&attacker,326,r+1);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],[",first?"":",",r,minimum,maximum,percent,typed,hi,crit,resistance,control,flags,start,action,afterStart,saved.dwPhysDamage,saved.dwFireDamage,saved.dwLtngDamage,saved.dwColdDamage,saved.dwDmgTotal,saved.dwStunLen,saved.wResultFlags,saved.dwHitClass,defender.stats[STAT_HITPOINTS],startDraws,draws,rangeBonus,modeCalls,thornsCalls,freeCalls,dispatched.wResultFlags,dispatched.dwPhysDamage,saved.dwColdLen,dispatched.dwColdLen,dispatched.dwHitClass);first=false;
bool sep=false;for(int bound:bounds){printf("%s%d",sep?",":"",bound);sep=true;}printf("],[");sep=false;for(auto&t:trace){printf("%s[%d,%d,%d]",sep?",":"",t.kind,t.a,t.b);sep=true;}printf("]]");
}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("{\"packets\":[");int endpoints[4][2]={{30,60},{496,526},{0,0},{4200,4200}};
for(int rank:{1,2,48,49})for(auto&p:endpoints)for(int percent:{-200,0,25,295})for(int typed:{0,1})for(int hi:{0,1})for(int crit:{0,5})for(int resistance:{0,33,100})emit(rank,p[0],p[1],percent,typed,hi,crit,resistance,0,1);
for(int control=1;control<=10;control++)for(int flags:{0,1,16,128,32768})for(int typed:{0,1})emit(1,30,60,25,typed,0,5,33,control,flags);
printf("],\"reduction\":[");first=true;
for(int value:{0,1,255,256,1048575,1048576,1048577,1048599,1048600,2000003,16640000})for(int resist:{-100,-33,0,33,50,99,100,150})for(int mdr:{0,1,100})for(int pct:{0,37,100})for(int flat:{0,1}){
if(value<mdr*256)continue;attacker={};defender={};game={};trace.clear();attacker.dwUnitType=defender.dwUnitType=UNIT_MONSTER;
defender.stats[STAT_FIRERESIST]=resist;defender.stats[STAT_MAGIC_DAMAGE_REDUCTION]=mdr;defender.stats[STAT_ITEM_ABSORBFIRE_PERCENT]=pct;defender.stats[STAT_ITEM_ABSORBFIRE]=flat;
D2DamageStrc damage;damage.dwFireDamage=value;SUNITDMG_CalculateTotalDamage(&game,&attacker,&defender,&damage);
printf("%s[%d,%d,%d,%d,%d,%d,%d]",first?"":",",value,resist,mdr,pct,flat,damage.dwFireDamage,damage.dwAbsLife);first=false;
}printf("]}\n");}
'''
    path=BASE/'bear-smite-source-probe.cpp';path.write_text(cpp)
    binary=path.with_suffix('')
    with (BASE/'bear-smite-compiler.log').open('w') as log:
        subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
    process=subprocess.run([str(binary)],capture_output=True)
    (BASE/'bear-smite-sanitizer.log').write_bytes(process.stderr)
    assert process.returncode==0 and not process.stderr,process.stderr.decode()[-3000:]
    data=json.loads(process.stdout);cases=data['packets'];reductions=data['reduction']
    assert len(cases)==1636 and len(reductions)==1248,(len(cases),len(reductions))
    def pct(value,percent):
        if value>1048576:return percent*(value//100)
        result=value*percent
        return (abs(result)//100)*(1 if result>=0 else -1)
    for context,out,bounds,trace in cases:
        rank,minimum,maximum,percent,typed,high,critical,resistance,control,flags=context
        valid=control not in [6,7,8]
        assert out[0]==int(valid) and out[1]==int(control not in [3,6,7])
        assert out[8]==0,'NPC326must not invent stun'
        assert out[2]==10000000,'Start mutated health'
        assert out[12]==out[13],'Release rerolled damage'
        flat=max(250,15+5*(rank-1))*256 if valid else 0
        expected=[0,0,0,0];expected_bounds=[]
        if valid and flags==1:
            bonus=max(-90,percent+15*rank)
            low,upper=max(1,minimum)*256,max(2,maximum)*256
            if upper<=low:upper=low+256
            low+=pct(low,bonus);upper+=pct(upper,bonus)
            expected[0]=low+(upper-low-1 if high and upper>low else 0)
            expected_bounds=[0]+([upper-low] if upper>low else [])
            if typed:
                for side,(lo,hi) in enumerate([(2,6),(3,7),(5,8)],1):
                    expected[side]=lo*256+((hi-lo)*256-1 if high else 0);expected_bounds.append((hi-lo)*256)
            expected_bounds.append(-1)
            if critical<5:expected=[n*2 for n in expected]
            expected=[pct(n,100-resistance) if resistance else n for n in expected]
        assert out[3:8]==[expected[0]+flat,*expected[1:],sum(expected)],(context,out,expected)
        assert out[20]==(pct(31,100-resistance) if typed and valid and flags==1 and resistance else 31 if typed and valid and flags==1 else 0)
        assert bounds==expected_bounds,(context,bounds,expected_bounds)
        release=valid and control not in [1,2,3,4,5,10] and flags==1
        initial=100*256 if control==9 else 0 if control==10 else 10000000
        health=initial-sum(expected) if release else initial
        if release and health<256:health=0
        assert out[11]==health,(context,out[11],health)
        if valid and control not in [2,3] and flags==1:assert out[14]==3
        if control==0 and flags==1:
            assert out[15:18]==[1,1,1]
            assert out[19]==min(out[3],10000000)
    differences=0
    for value,resist,mdr,absorb,flat,reduced,healed in reductions:
        after=value-mdr*256
        if resist:after=pct(after,100-min(100,max(-100,resist)))
        percent=pct(after,min(absorb,40)) if absorb>0 else 0
        fixed=min(max(0,flat*256),after-percent)
        assert [reduced,healed]==[after-percent-fixed,percent+fixed]
        rational=(value-mdr*256)*max(0,min(200,100-resist))//100
        rational_abs=rational*min(absorb,40)//100
        rational_flat=min(max(0,flat*256),rational-rational_abs)
        differences+=int([reduced,healed]!=[rational-rational_abs-rational_flat,rational_abs+rational_flat])
    assert differences>0
    fixture=BASE/'bear-smite-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,nativeConsumer=False,
        preflightSha256=sha(preflight_path),eventCodes={name:1000+i for i,name in enumerate(constants) if name.startswith('UNITEVENT_')},sourceHashes=sources,unchangedRoutineHashes={name:hashlib.sha256(body.encode()).hexdigest() for name,body in bodies.items()},reusedFillAbiSha256=sha(parent),
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=sha(fixture),packetCases=len(cases),reductionCases=len(reductions),largeValueRationalDifferences=differences,generatedSourceSha256=sha(path),sanitizers='ASan+UBSan'),
        established=['Complete start32,allocation,get-combat,unarmed fill/critical/hit-class,percentage,resistance/total,event health,do2and durability wrapper are composed. SourceNPC326damage percent/flat field and unchanged rank-independent do2are distinct. Start does not commit HP;release uses the stored packet despite changed rank/damage/resistance services.','Complete current-table row checks pin326/start32/do2/SrcDam128/ResultFlags8,rank-one15percent and max250formula,zero skill elements/AR,and Grizzly Crit5/hit-class3. Zero-bound elemental request consumes no supplied RNG draw as supported by the pinned seed helper. NPC326never writes stun in these contexts. Knockback result flag8is prepared on successful start;post-allocation physical addition remains separate from the already computed health total. Full event health executes rather than a copied health block.','A separate1248-context complete resistance/absorb matrix includes values around1048576FP and reveals272differences from always-multiply-first rational mitigation. Source skips the percentage helper at zero resistance,uses its large-value early division otherwise,and also uses it for percent absorption. Native correction requires its own acceptance;the current native reducer is not certified here.'],
        services=['Target/row/formula/AR-result flags,zero NPC elemental getter/fill,bounded RNG endpoints,world/room/interaction/range and stat-map layouts are explicit. Successful NPC326row has no self/target aura or server overlay;unavailable state/equipment/leech paths trap.','Final ExecuteMissileDamage mode dispatch,Thorns event callbacks,cold-state receiver and global event handlers are explicit recording services;this is not full monster death/knockback geometry or modern executable equivalence. Target/control contradictions are synthetic branch coverage,not modern-reachability claims.'])
    output=ROOT/'tools/reference/bear-smite-engine-evidence.json';rendered=json.dumps(evidence,indent=2)+'\n'
    if check:assert output.read_text()==rendered
    else:output.write_text(rendered)
    print(f'BearSmite complete start/allocation/health/action composition:{len(cases)}packets/{len(reductions)}reducers pass ASan+UBSan;native/sequence/world remain separate.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
