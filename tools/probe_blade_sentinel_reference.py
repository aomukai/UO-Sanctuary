#!/usr/bin/env python3
"""Execute complete legacy Blade Sentinel dispatch, AI, follow and hit routines.

The general summon factory, weapon/stat suppliers, world movement and collision
are declared services. This does not prove those services or modern equivalence.
"""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  b=text[m.start():i];blocks[name]=hashlib.sha256(b.encode()).hexdigest();return b+'\n'
 raw=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';pin=json.loads((raw.parent.parent/'sources.json').read_text())['files']['json/skills.json']['sha256'];assert hashlib.sha256(raw.read_bytes()).hexdigest()==pin;skill=json.loads(raw.read_text())['257'];assert skill['srvdofunc']==44 and skill['calc4']=='ln12' and (skill['Param1'],skill['Param2'])==(100,12) and skill['srvmissilea']=='blade creeper'
 ass=source('source/D2Game/src/SKILLS/SkillAss.cpp');ai=source('source/D2Game/src/AI/AiThink.cpp');miss=source('source/D2Game/src/MISSILES/MissMode.cpp')
 cpp=r'''#include <algorithm>
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
struct D2GameStrc{int dwGameFrame=1000;} game;
struct D2UnitStrc{int x=100,y=200,dwFlags=0,level=99,ar=80,percent=11,mastery=17;void* pDynamicPath=(void*)1;} blade,owner,missile,weapon;
struct D2SkillsTxt{int dwCalc[4]={0,0,0,1},wSrvMissileA=392;} row;
struct D2SkillStrc{} learned;
struct D2MonStatsTxt{int nSkill[1]={257};} mon;
struct D2AiControlStrc{int dwAiParam[3]={-1,1,0};} control;
struct D2AiTickParamStrc{D2MonStatsTxt* pMonstatsTxt=&mon;D2AiControlStrc* pAiControl=&control;} tick;
struct D2AiCmdStrc{int nCmdParam[5]={0,100,200,108,208};} command;
struct Tables{int nMissilesTxtRecordCount=1000;} tables;Tables* sgptDataTables=&tables;
constexpr int STAT_TOHIT=1,STAT_ITEM_TOHIT_PERCENT=2,STAT_LEVEL=3,UNITFLAG_SKSRVDOFUNC=64,EVENTTYPE_AITHINK=4;
int rank=1,available=0,spawn=413,hasOwner=1,make=1,hasCommand=1,walkBits=0,walkOwner=0,target=1,dead=0;
int deathCalls=0,idle=0,created=0,stored=0,arAdded=0,percentAdded=0,velocity=0,fallback=0,walks=0,firstGoal=0,secondGoal=0,teleports=0,currentFrame=0,collisions=0,hitHandlers=0,eventFrame=0,deletedEvent=0,copied=0,moved=0;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){assert(id==257);return available==1?nullptr:&row;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc* p,int id){assert(p==&blade && id==257);return available==2?nullptr:&learned;}
int SKILLS_GetSkillLevel(D2UnitStrc* p,D2SkillStrc*,int effective){assert(p==&blade && effective==1);return rank;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int formula,int id,int lvl){assert(p==&blade && formula==1 && id==257 && lvl==rank);return 100+12*(lvl-1);}
void AITACTICS_ChangeModeAndTargetCoordinates(D2GameStrc*,D2UnitStrc* p,int a,int b,int c){assert(p==&blade && a==0 && b==0 && c==0);deathCalls++;}
D2UnitStrc* AIGENERAL_GetMinionOwner(D2UnitStrc* p){assert(p==&blade);return hasOwner?&owner:nullptr;}
D2UnitStrc* D2GAME_CreateMissile_6FD115E0(D2GameStrc*,D2UnitStrc* p,int id,int lvl,int m,int x,int y){assert(p==(hasOwner?&owner:&blade) && id==257 && lvl==rank && m==392 && x==blade.x && y==blade.y);created++;return make?&missile:nullptr;}
void UNITS_StoreOwner(D2UnitStrc* p,D2UnitStrc* o){assert(p==&missile && o==&blade);stored++;}
int UNITS_GetAttackRate(D2UnitStrc* p){return p->ar;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int layer){assert(layer==0);return stat==STAT_LEVEL?p->level:p->percent;}
void STATLIST_AddUnitStat(D2UnitStrc* p,int stat,int value,int layer){assert(p==&blade && layer==0);if(stat==STAT_TOHIT)arAdded+=value;else{assert(stat==STAT_ITEM_TOHIT_PERCENT);percentAdded+=value;}}
D2UnitStrc* sub_6FC7C7B0(D2UnitStrc*){return &weapon;}
int SKILLS_GetWeaponMasteryBonus(D2UnitStrc* p,D2UnitStrc* w,int a,int b){assert(w==&weapon && a==0 && b==0);return p->mastery;}
D2AiCmdStrc* AIGENERAL_GetCurrentAiCommandFromUnit(D2UnitStrc*){return hasCommand?&command:nullptr;}
void AITACTICS_IdleInNeutralMode(D2GameStrc*,D2UnitStrc*,int frames){idle=frames;}
void AITACTICS_SetVelocity(D2UnitStrc*,int a,int b,int c){assert(a==15 && b==0 && c==20);velocity++;}
int AITACTICS_WalkToTargetCoordinatesDeleteAiEvent(D2GameStrc*,D2UnitStrc*,int x,int y){assert((x==100 && y==200)||(x==108 && y==208));int goal=x==108?1:0;if(!walks)firstGoal=goal;else secondGoal=goal;return (walkBits>>walks++)&1;}
int D2GAME_AICORE_WalkToOwner_6FCD0B60(D2GameStrc*,D2UnitStrc*,D2UnitStrc* p,unsigned distance){assert(p==(hasOwner?&owner:nullptr) && distance==5);fallback++;return walkOwner;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
int D2GAME_GetSummonIdFromSkill_6FD15580(D2UnitStrc*,int,int id,int lvl,int* mode,int,int){assert(id==257 && lvl==rank);*mode=8;return spawn;}
int D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int* x,int* y){*x=108;*y=208;return target;}
D2UnitStrc* sub_6FCF8610(D2GameStrc*,D2UnitStrc* p,int x,int y,D2SkillsTxt* r,int id,int lvl){assert(p==&owner && x==0 && y==0 && r==&row && id==257 && lvl==rank);created++;return make?&blade:nullptr;}
void STATLIST_SetUnitStat(D2UnitStrc* p,int stat,int value,int layer){assert(p==&blade && stat==STAT_LEVEL && layer==0);p->level=value;}
void* UNITS_GetRoom(D2UnitStrc*){return nullptr;}
void sub_6FCBDFE0(D2GameStrc*,D2UnitStrc* p,void*,int x,int y,int a,int b){assert(p==&blade && x==owner.x && y==owner.y && a==0 && b==0);moved++;}
void AIGENERAL_CopyAiCommand(D2GameStrc*,D2UnitStrc* p,D2AiCmdStrc* cmd){assert(p==&blade);command=*cmd;copied++;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int type,int zero){assert(type==EVENTTYPE_AITHINK && zero==0);deletedEvent++;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int a,int b){assert(type==EVENTTYPE_AITHINK && a==0 && b==0);eventFrame=frame;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return hasOwner?&blade:nullptr;}
int SUNIT_IsDead(D2UnitStrc*){return dead;}
void MISSMODE_SrvDmgHitHandler(D2GameStrc*,D2UnitStrc*,D2UnitStrc* victim,int barrier){assert(victim==nullptr && barrier==1);hitHandlers++;}
void D2Common_10229(void*,D2UnitStrc* p,void*,int x,int y){assert(p==&missile && x==blade.x && y==blade.y);teleports++;}
void MISSILE_SetCurrentFrame(D2UnitStrc*,int frame){currentFrame=frame;}
void MISSMODE_HandleMissileCollision(D2GameStrc*,D2UnitStrc*){collisions++;}
void reset(){blade={};blade.ar=35;blade.percent=2;blade.mastery=3;owner={};missile={};weapon={};row={};command={};control={};available=0;hasOwner=make=hasCommand=target=1;walkBits=walkOwner=dead=0;spawn=413;deathCalls=idle=created=stored=arAdded=percentAdded=velocity=fallback=walks=firstGoal=secondGoal=teleports=currentFrame=collisions=hitHandlers=eventFrame=deletedEvent=copied=moved=0;}
'''
 for text,name in [(ass,'SKILLS_SrvDo044_BladeSentinel'),(ai,'D2GAME_AI_Unk102_6FCE9FB0'),(ai,'AITHINK_Fn102_BladeCreeper'),(miss,'MISSMODE_SrvDo20_BladeCreeper'),(miss,'MISSMODE_SrvHit37_BladeCreeper')]:cpp+=routine(text,name)
 cpp+=r'''int main(){bool first=true;printf("{\"ai\":[");
for(int r:{1,3,20,21,255})for(int expiry:{-1,999,1000})for(int done:{0,1})for(int mid:{-1,392})for(int own:{0,1})for(int factory:{0,1})for(int cmd:{0,1})for(int dir:{0,1})for(int bits:{0,1,2,3})for(int follow:{0,1}){
 reset();rank=r;control.dwAiParam[0]=expiry;control.dwAiParam[1]=dir;control.dwAiParam[2]=done;row.wSrvMissileA=mid;hasOwner=own;make=factory;hasCommand=cmd;walkBits=bits;walkOwner=follow;
 AITHINK_Fn102_BladeCreeper(&game,&blade,&tick);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",r,expiry,done,mid,own,factory,cmd,dir,bits,follow,control.dwAiParam[0],control.dwAiParam[1],control.dwAiParam[2],deathCalls,idle,created,stored,arAdded,percentAdded,velocity,fallback,walks,firstGoal,secondGoal,copied,moved);first=false;
}
printf("],\"missing\":[");first=true;for(int missing:{1,2})for(int r:{1,3,20,21,255}){reset();rank=r;available=missing;AITHINK_Fn102_BladeCreeper(&game,&blade,&tick);printf("%s[%d,%d,%d,%d,%d]",first?"":",",missing,r,deathCalls,created,walks);first=false;}
printf("],\"dispatch\":[");first=true;for(int exists:{0,1})for(int summon:{-1,413})for(int tgt:{0,1})for(int factory:{0,1})for(int r:{1,3,20,21,255}){reset();rank=r;available=exists?0:1;spawn=summon;target=tgt;make=factory;blade.level=1;blade.dwFlags=8;int result=SKILLS_SrvDo044_BladeSentinel(&game,&owner,257,rank);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",exists,summon,tgt,factory,r,result,created,moved,copied,deletedEvent,eventFrame,blade.level,blade.dwFlags,owner.dwFlags,command.nCmdParam[0],command.nCmdParam[1],command.nCmdParam[2],command.nCmdParam[3],command.nCmdParam[4]);first=false;}
printf("],\"follow\":[");first=true;for(int own:{0,1})for(int isdead:{0,1})for(int path:{0,1}){reset();hasOwner=own;dead=isdead;missile.pDynamicPath=path?(void*)1:nullptr;int result=MISSMODE_SrvDo20_BladeCreeper(&game,&missile);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",own,isdead,path,result,hitHandlers,teleports,currentFrame,collisions);first=false;}
reset();control.dwAiParam[0]=55;control.dwAiParam[1]=55;control.dwAiParam[2]=55;D2GAME_AI_Unk102_6FCE9FB0(&game,&blade,&tick);printf("],\"init\":[%d,%d,%d],\"hit\":[%d,%d]}\n",control.dwAiParam[0],control.dwAiParam[1],control.dwAiParam[2],MISSMODE_SrvHit37_BladeCreeper(&game,&missile,nullptr),MISSMODE_SrvHit37_BladeCreeper(&game,&missile,&owner));}
'''
 path=STAGE/'blade-sentinel-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data['ai'])==7680 and len(data['missing'])==10 and len(data['dispatch'])==80 and len(data['follow'])==8
 for case in data['ai']:
  r,expiry,done,mid,own,factory,cmd,direction,bits,follow,*actual=case
  end=1000+100+12*(r-1) if expiry<0 else expiry;deadcase=end<1000
  create=int(not deadcase and not done and mid==392);stored=create*factory
  outdir=direction;walks=0;firstgoal=secondgoal=0;fallback=velocity=idle=0
  if not deadcase:
   if not cmd:idle=3
   else:
    velocity=1;walks=1;firstgoal=direction
    if not bits&1:
     outdir=1-direction;walks=2;secondgoal=outdir
     if not bits&2:fallback=1;idle=0 if follow else 5
  expected=[end,outdir,1 if create else done,int(deadcase),idle,create,stored,(80 if own else 35)*stored,(28 if own else 5)*stored,velocity,fallback,walks,firstgoal,secondgoal,0,0]
  assert actual==expected,(case,expected)
 for m,r,death,create,walks in data['missing']:assert (death,create,walks)==(1,0,0)
 for exists,summon,tgt,factory,r,result,create,moved,copied,deleted,event,level,bflags,oflags,*command in data['dispatch']:
  gated=bool(exists and summon>=0);attempt=bool(gated and tgt);success=bool(attempt and factory)
  assert [result,create,moved,copied,deleted,event,level,bflags,oflags,command]==[int(success),int(attempt),int(success),int(success),int(success),1001 if success else 0,99 if success else 1,0 if success else 8,64 if gated else 0,[0,100,200,108,208]]
 for own,dead,path,result,hit,teleport,frame,collision in data['follow']:
  live=bool(own and not dead);assert [result,hit,teleport,frame,collision]==[1 if live else 2,int(not live),int(live and path),10 if live else 0,int(live)]
 assert data['init']==[-1,1,0] and data['hit']==[0,2]
 out=STAGE/'blade-sentinel-source-probe.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',talentId='assassin.bladesentinel',handlerEnabled=False,installed=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledUnchangedRoutines=blocks,pinnedSkillHash=pin,fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=dict(ai=7680,missingSkill=10,dispatch=80,follow=8,initialization=1,hit=2),total=7781),established=[
  'Full do44 gates missing row/summon/target/factory, sets server flag before target lookup, summons at caster through declared generic factory, overwrites final pet level with caster level, relocates to caster and copies fixed origin/cursor endpoints. AI is scheduled gameFrame+1 and flag8 cleared only after successful creation.',
  'Full initialization sets expiry=-1,direction=1,created=0. First AI evaluation stores frame+100+12*(effective rank-1); expiration is strict less-than, not less-or-equal. Missing skill row or learned summon skill requests death mode0.',
  'First eligible AI tries missile392 once, initially created through minion owner (or self fallback) at pet coordinates and then owned by pet. Only successful creation adds owner attack rate and item attack percent plus weapon mastery to pet. A failed allocation still marks attempt complete. Invalid missile id does not mark it complete.',
  'AI walks toward stored destination first, switches direction on failure/endpoint and tries origin; reverse phase mirrors this. Both failures try owner range5, otherwise idle5. Missing command idles3. Velocity call15/0/20 is retained; the actual world walk/movement service is not compiled here.',
  'Full missile follow routine removes when pet owner missing/dead, otherwise teleports to live pet when a dynamic path exists, resets current frame10 each update and invokes collision. Lifetime therefore follows pet existence rather than a standalone missile Range counter. Full hit37 returns2 for actor contact,0 without actor; shared router meanings remain separate.'
 ],services=[
  'General sub_6FCF8610 summon factory, base/passive stat suppliers, rank/duration formula, world walk/room/path, missile creator, owner lookup, attack rate/mastery/weapon and collision are explicit checked services. This proof does not execute generated base stats, current14 passive slots, weapon damage, hit router, pet cap replacement or path traversal.',
  'Current table values are hash-pinned; older routine/control-flow proof is not exact modern executable equivalence. No native handler enabled, class completion, final asset or arbitrary save compatibility claim.'
 ])
 p=ROOT/'tools/reference/blade-sentinel-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Blade Sentinel complete dispatch/AI/follow/hit checks passed:7781 cases; declared summon/stat/path/damage services remain.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
