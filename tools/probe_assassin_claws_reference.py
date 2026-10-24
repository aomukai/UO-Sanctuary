#!/usr/bin/env python3
"""Compile full historical usable-item and weapon-composition routines."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  blocks[name]=text[m.start():i];return blocks[name]+'\n'
 itemheader=source('source/D2Common/include/D2Items.h');itemflags={name:int(value,16) for name,value in re.findall(r'(IFLAG_BROKEN|IFLAG_NOEQUIP)\s*=\s*(0x[0-9A-Fa-f]+)',itemheader)};assert itemflags=={'IFLAG_BROKEN':256,'IFLAG_NOEQUIP':16384}
 compos=source('source/D2Common/src/D2Composit.cpp');header=source('source/D2Common/include/D2Composit.h');items=source('source/D2Common/src/Items/Items.cpp')
 enum=re.search(r'enum D2WeaponClasses\s*\{.*?\};',header,re.S).group();table=re.search(r'static const D2CompositStrc stru_6FDCE260\[\]\s*=\s*\{.*?\};',compos,re.S).group()
 cpp=r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __stdcall
#define ARRAY_SIZE(x) (sizeof(x)/sizeof((x)[0]))
using BOOL=int;constexpr int BODYLOC_RARM=0,BODYLOC_LARM=1,TRUE=1,FALSE=0,UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_OBJECT=2,UNIT_ITEM=4,PCLASS_ASSASSIN=6,PCLASS_BARBARIAN=4,PCLASS_PALADIN=3,PLRMODE_DEAD=17,MONMODE_DEATH=0,MONMODE_DEAD=12,COMPOSIT_RIGHTHAND=5,COMPOSIT_LEFTHAND=6,IFLAG_BROKEN=256,IFLAG_NOEQUIP=16384,ITEMTYPE_WEAPON=1,MONSTATS2FLAGINDEX_COMPOSITEDEATH=0,UNITFLAGEX_ISSHAPESHIFTED=1;
struct D2ItemDataStrc{uint32_t dwItemFlags=0;} data[2];
struct D2InventoryStrc{} inventory;
struct D2UnitStrc{int dwUnitType=0,dwClassId=6,dwFlagEx=0,mode=1,kind=0;D2ItemDataStrc* pItemData=nullptr;} owner,hands[2];
struct D2CompositStrc{int nWeaponClassCode,nWeaponClassId;};
struct D2CharStatsTxt{int dwBaseWClass=' hth';} character;
struct D2MonStats2Txt{int nBaseW=0,dwFlags=0;};
uint32_t gdwBitMasks[]={1};
D2UnitStrc* INVENTORY_GetItemFromBodyLoc(D2InventoryStrc*,int loc){return hands[loc].kind?&hands[loc]:nullptr;}
D2CharStatsTxt* UNITS_GetCharStatsTxtRecord(int){return &character;}
D2MonStats2Txt* UNITS_GetMonStats2TxtRecordFromMonsterId(int){return nullptr;}
uint8_t ITEMS_GetComponent(D2UnitStrc* p){return p->kind==3?COMPOSIT_LEFTHAND:COMPOSIT_RIGHTHAND;}
BOOL ITEMS_CheckItemTypeId(D2UnitStrc* p,int){return p && p->kind!=3;}
D2UnitStrc* INVENTORY_GetLeftHandWeapon(D2InventoryStrc*){return &hands[0];}
void INVENTORY_UpdateWeaponGUIDOnInsert(D2InventoryStrc*,D2UnitStrc*){}
int INVENTORY_GetWieldType(D2UnitStrc*,D2InventoryStrc*){return 1;}
int ITEMS_GetWeaponClassCode(D2UnitStrc* p){return p->kind==1?' 1th':p->kind==2?' sh1':' hth';}
int ITEMS_Get2HandWeaponClassCode(D2UnitStrc* p){return ITEMS_GetWeaponClassCode(p);}
int UNITS_GetAnimOrSeqMode(D2UnitStrc* p){return p->mode;}
void D2COMMON_11013_ConvertMode(D2UnitStrc*,int*,int*,int*,const char*,int){}
'''
 cpp+=enum+'\n'+table+'\n'+routine(items,'ITEMS_CanBeEquipped')+routine(compos,'COMPOSIT_GetWeaponClassIdFromCode')
 cpp+='int ITEMS_GetWeaponClassId(D2UnitStrc* p){return COMPOSIT_GetWeaponClassIdFromCode(ITEMS_GetWeaponClassCode(p));}\n'
 cpp+=routine(compos,'COMPOSIT_GetWeaponClassCode')+routine(compos,'COMPOSIT_GetWeaponClassId')
 cpp+=r'''int main(){bool first=true;std::printf("[");
for(int left:{0,1,2,3})for(int right:{0,1,2,3})for(int lf:{0,1,2,3})for(int rf:{0,1,2,3})for(int cls:{PCLASS_ASSASSIN,PCLASS_BARBARIAN,PCLASS_PALADIN})for(int mode:{0,1,2,7,10,17})for(int inv:{0,1})for(int payload:{0,1}){
 owner.dwClassId=cls;owner.mode=mode;hands[0].dwUnitType=hands[1].dwUnitType=UNIT_ITEM;
 hands[0].kind=right;hands[1].kind=left;data[0].dwItemFlags=(rf&1?IFLAG_BROKEN:0)|(rf&2?IFLAG_NOEQUIP:0);data[1].dwItemFlags=(lf&1?IFLAG_BROKEN:0)|(lf&2?IFLAG_NOEQUIP:0);
 for(int i=0;i<2;i++)hands[i].pItemData=payload?&data[i]:nullptr;
 int id=-1;COMPOSIT_GetWeaponClassId(&owner,inv?&inventory:nullptr,&id,-1,1);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d]",left,right,lf,rf,cls,mode,inv,payload,id);
}std::printf("]\n");}'''
 path=STAGE/'assassin-claws-source-probe.cpp';path.write_text(cpp);binary=STAGE/'assassin-claws-source-probe';subprocess.run(['g++','-std=c++20','-Wno-multichar','-O2',str(path),'-o',str(binary)],check=True)
 rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==18432
 for left,right,lf,rf,cls,mode,inv,payload,ident in rows:
  expected=bool(cls==6 and mode not in (0,17) and inv and left in (1,2) and right in (1,2) and (not payload or lf==rf==0))
  assert (ident==13)==expected
 fixture=STAGE/'assassin-claws-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',talentId='assassin.weaponblock',proofEnablesHandler=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledUnchangedRoutines={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},verifiedItemFlags=itemflags,compiledUnchangedTableSha256=hashlib.sha256(table.encode()).hexdigest(),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(rows)),established=['Complete item usability rejects BROKEN/NOEQUIP for ordinary item-data payloads. Complete compositor and ID wrapper select HT2/13 for two usable weapons on an Assassin in nonzero/nondead modes. The compositor does not itself restrict those two weapons to claws; legal equip and passive h2h stat-layer supply are separate.','The compiled source enum and code table establish HT2=13 without a supplied-composite shortcut. Null item data is explicitly synthetic and preserved as the original usability routine permits it.'],services=['Inventory/primary/type/component/weapon-code/animation/character-row lookups are explicit services. Actor is an untransformed player; monster table is absent, wield type1 and primary right hand are supplied. Full legal item-equip/stat engine is not simulated.'],limits=['No current executable or native gear transaction proof. Native legal claw families, owner/effective-rank properties, attack/missile defense/reaction, persistence and current acceptance remain separate.'])
 target=ROOT/'tools/reference/assassin-claws-engine-evidence.json';rendered=json.dumps(evidence,indent=2)+'\n'
 if check:assert target.read_text()==rendered
 else:target.write_text(rendered)
 print('Assassin claws:18432 complete usable-item/compositor cases pass; real native equipment remains a separate gate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
