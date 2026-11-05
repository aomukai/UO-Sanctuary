#!/usr/bin/env python3
"""Execute fresh numeric/layered property creation with unchanged source writers."""
import argparse,ast,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'item-property-creation-source'
FUNCTIONS={1,2,3,4,5,6,7,8,10,15,16,17,20,21}
RANGES=[(1,3,0),(3,1,0),(0,5,0),(-2,2,8),(0,0,23),(1,2,21),(1,1,3),(2,7,7)]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);inputs={};tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 path=BASE/'d2moo-pinned-source/source/D2Common/src/Items/ItemMods.cpp';raw=path.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']=='source/D2Common/src/Items/ItemMods.cpp');inputs[str(path.relative_to(ROOT))]=sha(path);mods=raw.decode()
 old=ROOT/'tools/reference/item-property-writers-source-evidence.json';baseline=json.loads(old.read_text());inputs[str(old.relative_to(ROOT))]=sha(old)
 template=ROOT/'tools/probe_item_property_writers.py';assert sha(template)==baseline['inputs'][str(template.relative_to(ROOT))];inputs[str(template.relative_to(ROOT))]=sha(template)
 # Reuse its accepted fixture-service declarations. No previous receipt or
 # diagnostic file is fabricated when old ignored artifacts are unavailable.
 syntax=ast.parse(template.read_text());cpp=next(n.value.value for n in ast.walk(syntax) if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='cpp' for t in n.targets) and isinstance(n.value,ast.Constant))
 cpp+='\n#define FALSE 0\nconstexpr int STAT_ITEM_INDESCTRUCTIBLE=152;\n'
 seedPath=BASE/'d2moo-pinned-source/source/D2Common/include/D2Seed.h';seedRaw=seedPath.read_bytes();assert hashlib.sha1(b'blob '+str(len(seedRaw)).encode()+b'\0'+seedRaw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']=='source/D2Common/include/D2Seed.h');inputs[str(seedPath.relative_to(ROOT))]=sha(seedPath)
 seeds={n:function(seedRaw.decode(),n) for n in ('SEED_RollRandomNumber','SEED_RollLimitedRandomNumber')}
 cpp=cpp.replace('struct Seed{int count=0;};','struct Seed{int count=0;uint64_t value=0;bool real=false;};')
 service='int SEED_RollLimitedRandomNumber(Seed*s,int bound){assert(bound>0);int value=s->count++%bound;trace.push_back({6,bound,value,0});return value;}'
 actual=r'''namespace source_seed {
constexpr unsigned long long operator""i64(unsigned long long value){return value;}
union D2SeedStrc{uint64_t lSeed;struct{uint32_t nLowSeed,nHighSeed;};};
'''+ '\n'.join(seeds.values())+r'''
}
int SEED_RollLimitedRandomNumber(Seed*s,int bound){assert(bound>0);int value=0;if(s->real){source_seed::D2SeedStrc seed;seed.lSeed=s->value;value=source_seed::SEED_RollLimitedRandomNumber(&seed,bound);s->value=seed.lSeed;s->count++;}else value=s->count++%bound;trace.push_back({6,bound,value,0});return value;}
'''
 assert service in cpp;cpp=cpp.replace(service,actual)
 planspath=ROOT/'data/Sanctuary/Balance/equipment-property-plans.json';plans=json.loads(planspath.read_text());inputs[str(planspath.relative_to(ROOT))]=sha(planspath)
 fixture=BASE/'item-property-writers-source-probe.json';assert sha(fixture)==baseline['artifacts'][str(fixture.relative_to(ROOT))];prior=json.loads(fixture.read_text());inputs[str(fixture.relative_to(ROOT))]=sha(fixture)
 assert plans==[dict(Code=p['Code'],Ordinal=p['Ordinal'],Supported=p['Supported'],Steps=[dict(Function=f,Stat=s,Set=bool(n),Value=v) for f,s,n,v in p['Steps']]) for p in prior['plans']]
 properties=ROOT/'tools/reference/d2r-named-equipment/json/properties.json';assert sha(properties)==baseline['inputs'][str(properties.relative_to(ROOT))];inputs[str(properties.relative_to(ROOT))]=sha(properties);rows=json.loads(properties.read_text())
 costs=ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json';assert sha(costs)==baseline['inputs'][str(costs.relative_to(ROOT))];inputs[str(costs.relative_to(ROOT))]=sha(costs);definitions=json.loads(costs.read_text());ids={r['Name']:r['Id'] for r in definitions}
 for plan in plans:
  row=rows[plan['Code']];assert row['lineNumber']==plan['Ordinal']
  steps=[dict(Function=row['func'+str(i)],Stat=ids.get(row.get('stat'+str(i)),-1),Set=bool(row.get('set'+str(i))),Value=row.get('val'+str(i),0)) for i in range(1,8) if row.get('func'+str(i))]
  assert steps==plan['Steps'],plan['Code']
 creation=[dict(Code=p['Code'],Ordinal=p['Ordinal'],Creatable=bool(p['Steps']) and p['Code']!='war-tab-rand' and all(s['Function'] in FUNCTIONS for s in p['Steps'])) for p in plans];assert sum(p['Creatable'] for p in creation)==202
 names=['sub_6FD92CF0','ITEMMODS_ApplyEthereality','ITEMMODS_AddPropertyToItemStatList']+['ITEMMODS_PropertyFunc'+str(n).zfill(2) for n in sorted(FUNCTIONS)]+['D2COMMON_11292_ItemAssignProperty'];bodies={n:function(mods,n) for n in names}
 for n,h in baseline['compiledUnchangedRoutineHashes'].items():assert hashlib.sha256(bodies[n].encode()).hexdigest()==h,n
 cpp+='\n'.join(bodies.values())+'\nvoid Init(){\n'
 for c in definitions:cpp+=f'costs[{c["Id"]}].nValShift={c["Shift"]};\n'
 for p in plans:
  for i,s in enumerate(p['Steps']):cpp+=f'properties[{p["Ordinal"]}].nFunc[{i}]={s["Function"]};properties[{p["Ordinal"]}].nSet[{i}]={int(s["Set"])};properties[{p["Ordinal"]}].wStat[{i}]={s["Stat"]};properties[{p["Ordinal"]}].wVal[{i}]={s["Value"]};\n'
 synthetic=[dict(Function=1,Stat=83,Set=False,Value=0),dict(Function=10,Stat=188,Set=False,Value=21),dict(Function=21,Stat=83,Set=False,Value=7)]
 for i,s in enumerate(synthetic):cpp+=f'properties[121].nFunc[{i}]={s["Function"]};properties[121].wStat[{i}]={s["Stat"]};properties[121].wVal[{i}]={s["Value"]};\n'
 for n in sorted(FUNCTIONS):cpp+=f'off_6FDE3920[{n}]=ITEMMODS_PropertyFunc{n:02d};\n'
 cpp+='}\nint eligible[]={'+','.join(str(p['Ordinal']) for p in creation if p['Creatable'])+'};\nD2ItemsTxt bases[]={'+','.join('{'+','.join(map(str,r['Values']))+'}' for r in prior['bases'])+'};\n'
 cpp+=r'''
void Array(D2StatListStrc&l){printf("[");bool first=true;for(auto [id,value]:l.values){printf("%s[%u,%d]",first?"":",",id,value);first=false;}printf("]");}
bool first=true;
void Case(int prop,int b,int mode,int minimum,int maximum,int parameter,int eth,int alloc){
D2UnitStrc item;item.weapon=b!=3;item.throwable=b==2;basis=bases[b];allocate=alloc;child.values.clear();trace.clear();
for(auto pair:{std::array{21,basis.nMinDam},std::array{22,basis.nMaxDam},std::array{23,basis.n2HandMinDam},std::array{24,basis.n2HandMaxDam},std::array{159,basis.nMinMisDam},std::array{160,basis.nMaxMisDam},std::array{31,basis.dwMinAc}}){if(pair[1])item.base.values[Key(pair[0],0)]=pair[1];}
D2PropertyStrc p{prop,minimum,maximum,parameter};D2COMMON_11292_ItemAssignProperty(mode,nullptr,&item,nullptr,0,0,&p,0,0x40,nullptr);if(eth)ITEMMODS_ApplyEthereality(&item);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d],",first?"":",",prop,b,mode,minimum,maximum,parameter,eth,alloc);first=false;Array(item.base);printf(",");Array(child);printf(",%d,[",item.pSeed.count);bool comma=false;for(auto t:trace){printf("%s[%d,%d,%d,%d]",comma?",":"",t[0],t[1],t[2],t[3]);comma=true;}printf("]]");}
int main(){Init();printf("[");
for(int prop:eligible)for(int b=0;b<4;b++)for(int mode:{0,1,3})for(int scalar:{0,1,5,29,200})for(int eth:{0,1})for(int alloc:{0,1})Case(prop,b,mode,scalar,scalar,8,eth,alloc);
'''
 cpp+='int ranges[][3]={'+','.join('{'+','.join(map(str,r))+'}' for r in RANGES)+'};\n'
 cpp+='for(int prop:eligible)for(int b=0;b<4;b++)for(auto&r:ranges)for(int alloc:{0,1})Case(prop,b,0,r[0],r[1],r[2],0,alloc);\nfor(int b=0;b<4;b++)for(auto&r:ranges)for(int alloc:{0,1})Case(121,b,0,r[0],r[1],r[2],0,alloc);printf("]");\n'
 # Fresh graphs use complete source LCG functions, independently of the
 # counter-stream writer controls. Their bases/services remain declared inputs.
 calls=[(code,1,3,0) for code in ('ama','sor','nec','pal','bar','dru','ass','war','fireskill')]+[('indestruct',0,0,0),('dmg%',20,40,0),('hp',20,20,0),('str',0,5,0),('dmg-fire',3,7,0),('dmg-pois',2,4,8)]
 ordinals={p['Code']:p['Ordinal'] for p in plans}
 cpp+=r'''printf(",\"fresh\":[");first=true;
for(int b=0;b<4;b++)for(uint64_t initial:{0ULL,2860448284845ULL,18446744073709551615ULL})for(int eth:{0,1}){
D2UnitStrc item;item.weapon=b!=3;item.throwable=b==2;basis=bases[b];allocate=true;child.values.clear();trace.clear();item.pSeed.real=true;item.pSeed.value=initial;
for(auto pair:{std::array{21,basis.nMinDam},std::array{22,basis.nMaxDam},std::array{23,basis.n2HandMinDam},std::array{24,basis.n2HandMaxDam},std::array{159,basis.nMinMisDam},std::array{160,basis.nMaxMisDam},std::array{31,basis.dwMinAc}}){if(pair[1])item.base.values[Key(pair[0],0)]=pair[1];}
'''
 for i,(code,minimum,maximum,parameter) in enumerate(calls):cpp+=f'D2PropertyStrc p{i}{{{ordinals[code]},{minimum},{maximum},{parameter}}};D2COMMON_11292_ItemAssignProperty(0,nullptr,&item,nullptr,0,0,&p{i},0,0x40,nullptr);\n'
 cpp+=r'''if(eth)ITEMMODS_ApplyEthereality(&item);
printf("%s[%d,\"%llu\",%d,\"%llu\",%d,",first?"":",",b,(unsigned long long)initial,eth,(unsigned long long)item.pSeed.value,item.pSeed.count);first=false;Array(item.base);printf(",");Array(child);
'''+f'D2PropertyStrc next{{{ordinals["hp"]},1,5,0}};D2COMMON_11292_ItemAssignProperty(0,nullptr,&item,nullptr,0,0,&next,0,0x40,nullptr);\n'+r'''
printf(",\"%llu\",%d,",(unsigned long long)item.pSeed.value,item.pSeed.count);Array(item.base);printf(",");Array(child);printf("]");
}printf("]}\n");}
'''
 cpp=cpp.replace('int main(){Init();printf("[");','int main(){Init();printf("{\\"cases\\":[");')
 for n,b in (bodies|seeds).items():assert function(cpp,n)==b,n
 source=STAGE/'source.cpp';source.write_text(cpp);binary=STAGE/'probe'
 with (STAGE/'compiler.log').open('w') as f:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(source),'-o',str(binary)],stdout=f,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
 data=json.loads(run.stdout);cases=data['cases'];assert len(cases)==61472 and len(data['fresh'])==24
 output=STAGE/'cases.json';output.write_text(json.dumps(dict(plans=creation,bases=prior['bases'],synthetic=dict(Code='synthetic-layer-sharing',Ordinal=121,Steps=synthetic),calls=calls,cases=cases,fresh=data['fresh']),separators=(',',':'))+'\n')
 skillPath=BASE/'d2moo-pinned-source/source/D2Common/src/D2Skills.cpp';skillRaw=skillPath.read_bytes();assert hashlib.sha1(b'blob '+str(len(skillRaw)).encode()+b'\0'+skillRaw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']=='source/D2Common/src/D2Skills.cpp');inputs[str(skillPath.relative_to(ROOT))]=sha(skillPath)
 e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,inputs=inputs,completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in (bodies|seeds).items()},
  creationPlans=202,freshAdmittedPlans=201,originalProjectionPlans=191,propertyRows=284,fixedCases=48480,rangeCases=12928,syntheticSharingCases=64,totalCases=61472,freshGraphCases=24,
  skillTabReaderStaticReview=dict(hash=hashlib.sha256(function(skillRaw.decode(),'SKILLS_GetSkillLevel').encode()).hexdigest(),writer='parameter%3+8*parameter/3',reader='skillPage+8*classId-1',executed=False,currentMeaningUnverified=True),
  artifacts={str(p.relative_to(ROOT)):sha(p) for p in (source,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',output,Path(__file__).resolve())},
  established=['20complete unchanged routines compose14numeric/layered writers with dispatcher,base-reset,stat-list write,ethereality and2source seed functions. All15accepted older routine hashes retain. Current284property plans and stat shifts match pinned metadata;four base contexts inherit accepted baseline evidence.',
               'New kernel scope includes class/element skills via fixed property-step layer,literal old skilltab parameter%3+8*parameter/3,and indestructible additive stat152. Fresh admission excludes uncertain skilltab;original saved-item projection stays at191plans.',
               '24fresh graph controls independently compose15declared property calls,source LCG,post-property ethereality and a subsequent healing-stat property. Base/property arrays and initial/final/continued seed/draw counts bound fresh native graph/reload verification;this is not full item initialization.',
               'Fixed scalar/grade/unique/ethereality/allocation controls retain the old numeric set and include11new plans. Actual reversed/zero-crossing ranges and parameters0/3/7/8/21/23exercise all202plans. Synthetic sharing verifies retained first-result behavior acrossfunc1/10/21.'],
  services=['Valid item type4,item/base/property/stat lookup,allocation and counter-stream RNG are declared services. Range/draw/write traces are exact source controls,not a global item-seed or full world initialization certificate.',
            'Missing old ignored source/diagnostic files are not fabricated. The accepted fixture hash,original unchanged generator header,284plans,15routine hashes and inherited four base inputs are reused explicitly. Fresh18routine source and sanitizer logs are preserved separately.'],
  remaining=['war-tab-rand remains uncreatable:current steps pass values21/22/23,but the olderfunc10ignores those step values and reuses the property parameter. Current executable meaning remains uncertain.',
             'Ordinary skilltab literal multiply-before-divide writer conflicts with its own reviewed class*8+tree reader. Source-only controls retain literal arithmetic;fresh current admission refuses it pending an approved adaptation or current producer evidence.',
             'Procs,charges,sockets,remaining specialized properties,complete item initialization/quality fallback/NPC stat/event/skill consumers and current executable/client acceptance remain open. No new learned handler or complete generated NPC item is certified.'])
 path=ROOT/'tools/reference/item-property-creation-source-evidence.json';text=json.dumps(e,indent=2)+'\n'
 if check:assert path.read_text()==text,'Property creation evidence stale'
 else:path.write_text(text)
 print('Item property creation:202kernel/201fresh plans;61472writer/24fresh-source controls;20complete unchanged routines;ASan+UBSan clean;original191projection/skilltab gaps retained.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
