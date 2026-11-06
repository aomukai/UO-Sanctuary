#!/usr/bin/env python3
"""Execute the complete pinned older hand-GUID routines, never a rewrite oracle."""
import hashlib,json,random,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'inventory-weapon-identity-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={}
 def source(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 inv=source('source/D2Common/src/D2Inventory.cpp');item=source('source/D2Common/src/Items/Items.cpp');header=source('source/D2Common/include/D2Inventory.h')
 names=['INVENTORY_GetLeftHandWeapon','INVENTORY_GetSecondaryWeapon','INVENTORY_UpdateWeaponGUIDOnInsert','INVENTORY_UpdateWeaponGUIDOnRemoval','INVENTORY_UnitIsItem','ITEMS_CanBeEquipped'];bodies={n:function(item if n.startswith('ITEMS_')else inv,n)for n in names};hashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()}
 inlines=[line.strip()for line in header.splitlines()if line.startswith('inline ')and any(n+'('in line for n in ['INVENTORY_CheckSignature','INVENTORY_GetPtrIfValid'])];assert len(inlines)==2
 cpp=r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#define __stdcall
using BOOL=int;constexpr int TRUE=1,FALSE=0,UNIT_ITEM=4,BODYLOC_RARM=4,BODYLOC_LARM=5,ITEMTYPE_WEAPON=1,ITEMTYPE_MISSILE_POTION=2,INVGRID_BODYLOC=0;
constexpr uint32_t D2UnitInvalidGUID=0xffffffff,D2C_InventoryHeader=0x10203040,IFLAG_BROKEN=0x100,IFLAG_NOEQUIP=0x4000;
struct ItemData{uint32_t dwItemFlags=0;};
struct D2UnitStrc{uint32_t dwUnitId=0;int dwUnitType=4;bool weapon=false,potion=false;int body=0;ItemData*pItemData=nullptr;};
struct D2InventoryGridStrc{D2UnitStrc*ppItems[13]={};};
struct D2InventoryStrc{uint32_t dwSignature=D2C_InventoryHeader,dwLeftItemGUID=D2UnitInvalidGUID;bool grid=true;D2InventoryGridStrc slots;};
int gBodyLocInventoryGridInfo=0;
D2InventoryGridStrc*INVENTORY_GetGrid(D2InventoryStrc*i,int,int*){return i->grid?&i->slots:nullptr;}
bool ITEMS_CheckItemTypeId(D2UnitStrc*i,int type){return i&&(type==ITEMTYPE_WEAPON?i->weapon:i->potion);}
int ITEMS_GetBodyLocation(D2UnitStrc*i){return i->body;}
'''
 cpp+='\n'.join(inlines)+'\n'+bodies['INVENTORY_UnitIsItem']+'\n'+bodies['ITEMS_CanBeEquipped']+'\n'+''.join(bodies[n]+'\n'for n in names[:4])
 cpp+=r'''
void Print(D2InventoryStrc*i){auto a=INVENTORY_GetLeftHandWeapon(i),b=INVENTORY_GetSecondaryWeapon(i);printf("[%u,%u,%u]",i?i->dwLeftItemGUID:D2UnitInvalidGUID,a?a->dwUnitId:0,b?b->dwUnitId:0);}
int main(){int count;assert(scanf("%d",&count)==1);printf("[");for(int n=0;n<count;n++){
int present,signature,grid,choice;uint32_t guid;assert(scanf("%d%d%d%u%d",&present,&signature,&grid,&guid,&choice)==5);D2InventoryStrc storage;storage.dwSignature=signature?D2C_InventoryHeader:0;storage.grid=grid;storage.dwLeftItemGUID=guid;auto inventory=present?&storage:nullptr;
D2UnitStrc units[3];ItemData data[3];bool exists[3];for(int j=0;j<3;j++){int yes,weapon,potion,has;assert(scanf("%d%u%d%d%d%d%d%u",&yes,&units[j].dwUnitId,&units[j].dwUnitType,&weapon,&potion,&units[j].body,&has,&data[j].dwItemFlags)==8);exists[j]=yes;units[j].weapon=weapon;units[j].potion=potion;units[j].pItemData=has?&data[j]:nullptr;}
storage.slots.ppItems[4]=exists[0]?&units[0]:nullptr;storage.slots.ppItems[5]=exists[1]?&units[1]:nullptr;auto action=choice<0?nullptr:(exists[choice]?&units[choice]:nullptr);
printf("%s[",n?",":"");Print(inventory);INVENTORY_UpdateWeaponGUIDOnInsert(inventory,action);printf(",");Print(inventory);INVENTORY_UpdateWeaponGUIDOnRemoval(inventory,action);printf(",");Print(inventory);printf("]");}printf("]\n");}
'''
 rng=random.Random(0x10289);cases=[]
 # Deterministic independent controls: nulls,invalid signatures/grids, stale
 # GUIDs, hand reversal, missile potions, non-items and both forbidden flags.
 for n in range(8192):
  rows=[]
  for j in range(3):rows.append([int(rng.randrange(6)!=0),101+j,rng.choice([1,4,4,4]),rng.randrange(2),rng.randrange(2),[4,5,rng.choice([0,1,4,5,11,12])][j],rng.randrange(2),rng.choice([0,0x100,0x4000,0x4100,0x82010,0xffffffff])])
  cases.append(dict(Inventory=[int(n%17!=0),int(n%13!=0),int(n%11!=0),rng.choice([0xffffffff,0,101,102,103,999]),rng.choice([-1,0,1,2])],Items=rows))
 numbers=[len(cases)]
 for c in cases:numbers+=c['Inventory']+[v for row in c['Items']for v in row]
 path=STAGE/'source.cpp';path.write_text(cpp);request=STAGE/'input.txt';request.write_text(' '.join(map(str,numbers))+'\n');binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(path),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],input=request.read_bytes(),capture_output=True,timeout=60);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 output=STAGE/'output.json';output.write_bytes(run.stdout);observed=json.loads(run.stdout);assert len(observed)==len(cases)
 for c,o in zip(cases,observed):c['Stages']=o
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 proof=dict(referenceVersion='3.3.93847',sourceCommit=tree['sha'],currentExecutableParity=False,contexts=len(cases),completeRoutineHashes=hashes,inlineServiceHashes={line.split('(')[0].split()[-1]:hashlib.sha256(line.encode()).hexdigest()for line in inlines},inputs=inputs,
  scope='Six complete older source routines plus exact inventory signature/pointer inline services. Grid lookup,item-type classification and body location are explicit supplied boundary services.Three stages per context:before,insert,remove.Supplied source controls,not proof of current D2R world inventory state.',
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in [path,request,binary,output,out,STAGE/'compiler.log',STAGE/'sanitizer.log',Path(__file__).resolve()]})
 (ROOT/'tools/reference/inventory-weapon-identity-source-evidence.json').write_text(json.dumps(proof,indent=2)+'\n');print('Inventory weapon identity:8192contexts/24576stages;six complete source routines;ASan/UBSan clean.')
if __name__=='__main__':main()
