#!/usr/bin/env python3
"""Execute full legacy affix selection against the pinned current tables, with declared item services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';REF=ROOT/'tools/reference/d2r-named-equipment'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^(?:static |inline )?[A-Za-z_][^;\n]*\b'+name+r'\([^;{]*?\)\s*\n\{',text,re.M);assert m,name;end=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
  block=text[m.start():end];blocks[name]=hashlib.sha256(block.encode()).hexdigest();return block+'\n'
 text=source('source/D2Game/src/ITEMS/ItemsMagic.cpp');names=['ITEMS_ComputeCraftedMagicAffixLevel','ITEMMODS_AffixPlayerClassisCompatible','ITEMMODS_HasAffixWithGroup','ComputeAffixFrequency','MagicItemRollAffix','ITEMS_RollMagicAffixesNew'];f={n:routine(text,n) for n in names}
 can=routine(source('source/D2Common/src/Items/ItemMods.cpp'),'ITEMMODS_CanItemHaveMagicAffix')
 seed=source('source/D2Common/include/D2Seed.h');rng=routine(seed,'SEED_RollRandomNumber')+routine(seed,'SEED_RollLimitedRandomNumber')
 catalogue=json.loads((ROOT/'data/Sanctuary/Balance/valkyrie-affix-catalogue.json').read_text());meta=json.loads((REF/'sources.json').read_text());tables={};pins={}
 for name in ['magicprefix','magicsuffix','itemtypes','properties','weapons','armor','misc']:
  raw=(REF/'json'/f'{name}.json').read_bytes();pins[name]=hashlib.sha256(raw).hexdigest();assert pins[name]==meta['files']['json/'+name+'.json']['sha256'];tables[name]=json.loads(raw)
 types={r['Code']:i+1 for i,r in enumerate(tables['itemtypes'].values()) if r.get('Code')};classes={name:i for i,name in enumerate(['ama','sor','nec','pal','bar','dru','ass','war'])};props={k.casefold():i for i,k in enumerate(tables['properties'])};suffixCount=max(map(int,tables['magicsuffix']))+1;prefixCount=max(map(int,tables['magicprefix']))+1;count=suffixCount+prefixCount
 cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <initializer_list>
#include <string>
#include <vector>
#define __fastcall
#define __stdcall
#define ARRAY_SIZE(x) (sizeof(x)/sizeof((x)[0]))
#define D2Clamp std::clamp
using BOOL=bool;constexpr bool FALSE=false,TRUE=true;
constexpr int UNIT_ITEM=4,ITEMS_MAX_MODS=3,HARDCODEDCST_MAX_LEVEL=99,PCLASS_INVALID=-1,ITEMQUAL_RARE=6,ITEMQUAL_CRAFT=8,ITEMQUAL_TEMPERED=9,STAT_ITEM_NUMSOCKETS=1,PROPMODE_AFFIX=0;
constexpr unsigned long long operator""i64(unsigned long long value){return value;}
union D2SeedStrc{uint64_t lSeed;struct{uint32_t nLowSeed,nHighSeed;};};
struct D2PropertyStrc{int nProperty=0;};
struct D2MagicAffixTxt{int wVersion=0,wSpawnable=0,dwLevel=0,dwGroup=0,dwMaxLevel=0,nRare=0,wFrequency=0;uint8_t nClassSpecific=255;D2PropertyStrc pProperties[3];uint16_t wEType[5]={},wIType[7]={};};
struct D2ItemData{uint16_t wMagicPrefix[3]={},wMagicSuffix[3]={};};
struct D2UnitStrc{int dwUnitType=UNIT_ITEM,dwClassId=0,format=100,quality=6,level=25,qlvl=1,magic=0,type=0,itemClass=-1;D2ItemData* pItemData=nullptr;D2SeedStrc seed;}item;
struct D2ItemsTxt{int nMagicLevel=0;}basis;
struct D2PropertiesTxt{int wStat[1]={};};
struct D2MagicAffixDataTbl{D2MagicAffixTxt* pMagicAffixTxt;D2MagicAffixTxt* pMagicPrefix;D2MagicAffixTxt* pMagicSuffix;D2MagicAffixTxt* pAutoMagic;int nMagicAffixTxtRecordCount;};
struct D2MagicAffixIndexAndPtrStrc{int nAffixId;const D2MagicAffixTxt* pMagicAffixRecord;};
'''+f'constexpr int recordCount={count},suffixCount={suffixCount};\nD2MagicAffixTxt affixes[recordCount];std::string ids[recordCount];\nD2PropertiesTxt propertyRows[{len(props)+1}];\n'+r'''
D2MagicAffixDataTbl magicTables{affixes,affixes+suffixCount,affixes,affixes+recordCount,recordCount};
struct Tables{int nPropertiesTxtRecordCount;D2PropertiesTxt* pPropertiesTxt;}tables;
auto* sgptDataTables=&tables;
int parents[200][2]={};
bool IsType(int current,int wanted){if(current==wanted)return true;if(current<=0 || current>=200)return false;for(int p:parents[current])if(p && IsType(p,wanted))return true;return false;}
D2MagicAffixDataTbl* DATATBLS_GetMagicAffixDataTables(){return &magicTables;}
D2MagicAffixTxt* DATATBLS_GetMagicAffixTxtRecord(int id){return id>0 && id<=recordCount?&affixes[id-1]:nullptr;}
int ITEMS_GetItemQlvl(D2UnitStrc*p){return p->qlvl;}
int ITEMS_GetItemLevel(D2UnitStrc*p){return p->level;}
int ITEMS_GetItemFormat(D2UnitStrc*p){return p->format;}
int ITEMS_GetItemQuality(D2UnitStrc*p){return p->quality;}
int ITEMS_GetClassOfClassSpecificItem(D2UnitStrc*p){return p->itemClass;}
D2SeedStrc* ITEMS_GetItemSeed(D2UnitStrc*p){return &p->seed;}
D2ItemsTxt* DATATBLS_GetItemsTxtRecord(int){basis.nMagicLevel=item.magic;return &basis;}
bool ITEMS_CheckItemTypeId(D2UnitStrc*p,int id){return IsType(p->type,id);}
bool ITEMS_CheckIfStackable(D2UnitStrc*){return false;}
bool ITEMS_CheckIfThrowable(D2UnitStrc*){return false;}
bool ITEMS_CheckIfSocketable(D2UnitStrc*){return true;}
int ITEMS_GetMaxSockets(D2UnitStrc*){return 3;}
void ITEMMODS_AssignProperty(int,int,D2UnitStrc*,const D2MagicAffixTxt*,int,int){std::abort();}
'''+rng+f['ITEMS_ComputeCraftedMagicAffixLevel']+f['ITEMMODS_AffixPlayerClassisCompatible']+f['ITEMMODS_HasAffixWithGroup']+f['ComputeAffixFrequency']+can+'\nnamespace original_picker{\n'+f['MagicItemRollAffix']+'\n}\n'+r'''
std::vector<int> poolIds,poolWeights;int totalWeight=0;
D2MagicAffixIndexAndPtrStrc* MagicItemRollAffix(D2UnitStrc*p,int magic,D2MagicAffixIndexAndPtrStrc* pool,int count,int total){poolIds.clear();poolWeights.clear();totalWeight=total;for(int i=0;i<count;i++){poolIds.push_back(pool[i].nAffixId);poolWeights.push_back(ComputeAffixFrequency(magic,pool[i].pMagicAffixRecord));}return original_picker::MagicItemRollAffix(p,magic,pool,count,total);}
'''+f['ITEMS_RollMagicAffixesNew']+'\nint main(){\n'
 for key,r in tables['itemtypes'].items():
  if not r.get('Code'):continue
  for i,field in enumerate(['Equiv1','Equiv2']):
   if r.get(field):cpp+=f'parents[{types[r["Code"]]}][{i}]={types[r[field].strip()[:4]]};\n'
 for index,(key,row) in enumerate(tables['properties'].items()):
  if row.get('stat1')=='item_numsockets':cpp+=f'propertyRows[{index}].wStat[0]=STAT_ITEM_NUMSOCKETS;\n'
 cpp+=f'tables.nPropertiesTxtRecordCount={len(props)+1};tables.pPropertiesTxt=propertyRows;\n'
 for table,offset,kind in [('magicsuffix',0,'MagicSuffix'),('magicprefix',suffixCount,'MagicPrefix')]:
  for idx,row in tables[table].items():
   at=offset+int(idx);cpp+=f'ids[{at}]="{kind}:{idx}";\n'
   for field,value in [('wVersion',row.get('version',0)),('wSpawnable',int(bool(row.get('spawnable')))),('dwLevel',row.get('level',0)),('dwMaxLevel',row.get('maxlevel',0)),('dwGroup',row.get('group',0)),('nRare',int(bool(row.get('rare')))),('wFrequency',row.get('frequency',0)),('nClassSpecific',classes.get(row.get('classspecific'),255))]:
    if value or field=='nClassSpecific':cpp+=f'affixes[{at}].{field}={value};\n'
   if row.get('mod1code'):cpp+=f'affixes[{at}].pProperties[0].nProperty={props[row["mod1code"].casefold()]};\n'
   # Inactive nameless rows use a valid neutral property sentinel. Their zero
   # frequency prevents selection; no invalid negative pointer behavior is claimed.
   for prefix,member,maximum in [('itype','wIType',7),('etype','wEType',5)]:
    for i in range(1,maximum+1):
     if not row.get(prefix+str(i)):break
     value=types[row[prefix+str(i)].strip()[:4]];cpp+=f'affixes[{at}].{member}[{i-1}]={value};\n'
 cpp+=r'''bool first=true;std::printf("[");
'''
 for b in catalogue['bases']:
  cpp+=f'item.qlvl={b["qualityLevel"]};item.magic={b["magicLevel"]};item.type={types[b["type"]]};item.itemClass={classes.get(b["requiredClass"],-1)};\n'
  cpp+=r'''for(int level:{1,25,46,99,103,787})for(int quality:{4,6})for(int prefix:{0,1})for(uint64_t initial:{0ULL,2860518340611ULL,18446744073709551615ULL})for(int existing:{0,1})for(int force:{0,1}){
D2ItemData data;item.pItemData=&data;item.level=level;item.quality=quality;item.seed.lSeed=initial;
// A source-selected earlier affix supplies a real group exclusion input.
int previous=0;if(existing){poolIds.clear();previous=ITEMS_RollMagicAffixesNew(&item,1,1,0,1,0,0);if(previous)data.wMagicPrefix[0]=previous;}
item.seed.lSeed=initial;poolIds.clear();poolWeights.clear();totalWeight=0;
int selected=ITEMS_RollMagicAffixesNew(&item,1,force,0,prefix,0,0);uint64_t finalSeed=item.seed.lSeed;basis.nMagicLevel=item.magic;
if(!first)std::printf(",");first=false;
'''+f'std::printf("[\\"{b["code"]}\\",%d,%d,%d,\\"%llu\\",%d,\\"%s\\",%d,\\"%s\\",\\"%llu\\",%d,[",level,quality,prefix,(unsigned long long)initial,existing,previous?ids[previous-1].c_str():"",ITEMS_ComputeCraftedMagicAffixLevel(&item,&basis),selected?ids[selected-1].c_str():"",(unsigned long long)finalSeed,totalWeight);\n'+r'''
for(size_t i=0;i<poolIds.size();i++){std::printf(i?",[\"%s\",%d]":"[\"%s\",%d]",ids[poolIds[i]].c_str(),poolWeights[i]);}std::printf("],%d]",force);
}
'''
 cpp+='std::printf("]\\n");}\n'
 p=STAGE/'valkyrie-affix-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O2','-Wno-literal-suffix',str(p),'-o',str(binary)],check=True)
 fixture=STAGE/'valkyrie-affix-source-probe.json';rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==5184,len(rows);fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 # No Python clone supplies the selection expectation. The unchanged functions
 # above emit selected source references, complete pool/weights and final seeds.
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,compiledUnchangedRoutines=blocks,pinnedTableHashes=pins,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(rows)),established=['Complete unchanged new-affix selector, level/frequency/weighted picker, existing-group/class-compatibility, item-type/property eligibility and two seed helpers execute against all current affix rows and all18 Valkyrie bases.','Each fixture records the whole ordered eligible pool with weights, exact selected source reference and final seed. Real earlier source-selected prefix groups are excluded. Normal/raw levels1/25/46/99/103/787, magic/rare qualities, both affix kinds three seed states, and forced/unforced attempts are covered.','The complete old weighted picker draws from totalWeight+1, with the endpoint assigned to the last eligible row. Even forced selection consumes the two-way seed draw first.','Raw item level is retained; only affix level clamps1..99. Circlet magic levels multiply each affix frequency by its level. Player supported-property filtering is not applied.'],services=['Item format100, current base/row/property lookup, current-equivalence item-type classifier and existing item affix storage are declared services. No full source table loader, item creation or property execution is claimed.','The original weighted picker is enclosed in a namespace, and a wrapper records its input pool before delegating. Its body is unchanged. All selected references are pinned extraction row IDs, not assertions about modern binary affix IDs.','Inactive nameless/zero-frequency rows use a valid neutral first-property lookup sentinel to avoid undefined old negative pointer access. They cannot be selected. The modern table parser and invalid-record behavior remain outside this proof.'],limits=['This is affix selection only. Magic/rare outer orchestration, rare names, property value generation/stat application, item-level side effects, actual NPC inventory/combat/casting/lifecycle/saves and modern/client acceptance remain pending. No learned Valkyrie handler is enabled.','Full legacy CanItemHaveMagicAffix rejects a first socket property independently of the no-sockets summon request. This suspicious old behavior is preserved as bounded supplementary evidence; modern socket-affix eligibility is explicitly uncertain.','No original global item/owner seed allocation or exact modern RNG stream claim. No class completion certificate.'])
 out=ROOT/'tools/reference/valkyrie-affix-engine-evidence.json';rendered=json.dumps(evidence,indent=2)+'\n'
 if check:assert out.read_text()==rendered
 else:out.write_text(rendered)
 print(f'Valkyrie affixes:{len(rows)} complete source selection/pool/weight/seed cases; native comparison remains separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
