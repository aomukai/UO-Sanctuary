#!/usr/bin/env python3
"""Compile unchanged Avoid dispatch, missile flags and copied area-impact wrappers."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 hashes={};blocks={}
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
  hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;end=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
  blocks[name]=text[m.start():end];return blocks[name]+'\n'
 damage=source('source/D2Game/src/UNIT/SUnitDmg.cpp');miss=source('source/D2Game/src/MISSILES/MissMode.cpp');skills=source('source/D2Game/src/SKILLS/Skills.cpp');assassin=source('source/D2Game/src/SKILLS/SkillAss.cpp');header=source('source/D2Game/include/UNIT/SUnitDmg.h')
 for text,name in [(damage,'SUNITDMG_ApplyDodge'),(damage,'SUNITDMG_ApplyBlockOrDodge'),(miss,'MISSMODE_SetDamageFlags'),(skills,'sub_6FD10140'),(assassin,'sub_6FCF5DE0')]:routine(text,name)
 names='\n'.join(blocks.values());stats=sorted(set(re.findall(r'\bSTAT_[A-Z0-9_]+',names)));indices=sorted(set(re.findall(r'\bMISSILESFLAGINDEX_[A-Z0-9_]+',names)))
 flags={n:int(v,16) for n,v in re.findall(r'^\s*((?:DAMAGERESULTFLAG|DAMAGEHITFLAG|BLOCKFLAG)_\w+)\s*=\s*(0x[0-9a-fA-F]+)',header,re.M)}
 cpp=r'''#include <cstdint>
#include <cstdio>
#include <cstring>
#include <initializer_list>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_MISSILE=3,PLRMODE_WALK=2,PLRMODE_RUN=3,MONMODE_WALK=2,MONMODE_RUN=15,STATE_UNINTERRUPTABLE=1;
'''
 cpp+='enum {'+','.join(stats)+'};\nenum {'+','.join(indices)+'};\n'
 cpp+='constexpr uint32_t '+','.join(n+'='+str(v) for n,v in flags.items())+';\n'
 cpp+='constexpr uint32_t gdwBitMasks[]={'+','.join(str(1<<i)+'u' for i in range(32))+'};\n'
 cpp+=r'''struct D2UnitStrc{int dwUnitType=0,dwAnimMode=1,dwClassId=0,pSeed=0;void* pInventory=nullptr;} owner,target,shot;
struct D2GameStrc{bool bExpansion=true;} game;
struct D2DamageStrc{uint32_t wResultFlags=0,dwHitClass=0,dwHitFlags=0;int dwPhysDamage=0,nHitClassActiveSet=0,dwPiercePct=0;};
struct D2AuraCallbackStrc{D2GameStrc* pGame;D2UnitStrc* pOwner;void* pArgs;};
struct D2MissilesTxt{uint32_t dwMissileFlags=0;int nKnockBack=0;} row;
int avoidChance=0,shield=0,draw=0,draws=0,events=0,commits=0,lastFlags=0;bool uninterruptable=false;
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int){return stat==STAT_PASSIVE_AVOID?avoidChance:0;}
int ITEMS_RollRandomNumber(int*){draws++;return draw;}
int SUNITDMG_GetWeaponBlock(D2UnitStrc*){return 0;}
void COMPOSIT_GetWeaponClassId(D2UnitStrc*,void*,int* value,int,int){*value=0;}
int UNITS_GetBlockRate(D2UnitStrc*,bool){return shield;}
bool UNITS_IsInMovingMode(D2UnitStrc* p){return p->dwAnimMode==PLRMODE_WALK || p->dwAnimMode==PLRMODE_RUN;}
bool STATES_CheckState(D2UnitStrc*,int){return uninterruptable;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return &owner;}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return &row;}
int MISSILE_GetHitClass(D2UnitStrc*){return 0;}
int MISSILE_GetFlags(D2UnitStrc*){return 0;}
void D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70(D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,D2DamageStrc*){events++;}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p){commits++;lastFlags=p->wResultFlags;}
void reset(int moving,int chance,int roll){target.dwAnimMode=moving==1?PLRMODE_WALK:moving==2?PLRMODE_RUN:1;avoidChance=chance;draw=roll;draws=events=commits=lastFlags=0;uninterruptable=false;shield=0;shot.dwUnitType=UNIT_MISSILE;}
'''
 cpp+='\n'.join(blocks.values())
 cpp+=r'''int main(){bool first=true;std::printf("{\"dispatch\":[");
for(int moving:{0,1,2})for(int projectile:{0,1})for(int chance:{0,24,31,36,65})for(int roll:{0,23,24,30,31,35,36,64,65,99})for(int rate:{0,25,75})for(int block:{0,1}){
reset(moving,chance,roll);shield=rate;int result=SUNITDMG_ApplyBlockOrDodge(&game,&owner,&target,projectile,block);
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d]",moving,projectile,chance,roll,rate,block,result,draws);}
std::printf("],\"flags\":[");first=true;
for(int moving:{0,1,2})for(int chance:{0,24,36,65})for(int rate:{0,75})for(int physical:{0,256})for(int gethit:{0,1})for(int soft:{0,1})for(int knock:{0,100})for(int unt:{0,1})for(int roll:{0,23,24,30,31,35,36,64,65,99}){
reset(moving,chance,roll);shield=rate;uninterruptable=unt;row.nKnockBack=knock;row.dwMissileFlags=(gethit?gdwBitMasks[MISSILESFLAGINDEX_GETHIT]:0)|(soft?gdwBitMasks[MISSILESFLAGINDEX_SOFTHIT]:0);
D2DamageStrc damage;damage.dwPhysDamage=physical;MISSMODE_SetDamageFlags(&game,&shot,&target,&damage);
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,%d]",moving,chance,rate,physical,gethit,soft,knock,unt,roll,damage.wResultFlags,events,commits,draws);}
std::printf("],\"area\":[");first=true;
for(int moving:{0,1,2})for(int chance:{0,24,36,65})for(int roll:{0,23,24,30,31,35,36,64,65,99})for(int initial:{0,1,5})for(int missileOwner:{0,1})for(int unt:{0,1}){
reset(moving,chance,roll);uninterruptable=unt;D2DamageStrc original;original.wResultFlags=initial;
D2AuraCallbackStrc callback{&game,missileOwner?&shot:&owner,&original};sub_6FCF5DE0(&callback,&target);
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u]",moving,chance,roll,initial,missileOwner,unt,lastFlags,events,commits,draws,original.wResultFlags);}
std::printf("]}\n");}
'''
 p=STAGE/'amazon-avoid-source-probe.cpp';p.write_text(cpp);binary=STAGE/'amazon-avoid-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
 data=json.loads(subprocess.check_output([str(binary)]));assert {k:len(v) for k,v in data.items()}==dict(dispatch=1800,flags=7680,area=1440)
 for moving,projectile,chance,roll,shield,block,result,draws in data['dispatch']:
  rate=shield//3 if moving==2 else shield;tried=block and rate>0;blocked=tried and roll<rate;avoid=not blocked and not moving and projectile and chance>0
  expected=flags['BLOCKFLAG_BLOCK'] if blocked else flags['BLOCKFLAG_AVOID'] if avoid and roll<chance else 0
  assert (result,draws)==(expected,int(tried)+int(avoid))
 for moving,chance,rate,physical,gethit,soft,knock,unt,roll,result,events,commits,draws in data['flags']:
  shield=rate//3 if moving==2 else rate;shield_try=physical and shield>0;blocked=shield_try and roll<shield;avoid_try=not blocked and not moving and chance>0;avoided=avoid_try and roll<chance
  expected=flags['DAMAGERESULTFLAG_SUCCESSFULHIT'];defense=flags['BLOCKFLAG_BLOCK'] if blocked else flags['BLOCKFLAG_AVOID'] if avoided else 0
  if not unt:expected|=flags['DAMAGERESULTFLAG_GETHIT'] if gethit else flags['DAMAGERESULTFLAG_SOFTHIT'] if soft else 0;expected|=flags['DAMAGERESULTFLAG_KNOCKBACK'] if knock and roll<knock else 0
  if defense and not gethit and not knock:expected=expected&~flags['DAMAGERESULTFLAG_SUCCESSFULHIT']|flags['DAMAGERESULTFLAG_SOFTHIT'];defense=0
  if defense&2:expected|=flags['DAMAGERESULTFLAG_AVOID']
  if defense&0x11:expected|=flags['DAMAGERESULTFLAG_BLOCK']
  if defense:expected&=~flags['DAMAGERESULTFLAG_SUCCESSFULHIT']
  assert (result,events,commits,draws)==(expected,int(bool(expected&1)),1,int(not unt and knock>0)+int(bool(shield_try))+int(bool(avoid_try)))
 for moving,chance,roll,initial,missile_owner,unt,result,events,commits,draws,original in data['area']:
  avoided=bool(initial and not moving and roll<chance);expected=initial
  if avoided:expected=(expected|100)&0xFE
  if expected&1 and not unt:expected|=4
  assert (result,events,commits,draws,original)==(expected if initial else 0,int(bool(expected&1)),int(initial!=0),int(initial!=0 and not moving and chance>0),initial)
 fixture=STAGE/'amazon-avoid-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 # Reviewed only call chains distinguish missile/area defense from direct CE/aura effects.
 for path,routines in [('source/D2Game/src/MISSILES/MissMode.cpp',['MISSMODE_SrvHit01_Fireball_ExplodingArrow_FreezingArrowExplosion','MISSMODE_SrvHit13_GlacialSpike_HellMeteorDown','MISSMODE_SrvDo03_PoisonCloud_Blizzard_ThunderStorm_HandOfGod','MISSMODE_SrvDmgHitHandler']),('source/D2Game/src/SKILLS/Skills.cpp',['sub_6FD10200']),('source/D2Game/src/SKILLS/SkillNec.cpp',['sub_6FD0D000'])]:
  text=source(path)
  for name in routines:routine(text,name)
 skill=next(x for x in json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text()) if x['talentId']=='amazon.avoid')
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks)[:5],reviewedOnlyRoutines=list(blocks)[5:],blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},pinnedSkill=skill,
  probe=dict(caseCounts={k:len(v) for k,v in data.items()},fixture=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),currentObservation='tools/reference/amazon-avoidance-current-tooltips.json',
  established=['Full block/dodge dispatcher: shield first when requested, stationary missile Avoid after no/failed shield; moving selects separate Evade. Melee selects Dodge instead. Only positive chances draw; equality fails.',
  'Full missile flag wrapper always requests bAvoid1, physical nonzero controls shield request. Uninterruptable suppresses get-hit/knockback presentation but does not bypass the avoidance request. GetHit0/Knockback0 converts a defense result to soft-hit while still removing successful hit. Successful events are not dispatched; the final missile presentation call still occurs.',
  'Full area copy/wrapper requests Avoid with shield disabled and independently copies the damage packet for each victim. Zero initial result flags skip defense/events/commit. No mutation leaks into the shared original packet.',
  'Reviewed ordinary missile callback runs collision/NextHit/return-fire/hit-function (including chain child) before damage flags. Avoid must not erase already executed child creation or collision history.',
  'Reviewed Fireball/Glacial Spike/Meteor areas reach sub_6FD10200 -> copied area wrapper. Blizzard/cloud/Thunder Storm use ordinary missile collision. Reviewed Corpse Explosion callback directly dispatches damage events without this avoidance request; do not attach Avoid globally to all AoE/health damage.'],
  services=['Explicit table/stat/mode/owner services and deterministic integer draws, repeated per requested draw. Full seed independence, AR/world scan/path/factory/stat layers are not supplied.',
  'Event and final presentation services only record calls/flags; full health/control/armor/critical consumers and exact current executable behavior are not compiled by this probe. Reviewed-only routines are not included in the10920-case count.'],
  limits=['No native/modern/class completion certificate. Native integration/eligibility/double-roll controls and real projectile timing/reload acceptance remain separate.',
  'Existing approved accepted-running-step suppression supersedes source running Evade/block. Current Param8=2 and released2.4 override old action-lock behavior; no modern S1 animation/RNG equivalence inferred.',
  'Dodge3/Avoid3 current readings support retained two-stage rounding at those ranks; other parameters/ranks/contexts, owner/PvP and nonplayer passives remain separate.'])
 target=ROOT/'tools/reference/amazon-avoid-engine-evidence.json';rendered=json.dumps(evidence,indent=2)+'\n'
 if check:assert target.read_text()==rendered,'Avoid evidence changed'
 else:target.write_text(rendered)
 print('Avoid:1800 dispatch,7680 missile flags and1440 copied-area source cases pass; native/current executable proof separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
