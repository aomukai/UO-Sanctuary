#!/usr/bin/env python3
"""Compile unchanged hand selection and damage-stat merge toggle contracts."""
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
 blocks[name]=text[m.start():end];return blocks[name]
units=source('source/D2Common/src/Units/Units.cpp');stats=source('source/D2Common/src/D2StatList.cpp');bitmanip=source('source/Fog/include/D2BitManip.h')
cpp=r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __stdcall
using BOOL=int;constexpr int FALSE=0,TRUE=1,UNITFLAGEX_SERVERUNIT=1,COMPOSIT_LEFTHAND=0,COMPOSIT_RIGHTHAND=1,ITEMTYPE_WEAPON=42,STATLIST_DYNAMIC=1,ITEMSTATCOSTFLAGINDEX_DAMAGERELATED=0;
constexpr unsigned gdwBitMasks[]={1};
struct D2SkillsTxt{int nWeapSel=0;} row;
struct D2SkillStrc{D2SkillsTxt* row=&::row;int flags=0;} skill;
struct D2StatStrc{int nStat=0,nPackedValue=0,nValue=0;};
struct D2StatsArrayStrc{D2StatStrc* pStat=nullptr;int nStatCount=0;};
struct D2UnitStrc;
struct D2StatListExStrc{D2UnitStrc* pUnit=nullptr;unsigned dwFlags=0;bool extended=true;D2StatsArrayStrc FullStats,Stats;};
struct D2UnitStrc{void* pInventory=(void*)1;void* pSkills=(void*)1;unsigned dwSeqFrame=0,dwFlagEx=1;bool weapon=true,equippable=true;int id=0;D2StatListExStrc* pStatListEx=nullptr;} owner,primary,secondary,fallback;
bool dual=true,used=true,record=true;int mask=3,merges=0,changes=0,sum=0;
bool UNITS_CanDualWield(D2UnitStrc*){return dual;}
void INVENTORY_GetEquippedWeapon(void*,D2UnitStrc** item,int* loc,BOOL* left){*item=&fallback;}
D2SkillStrc* SKILLS_GetUsedSkillFromSkillList(void*){return used?&skill:nullptr;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecordFromSkill(D2SkillStrc*){return record?&row:nullptr;}
D2UnitStrc* INVENTORY_GetCompositItem(void*,int hand){return hand==COMPOSIT_RIGHTHAND?(mask&1?&primary:nullptr):(mask&2?&secondary:nullptr);}
bool ITEMS_CheckItemTypeId(D2UnitStrc* p,int){return p&&p->weapon;}
bool ITEMS_CanBeEquipped(D2UnitStrc* p){return p&&p->equippable;}
int SKILLS_GetFlags(D2SkillStrc* p){return p->flags;}
void STATLIST_MergeStatLists(D2UnitStrc*,D2UnitStrc*,bool){merges++;}
bool STATLIST_IsExtended(D2StatListExStrc* p){return p->extended;}
__BITMANIP__
D2StatListExStrc* STATLIST_StatListExCast(D2StatListExStrc* p){return p;}
struct D2ItemStatCostTxt{unsigned dwItemStatFlags=0;} costs[3];
D2ItemStatCostTxt* ITEMS_GetItemStatCostTxtRecord(int id){return id>=0&&id<3?&costs[id]:nullptr;}
void sub_6FDB6C10(D2StatListExStrc*,int,int value,D2UnitStrc*){changes++;sum+=value;}
'''
cpp=cpp.replace('__BITMANIP__','template<typename T>\n'+routine(bitmanip,'BITMANIP_SetBitsValueForMask'))
for text,name in [(units,'D2Common_10434'),(stats,'D2Common_11274_11275_Impl')]:cpp+=routine(text,name)+'\n'
cpp+=r'''int main(){primary.id=1;secondary.id=2;fallback.id=3;bool first=true;std::printf("{\"handCases\":[");
for(int gate=0;gate<16;gate++)for(int select:{0,1,2,3,4})for(int frame:{0,1,2,5,6,7})for(int server:{0,1})for(int damage:{0,1})for(int hands:{0,1,2,3}){
 owner.pInventory=gate&1?(void*)1:nullptr;dual=gate&2;used=gate&4;record=gate&8;row.nWeapSel=select;owner.dwSeqFrame=frame<<8;owner.dwFlagEx=server;mask=hands;skill.flags=0;
 auto* result=D2Common_10434(&owner,damage);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d]",gate,select,frame,server,damage,hands,result?result->id:0);
}
std::printf("],\"switchFlagCases\":[");first=true;owner.pInventory=(void*)1;dual=used=record=true;row.nWeapSel=2;mask=3;
for(int flags:{0,8192})for(int rightWeapon:{0,1})for(int leftWeapon:{0,1})for(int equip:{0,1}){skill.flags=flags;primary.weapon=rightWeapon;secondary.weapon=leftWeapon;primary.equippable=secondary.equippable=equip;auto* result=D2Common_10434(&owner,1);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d]",flags,rightWeapon,leftWeapon,equip,result?result->id:0);}
std::printf("],\"mergeCases\":[");first=true;D2StatStrc values[3]={{0,0,10},{1,1,20},{2,2,30}};costs[0].dwItemStatFlags=costs[2].dwItemStatFlags=1;costs[1].dwItemStatFlags=0;
for(int gates=0;gates<32;gates++)for(int old:{0,1})for(int add:{0,1}){
 D2StatListExStrc targetList,weaponList;targetList.extended=gates&16;owner.pStatListEx=gates&8?&targetList:nullptr;primary.pStatListEx=gates&4?&weaponList:nullptr;weaponList.pUnit=gates&2?&owner:&secondary;weaponList.dwFlags=old;weaponList.FullStats={values,3};merges=changes=sum=0;
 int result=D2Common_11274_11275_Impl(gates&1?&owner:nullptr,&primary,add);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d]",gates,old,add,result,weaponList.dwFlags,merges,changes,sum);
}std::printf("]}\n");}
'''
path=STAGE/'weapon-hands-source-probe.cpp';path.write_text(cpp);binary=STAGE/'weapon-hands-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]))
for gate,select,frame,server,damage,hands,result in data['handCases']:
 if not gate&1:expected=0
 elif gate!=15:expected=3
 elif select==4:expected=0
 else:
  secondary=select==1 or select==3 and frame>0 and (not damage or (frame%2!=0 if server else frame>5))
  chosen=2 if secondary else 1
  if select not in (1,3) and not hands&1:chosen=2
  expected=chosen if hands&(1 if chosen==1 else 2) else 3
 assert result==expected,(gate,select,frame,server,damage,hands,result,expected)
for flags,right,left,equip,result in data['switchFlagCases']:
 chosen=2 if not right or left and flags&8192 else 1
 assert result==(chosen if equip and (right if chosen==1 else left) else 3)
for gates,old,add,result,flags,merges,changes,total in data['mergeCases']:
 active=bool(gates&1 and gates&4);bound=bool(gates&2);valid=active and bound and bool(gates&8 and gates&16)
 changed=valid and old==add
 assert (result,flags,merges,changes,total)==(int(changed),1-add if changed else old,int(active and not bound),2 if changed else 0,(40 if add else -40) if changed else 0)
data_path=STAGE/'weapon-hands-source-probe.json';data_path.write_text(json.dumps(data,indent=2)+'\n')
for text,name in [(units,'UNITS_MergeDualWieldWeaponStatLists'),(units,'UNITS_CanDualWield')]:routine(text,name)
out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,probe=dict(caseCounts={k:len(v) for k,v in data.items()},totalCases=sum(len(v) for v in data.values()),dataPath=str(data_path.relative_to(ROOT)),blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()}),established=['Selected-hand resolver respects used-skill weapsel, available/equippable weapon fallbacks and server/client sequence phase differences. Server weapsel3 uses precise sequence-frame parity; the other selector flag can choose the secondary for all nonzero frames.','Damage-stat merge toggles only the damagerelated flagged stats, preserving nondamage stats. Apply/subtract is idempotent; missing/binding/extended-list guards are separate.','The surrounding merge routine resolves active hand and toggles primary/secondary stat contributions; full engine/stat propagation, native inventory/primary identity and modern sequence behavior are not certified by declared-service probes.'],limits=['Unchanged selector and damage merge toggle compiled; inventory/type/equip/stat arrays/seed-free services are declared fixtures.','Nullmodern start68/Double Swing SQ events, native dual equip, persistent primary weapon identity, modern damage-related processing and complete client/gear lifecycle remain separate work.','No Double Swing handler or native dual equip is enabled by this proof.'])
(ROOT/'tools/reference/weapon-hands-engine-evidence.json').write_text(json.dumps(out,indent=2)+'\n');print('Hand selection/stat merge source cases:',out['probe']['totalCases'])
