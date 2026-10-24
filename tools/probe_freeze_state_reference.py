#!/usr/bin/env python3
"""Execute unchanged freeze application/removal, Ice Arrow callback and mitigation order."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
blocks={};hashes={}
def routine(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
 hashes[rel]=hashlib.sha256(raw).hexdigest();s=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name
 end=s.index('{',m.start())+1;depth=1
 while depth:depth+=(s[end]=='{')-(s[end]=='}');end+=1
 result=s[m.start():end];blocks[name]=hashlib.sha256(result.encode()).hexdigest();return result
unit='source/D2Game/src/UNIT/SUnitDmg.cpp';miss='source/D2Game/src/MISSILES/MissMode.cpp';unique='source/D2Game/src/MONSTER/MonsterUnique.cpp'
remove=routine(unit,'SUNITDMG_RemoveFreezeState');apply=routine(unit,'SUNITDMG_ApplyFreezeState');callback=routine(miss,'MISSMODE_SrvDmg02_IceArrow_RoyalStrikeChaos');percentage=routine(unique,'MONSTERUNIQUE_CalculatePercentage');total=routine(unit,'SUNITDMG_CalculateTotalDamage');resist=routine(unit,'SUNITDMG_ApplyResistancesAndAbsorb')
constants=sorted(set(re.findall(r'\b(?:STAT_|STATE_|DAMAGEHITFLAG_|UNITFLAG_|DIFFMODE_|UNITEVENT_)\w+',total+resist+apply+remove+callback)))
prefix=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstddef>
#include <iterator>
#include <map>
#include <cassert>
#define __fastcall
#define D2_MAYBE_UNUSED(x) (void)(x)
#define D2_ASSERT(x) assert(x)
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,EVENTTYPE_REMOVESTATE=1,EVENTTYPE_AITHINK=2;
'''+''.join('constexpr int '+s+'='+str(i+10)+';\n' for i,s in enumerate(constants))+r'''
enum D2DamageReductionType{DAMAGE_REDUCTION_NONE=0,DAMAGE_REDUCTION_PHYSICAL=1,DAMAGE_REDUCTION_MAGICAL=2};
struct D2MonStatsTxt{int nColdEffect[3]={-50,-40,-25};int nAIdel[3]={15,14,13};};
struct D2MonsterDataStrc{D2MonStatsTxt* pMonstatsTxt;};
struct D2DifficultyLevelsTxt{int dwMonsterFreezeDiv=1,dwResistPenalty=0,dwHireableBossDmgPercent=25;};
struct D2GameStrc{int nDifficulty=0,dwGameFrame=1000,nGameType=0,dwGameType=0;bool bExpansion=true;void* pMemoryPool=nullptr;};
struct D2StatListStrc{int expiry=0,owner=0;};
struct D2UnitStrc{int dwUnitType=1,dwClassId=28,dwUnitId=17,dwFlags=0;D2GameStrc* pGame;D2MonsterDataStrc* pMonsterData;bool freeze=false,dead=false,stay=false,uninterruptable=false,boss=false,unique=false,hireling=false;std::map<int,int> stats;D2StatListStrc list;};
struct D2DamageStrc{int dwPhysDamage=0,dwFireDamage=0,dwLtngDamage=0,dwColdDamage=0,dwMagDamage=0,dwPoisDamage=0,dwPoisLen=0,dwColdLen=0,dwFrzLen=0,dwLifeLeech=0,dwManaLeech=0,dwStamLeech=0,dwAbsLife=0,dwDmgTotal=0,dwPiercePct=0,dwHitFlags=0,dwBurnLen=0;};
struct D2DamageStatTableStrc{int nOffsetInDamageStrc,nResStatId,nMaxResStatId,nPierceStatId,nAbsorbPctStatId,nAbsorbStatId;D2DamageReductionType nDamageReductionType;int unk0x1C,unk0x20;const char* szName;uint8_t unk0x28,pad0x29,pad0x2A,pad0x2B;};
struct D2DamageInfoStrc{D2GameStrc* pGame;D2DifficultyLevelsTxt* pDifficultyLevelsTxt;D2UnitStrc* pAttacker;D2UnitStrc* pDefender;int bAttackerIsMonster,bDefenderIsMonster;D2DamageStrc* pDamage;int nDamageReduction[3];};
struct D2MissilesTxt{int dwDmgParam[1]={100};};
D2GameStrc game;D2UnitStrc attacker,defender,missile;D2MonStatsTxt mon;D2MonsterDataStrc monsterData{&mon};D2DifficultyLevelsTxt difficulty[3]={{1},{2},{4}};D2MissilesTxt row;bool known=true;int coldCalls=0,coldLength=0,removeEvent=0,aiEvent=0,deletedAi=0,combatMode=0;
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return known?&mon:nullptr;}
bool MONSTERS_IsBoss(D2MonStatsTxt*,D2UnitStrc* u){return u && u->boss;}
bool MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc* u,unsigned){return u->unique;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc* u){return u && u->hireling;}
bool MONSTERS_IsPrimeEvil(D2UnitStrc*){return false;}
bool MONSTERS_IsUndead(D2UnitStrc*){return false;}
bool MONSTERS_IsDemon(D2UnitStrc*){return false;}
bool STATES_CheckState(D2UnitStrc* u,int state){return state==STATE_UNINTERRUPTABLE && u->uninterruptable;}
D2DifficultyLevelsTxt* DATATBLS_GetDifficultyLevelsTxtRecord(int d){return &difficulty[d];}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* u,int){return u->freeze?&u->list:nullptr;}
int STATLIST_GetExpireFrame(D2StatListStrc* l){return l->expiry;}
void STATLIST_SetExpireFrame(D2StatListStrc* l,int at){l->expiry=at;}
void STATES_ToggleState(D2UnitStrc* u,int,int set){u->freeze=set;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int at,int,int owner){defender.list.expiry=at;defender.list.owner=owner;return &defender.list;}
void STATLIST_SetState(D2StatListStrc*,int){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)(D2UnitStrc*,int,D2StatListStrc*)){}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int at,int,int){(type==EVENTTYPE_REMOVESTATE?removeEvent:aiEvent)=at;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){deletedAi++;}
void SUNITDMG_ApplyColdState(D2UnitStrc*,D2UnitStrc*,int frames){coldCalls++;coldLength=frames;}
bool SUNIT_IsDead(D2UnitStrc* u){return u->dead;}
bool STATES_CheckStateMaskStayDeathOnUnitByStateId(D2UnitStrc* u,int){return u->stay;}
void SUNIT_SetCombatMode(D2GameStrc*,D2UnitStrc*,int){combatMode++;}
void FOG_DisplayWarning(const char*,const char*,int){}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return known?&row:nullptr;}
int STATLIST_UnitGetStatValue(D2UnitStrc* u,int stat,int){return u?u->stats[stat]:0;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* u,int stat,int){return u?u->stats[stat]:0;}
void SUNITEVENT_Trigger(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){}
template<class T>T D2Clamp(T x,T lo,T hi){return std::clamp(x,lo,hi);}
void reset(){game={};attacker={};defender={};missile={};attacker.dwUnitId=17;attacker.dwUnitType=0;attacker.pGame=defender.pGame=&game;defender.pMonsterData=&monsterData;known=true;coldCalls=coldLength=removeEvent=aiEvent=deletedAi=combatMode=0;}
'''
cpp=prefix+'\n'+percentage+'\n'+remove+'\n'+apply+'\n'+callback+'\n'+resist+'\n'+total+r'''
int main(){bool first=true;std::printf("{\"state\":[");
for(int type:{0,1,2})for(int diff:{0,1,2})for(int hasRow:{0,1})for(int effect:{-50,0,33})for(int frames:{-1,0,1,3,4,101,301})for(int existing:{0,1})for(int flags=0;flags<16;flags++){
 reset();game.nDifficulty=diff;defender.dwUnitType=type;known=hasRow;mon.nColdEffect[diff]=effect;defender.freeze=existing;defender.list.expiry=existing?1200:0;defender.list.owner=existing?41:0;
 defender.uninterruptable=flags&1;defender.boss=flags&2;defender.unique=flags&4;defender.hireling=flags&8;SUNITDMG_ApplyFreezeState(&attacker,&defender,frames);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",type,diff,hasRow,effect,frames,existing,flags,defender.freeze,defender.list.expiry,defender.list.owner,coldCalls,coldLength,removeEvent,aiEvent);
}
std::printf("],\"remove\":[");first=true;
for(int type:{0,1,2})for(int dead:{0,1})for(int stay:{0,1})for(int hasData:{0,1})for(int hasRow:{0,1})for(int mode:{0,1,2})for(int diff:{0,1,2})for(int delay:{-1,0,7}){
 reset();defender.freeze=true;defender.dwUnitType=type;defender.dead=dead;defender.stay=stay;defender.pMonsterData=hasData?&monsterData:nullptr;monsterData.pMonstatsTxt=hasRow?&mon:nullptr;game.nGameType=mode==1;game.dwGameType=mode==2;game.nDifficulty=diff;for(int i=0;i<3;i++)mon.nAIdel[i]=delay+i*10;
 SUNITDMG_RemoveFreezeState(&defender,STATE_FREEZE,&defender.list);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",type,dead,stay,hasData,hasRow,mode,diff,delay,defender.freeze,combatMode,aiEvent);
}
std::printf("],\"callback\":[");first=true;
for(int hasMissile:{0,1})for(int hasRow:{0,1})for(int pct:{-1,0,50,100,150})for(int frames:{0,1,3,50,145}){
 reset();known=hasRow;row.dwDmgParam[0]=pct;missile.stats[STAT_COLDLENGTH]=frames;D2DamageStrc d;d.dwColdLen=66;d.dwFrzLen=99;d.dwPhysDamage=123;d.dwColdDamage=789;MISSMODE_SrvDmg02_IceArrow_RoyalStrikeChaos(&game,hasMissile?&missile:nullptr,&defender,&d);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d]",hasMissile,hasRow,pct,frames,d.dwColdLen,d.dwFrzLen,d.dwPhysDamage,d.dwColdDamage);
}
std::printf("],\"length\":[");first=true;
for(int frames:{1,2,3,5,50,101,145})for(int resistance:{-100,-33,0,33,50,99,100,150})for(int cannot:{0,1})for(int half:{0,1}){
 reset();defender.stats[STAT_COLDRESIST]=resistance;defender.stats[STAT_ITEM_CANNOTBEFROZEN]=cannot;defender.stats[STAT_ITEM_HALFFREEZEDURATION]=half;D2DamageStrc d;d.dwColdLen=d.dwFrzLen=frames;SUNITDMG_CalculateTotalDamage(&game,&attacker,&defender,&d);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",frames,resistance,cannot,half,d.dwColdLen,d.dwFrzLen);
}std::printf("]}\n");}
'''
path=STAGE/'freeze-state-source-probe.cpp';path.write_text(cpp);binary=STAGE/'freeze-state-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data['state'])==12096 and len(data['remove'])==1296 and len(data['callback'])==100 and len(data['length'])==224
for frames,resistance,cannot,half,cold,freeze in data['length']:
 expected=0 if cannot else (frames//2 if half else frames)*(100-min(100,max(-100,resistance)))//100
 assert cold==freeze==expected,(frames,resistance,cannot,half,cold,freeze,expected)
fixture=STAGE/'freeze-state-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
pin=ROOT/'tools/reference/weapon-leech-difficulty-source.json';diff=json.loads(pin.read_text());divisors=[diff[x]['MonsterFreezeDivisor'] for x in ['Normal','Nightmare','Hell']];assert divisors==[1,2,4]
compiledBlocks=blocks.copy()
reviewed={}
for rel,name in [('source/D2Game/src/GAME/Event.cpp','D2GAME_InitTimer_6FC351D0'),('source/D2Common/src/Monsters/Monsters.cpp','MONSTERS_IsBoss'),('source/D2Game/src/MONSTER/MonsterUnique.cpp','D2GAME_SpawnMonster_6FC6F220')]:
 routine(rel,name);reviewed[name]=blocks[name]
statePath=ROOT/'tools/reference/d2r-named-equipment/json/states.json';states=json.loads(statePath.read_text());freezeRow=next(v for v in states.values() if v.get('state')=='freeze')
e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(compiledBlocks),blockHashes=compiledBlocks,reviewedBlockHashes=reviewed,pinnedFreezeState=dict(path=str(statePath.relative_to(ROOT)),sha256=hashlib.sha256(statePath.read_bytes()).hexdigest(),row=freezeRow),difficultySource=dict(path=str(pin.relative_to(ROOT)),sha256=hashlib.sha256(pin.read_bytes()).hexdigest(),freezeDivisors=divisors),probe=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),stateCases=12096,removalCases=1296,iceCallbackCases=100,lengthCases=224),established=['Complete unchanged Ice Arrow callback converts captured cold length to freeze length and clears chill length without converting physical damage.','Complete unchanged freeze application retains original owner and the longer active expiry; source boss/unique-bit8/hireling paths delegate to cold, while uninterruptable and nonnegative ColdEffect gates are separate. Monster freeze division has no cold-state minimum-one clamp.','Complete unchanged removal honors the supplied death-stay mask, restores combat mode, and schedules normal or game-type-specific AI delay with15-frame fallback.','Complete unchanged total-damage and resistance routines establish cannot-freeze/half-duration handling before resistance. Odd input lengths distinguish the previous native helper order. Cold/freeze length lanes have no physical or magical flat reduction.'],limits=['Stat/event/AI/mode/classification/world services are declared harness inputs. This is bounded older engine execution, not a full modern executable certificate. Player/PvP/hireling resistance special modes are outside the length matrix, which supplies a non-hireling monster defender and ordinary player attacker.','The reviewed event initializer moves expiry at or before the current frame to the next frame. Pinned freeze state has monstaydeath/hide/shatter/udead; this is distinct from ordinary cold random SHATTER. Exact death animation and full native event/corpse presentation remain acceptance work.','Cold fallback is a declared delegation; the separate cold-state evidence compiles its implementation. Event services record requested frames without executing the source scheduler. Raw zero-length-after-division expiry, AI wake timing, native mode mapping, source death/corpse masks and runtime persistence require individual integration checks.'])
out=ROOT/'tools/reference/freeze-state-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Freeze source evidence stale'
else:out.write_text(encoded)
print('Freeze state:12096 application,1296 removal,100 Ice Arrow callback and224 complete length-mitigation cases compiled.')
