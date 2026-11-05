#!/usr/bin/env python3
"""Compose complete old affix callers/pickers/dispatch with current raw writers."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
def function(text,name):
 match=re.search(r'^(?:static |inline )?[A-Za-z_][^;\n]*\b'+name+r'\([^;{]*?\)\s*\n\{',text,re.M);assert match,name
 end=text.index('{',match.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[match.start():end]
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'current-item-affixes-source'
CLASSES=('ama','sor','nec','pal','bar','dru','ass','war')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 inputs={};bodies={};tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def load(rel):
  p=ROOT/rel;inputs[rel]=sha(p);return json.loads(p.read_text())
 def read(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 def enum(rel,name):
  raw=read(rel);return raw[raw.index('enum '+name):raw.index('};',raw.index('enum '+name))+2]
 for rel,names in {'source/D2Game/src/ITEMS/Items.cpp':['sub_6FC4D5E0'],'source/D2Game/src/ITEMS/ItemsMagic.cpp':['D2GAME_RollRareItem_6FC53360','D2GAME_RollRareAffix_6FC53610','ITEMS_ComputeCraftedMagicAffixLevel','ITEMMODS_AffixPlayerClassisCompatible','ITEMMODS_HasAffixWithGroup','ComputeAffixFrequency','MagicItemRollAffix','ITEMS_RollMagicAffixesNew'],'source/D2Common/src/Items/ItemMods.cpp':['ITEMMODS_CanItemHaveMagicAffix','ITEMMODS_CanItemHaveRareAffix','ITEMMODS_AssignProperty','sub_6FD95810'],'source/D2Common/src/Items/Items.cpp':['ITEMS_CanBeRare'],'source/D2Common/include/D2Seed.h':['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber']}.items():
  raw=read(rel)
  for n in names:bodies[n]=function(raw,n)
 numericEvidence=load('tools/reference/item-skill-property-source-evidence.json');numericPath=BASE/'item-skill-property-source/source.cpp';assert sha(numericPath)==numericEvidence['artifacts'][str(numericPath.relative_to(ROOT))];inputs[str(numericPath.relative_to(ROOT))]=sha(numericPath)
 numeric=numericPath.read_text().split('bool comma=false;',1)[0]
 numericHashes=numericEvidence['inheritedCompleteRoutineHashes']|numericEvidence['newCompleteRoutineHashes']
 for n,h in numericHashes.items():assert hashlib.sha256(function(numeric,n).encode()).hexdigest()==h,n
 treeDecision=load('tools/reference/item-tree-packing-decision.json');assert treeDecision['status']=='user-approved-sanctuary-adaptation'
 oldTree=function(numeric,'ITEMMODS_PropertyFunc10');literal='pProperty->nLayer % 3 + 8 * pProperty->nLayer / 3';assert oldTree.count(literal)==2
 newTree=oldTree.replace('ITEMMODS_PropertyFunc10','SANCTUARY_TreeProperty',1).replace(literal,'pProperty->nLayer % 3 + 8 * (pProperty->nLayer / 3)')
 numeric+='\n'+newTree+'\n'
 staffEvidence=load('tools/reference/item-staffmods-source-evidence.json');staffPath=BASE/'item-staffmods-source/source.cpp';assert sha(staffPath)==staffEvidence['artifacts'][str(staffPath.relative_to(ROOT))];inputs[str(staffPath.relative_to(ROOT))]=sha(staffPath)
 staff=staffPath.read_text().split('Config cases[]={',1)[0]
 for n,h in staffEvidence['completeRoutineHashes'].items():assert hashlib.sha256(function(staff,n).encode()).hexdigest()==h,n
 assert hashlib.sha256(function(staff,'SANCTUARY_StaffmodsByRequiredLevel').encode()).hexdigest()==staffEvidence['adaptedRoutineSha256']
 cat=load('data/Sanctuary/Balance/npc-item-affix-catalogue.json');fin=load('data/Sanctuary/Balance/item-finishing-catalogue.json');plans=load('data/Sanctuary/Balance/equipment-property-plans.json');layout=load('data/Sanctuary/Balance/item-affix-layout.json');decision=load('tools/reference/item-staffmods-tier-decision.json')
 types={r['code']:r for r in cat['types']};ordinals={r['code']:r['ordinal']for r in fin['types']};labels={r['code']:r['staffMods']for r in fin['types']};bybase={r['code']:r for r in cat['bases']};rare={r['code']:r for r in cat['rareBases']};planids={r['Code']:r['Ordinal']for r in plans};prefixRows=layout['MagicPrefixRows'];suffixRows=layout['MagicSuffixRows'];affixCount=prefixRows+suffixRows
 def isa(t,p):return t==p or any(isa(v,p)for v in types[t]['parents'])
 def eligible(b,a):
  d=a['definition'];return not a['firstPropertySockets']and any(isa(b['type'],t)for t in d['include'])and not any(isa(b['type'],t)for t in d['exclude'])and(not d['classSpecific']or not b['requiredClass']or d['classSpecific']==b['requiredClass'])
 def aid(a):return a['row']+1+(suffixRows if a['definition']['kind']=='Prefix'else 0)
 seeds=[666<<32,(666<<32)|511,(1<<64)-1];cases=[];native=[]
 # code,level,quality,seed,mode,three local prefix and suffix preferences,
 # direct global affix ID,tree parameter/min/max,dropflags.
 def case(code,level,q,seed,mode,prefs=None,direct=0,param=0,minimum=0,maximum=0,flags=0):
  cases.append([code,level,q,str(seed),mode,*(prefs or[0]*6),direct,param,minimum,maximum,flags])
 for i,b in enumerate(cat['bases']):
  for level in (1,37,99):
   for q in (4,6):
    for n,seed in enumerate(seeds):
     index=len(cases);case(b['code'],level,q,seed,0 if q==4 else 1,flags=32 if n==2 else 0)
     if level==(1,37,99)[i%3]and q==(4 if i%2==0 else 6)and n==i%3:native.append(index)
 assert len(native)==692
 for code in ('ssd','qui','rin','amu','6bs','dgr','ba1','7ar','cm1','jew'):
  if code not in bybase:continue
  b=bybase[code];p=next((a['row']+1 for a in cat['affixes']if a['definition']['kind']=='Prefix'and eligible(b,a)),0);s=next((a['row']+1 for a in cat['affixes']if a['definition']['kind']=='Suffix'and eligible(b,a)),0)
  for q in (4,6):
   for seed in seeds:
    for pp,ss in ((-1,-1),(p,-1),(-1,s),(p,s)):
     case(code,99,q,seed,0 if q==4 else 1,[pp,pp,pp,ss,ss,ss])
 directStart=len(cases)
 for a in cat['affixes']:
  if a['firstPropertySockets']:continue
  b=next(b for b in cat['bases']if eligible(b,a))
  for n,seed in enumerate(seeds[:2]):
   index=len(cases);case(b['code'],99,4,seed,2,direct=aid(a))
   if n==1:native.append(index)
 assert len(native)==1680
 for parameter in range(24):
  for minimum,maximum in ((1,1),(1,3),(3,1)):
   for seed in seeds:case('ssd',37,4,seed,3,param=parameter,minimum=minimum,maximum=maximum)
 noincludes=lambda text:re.sub(r'^#include[^\n]*\n','',text,flags=re.M)
 cpp='\n'.join('#include <'+n+'>'for n in ('algorithm','array','cassert','cstdint','cstdio','cstdlib','map','string','vector'))+'\nnamespace numeric{\n'+noincludes(numeric)+'\n}\nnamespace staff{\n'+noincludes(staff)+'\n}\n'
 cpp+='\n#define D2_UNREACHABLE std::abort()\n#define D2Clamp std::clamp\nusing BOOL=bool;\nconstexpr int ITEMS_MAX_MODS=3,HARDCODEDCST_MAX_LEVEL=99,PCLASS_INVALID=-1,UNIT_ITEM=4,IFLAG_IDENTIFIED=16;\n'
 cpp+='\n'.join((enum('source/D2Common/include/D2Items.h','D2C_ItemQualities'),enum('source/D2Common/include/D2Items.h','D2C_ItemTypes'),enum('source/D2Common/include/D2StatList.h','D2C_ItemStats'),enum('source/D2Common/include/D2StatList.h','D2C_PropertyModes'),enum('source/D2Common/include/D2StatList.h','D2C_StatlistFlags'),enum('source/D2Common/include/D2States.h','D2C_States')))
 cpp+=f'\nconstexpr int JEWEL_REFERENCE_ROW={ordinals["jewl"]};\n'
 cpp+=r'''
constexpr unsigned long long operator""i64(unsigned long long n){return n;}
union D2SeedStrc{uint64_t lSeed;struct{uint32_t nLowSeed,nHighSeed;};};
namespace seed_reference{
'''+bodies['SEED_RollRandomNumber']+'\n'+bodies['SEED_RollLimitedRandomNumber']+r'''
}
int draws=0;uint32_t SEED_RollLimitedRandomNumber(D2SeedStrc*s,int n){if(n>0)draws++;return seed_reference::SEED_RollLimitedRandomNumber(s,n);}
uint32_t ITEMS_RollLimitedRandomNumber(D2SeedStrc*s,int n){return SEED_RollLimitedRandomNumber(s,n);}
uint32_t ITEMS_RollRandomNumber(D2SeedStrc*s){draws++;return seed_reference::SEED_RollRandomNumber(s);}
struct D2PropertyStrc{int nProperty=-1,nMin=0,nMax=0,nLayer=0;};
struct D2MagicAffixTxt{int wVersion=0,wSpawnable=0,dwLevel=0,dwGroup=0,dwMaxLevel=0,nRare=0,wFrequency=0;uint8_t nClassSpecific=255;D2PropertyStrc pProperties[3];uint16_t wEType[5]={},wIType[7]={};};
struct D2RareAffixTxt{int wVersion=0;uint16_t wEType[4]={},wIType[7]={};};
struct D2ItemData{uint16_t wMagicPrefix[3]={},wMagicSuffix[3]={};};
struct D2UnitStrc{int dwUnitType=4,dwClassId=0,quality=4,level=1,qlvl=0,itemClass=-1,staffClass=-1;D2ItemData data,*pItemData=&data;D2SeedStrc seed;int rarePrefix=0,rareSuffix=0,flags=16;bool weapon=false,throws=false,has=false;std::map<uint32_t,int>base,props;std::vector<int>applied;}item;
struct D2ItemDropStrc{int nPrefix[3]={},nSuffix[3]={};uint32_t dwFlags2=0;}drop;
struct D2ItemsTxt{int wType[2]={},nMagicLevel=0;numeric::D2ItemsTxt numeric;}basis;
struct D2ItemTypesTxt{int nRare=0;}typeRows[110];
struct D2PropertiesTxt{short wStat[7]{};}propertyRows[285];
struct D2UniqueItemsTxt{D2PropertyStrc pProperties[12];};struct D2SetItemsTxt{D2PropertyStrc pProperties[9],pPartialBoni[10];int nAddFunc=0;};struct D2GemsTxt{D2PropertyStrc pProperties[3][3];};
struct Tables{int nPropertiesTxtRecordCount=285,nItemTypesTxtRecordCount=110,nUniqueItemsTxtRecordCount=0,nSetItemsTxtRecordCount=0;D2PropertiesTxt*pPropertiesTxt=propertyRows;D2ItemTypesTxt*pItemTypesTxt=typeRows;D2UniqueItemsTxt*pUniqueItemsTxt=nullptr;D2SetItemsTxt*pSetItemsTxt=nullptr;}tables;auto*sgptDataTables=&tables;
struct LegacyAssign{void(*pfAssign)(...)=nullptr;short nStatId=0;}stru_6FDE3160[285];
struct D2MagicAffixDataTbl{D2MagicAffixTxt*pMagicAffixTxt,*pMagicPrefix,*pMagicSuffix,*pAutoMagic;int nMagicAffixTxtRecordCount;};
struct D2RareAffixDataTbl{D2RareAffixTxt*pRareAffixTxt,*pRarePrefix,*pRareSuffix;int nRareAffixTxtRecordCount;};
struct D2MagicAffixIndexAndPtrStrc{int nAffixId;const D2MagicAffixTxt*pMagicAffixRecord;};
struct D2RareAffixIndexAndPtrStrc{int nIndex;D2RareAffixTxt*pRareAffixRecord;};
int parents[110][2]{};bool IsType(int t,int p){if(t==p)return true;if(t<0||t>=110)return false;for(int q:parents[t])if(q>0&&IsType(q,p))return true;return false;}
bool ITEMS_CheckItemTypeId(D2UnitStrc*,int t){return IsType(basis.wType[0],t);}
int ITEMS_GetItemType(D2UnitStrc*){return basis.wType[0]==JEWEL_REFERENCE_ROW?ITEMTYPE_JEWEL:basis.wType[0];}uint16_t ITEMS_GetItemFormat(D2UnitStrc*){return 100;}int ITEMS_GetItemLevel(D2UnitStrc*p){return p->level;}int ITEMS_GetItemQlvl(D2UnitStrc*p){return p->qlvl;}
int ITEMS_GetItemQuality(D2UnitStrc*p){return p->quality;}int ITEMS_GetClassOfClassSpecificItem(D2UnitStrc*p){return p->itemClass;}
D2SeedStrc*ITEMS_GetItemSeed(D2UnitStrc*p){return &p->seed;}D2ItemsTxt*DATATBLS_GetItemsTxtRecord(int){return &basis;}
bool ITEMS_CheckIfStackable(D2UnitStrc*){return false;}bool ITEMS_CheckIfThrowable(D2UnitStrc*p){return p->throws;}bool ITEMS_CheckIfSocketable(D2UnitStrc*){return true;}int ITEMS_GetMaxSockets(D2UnitStrc*){return 6;}
int ITEMS_GetFileIndex(D2UnitStrc*){return -1;}
int ITEMS_GetPrefixId(D2UnitStrc*p,int slot){return p->data.wMagicPrefix[slot];}int ITEMS_GetSuffixId(D2UnitStrc*p,int slot){return p->data.wMagicSuffix[slot];}
void ITEMS_AssignPrefix(D2UnitStrc*p,int id,int slot){p->data.wMagicPrefix[slot]=id;}void ITEMS_AssignSuffix(D2UnitStrc*p,int id,int slot){p->data.wMagicSuffix[slot]=id;}
void ITEMS_AssignRarePrefix(D2UnitStrc*p,int id){p->rarePrefix=id;}void ITEMS_AssignRareSuffix(D2UnitStrc*p,int id){p->rareSuffix=id;}
void ITEMS_SetItemFlag(D2UnitStrc*p,int flag,int enabled){assert(flag==16&&!enabled);p->flags&=~flag;}
int sub_6FC53760(D2UnitStrc*,D2ItemDropStrc*){std::abort();}
void ITEMMODS_AssignProperty(int,D2UnitStrc*,D2UnitStrc*,const void*,int,int);
void D2COMMON_11292_ItemAssignProperty(int,D2UnitStrc*,D2UnitStrc*,const void*,int,int,const D2PropertyStrc*,int,int,D2UnitStrc*);
void sub_6FC52410(D2UnitStrc*,D2ItemDropStrc*);
'''
 cpp+=f'D2MagicAffixTxt affixes[{affixCount}];D2RareAffixTxt names[201];\nD2MagicAffixDataTbl magicTables{{affixes,affixes+{suffixRows},affixes,affixes+{affixCount},{affixCount}}};\nD2RareAffixDataTbl rareTables{{names,names+155,names,201}};\n'
 cpp+=f'D2MagicAffixDataTbl*DATATBLS_GetMagicAffixDataTables(){{return &magicTables;}}D2RareAffixDataTbl*DATATBLS_GetRareAffixDataTables(){{return &rareTables;}}\nD2MagicAffixTxt*DATATBLS_GetMagicAffixTxtRecord(int id){{return id>0&&id<={affixCount}?affixes+id-1:nullptr;}}\n'
 cpp+='namespace property_dispatch{\nvoid sub_6FD95810(int,D2UnitStrc*,D2UnitStrc*,const void*,int,int,int,const D2PropertyStrc*,int,int,D2UnitStrc*);\n'+bodies['ITEMMODS_AssignProperty']+'\n'+bodies['sub_6FD95810']+'\n}\n'
 order=['ITEMS_CanBeRare','ITEMMODS_CanItemHaveMagicAffix','ITEMMODS_CanItemHaveRareAffix','ITEMS_ComputeCraftedMagicAffixLevel','ITEMMODS_AffixPlayerClassisCompatible','ITEMMODS_HasAffixWithGroup','ComputeAffixFrequency','MagicItemRollAffix','ITEMS_RollMagicAffixesNew','D2GAME_RollRareAffix_6FC53610']
 cpp+='\n'.join(bodies[n]for n in order)+r'''
int16_t ITEMS_RollMagicAffixes(D2UnitStrc*p,int a,int b,int c,int d,int e){return ITEMS_RollMagicAffixesNew(p,a,b,c,d,e,0);}
void D2COMMON_11292_ItemAssignProperty(int mode,D2UnitStrc*,D2UnitStrc*p,const void*,int,int,const D2PropertyStrc*prop,int state,int flags,D2UnitStrc*){
 numeric::D2UnitStrc n;n.weapon=p->weapon;n.throwable=p->throws;n.level=p->level;n.pSeed.real=true;n.pSeed.value=p->seed.lSeed;n.base.values=p->base;numeric::basis=basis.numeric;numeric::child.values=p->props;numeric::allocate=true;numeric::trace.clear();numeric::D2PropertyStrc value{prop->nProperty,prop->nMin,prop->nMax,prop->nLayer};
 numeric::D2COMMON_11292_ItemAssignProperty(mode,nullptr,&n,nullptr,0,0,&value,state,flags,nullptr);
 draws+=n.pSeed.count;p->seed.lSeed=n.pSeed.value;p->base=n.base.values;p->props=numeric::child.values;
 for(auto t:numeric::trace)if(t[0]==3&&t[3])p->has=true;
}
void ITEMMODS_AssignProperty(int mode,D2UnitStrc*u,D2UnitStrc*p,const void*r,int set,int apply){assert(mode==0);p->applied.push_back((const D2MagicAffixTxt*)r-affixes+1);property_dispatch::ITEMMODS_AssignProperty(mode,u,p,r,set,apply);}
void sub_6FC52410(D2UnitStrc*p,D2ItemDropStrc*d){
 if(p->staffClass<0)return;
 staff::current={};staff::current.cls=p->staffClass;staff::current.first=staff::tiers[p->staffClass][0][0];staff::current.format=100;staff::current.quality=p->quality;staff::current.listmode=1;
 for(int type=1;type<110;type++)if(IsType(basis.wType[0],type))staff::current.allowed.push_back(type);
 staff::item={};staff::item.has=p->has;staff::item.data.pSeed.lSeed=p->seed.lSeed;
 for(auto [key,v]:p->props)if(key>>16==107)staff::item.list.skills[key&65535]=v;
 staff::trace.clear();staff::SANCTUARY_StaffmodsByRequiredLevel(&staff::item,p->level,staff::current.first,30,d->dwFlags2&32?p->level:0);
 for(auto t:staff::trace)if(t[0]==0)draws++;
 p->seed.lSeed=staff::item.data.pSeed.lSeed;p->has=staff::item.has;
 for(auto i=p->props.begin();i!=p->props.end();)if(i->first>>16==107)i=p->props.erase(i);else++i;
 for(auto [id,v]:staff::item.list.skills)p->props[(107u<<16)|id]=v;
}
'''+bodies['sub_6FC4D5E0']+'\n'+bodies['D2GAME_RollRareItem_6FC53360']+'\n'
 # Verify original copies in each independently inherited/constructed section.
 for n in order+['sub_6FC4D5E0','D2GAME_RollRareItem_6FC53360','ITEMMODS_AssignProperty','sub_6FD95810']:
  assert function(cpp[cpp.index('namespace property_dispatch{'):],n)==bodies[n],n
 cpp+='void InitTables(){numeric::Init();numeric::Skills();numeric::off_6FDE3920[10]=numeric::SANCTUARY_TreeProperty;numeric::off_6FDE3920[11]=numeric::ITEMMODS_PropertyFunc11;numeric::off_6FDE3920[19]=numeric::ITEMMODS_PropertyFunc19;\n'
 for code,t in types.items():
  i=ordinals[code];cpp+=f'typeRows[{i}].nRare={int(t["rare"])};\n'
  for n,parent in enumerate(t['parents']):cpp+=f'parents[{i}][{n}]={ordinals[parent]};\n'
 for r in plans:
  if r['Steps']:cpp+=f'propertyRows[{r["Ordinal"]}].wStat[0]={r["Steps"][0]["Stat"]};\n'
 for a in cat['affixes']:
  index=aid(a)-1;d=a['definition'];cpp+=f'affixes[{index}].wVersion={a["version"]};affixes[{index}].wSpawnable=1;affixes[{index}].dwLevel={d["level"]};affixes[{index}].dwMaxLevel={d["maxLevel"]};affixes[{index}].dwGroup={d["group"]};affixes[{index}].nRare={int(d["rare"])};affixes[{index}].wFrequency={d["frequency"]};affixes[{index}].nClassSpecific={CLASSES.index(d["classSpecific"])if d["classSpecific"]else 255};\n'
  for n,m in enumerate(d['modifiers']):cpp+=f'affixes[{index}].pProperties[{n}]={{{planids[m["code"]]},{m["min"]},{m["max"]},{int(m["parameter"]or 0)}}};\n'
  for name,member in [('include','wIType'),('exclude','wEType')]:
   for n,t in enumerate(d[name]):cpp+=f'affixes[{index}].{member}[{n}]={ordinals[t]};\n'
 for r in cat['rareNames']:
  at=r['row']+(155 if r['prefix']else 0);cpp+=f'names[{at}].wVersion={r["version"]};\n'
  for name,member in [('include','wIType'),('exclude','wEType')]:
   for n,t in enumerate(r[name]):cpp+=f'names[{at}].{member}[{n}]={ordinals[t]};\n'
 restriction=load('data/Sanctuary/Balance/item-skill-execution.json')['Skills']
 for r in restriction:cpp+=f'staff::records[{r["Id"]}].nITypeA[0]={ordinals[r["Fields"]["itypea1"]]if r["Fields"].get("itypea1")else 0};\n'
 cpp+='}\nstruct Config{int level,quality;uint64_t seed;int mode,p[3],s[3],affix,tree,min,max;uint32_t flags;int type,qlvl,magic,cls,staff;bool weapon,throws;numeric::D2ItemsTxt basis;};\nConfig cases[]={\n'
 for a in cases:
  b=bybase[a[0]];raw=b['sourceFields'];values=[raw.get(k,0)for k in ('mindam','maxdam','2handmindam','2handmaxdam','minmisdam','maxmisdam','minac','maxac')]
  cpp+='{'+str(a[1])+','+str(a[2])+','+a[3]+'ULL,'+str(a[4])+',{'+','.join(map(str,a[5:8]))+'},{'+','.join(map(str,a[8:11]))+'},'+','.join(map(str,a[11:16]))+','+','.join(map(str,(ordinals[b['type']],b['qualityLevel'],b['magicLevel'],CLASSES.index(b['requiredClass'])if b['requiredClass']else -1,CLASSES.index(labels[b['type']])if labels[b['type']]else -1,int(isa(b['type'],'weap')),int(rare[b['code']]['throwable']))))+',{'+','.join(map(str,values))+'}},\n'
 cpp+=r'''};
void Array(const std::map<uint32_t,int>&a){printf("[");bool comma=false;for(auto [k,v]:a){printf("%s[%u,%d]",comma?",":"",k,v);comma=true;}printf("]");}
void List(const std::vector<int>&a){printf("[");for(size_t i=0;i<a.size();i++)printf("%s%d",i?",":"",a[i]);printf("]");}
int main(){InitTables();printf("[");bool comma=false;for(auto c:cases){item={};item.pItemData=&item.data;item.level=c.level;item.quality=c.quality;item.qlvl=c.qlvl;item.itemClass=c.cls;item.staffClass=c.staff;item.weapon=c.weapon;item.throws=c.throws;item.seed.lSeed=c.seed;basis={{c.type,0},c.magic,c.basis};drop={};drop.dwFlags2=c.flags;for(int i=0;i<3;i++){drop.nPrefix[i]=c.p[i];drop.nSuffix[i]=c.s[i];}draws=0;
for(auto pair:{std::array{21,11},std::array{22,27},std::array{23,0},std::array{24,0},std::array{159,0},std::array{160,0},std::array{31,51},std::array{72,17},std::array{73,23}})item.base[uint32_t(pair[0])<<16]=pair[1];int result=1;D2UnitStrc*p=&item;
switch(c.mode){case 0:result=sub_6FC4D5E0(&p,&drop);break;case 1:result=D2GAME_RollRareItem_6FC53360(p,&drop);break;case 2:ITEMMODS_AssignProperty(0,nullptr,p,affixes+c.affix-1,0,0);break;case 3:{D2PropertyStrc prop{125,c.min,c.max,c.tree};D2COMMON_11292_ItemAssignProperty(0,nullptr,p,nullptr,0,0,&prop,0,0x40,nullptr);break;}}
printf("%s[%d,%d,%d,%d,",comma?",":"",result,item.flags,item.rarePrefix,item.rareSuffix);comma=true;List({item.data.wMagicPrefix[0],item.data.wMagicPrefix[1],item.data.wMagicPrefix[2]});printf(",");List({item.data.wMagicSuffix[0],item.data.wMagicSuffix[1],item.data.wMagicSuffix[2]});printf(",");List(item.applied);printf(",\"%llu\",%s,",(unsigned long long)item.seed.lSeed,item.has?"true":"false");Array(item.base);printf(",");Array(item.props);printf(",%d",draws);
D2PropertyStrc hp{13,1,5,0};D2COMMON_11292_ItemAssignProperty(0,nullptr,p,nullptr,0,0,&hp,0,0x40,nullptr);printf(",\"%llu\",",(unsigned long long)item.seed.lSeed);Array(item.props);printf("]");}printf("]\n");}
'''
 STAGE.mkdir(exist_ok=True);generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with (STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-Wno-literal-suffix','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True,timeout=120);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
 results=json.loads(run.stdout);assert len(results)==len(cases);out=STAGE/'cases.json';out.write_text(json.dumps(dict(inputs=cases,outputs=results,native=native,directStart=directStart),separators=(',',':'))+'\n')
 groups=dict(affix={n:hashlib.sha256(v.encode()).hexdigest()for n,v in bodies.items()},numeric=numericHashes,staff=staffEvidence['completeRoutineHashes'])
 record=dict(referenceVersion='3.3.93847',currentExecutableParity=False,cases=len(cases),originalNativeContexts=len(native),fullBaseContexts=692,directAffixContexts=988,treeControls=216,completeRoutineGroups=groups,
  treeAdaptedRoutineSha256=hashlib.sha256(newTree.encode()).hexdigest(),staffAdaptedRoutineSha256=staffEvidence['adaptedRoutineSha256'],approvedAdaptations=['item-tree-packing-decision.json','item-staffmods-tier-decision.json'],inputs=inputs,
  scope='Complete older magic/rare outer callers,weighted picker/group/type/rare-name services,affix property dispatcher/current-format wrapper and raw numeric/proc/charge writers compose on shared bit-identical item RNG. Approved current tree packing and required-level staffmods are separately identified variants. All692bases and all988selectable affix rows are included without a supported-property filter.',
  limits=['Namespace bridges explicitly supply immutable current tables,valid original item/type/base context,successful raw stat-list allocation and seed/graph transfers. Only affix mode0 and format100 are exercised;unused unique/set/gem/legacy property branches are not certified.',
   'Class ABI uses a neutral -1 sentinel for no class,distinct from valid Warlock7. Raw row ordinals remain reference identities,not current compiled IDs. The primary-jewel query explicitly maps the current jewl reference row to the reviewed symbolic enum;type hierarchy queries remain supplied services. Existing complete older first-socket rejection remains;modern socket eligibility/full materialization/current executable parity are not established. Approved adaptations do not claim hidden modern RNG/order.',
   'This is existing-graph affix composition. Full quality fallback/automagic/quantity,materialization,NPC graph/event/skill delivery,actors/learned skills remain required.'],
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in (generated,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',out,Path(__file__).resolve())})
 target=ROOT/'tools/reference/current-item-affixes-source-evidence.json';encoded=json.dumps(record,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Current affix source proof stale'
 else:target.write_text(encoded)
 print('Current affixes:',len(cases),'complete compositions;',len(native),'original contexts;692bases/988affixes/216tree controls;ASan+UBSan clean.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
