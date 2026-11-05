#!/usr/bin/env python3
"""Run the complete older common skill executor through explicit nested services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'skill-execution-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={}
 def read(relative):
  p=BASE/'d2moo-pinned-source'/relative;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==relative);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 text=read('source/D2Game/src/SKILLS/Skills.cpp');body=function(text,'D2GAME_SKILLS_Handler_6FD12BA0')
 a=text.index('static const SkillDoFunc gpSkillSrvDoFnTable_6FD40A20[]');end=text.index('};',a);initializer=re.sub(r'//[^\n]*|/\*.*?\*/','',text[a:end],flags=re.S).split('{',1)[1];assert len([v for v in initializer.split(',') if v.strip()])==191
 header=read('source/D2Common/include/DataTbls/SkillsTbls.h');players=read('source/D2Common/include/Units/Player.h')
 def ordinal(raw,first,last):return raw[raw.index(first):raw.index(last)].count(',')

 for field,kind in {'wSrvDoFunc':'uint16_t','wItemEffect':'uint16_t','wSrvMissile':'int16_t','wSrvStartFunc':'int16_t','nAnim':'uint8_t','dwDelay':'uint32_t'}.items():assert re.search(r'\b'+kind+r'\s+'+field+r'\s*;',header),field
 assert ordinal(header,'SKILLSFLAGINDEX_DECQUANT = 0','SKILLSFLAGINDEX_INTOWN')==8
 assert ordinal(header,'SKILLSFLAGINDEX2_TGTPLACECHECK = 0','SKILLSFLAGINDEX2_USEMANAONDO')==5
 assert ordinal(players,'PLRMODE_DEATH,','PLRMODE_SEQUENCE,')==18
 assert ordinal(players,'PLRMODE_DEATH,','PLRMODE_CAST,')==10
 assert ordinal(players,'PLRMODE_DEATH,','PLRMODE_TOWNNEUTRAL,')==5
 cpp=r'''
#include <array>
#include <cstdint>
#include <cstdio>
#include <utility>
#include <vector>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNITFLAG_SKSRVDOFUNC=64,PLRMODE_TOWNNEUTRAL=5,PLRMODE_SEQUENCE=18;
constexpr int SKILLSFLAGINDEX_INTOWN=8,SKILLSFLAGINDEX_LOB=1,SKILLSFLAGINDEX_DECQUANT=0,SKILLSFLAGINDEX2_USEMANAONDO=5;
uint32_t gdwBitMasks[32];
struct D2GameStrc{};struct D2ActiveRoomStrc{}room;
struct D2SkillStrc{int id=82,rank=3,tag=1;uint32_t flags=0;};
struct D2DynamicPathStrc{int x=17,y=19;};
struct D2UnitStrc{int dwUnitType=1,x=5,y=7;uint32_t dwFlags=0x8000,dwSeqFrame=0;D2DynamicPathStrc*pDynamicPath=nullptr;};
struct D2SkillsTxt{uint16_t wSrvDoFunc=30,wItemEffect=1;int16_t wSrvMissile=-1,wSrvStartFunc=0;uint8_t nAnim=10;uint32_t dwDelay=17,dwFlags[2]={};}record;
struct Tables{int nMissilesTxtRecordCount=10;int pMissilesTxt[10]={};}tables;auto*sgptDataTables=&tables;
std::array<int,24> c;D2SkillStrc used,highest;D2UnitStrc unit,target;bool cleared=false;std::vector<std::vector<int>>trace;
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int skill){trace.push_back({0,skill});return c[1]==3?nullptr:&record;}
D2SkillStrc*UNITS_GetUsedSkill(D2UnitStrc*){trace.push_back({1,c[2]?1:0});return c[2]?&used:nullptr;}
uint32_t SKILLS_GetFlags(D2SkillStrc*p){return p->flags;}
D2ActiveRoomStrc*UNITS_GetRoom(D2UnitStrc*){trace.push_back({2});return c[4]?&room:nullptr;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){trace.push_back({15});return c[4]==1;}
void UNITS_SetUsedSkill(D2UnitStrc*,D2SkillStrc*){trace.push_back({3});cleared=true;}
void sub_6FC817D0(D2GameStrc*,D2UnitStrc*,int,int mode,int,int,int){if(mode!=5)__builtin_trap();trace.push_back({4});}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc*p,const char*,int){return p->id;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*p,int bonuses){trace.push_back({5,p->tag,bonuses});return p->rank;}
D2SkillStrc*SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int skill){trace.push_back({6,skill});return c[3]?&highest:nullptr;}
int sub_6FD10CE0(D2UnitStrc*){trace.push_back({7});return c[7];}
using SkillDoFunc=int(*)(D2GameStrc*,D2UnitStrc*,int,int);
template<size_t I>int action(D2GameStrc*,D2UnitStrc*,int skill,int level){trace.push_back({8,(int)I,skill,level});return c[11];}
template<size_t...I>auto actions(std::index_sequence<I...>){return std::array<SkillDoFunc,sizeof...(I)>{&action<I>...};}
auto allActions=actions(std::make_index_sequence<191>{});std::array<SkillDoFunc,191>gpSkillSrvDoFnTable_6FD40A20;
int start(){return 1;}std::array<int(*)(),128>gpSkillSrvStartFnTable_6FD408B0;
D2UnitStrc*SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){trace.push_back({9});return c[13]>=1&&c[13]<=3?&target:nullptr;}
int CLIENTS_GetUnitX(D2UnitStrc*p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc*p){return p->y;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc*p){return p->x;}int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc*p){return p->y;}
void*sub_6FD11710(D2GameStrc*,int missile,D2UnitStrc*,int skill,int level,int x,int y,int tx,int ty,int flags){trace.push_back({10,1,missile,skill,level,x,y,tx,ty,flags});return nullptr;}
void*sub_6FD11420(D2GameStrc*,int missile,D2UnitStrc*,int skill,int level,int x,int y,int tx,int ty,int flags){trace.push_back({10,0,missile,skill,level,x,y,tx,ty,flags});return nullptr;}
void D2GAME_SKILLMANA_Consume_6FD10A50(D2GameStrc*,D2UnitStrc*,int skill,int level){trace.push_back({11,skill,level});}
void sub_6FD118C0(D2GameStrc*,D2UnitStrc*){trace.push_back({12});}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int skill,int level){trace.push_back({13,formula,skill,level});return c[17];}
void D2GAME_SKILLS_SetDelay_6FD11C00(D2GameStrc*,D2UnitStrc*,int frames){trace.push_back({14,frames});}
'''+body+r'''
bool comma=false;
std::array<int,24> base(){return {1,0,0,1,2,0,0,1,30,1,1,1,-1,0,0,1,0,0,10,0,0,1,1,5};}
void run(){D2GameStrc game;D2DynamicPathStrc path;path.x=c[13]==4?0:17;
unit=D2UnitStrc{};unit.dwUnitType=c[0];unit.dwSeqFrame=c[19];unit.pDynamicPath=&path;
target=D2UnitStrc{};target.x=c[13]==2?0:23;target.y=c[13]==3?0:29;
used=D2SkillStrc{};used.id=c[2]==3?81:82;used.rank=c[2]==2?0:3;used.flags=c[2]==4?1:0;
highest=D2SkillStrc{};highest.rank=c[3]==2?0:4;highest.tag=2;
record=D2SkillsTxt{};record.wSrvDoFunc=c[8];record.wItemEffect=c[9];record.wSrvMissile=c[12];record.nAnim=c[18];
record.dwFlags[0]=(c[5]?256:0)|(c[14]?2:0)|(c[16]?1:0);record.dwFlags[1]=c[6]?32:0;
gpSkillSrvDoFnTable_6FD40A20=allActions;if(!c[10])gpSkillSrvDoFnTable_6FD40A20.fill(nullptr);
gpSkillSrvStartFnTable_6FD408B0.fill(c[15]?&start:nullptr);cleared=false;trace.clear();
int result=D2GAME_SKILLS_Handler_6FD12BA0(c[1]==1?nullptr:&game,c[1]==2?nullptr:&unit,82,c[23],c[20],c[21],c[22]);
if(comma)printf(",");comma=true;printf("[[");for(size_t i=0;i<c.size();i++)printf("%s%d",i?",":"",c[i]);printf("],[%d,%d,%u],[",result,cleared,unit.dwFlags);
for(size_t i=0;i<trace.size();i++){printf("%s[",i?",":"");for(size_t j=0;j<trace[i].size();j++)printf("%s%d",j?",":"",trace[i][j]);printf("]");}printf("]]");}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");
// Admission and short-circuit rank/resource paths.
for(int kind:{0,1,3})for(int used:{0,1,2,3,4})for(int highest:{0,1,2})for(int room:{0,1,2})for(int town:{0,1})for(int item:{0,1})for(int mana:{0,1})for(int allow:{0,1})for(int fn:{-1,30,191}){c=base();c[0]=kind;c[2]=used;c[3]=highest;c[4]=room;c[5]=town;c[6]=mana;c[7]=allow;c[8]=fn;c[21]=item;run();}
// Independent do and missile execution, all caller flags and aim zero edges.
for(int kind:{0,1,3})for(int finish:{0,1})for(int item:{0,1})for(int outgoing:{0,1})for(int fn:{30,190})for(int effect:{1,36,190})for(int present:{0,1})for(int result:{0,1,7})for(int missile:{-1,0,10})for(int aim:{0,1,2,3,4})for(int lob:{0,1}){c=base();c[0]=kind;c[8]=fn;c[9]=effect;c[10]=present;c[11]=result;c[12]=missile;c[13]=aim;c[14]=lob;c[20]=finish;c[21]=item;c[22]=outgoing;run();}
// Post-success payment, quantity and sequence-delay suppression.
for(int kind:{0,1,3})for(int start:{0,1})for(int mana:{0,1})for(int quantity:{0,1})for(int delay:{-1,0,5})for(int anim:{10,18})for(int frame:{0,255,256})for(int result:{0,1,7})for(int missile:{-1,0})for(int item:{0,1}){c=base();c[0]=kind;c[6]=mana;c[11]=result;c[12]=missile;c[15]=start;c[16]=quantity;c[17]=delay;c[18]=anim;c[19]=frame;c[20]=1;c[21]=item;run();}
for(int kind:{0,1,3})for(int guard:{0,1,2,3})for(int finish:{0,1})for(int item:{0,1})for(int outgoing:{0,1}){c=base();c[0]=kind;c[1]=guard;c[20]=finish;c[21]=item;c[22]=outgoing;run();}
printf("]\n");}
'''
 assert function(cpp,'D2GAME_SKILLS_Handler_6FD12BA0')==body
 source=STAGE/'source.cpp';source.write_text(cpp);binary=STAGE/'probe'
 with (STAGE/'compiler.log').open('w') as f:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(source),'-o',str(binary)],stdout=f,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
 cases=json.loads(run.stdout);assert len(cases)==37680,len(cases);output=STAGE/'cases.json';output.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,cases=len(cases),legacyDoSlots=191,skillFieldWidthsVerified=True,inputs=inputs,completeRoutineHashes={'D2GAME_SKILLS_Handler_6FD12BA0':hashlib.sha256(body.encode()).hexdigest()},artifacts={str(p.relative_to(ROOT)):sha(p) for p in (source,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',output,Path(__file__).resolve())},
 established=['Complete common executor runs ordinary/item/outgoing callers for player,monster and nonliving units. Return,used-skill clearing,server-do flags and ordered rank/room/mana/do/missile/quantity/delay service traces are emitted.','Do-function execution and direct missile creation are independent. A valid missile route sets server-do and result1even when the mandatory nested factory returns null. Outgoing item aiming preserves zero-coordinate edges and extends nonzero aim beyond the target.','Item-effect override applies only for item+outgoing after original do bounds and use-mana-on-do checks. Item callers bypass learned-skill lookup,but retain the used-skill/town and conditional mana admission paths. Finish controls payment,quantity and delay separately.'],
 services=['Metadata,used-skill/flags/ranks/room,tables/do-functions,resource checks,missile factories,formula evaluation and state notifications are explicit services. Synthetic callable tables use191legacy slots;this does not certify every current NPC spell or current extension. Immutable getters and source diagnostic file/line arguments are not world service operations.','All source override indexes and start indexes are valid. Corrupt out-of-range overrides have undefined C++ behavior and are excluded;production must explicitly refuse them. Source-coordinate controls avoid signed overflow.'],remaining=['Actual NPC adapters must provide their own explicit skill/room/resource/stat/missile contexts rather than invoke PlayerMobile handlers. Current executable,global ordering/seed and full generic NPC admission remain unfinished.'])
 p=ROOT/'tools/reference/skill-execution-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==encoded,'Skill execution source receipt stale'
 else:p.write_text(encoded)
 print('Skill execution:37680complete source common-executor controls;ordinary/item/outgoing do,missile,rank,town,mana,quantity,delay traces;ASan+UBSan clean.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
