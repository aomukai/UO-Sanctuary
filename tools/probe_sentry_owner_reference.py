#!/usr/bin/env python3
"""Execute trap removal/registry/allegiance/death-end routing, not damage commit.

Killing/removing/mode/event services are explicit boundaries. This probe does
not infer corpse retention duration or enable damage from retired native actors.
"""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};routines={};reviewed={}
 def extract(rel,name,compile=True):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();s=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;i=s.index('{',m.start())+1;depth=1
  while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
  body=s[m.start():i];(routines if compile else reviewed)[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
 unit='source/D2Game/src/UNIT/SUnit.cpp';ai='source/D2Game/src/AI/AiGeneral.cpp';pets='source/D2Game/src/PLAYER/PlayerPets.cpp';mode='source/D2Game/src/MONSTER/MonsterMode.cpp';damage='source/D2Game/src/UNIT/SUnitDmg.cpp'
 # These bodies are reviewed to identify the next proof boundary, not executed.
 for rel,name in [(damage,'SUNITDMG_KillMonster'),(damage,'SUNITDMG_ExecuteEvents'),(damage,'SUNITDMG_ExecuteMissileDamage'),(damage,'SUNITDMG_CalculateTotalDamage')]:extract(rel,name,False)
 cpp=r'''#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define D2_ASSERT assert
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_OBJECT=2,UNIT_MISSILE=3,UNIT_ITEM=4,UNIT_TILE=6;
constexpr unsigned UNITFLAGEX_STOREOWNERINFO=1,UNITFLAGEX_HASBEENDELETED=2,UNITFLAG_NOXP=4;
constexpr int MONSTATSFLAGINDEX_KILLABLE=0,MONMODE_DEATH=0,UNITEVENT_DEATH=1;
unsigned gdwBitMasks[32];struct D2GameStrc;struct D2UnitStrc;
struct D2AiControlStrc{D2GameStrc* pGame=nullptr;int dwOwnerTypeEx=0,dwOwnerGUIDEx=1;};
struct D2UnitStrc{int dwUnitType=1,dwUnitId=77,dwClassId=413,dwAnimMode=1,dwOwnerType=1,dwOwnerGUID=77,group=1;unsigned dwFlagEx=0,dwFlags=0;D2UnitStrc* pListNext=nullptr;D2AiControlStrc* ai=nullptr;}actor,master,missile,target;
struct D2GameStrc{D2UnitStrc* pUnitList[5][128]={};D2UnitStrc* pTileList=nullptr;}game;
struct D2MonStatsTxt{unsigned dwMonStatsFlags=0;int nSplEndDeath=0,wMinion[2]={-1,-1};}row;
struct D2ModeChangeStrc{D2UnitStrc* pTargetUnit=nullptr;D2UnitStrc* pUnit=nullptr;};struct D2PetArgStrc{int nPetGUID=0;};
struct Tables{int nMonStatsTxtRecordCount=1000;}tables;auto* sgptDataTables=&tables;
int kills=0,removes=0,modes=0,syncs=0,deathEvents=0;
D2AiControlStrc* AIGENERAL_GetAiControlFromUnit(D2UnitStrc* p){return p?p->ai:nullptr;}
int PLAYERLIST_CheckFlag(D2UnitStrc*,D2UnitStrc*,int){assert(false);return 0;}
int STATLIST_AreUnitsAligned(D2UnitStrc* a,D2UnitStrc* b){assert(a&&b);return a->group==b->group;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return &row;}
void FOG_DisplayWarning(const char*,const char*,int){}
void SUNIT_RemoveUnit(D2GameStrc* g,D2UnitStrc* p){removes++;assert(p==&actor);g->pUnitList[1][77&127]=nullptr;}
// Declared mode/kill service preserves registration. It is not a full kill proof.
void SUNITDMG_KillMonster(D2GameStrc*,D2UnitStrc* p,D2UnitStrc* attacker,int petKill){assert(p==&actor&&!attacker&&petKill==0);kills++;if(p->dwAnimMode!=0&&p->dwAnimMode!=12)p->dwAnimMode=0;}
void MONSTERMODE_GetModeChangeInfo(D2UnitStrc* p,int mode,D2ModeChangeStrc* out){assert(mode==0);out->pUnit=p;}
void D2GAME_ModeChange_6FC65220(D2GameStrc*,D2ModeChangeStrc* mode,int one){assert(one==1&&mode->pUnit==&actor);modes++;actor.dwAnimMode=0;}
void PLAYERPETS_PlayerIterate_SynchronizeWithClient(){}
void SUNIT_IterateLivingPlayers(D2GameStrc*,void(*)(),D2PetArgStrc* a){assert(a->nPetGUID==77);syncs++;}
void SUNIT_SetCombatMode(D2GameStrc*,D2UnitStrc* p,int mode){assert(p==&actor&&mode==12);p->dwAnimMode=mode;}
void SUNITEVENT_Trigger(D2GameStrc*,int event,D2UnitStrc* p,int,int){assert(event==UNITEVENT_DEATH&&p==&actor);deathEvents++;}
void MONSTER_Reinitialize(D2GameStrc*,D2UnitStrc*,int,int){assert(false);}void MONSTER_UpdateAiCallbackEvent(D2GameStrc*,D2UnitStrc*){assert(false);}
'''
 for rel,name in [(unit,'SUNIT_GetUnitList'),(unit,'SUNIT_GetServerUnit'),(unit,'SUNIT_GetOwner'),(ai,'AIGENERAL_GetMinionOwner'),(unit,'sub_6FCBD900'),(pets,'sub_6FC7CF50'),(mode,'sub_6FC641D0')]:cpp+=extract(rel,name)
 cpp+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int actorMode:{1,0,12})for(int masterMode:{1,0,12})for(int flag:{0,1})for(int registered:{0,1})for(int player:{0,1})for(int deleted:{0,1})for(int killable:{0,1})for(int aligned:{0,1})for(int stage:{0,1,2}){
 game={};actor={};master={};missile={};target={};row={};D2AiControlStrc control;control.pGame=&game;actor.ai=&control;master.dwUnitType=0;master.dwUnitId=1;master.dwAnimMode=masterMode;actor.dwAnimMode=actorMode;actor.dwFlagEx=deleted?UNITFLAGEX_HASBEENDELETED:0;
 if(registered)game.pUnitList[1][77]=&actor;if(player)game.pUnitList[0][1]=&master;
 missile.dwUnitType=3;missile.dwUnitId=88;missile.dwFlagEx=flag?UNITFLAGEX_STOREOWNERINFO:0;row.dwMonStatsFlags=killable?gdwBitMasks[MONSTATSFLAGINDEX_KILLABLE]:0;target.dwUnitId=99;target.group=aligned?1:2;kills=removes=modes=syncs=deathEvents=0;
 auto* before=SUNIT_GetOwner(&game,&missile);if(stage)sub_6FC7CF50(&game,77);
 if(stage==2&&registered&&!deleted)sub_6FC641D0(&game,&actor);
 auto* after=SUNIT_GetOwner(&game,&missile);int hostile=after?sub_6FCBD900(&game,after,&target):-1;
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%d]",first?"":",",actorMode,masterMode,flag,registered,player,deleted,killable,aligned,stage,before!=nullptr,after!=nullptr,hostile,kills,removes,modes,syncs,actor.dwFlags,actor.dwAnimMode,deathEvents);first=false;
}printf("]\n");}
'''
 p=STAGE/'sentry-owner-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sentry-owner-sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:];data=json.loads(run.stdout);assert len(data)==1728
 for am,pm,flag,present,player,deleted,killable,aligned,stage,before,after,hostile,kills,removes,modes,syncs,flags,modeNow,ends in data:
  called=bool(stage and present);retained=present and not(stage and deleted);end=bool(stage==2 and present and not deleted)
  expected=(int(flag and present),int(flag and retained),(int(not aligned) if flag and retained else -1),int(called and not deleted and killable),int(called and deleted),int(called and not deleted and not killable),int(called),4 if called and not deleted else 0,12 if end else (0 if called and not deleted and (not killable or am not in [0,12]) else am),int(end))
  assert (before,after,hostile,kills,removes,modes,syncs,flags,modeNow,ends)==expected
 out=STAGE/'sentry-owner-source-probe.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',nativePolicyChanged=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutineHashes=routines,reviewedNotCompiled=reviewed,fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=1728),established=['Full typed GUID list/getter and missile owner lookup do not filter actor death mode. Full minion-owner lookup/allegiance resolve a registered player owner without an alive check; player and actor mode1/0/12 controls preserve this routing.','Complete pet removal sends already-deleted actors to explicit RemoveUnit; otherwise marks NOXP and calls kill for killable row or death mode for a nonkillable control. Full death-end callback selects dead mode12 and triggers death, without itself removing a regular SplEndDeath0 unit.','Under the declared registration-preserving kill/mode service, a retained dead actor still resolves as missile owner and can pass allegiance; an actually removed registry actor cannot. This establishes why death and deletion must not be conflated, NOT how long the real actor stays registered.'],limits=['KillMonster, full death-mode entry/stat cleanup, RemoveUnit unlink, event subscribers and client synchronization are explicit services. Complete KillMonster/CalculateTotalDamage/ExecuteEvents/ExecuteMissileDamage are hash-pinned reviewed bodies, not compiled here. Health damage from a dead actor, corpse/GUID retention duration, current executable behavior and native policy remain unverified.','Alignment comparison and AI-control accessor are declared services; typed registry and minion/master lookup execute fully. The unbound master control leaves declared actor alignment in force. PvP/owner cycles, all unit kinds and arbitrary registry corruption are outside scope.','Synthetic ABI flag positions isolate control flow. This is not a binary layout, whole engine or current D2R corpse certificate. Existing native living-actor damage gate is unchanged; do not infer an immortal damage proxy or replace missing actor ownership with the player.'],sanitizers='ASan/UBSan; exit0 and empty runtime stderr required')
 meta=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text());pinned=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json';raw=pinned.read_bytes();assert hashlib.sha256(raw).hexdigest()==meta['files']['json/monstats.json']['sha256'];row=next(x for x in json.loads(raw).values() if x.get('Id')=='chargeboltsentry');assert row['killable']==1 and row.get('SplEndDeath',0)==0
 e['pinnedRegularTrapRow']=dict(path=str(pinned.relative_to(ROOT)),sha256=hashlib.sha256(raw).hexdigest(),id='chargeboltsentry',killable=1,SplEndDeath=0)
 e['nonblocking']=True
 e['next']='Compose the full damage flags/events/health path for a retained dead owner and establish actual corpse/GUID lifetime before changing the native living-actor gate.'
 target=ROOT/'tools/reference/sentry-owner-source-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert target.read_text()==render
 else:target.write_text(render)
 print('1728 full removal/death-end/typed-registry/owner/allegiance routing cases pass; full kill/health/lifetime and native policy remain open.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
