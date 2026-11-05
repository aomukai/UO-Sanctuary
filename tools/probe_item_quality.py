#!/usr/bin/env python3
"""Execute complete pinned item quality orchestration,roll,finalizer and resets."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'item-quality-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={};bodies={}
 def read(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 for rel,names in {'source/D2Game/src/ITEMS/ItemMode.cpp':['sub_6FC4C5F0_End','sub_6FC4C5F0'],'source/D2Game/src/ITEMS/Items.cpp':['ITEMS_RollItemQuality','sub_6FC4E1A0','sub_6FC4E1F0','sub_6FC4E430'],'source/D2Common/src/D2Seed.cpp':['SEED_InitLowSeed','SEED_GetLowSeed'],'source/D2Common/include/D2Seed.h':['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber'],'source/D2Common/src/Items/Items.cpp':['ITEMS_GetItemSeed']}.items():
  raw=read(rel)
  for n in names:bodies[n]=function(raw,n)
 header=read('source/D2Common/include/D2Items.h');type_enum=header[header.index('enum D2C_ItemTypes'):header.index('};',header.index('enum D2C_ItemTypes'))+2];assert 'ITEMDROPFLAG_SUPERIOR = 0x00000040' in header
 header=read('source/D2Common/include/DataTbls/ItemsTbls.h');assert 'UNIQUEITEMSFLAGINDEX_ENABLED = 0,' in header and 'UNIQUEITEMSFLAGINDEX_CARRY1,\n\tUNIQUEITEMSFLAGINDEX_LADDER' in header
 ratios=[[100,80,60,40,20,10,1,2,3,4,5,6],[1]*12,[1000000000]*6+[1]*6,[20,15,10,8,6,4,2,3,4,5,6,7]]
 # version,enabled,ladder,base code,level,rarity;no current unique table is simulated as proved.
 profiles=[[],[[0,1,0,42,1,0],[100,1,1,42,15,3],[0,0,0,42,1,50],[0,1,0,43,1,1]],[[0,1,0,42,1,1],[0,1,0,42,1,3]],[[100,1,1,42,99,2],[0,1,0,42,100,9]],[[0,1,0,43,1,1],[0,1,0,42,1,0],[100,1,0,42,1,7]]]
 seeds=[0,((666<<32)|511),(1<<64)-1];cases=[]
 def case(entry=0,fmt=100,q=6,policy=72,mask=0,profile=1,pref=0,dropped=0,lookup=15,canDrop=1,auto=17,autoResult=71,after=0,ratio=0,level=99,baselevel=1,seed=0):
  cases.append([entry,fmt,q,policy,mask,profile,pref,dropped,lookup,canDrop,auto,autoResult,after,ratio,level,baselevel,str(seed)])
 def mask(n):return sum(1<<q for bit,q in enumerate((1,3,6,4,8,5,10,11))if n&(1<<(bit%4)))
 for fmt in (0,1,99,100):
  for q in range(-1,11):
   for n in range(16):
    for seed in seeds:case(fmt=fmt,q=q,mask=mask(n),seed=seed)
 policies=[0,8,9,10,11,12,13,24,40,56,255,8|512,8|1024,8|2048]
 for fmt in (0,1,99,100):
  for q in range(-1,11):
   for policy in policies:
    for m in (0,4095):
     for seed in seeds:case(fmt=fmt,q=q,policy=policy,mask=m,seed=seed)
 seq=0
 for fmt in (0,1,99,100):
  for profile in range(5):
   for pref in (-1,0,1,2,9):
    for force in (0,1):
     for dropped in (0,1,15):
      for lookup in (0,15):
       for canDrop in (0,1):
        for seed in seeds:
         policy=8|64|(force<<7)|((seq%2)<<8)|((seq%3==0)<<2)|((seq%11==0)<<4);seq+=1
         case(fmt=fmt,q=7,policy=policy,profile=profile,pref=pref,dropped=dropped,lookup=lookup,canDrop=canDrop,seed=seed)
 for fmt in (0,1,99,100):
  for q in range(-1,11):
   for auto in (0,17):
    for result in (0,71,-1):
     for after in range(5):
      for seed in seeds:case(entry=2,fmt=fmt,q=q,auto=auto,autoResult=result,after=after,seed=seed)
 for fmt in (0,1,99,100):
  for q in (0,4):
   for ratio in range(4):
    for level in (0,1,99,787):
     for baselevel in (0,99):
      for flags in range(4):
       for seed in seeds:case(entry=1,fmt=fmt,q=q,policy=8|((flags&1)<<2)|((flags&2)<<11)|((level%2)<<13),ratio=ratio,level=level,baselevel=baselevel,seed=seed)
 for entry in (3,4,5):
  for fmt in (0,1,99,100):
   for q in (0,4,6):
    for n in range(16):
     for seed in seeds:case(entry=entry,fmt=fmt,q=q,mask=mask(n),seed=seed)
 cpp=r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <iterator>
#include <map>
#include <vector>
#define __fastcall
#define __stdcall
#define D2_ASSERT(x) assert(x)
#define D2_MAYBE_UNUSED(x) ((void)x)
constexpr unsigned long long operator""i64(unsigned long long n){return n;}
constexpr int UNIT_ITEM=4,IFLAG_IDENTIFIED=16,ITEMDROPFLAG_SUPERIOR=64,UNIQUEITEMSFLAGINDEX_ENABLED=0,UNIQUEITEMSFLAGINDEX_LADDER=3;
enum{ITEMQUAL_INFERIOR=1,ITEMQUAL_NORMAL,ITEMQUAL_SUPERIOR,ITEMQUAL_MAGIC,ITEMQUAL_SET,ITEMQUAL_RARE,ITEMQUAL_UNIQUE,ITEMQUAL_CRAFT,ITEMQUAL_TEMPERED};
uint32_t gdwBitMasks[32];
union D2SeedStrc{uint64_t lSeed;struct{uint32_t nLowSeed,nHighSeed;};};
struct D2GameStrc{int nDifficulty=2,nGameType=0;uint32_t dwGameType=0;}game;
struct D2ItemDataStrc{D2SeedStrc pSeed;};
struct D2UnitStrc{int dwUnitType=4,dwClassId=1;D2GameStrc*pGame=&game;D2SeedStrc pSeed;D2ItemDataStrc data;int quality=6,format=100,file=41,aut=51,flags=16,rp=31,rs=32;int prefix[3]={11,12,13},suffix[3]={21,22,23};uint32_t start=17;std::map<int,int>stats={{7,1000},{72,60},{73,80}};}item;
struct D2ItemDropStrc{int nQuality=0,nItemLvl=99,nItemIndex=0;uint32_t dwFlags2=0;bool bForce=false;}drop;
struct D2ItemsTxt{uint32_t dwCode=42;int nQuest=0,nLevel=1;int16_t wAutoPrefix=17;}basis;
struct D2UniqueItemsTxt{uint16_t wVersion=0;uint32_t dwBaseItemCode=42,dwUniqueItemFlags=1;uint16_t wLvl=1,wRarity=1;};
struct DataTables{int nUniqueItemsTxtRecordCount=0;D2UniqueItemsTxt*pUniqueItemsTxt=nullptr;}tables,*sgptDataTables=&tables;
struct Weighted{int nIndex,nAccumulatedRarity;}stru_6FD45C18[32];
struct D2MagicAffixTxt{int id;}affix;
struct D2ItemRatioTxt{int dwUnique,dwRare,dwSet,dwMagic,dwHiQuality,dwNormal,dwUniqueDivisor,dwRareDivisor,dwSetDivisor,dwMagicDivisor,dwHiQualityDivisor,dwNormalDivisor;}ratio;
int policy=72,success=0,lookupMask=15,droppedMask=0,canDrop=1,autoResult=71,after=0;std::vector<std::vector<int>>trace;
void T(std::initializer_list<int>v){trace.emplace_back(v);}
D2ItemDataStrc*ITEMS_GetItemData(D2UnitStrc*p){return &p->data;}
D2ItemsTxt*DATATBLS_GetItemsTxtRecord(int){return policy&2048?nullptr:&basis;}
D2ItemRatioTxt*DATATBLS_GetItemRatioTxtRecord(int,int,int){return &ratio;}
int ITEMS_GetItemFormat(D2UnitStrc*p){return p->format;}int ITEMS_GetItemQuality(D2UnitStrc*p){return p->quality;}
int ITEMS_CheckItemTypeId(D2UnitStrc*,int kind){assert(kind==ITEMTYPE_MISCELLANEOUS);return (policy&4096)!=0;}
void ITEMS_SetItemQuality(D2UnitStrc*p,int q){p->quality=q;T({1,q});}
void ITEMS_SetAutoAffix(D2UnitStrc*p,int id){p->aut=id;T({2,id});}
void ITEMS_AssignPrefix(D2UnitStrc*p,int id,int slot){p->prefix[slot]=id;T({3,1,slot,id});}void ITEMS_AssignSuffix(D2UnitStrc*p,int id,int slot){p->suffix[slot]=id;T({3,0,slot,id});}
void ITEMS_AssignRarePrefix(D2UnitStrc*p,int id){p->rp=id;T({4,1,id});}void ITEMS_AssignRareSuffix(D2UnitStrc*p,int id){p->rs=id;T({4,0,id});}
void ITEMS_SetFileIndex(D2UnitStrc*p,int id){p->file=id;T({5,id});}
void ITEMS_SetItemStartSeed(D2UnitStrc*p,uint32_t seed){p->start=seed;T({6,(int)seed});}
int ITEMS_CanBeMagic(D2UnitStrc*){return policy&1;}int ITEMS_IsQuestItem(D2UnitStrc*){return policy&2;}int ITEMS_IsMagSetRarUniCrfOrTmp(D2UnitStrc*p){return p->quality>=4&&p->quality<=9;}
int ITEMS_CanBeRare(D2UnitStrc*){return policy&8;}int ITEMS_GetUniqueColumnFromItemsTxt(D2UnitStrc*){return policy&16;}int ITEMS_CanBeNormal(D2UnitStrc*){return policy&32;}int ITEMS_HasDurability(D2UnitStrc*){return policy&64;}
constexpr int STAT_DURABILITY=72,STAT_MAXDURABILITY=73;
int STATLIST_UnitGetStatValue(D2UnitStrc*p,int stat,int layer){assert(layer==0);return p->stats[stat]+(stat==72?7:0);}int STATLIST_GetUnitBaseStat(D2UnitStrc*p,int stat,int layer){assert(layer==0);return p->stats[stat];}
void STATLIST_SetUnitStat(D2UnitStrc*p,int stat,int value,int layer){assert(layer==0);p->stats[stat]=value;T({7,stat,value});}
int ITEMS_GetItemLevel(D2UnitStrc*){return drop.nItemLvl;}
int ITEMS_HasUniqueBeenDroppedAlready(D2GameStrc*,int id){T({8,id});return droppedMask&(1<<id);}
D2UniqueItemsTxt*ITEMS_GetUniqueItemsTxtRecord(int id){T({9,id});return id>=0&&id<tables.nUniqueItemsTxtRecordCount&&(lookupMask&(1<<id))?tables.pUniqueItemsTxt+id:nullptr;}
int ITEMS_CanUniqueItemBeDropped(D2GameStrc*,D2UnitStrc*){T({10});return canDrop;}
void ITEMS_SetItemFlag(D2UnitStrc*p,int flag,int value){assert(flag==16);if(value)p->flags|=flag;else p->flags&=~flag;T({11,value});}
D2MagicAffixTxt*DATATBLS_GetMagicAffixTxtRecord(int id){T({12,id});affix.id=id;return id>0?&affix:nullptr;}
void ITEMMODS_AssignProperty(int mode,void*,D2UnitStrc*p,D2MagicAffixTxt*r,int,int){T({14,mode,r?r->id:0});if(mode==3||r)p->stats[7]+=(mode==3?17:11)*256;}
void ITEMS_MakeEthereal(D2UnitStrc*p,D2ItemDropStrc*){T({15});p->flags|=128;if(after==2){p->quality=7;p->format=0;}if(after==4)p->quality=0;}
void sub_6FC4D6B0(D2GameStrc*,D2UnitStrc*p,D2ItemDropStrc*){T({16});if(after==3)p->quality=9;}
int16_t ITEMS_RollMagicAffixes(D2UnitStrc*,int a,int b,int c,int d,int e,int f){T({17,a,b,c,d,e,f});return autoResult;}
int32_t ITEMS_RollLimitedRandomNumber(D2SeedStrc*,int);
int G(D2UnitStrc*,int);
int sub_6FC55270(D2UnitStrc*p,D2ItemDropStrc*){return G(p,1);}int sub_6FC54690(D2UnitStrc*p,D2ItemDropStrc*){return G(p,3);}int D2GAME_RollRareItem_6FC53360(D2UnitStrc*p,D2ItemDropStrc*){return G(p,6);}int sub_6FC4D5E0(D2UnitStrc**p,D2ItemDropStrc*){return G(*p,4);}int sub_6FC53CD0(D2UnitStrc*p,D2ItemDropStrc*){return G(p,8);}int sub_6FC542C0(D2UnitStrc*p,D2ItemDropStrc*){return G(p,5);}
int16_t ITEMS_RollTemperedItemAffix(D2UnitStrc*p,int prefix){int q=prefix?10:11;return G(p,q)?q+600:0;}
void sub_6FC4DC20(D2GameStrc*,D2UnitStrc*p,D2ItemDropStrc*){G(p,2);}
void D2GAME_ITEMS_AssignSpecial_6FC4DB60(D2GameStrc*,D2UnitStrc*p,D2ItemDropStrc*){G(p,20);}
void sub_6FC4D900(D2GameStrc*,D2UnitStrc*p,D2ItemDropStrc*){G(p,21);}
'''
 cpp=cpp.replace('uint32_t gdwBitMasks[32];',type_enum+'\nuint32_t gdwBitMasks[32];')
 order=['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber','SEED_InitLowSeed','SEED_GetLowSeed','ITEMS_GetItemSeed','ITEMS_RollItemQuality','sub_6FC4E1A0','sub_6FC4E1F0','sub_6FC4E430','sub_6FC4C5F0_End','sub_6FC4C5F0']
 cpp+='\n'.join(bodies[n]for n in order)+r'''
int32_t ITEMS_RollLimitedRandomNumber(D2SeedStrc*p,int bound){auto v=SEED_RollLimitedRandomNumber(p,bound);T({0,bound,(int)v});return v;}
int G(D2UnitStrc*p,int q){int result=(success&(1<<q))!=0;T({13,q,result});for(int i=0;i<q%3+1;i++)ITEMS_RollLimitedRandomNumber(ITEMS_GetItemSeed(p),1000);p->prefix[0]=200+q;p->suffix[0]=300+q;p->rs=400+q;p->file=500+q;p->stats[7]+=q*256;if(after==1)p->quality=0;return result;}
'''
 cpp+='D2ItemRatioTxt ratios[]={'+','.join('{'+','.join(map(str,r))+'}'for r in ratios)+'};\n'
 cpp+='D2UniqueItemsTxt uniqueRows[5][4]={'+','.join('{'+','.join('{'+','.join(map(str,(r[0],r[3],r[1]|(r[2]<<3),r[4],r[5])))+'}'for r in rows)+'}'for rows in profiles)+'};\nint uniqueCounts[]={'+','.join(map(str,map(len,profiles)))+'};\n'
 cpp+='struct Config{int entry,format,requested,policy,mask,profile,preferred,dropped,lookup,canDrop,aut,autoResult,after,ratio,level,baselevel;uint64_t seed;};\nConfig cases[]={\n'
 for row in cases:cpp+='{'+','.join(map(str,row[:16]))+','+row[16]+'ULL},\n'
 cpp+=r'''};
void array(int*a,int count){printf("[");for(int i=0;i<count;i++)printf("%s%d",i?",":"",a[i]);printf("]");}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool comma=false;for(auto c:cases){item={};item.pSeed.lSeed=c.seed^0x123456789ABCDEF0ULL;item.data.pSeed.lSeed=c.seed;item.quality=c.requested;item.format=c.format;item.dwUnitType=c.policy&1024?0:4;policy=c.policy;success=c.mask;lookupMask=c.lookup;droppedMask=c.dropped;canDrop=c.canDrop;autoResult=c.autoResult;after=c.after;drop={c.requested,c.level,c.preferred,(uint32_t)(c.policy&8192?64:0),(c.policy&128)!=0};basis={42,(c.policy&4)!=0,c.baselevel,(int16_t)c.aut};game.nGameType=(c.policy&256)!=0;tables.nUniqueItemsTxtRecordCount=uniqueCounts[c.profile];tables.pUniqueItemsTxt=uniqueRows[c.profile];ratio=ratios[c.ratio];trace.clear();D2UnitStrc*ptr=policy&512?nullptr:&item;uint32_t ref=SEED_GetLowSeed(&item.data.pSeed);int result=1;
switch(c.entry){case 0:result=sub_6FC4C5F0(&game,&ptr,&drop);break;case 1:result=ITEMS_RollItemQuality(&item,&drop);break;case 2:result=sub_6FC4C5F0_End(&game,&item,&drop,&basis);break;case 3:sub_6FC4E1F0(&game,&drop,&ptr,ref);break;case 4:result=sub_6FC4E430(&game,&drop,&ptr,&ref);break;case 5:sub_6FC4E1A0(&ptr);break;}
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,",comma?",":"",result,item.quality,drop.nQuality,item.file,item.aut,item.flags,item.rp,item.rs);comma=true;array(item.prefix,3);printf(",");array(item.suffix,3);printf(",\"%u\",\"%llu\",\"%llu\",[",item.start,(unsigned long long)item.data.pSeed.lSeed,(unsigned long long)item.pSeed.lSeed);bool sc=false;for(auto [stat,value]:item.stats){printf("%s[%d,%d]",sc?",":"",stat,value);sc=true;}printf("],[");bool tc=false;for(auto t:trace){printf("%s",tc?",":"");tc=true;array(t.data(),t.size());}printf("],\"%u\",%d]",ref,item.format);
}printf("]\n");}
'''
 for n,b in bodies.items():assert function(cpp,n)==b,n
 generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with (STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
 results=json.loads(run.stdout);assert len(results)==len(cases);assert all(int(row[12])==(int(c[16])^0x123456789ABCDEF0)for c,row in zip(cases,results))
 output=STAGE/'cases.json';output.write_text(json.dumps(dict(ratios=ratios,profiles=profiles,inputs=cases,outputs=results),separators=(',',':'))+'\n')
 receipt=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,inputs=inputs,completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},cases=len(cases),sanitizers='ASan+UBSan clean',
  scope='Six entry shapes execute complete outer quality generation,quality roll,finalizer,normal/superior fallback and metadata reset with complete property-seed getters/reset/LCG. Ordered actions and all state,including low-seed continuation,partial nested mutations and untouched unit RNG,are emitted.',
  services=['Synthetic ratio and unique tables are explicit inputs,not modern table certification. Nested quality generators deliberately mutate metadata/HP and consume declared item RNG even on failure;this checks outer cleanup/ordering,not those complete quality bodies.',
   'Ethereal/normal finalization,automagic selection,unique world/drop eligibility,property lookup/application and item metadata are declared services. The original item identity stays stable,as in the already reviewed magic/rare outer helpers. Extreme signed overflow or candidate-buffer exhaustion is outside these bounded contexts.'],
  remaining=['Compose full current tables/pickers/property writers,superior/normal/ethereal/staffmod/automagic bodies,complete item initialization/materialization,NPC equipment graphs/actors and actual lifecycle/input/saves. No class or learned handler is completed by this source proof.'],
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in (generated,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',output,Path(__file__).resolve())})
 target=ROOT/'tools/reference/item-quality-source-evidence.json';encoded=json.dumps(receipt,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Item quality source proof stale'
 else:target.write_text(encoded)
 print('Item quality:',len(cases),'complete source controls across6entries/11routines;ASan+UBSan clean,unit RNG unchanged.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
