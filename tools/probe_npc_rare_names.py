#!/usr/bin/env python3
"""Complete source rare-name selector/eligibility/RNG on current and cap/format controls."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-rare-names-source';REF=ROOT/'tools/reference/d2r-named-equipment';RARE=ROOT/'tools/reference/item-generation'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={};bodies={}
 for rel,names in {'source/D2Game/src/ITEMS/ItemsMagic.cpp':['D2GAME_RollRareAffix_6FC53610'],'source/D2Common/src/Items/ItemMods.cpp':['ITEMMODS_CanItemHaveRareAffix'],'source/D2Common/include/D2Seed.h':['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber']}.items():
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p)
  for n in names:bodies[n]=function(raw.decode(),n)
 pins=json.loads((REF/'sources.json').read_text());tables={}
 for n in ('itemtypes','weapons','armor','misc'):
  p=REF/'json'/(n+'.json');assert sha(p)==pins['files']['json/'+n+'.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);tables[n]=json.loads(p.read_text())
 pins=json.loads((RARE/'sources.json').read_text())
 for n in ('raresuffix','rareprefix'):
  p=RARE/(n+'.json');assert sha(p)==pins['files'][n+'.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);tables[n]=json.loads(p.read_text())
 types={r['Code']:i+1 for i,r in enumerate(tables['itemtypes'].values())};suffixCount=len(tables['raresuffix']);prefixCount=len(tables['rareprefix']);assert (suffixCount,prefixCount)==(155,46)
 cpp=r'''
#include <cstdint>
#include <cstdio>
#include <vector>
#include <algorithm>
#define __fastcall
#define __stdcall
#define ARRAY_SIZE(x) (sizeof(x)/sizeof((x)[0]))
using BOOL=bool;constexpr bool TRUE=true,FALSE=false;
constexpr unsigned long long operator""i64(unsigned long long value){return value;}
union D2SeedStrc{uint64_t lSeed;struct{uint32_t nLowSeed,nHighSeed;};};
struct D2UnitStrc{int type=0,format=100;bool stackable=false,throwable=false;D2SeedStrc seed;}item;
struct D2RareAffixTxt{uint16_t wVersion=0,wIType[7]={},wEType[4]={};};
struct D2RareAffixDataTbl{D2RareAffixTxt*pRareAffixTxt=nullptr,*pRarePrefix=nullptr,*pRareSuffix=nullptr;int nRareAffixTxtRecordCount=0;}table;
struct D2RareAffixIndexAndPtrStrc{int nIndex=0;D2RareAffixTxt*pRareAffixRecord=nullptr;};
int parents[200][2]={};bool IsType(int child,int parent){return child==parent||(parents[child][0]&&IsType(parents[child][0],parent))||(parents[child][1]&&IsType(parents[child][1],parent));}
bool ITEMS_CheckItemTypeId(D2UnitStrc*p,int t){return IsType(p->type,t);}bool ITEMS_CheckIfStackable(D2UnitStrc*p){return p->stackable;}bool ITEMS_CheckIfThrowable(D2UnitStrc*p){return p->throwable;}int ITEMS_GetItemFormat(D2UnitStrc*p){return p->format;}
D2RareAffixDataTbl*DATATBLS_GetRareAffixDataTables(){return &table;}D2SeedStrc*ITEMS_GetItemSeed(D2UnitStrc*p){return &p->seed;}
'''+bodies['SEED_RollRandomNumber']+'\n'+bodies['SEED_RollLimitedRandomNumber']+r'''
uint32_t ITEMS_RollLimitedRandomNumber(D2SeedStrc*p,int bound){return SEED_RollLimitedRandomNumber(p,bound);}
'''+bodies['ITEMMODS_CanItemHaveRareAffix']+'\n'+bodies['D2GAME_RollRareAffix_6FC53610']+r'''
std::vector<D2RareAffixTxt> names;
void Set(int suffixes){table={names.data(),names.data()+suffixes,names.data(),(int)names.size()};}
void Pool(bool prefix){printf("[");auto begin=prefix?table.pRarePrefix:table.pRareSuffix;int count=prefix?table.nRareAffixTxtRecordCount-(table.pRarePrefix-table.pRareAffixTxt):table.pRarePrefix-table.pRareSuffix,kept=0;if(begin)for(int i=0;i<count;i++)if(ITEMMODS_CanItemHaveRareAffix(&item,&begin[i])&&kept<511){printf("%s%d",kept?",":"",(int)(&begin[i]-table.pRareAffixTxt)+1);kept++;}printf("]");}
int main(){
'''
 for r in tables['itemtypes'].values():
  for i,k in enumerate(('Equiv1','Equiv2')):
   if r.get(k):cpp+=f'parents[{types[r["Code"]]}][{i}]={types[r[k].strip()[:4]]};\n'
 cpp+=f'names.resize({suffixCount+prefixCount});\n'
 for n,offset in [('raresuffix',0),('rareprefix',suffixCount)]:
  for i,r in tables[n].items():
   at=offset+int(i);cpp+=f'names[{at}].wVersion={r.get("version",0)};\n'
   for p,field,maximum in [('itype','wIType',7),('etype','wEType',4)]:
    for j in range(1,maximum+1):
     if not r.get(p+str(j)):break
     cpp+=f'names[{at}].{field}[{j-1}]={types[r[p+str(j)].strip()[:4]]};\n'
 cpp+='Set(155);bool comma=false;printf("{\\"current\\":[");\n'
 bases={r['code']:r for n in ('weapons','armor','misc') for r in tables[n].values() if r.get('code')}
 for code,r in sorted(bases.items()):
  t=tables['itemtypes'][r['type']];cpp+=f'item.type={types[r["type"]]};item.stackable={int(bool(r.get("stackable")))};item.throwable={int(bool(t.get("Throwable")))};\n'
  cpp+=r'''for(int prefix:{0,1})for(int format:{0,99,100})for(uint64_t seed:{0ULL,2860518340611ULL,18446744073709551615ULL}){item.format=format;item.seed.lSeed=seed;int result=D2GAME_RollRareAffix_6FC53610(&item,prefix);if(comma)printf(",");comma=true;
'''+f'printf("[\\"{code}\\",%d,%d,\\"%llu\\",%d,\\"%llu\\",",prefix,format,(unsigned long long)seed,result,(unsigned long long)item.seed.lSeed);Pool(prefix);printf("]");'+'}\n'
 cpp+=r'''printf("],\"synthetic\":[");comma=false;
for(int size:{0,1,510,511,512,600,700})for(int prefix:{0,1})for(int format:{0,99,100})for(int version:{0,100})for(int stack:{0,1})for(int thrown:{0,1})for(int missing:{0,1})for(uint64_t seed:{0ULL,2860518340611ULL,18446744073709551615ULL}){
names.assign(std::max(size,1),{});for(auto&r:names){r.wVersion=version;r.wIType[0]='''+str(types['weap'])+r''';}Set(prefix?0:size);table.nRareAffixTxtRecordCount=size;
item.type='''+str(types['weap'])+r''';item.format=format;item.stackable=stack;item.throwable=thrown;item.seed.lSeed=seed;
if(missing){if(prefix)table.pRarePrefix=nullptr;else table.pRareSuffix=nullptr;}int result=D2GAME_RollRareAffix_6FC53610(&item,prefix);
if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,\"%llu\",%d,\"%llu\",",size,prefix,format,version,stack,thrown,missing,(unsigned long long)seed,result,(unsigned long long)item.seed.lSeed);
if(missing)printf("[]");else Pool(prefix);printf("]");
}printf("]}\n");}
'''
 source=STAGE/'source.cpp';source.write_text(cpp);binary=STAGE/'probe'
 with (STAGE/'compiler.log').open('w') as f:subprocess.run(['g++','-std=c++20','-O1','-g','-Wno-literal-suffix','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(source),'-o',str(binary)],stdout=f,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
 data=json.loads(run.stdout);assert len(data['current'])==12456 and len(data['synthetic'])==2016
 cases=STAGE/'cases.json';cases.write_text(json.dumps(data,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableCodeParity=False,currentCases=12456,syntheticCases=2016,bases=692,names=201,
  completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in (source,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',cases,Path(__file__).resolve())},
  established=['Complete rare-name caller and eligibility plus two complete seed routines execute on all692current bases/201current rare names with formats0/99/100,both sides,three seed states. Candidate lists,source ordinals and final seeds are emitted.',
               'Synthetic0/1/510/511/512/600/700pools verify cap511,old-format version/stackable/throwable refusal,missing selected table,no-pool no-draw and exact bounded uniform selection. No affix frequency or player implemented-property filter applies.'],
  services=['Current item-type ancestry and item/base/table lookup,format,stackable and primary-type Throwable fields are supplied services. Table concatenation is reviewed older suffix-then-prefix layout;ordinals are extraction/source identities,not claims about modern binary IDs.',
            'Ordered pool diagnostics call the same complete eligibility helper without mutating item seed;they do not replace the complete selector. Current full table loader/localized string resolution and global item seed allocation are not executed.'],
  limits=['No generated NPC item/property graph,quality fallback,final initialization,AI/casting/actor/lifecycle/input/save or modern executable/client certificate. No learned Shadow/Valkyrie handler is enabled.'])
 out=ROOT/'tools/reference/npc-rare-names-source-evidence.json';rendered=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==rendered,'Rare-name evidence stale'
 else:out.write_text(rendered)
 print('NPC rare names:12456current/2016synthetic complete source controls;ASan+UBSan clean;no generated item.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
