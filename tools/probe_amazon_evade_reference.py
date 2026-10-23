#!/usr/bin/env python3
"""Compile positive moving Evade dispatch and full missile presentation routine."""
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
 damage=source('source/D2Game/src/UNIT/SUnitDmg.cpp');header=source('source/D2Game/include/UNIT/SUnitDmg.h')
 for name in ['SUNITDMG_ApplyDodge','SUNITDMG_ApplyBlockOrDodge','SUNITDMG_ExecuteMissileDamage']:routine(damage,name)
 flags={n:int(v,16) for n,v in re.findall(r'^\s*((?:DAMAGERESULTFLAG|BLOCKFLAG)_\w+)\s*=\s*(0x[0-9a-fA-F]+)',header,re.M)}
 joined='\n'.join(blocks.values());other=sorted(set(re.findall(r'\b(?:STAT|STATE|MONSTATSFLAGINDEX|MONMODE|PLRMODE|MONSTER|UNITFLAG)_[A-Z0-9_]+',joined)) - {'MONMODE_WALK','MONMODE_RUN','PLRMODE_WALK','PLRMODE_RUN'})
 cpp=r'''#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <initializer_list>
#include <cmath>
#define __fastcall
using BOOL=int;constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,PLRMODE_WALK=2,PLRMODE_RUN=3,MONMODE_WALK=2,MONMODE_RUN=15;
'''
 cpp+='enum {'+','.join(other)+'};\nconstexpr uint32_t '+','.join(n+'='+str(v) for n,v in flags.items())+';\n'
 cpp+='constexpr uint32_t gdwBitMasks[]={'+','.join(str(1<<i)+'u' for i in range(32))+'};\n'
 cpp+=r'''struct D2UnitStrc{int dwUnitType=0,dwAnimMode=1,dwClassId=0,dwUnitId=1,pSeed=0,dwLastHitClass=0;uint32_t dwFlags=0;void* pInventory=nullptr;} owner,target;
struct D2GameStrc{bool bExpansion=true;int dwGameFrame=0;} game;
struct D2DamageStrc{uint32_t wResultFlags=0;int nHitClassActiveSet=0,dwHitClass=0,dwDmgTotal=0;} damage;
struct D2StatListStrc{} state;struct D2SkillStrc{} skill;struct D2SkillsTxt{int wStSound=1;} row;
struct D2ActiveRoomStrc{};struct D2MonStatsTxt{uint32_t dwMonStatsFlags=0;} mon;
struct D2MonStats2Txt{uint32_t dwModeFlags=0;} mon2;struct D2ModeChangeStrc{D2UnitStrc* pTargetUnit=nullptr;};
int evade=0,draw=0,draws=0,shield=0,sounds=0,modes=0,skillFlags=0,stateWrites=0;bool hasState=true,learned=true,hasRow=true,unt=false;
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int){return stat==STAT_PASSIVE_EVADE?evade:stat==STAT_PASSIVE_DODGE?18:stat==STAT_PASSIVE_AVOID?24:0;}
int ITEMS_RollRandomNumber(int*){draws++;return draw;}
int SUNITDMG_GetWeaponBlock(D2UnitStrc*){return 0;}
void COMPOSIT_GetWeaponClassId(D2UnitStrc*,void*,int* value,int,int){*value=0;}
int UNITS_GetBlockRate(D2UnitStrc*,bool){return shield;}
bool UNITS_IsInMovingMode(D2UnitStrc* p){return p->dwAnimMode==PLRMODE_WALK || p->dwAnimMode==PLRMODE_RUN;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return nullptr;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return false;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return &mon;}
int SUNITDMG_GetHitClass(D2DamageStrc*,int value){return value;}
bool STATES_CheckState(D2UnitStrc*,int value){return value==STATE_UNINTERRUPTABLE && unt;}
void STATES_ToggleState(D2UnitStrc*,int,int){stateWrites++;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return hasState?&state:nullptr;}
int STATLIST_GetStatValue(D2StatListStrc*,int,int){return 29;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int id){if(id!=29)std::abort();return learned?&skill:nullptr;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){if(id!=29)std::abort();return hasRow?&row:nullptr;}
void SUNIT_AttachSound(D2UnitStrc*,int code,D2UnitStrc*){if(code!=12)std::abort();sounds++;}
template<class... T>void sub_6FC817D0(T...){modes++;}
template<class... T>void D2GAME_PLAYERMODE_Change_6FC81A00(T...){modes++;}
void STATLIST_SetUnitStat(D2UnitStrc*,int,int,int){}
bool sub_6FCC1870(D2UnitStrc*,D2DamageStrc*,int){return true;}
void UNITROOM_RefreshUnit(D2UnitStrc*){}
int SKILLS_GetFlags(D2SkillStrc*){return 0;}void SKILLS_SetFlags(D2SkillStrc*,int){skillFlags++;}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return &mon2;}
void sub_6FC62DF0(D2UnitStrc*,D2DamageStrc*){}
void SUNITDMG_KillMonster(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){}
void MONSTERMODE_GetModeChangeInfo(D2UnitStrc*,int,D2ModeChangeStrc*){}
void D2GAME_ModeChange_6FC65220(D2GameStrc*,D2ModeChangeStrc*,int){modes++;}
void MONSTER_SetAiState(D2UnitStrc*,int){}
void sub_6FC6E8A0(D2GameStrc*,D2UnitStrc*){}
int sub_6FC62F50(D2UnitStrc*){return 0;}
void reset(){target={};damage={};sounds=modes=skillFlags=stateWrites=draws=0;hasState=learned=hasRow=true;unt=false;row.wStSound=1;}
'''
 cpp+='\n'.join(blocks.values())
 cpp+=r'''int main(){bool first=true;std::printf("{\"dispatch\":[");
for(int kind:{UNIT_PLAYER,UNIT_MONSTER})for(int moving:{0,1,2})for(int projectile:{0,1})for(int chance:{0,18,29,40,56})for(int roll:{0,17,18,28,29,39,40,55,56,99})for(int rate:{0,25,75})for(int block:{0,1}){
reset();target.dwUnitType=kind;target.dwAnimMode=moving==0?1:moving==1?(kind==UNIT_PLAYER?PLRMODE_WALK:MONMODE_WALK):(kind==UNIT_PLAYER?PLRMODE_RUN:MONMODE_RUN);evade=chance;draw=roll;shield=rate;
int result=SUNITDMG_ApplyBlockOrDodge(&game,&owner,&target,projectile,block);
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d]",kind,moving,projectile,chance,roll,rate,block,result,draws);}
std::printf("],\"presentation\":[");first=true;
for(int extra:{0,int(DAMAGERESULTFLAG_WILLDIE),int(DAMAGERESULTFLAG_GETHIT),int(DAMAGERESULTFLAG_KNOCKBACK),int(DAMAGERESULTFLAG_WILLDIE|DAMAGERESULTFLAG_GETHIT)})for(int attacker:{0,1})for(int unit:{0,1})for(int interrupt:{0,1})for(int present:{0,1})for(int level:{0,1})for(int record:{0,1})for(int sound:{0,1,12}){
reset();hasState=present;learned=level;hasRow=record;row.wStSound=sound;unt=interrupt;damage.wResultFlags=DAMAGERESULTFLAG_EVADE|extra;
SUNITDMG_ExecuteMissileDamage(&game,attacker?&owner:nullptr,unit?&target:nullptr,&damage);
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",extra,attacker,unit,interrupt,present,level,record,sound,sounds,modes,skillFlags,stateWrites);}
std::printf("],\"priority\":[");first=true;
for(int flag:{int(DAMAGERESULTFLAG_EVADE),int(DAMAGERESULTFLAG_EVADE|DAMAGERESULTFLAG_DODGE),int(DAMAGERESULTFLAG_EVADE|DAMAGERESULTFLAG_AVOID)}){
reset();damage.wResultFlags=flag;SUNITDMG_ExecuteMissileDamage(&game,&owner,&target,&damage);
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d]",flag,sounds,modes,skillFlags);}
std::printf("]}\n");}
'''
 p=STAGE/'amazon-evade-source-probe.cpp';p.write_text(cpp);binary=STAGE/'amazon-evade-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
 data=json.loads(subprocess.check_output([str(binary)]));assert {k:len(v) for k,v in data.items()}==dict(dispatch=3600,presentation=960,priority=3)
 for kind,moving,projectile,chance,roll,rate,block,result,draws in data['dispatch']:
  shield=rate//3 if kind==0 and moving==2 else rate;tried=block and shield>0;blocked=tried and roll<shield;passive=chance if moving else 24 if projectile else 18;second=not blocked and passive>0
  expected=flags['BLOCKFLAG_BLOCK'] if blocked else (flags['BLOCKFLAG_EVADE'] if moving else flags['BLOCKFLAG_AVOID'] if projectile else flags['BLOCKFLAG_DODGE']) if second and roll<passive else 0
  assert (result,draws)==(expected,int(tried)+int(second))
 for extra,attacker,unit,unt,state,level,row,sound,sounds,modes,skill_flags,state_writes in data['presentation']:
  assert (sounds,modes,skill_flags,state_writes)==(int(attacker and unit and not unt and state and level and row and sound>0),0,0,int(unt and bool(extra&flags['DAMAGERESULTFLAG_WILLDIE'])))
 assert data['priority']==[[flags['DAMAGERESULTFLAG_EVADE'],1,0,0],[flags['DAMAGERESULTFLAG_EVADE']|flags['DAMAGERESULTFLAG_DODGE'],0,1,1],[flags['DAMAGERESULTFLAG_EVADE']|flags['DAMAGERESULTFLAG_AVOID'],0,1,1]]
 fixture=STAGE/'amazon-evade-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 skill=next(x for x in json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text()) if x['talentId']=='amazon.evade')
 out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},pinnedSkill=skill,currentObservation='tools/reference/amazon-avoidance-current-tooltips.json',
  probe=dict(caseCounts={k:len(v) for k,v in data.items()},fixture=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),
  established=['Full unchanged block/dodge dispatcher chooses moving Evade for either melee or missile on players and monsters, with no stationary fallback when Evade0/fails. Shield is attempted first when requested; only source player running divides block by3. Zero chance draws none; equality fails.',
   'Full unchanged missile presentation function: player Evade flags request sound12 only with a state, learned skill, row and positive sound. It returns without mode changes or skill-flag writes, even with get-hit/knockback/will-die companion flags. Earlier uninterruptable/death-delay and null-attacker/target guards are observed separately.',
   'Mixed result flags prioritize Dodge, then Avoid before Evade; those standing branches set skill flags and request mode13. This does not establish that ordinary source flow generates mixed flags.',
   'Current Evade dm12 parameters10/65 and user effective6=40 agree with the retained native curve. Missing Param8/row animS1 alone does not establish a server Evade animation lock.'],
  services=['Declared stats, modes, shield rate and integer draws; no real source RNG stream, full stat lists, weapon-block, world/AR/damage calculation or modern executable is supplied.',
   'Full presentation routine compiles unchanged with explicit no-town and player fixtures; native death/control/mode/sound functions are recorded services. Only Evade guards/priority and bounded standing controls are exercised. Monster presentation and town/health consumers are outside the matrix.'],
  limits=['Source preparation, not native/class/current executable certification. All native integration, actual accepted movement, enemy hit/bolt, gear and reload checks remain separate.',
   'Approved Sanctuary running-step suppression supersedes source running Evade and reduced block; native implementation must enable Evade only during accepted walking.',
   'Legacy historical Evade-lock prose conflicts with the compiled sound-only server branch. Full current client/animation/audio/owner/PvP/nonplayer and mapped spell eligibility remain acceptance work; do not invent a stop/lock from that prose.'])
 target=ROOT/'tools/reference/amazon-evade-engine-evidence.json';rendered=json.dumps(out,indent=2)+'\n'
 if check:assert target.read_text()==rendered,'Evade evidence changed'
 else:target.write_text(rendered)
 print('Evade:3600 dispatch,960 full-function presentation and3 mixed-priority source cases pass; native/current proof separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
