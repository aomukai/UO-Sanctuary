#!/usr/bin/env python3
"""Compare complete S2 mode/rate, dual merge, frame bonus and event scheduler bodies."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
 def source(path):
  b=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);hashes[path]=hashlib.sha256(b).hexdigest();return b.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  body=text[m.start():i];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
 common=source('source/D2Common/src/Units/Units.cpp');unit=source('source/D2Game/src/UNIT/SUnit.cpp');players=source('source/D2Common/include/Units/Player.h');assert players.index('PLRMODE_SPECIAL2')>players.index('PLRMODE_SPECIAL1');start=common.index('struct D2AnimUnkStrc');end=common.index('enum D2C_UnitAnimModeStats',start);tables=common[start:end];blocks['animModeTables']=hashlib.sha256(tables.encode()).hexdigest()
 cpp=r'''#include <algorithm>
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#include <initializer_list>
#define __fastcall
#define __stdcall
#define __forceinline
#define D2_ASSERT(x) assert(x)
using BOOL=int;constexpr int TRUE=1,FALSE=0,UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_TYPES_COUNT=6,NUMBER_OF_PLAYERCLASSES=7,NUM_WEAPON_CLASSES=14,WEAPONCLASS_HTH=0,MONSTER_FIRST_EXPANSION=410,NUMBER_OF_PLRMODES=20,NUMBER_OF_MONMODES=16;
constexpr int PLRMODE_ATTACK1=7,PLRMODE_ATTACK2=8,PLRMODE_SPECIAL3=15,PLRMODE_SPECIAL4=16,PLRMODE_SEQUENCE=18,EVENTTYPE_MODECHANGE=8,EVENTTYPE_ENDANIM=9;
constexpr int STAT_ITEM_FASTERATTACKRATE=0,STAT_ATTACKRATE=1,BODYLOC_RARM=0,BODYLOC_LARM=1,ITEMTYPE_WEAPON=1,STATEMASK_MELEEONLY=0,SKILLSFLAG_USEATTACKRATE=1;
struct D2AnimSeqTxt{};struct Anim{int dwAnimSpeed=128,pFrameFlags[144]={};}anim;
struct D2SkillsTxt{int nSkillId=251;unsigned dwFlags[2]={1,0};}skill;
struct D2SkillStrc{D2SkillsTxt* pSkillsTxt=&skill;}used;
struct D2UnitStrc{void* pInventory=nullptr;Anim* pAnimData=&anim;int dwUnitType=0,dwClassId=6,dwAnimMode=14,dwSeqSpeed=128,wAnimSpeed=128,dwSeqFrameCount=0,dwFrameCountPrecise=2048,nSeqCurrentFramePrecise=0,ias=0,attackRate=0;void* pAnimSeq=nullptr;}owner,right,left;
struct D2GameStrc{int dwGameFrame=100;}game;
bool pair=false,hasSkill=true;int gearIas=0,skillIas=0,slow=0,mergeCalls=0,weaponClass=0;D2UnitStrc* active=&right;
std::vector<std::array<int,4>> events;
bool UNITS_CanDualWield(D2UnitStrc*){return pair;}
D2UnitStrc* D2Common_10434(D2UnitStrc*,int){return &right;}
D2UnitStrc* INVENTORY_GetLeftHandWeapon(void*){return &right;}D2UnitStrc* INVENTORY_GetSecondaryWeapon(void*){return pair?&left:nullptr;}
D2UnitStrc* INVENTORY_GetItemFromBodyLoc(void*,int slot){return slot==BODYLOC_RARM?&right:pair?&left:nullptr;}
D2UnitStrc* STATLIST_GetOwner(D2UnitStrc* p,int){return p?&owner:nullptr;}
void STATLIST_MergeStatLists(D2UnitStrc*,D2UnitStrc* p,int include){mergeCalls++;if(include)active=p;}
bool ITEMS_CanBeEquipped(D2UnitStrc* p){return p!=nullptr;}bool ITEMS_CheckItemTypeId(D2UnitStrc* p,int){return p!=nullptr;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,int,int){return gearIas+active->ias;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int,int){return p==&owner?100+skillIas-slow+active->attackRate:p->attackRate;}
bool STATES_CheckStateMaskOnUnit(D2UnitStrc*,int){return false;}int D2Common_11043(D2UnitStrc*){return 0;}
int D2Clamp(int x,int lo,int hi){return std::clamp(x,lo,hi);}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return hasSkill?&used:nullptr;}D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){return &skill;}
void D2COMMON_11013_ConvertMode(D2UnitStrc*,int*,int*,int*,const char*,int){}
int ITEMS_GetWeaponClassId(D2UnitStrc*){return weaponClass;}
void DATATBLS_GetSequenceEvent(D2AnimSeqTxt*,int,int*){assert(false);}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int kind,int ordinal){events.push_back({type,frame,kind,ordinal});}
'''+tables
 for text,name in [(common,'UNITS_CanAnimModeUseAttackRate'),(common,'UNITS_MergeDualWieldWeaponStatLists'),(common,'UNITS_UpdateAttackAnimRateAndVelocity'),(common,'UNITS_GetFrameBonus'),(unit,'sub_6FCBCE70')]:cpp+=routine(text,name)
 cpp+=r'''int main(){bool first=true;printf("{\"mode\":[");
for(int mode:{7,10,11,13,14,18})for(int flag:{0,1})for(int used:{0,1}){owner.dwAnimMode=mode;skill.dwFlags[0]=flag;hasSkill=used;int result=UNITS_CanAnimModeUseAttackRate(0,mode,&owner,6);printf("%s[%d,%d,%d,%d]",first?"":",",mode,flag,used,result);first=false;}
printf("],\"clock\":[");first=true;
for(int primarySpeed:{-30,0,20})for(int secondarySpeed:{-30,0,20})for(int primaryIas:{0,20,60,200})for(int secondaryIas:{0,40})for(int gear:{0,20})for(int boost:{0,50})for(int chilled:{0,50})for(int dual:{0,1}){
 owner={};right={};left={};active=&right;right.ias=primaryIas;left.ias=secondaryIas;right.attackRate=-primarySpeed;left.attackRate=-secondarySpeed;gearIas=gear;skillIas=boost;slow=chilled;pair=dual;anim={};anim.dwAnimSpeed=128;anim.pFrameFlags[4]=2;owner.pAnimData=&anim;mergeCalls=0;events.clear();UNITS_UpdateAttackAnimRateAndVelocity(&owner);sub_6FCBCE70(&game,&owner);
 int action=-1,end=-1;for(auto e:events){if(e[0]==8){assert(e[2]==2);action=e[1]-100;}if(e[0]==9)end=e[1]-100;}
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",primarySpeed,secondarySpeed,primaryIas,secondaryIas,gear,boost,chilled,dual,owner.wAnimSpeed,action,end,UNITS_GetFrameBonus(&owner),mergeCalls);first=false;
}printf("]}\n");}
'''
 path=STAGE/'fire-blast-timing-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert {k:len(v) for k,v in data.items()}==dict(mode=24,clock=1152)
 for mode,flag,used,result in data['mode']:assert result==int(mode in (7,11) or mode in (13,14,18) and flag and used)
 for p,s,ias,off,gear,boost,slow,pair,advance,event,end,bonus,merges in data['clock']:
  # C++ truncates negative signed dual-weapon sums towards zero.
  wsm=int((p+s)/2) if pair else p;rate=max(15,min(175,100+120*(ias+gear)//(120+ias+gear)+boost-slow-wsm));speed=128*rate//100
  assert (advance,event,end,bonus,merges)==(speed,(4*256+speed-1)//speed,(8*256+speed-1)//speed,0,2*pair)
 out=STAGE/'fire-blast-timing-source-probe.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',talentId='assassin.fireblast',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledUnchangedRoutines=blocks,fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),caseCounts={k:len(v) for k,v in data.items()}),established=['Complete mode selector makes Assassin S2/mode14 use attack rate only when the used skill has UseAttackRate. Throw/A1 mode controls select attack rate independently; SC does not.','Complete dual stat merge activates the primary weapon and deactivates the secondary damage-stat list. Declared active-list IAS and WSM services then feed the complete source rate function. Primary IAS plus gear IAS use the120 diminishing expression; offhand IAS is excluded by that declared service. Paired source weapon attackrate correction produces the signed-truncated average WSM under the explicit primary-to-RARM adapter.', 'With declared S2 metadata8frames/speed128/event4, the complete frame-bonus getter returns0 and the complete scheduler produces ceil(1024/advance) effect and ceil(2048/advance) end. The existing current ordinary-repeat minus-one adapter is separate from that old end event; native input/held pacing/current acceptance remain required.'],services=['Active/inactive damage-stat collection, inventory primary/RARM identity, usable source weapons and gear/skill/chill stats are declared services. The full property-stat engine/compositor/gear transactions are not newly compiled here; existing DamageRelated swing1/2/3 representation and real claw checks are reuse points.', 'Metadata8/128/event4 comes from the separately hashed supplementary cached AnimData records; its provenance is incomplete. Empty original event list and unchanged scheduler isolate event clocks; owner class6/mode14 is fixed for those cases.','No source seed/modern executable/client, melee-only mode, transforms, NPC/foreign suppliers or complete event mode acceptance is established by these fixtures.'])
 p=ROOT/'tools/reference/fire-blast-timing-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Fire Blast:24 mode selectors and1152 complete S2 rate/dual-merge/scheduler cases pass; native delivery remains disabled.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
