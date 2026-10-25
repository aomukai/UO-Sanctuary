#!/usr/bin/env python3
"""Complete legacy kick hand-selector/fill-context wrappers, with declared stat services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m;end=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
  blocks[name]=text[m.start():end];return blocks[name]
 units=source('source/D2Common/src/Units/Units.cpp');player=source('source/D2Game/src/PLAYER/Player.cpp')
 cpp=r'''
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __stdcall
#define __fastcall
using BOOL=int;constexpr int TRUE=1,FALSE=0,UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_ITEM=4,PCLASS_BARBARIAN=4,PCLASS_ASSASSIN=6,MONSTER_SHADOWWARRIOR=1001,MONSTER_SHADOWMASTER=1002,ITEMTYPE_WEAPON=42;
struct D2SkillsTxt{int nWeapSel=4;} row;
struct D2SkillStrc{} skill;
struct D2UnitStrc{int dwUnitType=UNIT_PLAYER,dwClassId=PCLASS_ASSASSIN,id=0;unsigned dwSeqFrame=0;void* pInventory=(void*)1;} owner,first,second;
int hands=3,primary=1,owned=3,active=0,mergeCalls=0,trace=0,flags=0;bool used=true,valid=true;
D2UnitStrc* hand(int id){return (hands&(1<<(id-1)))?(id==1?&first:&second):nullptr;}
bool ITEMS_CheckItemTypeId(D2UnitStrc* p,int){return p!=nullptr;}
bool ITEMS_CanBeEquipped(D2UnitStrc* p){return p!=nullptr;}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return used?&skill:nullptr;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecordFromSkill(D2SkillStrc*){return valid?&row:nullptr;}
int SKILLS_GetFlags(D2SkillStrc*){return flags;}
void INVENTORY_GetEquippedWeapon(void*,D2UnitStrc** weapon,int* body,int* left){*weapon=hand(primary);*body=4;*left=0;}
D2UnitStrc* INVENTORY_GetCompositItem(void*,int location){return hand(location==5?1:2);}
D2UnitStrc* INVENTORY_GetLeftHandWeapon(void*){return hand(primary);}
D2UnitStrc* INVENTORY_GetSecondaryWeapon(void*){return hand(3-primary);}
D2UnitStrc* STATLIST_GetOwner(D2UnitStrc* p,int){return p && (owned&(1<<(p->id-1)))?&owner:nullptr;}
void STATLIST_MergeStatLists(D2UnitStrc*,D2UnitStrc* p,int include){int bit=1<<(p->id-1);active=include?active|bit:active&~bit;++mergeCalls;trace=trace*10+(p->id*2+include);}
'''
 cpp+=routine(units,'UNITS_CanDualWield')+'\n'+routine(player,'sub_6FC7C7B0')+'\n'+routine(player,'sub_6FC7C900')+r'''
int main(){first.id=1;second.id=2;bool comma=false;printf("[");
for(int kind:{0,1,2,3,4,5})for(int mask=0;mask<4;++mask)for(int pri:{1,2})for(int own=0;own<4;++own)for(int before=0;before<4;++before)for(int mode:{0,1})for(int select:{0,1,3,4})for(int frame:{0,1}){
 owner.dwUnitType=kind<3?UNIT_PLAYER:kind<5?UNIT_MONSTER:UNIT_ITEM;owner.dwClassId=kind==0?PCLASS_ASSASSIN:kind==1?PCLASS_BARBARIAN:kind==3?MONSTER_SHADOWWARRIOR:kind==4?MONSTER_SHADOWMASTER:0;owner.dwSeqFrame=frame<<8;hands=mask;primary=pri;owned=own;active=before;row.nWeapSel=select;mergeCalls=trace=0;auto* selected=sub_6FC7C7B0(&owner);sub_6FC7C900(&owner,mode);
 if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",kind,mask,pri,own,before,mode,select,frame,selected?selected->id:0,active,mergeCalls,trace);
}printf("]\n");}
'''
 path=STAGE/'dragon-talon-hands-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(path),'-o',str(binary)],check=True)
 data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==12288
 for kind,hands,primary,owned,before,mode,select,frame,selected,after,calls,trace in data:
  dual=kind in [0,1,3,4]
  expected=primary if hands&(1<<(primary-1)) else 0
  if dual:
   desired=2 if select==1 or select==3 and frame else 1
   if select==4:expected=0
   elif hands&(1<<(desired-1)):expected=desired
   elif select==0 and hands&2:expected=2
  assert selected==expected
  if select==4:
   active=before
   if mode:active&=~(hands&owned)
   primary_bit=(1<<(primary-1))&hands&owned
   if dual:active|=primary_bit
   assert after==active
 fixture=STAGE/'dragon-talon-hands-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',talentId='assassin.dragontalon',legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(data),sanitizers='ASan+UBSan'),declaredServices=['Inventory maps logical hand1/2 and primary identity. Item pointers are declared equippable weapons; used-skill and row exist. Missing hands, bound stat-list ownership and starting damage-stat merge mask vary explicitly.','MergeStatLists is an observed damage-stat mask/call-trace service. Its full propagation is not compiled here; existing weapon-hands-engine-evidence separately compiles its shared DamageRelated toggle. Non-damage stats are outside this mask and must not be removed.','Shadow class constants are distinct symbolic fixture tags, not a source monster-ID catalogue. Source CanDualWield branch logic executes unchanged; current Warlock and other modern classes are outside that old routine.'],established=['For an Assassin using weapsel4, the full player weapon selector returns null. FillDamageValues calls sub_6FC7C900(a5=1): this disables owned hand damage stats, then its null-selected fallback re-enables inventory primary damage stats for source dual-wield-capable classes.','Therefore earlier temporary weapon damage-stat suppression during boot arithmetic does not prove blanket exclusion from later kick elemental/leech fill. Selected primary, not both weapon damage lists, is restored in this context.','Nondual contexts retain a distinct path; native kick use for foreign classes/shadows is not established.'],limits=['No complete FillDamageValues or kick boot/body arithmetic or item proc/event pipeline is compiled by this probe. Hand services and class tags are explicit boundaries.','No modern guaranteed-hit/block/consume-one, timing/payment/input/native handler or class completion certificate. Effects/icons remain separate.'])
 out=ROOT/'tools/reference/dragon-talon-hands-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Dragon Talon hands evidence stale'
 else:out.write_text(encoded)
 print('Dragon Talon:12288 complete dual-wield eligibility/weapon-selector/fill-context cases pass; no full damage or native handler certificate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
