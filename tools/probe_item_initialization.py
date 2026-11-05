#!/usr/bin/env python3
"""Execute complete pinned base-stat initializer and stack/seed dependencies."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'item-initialization-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={};bodies={}
 def source(relative):
  p=BASE/'d2moo-pinned-source'/relative;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==relative);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 for rel,names in {'source/D2Game/src/ITEMS/Items.cpp':['D2GAME_InitItemStats_6FC4E520'],'source/D2Common/src/Items/Items.cpp':['ITEMS_GetItemSeed','ITEMS_GetTotalMaxStack','ITEMS_GetMinStackFromItemId','ITEMS_GetMinStack','ITEMS_GetSpawnStackFromItemId','ITEMS_GetSpawnStack'],'source/D2Common/include/D2Seed.h':['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber']}.items():
  raw=source(rel)
  for n in names:bodies[n]=function(raw,n)
 # Pin the declarations inspected for the two type branches and all stat writes.
 header=source('source/D2Common/include/D2Items.h');assert header.index('ITEMTYPE_GOLD')<header.index('ITEMTYPE_ELIXIR')
 source('source/D2Common/include/D2StatList.h')
 ref=ROOT/'tools/reference/d2r-named-equipment';meta=json.loads((ref/'sources.json').read_text());tables={}
 for n in ('itemtypes','weapons','armor','misc'):
  p=ref/'json'/(n+'.json');assert sha(p)==meta['files']['json/'+n+'.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);tables[n]=json.loads(p.read_text())
 types={r['Code']:r for r in tables['itemtypes'].values()if r.get('Code')}
 assert types['gold']['lineNumber']==4 and types['elix']['lineNumber']==11
 def isa(t,p):return t==p or any(isa(types[t][k].strip()[:4],p)for k in ('Equiv1','Equiv2')if types[t].get(k))
 descriptors=[]
 for code,r in sorted({r['code']:r for n in ('weapons','armor','misc')for r in tables[n].values()if r.get('code')}.items()):
  get=lambda k:r.get(k,0);t=types[r['type']]
  b=dict(zip(('Block','Speed','Durability','MinArmor','MaxArmor','Minimum','Maximum','SecondaryMinimum','SecondaryMaximum','ThrowMinimum','ThrowMaximum'),[get(k)for k in ('block','speed','durability','minac','maxac','mindam','maxdam','2handmindam','2handmaxdam','minmisdam','maxmisdam')]))
  b.update(Quest=bool(get('quest')),QuestDifficulty=bool(get('questdiffcheck')))
  descriptors.append(dict(Code=code,Base=b,Type=t['lineNumber'],Quiver=bool(t.get('Quiver')),Armor=isa(r['type'],'armo'),Weapon=isa(r['type'],'weap'),Stackable=bool(get('stackable')),MinStack=get('minstack'),BaseMaxStack=get('maxstack'),ExtraStack=0,SpawnStack=get('spawnstack'),Varieties=t.get('VarInvGfx',0),Throwable=bool(t.get('Throwable'))))
 assert len(descriptors)==692 and all(0<=d['Base']['Durability']<=255 for d in descriptors)
 by={d['Code']:d for d in descriptors};synthetic=[]
 for code in ('gld','aqv','qui','jav','tbk','elx','amu'):
  assert code in by,code
  synthetic.append(json.loads(json.dumps(by[code])))
 # Exercise zero RNG bounds,spawn fallback,throw-min suppression,
 # armor priority,255 durability,all quest flag pairs and an existing list.
 for i,d in enumerate(synthetic):
  d['Code']='synthetic-'+str(i);d['Base'].update(Quest=True,QuestDifficulty=True,Durability=255,Speed=-20)
  d['Varieties']=3
  if d['Stackable']:d.update(MinStack=7,BaseMaxStack=7,SpawnStack=0)
 for quest in ((False,False),(True,False),(False,True)):
  d=json.loads(json.dumps(by['qui']));d['Code']='quest-'+str(len(synthetic));d['Base'].update(Quest=quest[0],QuestDifficulty=quest[1]);synthetic.append(d)
 d=json.loads(json.dumps(by['jav']));d.update(Code='synthetic-stack-cap',MinStack=510,BaseMaxStack=500,ExtraStack=70,SpawnStack=505);d['Base'].update(Durability=0,ThrowMinimum=9,ThrowMaximum=0,Quest=True,QuestDifficulty=True);synthetic.append(d)
 d=json.loads(json.dumps(by['qui']));d.update(Code='synthetic-priority',Weapon=True,Stackable=True);d['Base'].update(Durability=1,Quest=True,QuestDifficulty=True);synthetic.append(d)
 for extra in (70,600):
  d=json.loads(json.dumps(by['jav']));d.update(Code='current-jav-extra-'+str(extra),ExtraStack=extra);synthetic.append(d)
 descriptors+=synthetic
 seeds=[0,((666<<32)|511),(1<<64)-1]
 cases=[]
 scenarios=[(1,0,0,0,0,1,1,0,1),(1,0,0,0,1,1,1,0,1),(99,77,0,0,1,1,1,0,1),(99,0,1,0,1,1,1,0,1),(99,77,1,0,1,1,1,0,1),(0,-1,0,0,1,1,1,0,1),(99,0,0,1,1,1,1,0,1),(99,0,0,1,1,1,1,1,1)]
 # Inputs:descriptor,seed,level,override,magic,generate,drop,result,itemAfter,recordAfter,listInitially,allocationSuccess.
 for i in range(692):
  for seed in seeds:
   for level,qty,magic,generate,drop,after,record,lst,alloc in scenarios:cases.append([i,str(seed),level,qty,magic,generate,drop,7 if lst else 0,after,record,lst,alloc])
 for i in range(692,len(descriptors)):
  for seed in seeds:
   for generate in (0,1):
    for drop in (0,1):
     for after in (0,1):
      for record in (0,1):
       for lst in (0,1):
        for alloc in (0,1):cases.append([i,str(seed),99,77,1,generate,drop,7,after,record,lst,alloc])
 for row in cases:row.append(str(seeds[(seeds.index(int(row[1]))+1)%3]))
 cpp=r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <map>
#include <vector>
#define __fastcall
#define __stdcall
#define D2_ASSERT(x) assert(x)
constexpr unsigned long long operator""i64(unsigned long long n){return n;}
constexpr uint8_t operator""ui8(unsigned long long n){return n;}
constexpr int UNIT_ITEM=4,ITEMTYPE_GOLD=4,ITEMTYPE_ELIXIR=11,ITEMTYPE_ANY_ARMOR=50,ITEMTYPE_WEAPON=45,IFLAG_IDENTIFIED=16;
constexpr int STAT_GOLD=14,STAT_TOBLOCK=20,STAT_VELOCITYPERCENT=67,STAT_DURABILITY=72,STAT_MAXDURABILITY=73,STAT_ARMORCLASS=31,STAT_QUANTITY=70,STAT_MAXDAMAGE=22,STAT_MINDAMAGE=21,STAT_SECONDARY_MINDAMAGE=23,STAT_SECONDARY_MAXDAMAGE=24,STAT_ITEM_THROW_MINDAMAGE=159,STAT_ITEM_THROW_MAXDAMAGE=160,STAT_ATTACKRATE=68,STAT_QUESTITEMDIFFICULTY=356,STAT_ITEM_EXTRA_STACK=254;
union D2SeedStrc{uint64_t lSeed;struct{uint32_t nLowSeed,nHighSeed;};};
struct D2StatListStrc{std::map<int,int>v;};
struct D2ItemsTxt{int nBlock=0,dwSpeed=0;uint8_t nDurability=0;int dwMinAc=0,dwMaxAc=0,nMinDam=0,nMaxDam=0,n2HandMinDam=0,n2HandMaxDam=0,nMinMisDam=0,nMaxMisDam=0,nQuest=0,nQuestDiffCheck=0,dwMinStack=0,dwMaxStack=0,dwSpawnStack=0;};
struct D2ItemDataStrc{D2SeedStrc pSeed;};
struct D2UnitStrc{int dwClassId=1,dwUnitType=4,dwUnitId=17;void*pMemoryPool=nullptr;D2SeedStrc pSeed;D2ItemDataStrc data;std::map<int,int>v;}item;
struct D2ItemDropStrc{int nItemLvl=0,nQtyOverride=0;}drop;
struct D2GameStrc{int nDifficulty=2;}game;
struct Facts{D2ItemsTxt b;int type,quiver,armor,weapon,stackable,varieties,extra;};
Facts facts;D2StatListStrc magic;D2UnitStrc*active=nullptr;bool hasRecord=true,list=false,magicBetter=false,itemAfter=true,recordAfter=true,allocation=true;int qualityResult=0,gfx=-1,elixirs=0,identified=0;
std::vector<std::vector<int>>trace;
void T(std::initializer_list<int> v){trace.emplace_back(v);}
D2ItemDataStrc*ITEMS_GetItemData(D2UnitStrc*p){return &p->data;}
D2SeedStrc*ITEMS_GetItemSeed(D2UnitStrc*);
D2ItemsTxt*DATATBLS_GetItemsTxtRecord(int id){return id<0||!hasRecord?nullptr:&facts.b;}
int ITEMS_GetItemType(D2UnitStrc*){return facts.type;}int ITEMS_GetQuiverType(D2UnitStrc*){return facts.quiver;}
bool ITEMS_CheckItemTypeId(D2UnitStrc*,int type){return type==ITEMTYPE_ANY_ARMOR?facts.armor:facts.weapon;}
bool ITEMS_CheckIfStackable(D2UnitStrc*){return facts.stackable;}bool ITEMS_IsMagSetRarUniCrfOrTmp(D2UnitStrc*){return magicBetter;}
int ITEMS_GetVarInvGfxCount(D2UnitStrc*){return facts.varieties;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int layer){assert(stat==STAT_ITEM_EXTRA_STACK&&layer==0);return facts.extra;}
void STATLIST_SetUnitStat(D2UnitStrc*p,int stat,int value,int layer){assert(layer==0);p->v[stat]=value;T({1,stat,value});}
void D2GAME_SetStatOrResetGold_6FC7CA70(D2UnitStrc*p,int stat,int value){assert(stat==14);p->v[stat]=value;T({2,value});}
void ITEMMODS_SetRandomElixirFileIndex(D2UnitStrc*){elixirs++;T({3});}
void ITEMS_SetVarGfxIndex(D2UnitStrc*,int value){gfx=value;T({4,value});}
int sub_6FC4C5F0(D2GameStrc*,D2UnitStrc**p,D2ItemDropStrc*){T({5,qualityResult});SEED_RollRandomNumber(ITEMS_GetItemSeed(&item));if(!itemAfter)*p=nullptr;hasRecord=recordAfter;return qualityResult;}
D2StatListStrc*STATLIST_GetStatListFromUnitStateOrFlag(D2UnitStrc*,int state,unsigned flags){assert(state==0&&flags==64);return list?&magic:nullptr;}
D2StatListStrc*STATLIST_AllocStatList(void*,unsigned flags,int state,int kind,int id){assert(flags==64&&state==0&&kind==4);T({6,id});list=allocation;return list?&magic:nullptr;}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*p,int apply){assert(apply==1);T({7,p!=nullptr});}
void STATLIST_SetStatIfListIsValid(D2StatListStrc*p,int stat,int value,int layer){assert(layer==0);T({8,stat,value});if(p)p->v[stat]=value;}
void ITEMS_SetItemFlag(D2UnitStrc*,int flag,int value){assert(flag==16&&value==1);identified=1;T({9});}
'''
 # Seed functions precede the quality service which consumes an explicit draw.
 cpp=cpp.replace('int sub_6FC4C5F0(', '\n'+bodies['SEED_RollRandomNumber']+'\n'+bodies['SEED_RollLimitedRandomNumber']+'\nint sub_6FC4C5F0(',1)
 cpp+='\nint ITEMS_GetMinStack(D2UnitStrc*);int ITEMS_GetTotalMaxStack(D2UnitStrc*);int ITEMS_GetSpawnStack(D2UnitStrc*);\nuint32_t ITEMS_RollLimitedRandomNumber(D2SeedStrc*p,int bound){auto v=SEED_RollLimitedRandomNumber(p,bound);T({0,bound,(int)v});return v;}\n'
 for n,b in bodies.items():
  if not n.startswith('SEED_'):cpp+=b+'\n'
 cpp+='Facts descriptors[]={\n'
 for d in descriptors:
  b=d['Base'];vals=[b[k]for k in ('Block','Speed','Durability','MinArmor','MaxArmor','Minimum','Maximum','SecondaryMinimum','SecondaryMaximum','ThrowMinimum','ThrowMaximum','Quest','QuestDifficulty')]+[d[k]for k in ('MinStack','BaseMaxStack','SpawnStack')]
  fields=[d[k]for k in ('Type','Quiver','Armor','Weapon','Stackable','Varieties','ExtraStack')]
  cpp+='{{'+','.join(str(int(v))for v in vals)+'},'+','.join(str(int(v))for v in fields)+'},\n'
 cpp+='};\nstruct Input{int basis;uint64_t seed;int level,qty,mag,generate,drop,result,after,record,lst,alloc;uint64_t itemSeed;};\nInput cases[]={\n'
 for r in cases:cpp+='{'+str(r[0])+','+r[1]+'ULL,'+','.join(map(str,r[2:12]))+','+r[12]+'ULL},\n'
 cpp+=r'''};
void stats(const std::map<int,int>&m){printf("[");bool comma=false;for(auto [s,v]:m){printf("%s[%d,%d]",comma?",":"",s,v);comma=true;}printf("]");}
int main(){printf("[");bool comma=false;for(auto c:cases){facts=descriptors[c.basis];item={};item.pSeed.lSeed=c.seed;item.data.pSeed.lSeed=c.itemSeed;if(facts.extra)item.v[254]=facts.extra;active=&item;hasRecord=true;list=c.lst;magic.v.clear();magicBetter=c.mag;itemAfter=c.after;recordAfter=c.record;allocation=c.alloc;qualityResult=c.result;gfx=-1;elixirs=identified=0;trace.clear();drop={c.level,c.qty};
int result=D2GAME_InitItemStats_6FC4E520(&game,&active,c.drop?&drop:nullptr,c.generate);printf("%s[%d,\"%llu\",",comma?",":"",result,(unsigned long long)item.pSeed.lSeed);comma=true;stats(item.v);printf(",%d,%d,",gfx,elixirs);stats(magic.v);printf(",%d,%d,[",list,identified);bool tc=false;for(auto t:trace){printf("%s[",tc?",":"");tc=true;for(size_t j=0;j<t.size();j++)printf("%s%d",j?",":"",t[j]);printf("]");}D2SeedStrc next=*ITEMS_GetItemSeed(&item);auto hp=SEED_RollLimitedRandomNumber(&next,5)+1;printf("],%d,\"%llu\",%u,\"%llu\"]",active!=nullptr,(unsigned long long)next.lSeed,hp,(unsigned long long)item.data.pSeed.lSeed);
}printf("]\n");}
'''
 for n,b in bodies.items():assert function(cpp,n)==b,n
 generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with (STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
 results=json.loads(run.stdout);assert len(results)==len(cases)
 path=STAGE/'cases.json';path.write_text(json.dumps(dict(descriptors=descriptors,inputs=cases,outputs=results),separators=(',',':'))+'\n')
 receipt=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,inputs=inputs,completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},currentBases=692,syntheticBases=len(synthetic),cases=len(cases),sanitizers='ASan+UBSan clean',
  scope='Distinct unit/base and item-property RNG streams are bound through the complete item-seed getter. Complete base-stat initializer and five current stack getters execute with complete source LCG;current raw bases/type facts and synthetic quantity/durability/quest/callback cases. Ordered writes,bounded calls,quality callback and quest-list/identification services are emitted.',
  services=['Unit/base RNG and item-data/property RNG are initialized to distinct pairs in every case. The complete item-seed getter selects the property stream;the quality mutation service and subsequent property draw leave unit/base RNG separate.',
   'Type hierarchy,quiver,variation and raw item tables are supplied from pinned current JSON. Gold and elixir side effects are declared services,not their full implementations. Quality is an explicit one-draw mutation callback,not the full quality factory. Quest allocation/post/set helpers are declared services,including allocation refusal.',
   'An item removed by the quality callback makes subsequent base lookup absent. No fixture invents a valid table record for sentinel class-1. Getter-only lookups are not included in the ordered action trace.'],
  remaining=['Full quality/fallback,automagic/staffmods/ethereality/finalization,native appearance/materialization,NPC graph attachment and summon actors/AI/input/saves remain separate requirements. No learned handler is enabled by this source evidence.'],
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in (generated,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',path,Path(__file__).resolve())})
 target=ROOT/'tools/reference/item-initialization-source-evidence.json';encoded=json.dumps(receipt,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Item initialization source evidence stale'
 else:target.write_text(encoded)
 print('Item initialization:',len(cases),'complete source controls,692current bases,',len(synthetic),'synthetic bases;ASan+UBSan clean.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
