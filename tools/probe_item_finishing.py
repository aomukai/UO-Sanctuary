#!/usr/bin/env python3
"""Execute complete physical quality,normal routing,socket and ethereal bodies."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'item-finishing-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={};bodies={}
 def read(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 for rel,names in {'source/D2Game/src/ITEMS/Items.cpp':['sub_6FC4D6B0','ITEMS_MakeEthereal','sub_6FC4D900','D2GAME_ITEMS_AssignSpecial_6FC4DB60','sub_6FC4DC20'],'source/D2Game/src/ITEMS/ItemsMagic.cpp':['sub_6FC54690','sub_6FC549F0','sub_6FC54D00','sub_6FC55270'],'source/D2Common/src/Items/ItemMods.cpp':['ITEMMODS_CanItemBeHighQuality','ITEMMODS_ApplyEthereality'],'source/D2Common/src/Items/Items.cpp':['ITEMS_GetItemSeed'],'source/D2Common/include/D2Seed.h':['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber']}.items():
  text=read(rel)
  for n in names:bodies[n]=function(text,n)
 header=read('source/D2Common/include/D2Items.h');enums='\n'.join(header[header.index('enum '+n):header.index('};',header.index('enum '+n))+2]for n in ('D2C_ItemTypes','D2C_ItemDropFlags','D2C_ItemQualities')) if 'enum D2C_ItemQualities' in header else ''
 # Quality enum is named D2C_ItemQuality in some source revisions.
 if not enums:
  names=('D2C_ItemTypes','D2C_ItemDropFlags');enums='\n'.join(header[header.index('enum '+n):header.index('};',header.index('enum '+n))+2]for n in names)
  enums+='\nenum{ITEMQUAL_INFERIOR=1,ITEMQUAL_SET=5};\n'
 catalog=ROOT/'data/Sanctuary/Balance/item-finishing-catalogue.json';data=json.loads(catalog.read_text());inputs[str(catalog.relative_to(ROOT))]=sha(catalog)
 basespath=ROOT/'data/Sanctuary/Balance/npc-item-affix-catalogue.json';cat=json.loads(basespath.read_text());inputs[str(basespath.relative_to(ROOT))]=sha(basespath)
 types={r['code']:r for r in cat['types']};meta={r['code']:r for r in data['types']}
 def isa(t,p):return t==p or any(isa(x,p)for x in types[t]['parents'])
 rows=data['superior'];cases=[];seeds=[(666<<32),(666<<32)|511,(1<<64)-1]
 # Input:entry,format,quality,type,traits,flags1,flags2,difficulty,maxSockets,start,count,recordMask,durability,selector,propertySeed,unitSeed.
 def case(entry=0,fmt=100,q=3,kind=30,traits=1|32|64,f1=0,f2=0,diff=0,maxsock=6,start=511,count=8,mask=4095,dur=45,selected=0,seed=None):
  seed=seeds[0]if seed is None else seed;cases.append([entry,fmt,q,kind,traits,f1,f2,diff,maxsock,start,count,mask,dur,selected,str(seed),str(seed^0x123456789ABCDEF0)])
 for b in cat['bases']:
  raw=b['sourceFields'];t=b['type'];traits=int(isa(t,'weap'))|int(isa(t,'armo'))*2|int(isa(t,'tors'))*4|int(isa(t,'helm'))*8|int(next(r['throwable']for r in cat['rareBases']if r['code']==b['code']))*16
  for selected in range(8):case(entry=9,kind=meta[t]['ordinal'],traits=traits,selected=selected)
 physical=[(30,1|32|64),(44,1|16|32|128),(3,2|4|32|64),(37,2|8|32|64),(52,0),(24,1|32|64),(25,1|32|64),(27,1|64|512),(2,2|32|64)]
 for entry in (0,1,2,3):
  for kind,traits in physical:
   for fmt in (0,1,100):
    for missing in (0,1024,2048,4096,8192):
     for dur in (0,1,45,255):
      for seed in seeds:case(entry=entry,kind=kind,traits=traits|missing,fmt=fmt,dur=dur,seed=seed)
 for kind,traits in physical:
  for count in (0,1,4,8,12):
   for mask in (0,1,15,4095):
    for seed in seeds:case(count=count,mask=mask,kind=kind,traits=traits,seed=seed)
 for entry in (4,5):
  for kind,traits in physical:
   for fmt in (0,1,99,100):
    for q in (1,2,3,5,7):
     for f2 in (0,2,4,8,16,2|4,8|16):
      for seed in seeds:case(entry=entry,kind=kind,traits=traits,fmt=fmt,q=q,f1=0x400800 if seed==seeds[2]else 0,f2=f2,diff=int(seed%3),maxsock=int(seed%9),seed=seed)
 for entry in (6,7,8):
  for kind in (13,40,7,22,18,30,3,37):
   for fmt in (0,1,100):
    for drop in (0,1):
     for seed in seeds:case(entry=entry,kind=kind,traits=(1|32|64 if kind==30 else 2|32|64 if kind in(3,37)else 0)|(16384 if drop else 0),fmt=fmt,selected=77,seed=seed)
 control_count=len(cases);native_codes=[]
 bybase={b['code']:b for b in cat['bases']};rare={r['code']:r for r in cat['rareBases']}
 for code in ('ssd','qui','jav','cap','buc','rin','amu','hxb','spc','hax','lbt','lgl'):
  b=bybase[code];raw=b['sourceFields'];kind=meta[b['type']]['ordinal'];ty=meta[b['type']];assert not ty['staffMods']
  traits=int(isa(b['type'],'weap'))|int(isa(b['type'],'armo'))*2|int(isa(b['type'],'tors'))*4|int(isa(b['type'],'helm'))*8|int(rare[code]['throwable'])*16|int(not raw.get('nodurability')and raw.get('durability',0)>0)*32|int(bool(raw.get('hasinv')))*64|int(bool(raw.get('stackable')))*128|int(bool(raw.get('quest')))*256|int(bool(raw.get('nodurability')))*512|32768
  maximum=min(ty['maxSockets1'],raw.get('gemsockets',0))
  for entry in (0,3,4,5):
   for seed in seeds[:2]:
    case(entry=entry,kind=kind,traits=traits,q=1 if entry==3 else 3,f2=12,maxsock=maximum,dur=raw.get('durability',0),seed=seed)
    native_codes.append(code)
 cpp=r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#include <map>
#define __fastcall
#define __stdcall
#define D2_ASSERT(x) assert(x)
#define D2_MAYBE_UNUSED(x) ((void)x)
#define ARRAY_SIZE(x) (sizeof(x)/sizeof((x)[0]))
#define BOOL int
#define TRUE 1
#define FALSE 0
constexpr unsigned long long operator""i64(unsigned long long n){return n;}
constexpr int UNIT_ITEM=4,DIFFMODE_NORMAL=0,DIFFMODE_NIGHTMARE=1,DIFFMODE_HELL=2;
constexpr uint32_t IFLAG_SOCKETED=0x800,IFLAG_ISEAR=0x10000,IFLAG_ETHEREAL=0x400000;
constexpr int STAT_DURABILITY=72,STAT_MAXDURABILITY=73,STAT_MINDAMAGE=21,STAT_MAXDAMAGE=22,STAT_SECONDARY_MINDAMAGE=23,STAT_SECONDARY_MAXDAMAGE=24,STAT_ITEM_THROW_MINDAMAGE=159,STAT_ITEM_THROW_MAXDAMAGE=160,STAT_ARMORCLASS=31;
'''+enums+r'''
union D2SeedStrc{uint64_t lSeed;struct{uint32_t nLowSeed,nHighSeed;};};
struct D2ItemDataStrc{D2SeedStrc pSeed;};
struct D2UnitStrc{int dwClassId=1;D2SeedStrc pSeed;D2ItemDataStrc data;std::map<int,int>stats={{21,11},{22,27},{23,0},{24,0},{159,0},{160,0},{31,51},{72,17},{73,23}};int file=-1,suffix=-1,staff=0,charm=0,properties=0,sockets=0;uint32_t flags=0;}item,dropUnit;
struct D2GameStrc{int nDifficulty=0;}game;
struct D2ItemDropStrc{uint32_t dwFlags1=0,dwFlags2=0;int nItemIndex=77;D2UnitStrc*pUnit=nullptr;}drop;
struct D2ItemsTxt{int nNoDurability=0,nDurability=45;}basis;
struct D2QualityItemsTxt{int nArmor,nWeapon,nShield,nScepter,nWand,nStaff,nBow,nBoots,nGloves,nBelt,row;};
struct D2ItemTypeCheckStrc{int bItemType,nItemType;};
struct D2QualityItemDataTbl{int nQualityItemsTxtRecordCount=8;}highTable;
struct D2LowQualityItemDataTbl{int nLowQualityItemsTxtRecordCount=4;}lowTable;
int kind=30,traits=97,format=100,quality=3,recordMask=4095;uint32_t maxSockets=6,startSeed=511;std::vector<std::vector<int>>trace;
void T(std::initializer_list<int>v){trace.emplace_back(v);}
D2ItemDataStrc*ITEMS_GetItemData(D2UnitStrc*p){return &p->data;}
D2ItemsTxt*DATATBLS_GetItemsTxtRecord(int id){return id<0||traits&2048?nullptr:&basis;}
D2QualityItemDataTbl*DATATBLS_GetQualityItemDataTables(){return traits&4096?nullptr:&highTable;}
D2LowQualityItemDataTbl*DATATBLS_GetLowQualityItemDataTables(){return traits&8192?nullptr:&lowTable;}
int ITEMS_GetItemType(D2UnitStrc*){return kind;}int ITEMS_GetItemQuality(D2UnitStrc*){return quality;}uint32_t ITEMS_GetItemFormat(D2UnitStrc*){return format;}
int ITEMS_CheckItemTypeId(D2UnitStrc*,int type){if(type==45)return traits&1;if(type==50)return traits&2;if(type==3)return traits&4;if(type==37)return traits&8;return kind==type;}
int ITEMS_CheckIfThrowable(D2UnitStrc*){return traits&16;}int ITEMS_HasDurability(D2UnitStrc*){return traits&32;}int ITEMS_CheckIfSocketable(D2UnitStrc*){return traits&64;}int ITEMS_CheckIfStackable(D2UnitStrc*){return traits&128;}int ITEMS_IsQuestItem(D2UnitStrc*){return traits&256;}
uint32_t ITEMS_GetMaxSockets(D2UnitStrc*){return maxSockets;}uint32_t ITEMS_GetItemStartSeed(D2UnitStrc*){return startSeed;}
void ITEMS_SetFileIndex(D2UnitStrc*p,int n){p->file=n;T({2,n});}
int STATLIST_GetUnitBaseStat(D2UnitStrc*p,int stat,int layer){assert(layer==0);return p->stats[stat];}
int STATLIST_UnitGetStatValue(D2UnitStrc*p,int stat,int layer){return STATLIST_GetUnitBaseStat(p,stat,layer)+(traits&32768?0:stat==73?4:stat==72?2:0);}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*p,int stat,int layer){return STATLIST_GetUnitBaseStat(p,stat,layer)+(traits&32768?0:stat==159?9:stat==160?13:0);}
void STATLIST_SetUnitStat(D2UnitStrc*p,int stat,int value,int layer){assert(layer==0);p->stats[stat]=value;T({3,stat,value});}
void ITEMS_SetItemFlag(D2UnitStrc*p,uint32_t flag,int value){p->flags=value?p->flags|flag:p->flags&~flag;T({4,(int)flag,value});}
void ITEMS_AddSockets(D2UnitStrc*p,uint32_t n){p->sockets=n;T({5,(int)n});}
void sub_6FC52410(D2UnitStrc*p,D2ItemDropStrc*){p->staff++;T({6});}
void ITEMMODS_AssignProperty(int mode,int,D2UnitStrc*p,D2QualityItemsTxt*r,int,int){assert(mode==1);p->properties+=r->row+1;T({7,r->row});}
void ITEMS_AssignCharmAffixes(D2GameStrc*,D2UnitStrc*p,D2ItemDropStrc*){p->charm++;T({8});}
int sub_6FC54240(D2UnitStrc*,int scroll){T({9,scroll});return scroll?71:88;}
void ITEMS_AssignSuffix(D2UnitStrc*p,int id,int slot){assert(slot==0);p->suffix=id;T({10,id});}
D2QualityItemsTxt rows[12];
D2QualityItemsTxt*DATATBLS_GetQualityItemsTxtRecord(int id){T({11,id});return id<0||id>=highTable.nQualityItemsTxtRecordCount||!(recordMask&(1<<id))?nullptr:rows+id;}
uint32_t ITEMS_RollLimitedRandomNumber(D2SeedStrc*,int);uint32_t ITEMS_RollRandomNumber(D2SeedStrc*);
'''
 order=['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber','ITEMS_GetItemSeed','ITEMMODS_CanItemBeHighQuality','ITEMMODS_ApplyEthereality','sub_6FC4D6B0','ITEMS_MakeEthereal','sub_6FC4D900','D2GAME_ITEMS_AssignSpecial_6FC4DB60','sub_6FC4DC20','sub_6FC54690','sub_6FC549F0','sub_6FC54D00','sub_6FC55270']
 cpp+='\n'.join(bodies[n]for n in order)+r'''
uint32_t ITEMS_RollLimitedRandomNumber(D2SeedStrc*p,int bound){auto value=SEED_RollLimitedRandomNumber(p,bound);T({0,p==&item.pSeed?1:0,bound,(int)value});return value;}
uint32_t ITEMS_RollRandomNumber(D2SeedStrc*p){auto value=(uint32_t)SEED_RollRandomNumber(p);T({1,p==&item.pSeed?1:0,(int)value});return value;}
'''
 cpp+='D2QualityItemsTxt initial[]={'+','.join('{'+','.join(str(int(r[k]))for k in ('armor','weapon','shield','scepter','wand','staff','bow','boots','gloves','belt'))+','+str(i)+'}'for i,r in enumerate(rows))+'};\n'
 cpp+='struct Config{int entry,format,quality,kind,traits;uint32_t f1,f2;int difficulty;uint32_t maxsock,start;int count,mask,durability,selector;uint64_t itemSeed,unitSeed;};\nConfig cases[]={\n'
 for r in cases:cpp+='{'+','.join(map(str,r[:14]))+','+r[14]+'ULL,'+r[15]+'ULL},\n'
 cpp+=r'''};
int main(){printf("[");bool comma=false;for(auto c:cases){item={};dropUnit={};dropUnit.dwClassId=123;item.pSeed.lSeed=c.unitSeed;item.data.pSeed.lSeed=c.itemSeed;traits=c.traits;kind=c.kind;format=c.format;quality=c.quality;recordMask=c.mask;maxSockets=c.maxsock;startSeed=c.start;highTable.nQualityItemsTxtRecordCount=c.count;basis={int((traits&512)!=0),c.durability};game.nDifficulty=c.difficulty;drop={c.f1,c.f2,77,traits&16384?&dropUnit:nullptr};for(int i=0;i<12;i++){rows[i]=i<8?initial[i]:D2QualityItemsTxt{};rows[i].row=i;}trace.clear();D2UnitStrc*p=traits&1024?nullptr:&item;int result=1;
switch(c.entry){case 0:result=sub_6FC54690(p,&drop);break;case 1:result=sub_6FC549F0(p,&drop);break;case 2:result=sub_6FC54D00(p,&drop);break;case 3:result=sub_6FC55270(p,&drop);break;case 4:sub_6FC4D6B0(&game,p,&drop);break;case 5:ITEMS_MakeEthereal(p,&drop);break;case 6:sub_6FC4D900(&game,p,&drop);break;case 7:D2GAME_ITEMS_AssignSpecial_6FC4DB60(&game,p,&drop);break;case 8:sub_6FC4DC20(&game,p,&drop);break;case 9:result=ITEMMODS_CanItemBeHighQuality(p,rows+c.selector);break;}
printf("%s[%d,%d,%d,%d,%d,%d,%d,%u,\"%llu\",\"%llu\",[",comma?",":"",result,item.file,item.suffix,item.staff,item.charm,item.properties,item.sockets,item.flags,(unsigned long long)item.data.pSeed.lSeed,(unsigned long long)item.pSeed.lSeed);comma=true;bool sc=false;for(auto [s,v]:item.stats){printf("%s[%d,%d]",sc?",":"",s,v);sc=true;}printf("],[");bool tc=false;for(auto row:trace){printf("%s[",tc?",":"");tc=true;for(size_t j=0;j<row.size();j++)printf("%s%d",j?",":"",row[j]);printf("]");}printf("]]");
}printf("]\n");}
'''
 for n,b in bodies.items():assert function(cpp,n)==b,n
 generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with (STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
 results=json.loads(run.stdout);assert len(results)==len(cases);output=STAGE/'cases.json';output.write_text(json.dumps(dict(inputs=cases[:control_count],outputs=results[:control_count],nativeInputs=cases[control_count:],nativeOutputs=results[control_count:],nativeCodes=native_codes),separators=(',',':'))+'\n')
 receipt=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,cases=control_count,nativeContexts=len(native_codes),currentEligibilityCases=5536,completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},inputs=inputs,sanitizers='ASan+UBSan clean',
  scope='Complete superior selector/eligibility,inferior core/format wrappers,socket/ethereal/normal routing and numeric ethereality with independent unit/property RNG. All692current bases versus8owned superior rows plus bounded branch contexts.',
  services=['Superior property delivery is an observed service here;approved current-table numeric compositions are proved separately. Staffmods/charm/book and actual socket allocation remain declared services,not their complete bodies.',
   'Superior count is bounded to0..12;nonzero initialized property seeds avoid the source zero-seed fixed point. Type helpers are supplied from current type hierarchy for current cases and explicit flags for synthetic controls. Extreme signed overflow remains outside the test domain.'],
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in (generated,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',output,Path(__file__).resolve())})
 target=ROOT/'tools/reference/item-finishing-source-evidence.json';encoded=json.dumps(receipt,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Finishing source proof stale'
 else:target.write_text(encoded)
 print('Item finishing:',control_count,'complete source controls/14routines;5536current eligibility pairs;ASan+UBSan clean.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
