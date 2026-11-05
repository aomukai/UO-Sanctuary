#!/usr/bin/env python3
"""Execute complete proc/charge writers with current metadata and explicit services."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'item-skill-property-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);inputs={};tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 previous=ROOT/'tools/reference/item-property-creation-source-evidence.json';prior=json.loads(previous.read_text());inputs[str(previous.relative_to(ROOT))]=sha(previous)
 source=BASE/'item-property-creation-source/source.cpp';assert sha(source)==prior['artifacts'][str(source.relative_to(ROOT))];inputs[str(source.relative_to(ROOT))]=sha(source);cpp=source.read_text().split('bool first=true;\nvoid Case(')[0]
 bodies={}
 for rel,names in {'source/D2Common/src/Items/ItemMods.cpp':['ITEMMODS_PropertyFunc11','ITEMMODS_PropertyFunc19'],'source/D2Common/src/D2Skills.cpp':['SKILLS_GetRequiredLevel'],'source/D2Common/src/DataTbls/SkillsTbls.cpp':['DATATBLS_GetSkillsTxtRecord'],'source/D2Common/src/D2StatList.cpp':['STATLIST_SetStatIfListIsValid']}.items():
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p)
  for n in names:bodies[n]=function(raw.decode(),n)
 refs=ROOT/'tools/reference/d2r-named-equipment';pins=json.loads((refs/'sources.json').read_text());tables={}
 for n in ('skills','itemstatcost'):
  p=refs/'json'/(n+'.json');assert sha(p)==pins['files']['json/'+n+'.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);tables[n]=json.loads(p.read_text())
 catalogPath=ROOT/'data/Sanctuary/Balance/item-skill-properties.json';catalog=json.loads(catalogPath.read_text());inputs[str(catalogPath.relative_to(ROOT))]=sha(catalogPath);assert len(catalog['Skills'])==429 and catalog['ChargedLevelBits']==6
 for r in catalog['Skills']:
  raw=tables['skills'][str(r['Id'])];assert (r['Name'],r['RequiredLevel'],r['MaximumLevel'])==(raw.get('skill',''),raw.get('reqlevel',0),raw.get('maxlvl',0))
 plans=json.loads((ROOT/'data/Sanctuary/Balance/equipment-property-plans.json').read_text());eligible=[p for p in plans if p['Steps'] and all(s['Function'] in (11,19) for s in p['Steps'])];assert len(eligible)==7
 cpp=cpp.replace('dwClassId=0;Seed pSeed','dwClassId=0,level=1;Seed pSeed')
 structure='struct Tables{int nPropertiesTxtRecordCount=285;D2PropertiesTxt*pPropertiesTxt=properties.data();}tables;'
 extra='struct D2SkillsTxt{int wReqLevel=0,wMaxLvl=0;};std::array<D2SkillsTxt,429> skillRows;\nstruct Tables{int nPropertiesTxtRecordCount=285;D2PropertiesTxt*pPropertiesTxt=properties.data();D2SkillsTxt*pSkillsTxt=skillRows.data();int nSkillsTxtRecordCount=429,nStuff=6,nShiftedStuff=63;}tables;'
 assert structure in cpp;cpp=cpp.replace(structure,extra)
 setter='void STATLIST_SetStatIfListIsValid(D2StatListStrc*l,int id,int value,int layer){l->values[Key(id,layer)]=value;trace.push_back({4,id,value,layer});}'
 replacement='void STATLIST_SetStat(D2StatListStrc*l,int id,int value,uint16_t layer){l->values[Key(id,layer)]=value;trace.push_back({4,id,value,layer});}\n'+bodies['STATLIST_SetStatIfListIsValid']
 assert setter in cpp;cpp=cpp.replace(setter,replacement)
 cpp+='\nint ITEMS_GetItemLevel(D2UnitStrc*p){return p->level;}\n'+bodies['DATATBLS_GetSkillsTxtRecord']+'\n'+bodies['SKILLS_GetRequiredLevel']+'\n'+bodies['ITEMMODS_PropertyFunc11']+'\n'+bodies['ITEMMODS_PropertyFunc19']+'\n'
 cpp+='void Skills(){\n'
 for r in catalog['Skills']:cpp+=f'skillRows[{r["Id"]}]={{{r["RequiredLevel"]},{r["MaximumLevel"]}}};\n'
 cpp+='}\nint encoded[]={'+','.join(str(p['Ordinal']) for p in eligible)+'};\n'
 cpp+=r'''
bool comma=false;
void Case(int property,int skill,int level,int minimum,int maximum,int bits,uint64_t seed,int alloc,int req=-1,int max=-1){
Skills();if(req>=0&&skill>=0&&skill<429)skillRows[skill]={req,max};tables.nStuff=bits;tables.nShiftedStuff=(1<<bits)-1;
D2UnitStrc item;item.level=level;item.pSeed.real=true;item.pSeed.value=seed;allocate=alloc;child.values.clear();trace.clear();
D2PropertyStrc p{property,minimum,maximum,skill};D2COMMON_11292_ItemAssignProperty(0,nullptr,&item,nullptr,0,0,&p,0,0x40,nullptr);
if(comma)printf(",");comma=true;printf("[[%d,%d,%d,%d,%d,%d,\"%llu\",%d,%d,%d],",property,skill,level,minimum,maximum,bits,(unsigned long long)seed,alloc,req,max);Array(child);printf(",\"%llu\",%d,[",(unsigned long long)item.pSeed.value,item.pSeed.count);bool t=false;for(auto x:trace){printf("%s[%d,%d,%d,%d]",t?",":"",x[0],x[1],x[2],x[3]);t=true;}printf("]]");}
int main(){Init();Skills();off_6FDE3920[11]=ITEMMODS_PropertyFunc11;off_6FDE3920[19]=ITEMMODS_PropertyFunc19;
printf("{\"current\":[");
for(int skill=0;skill<429;skill++)for(int property:encoded)for(int level:{1,99,787})for(uint64_t seed:{0ULL,2860448284845ULL})for(int alloc:{0,1})Case(property,skill,level,property==244?-30:0,property==244?-8:0,6,seed,alloc);
printf("],\"npc\":[");comma=false;
'''
 npcPath=ROOT/'data/Sanctuary/Balance/npc-item-affix-catalogue.json';npc=json.loads(npcPath.read_text());inputs[str(npcPath.relative_to(ROOT))]=sha(npcPath);codes={p['Code']:p['Ordinal'] for p in eligible};specs=sorted({(m['code'],int(m['parameter']),m['min'],m['max']) for a in npc['affixes'] for m in a['definition']['modifiers'] if m['code'] in codes});assert len(specs)==131,len(specs)
 for code,skill,minimum,maximum in specs:cpp+=f'for(int level:{{1,25,50,99,787}})for(uint64_t seed:{{0ULL,2860448284845ULL,18446744073709551615ULL}})for(int alloc:{{0,1}})Case({codes[code]},{skill},level,{minimum},{maximum},6,seed,alloc);\n'
 cpp+=r'''printf("],\"synthetic\":[");comma=false;
int pairs[][2]={{-256,-64},{-30,-8},{-1,-1},{0,0},{1,1},{5,63},{255,64},{300,255},{0,-8},{5,-1},{1,0},{-30,0},{255,-1},{300,0}};
for(int property:{127,244})for(int req:{0,30,99,100})for(int max:{0,1,20,70})for(auto p:pairs)for(int level:{1,30,99,787})for(int bits:{1,6,8})for(uint64_t seed:{0ULL,2860448284845ULL,18446744073709551615ULL})for(int alloc:{0,1})Case(property,428,level,p[0],p[1],bits,seed,alloc,req,max);
printf("],\"missing\":[");comma=false;
for(int property:encoded)for(int skill:{-1,429})for(int level:{1,30,99,787})for(uint64_t seed:{0ULL,2860448284845ULL,18446744073709551615ULL})for(int alloc:{0,1})Case(property,skill,level,-30,-8,6,seed,alloc);
printf("]");
'''
 freshCalls=[('att-skill',36,0,0),('hit-skill',48,12,4),('gethit-skill',38,10,3),('kill-skill',66,5,1),('death-skill',62,0,-8),('levelup-skill',53,0,64),('charged',101,-30,-8),('att-skill',36,7,0),('charged',101,-30,-8),('charged',429,-30,-8)]
 fresh=r'''void Fresh(int level,uint64_t seed){Skills();tables.nStuff=6;tables.nShiftedStuff=63;D2UnitStrc item;item.level=level;item.pSeed.real=true;item.pSeed.value=seed;allocate=true;child.values.clear();trace.clear();
'''
 for i,(code,skill,minimum,maximum) in enumerate(freshCalls):fresh+=f'D2PropertyStrc p{i}{{{codes[code]},{minimum},{maximum},{skill}}};D2COMMON_11292_ItemAssignProperty(0,nullptr,&item,nullptr,0,0,&p{i},0,0x40,nullptr);\n'
 fresh+=r'''if(comma)printf(",");comma=true;printf("[%d,\"%llu\",\"%llu\",%d,",level,(unsigned long long)seed,(unsigned long long)item.pSeed.value,item.pSeed.count);Array(child);
D2PropertyStrc next{244,-30,-8,101};D2COMMON_11292_ItemAssignProperty(0,nullptr,&item,nullptr,0,0,&next,0,0x40,nullptr);
printf(",\"%llu\",%d,",(unsigned long long)item.pSeed.value,item.pSeed.count);Array(child);printf("]");}
'''
 cpp=cpp.replace('int main(){Init();Skills();',fresh+'\nint main(){Init();Skills();')
 cpp+=r'''printf(",\"fresh\":[");comma=false;for(int level:{1,47,99,787})for(uint64_t seed:{0ULL,2860448284845ULL,18446744073709551615ULL})Fresh(level,seed);printf("]}\n");}
'''
 for n,b in bodies.items():assert function(cpp,n)==b,n
 file=STAGE/'source.cpp';file.write_text(cpp);binary=STAGE/'probe'
 with (STAGE/'compiler.log').open('w') as f:subprocess.run(['g++','-std=c++20','-O1','-g1','-Wno-literal-suffix','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(file),'-o',str(binary)],stdout=f,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
 data=json.loads(run.stdout);counts={k:len(v) for k,v in data.items()};assert counts==dict(current=36036,npc=3930,synthetic=32256,missing=336,fresh=12),counts
 data['calls']=freshCalls
 output=STAGE/'cases.json';output.write_text(json.dumps(data,separators=(',',':'))+'\n')
 loaderPath=BASE/'d2moo-pinned-source/source/D2Common/src/DataTbls/ItemsTbls.cpp';loaderRaw=loaderPath.read_bytes();assert hashlib.sha1(b'blob '+str(len(loaderRaw)).encode()+b'\0'+loaderRaw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']=='source/D2Common/src/DataTbls/ItemsTbls.cpp');inputs[str(loaderPath.relative_to(ROOT))]=sha(loaderPath)
 assert 'sgptDataTables->nShiftedStuff = (1 << sgptDataTables->nStuff) - 1;' in loaderRaw.decode()
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,inputs=inputs,newCompleteRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},
  inheritedCompleteRoutineHashes=prior['completeRoutineHashes'],cases=counts,totalCases=sum(counts.values()),plans=7,currentSkillRows=429,blankAddressableSkill=362,chargedLevelBits=6,loaderReviewOnly=True,
  artifacts={str(p.relative_to(ROOT)):sha(p) for p in (file,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',output,Path(__file__).resolve())},
  established=['Complete property11/property19,required-level lookup,bounded skill-record lookup and null-safe setter execute unchanged. All20inherited writer/helper/LCG routine copies remain in the supplied harness.',
               'Current429rows include addressable blank362. All7plans and all131unique NPC proc/charge modifiers are checked over item levels,seed states and allocation success/failure. Additional controls cover default/negative/explicit levels,chance defaults,capacity1..255,fill intervals,level-bit masks,uint16layer conversion and missing skill records.',
               'Property11 adds chance on encoded skill/level layers;creation consumes no RNG. Property19 rolls before allocation and sets capacity/current charges even though a null list suppresses only the write. It consumes a draw for capacity1.'],
  services=['Valid item type4,current immutable skill/table/stat/property lookups and allocation are explicit. Source complete LCG functions supply every draw. Metadata lookup counts are not compared as observable world events.',
            'Table loader review supplies strength-row stuff6,with older fallback6for out-of-range1..8. Charge packing uses that explicit bit context;the stat setter has a uint16layer parameter. Synthetic8bit cases exercise truncation,not a new modern configuration.',
            'This is raw property encoding. No event subscription/trigger ordering,NPC proc spell execution,charge-backed available-skill list,charge spending/recharge,full item birth/quality fallback or public summon handler is certified.'],
  remaining=['Current executable parity and full actor event/skill/charge consumers remain open. Existing NpcCombatEvents refusal gates must remain until those consumers are verified. Numeric creation/original-item admission masks remain unchanged;encoded skill application uses a separate explicit API.',
             'Ordinary skilltab adaptation question and modern war-tab-rand remain unresolved;unrelated skill-property encoding does not settle them. No class completion,asset creation or deployment.'])
 target=ROOT/'tools/reference/item-skill-property-source-evidence.json';text=json.dumps(evidence,indent=2)+'\n'
 if check:assert target.read_text()==text,'Item skill-property evidence stale'
 else:target.write_text(text)
 print('Item skill properties:72558writer/12fresh source controls;7plans/429rows/131NPCspecs;5new+20inherited routines;ASan+UBSan clean;actor consumers remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
