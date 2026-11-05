#!/usr/bin/env python3
"""Compose existing affix proof with complete initialization/quality/finishing."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'item-quality-current-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(parents=True,exist_ok=True)
 inputs={};bodies={};tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def load(rel):
  p=ROOT/rel;inputs[rel]=sha(p);return json.loads(p.read_text())
 def read(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 for rel,names in {'source/D2Game/src/ITEMS/Items.cpp':['D2GAME_InitItemStats_6FC4E520','ITEMS_RollItemQuality','sub_6FC4E1A0','sub_6FC4E1F0','sub_6FC4E430','sub_6FC4D6B0','ITEMS_MakeEthereal','sub_6FC4D900','D2GAME_ITEMS_AssignSpecial_6FC4DB60','sub_6FC4DC20'],'source/D2Game/src/ITEMS/ItemsMagic.cpp':['sub_6FC54690','sub_6FC549F0','sub_6FC54D00','sub_6FC55270'],'source/D2Game/src/ITEMS/ItemMode.cpp':['sub_6FC4C5F0_End','sub_6FC4C5F0'],'source/D2Common/src/Items/ItemMods.cpp':['ITEMMODS_CanItemBeHighQuality','ITEMMODS_ApplyEthereality','ITEMMODS_PropertyFunc13'],'source/D2Common/src/D2Seed.cpp':['SEED_InitLowSeed','SEED_GetLowSeed'],'source/D2Common/src/Items/Items.cpp':['ITEMS_AllocItemData']}.items():
  text=read(rel)
  for n in names:bodies[n]=function(text,n)
 prior=load('tools/reference/current-item-affixes-source-evidence.json');path=BASE/'current-item-affixes-source/source.cpp';assert sha(path)==prior['artifacts'][str(path.relative_to(ROOT))];inputs[str(path.relative_to(ROOT))]=sha(path)
 cpp=path.read_text().split('struct Config{int level,quality;',1)[0]
 current=load('data/Sanctuary/Balance/item-quality-current.json');pool=load('data/Sanctuary/Balance/npc-item-affix-catalogue.json');finish=load('data/Sanctuary/Balance/item-finishing-catalogue.json');init=load('data/Sanctuary/Balance/item-initialization-types.json');plans=load('data/Sanctuary/Balance/equipment-property-plans.json');layout=load('data/Sanctuary/Balance/item-affix-layout.json')
 for rel in ('tools/reference/item-superior-table-decision.json','tools/reference/item-tree-packing-decision.json','tools/reference/item-staffmods-tier-decision.json'):assert load(rel)['status']=='user-approved-sanctuary-adaptation'
 typ={r['code']:r for r in pool['types']};ordinals={r['code']:r['ordinal']for r in finish['types']};ft={r['code']:r for r in finish['types']};it={r['code']:r for r in init['types']};policies={r['Code']:r for r in current['Types']};bycode={r['code']:r for r in pool['bases']};rare={r['code']:r for r in pool['rareBases']};pids={r['Code']:r['Ordinal']for r in plans};classes=('ama','sor','nec','pal','bar','dru','ass','war');offset=layout['MagicSuffixRows']+layout['MagicPrefixRows'];total=offset+44
 def isa(t,p):return t==p or any(isa(q,p)for q in typ[t]['parents'])
 qualified=[b for b in pool['bases']if(isa(b['type'],'weap')or isa(b['type'],'armo')or b['type']in('ring','amul'))and not b['sourceFields'].get('quest')and not b['sourceFields'].get('unique')]
 assert len(qualified)==516
 cases=[];native=[];seeds=[(666<<32)|511,(1<<64)-1]
 # code,level,requested quality,difficulty,unit seed,item seed,start,mode,family/direct row.
 for i,b in enumerate(qualified):
  for q in (1,2,3,4,6):
   for level in (1,37,99):
    for n,seed in enumerate(seeds):
     index=len(cases);cases.append([b['code'],level,q,i%3,str(seed^0x123456789ABCDEF0),str(seed),511,0,0])
     if level==(1,37,99)[i%3]and n==i%2:native.append(index)
 assert len(native)==2580
 for code in ('ssd','qui','rin'):
  for family in range(300,307):
   for level in (1,37,99):
    for q in (4,6):
     for seed in seeds:cases.append([code,level,q,0,str(seed^0x123456789ABCDEF0),str(seed),511,1,family])
 for row in range(44):
  for seed in seeds:
   index=len(cases);cases.append(['ssd',99,4,0,str(seed^0x123456789ABCDEF0),str(seed),511,2,row])
   if seed==seeds[0]:native.append(index)
 assert len(cases)==15820 and len(native)==2624
 # Extend only declared fixture ABI. All inherited source bodies remain intact.
 cpp=cpp.replace('int draws=0;uint32_t SEED_RollLimitedRandomNumber(D2SeedStrc*s,int n){if(n>0)draws++;','int draws=0,unitDraws=0;D2SeedStrc*unitRandom=nullptr;uint32_t SEED_RollLimitedRandomNumber(D2SeedStrc*s,int n){if(n>0){if(s==unitRandom)unitDraws++;else draws++;}')
 cpp=cpp.replace('staffClass=-1;D2ItemData data','staffClass=-1;void*pMemoryPool=nullptr;int dwUnitId=17;D2SeedStrc pSeed;int file=0,aut=0,variant=0;uint32_t start=0;struct D2GameStrc*pGame=nullptr;D2ItemData data')
 cpp=cpp.replace('struct D2ItemDropStrc{int nPrefix[3]={},nSuffix[3]={};uint32_t dwFlags2=0;}drop;','struct D2ItemDropStrc{int nPrefix[3]={},nSuffix[3]={};uint32_t dwFlags1=0,dwFlags2=10;int nQuality=0,nItemLvl=1,nItemIndex=0,nQtyOverride=0;bool bForce=false;D2UnitStrc*pUnit=nullptr;}drop;')
 cpp=cpp.replace('int wType[2]={},nMagicLevel=0;numeric::D2ItemsTxt numeric;','int wType[2]={},nMagicLevel=0,nBlock=0,dwSpeed=0;uint8_t nDurability=0;int nNoDurability=0,nQuest=0,nQuestDiffCheck=0,nLevel=1;uint32_t dwCode=0;int16_t wAutoPrefix=0;int nMinDam=0,nMaxDam=0,n2HandMinDam=0,n2HandMaxDam=0,nMinMisDam=0,nMaxMisDam=0,dwMinAc=0,dwMaxAc=0;numeric::D2ItemsTxt numeric;')
 cpp=cpp.replace('struct D2ItemTypesTxt{int nRare=0;}','struct D2ItemTypesTxt{int nRare=0,nMagic=0,nNormal=0;}')
 cpp=cpp.replace('struct D2UniqueItemsTxt{D2PropertyStrc pProperties[12];};','struct D2UniqueItemsTxt{uint16_t wVersion=0,wLvl=1,wRarity=1;uint32_t dwBaseItemCode=0,dwUniqueItemFlags=1;D2PropertyStrc pProperties[12];};')
 cpp=cpp.replace('D2MagicAffixTxt affixes[1508];','D2MagicAffixTxt affixes[1552];').replace('affixes+1508,1508};','affixes+1508,1552};').replace('id<=1508?affixes+id-1','id<=1552?affixes+id-1')
 cpp=cpp.replace('assert(flag==16&&!enabled);p->flags&=~flag;','if(enabled)p->flags|=flag;else p->flags&=~flag;')
 cpp=cpp.replace('void ITEMMODS_AssignProperty(int mode,D2UnitStrc*u,D2UnitStrc*p,const void*r,int set,int apply){assert(mode==0);','void SuperiorProperties(D2UnitStrc*,const void*);\nvoid ITEMMODS_AssignProperty(int mode,D2UnitStrc*u,D2UnitStrc*p,const void*r,int set,int apply){if(mode==1){SuperiorProperties(p,r);return;}assert(mode==0);')
 # Add complete durability writer with its explicit operative stat service.
 cpp+='namespace numeric{constexpr int STAT_DURABILITY=72;int STATLIST_GetMaxDurabilityFromUnit(D2UnitStrc*p){int b=Read(p->base,73);return b+b*Read(child,75)/100;}\n'+bodies['ITEMMODS_PropertyFunc13']+'\n}\n'
 cpp+=r'''
constexpr uint8_t operator""ui8(unsigned long long n){return n;}
constexpr uint32_t IFLAG_SOCKETED=0x800,IFLAG_ISEAR=0x10000,IFLAG_ETHEREAL=0x400000;
constexpr int ITEMDROPFLAG_NEVERETH=2,ITEMDROPFLAG_NOSOCKETS=8,ITEMDROPFLAG_ALWAYSETH=4,ITEMDROPFLAG_ALWAYSSOCKETS=16,ITEMDROPFLAG_SUPERIOR=64,UNIQUEITEMSFLAGINDEX_ENABLED=0,UNIQUEITEMSFLAGINDEX_LADDER=3;
constexpr int DIFFMODE_NORMAL=0,DIFFMODE_NIGHTMARE=1,DIFFMODE_HELL=2;
uint32_t gdwBitMasks[32];struct Weighted{int nIndex,nAccumulatedRarity;}stru_6FD45C18[32];
struct D2GameStrc{int nDifficulty=0,nGameType=0;uint32_t dwGameType=0;}game;
struct D2ItemRatioTxt{int dwUnique,dwRare,dwSet,dwMagic,dwHiQuality,dwNormal,dwUniqueDivisor,dwRareDivisor,dwSetDivisor,dwMagicDivisor,dwHiQualityDivisor,dwNormalDivisor;};
D2ItemRatioTxt*DATATBLS_GetItemRatioTxtRecord(int,int,int){std::abort();}
bool ITEMS_CanBeMagic(D2UnitStrc*){return typeRows[basis.wType[0]].nMagic;}bool ITEMS_CanBeNormal(D2UnitStrc*){return typeRows[basis.wType[0]].nNormal;}bool ITEMS_IsQuestItem(D2UnitStrc*){return basis.nQuest;}
bool ITEMS_GetUniqueColumnFromItemsTxt(D2UnitStrc*){return false;}bool ITEMS_IsMagSetRarUniCrfOrTmp(D2UnitStrc*p){return p->quality>=4&&p->quality<=9;}
void ITEMS_SetItemQuality(D2UnitStrc*p,int q){p->quality=q;}void ITEMS_SetAutoAffix(D2UnitStrc*p,int id){p->aut=id;}void ITEMS_SetFileIndex(D2UnitStrc*p,int id){p->file=id;}
void ITEMS_SetItemStartSeed(D2UnitStrc*p,uint32_t seed){p->start=seed;}uint32_t ITEMS_GetItemStartSeed(D2UnitStrc*p){return p->start;}
bool ITEMS_HasUniqueBeenDroppedAlready(D2GameStrc*,int){std::abort();}D2UniqueItemsTxt*ITEMS_GetUniqueItemsTxtRecord(int){std::abort();}bool ITEMS_CanUniqueItemBeDropped(D2GameStrc*,D2UnitStrc*){std::abort();}
int STATLIST_GetUnitBaseStat(D2UnitStrc*p,int stat,int layer){assert(layer==0);return p->base[uint32_t(stat)<<16];}
int STATLIST_UnitGetStatValue(D2UnitStrc*p,int stat,int layer){assert(layer==0);int b=STATLIST_GetUnitBaseStat(p,stat,0),v=p->props[uint32_t(stat)<<16];if(stat==73)return b+b*p->props[75u<<16]/100+v;return b+v;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*p,int stat,int layer){return STATLIST_UnitGetStatValue(p,stat,layer);}
void STATLIST_SetUnitStat(D2UnitStrc*p,int stat,int value,int layer){assert(layer==0);p->base[uint32_t(stat)<<16]=value;}
bool ITEMS_HasDurability(D2UnitStrc*p){return !basis.nNoDurability&&basis.nDurability&&STATLIST_UnitGetStatValue(p,73,0)&&STATLIST_UnitGetStatValue(p,152,0)<=0;}
int maxSockets=0,minStack=0,maxStack=0,spawnStack=0,varieties=0;bool quiver=false,stackable=false;
int ITEMS_GetQuiverType(D2UnitStrc*){return quiver;}int ITEMS_GetMinStack(D2UnitStrc*){return minStack;}int ITEMS_GetTotalMaxStack(D2UnitStrc*){return std::min(maxStack+STATLIST_UnitGetStatValue(&item,254,0),511);}int ITEMS_GetSpawnStack(D2UnitStrc*){return spawnStack;}
int ITEMS_GetVarInvGfxCount(D2UnitStrc*){return varieties;}void ITEMS_SetVarGfxIndex(D2UnitStrc*p,int n){p->variant=n;}
void D2GAME_SetStatOrResetGold_6FC7CA70(D2UnitStrc*,int,int){std::abort();}void ITEMMODS_SetRandomElixirFileIndex(D2UnitStrc*){std::abort();}
struct D2StatListStrc{};D2StatListStrc*STATLIST_GetStatListFromUnitStateOrFlag(D2UnitStrc*,int,int){std::abort();}D2StatListStrc*STATLIST_AllocStatList(...){std::abort();}void D2COMMON_10475_PostStatToStatList(...){std::abort();}void STATLIST_SetStatIfListIsValid(...){std::abort();}
struct D2QualityItemsTxt{int nArmor,nWeapon,nShield,nScepter,nWand,nStaff,nBow,nBoots,nGloves,nBelt,row;};struct D2ItemTypeCheckStrc{int bItemType,nItemType;};
struct D2QualityItemDataTbl{int nQualityItemsTxtRecordCount=8;}high;struct D2LowQualityItemDataTbl{int nLowQualityItemsTxtRecordCount=4;}low;
D2QualityItemsTxt highRows[8];D2QualityItemDataTbl*DATATBLS_GetQualityItemDataTables(){return &high;}D2LowQualityItemDataTbl*DATATBLS_GetLowQualityItemDataTables(){return &low;}D2QualityItemsTxt*DATATBLS_GetQualityItemsTxtRecord(int i){return i>=0&&i<8?highRows+i:nullptr;}
void ITEMS_AddSockets(D2UnitStrc*,uint32_t){std::abort();}
void ITEMS_AssignCharmAffixes(D2GameStrc*,D2UnitStrc*,D2ItemDropStrc*){std::abort();}int sub_6FC54240(D2UnitStrc*,int){std::abort();}
int sub_6FC53CD0(D2UnitStrc*,D2ItemDropStrc*){std::abort();}int sub_6FC542C0(D2UnitStrc*,D2ItemDropStrc*){std::abort();}int16_t ITEMS_RollTemperedItemAffix(D2UnitStrc*,int){std::abort();}
int32_t sub_6FC4C5F0(D2GameStrc*,D2UnitStrc**,D2ItemDropStrc*);
'''
 # Extend declared helpers with actual current socket/stackable traits.
 cpp=cpp.replace('bool ITEMS_CheckIfStackable(D2UnitStrc*){return false;}','extern bool stackable;bool ITEMS_CheckIfStackable(D2UnitStrc*){return stackable;}')
 cpp=cpp.replace('bool ITEMS_CheckIfSocketable(D2UnitStrc*){return true;}int ITEMS_GetMaxSockets(D2UnitStrc*){return 6;}','extern int maxSockets;bool ITEMS_CheckIfSocketable(D2UnitStrc*){return maxSockets>0;}int ITEMS_GetMaxSockets(D2UnitStrc*){return maxSockets;}')
 order=['SEED_InitLowSeed','SEED_GetLowSeed','ITEMMODS_CanItemBeHighQuality','ITEMMODS_ApplyEthereality','sub_6FC4D6B0','ITEMS_MakeEthereal','sub_6FC4D900','D2GAME_ITEMS_AssignSpecial_6FC4DB60','sub_6FC4DC20','sub_6FC54690','sub_6FC549F0','sub_6FC54D00','sub_6FC55270','ITEMS_RollItemQuality','sub_6FC4E1A0','sub_6FC4E1F0','sub_6FC4E430','sub_6FC4C5F0_End','sub_6FC4C5F0','D2GAME_InitItemStats_6FC4E520']
 cpp+='\n'.join(bodies[n]for n in order)
 cpp+='\nint16_t ITEMS_RollMagicAffixes(D2UnitStrc*p,int a,int b,int c,int d,int e,int f){return ITEMS_RollMagicAffixesNew(p,a,b,c,d,e,f);}\n'
 # Forward declarations precede finalizer's overloaded picker call.
 at=cpp.index('constexpr uint32_t IFLAG_SOCKETED=')
 cpp=cpp[:at]+'int16_t ITEMS_RollMagicAffixes(D2UnitStrc*,int,int,int,int,int,int);\n'+cpp[at:]
 cpp+='D2PropertyStrc grade[8][2]={\n'
 for row in finish['superior']:
  values=['{'+','.join(map(str,(pids[m['code']],m['min'],m['max'],int(m['parameter']))))+'}'for m in row['modifiers']]
  values+=['{-1,0,0,0}']*(2-len(values));cpp+='{'+','.join(values)+'},\n'
 cpp+='};\nvoid SuperiorProperties(D2UnitStrc*p,const void*r){auto row=(const D2QualityItemsTxt*)r;for(auto prop:grade[row->row]){if(prop.nProperty<0)break;D2COMMON_11292_ItemAssignProperty(1,nullptr,p,r,0,0,&prop,0,0x40,nullptr);}}\n'
 cpp+='void CurrentTables(){InitTables();numeric::off_6FDE3920[13]=numeric::ITEMMODS_PropertyFunc13;\n'
 for i in range(32):cpp+=f'gdwBitMasks[{i}]=1u<<{i};\n'
 for r in current['Types']:cpp+=f'typeRows[{ordinals[r["Code"]]}].nMagic={int(r["Magic"])};typeRows[{ordinals[r["Code"]]}].nNormal={int(r["Normal"])};\n'
 for r in finish['superior']:
  cpp+=f'highRows[{r["row"]}]={{'+','.join(str(int(r[k]))for k in ('armor','weapon','shield','scepter','wand','staff','bow','boots','gloves','belt'))+','+str(r['row'])+'};\n'
 for row in current['AutoMagic']:
  i=offset+row['Row'];cpp+=f'affixes[{i}].wVersion={row["Version"]};affixes[{i}].wSpawnable={int(row["Spawnable"])};affixes[{i}].nRare={int(row["Rare"])};affixes[{i}].dwLevel={row["Level"]};affixes[{i}].dwMaxLevel={row["MaxLevel"]};affixes[{i}].wFrequency={row["Frequency"]};affixes[{i}].dwGroup={row["Group"]};\n'
  for n,m in enumerate(row['Modifiers']):cpp+=f'affixes[{i}].pProperties[{n}]={{{pids[m["Code"]]},{m["Min"]},{m["Max"]},{int(m["Parameter"])}}};\n'
  for field,member in [('Include','wIType'),('Exclude','wEType')]:
   for n,t in enumerate(row[field]):cpp+=f'affixes[{i}].{member}[{n}]={ordinals[t]};\n'
 cpp+='}\nstruct Prepared{int level,quality,difficulty;uint64_t unit,item;uint32_t start;int mode,extra,type,qlvl,magic,cls,staff;bool weapon,throws,quiver,stackable;int minstack,maxstack,spawnstack,varieties,sockets;D2ItemsTxt basis;};\nPrepared cases[]={\n'
 for a in cases:
  b=bycode[a[0]];r=b['sourceFields'];I=lambda key:r.get(key,0);t=b['type'];props=[I(k)for k in ('mindam','maxdam','2handmindam','2handmaxdam','minmisdam','maxmisdam','minac','maxac')];pol=policies[t]
  socket=min(ft[t]['maxSockets40']if a[1]>40 else ft[t]['maxSockets25']if a[1]>25 else ft[t]['maxSockets1'],I('gemsockets'))
  code=int.from_bytes(b['code'].ljust(4).encode(),'little');cls=classes.index(b['requiredClass'])if b['requiredClass']else -1;staff=classes.index(ft[t]['staffMods'])if ft[t]['staffMods']else -1
  basevalues=[ordinals[t],b['magicLevel'],I('block'),I('speed'),I('durability'),I('nodurability'),0,0,b['qualityLevel'],code,I('auto prefix'),*props[:6],props[6],props[7]]
  cpp+='{'+','.join(map(str,a[1:4]))+','+a[4]+'ULL,'+a[5]+'ULL,'+','.join(map(str,a[6:9]))+','+','.join(map(str,(ordinals[t],b['qualityLevel'],b['magicLevel'],cls,staff,int(isa(t,'weap')),int(rare[a[0]]['throwable']),int(it[t]['quiver']),int(I('stackable')),I('minstack'),I('maxstack'),I('spawnstack'),it[t]['varieties'],socket)))+',{{'+str(basevalues[0])+',0},'+','.join(map(str,basevalues[1:]))+',{'+','.join(map(str,props))+'}}},\n'
 cpp+=r'''};
void Array(const std::map<uint32_t,int>&a){printf("[");bool comma=false;for(auto [k,v]:a){printf("%s[%u,%d]",comma?",":"",k,v);comma=true;}printf("]");}
void List(const std::vector<int>&a){printf("[");for(size_t i=0;i<a.size();i++)printf("%s%d",i?",":"",a[i]);printf("]");}
int main(){CurrentTables();printf("[");bool comma=false;for(auto c:cases){item={};item.pItemData=&item.data;item.pGame=&game;item.level=c.level;item.quality=c.mode?c.quality:0;item.qlvl=c.qlvl;item.itemClass=c.cls;item.staffClass=c.staff;item.weapon=c.weapon;item.throws=c.throws;item.flags=0;item.file=0;item.aut=0;item.variant=0;item.start=c.start;item.seed.lSeed=c.item;item.pSeed.lSeed=c.unit;unitRandom=&item.pSeed;draws=unitDraws=0;basis=c.basis;drop={};drop.nItemLvl=c.level;drop.nQuality=c.quality;drop.dwFlags2=10;game.nDifficulty=c.difficulty;maxSockets=c.sockets;quiver=c.quiver;stackable=c.stackable;minStack=c.minstack;maxStack=c.maxstack;spawnStack=c.spawnstack;varieties=c.varieties;D2UnitStrc*p=&item;int result=1;
if(c.mode==0){result=D2GAME_InitItemStats_6FC4E520(&game,&p,&drop,1);ITEMS_SetItemFlag(p,16,1);}else{
 for(auto pair:{std::array{21,11},std::array{22,27},std::array{23,0},std::array{24,0},std::array{159,0},std::array{160,0},std::array{31,51},std::array{72,17},std::array{73,23}})item.base[uint32_t(pair[0])<<16]=pair[1];
 if(c.mode==1){item.aut=ITEMS_RollMagicAffixesNew(p,0,1,0,1,0,c.extra);if(item.aut)ITEMMODS_AssignProperty(0,nullptr,p,DATATBLS_GetMagicAffixTxtRecord(item.aut),0,0);}else{item.aut=1509+c.extra;ITEMMODS_AssignProperty(0,nullptr,p,DATATBLS_GetMagicAffixTxtRecord(item.aut),0,0);}
}
printf("%s[%d,%d,%d,%u,\"%llu\",%d,%d,%d,%d,",comma?",":"",result,item.quality,drop.nQuality,item.start,(unsigned long long)item.pSeed.lSeed,item.variant,item.file,item.aut,item.flags);comma=true;
List({item.data.wMagicPrefix[0],item.data.wMagicPrefix[1],item.data.wMagicPrefix[2]});printf(",");List({item.data.wMagicSuffix[0],item.data.wMagicSuffix[1],item.data.wMagicSuffix[2]});printf(",%d,%d,",item.rarePrefix,item.rareSuffix);std::vector<int>ordinary,automatic;for(int id:item.applied){if(id<=1508)ordinary.push_back(id);else automatic.push_back(id);}List(ordinary);printf(",");List(automatic);printf(",\"%llu\",%s,",(unsigned long long)item.seed.lSeed,item.has?"true":"false");Array(item.base);printf(",");Array(item.props);printf(",%d,%d",draws,unitDraws);
D2PropertyStrc hp{13,1,5,0};D2COMMON_11292_ItemAssignProperty(0,nullptr,p,nullptr,0,0,&hp,0,0x40,nullptr);printf(",\"%llu\",",(unsigned long long)item.seed.lSeed);Array(item.props);printf("]");}printf("]\n");}
'''
 for n in order:assert function(cpp[cpp.index('constexpr uint32_t IFLAG_SOCKETED='):],n)==bodies[n],n
 generated=STAGE/'prepared-source.cpp';generated.write_text(cpp);binary=STAGE/'prepared-probe'
 with(STAGE/'prepared-compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-Wno-literal-suffix','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True,timeout=120);(STAGE/'prepared-sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
 results=json.loads(run.stdout);assert len(results)==len(cases);out=STAGE/'prepared-cases.json';out.write_text(json.dumps(dict(inputs=cases,outputs=results,native=native,qualified=[b['code']for b in qualified]),separators=(',',':'))+'\n')
 record=dict(referenceVersion='3.3.93847',currentExecutableParity=False,cases=len(cases),qualifiedBases=516,preparedControls=15480,automagicSelectionControls=252,directAutomagicControls=88,originalNativeContexts=2624,
  inheritedRoutineGroups=prior['completeRoutineGroups'],completeRoutineHashes={n:hashlib.sha256(bodies[n].encode()).hexdigest()for n in order+['ITEMMODS_PropertyFunc13']},reviewedOnlyRoutineHashes={n:hashlib.sha256(bodies[n].encode()).hexdigest()for n in ['ITEMS_AllocItemData']},inputs=inputs,
  scope='Complete older base initializer/quality dispatcher/finisher/socket/grade/inferior/normal bodies compose with accepted current affix pipeline and44owned automagic rows. Separate unit/item RNG,exact low-seed fallback,initial quality0,graphics/file state and final NPC-create identification are explicit. Grade/tree/staffmod adaptations retain their prior user approvals.',
  limits=['Qualified component covers516nonquest/nonunique body-equipment bases,format100,requested qualities1/2/3/4/6,NoSockets+NeverEthereal and explicit initial separate seed streams. Allocation-seed provenance,full materialization,NOSSELL/equip/full durability delivery and actors remain separate.',
   'Source ABI supplies immutable current metadata/logical type queries,successful list allocation,unit/item seed/graph transfer and maximum-durability stat reading. Unforced ratio/unique/set/craft/quest/charm/book/drop-world services abort if unexpectedly reached;retained ratio rows do not admit those routes.',
   'Reference IDs are not current compiled/binary/save identities. Exact modern executable/world/callback/seed parity remains unproved.'],
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in(generated,binary,STAGE/'prepared-compiler.log',STAGE/'prepared-sanitizer.log',out,Path(__file__).resolve())})
 target=ROOT/'tools/reference/item-quality-current-source-evidence.json';encoded=json.dumps(record,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Prepared current quality source proof stale'
 else:target.write_text(encoded)
 print('Prepared quality:',len(cases),'compositions/516bases/44automagic rows;',len(native),'original contexts;ASan+UBSan clean.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
