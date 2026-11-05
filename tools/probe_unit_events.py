#!/usr/bin/env python3
"""Execute complete older event lists, callback selector and event-stat changes."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'unit-events-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={};bodies={}
 def read(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(v['sha'] for v in tree['tree'] if v['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 text=read('source/D2Game/src/UNIT/SUnitEvent.cpp')
 for n in ('SUNITEVENT_FreeEventList','SUNITEVENT_Register','SUNITEVENT_FreeEvent','SUNITEVENT_Unregister','SUNITEVENT_GetEvent','SUNITEVENT_Trigger'):bodies[n]=function(text,n)
 bodies['sub_6FD156A0']=function(read('source/D2Game/src/SKILLS/Skills.cpp'),'sub_6FD156A0')
 bodies['D2GAME_ITEMMODE_ServerStatlistCallback_6FC41910']=function(read('source/D2Game/src/ITEMS/ItemMode.cpp'),'D2GAME_ITEMMODE_ServerStatlistCallback_6FC41910')
 header=read('source/D2Game/include/UNIT/SUnitEvent.h');assert 'enum D2C_UnitEventTypes: uint8_t' in header and 'uint16_t nFlags;' in header and 'uint32_t nQueueNo;' in header
 cost=read('source/D2Common/include/DataTbls/ItemsTbls.h');assert re.search(r'int16_t\s+wItemEvent\[2\]',cost) and re.search(r'uint16_t\s+wItemEventFunc\[2\]',cost)
 table=bodies['sub_6FD156A0'].split('gpEventCallbackTable_6FD40D20[] =',1)[1].split('{',1)[1].split('};',1)[0]
 names=[v.strip() for v in table.split(',') if v.strip()];assert len(names)==50 and names[0]=='nullptr' and all(n=='nullptr' for n in names[32:])
 aliases='\n'.join('#define '+n+' callback<'+str(i)+'>' for i,n in enumerate(names) if n!='nullptr')
 cpp=r'''
#include <algorithm>
#include <array>
#include <cstdint>
#include <cstdio>
#include <set>
#include <vector>
#define __fastcall
#define __stdcall
struct D2GameStrc{void*pMemoryPool=nullptr;};struct D2UnitStrc;struct D2DamageStrc{};
using D2C_UnitEventTypes=uint8_t;
using D2UnitEventCallbackFunction=int32_t(*)(D2GameStrc*,int32_t,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*,int32_t,int32_t);
constexpr int UNITEVENTFLAG_IN_CALLBACK=1,UNITEVENTFLAG_SHOULD_BE_FREED=2,UNIT_PLAYER=0,UNIT_MONSTER=1,PLRMODE_TOWNNEUTRAL=5;
struct D2UnitEventStrc{D2C_UnitEventTypes nUnitEvent=0;uint16_t nFlags=0;uint32_t nQueueNo=0;int32_t unk0x08=0,nGUID1=0,nGUID2=0;D2UnitEventCallbackFunction pCallback=nullptr;D2UnitEventStrc*pPrevious=nullptr,*pNext=nullptr;int id=0;};
struct D2MonStatsTxt{uint32_t dwDamageRegen=0;};struct MonsterData{D2MonStatsTxt*pMonstatsTxt=nullptr;};
struct D2UnitStrc{D2UnitEventStrc*pSrvUnitEventsList=nullptr;int dwUnitId=1,dwUnitType=1,dwClassId=1,dwAnimMode=1;MonsterData*pMonsterData=nullptr;};
struct D2ItemStatCostTxt{int16_t wItemEvent[2]={-1,-1};uint16_t wItemEventFunc[2]={};}meta;
struct D2SkillsTxt{int nCharClass=0;};struct D2SkillStrc{};
struct Tables{int nStatesTxtRecordCount=200,nStuff=6,nShiftedStuff=63;}tables;auto*sgptDataTables=&tables;
constexpr int STAT_MAXHP=7,STAT_HITPOINTS=6,STAT_HPREGEN=74,STAT_MAXMANA=9,STAT_MANA=8,STAT_MAXSTAMINA=11,STAT_STAMINA=10;
constexpr int STAT_ITEM_NONCLASSSKILL=97,STAT_ITEM_SINGLESKILL=107,STAT_ITEM_ADDCLASSSKILLS=83,STAT_ITEM_ADDSKILL_TAB=188,STAT_ITEM_ELEMSKILL=126,STAT_ITEM_ALLSKILLS=127,STAT_ITEM_AURA=151,STAT_ITEM_CHARGED_SKILL=204,STAT_STATE=98;
int allocated=0;std::set<D2UnitEventStrc*>live;std::vector<std::vector<int64_t>>trace;int mode=0;bool acted=false,recordExists=true;
D2UnitEventStrc*allocate(){auto*p=new D2UnitEventStrc{};p->id=++allocated;live.insert(p);return p;}
void release(D2UnitEventStrc*p){if(!live.erase(p))__builtin_trap();delete p;}
#define D2_CALLOC_STRC_POOL(pool,T) allocate()
#define D2_FREE_POOL(pool,p) release(p)
void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int32_t,int32_t);
void SUNITEVENT_Trigger(D2GameStrc*,D2C_UnitEventTypes,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*);
D2UnitEventStrc*SUNITEVENT_Register(D2GameStrc*,D2UnitStrc*,D2C_UnitEventTypes,int32_t,int32_t,D2UnitEventCallbackFunction,uint32_t,int32_t);
template<int I>int32_t callback(D2GameStrc*g,int32_t phase,D2UnitStrc*u,D2UnitStrc*,D2DamageStrc*,int32_t a,int32_t b){
trace.push_back({0,I,phase,a,b});
if(a==1000&&!acted){acted=true;D2UnitEventStrc*current=nullptr;for(auto*n=u->pSrvUnitEventsList;n;n=n->pNext)if(n->nGUID1==a&&(n->nFlags&1)){current=n;break;}if(!current)__builtin_trap();
if(mode==2)SUNITEVENT_Register(g,u,(uint8_t)phase,9000,9001,callback<21>,2,9000);
if(mode==3){SUNITEVENT_Unregister(g,u,(int32_t)current->nQueueNo,current->unk0x08);trace.push_back({21,current->nFlags});}
if(mode==4)SUNITEVENT_Trigger(g,phase==5?6:5,u,nullptr,nullptr);
}return mode==1?7:0;}
D2ItemStatCostTxt*SKILLS_GetItemStatCostTxtRecord(int stat){trace.push_back({10,stat});return recordExists?&meta:nullptr;}
// Non-event stat effects are mandatory external services and outside this
// execution domain. Trap if a case accidentally enters one of those branches.
template<class...A>int STATLIST_UnitGetStatValue(A...){__builtin_trap();}
template<class...A>void STATLIST_SetUnitStat(A...){__builtin_trap();}
template<class T>T D2Clamp(T x,T lo,T hi){return std::clamp(x,lo,hi);}
template<class...A>D2SkillsTxt*SKILLS_GetSkillsTxtRecord(A...){__builtin_trap();}
template<class...A>D2SkillStrc*SKILLS_GetSkillById(A...){__builtin_trap();}
template<class...A>D2SkillStrc*SKILLS_AddSkill(A...){__builtin_trap();}
template<class...A>void SKILLS_AssignSkill(A...){__builtin_trap();}
template<class...A>void D2GAME_PACKETS_SendPacket0x21_UpdateSkills_6FC3DB50(A...){__builtin_trap();}
template<class...A>void*SUNIT_GetClientFromPlayer(A...){__builtin_trap();}
template<class...A>void SKILLS_RefreshSkill(A...){__builtin_trap();}
template<class...A>void sub_6FD108D0(A...){__builtin_trap();}
template<class...A>int SKILLS_GetSkillLevel(A...){__builtin_trap();}
template<class...A>D2SkillStrc*UNITS_GetLeftSkill(A...){__builtin_trap();}
template<class...A>D2SkillStrc*UNITS_GetRightSkill(A...){__builtin_trap();}
template<class...A>void D2GAME_AssignSkill_6FD13800(A...){__builtin_trap();}
template<class...A>int MONSTERS_GetHirelingTypeId(A...){__builtin_trap();}
template<class...A>void D2GAME_RefreshPassiveSkills_6FD14CC0(A...){__builtin_trap();}
template<class...A>void D2COMMON_11013_ConvertMode(A...){__builtin_trap();}
template<class...A>void SKILLITEM_ActivateAura(A...){__builtin_trap();}
template<class...A>void SKILLITEM_DeactivateAura(A...){__builtin_trap();}
template<class...A>D2UnitStrc*STATLIST_GetOwner(A...){__builtin_trap();}
template<class...A>void D2Common_10954(A...){__builtin_trap();}
template<class...A>int STATES_CheckState(A...){__builtin_trap();}
template<class...A>void sub_6FD11C90(A...){__builtin_trap();}
template<class...A>void STATES_ToggleState(A...){__builtin_trap();}
template<class...A>void STATES_ToggleGfxStateFlag(A...){__builtin_trap();}
'''+aliases+'\n'+'\n'.join(bodies.values())+r'''
int fn(D2UnitEventCallbackFunction p){
'''+''.join('if(p==callback<'+str(i)+'>)return '+str(i)+';' for i in range(1,32))+r'''return 0;}
using Op=std::vector<int64_t>;bool comma=false;
void dump(D2UnitStrc*u){printf("[%d,[",allocated);bool first=true;for(auto*n=u->pSrvUnitEventsList;n;n=n->pNext){if(!live.contains(n))__builtin_trap();printf("%s[%d,%u,%d,%d,%d,%u,%d,%u]",first?"":",",n->id,n->nUnitEvent,n->nGUID1,n->nGUID2,fn(n->pCallback),n->nQueueNo,n->unk0x08,n->nFlags);first=false;}printf("]]");}
void run(int owner,int game,int first,int second,int f1,int f2,int exists,int action,const std::vector<Op>&ops){
if(!live.empty())__builtin_trap();allocated=0;mode=action;acted=false;recordExists=exists;meta.wItemEvent[0]=first;meta.wItemEvent[1]=second;meta.wItemEventFunc[0]=f1;meta.wItemEventFunc[1]=f2;D2GameStrc g;D2UnitStrc u;auto*p=owner?&u:nullptr;trace.clear();
if(comma)printf(",");comma=true;printf("[[%d,%d,%d,%d,%d,%d,%d,%d,[",owner,game,first,second,f1,f2,exists,action);for(size_t i=0;i<ops.size();i++){printf("%s[",i?",":"");for(size_t j=0;j<ops[i].size();j++)printf("%s%lld",j?",":"",(long long)ops[i][j]);printf("]");}printf("]],[");
for(size_t i=0;i<ops.size();i++){auto&o=ops[i];switch(o[0]){
case 0:SUNITEVENT_Register(&g,p,(uint8_t)o[1],o[2],o[3],callback<20>,(uint32_t)o[5],o[6]);break;
case 1:sub_6FD156A0(&g,p,(uint8_t)o[1],o[2],o[3],o[4],o[5],o[6]);break;
case 2:SUNITEVENT_Unregister(&g,p,o[1],o[2]);break;
case 3:{auto*n=SUNITEVENT_GetEvent(&g,p,o[1],o[2],o[3]);trace.push_back({20,n?n->id:0});break;}
case 4:SUNITEVENT_Trigger(&g,(uint8_t)o[1],p,nullptr,nullptr);break;
case 5:SUNITEVENT_FreeEventList(&g,p);break;
case 6:D2GAME_ITEMMODE_ServerStatlistCallback_6FC41910(game?&g:nullptr,p,nullptr,o[1],0,o[2]);break;
}printf("%s",i?",":"");dump(&u);}printf("],[");for(size_t i=0;i<trace.size();i++){printf("%s[",i?",":"");for(size_t j=0;j<trace[i].size();j++)printf("%s%lld",j?",":"",(long long)trace[i][j]);printf("]");}printf("]]");SUNITEVENT_FreeEventList(&g,&u);if(!live.empty())__builtin_trap();}
int main(){printf("[");
for(int owner:{0,1})for(int count=0;count<6;count++)for(uint32_t queue:{0u,2u,0xffffffffu})for(int phase:{0,5,6,255})for(int mode:{0,1,2,3,4}){
std::vector<Op> ops;for(int i=0;i<count;i++)ops.push_back({0,5+i%2,1000+i,2000+i,20,queue,3000+i});
ops.push_back({3,(int32_t)queue,3000,1000});ops.push_back({4,phase});ops.push_back({4,phase});ops.push_back({3,(int32_t)queue,3000,1000});ops.push_back({2,(int32_t)queue,3001});ops.push_back({5});ops.push_back({1,0,21,22,32,2,8});ops.push_back({1,0,21,22,31,2,8});ops.push_back({4,0});ops.push_back({5});run(owner,1,5,6,20,21,1,mode,ops);
}
for(int owner:{0,1})for(int game:{0,1})for(int exists:{0,1})for(int first:{-1,0,5})for(int second:{-1,6})for(int f1:{0,20,31,32})for(int f2:{0,21,30,32}){
int a=(198<<16)|4229,b=(198<<16)|5253;
std::vector<Op> ops={{6,a,100},{6,b,100},{3,2,a,a},{4,5},{6,a,25},{4,6},{6,a,0},{4,5},{6,a,-1},{6,a,0},{6,b,0},{5}};
run(owner,game,first,second,f1,f2,exists,0,ops);
}printf("]\n");}
'''
 for n,b in bodies.items():assert function(cpp,n)==b,n
 source=STAGE/'source.cpp';source.write_text(cpp);binary=STAGE/'probe'
 with (STAGE/'compiler.log').open('w') as f:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(source),'-o',str(binary)],stdout=f,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
 cases=json.loads(run.stdout);assert len(cases)==1488,len(cases);out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,cases=len(cases),completeRoutines=8,callbackTableSlots=50,availableCallbackSlots=list(range(1,32)),inputs=inputs,completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},artifacts={str(p.relative_to(ROOT)):sha(p) for p in (source,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',out,Path(__file__).resolve())},
 established=['Six complete list functions,complete50slot callback selector and complete stat-change routine execute with real new/delete under ASan/UBSan. Per-operation allocation ordinal and complete live node order/identity/payload/queue/key/flags plus callback/lookup traces are emitted.','Direct controls cover absent owners,LIFO registration,queue0one-shots,unsigned queue comparisons,lookup/removal,ignored callback results,self-unregister with deferred deletion,registration during callbacks and bounded nested other-phase triggers.','Event-stat controls cover missing game/owner/metadata,nonpositive first event,independent optional second callback,null callback slots,full packed-layer identity,duplicate suppression,positive value changes without reordering,zero removal and negative nonzero registration.'],
 services=['All non-event switch bodies in the complete stat-change callback are mandatory external services that trap if entered. This certificate covers its event-stat/default branch domain,not HP/mana/skill/aura/charge/state side effects. Function-table callback bodies are explicit observed services,not implemented skill effects.','Node field widths follow the header. Cases avoid freeing the cached successor or whole list during callbacks and same-event nested one-shot deletion;those old C++ allocator/reentrancy contexts are undefined and not certified. Production refuses those unsupported mutations rather than inventing current behavior.'],remaining=['Current executable and general world event producers/removal contracts remain unverified. NPC actual callbacks,live graph ownership,atomic persistence and saved original identities need separate native checks.'])
 p=ROOT/'tools/reference/unit-events-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
 if check:assert p.read_text()==encoded,'Unit event source evidence stale'
 else:p.write_text(encoded)
 print('Unit events:1488complete source sequence controls/8routines;LIFO,packed keys,registration/removal/callback lifecycle;real allocations ASan+UBSan clean;non-event stat services excluded.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
