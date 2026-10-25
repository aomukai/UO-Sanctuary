#!/usr/bin/env python3
"""Compose complete owner/allegiance, flags, mitigation, event health and mode dispatch.

Retained registry/stats and empty event subscribers are declared contexts. This
is not corpse retention, full death entry, or current executable confirmation.
"""
from pathlib import Path
import argparse,hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run([sys.executable,str(ROOT/'tools/probe_sentry_owner_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};routines={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def extract(s,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;i=s.index('{',m.start())+1;depth=1
  while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
  b=s[m.start():i];routines[name]=hashlib.sha256(b.encode()).hexdigest();return b+'\n'
 dmg=source('source/D2Game/src/UNIT/SUnitDmg.cpp');miss=source('source/D2Game/src/MISSILES/MissMode.cpp');unit=source('source/D2Game/src/UNIT/SUnit.cpp');head=source('source/D2Game/include/UNIT/SUnitDmg.h')
 bodies=[extract(unit,'SUNIT_IsDead'),extract(dmg,'SUNITDMG_GetMonStatsTxtRecordFromUnit')]+[extract(dmg,n) for n in ['SUNITDMG_ApplyResistancesAndAbsorb','SUNITDMG_CalculateTotalDamage','SUNITDMG_ExecuteEvents','SUNITDMG_ExecuteMissileDamage']]+[extract(miss,'MISSMODE_SetDamageFlags')]
 code='\n'.join(bodies)
 base=(STAGE/'sentry-owner-source-probe.cpp').read_text().split('int main()',1)[0]
 base=base.replace('#include <initializer_list>','#include <initializer_list>\n#include <algorithm>\n#include <map>\n#include <cstddef>\n#include <cmath>\nusing BOOL=int;')
 base=base.replace('struct D2UnitStrc{int dwUnitType=', 'struct D2MonStatsTxt;struct D2MonsterDataStrc{D2MonStatsTxt* pMonstatsTxt=nullptr;};struct D2UnitStrc{D2GameStrc* pGame=nullptr;D2MonsterDataStrc* pMonsterData=nullptr;void* pInventory=nullptr;std::map<int,int> stats;int pSeed=0,dwLastHitClass=0;int dwUnitType=')
 base=base.replace('struct D2GameStrc{D2UnitStrc*','struct D2GameStrc{int nDifficulty=0,dwGameFrame=1000;bool bExpansion=true;void* pMemoryPool=nullptr;D2UnitStrc*')
 base=base.replace('int nSplEndDeath=0,','int nVelocity=0,nDrain[3]={100,100,100},nSplEndDeath=0,')
 old='void SUNITDMG_KillMonster(D2GameStrc*,D2UnitStrc* p,D2UnitStrc* attacker,int petKill){assert(p==&actor&&!attacker&&petKill==0);kills++;if(p->dwAnimMode!=0&&p->dwAnimMode!=12)p->dwAnimMode=0;}'
 assert old in base;base=base.replace(old,'void SUNITDMG_KillMonster(D2GameStrc*,D2UnitStrc* p,D2UnitStrc* attacker,int petKill){assert(p==&target&&attacker==&actor&&petKill==1);kills++;p->dwAnimMode=0;}')
 old='void SUNITEVENT_Trigger(D2GameStrc*,int event,D2UnitStrc* p,int,int){assert(event==UNITEVENT_DEATH&&p==&actor);deathEvents++;}'
 assert old in base;base=base.replace(old,'struct D2DamageStrc;void SUNITEVENT_Trigger(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*);')
 flags={n:int(v,16) for n,v in re.findall(r'^\s*((?:DAMAGERESULTFLAG|DAMAGEHITFLAG)_\w+)\s*=\s*(0x[0-9a-fA-F]+)',head,re.M)}
 base+='\nconstexpr uint32_t '+','.join(k+'='+str(v) for k,v in flags.items())+';\n'
 known=set(re.findall(r'\b(?:UNIT|UNITFLAG|UNITFLAGEX|MONMODE|PLRMODE|UNITEVENT|MONSTATSFLAGINDEX)_[A-Z0-9_]+',base))
 names=set(re.findall(r'\b(?:STAT|STATE|UNITFLAG|MONMODE|PLRMODE|MONSTER|UNITEVENT|EVENTTYPE|MISSILESFLAGINDEX|MONSTATSFLAGINDEX)_[A-Z0-9_]+',code))-known
 fixed={'MONMODE_DEAD':12,'PLRMODE_DEATH':0,'PLRMODE_DEAD':17,'MONMODE_WALK':2,'MONMODE_RUN':15,'PLRMODE_WALK':2,'PLRMODE_RUN':3}
 for k,v in fixed.items():
  if k in names:base+=f'constexpr int {k}={v};\n';names.remove(k)
 indices=sorted(n for n in names if 'FLAGINDEX_' in n)
 base+='enum{'+','.join(n+'='+str(i+1) for i,n in enumerate(indices))+'};\n';names-=set(indices)
 base+='enum{'+','.join(n+'='+str(i+20) for i,n in enumerate(sorted(names)))+'};\n'
 base+='constexpr int DIFFMODE_NIGHTMARE=1,DIFFMODE_HELL=2;\n'
 base+='enum D2DamageReductionType:uint32_t{DAMAGE_REDUCTION_NONE=0,DAMAGE_REDUCTION_PHYSICAL=1,DAMAGE_REDUCTION_MAGICAL=2,DAMAGE_REDUCTION_TYPES_COUNT=3};\n'
 base+='struct D2DifficultyLevelsTxt{int dwResistPenalty=0,dwHireableBossDmgPercent=100,dwLifeStealDiv=1,dwManaStealDiv=1;}difficulty;\n'
 for name in ['D2DamageStrc','D2DamageInfoStrc','D2DamageStatTableStrc']:
  m=re.search(r'struct '+name+r'\s*\{.*?^\};',head,re.M|re.S);assert m;row=m[0];base+=row+'\n'
 base+=r'''
struct D2ActiveRoomStrc{}room;struct D2StatListStrc{};struct D2SkillStrc{};struct D2SkillsTxt{int wStSound=0;};struct D2MonStats2Txt{unsigned dwModeFlags=0;}mon2;
struct D2MissilesTxt{unsigned dwMissileFlags=0;int nKnockBack=0;}missileRow;
unsigned blocked=0;int callbackCount=0;D2UnitStrc* lastAttacker=nullptr;
D2DifficultyLevelsTxt* DATATBLS_GetDifficultyLevelsTxtRecord(int){return &difficulty;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int s,int){return p?p->stats[s]:0;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int s,int l){return STATLIST_UnitGetStatValue(p,s,l);}
void STATLIST_SetUnitStat(D2UnitStrc* p,int s,int n,int){p->stats[s]=n;}
void STATLIST_AddUnitStat(D2UnitStrc* p,int s,int n,int){p->stats[s]+=n;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*){return 25600;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return false;}
bool STATES_CheckState(D2UnitStrc*,int){return false;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return 0;}bool MONSTERS_IsBoss(void*,D2UnitStrc*){return false;}bool MONSTERS_IsPrimeEvil(D2UnitStrc*){return false;}bool MONSTERS_IsUndead(D2UnitStrc*){return false;}bool MONSTERS_IsDemon(D2UnitStrc*){return false;}
int MONSTERUNIQUE_CalculatePercentage(int n,int p,int d){return int(int64_t(n)*p/d);}int D2Clamp(int n,int lo,int hi){return std::clamp(n,lo,hi);}
void SUNITEVENT_Trigger(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){callbackCount++;}
void UNITS_StoreLastAttacker(D2UnitStrc* p,D2UnitStrc* a){assert(p==&target);lastAttacker=a;}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return &missileRow;}
unsigned SUNITDMG_ApplyBlockOrDodge(D2GameStrc*,D2UnitStrc* a,D2UnitStrc* d,int missile,int physical){assert(a==&actor&&d==&target&&missile==1&&physical==0);return blocked;}
int MISSILE_GetHitClass(D2UnitStrc*){return 0;}int MISSILE_GetFlags(D2UnitStrc*){return 0;}
void D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70(D2UnitStrc* a,D2UnitStrc*,D2DamageStrc*){assert(a==&actor);}
void sub_6FC6E890(D2GameStrc*,D2UnitStrc* p){assert(p==&actor);}
void sub_6FC62DF0(D2UnitStrc* p,D2DamageStrc*){assert(p==&target);} // Declared non-sand-leaper target.
void sub_6FCF8DD0(D2UnitStrc* p){assert(p==&target);}
void UNITROOM_RefreshUnit(D2UnitStrc*){}void MONSTER_SetAiState(D2UnitStrc*,unsigned){}void sub_6FC6E8A0(D2GameStrc*,D2UnitStrc*){}
int SUNITDMG_GetHitClass(D2DamageStrc*,int n){return n;}
bool sub_6FCC1870(D2UnitStrc*,D2DamageStrc*,int){return true;}int sub_6FC62F50(D2UnitStrc*){return 0;}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return &mon2;}
void SUNITDMG_ApplyColdState(D2UnitStrc*,D2UnitStrc*,int n){assert(n==0);}void SUNITDMG_ApplyFreezeState(D2UnitStrc*,D2UnitStrc*,int n){assert(n==0);}void SUNITDMG_ApplyPoisonDamage(D2UnitStrc*,D2UnitStrc*,int d,int n){assert(d==0&&n==0);}void SUNITDMG_ApplyBurnDamage(D2UnitStrc*,D2UnitStrc*,int d,int n){assert(d==0&&n==0);}
int SUNITDMG_AddLeechedLife(D2UnitStrc*,int){assert(false);return 0;}int SUNITDMG_AddLeechedMana(D2UnitStrc*,int){assert(false);return 0;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return nullptr;}
int STATLIST_GetStatValue(D2StatListStrc*,int,int){assert(false);return 0;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){assert(false);return nullptr;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){assert(false);return nullptr;}
int SKILLS_GetFlags(D2SkillStrc*){assert(false);return 0;}
int ITEMS_RollLimitedRandomNumber(int*,int){assert(false);return 0;}unsigned ITEMS_RollRandomNumber(int*){assert(false);return 0;}
bool MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc*,unsigned){assert(false);return false;}
void sub_6FD10E50(D2UnitStrc*,int,D2StatListStrc*){assert(false);}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int,int,int){assert(false);return nullptr;}
#define UNUSED_VOID(name) template<class... T>void name(T...){assert(false);}
UNUSED_VOID(STATES_ToggleState) UNUSED_VOID(UNITS_SetOverlay) UNUSED_VOID(D2COMMON_11013_ConvertMode) UNUSED_VOID(EVENT_SetEvent) UNUSED_VOID(STATLIST_SetExpireFrame) UNUSED_VOID(STATLIST_SetState) UNUSED_VOID(STATLIST_SetStatRemoveCallback) UNUSED_VOID(D2COMMON_10475_PostStatToStatList) UNUSED_VOID(D2GAME_EVENTS_Delete_6FC34840) UNUSED_VOID(SUNIT_AttachSound) UNUSED_VOID(sub_6FC817D0) UNUSED_VOID(D2GAME_PLAYERMODE_Change_6FC81A00) UNUSED_VOID(SKILLS_SetFlags)
'''
 base+=code
 base+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int am:{1,0,12})for(int pm:{1,0,17})for(int store:{0,1})for(int present:{0,1})for(int align:{0,1})for(int dead:{0,1})for(int block:{0,1})for(int resist:{-100,0,50,100,150})for(int pierce:{0,20})for(int raw:{0,256,12800,30000})for(int suppress:{0,1}){
 game={};actor={};master={};missile={};target={};row={};D2AiControlStrc control;control.pGame=&game;actor.ai=&control;actor.pGame=target.pGame=master.pGame=missile.pGame=&game;
 actor.dwAnimMode=am;master.dwUnitType=0;master.dwUnitId=1;master.dwAnimMode=pm;game.pUnitList[0][1]=&master;if(present)game.pUnitList[1][77]=&actor;
 target.dwUnitId=99;target.dwAnimMode=dead?12:1;target.group=align?1:2;target.stats[STAT_HITPOINTS]=25600;target.stats[STAT_LIGHTRESIST]=resist;actor.stats[STAT_PASSIVE_LTNG_PIERCE]=pierce;
 missile.dwUnitType=3;missile.dwFlagEx=store?UNITFLAGEX_STOREOWNERINFO:0;row.dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_KILLABLE];missileRow={};missileRow.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_GETHIT];blocked=block;kills=callbackCount=0;lastAttacker=nullptr;
 D2DamageStrc d{};d.dwLtngDamage=raw;d.wResultFlags=suppress?DAMAGERESULTFLAG_32:0;MISSMODE_SetDamageFlags(&game,&missile,&target,&d);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,%d]",first?"":",",am,pm,store,present,align,dead,block,resist,pierce,raw,suppress,target.stats[STAT_HITPOINTS],d.dwLtngDamage,d.dwDmgTotal,d.wResultFlags,callbackCount,kills,lastAttacker==&actor);first=false;
}printf("]\n");}
'''
 p=STAGE/'sentry-dead-damage-source-probe.cpp';p.write_text(base);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sentry-dead-damage-sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:];rows=json.loads(run.stdout);assert len(rows)==23040
 for am,pm,store,present,align,dead,block,resist,pierce,raw,suppress,hp,light,total,result,callbacks,kills,last in rows:
  gate=store and present and not align and not dead and not block;eff=resist-pierce if resist<100 else resist;eff=max(-100,min(100,eff));damage=raw*(100-eff)//100 if gate else 0;remaining=25600-damage
  if remaining<256:remaining=0
  assert hp==remaining and total==damage and light==(damage if gate else raw) and callbacks==(1+2*int(remaining==0 and not suppress) if gate else 0) and kills==int(gate and remaining==0) and last==int(gate),(am,pm,store,present,align,dead,block,resist,pierce,raw,suppress,hp,total,kills,last)
 out=STAGE/'sentry-dead-damage-source-probe.json';out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',nativePolicyChanged=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutineHashes=routines,ownerEvidence='tools/reference/sentry-owner-source-evidence.json',fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=23040),established=['Complete source damage flags, total-damage table, resistance/absorb routine, ExecuteEvents health subtraction and ExecuteMissileDamage mode dispatcher execute with the complete typed registry/owner/minion/allegiance chain. Retained trap mode1/0/12 and registered master mode1/0/17 produce the same NPC lightning health loss in this declared retained-stat context.','Missing owner flag or registry, aligned/dead target or declared block suppresses health loss. Source pierce changes resistance below100 but does not break monster immunity. Below256 remaining life rounds to0 and reaches the recorded kill service. ResultFlags32 changes event dispatch without inventing a different health rule.'],limits=['Registry retention and owner stat availability are explicit contexts. Full trap death entry/stat cleanup, corpse/GUID lifetime and actual unit removal remain unverified; this probe must not be used to create an immortal or player-substituted damage source. Native live-actor damage policy is unchanged.','Empty event subscribers, critical0, declared block0/1, Normal non-town nonboss/nonhireling NPC, stat getter/storage and kill/mode/presentation services are explicit. Raw lightning is already rolled; no weapon, leech, poison/cold/stun or arbitrary item/reaction handlers are certified. Unexpected unrelated effect branches assert.','Source-only reconstructed composition is not current3.3 executable confirmation. Unit/state/stat ids and ancillary ABI layout remain declared; the damage struct and damage flags are taken from the pinned header.'],sanitizers='ASan/UBSan; exit0 and empty runtime stderr required')
 meta=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text());pinned=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';raw=pinned.read_bytes();assert hashlib.sha256(raw).hexdigest()==meta['files']['json/missiles.json']['sha256'];row=next(x for x in json.loads(raw).values() if x.get('Missile')=='sentrychargedbolt');assert row.get('GetHit')==1 and not row.get('SoftHit') and not row.get('KnockBack') and not row.get('pSrvDmgFunc')
 e['pinnedMissileContext']=dict(path=str(pinned.relative_to(ROOT)),sha256=hashlib.sha256(raw).hexdigest(),id='sentrychargedbolt',GetHit=1,SoftHit=0,KnockBack=0,pSrvDmgFunc=0)
 e['next']='Establish current trap corpse/GUID retention and death-entry stat cleanup before changing native actor ownership; continue unaffected Assassin skills while this remains uncertain.'
 target=ROOT/'tools/reference/sentry-dead-damage-source-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert target.read_text()==render
 else:target.write_text(render)
 print('23040 complete flags/mitigation/events/HP/mode cases pass for retained alive/dead trap owners; corpse lifetime/current behavior/native policy remain open.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
