#!/usr/bin/env python3
"""Complete NPC item freeze callback,zero-result events,mitigation,freeze and removal."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
from probe_npc_damage_fill import enum
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-freeze-hit-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);parent=BASE/'npc-flat-drains-source/source.cpp';prior=json.loads((ROOT/'tools/reference/npc-flat-drains-source-evidence.json').read_text());assert sha(parent)==prior['artifacts'][str(parent.relative_to(ROOT))]
 cpp=parent.read_text().split('int main(){for(int i=0;i<32;i++)')[0];tree=json.loads((BASE/'d2moo-source-tree.json').read_text());inputs={};bodies={}
 def read(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 source=read('source/D2Game/src/UNIT/SUnitDmg.cpp');skill=read('source/D2Game/src/SKILLS/SkillItem.cpp');states=enum(read('source/D2Common/include/D2States.h'),'D2C_States');events=enum(read('source/D2Game/include/GAME/Event.h'),'D2C_EventTypes : uint8_t');stats=enum(read('source/D2Common/include/D2StatList.h'),'D2C_ItemStats')
 for text,names in[(source,['SUNITDMG_ExecuteEvents','SUNITDMG_CalculateTotalDamage','SUNITDMG_ApplyResistancesAndAbsorb','SUNITDMG_ApplyFreezeState','SUNITDMG_RemoveFreezeState']),(skill,['SKILLITEM_EventFunc14_Freeze'])]:
  for n in names:bodies[n]=function(text,n)
 bound={}
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a[:100];cpp=cpp.replace(a,b)
 def stub(name,text):
  nonlocal cpp
  pattern=r'^[^\n;]*\b'+name+r'\([^\n;]*?\)\{[^\n]*\}\s*$';cpp,n=re.subn(pattern,lambda _:text,cpp,flags=re.M);assert n==1,(name,n)
 # Previously header-bound stat/event values remain audited;new state and
 # scheduler constants use their actual namespaces,not fixture ordinals.
 for name,value in states.items():
  pattern=r'constexpr int '+name+r'=\d+;'
  if re.search(pattern,cpp):cpp=re.sub(pattern,'constexpr int '+name+'='+str(value)+';',cpp);bound[name]=value
 for name in('STATE_FREEZE','STATE_UNINTERRUPTABLE'):
  if name not in bound:cpp='constexpr int '+name+'='+str(states[name])+';\n'+cpp;bound[name]=states[name]
 replace('EVENTTYPE_REMOVESTATE=1,EVENTTYPE_STATREGEN=2','EVENTTYPE_REMOVESTATE='+str(events['EVENTTYPE_REMOVESTATE'])+',EVENTTYPE_STATREGEN='+str(events['EVENTTYPE_STATREGEN']))
 cpp='bool known=true;constexpr int STAT_LEVEL='+str(stats['STAT_LEVEL'])+';constexpr int EVENTTYPE_AITHINK='+str(events['EVENTTYPE_AITHINK'])+';\n'+cpp
 bound.update({n:events[n]for n in('EVENTTYPE_REMOVESTATE','EVENTTYPE_STATREGEN','EVENTTYPE_AITHINK')});bound.update({n:stats[n]for n in('STAT_LEVEL','STAT_ITEM_FREEZE','STAT_COLDRESIST','STAT_ITEM_CANNOTBEFROZEN','STAT_ITEM_HALFFREEZEDURATION')})
 replace('struct D2CombatStrc;struct D2GameStrc{','struct D2MonsterDataStrc;struct D2CombatStrc;struct D2GameStrc{int nGameType=0,dwGameType=0;')
 replace('struct D2UnitStrc{int dwUnitType','struct D2UnitStrc{D2MonsterDataStrc*pMonsterData=nullptr;int dwUnitType')
 replace('int wMonType=0,nCrit=5,','int nColdEffect[3]={-50,-40,-25},nAIdel[3]={15,14,13};int wMonType=0,nCrit=5,')
 replace('int dwResistPenalty=0,','int dwMonsterFreezeDiv=1;int dwResistPenalty=0,')
 replace('int flags=0,until=0,rank=0;','int flags=0,until=0,rank=0,owner=0;')
 # Globals follow original source data/service declarations.
 marker='D2StatListStrc victimStun;'
 extra='struct D2MonsterDataStrc{D2MonStatsTxt*pMonstatsTxt;};D2MonsterDataStrc monsterData{&mon};D2StatListStrc freezeList;bool frozen=false,uninterruptible=false,stay=false;int removeFrame=0,aiFrame=0,modeCallsFreeze=0,statusHp=-1,nestedCalls=0;D2DamageStrc nestedPacket;\n'
 replace(marker,extra+marker)
 old='bool STATES_CheckState(D2UnitStrc*u,int state){return u==&defender&&((state==LIFE_TAP_STATE&&tapOn)||(state==STATE_DEATH_DELAY&&deathDelay));}'
 replace(old,'bool STATES_CheckState(D2UnitStrc*u,int state){return u==&defender&&((state==STATE_UNINTERRUPTABLE&&uninterruptible)||(state==LIFE_TAP_STATE&&tapOn)||(state==STATE_DEATH_DELAY&&deathDelay));}')
 stub('MONSTERMODE_GetMonStatsTxtRecord','D2MonStatsTxt*MONSTERMODE_GetMonStatsTxtRecord(int){return known?&mon:nullptr;}')
 stub('STATLIST_GetStatListFromUnitAndState','D2StatListStrc*STATLIST_GetStatListFromUnitAndState(D2UnitStrc*u,int state){wantedState=state;if(state==STATE_FREEZE)return frozen?&freezeList:nullptr;if(u==&defender&&state==STATE_STUNNED&&victimStunPresent)return &victimStun;return nullptr;}')
 stub('STATLIST_AllocStatList','D2StatListStrc*STATLIST_AllocStatList(void*,unsigned flags,int until,int type,int id){assert(type==UNIT_MONSTER&&id==17&&flags==2);statusHp=defender.stats[STAT_HITPOINTS];if(wantedState==STATE_FREEZE){freezeList={int(flags),until,0,id};return &freezeList;}victimStun={int(flags),until,0,id};return &victimStun;}')
 stub('STATLIST_SetState','void STATLIST_SetState(D2StatListStrc*,int){}')
 stub('D2COMMON_10475_PostStatToStatList','void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*s,int){if(s==&freezeList)frozen=true;else victimStunPresent=true;}')
 stub('STATLIST_SetExpireFrame','void STATLIST_SetExpireFrame(D2StatListStrc*s,int until){s->until=until;statusHp=defender.stats[STAT_HITPOINTS];}')
 cpp+='\nint STATLIST_GetExpireFrame(D2StatListStrc*s){return s->until;}\nbool STATES_CheckStateMaskStayDeathOnUnitByStateId(D2UnitStrc*,int){return stay;}\nvoid SUNIT_SetCombatMode(D2GameStrc*,D2UnitStrc*,int){modeCallsFreeze++;}\nvoid FOG_DisplayWarning(const char*,const char*,int){}\n'
 stub('STATES_ToggleState','void STATES_ToggleState(D2UnitStrc*,int state,int on){if(state==STATE_FREEZE){frozen=on!=0;statusHp=defender.stats[STAT_HITPOINTS];}}')
 stub('EVENT_SetEvent','void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int event,int until,int,int){if(event==EVENTTYPE_REMOVESTATE)removeFrame=until;else{assert(event==EVENTTYPE_AITHINK);aiFrame=until;}}')
 stub('D2GAME_EVENTS_Delete_6FC34840','void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int event,int){assert(event==EVENTTYPE_AITHINK);}')
 stub('SUNITDMG_ApplyFreezeState','void SUNITDMG_ApplyFreezeState(D2UnitStrc*,D2UnitStrc*,int);')
 # The nested call is observed without altering its complete body.
 cpp=cpp.replace('void __fastcall SUNITDMG_ExecuteEvents(', 'void __fastcall SUNITDMG_ExecuteEvents(')
 cpp+='\n'+bodies['SUNITDMG_RemoveFreezeState']+'\n'+bodies['SUNITDMG_ApplyFreezeState']+'\n'+bodies['SKILLITEM_EventFunc14_Freeze']
 main=r'''
void clean(){Reset(416,1);mon={};mon.nCrit=0;victimHire=victimBoss=victimUnique=victimStunPresent=tapOn=deathDelay=false;frozen=uninterruptible=stay=false;known=true;freezeList={};removeFrame=aiFrame=modeCallsFreeze=0;statusHp=-1;defender.pMonsterData=&monsterData;monsterData.pMonstatsTxt=&mon;difficulty.dwMonsterFreezeDiv=1;attacker.stats[STAT_HITPOINTS]=defender.stats[STAT_HITPOINTS]=25600;}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("{\"callback\":[");bool first=true;
for(int level:{1,20,60,99})for(int target:{1,20,60,99})for(int bonus:{0,1,2,5})for(int ranged:{0,1})for(int roll:{0,49,99}){
clean();attacker.stats[STAT_LEVEL]=level;defender.stats[STAT_LEVEL]=target;attacker.stats[134]=bonus;criticalRoll=roll;int result=SKILLITEM_EventFunc14_Freeze(&game,ranged?6:5,&attacker,&defender,nullptr,134<<16,0);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",level,target,bonus,ranged,roll,result,draws,frozen,freezeList.until,freezeList.owner,defender.stats[STAT_HITPOINTS],events);first=false;
}printf("],\"status\":[");first=true;
for(int frames:{1,3,50,101,225})for(int diff:{0,1,2})for(int resist:{-100,0,50,100})for(int flags:{0,1,2,4,8,16})for(int half:{0,1})for(int cannot:{0,1})for(int effect:{-50,0})for(int existing:{0,1}){
clean();game.nDifficulty=diff;difficulty.dwMonsterFreezeDiv=diff==0?1:diff==1?2:4;uninterruptible=flags==1;victimBoss=flags==2;victimUnique=flags==4;victimHire=flags==8;defender.stats[STAT_HITPOINTS]=flags==16?0:25600;mon.nColdEffect[diff]=effect;frozen=existing;freezeList={2,existing?1200:0,0,existing?41:0};defender.stats[STAT_COLDRESIST]=resist;defender.stats[STAT_ITEM_HALFFREEZEDURATION]=half;defender.stats[STAT_ITEM_CANNOTBEFROZEN]=cannot;
D2DamageStrc p;p.dwFrzLen=frames;SUNITDMG_ExecuteEvents(&game,&attacker,&defender,1,&p);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",frames,diff,resist,flags,half,cannot,effect,existing,p.dwFrzLen,frozen,freezeList.until,freezeList.owner,removeFrame,aiFrame,defender.stats[STAT_HITPOINTS]);first=false;
}printf("],\"remove\":[");first=true;
for(int dead:{0,1})for(int stayOnDeath:{0,1})for(int hasData:{0,1})for(int hasRecord:{0,1})for(int gameType:{0,1,2})for(int diff:{0,1,2})for(int delay:{-1,0,7}){
clean();frozen=true;defender.stats[STAT_HITPOINTS]=dead?0:25600;stay=stayOnDeath;defender.pMonsterData=hasData?&monsterData:nullptr;monsterData.pMonstatsTxt=hasRecord?&mon:nullptr;game.nGameType=gameType==1;game.dwGameType=gameType==2;game.nDifficulty=diff;for(int i=0;i<3;i++)mon.nAIdel[i]=delay+i*10;
SUNITDMG_RemoveFreezeState(&defender,STATE_FREEZE,&freezeList);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",dead,stayOnDeath,hasData,hasRecord,gameType,diff,delay,frozen,modeCallsFreeze,aiFrame);first=false;
}printf("]}\n");}
'''
 cpp+='\n'+main
 for n,b in bodies.items():assert function(cpp,n)==b,n
 path=STAGE/'source.cpp';path.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as f:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(path),'-o',str(binary)],stdout=f,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True,timeout=120);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 data=json.loads(run.stdout);assert {k:len(v)for k,v in data.items()}==dict(callback=384,status=5760,remove=432)
 out=STAGE/'cases.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n');inputs[str(parent.relative_to(ROOT))]=sha(parent);inputs[str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__).resolve())
 record=dict(referenceVersion='3.3.93847',currentExecutableParity=False,contexts={k:len(v)for k,v in data.items()},completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},newHeaderBindings=bound,inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in(path,binary,out,STAGE/'compiler.log',STAGE/'sanitizer.log')},scope='Complete item callback14 through complete zero-result missile ExecuteEvents,total/resistance and freeze/remove bodies. Ordinary actor/room/empty-event/stat-list/timer/meta/RNG services declared. Cold fallback is a declared delegation;existing cold-state evidence covers its body. No invented successful hit,HP loss or return event. Older source,not modern executable certification.')
 target=ROOT/'tools/reference/npc-freeze-hit-source-evidence.json';encoded=json.dumps(record,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'NPC freeze source proof stale'
 else:target.write_text(encoded)
 print('NPC freeze:384full callback,5760zero-result status,432removal contexts;six unchanged bodies;ASan/UBSan pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
