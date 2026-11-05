#!/usr/bin/env python3
"""Execute complete older inventory/body/stack/hand placement classifiers."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-equip-placement-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={};bodies={}
 def read(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 for rel,names in {'source/D2Common/src/D2Inventory.cpp':['D2Common_10299','sub_6FD91D50','sub_6FD91E80'],'source/D2Common/src/Items/Items.cpp':['ITEMS_CheckBodyLocation','ITEMS_Is1Or2Handed','ITEMS_AreStackablesEqual','ITEMS_CheckWeaponIfTwoHandedByItemId','ITEMS_CheckWeaponIfTwoHanded']}.items():
  text=read(rel)
  for n in names:bodies[n]=function(text,n)
 reviewed={}
 for rel,names in {'source/D2Game/src/MONSTER/MonsterAI.cpp':['sub_6FC61190'],'source/D2Common/src/D2StatList.cpp':['STATLIST_GetMaxDurabilityFromUnit','D2Common_11248','STATLIST_GetTotalStatValue_Layer0','STATLIST_GetTotalStatValue']}.items():
  text=read(rel)
  for name in names:reviewed[name]=hashlib.sha256(function(text,name).encode()).hexdigest()
 flagheader=read('source/D2Common/include/D2Items.h');assert 'IFLAG_NOSELL = 0x00001000'in flagheader
 header=read('source/D2Common/include/D2Inventory.h');validate=header[header.index('inline bool INVENTORY_ValidateBodyLoc'):].split('\n',1)[0];assert validate=='inline bool INVENTORY_ValidateBodyLoc(int nBodyLoc) { return nBodyLoc >= 0 && nBodyLoc < NUM_BODYLOC; }'
 def load(rel):
  p=ROOT/rel;inputs[rel]=sha(p);return json.loads(p.read_text())
 pool=load('data/Sanctuary/Balance/npc-item-affix-catalogue.json');types=load('data/Sanctuary/Balance/npc-equip-types.json')['Types'];load('tools/reference/npc-equip-types-evidence.json');fin=load('data/Sanctuary/Balance/item-finishing-catalogue.json');ordinal={r['code']:r['ordinal']for r in fin['types']};typefacts={r['Code']:r for r in types};hier={r['code']:r['parents']for r in pool['types']}
 def isa(t,p):return t==p or any(isa(q,p)for q in hier[t])
 items=[]
 for b in pool['bases']:
  raw=b['sourceFields'];t=typefacts[b['type']];I=lambda k:raw.get(k,0)
  items.append(dict(Code=b['code'],UnitType=4,HasData=True,Quality=2,File=0,Ethereal=False,Stackable=bool(I('stackable')),TwoHanded=bool(I('2handed')),OneOrTwoHanded=bool(I('1or2handed')),Weapon=isa(b['type'],'weap'),Claw=isa(b['type'],'h2h'),Body1=t['Body1'],Body2=t['Body2'],Ammo=t['Shoots'],Quiver=t['Quiver'],Types=[r['Code']for r in types if isa(b['type'],r['Code'])],Damage=[I(k)for k in ('mindam','maxdam','2handmindam','2handmaxdam','minmisdam','maxmisdam')],Sockets=0))
 bycode={r['Code']:i for i,r in enumerate(items)};basecount=len(items);assert basecount==692
 actors=[(1,0,0),(1,0,1),(1,0,2),(1,0,3),(1,0,4),(1,0,5),(0,4,0),(0,6,0),(0,0,0),(2,0,0)]
 # Input:unit type,class,symbolic monster,inventory/signature/grid/req/free/skip bits,body,candidate,same,other item indices.
 cases=[]
 for i in range(basecount):
  for body in range(13):
   for actor in actors[:3]:cases.append([*actor,63,body,i,-1,-1])
 hand=[bycode[c]for c in ('ssd','btl','ces','buc','rin','amu','qui','hxb','sbw','jav','aqv','cqv','2hs')];held=[-1]+hand
 for actor in actors:
  for body in (4,5,11,12):
   for item in hand:
    for same in held:
     for other in held:cases.append([*actor,63,body,item,same,other])
 variants=[]
 for change in ({},{'Quality':0},{'Quality':1},{'Quality':3},{'Quality':4},{'Quality':9},{'Quality':10},{'File':1},{'Ethereal':True},{'Sockets':1},{'Damage':[8,14,3,4,5,6]},{'UnitType':2},{'HasData':False}):
  variants.append(len(items));items.append(items[bycode['jav']]|change)
 for flags in range(64):
  for item in variants:
   for same in variants:cases.append([1,0,1,flags,4,item,same,bycode['ssd']])
 for actor in actors:
  for body in (-1,0,4,5,11,12,13,256):
   for flags in (0,1,3,7,15,31,63,191):cases.append([*actor,flags,body,-1,-1,bycode['2hs']])
 cpp=r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __stdcall
#define __fastcall
#define BOOL int
#define TRUE 1
#define FALSE 0
#define D2_ASSERT(x) assert(x)
#define D2_MAYBE_UNUSED(x) ((void)x)
#define FOG_Trace(...) ((void)0)
constexpr int UNIT_ITEM=4,UNIT_PLAYER=0,UNIT_MONSTER=1,PCLASS_BARBARIAN=4,PCLASS_ASSASSIN=6;
constexpr int MONSTER_SHADOWWARRIOR=1,MONSTER_SHADOWMASTER=2,MONSTER_ANCIENTBARB1=3,MONSTER_ANCIENTBARB2=4,MONSTER_ANCIENTBARB3=5;
constexpr int BODYLOC_NONE=0,BODYLOC_RARM=4,BODYLOC_LARM=5,BODYLOC_SWRARM=11,BODYLOC_SWLARM=12,NUM_BODYLOC=13,INVGRID_BODYLOC=0;
constexpr int STAT_MINDAMAGE=21,STAT_MAXDAMAGE=22,STAT_SECONDARY_MINDAMAGE=23,STAT_SECONDARY_MAXDAMAGE=24,STAT_ITEM_THROW_MINDAMAGE=159,STAT_ITEM_THROW_MAXDAMAGE=160,STAT_ITEM_NUMSOCKETS=194,IFLAG_ETHEREAL=0x400000;
'''+f'constexpr int ITEMTYPE_WEAPON={ordinal["weap"]},ITEMTYPE_HAND_TO_HAND={ordinal["h2h"]};\n'+validate+r'''
struct D2ItemDataStrc{int dwQualityNo=2,dwFileIndex=0;};struct D2UnitStrc;
struct D2InventoryGridStrc{D2UnitStrc*ppItems[13]={};};struct D2InventoryStrc{bool signature=true,grid=true;D2InventoryGridStrc body;};
struct D2UnitStrc{int dwUnitType=4,dwClassId=0;D2InventoryStrc*pInventory=nullptr;D2ItemDataStrc data,*pItemData=&data;bool ethereal=false;int damage[6]={},sockets=0;};
struct D2ItemsTxt{int wType[2],n1or2Handed,n2Handed,nStackable;};struct D2ItemTypesTxt{uint8_t nBodyLoc1,nBodyLoc2;};
struct Tables{int nItemTypesTxtRecordCount;D2ItemTypesTxt*pItemTypesTxt;}tables,*sgptDataTables=&tables;
'''
 cpp+='D2ItemsTxt bases[]={\n'
 for b in pool['bases']:
  r=b['sourceFields'];cpp+='{{'+str(ordinal[b['type']])+',0},'+','.join(str(r.get(k,0))for k in ('1or2handed','2handed','stackable'))+'},\n'
 cpp+='};\nD2ItemTypesTxt typeRows[110]={};\nstd::vector<int>ancestry[]={\n'
 for b in pool['bases']:cpp+='{'+','.join(str(ordinal[r['Code']])for r in types if isa(b['type'],r['Code']))+'},\n'
 cpp+='};\nint ammo[]={'+','.join(str(ordinal[typefacts[b['type']]['Shoots']])if typefacts[b['type']]['Shoots']else'0'for b in pool['bases'])+'};\nint quiver[]={'+','.join(str(ordinal[typefacts[b['type']]['Quiver']])if typefacts[b['type']]['Quiver']else'0'for b in pool['bases'])+'};\n'
 cpp+=r'''
bool req=true,freePosition=true;int reqCalls=0,freeCalls=0,gBodyLocInventoryGridInfo=0;
D2ItemsTxt*DATATBLS_GetItemsTxtRecord(int id){return id>=0&&id<692?bases+id:nullptr;}
int ITEMS_GetAmmoType(D2UnitStrc*p){return p&&p->dwUnitType==4?ammo[p->dwClassId]:0;}int ITEMS_GetQuiverType(D2UnitStrc*p){return p&&p->dwUnitType==4?quiver[p->dwClassId]:0;}
bool ITEMS_CheckItemTypeId(D2UnitStrc*p,int type){return p&&p->dwUnitType==4&&std::find(ancestry[p->dwClassId].begin(),ancestry[p->dwClassId].end(),type)!=ancestry[p->dwClassId].end();}
int MONSTERS_ValidateMonsterId(int n){return n;}int ITEMS_CheckRequirements(D2UnitStrc*,D2UnitStrc*,int,void*,void*,void*){reqCalls++;return req;}
int ITEMS_CheckItemFlag(D2UnitStrc*p,int flag,int,const char*){assert(flag==IFLAG_ETHEREAL);return p->ethereal;}
int STATLIST_UnitGetStatValue(D2UnitStrc*p,int stat,int layer){assert(layer==0);int ids[]={21,22,23,24,159,160};for(int i=0;i<6;i++)if(ids[i]==stat)return p->damage[i];assert(stat==194);return p->sockets;}
D2InventoryGridStrc*INVENTORY_GetGrid(D2InventoryStrc*p,int,const void*){return p&&p->grid?&p->body:nullptr;}
bool INVENTORY_CheckSignature(D2InventoryStrc*p){return p&&p->signature;}D2InventoryStrc*INVENTORY_GetPtrIfValid(D2InventoryStrc*p){return INVENTORY_CheckSignature(p)?p:nullptr;}
int UNITS_GetInventoryRecordId(D2UnitStrc*,int,int){return 0;}bool INVENTORY_GetFreePosition(D2InventoryStrc*,D2UnitStrc*,int,int*,int*,int){freeCalls++;return freePosition;}
int sub_6FD91D50(D2UnitStrc*,int,int,D2UnitStrc*,D2UnitStrc*,D2UnitStrc*,int);BOOL sub_6FD91E80(D2UnitStrc*,D2UnitStrc*,D2UnitStrc*);
'''
 order=['ITEMS_CheckWeaponIfTwoHandedByItemId','ITEMS_CheckWeaponIfTwoHanded','ITEMS_Is1Or2Handed','ITEMS_CheckBodyLocation','ITEMS_AreStackablesEqual','sub_6FD91E80','sub_6FD91D50','D2Common_10299'];cpp+='\n'.join(bodies[n]for n in order)
 cpp+='\nstruct ItemInput{int code,type,data,quality,file,eth;int damage[6];int sockets;};\nItemInput items[]={\n'
 for r in items:cpp+='{'+','.join(map(str,(bycode[r['Code']],r['UnitType'],int(r['HasData']),r['Quality'],r['File'],int(r['Ethereal']))))+',{'+','.join(map(str,r['Damage']))+'},'+str(r['Sockets'])+'},\n'
 cpp+='};\nint cases[][8]={\n'+''.join('{'+','.join(map(str,c))+'},\n'for c in cases)+'};\n'
 cpp+='void Init(){tables={110,typeRows};\n'
 for t in types:cpp+=f'typeRows[{ordinal[t["Code"]]}]={{{t["Body1"]},{t["Body2"]}}};\n'
 cpp+=r'''}
void Load(D2UnitStrc&u,int i){u={};if(i<0)return;auto c=items[i];u.dwClassId=c.code;u.dwUnitType=c.type;u.pItemData=c.data?&u.data:nullptr;u.data={c.quality,c.file};u.ethereal=c.eth;std::copy(c.damage,c.damage+6,u.damage);u.sockets=c.sockets;}
int main(){Init();printf("[");bool comma=false;for(auto c:cases){D2InventoryStrc inv;D2UnitStrc actor,a,b,d;actor.dwUnitType=c[0];actor.dwClassId=c[0]==1?c[2]:c[1];actor.pInventory=(c[3]&1)?&inv:nullptr;inv.signature=c[3]&2;inv.grid=c[3]&4;req=c[3]&8;freePosition=c[3]&16;Load(a,c[5]);Load(b,c[6]);Load(d,c[7]);auto item=c[5]<0?nullptr:&a;auto same=c[6]<0?nullptr:&b;auto other=c[7]<0?nullptr:&d;int opposite=c[4]==4?5:c[4]==5?4:c[4]==11?12:11;if(c[4]>=0&&c[4]<13)inv.body.ppItems[c[4]]=same;inv.body.ppItems[opposite]=other;auto p=(c[3]&128)?nullptr:&actor;reqCalls=freeCalls=0;int result=D2Common_10299(p,c[4],item,(c[3]&32)!=0);printf("%s[%d,%d,%d,%d,%d,%d]",comma?",":"",result,reqCalls,freeCalls,ITEMS_CheckBodyLocation(item,(uint8_t)c[4]),ITEMS_AreStackablesEqual(same,item),p?sub_6FD91E80(p,item,other):-1);comma=true;}printf("]\n");}
'''
 for n in order:assert function(cpp,n)==bodies[n],n
 generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True,timeout=120);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
 results=json.loads(run.stdout);assert len(results)==len(cases);out=STAGE/'cases.json';out.write_text(json.dumps(dict(items=items,inputs=cases,outputs=results,bodyControls=692*13*3),separators=(',',':'))+'\n')
 receipt=dict(referenceVersion='3.3.93847',currentExecutableParity=False,cases=len(cases),bodyControls=692*13*3,currentBases=692,completeRoutineHashes={n:hashlib.sha256(v.encode()).hexdigest()for n,v in bodies.items()},reviewedOnlyRoutineHashes=reviewed,noSellFlag=4096,inlineBodyValidationSha256=hashlib.sha256(validate.encode()).hexdigest(),inputs=inputs,
  scope='Complete older inventory outcome/hand compatibility/body allowance/equal-stack/one-or-two-hand classifiers with all692current body contexts,hand-family triples,stack variants and lazy prerequisite/free-grid gates. Monster labels are symbolic fixture contexts,not modern row IDs.',
  limits=['Requirements satisfaction,exchange-grid availability,valid inventory signature/grid,logical type ancestry,immutable table lookup and six operative damage/socket fields are explicit services. No complete inventory placement/equip/stat delivery,allocation failure,player Warlock behavior or current executable parity is claimed.'],artifacts={str(p.relative_to(ROOT)):sha(p)for p in(generated,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',out,Path(__file__).resolve())})
 path=ROOT/'tools/reference/npc-equip-placement-source-evidence.json';encoded=json.dumps(receipt,indent=2)+'\n'
 if check:assert path.read_text()==encoded,'NPC placement source evidence stale'
 else:path.write_text(encoded)
 print('NPC equip placement:',len(cases),'complete classifier controls/8routines+inline body validation;26988current body contexts;ASan+UBSan clean.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
