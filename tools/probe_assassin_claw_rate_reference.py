#!/usr/bin/env python3
"""Complete old claw rate + dual merge + common selector under explicit stat services."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from probe_weapon_sequence_rate import block
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Common/src/Units/Units.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
 names=['D2Common_10434','UNITS_MergeDualWieldWeaponStatLists','UNITS_UpdateAttackAnimRateAndVelocity'];blocks={n:block(raw.decode(),n) for n in names}
 cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <vector>
#include <initializer_list>
#define __forceinline
#define __fastcall
#define __stdcall
using BOOL=int;
constexpr int FALSE=0,TRUE=1,STAT_ITEM_FASTERATTACKRATE=0,STAT_ATTACKRATE=1,BODYLOC_RARM=0,BODYLOC_LARM=1,COMPOSIT_RIGHTHAND=0,COMPOSIT_LEFTHAND=1,ITEMTYPE_WEAPON=1,PLRMODE_SEQUENCE=18,STATEMASK_MELEEONLY=0,UNITFLAGEX_SERVERUNIT=1;
struct Anim{int dwAnimSpeed=208;} anim;struct D2SkillStrc{} skill;struct D2SkillsTxt{int nWeapSel=3;} row;
struct D2UnitStrc{void* pInventory=(void*)1;void* pSkills=(void*)1;Anim* pAnimData=&anim;int dwUnitType=0,dwAnimMode=18,dwSeqSpeed=0,wAnimSpeed=0,dwFlagEx=1,dwSeqFrame=0,id=0;} owner,right,left;
int mode=0,iasRight=0,iasLeft=0,otherIas=0,otherRate=0,wsmRight=0,wsmLeft=0,selected=1,merges=0;
bool UNITS_CanDualWield(D2UnitStrc*){return true;}
void INVENTORY_GetEquippedWeapon(void*,D2UnitStrc** p,int* loc,BOOL* l){*p=&right;*loc=0;*l=0;}
D2UnitStrc* INVENTORY_GetCompositItem(void*,int loc){return loc==0?&right:mode==0?nullptr:&left;}
D2UnitStrc* INVENTORY_GetItemFromBodyLoc(void* p,int loc){return INVENTORY_GetCompositItem(p,loc);}
D2UnitStrc* INVENTORY_GetLeftHandWeapon(void*){return &right;}
D2UnitStrc* INVENTORY_GetSecondaryWeapon(void*){return mode==0?nullptr:&left;}
bool ITEMS_CanBeEquipped(D2UnitStrc* p){return p && !(p==&left && mode==2);}
bool ITEMS_CheckItemTypeId(D2UnitStrc* p,int){return p && !(p==&left && mode==3);}
D2SkillStrc* SKILLS_GetUsedSkillFromSkillList(void*){return &skill;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecordFromSkill(D2SkillStrc*){return &row;}
int SKILLS_GetFlags(D2SkillStrc*){return 0;}
D2UnitStrc* STATLIST_GetOwner(D2UnitStrc*,BOOL*){return &owner;}
void STATLIST_MergeStatLists(D2UnitStrc*,D2UnitStrc* p,BOOL type){++merges;if(type)selected=p==&right?1:2;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,int,int){return otherIas+(selected==1?iasRight:iasLeft);}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int,int){return p==&right?-wsmRight:p==&left?-wsmLeft:100+otherRate-(selected==1?wsmRight:wsmLeft);}
bool STATES_CheckStateMaskOnUnit(D2UnitStrc*,int){return false;}
int D2Common_11043(D2UnitStrc*){return 0;}
int D2Clamp(int x,int lo,int hi){return std::clamp(x,lo,hi);}
'''+ '\n'.join(blocks.values())+r'''
int main(){bool comma=false;printf("[");for(int m:{0,1,2,3})for(int phase:{0,1})for(int a:{-30,-5,0,10})for(int b:{-30,-5,0,10})for(int i:{0,20,120})for(int j:{0,40})for(int other:{0,35})for(int rate:{-100,0,50,200}){
mode=m;owner.dwSeqFrame=phase<<8;wsmRight=a;wsmLeft=b;iasRight=i;iasLeft=j;otherIas=other;otherRate=rate;selected=1;merges=0;UNITS_UpdateAttackAnimRateAndVelocity(&owner);
if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",m,phase,a,b,i,j,other,rate,selected,merges,owner.dwSeqSpeed);
}printf("]\n");}
'''
 p=STAGE/'assassin-claw-rate-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(p),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==6144
 def div(a,b):return abs(a)//abs(b)*(-1 if a*b<0 else 1)
 for mode,phase,a,b,i,j,other,rate,selected,merges,speed in data:
  expectedHand=2 if mode==1 and phase else 1;assert selected==expectedHand
  bonus=other+(i if selected==1 else j);total=100+rate-(a if selected==1 else b)+120*bonus//(120+bonus)-30
  if mode==1:total+=div(-a-b,2)+a
  assert speed==208*max(15,min(175,total))//100
  assert merges==(1 if mode==0 else 2)
 fixture=STAGE/'assassin-claw-rate-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',talentId='assassin.fistsoffire',legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(raw).hexdigest()},compiledRoutines={n:hashlib.sha256(v.encode()).hexdigest() for n,v in blocks.items()},fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(data),sanitizers='ASan+UBSan'),established=['Complete common selector and dual merge precede complete attack-rate routine. Server ordinal0 selects primary IAS; ordinal1 selects secondary in the supplied valid pair. Missing/invalid secondary falls back to primary.','At sequence entry ordinal0, selected primary intrinsic attackrate plus source dual correction produces mean intrinsic rate, with C++ truncation toward zero. Item IAS diminishing return,skill rate,sequence penalty30,15..175 clamp and supplied208 animation base precede integer frame advance.','The phase1 legacy correction uses physical right-hand intrinsic rate even after selected-hand stat merge; recorded as a control, not adopted as a new native mid-animation speed mutation.'],services=['Item stat posting/accumulation supplies only the declared selected-hand IAS/intrinsic rate plus explicit globalIAS/skill-rate. Full STATLIST_MergeStatLists/stat graph is not compiled; existing source damageRelated table/native selected-hand consumers are separate evidence.','Primary source RARM maps to the existing persisted native primary identity; inventory/component/item eligibility services expose single/valid/unequippable/nonweapon secondary.','Animation base208 is an explicit input corroborated by selected current local A1HT1/HT2 metadata. Melee-only shape override and modern executable selection/rate mutation remain separate.'],limits=['This proves complete selected source numeric/selection control flow under declared stat services, not a current executable certificate.','Native learned action,real IAS/dual speed/skills/slow/hand/timing/payment/cancellation/input/persistence must still be tested.'])
 out=ROOT/'tools/reference/assassin-claw-rate-source-evidence.json';s=json.dumps(evidence,indent=2)+'\n'
 if check:assert out.read_text()==s,'Claw rate evidence stale'
 else:out.write_text(s)
 print('Claw rate:6144 complete selector/dual-merge/rate cases pass under declared stat services.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
