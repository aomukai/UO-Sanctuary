#!/usr/bin/env python3
"""Execute unchanged NPC416/418 start/do2/contact/health/stun ordering."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'goatman-npc-release'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);parent=ROOT/'tools/reference/goatman-npc-melee-source-evidence.json';proof=json.loads(parent.read_text());inputs={str(parent.relative_to(ROOT)):sha(parent)}
 for group in ['inputs','artifacts']:
  for n,h in proof[group].items():assert sha(ROOT/n)==h,n;inputs[n]=h
 original=BASE/'goatman-npc-melee/source.cpp';assert sha(original)==proof['artifacts'][str(original.relative_to(ROOT))];cpp=original.read_text().split('bool first=true;',1)[0]
 for name,h in proof['retainedFullPacketRoutines'].items():assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==h,name
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a[:100];cpp=cpp.replace(a,b,1)
 replace('int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return 0;}', 'bool victimHire=false,victimBoss=false,victimUnique=false;int MONSTERS_GetHirelingTypeId(D2UnitStrc* u){return u->dwUnitId==29&&victimHire;}')
 replace('int high=0,criticalRoll','int victimDraw=0;int high=0,criticalRoll')
 replace('return high?bound-1:0;}', 'return bound==100?victimDraw:high?bound-1:0;}')
 replace('bool MONSTERS_IsBoss(D2MonStatsTxt*,D2UnitStrc*){return false;}', 'bool MONSTERS_IsBoss(D2MonStatsTxt*,D2UnitStrc*){return victimBoss;}')
 replace('bool MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc*,unsigned){return false;}', 'bool MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc*,unsigned flag){assert(flag==8);return victimUnique;}')
 a='struct D2StatListStrc{int flags=0,until=0,rank=0;};D2StatListStrc stateList;bool statePresent=false,allowState=true;int stateAllocs=0,stateToggles=0,stateRefreshes=0,stateEvents=0;'
 b=a+'\nD2StatListStrc victimStun;bool victimStunPresent=false;int wantedState=0,stunAllocations=0,stunToggles=0,stunRefreshes=0,stunEvents=0,healthAtStun=-1;'
 replace(a,b)
 replace('D2StatListStrc*STATLIST_GetStatListFromUnitAndState(D2UnitStrc* u,int){return u==&attacker&&statePresent?&stateList:nullptr;}',
 'D2StatListStrc*STATLIST_GetStatListFromUnitAndState(D2UnitStrc* u,int state){wantedState=state;if(u==&attacker&&state==95&&statePresent)return &stateList;if(u==&defender&&state==STATE_STUNNED&&victimStunPresent)return &victimStun;return nullptr;}')
 replace('D2StatListStrc*STATLIST_AllocStatList(void*,unsigned flags,int until,int type,int id){assert(type==UNIT_MONSTER&&id==17);stateAllocs++;if(!allowState)return nullptr;stateList={int(flags),until,0};return &stateList;}',
 'D2StatListStrc*STATLIST_AllocStatList(void*,unsigned flags,int until,int type,int id){assert(type==UNIT_MONSTER&&id==17);if(wantedState==STATE_STUNNED){assert(flags==2);stunAllocations++;healthAtStun=defender.stats[STAT_HITPOINTS];trace.push_back({30,healthAtStun,until});victimStun={int(flags),until,0};return &victimStun;}assert(wantedState==95);stateAllocs++;if(!allowState)return nullptr;stateList={int(flags),until,0};return &stateList;}')
 replace('void STATLIST_SetState(D2StatListStrc*,int state){assert(state==95);}', 'void STATLIST_SetState(D2StatListStrc*,int state){assert(state==95||state==STATE_STUNNED);}')
 replace('void D2COMMON_10475_PostStatToStatList(D2UnitStrc* u,D2StatListStrc*,int){assert(u==&attacker);statePresent=true;}', 'void D2COMMON_10475_PostStatToStatList(D2UnitStrc* u,D2StatListStrc*,int){if(u==&defender){victimStunPresent=true;}else{assert(u==&attacker);statePresent=true;}}')
 replace('void STATLIST_SetExpireFrame(D2StatListStrc*,int){__builtin_trap();}', 'void STATLIST_SetExpireFrame(D2StatListStrc* s,int until){assert(s==&victimStun);s->until=until;stunRefreshes++;healthAtStun=defender.stats[STAT_HITPOINTS];trace.push_back({31,healthAtStun,until});}')
 replace('void STATES_ToggleState(D2UnitStrc*,int state,int on){assert(state==95&&on==1);stateToggles++;}', 'void STATES_ToggleState(D2UnitStrc* u,int state,int on){assert(on==1);if(u==&defender){assert(state==STATE_STUNNED);stunToggles++;}else{assert(u==&attacker&&state==95);stateToggles++;}}')
 replace('void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int event,int until,int,int){assert(event==EVENTTYPE_REMOVESTATE&&until==stateList.until);stateEvents++;}', 'void EVENT_SetEvent(D2GameStrc*,D2UnitStrc* u,int event,int until,int,int){assert(event==EVENTTYPE_REMOVESTATE);if(u==&defender){assert(until==victimStun.until);stunEvents++;}else{assert(until==stateList.until);stateEvents++;}}')
 # Every production routine remains byte-for-byte source text. Only external
 # recipient/metadata/stat/event/random services changed in this new fixture.
 routines={}
 for name,h in {**proof['unchangedRoutines'],**proof['retainedFullPacketRoutines']}.items():
  assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==h,name;routines[name]=h
 cpp+=r'''
bool first=true;
void Emit(int id,int rank,int flags,int hp,int gate,int unique,int boss,int velocity,int hire,int roll,int existing,int elapsed,int resist){
Reset(id,rank);mon={};victimHire=victimBoss=victimUnique=false;victimDraw=roll;victimStun={2,2000,0};victimStunPresent=false;wantedState=0;stunAllocations=stunToggles=stunRefreshes=stunEvents=0;healthAtStun=-1;
attacker.dwAnimMode=4;attacker.stats[STAT_MINDAMAGE]=30;attacker.stats[STAT_MAXDAMAGE]=50;attacker.stats[STAT_DAMAGEPERCENT]=210;
attacker.stats[STAT_FIREMINDAM]=2;attacker.stats[STAT_FIREMAXDAM]=6;attacker.stats[STAT_LIGHTMINDAM]=3;attacker.stats[STAT_LIGHTMAXDAM]=7;attacker.stats[STAT_COLDMINDAM]=5;attacker.stats[STAT_COLDMAXDAM]=8;attacker.stats[STAT_COLDLENGTH]=31;
for(int stat:{STAT_DAMAGERESIST,STAT_MAGICRESIST,STAT_FIRERESIST,STAT_LIGHTRESIST,STAT_COLDRESIST})defender.stats[stat]=resist;
resultFlags=flags;int start=Start(rank);D2DamageStrc saved=attacker.pCombat?attacker.pCombat->tDamage:D2DamageStrc{};
defender.stats[STAT_HITPOINTS]=hp;victimHire=hire;victimBoss=boss;victimUnique=unique;victimStunPresent=existing;mon.nVelocity=velocity;mon.dwMonStatsFlags=0xffffffff&~gdwBitMasks[MONSTATSFLAGINDEX_INTOWN];
if(gate==1)inRange=false;if(gate==2)attacker.dwAnimMode=0;if(gate==3)hasTarget=false;if(gate==4)hasRoom=false;if(gate==5)interact=false;if(gate==6)town=true;if(gate==7)mon.dwMonStatsFlags&=~gdwBitMasks[MONSTATSFLAGINDEX_KILLABLE];
game.dwGameFrame+=elapsed;if(statePresent&&stateList.until<=game.dwGameFrame)statePresent=false;
// Changed release damage/rank/resistance cannot recalculate an allocated packet.
attacker.stats[STAT_MINDAMAGE]=900;attacker.stats[STAT_MAXDAMAGE]=999;attacker.stats[STAT_DAMAGEPERCENT]=999;
for(int stat:{STAT_DAMAGERESIST,STAT_MAGICRESIST,STAT_FIRERESIST,STAT_LIGHTRESIST,STAT_COLDRESIST})defender.stats[stat]=100;
bounds.clear();trace.clear();draws=modeCalls=thornsCalls=freeCalls=0;rangeBonus=-1;dispatched={};npcRank=rank==255?254:rank+1;
int action=SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws(&game,&attacker,id,npcRank);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],",first?"":",",id,rank,flags,hp,gate,unique,boss,velocity,hire,roll,existing,elapsed,resist,start,action,defender.stats[STAT_HITPOINTS],saved.dwPhysDamage,saved.dwMagDamage,saved.dwFireDamage,saved.dwLtngDamage,saved.dwColdDamage,saved.dwDmgTotal,saved.dwStunLen,dispatched.wResultFlags,dispatched.dwPhysDamage,dispatched.dwDmgTotal,dispatched.dwHitClass,modeCalls,thornsCalls,freeCalls,rangeBonus,attacker.pCombat!=nullptr,draws);first=false;
printf("[%d,%d,%d,%d,%d,%d,%d],",victimStunPresent,victimStun.until,stunAllocations,stunRefreshes,stunToggles,stunEvents,healthAtStun);State();printf(",[");for(size_t i=0;i<trace.size();i++)printf("%s[%d,%d,%d]",i?",":"",trace[i].kind,trace[i].a,trace[i].b);printf("],[");for(size_t i=0;i<bounds.size();i++)printf("%s%d",i?",":"",bounds[i]);printf("]]");
}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");
for(int id:{416,418})for(int rank:{1,8,20,255})for(int flags:{0,1,16})for(int hp:{10000000,25601,257,256,255,0})for(int gate=0;gate<=7;gate++)for(int elapsed:{0,100})Emit(id,rank,flags,hp,gate,0,0,5,0,99,0,elapsed,50);
for(int id:{416,418})for(int rank:{1,20,255})for(int unique:{0,1})for(int boss:{0,1})for(int velocity:{0,5})for(int hire:{0,1})for(int roll:{0,89,90,99})for(int existing:{0,1})for(int resist:{0,100})Emit(id,rank,1,256000,0,unique,boss,velocity,hire,roll,existing,0,resist);
printf("]\n");}
'''
 p=STAGE/'source.cpp';p.write_text(cpp);compiler=STAGE/'compiler.log';san=STAGE/'sanitizer.log'
 with compiler.open('w') as f:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(p.with_suffix(''))],stdout=f,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(p.with_suffix(''))],capture_output=True);san.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:];cases=json.loads(run.stdout)
 assert len(cases)==2304+1536
 comparisons=0;zeroStuns=0;lethalStuns=0;shortened=0
 for inp,out,stun,state,trace,draws in cases:
  id,rank,flags,hp,gate,unique,boss,velocity,hire,roll,existing,elapsed,resist=inp
  assert out[0]==1 and out[1]==int(gate!=3)
  reaches=bool(flags&1) and gate not in [1,2,3,4,5,6,7] and hp>0
  total=out[8] if reaches else 0;expectedHp=(0 if hp-total<256 else hp-total) if total>0 else hp;assert out[2]==expectedHp,(inp,out)
  eligible=reaches and id==416 and (not unique or roll>=90) and not boss and velocity!=0
  duration=min(30+min(rank-1,15)*5+max(rank-16,0)*2,250)
  if hire and duration>=13:duration=13
  if eligible:
   assert stun==[1,1000+elapsed+duration,int(not existing),int(existing),int(not existing),1,expectedHp],(inp,stun)
   at=next(i for i,x in enumerate(trace) if x[0] in [30,31]);writes=[i for i,x in enumerate(trace) if x[0]==21 and x[1]==29]
   assert not writes or writes[-1]<at
   if total==0:zeroStuns+=1
   if expectedHp==0:lethalStuns+=1
   if existing and stun[1]<2000:shortened+=1
  else:assert stun==[existing,2000,0,0,0,0,-1],(inp,stun)
  assert draws==([100] if reaches and id==416 and unique else []),(inp,draws)
  if id==418:
   assert state[0]==1 and state[3]==(254 if rank==255 else rank+1)
   assert state[1:3]==([2,1000+(75-min(110*rank//(rank+6)*50//100,50))] if elapsed==0 else [4,0]),(inp,state)
  comparisons+=1
 assert zeroStuns>0 and lethalStuns>0 and shortened>0
 out=BASE/'goatman-npc-release-source-probe.json';encoded=json.dumps(cases,separators=(',',':'))+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 result=dict(referenceVersion='3.3.93847',component='Goatman NPC start/do2/health/stun release composition',legacyCommit=proof['legacyCommit'],contexts=len(cases),zeroDamageStuns=zeroStuns,lethalHealthFieldStuns=lethalStuns,shortenedExistingStuns=shortened,processMemoryRead=False,currentExecutableParity=False,nativeConsumerAccepted=False,handlerEnabled=False,inputs=inputs,unchangedRoutineHashes=routines,artifacts={str(p.relative_to(ROOT)):sha(p) for p in [p,compiler,san,out,Path(__file__).resolve()]},
  established=['Complete unchanged NPCstart32/39,do2,allocation/fill/critical/mitigation/total,ExecuteEvents and durability/contact wrapper compose3840contexts under ASan+UBSan. Changed release rank/damage/resistance does not rebuild the stored packet;Berserk self-state refresh happens before target/contact guards.', 'Health commits before recipient stun. Eligible zero-damage/immune hits can stun. Unique recipients consume their one100-bound roll before boss/velocity exclusion;ordinary recipients consume none. Hireling13/ordinary250caps and unconditional shorter replacement expiry remain explicit. Lethal HP-field controls can transiently create stun before the later death-mode consumer;native lethal/reentrant equivalence is not claimed.', 'Out-of-range contact frees its packet. Death-mode0successful-hit early return retains it for outer lifecycle cleanup. Missing target bypasses contact. Room/hostility/town/killable/dead guards preserve their distinct source flags/event paths. Attack-attempt callbacks and thorns/return follow health/stun through the unchanged wrapper.'],
  services=['Shared source formula/stat/random/allocation/event/metadata services are declared. Actor/recipient are NPCs with no inventory,leech,poison or real event subscribers. Recipient new-state allocation succeeds;existing-state and expiry-before-release are explicit supplied services. Source event/death/animation engine completion and modern event16/CrushingBlow are not supplied.', 'Town refusal uses an NPC without the INTOWNflag,matching the ordinary Goatman table. The single supplied monstats fixture supports separate boss/unique/velocity/hire controls;it is not a current world actor. No C#/runtime data/DLL changes were made while the separate full class regression was running.'])
 target=ROOT/'tools/reference/goatman-npc-release-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==encoded
 else:target.write_text(encoded)
 print(f'NPC release:{comparisons}complete-source contexts;zero-damage stuns={zeroStuns},lethal health-field stuns={lethalStuns},shorter refreshes={shortened};native consumer pending.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
