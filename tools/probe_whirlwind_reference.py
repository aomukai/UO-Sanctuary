#!/usr/bin/env python3
"""Compile old Whirlwind phase/cleanup/target contracts, separating released modern cadence."""
from pathlib import Path
import hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
hashes={};blocks={}
def source(path):
 raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();blob=next(v['sha'] for v in tree['tree'] if v['path']==path)
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blob;hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
def routine(text,name):
 m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
 end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 blocks[name]=text[m.start():end];return blocks[name]+'\n'
bar=source('source/D2Game/src/SKILLS/SkillBar.cpp');skills=source('source/D2Game/src/SKILLS/Skills.cpp');collision=source('source/D2Common/include/D2Collision.h')
assert 'COLLIDE_MASK_PLAYER_WW = COLLIDE_WALL | COLLIDE_OBJECT | COLLIDE_DOOR' in collision
cpp=r'''#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <iterator>
#include <initializer_list>
#include <vector>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,COMPOSIT_LEFTHAND=6,COMPOSIT_RIGHTHAND=5,ITEMTYPE_MELEE_WEAPON=1,
 COLLIDE_PLAYER=128,COLLIDE_MONSTER=256,COLLIDE_MASK_PLAYER_PATH=0x1C09,COLLIDE_MASK_MONSTER_PATH=0x1C89,DAMAGERESULTFLAG_SUCCESSFULHIT=1;
struct D2DynamicPathStrc{int footprint=0,move=0;} path;
struct D2SkillStrc{int flags=1,params[4]={100,100,10,0};} skill;
struct D2StatListStrc{} statlist;
struct D2SkillsTxt{int nAuraState=5,wResultFlags=16,dwHitFlags=32,dwHitClass=4,dwCalc[1]={11};uint8_t nSrcDam=0;} row;
struct D2UnitStrc{int dwUnitType=0,dwFrameCountPrecise=0,pSeed=0,dwUnitId=1;D2DynamicPathStrc* pDynamicPath=&path;} owner,primary,secondary,targets[3];
struct D2GameStrc{int dwGameFrame=10000;bool bExpansion=true;} game;
struct D2DamageStrc{int wResultFlags=0,dwHitFlags=0,dwHitClass=0,dwEnDmgPct=0;} lastDamage;
struct Tables{int nStatesTxtRecordCount=10;} tables;auto* sgptDataTables=&tables;
struct D2_6FD10880_Strc{D2UnitStrc* unk0x00=nullptr;uint32_t unk0x04=0;D2UnitStrc* unk0x08=nullptr;uint32_t unk0x0C=0,unk0x10=0;int unk0x14=0;};
struct D2AuraCallbackStrc{void* pArgs=nullptr;};
bool record=true,used=true,alive=true,pair=true,hit=true;int speed=11,count=3,removes=0,clears=0,resets=0,rewinds=0,anims=0,allocs=0,drains=0,elements=0,searches=0,lastSource=0,trace=0;
std::vector<int> order;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return record?&row:nullptr;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return used?&skill:nullptr;}
int SKILLS_GetParam1(D2SkillStrc* p){return p->params[0];}int SKILLS_GetParam2(D2SkillStrc* p){return p->params[1];}int SKILLS_GetParam3(D2SkillStrc* p){return p->params[2];}int SKILLS_GetParam4(D2SkillStrc* p){return p->params[3];}
void SKILLS_SetParam3(D2SkillStrc* p,int v){p->params[2]=v;}void SKILLS_SetParam4(D2SkillStrc* p,int v){p->params[3]=v;}
int SKILLS_GetFlags(D2SkillStrc* p){return p->flags;}void SKILLS_SetFlags(D2SkillStrc* p,int v){p->flags=v;}
void sub_6FCBDE90(D2UnitStrc*,int){clears++;}void sub_6FCC63D0(D2UnitStrc*,int){removes++;}
void* UNITS_GetRoom(D2UnitStrc*){return &owner;}
void COLLISION_ResetMaskWithPattern(void*,int,int,int,int){resets++;}
int PATH_GetUnitCollisionPattern(D2UnitStrc*){return 1;}
void PATH_SetFootprintCollisionMask(D2DynamicPathStrc* p,int v){p->footprint=v;}void PATH_SetMoveTestCollisionMask(D2DynamicPathStrc* p,int v){p->move=v;}
void STATES_ToggleState(D2UnitStrc*,int,int){}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return &statlist;}
void D2Common_10474(D2UnitStrc*,D2StatListStrc*){}void STATLIST_FreeStatList(D2StatListStrc*){}
bool SUNIT_IsDead(D2UnitStrc*){return !alive;}
void UNITS_SetAnimationFrame(D2UnitStrc*,int){anims++;}
void D2GAME_SKILLS_RewindSkillEx_6FCBD260(D2GameStrc*,D2UnitStrc*,int){rewinds++;}
int ITEMS_RollRandomNumber(int*){return 0;}
D2UnitStrc* sub_6FC7C7B0(D2UnitStrc*){return speed<0?nullptr:&primary;}
int ITEMS_GetWeaponAttackSpeed(D2UnitStrc*,D2UnitStrc*){return speed;}
D2UnitStrc* INVENTORY_GetCompositItem(void*,int hand){return pair?(hand==5?&primary:&secondary):nullptr;}
bool ITEMS_CheckItemTypeId(D2UnitStrc* p,int){return p!=nullptr;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int){return hit?1:0;}
int SKILLS_GetToHitFactor(D2UnitStrc*,int,int){return 50;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return 30;}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc*,int,int){elements++;}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc* target,D2DamageStrc* damage,int src){allocs++;lastDamage=*damage;lastSource=src;trace=trace*100+target->dwUnitId;}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){drains++;}
void sub_6FD0FE80(D2GameStrc*,D2UnitStrc*,int,int,int radius,uint32_t filter,int(*callback)(D2AuraCallbackStrc*,D2UnitStrc*),void* args,int,const char*,int){if(radius!=5||filter!=0xA783)std::abort();D2AuraCallbackStrc data{args};for(int i:order){if(i<count)callback(&data,&targets[i]);}}
'''
# The inventory address is a declared service; include a real field for the unchanged routine.
cpp=cpp.replace('int dwUnitType=0,','void* pInventory=(void*)1;int dwUnitType=0,')
cpp+='int32_t __fastcall sub_6FD10880(D2AuraCallbackStrc*,D2UnitStrc*);\n'
for text,name in [(skills,'sub_6FD10880'),(skills,'sub_6FD107F0'),(bar,'SKILLS_RemoveWhirlwindStats'),(bar,'SKILLS_SrvDo076_Whirlwind')]:cpp+=routine(text,name)
cpp+=r'''int main(){for(int i=0;i<3;i++)targets[i].dwUnitId=11+i;bool first=true;std::printf("{\"phaseCases\":[");
for(int flags:{0,1,3})for(int player:{0,1})for(int expansion:{0,1})for(int frame:{0,9999,10001})for(int ws:{-1,11,12,15,18,20,23,26})for(int dual:{0,1})for(int n:{0,1,3}){
 skill={};skill.flags=flags;skill.params[3]=frame;owner={};owner.dwUnitType=player?0:1;game.bExpansion=expansion;speed=ws;pair=dual;count=n;order={2,0,1};removes=clears=resets=rewinds=anims=allocs=drains=elements=searches=lastSource=trace=0;lastDamage={};path={};
 int result=SKILLS_SrvDo076_Whirlwind(&game,&owner,151,1);if(allocs && (lastDamage.wResultFlags!=17 || lastDamage.dwHitFlags!=32 || lastDamage.dwHitClass!=4 || lastDamage.dwEnDmgPct!=30))std::abort();
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",flags,player,expansion,frame,ws,dual,n,result,skill.flags,skill.params[2],skill.params[3],removes,allocs,drains,lastSource,trace,rewinds);}
 std::printf("],\"selectorCases\":[");first=true;
for(int n:{0,1,2,3})for(int prev:{-1,0,10,11,12,13,14})for(int reversed:{0,1}){
 count=n;order=reversed?std::vector<int>{2,1,0}:std::vector<int>{0,1,2};int counter=0;auto* result=sub_6FD107F0(&game,&owner,0,0,5,3,prev,&counter);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d]",n,prev,reversed,result?result->dwUnitId:-1,counter);
}std::printf("]}\n");}
'''
p=STAGE/'whirlwind-source-probe.cpp';p.write_text(cpp);binary=STAGE/'whirlwind-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]))
def duration(speed):
 if speed<0:return 10
 for bound,value in [(12,4),(15,6),(18,8),(20,10),(23,12),(26,14)]:
  if speed<bound:return value
 return 16
for flags,player,expansion,frame,speed,dual,n,result,newflags,prev,newframe,removed,allocs,drains,src,trace,rewinds in data['phaseCases']:
 if flags==0:expected=(0,0,10,frame,0,0,0,0,0,0)
 elif flags==3:expected=(1,0,10,frame,1,0,0,0,0,0)
 else:
  attacks=1;nextframe=frame
  if player and expansion:
   if frame==0:nextframe=10004
   elif frame<=10000:attacks=2 if dual else 1;nextframe=max(frame+duration(speed),10000+duration(speed)) if frame+duration(speed)<10000 else frame+duration(speed)
   else:attacks=0
  iterations=attacks if n else min(1,attacks);allocated=attacks if n else 0
  ids=[11+(i%n) for i in range(allocated)] if n else []
  value=0
  for i in ids:value=value*100+i
  expected=(1,1^(8192 if iterations%2 else 0),ids[-1] if ids else -1 if iterations else 10,nextframe,0,allocated,allocated,128 if allocated else 0,value,int(player))
 assert (result,newflags,prev,newframe,removed,allocs,drains,src,trace,rewinds)==expected,(flags,player,expansion,frame,speed,dual,n,expected,(result,newflags,prev,newframe,removed,allocs,drains,src,trace,rewinds))
for n,prev,reversed,result,counter in data['selectorCases']:
 ids=list(range(11,11+n));greater=[v for v in ids if v>(prev&0xffffffff)];expected=min(greater or ids) if ids else -1
 assert result==expected,(n,prev,reversed,result,expected)
data_path=STAGE/'whirlwind-source-probe.json';data_path.write_text(json.dumps(data,indent=2)+'\n')
# Reviewed lifecycle and raw walking speed are not represented as compiled start proof.
for text,name in [(bar,'SKILLS_SrvSt38_Whirlwind'),(skills,'sub_6FD15500')]:routine(text,name)
modern_path=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';modern=next({k:v for k,v in row.items() if v not in ('',None)} for row in json.loads(modern_path.read_text()).values() if row['skill']=='Whirlwind')
char_path=ROOT/'tools/reference/d2r-named-equipment/json/charstats.json'
char=next(v for v in json.loads(char_path.read_text()).values() if v['class']=='Barbarian')
assert char['WalkVelocity']==6
out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,modernTableHash=hashlib.sha256(modern_path.read_bytes()).hexdigest(),probe=dict(caseCounts={k:len(v) for k,v in data.items()},totalCases=sum(map(len,data.values())),dataPath=str(data_path.relative_to(ROOT)),compiledRoutines=['sub_6FD10880','sub_6FD107F0','SKILLS_RemoveWhirlwindStats','SKILLS_SrvDo076_Whirlwind'],blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()}),skill=modern,characterMotion={k:char[k] for k in ['WalkVelocity','RunVelocity']},characterTableHash=hashlib.sha256(char_path.read_bytes()).hexdigest(),
 releasedPrimary=[{'url':'https://news.blizzard.com/en-gb/article/23814050/diablo-ii-resurrected-patch-2-4-3-now-live','rules':['All equipment IAS; interval is basic-attack effect frame; dual hands average upward.','First attack uses both hands/targets; short-distance attacks cannot end without an attack; broken weapons must not contribute.','Immediate Whirlwind/Leap/Leap Attack chaining after finish; current30+5/rank ED and50+5/rank AR.']},{'url':'https://news.blizzard.com/en-gb/article/23788293/diablo-ii-resurrected-patch-2-4-ladder-now-live','rules':['Targeted destination snapshots the initial target position without chasing. Adjacent targets still cause a spin.']}],
 established=['Old do76 phase guards, cleanup, zero/future/due attack deadlines, per-pulse dual target count, source result flags/weapon damage/durability and hand-bit toggles are compiled. Its old weapon-speed duration table and initial single-hand pulse are explicitly superseded by released2.4.3.','The shared target helper uses source serial-cycle order: lowest eligible ID above previous, wrapping to the lowest ID. The provided area scanner is a declared service; actual source radius5/filterA783 and spatial/callback services are reviewed separately.','Reviewed start fixes a straight path, reserves its last point, sets uninterruptible/skill_move and stat state. Cleanup releases reservation/masks/state. Reviewed player speed helper returns current character WalkVelocity<<8, without run/missile75% substitution.'],
 limits=['Four unchanged legacy routines compile with declared room/target eligibility, damage/RNG/weapon/stat/animation services. The full start, native collision/path, current executable and modern changed scheduling are not compiled here.','Modern cadence must use the released rule through the shared basic-attack consumer, not the old compiled weapon-speed thresholds or initial one-hand shortcut. Exact current first frame/geometry/IAS/WSM/FRW/slow/owner/client and full proc handling remain acceptance work.'])
(ROOT/'tools/reference/whirlwind-engine-evidence.json').write_text(json.dumps(out,indent=2)+'\n');print('Whirlwind source cases:',out['probe']['totalCases'])
