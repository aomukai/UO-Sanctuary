#!/usr/bin/env python3
"""Approved current superior-table composition through complete numeric writers."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/item-finishing-properties-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True)
 baseline=ROOT/'tools/reference/item-property-creation-source-evidence.json';prior=json.loads(baseline.read_text());source=ROOT/'.local/class-skills/item-property-creation-source/source.cpp'
 assert sha(source)==prior['artifacts'][str(source.relative_to(ROOT))]
 cpp=source.read_text().split('int main(){',1)[0]
 mods=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Common/src/Items/ItemMods.cpp';raw=mods.read_bytes();tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']=='source/D2Common/src/Items/ItemMods.cpp')
 durability=function(raw.decode(),'ITEMMODS_PropertyFunc13')
 helper='constexpr int STAT_DURABILITY=72;\nint STATLIST_GetMaxDurabilityFromUnit(D2UnitStrc*p){int base=Read(p->base,73);int percent=Read(child,75);return base+base*percent/100;}\n'
 cpp=cpp.replace('void Init(){',helper+durability+'\nvoid Init(){off_6FDE3920[13]=ITEMMODS_PropertyFunc13;',1)

 table=ROOT/'data/Sanctuary/Balance/item-finishing-catalogue.json';rows=json.loads(table.read_text())['superior'];assert len(rows)==8
 decision=ROOT/'tools/reference/item-superior-table-decision.json';assert json.loads(decision.read_text())['status']=='user-approved-sanctuary-adaptation'
 plans=json.loads((ROOT/'data/Sanctuary/Balance/equipment-property-plans.json').read_text());ids={r['Code']:r['Ordinal']for r in plans}
 cpp+='D2PropertyStrc grade[8][2]={\n'
 for row in rows:
  props=['{'+','.join(map(str,(ids[m['code']],m['min'],m['max'],int(m['parameter']))))+'}'for m in row['modifiers']]
  while len(props)<2:props.append('{-1,0,0,0}')
  cpp+='{'+','.join(props)+'},\n'
 cpp+='};\n'+r'''
int main(){Init();printf("[");bool comma=false;
for(int b=0;b<4;b++)for(int row=0;row<8;row++)for(uint64_t initial:{2860448283136ULL,2860448283647ULL,18446744073709551615ULL})for(int eth:{0,1}){
D2UnitStrc item;item.weapon=b!=3;item.throwable=b==2;basis=bases[b];allocate=true;child.values.clear();trace.clear();item.pSeed.real=true;item.pSeed.value=initial;
for(auto pair:{std::array{21,basis.nMinDam},std::array{22,basis.nMaxDam},std::array{23,basis.n2HandMinDam},std::array{24,basis.n2HandMaxDam},std::array{159,basis.nMinMisDam},std::array{160,basis.nMaxMisDam},std::array{31,basis.dwMinAc}}){if(pair[1])item.base.values[Key(pair[0],0)]=pair[1];}
for(auto&p:grade[row]){if(p.nProperty<0)break;D2COMMON_11292_ItemAssignProperty(1,nullptr,&item,nullptr,0,0,&p,0,0x40,nullptr);}if(eth)ITEMMODS_ApplyEthereality(&item);
printf("%s[%d,%d,\"%llu\",%d,\"%llu\",",comma?",":"",b,row,(unsigned long long)initial,eth,(unsigned long long)item.pSeed.value);comma=true;Array(item.base);printf(",");Array(child);printf(",%d",item.pSeed.count);
D2PropertyStrc next{13,1,5,0};D2COMMON_11292_ItemAssignProperty(0,nullptr,&item,nullptr,0,0,&next,0,0x40,nullptr);printf(",\"%llu\",",(unsigned long long)item.pSeed.value);Array(child);printf("]");
}printf("]\n");}
'''
 controls=ROOT/'.local/class-skills/item-finishing-source/cases.json';allcontrols=json.loads(controls.read_text());currentpath=ROOT/'data/Sanctuary/Balance/npc-item-affix-catalogue.json';current=json.loads(currentpath.read_text());bybase={r['code']:r for r in current['bases']};types={r['code']:r for r in current['types']};rare={r['code']:r for r in current['rareBases']}
 def isa(typ,parent):return typ==parent or any(isa(p,parent)for p in types[typ]['parents'])
 selected=[]
 for i,(a,o,code)in enumerate(zip(allcontrols['nativeInputs'],allcontrols['nativeOutputs'],allcontrols['nativeCodes'])):
  if a[0]!=0 or o[0]!=1:continue
  b=bybase[code];raw=b['sourceFields'];values=[raw.get(k,0)for k in ('mindam','maxdam','2handmindam','2handmaxdam','minmisdam','maxmisdam','minac','maxac')]
  selected.append((i,o[1],o[8],int(isa(b['type'],'weap')),int(rare[code]['throwable']),values))
 assert len(selected)==20
 cpp=cpp.replace('int main(){Init();printf("[");','void OriginalCases(){Init();printf("[");',1)
 cpp+='\nstruct Selected{int index,row;uint64_t seed;bool weapon,throws;D2ItemsTxt basis;};\nSelected selected[]={\n'
 for i,row,seed,w,th,values in selected:cpp+='{'+','.join(map(str,(i,row)))+','+seed+'ULL,'+str(w)+','+str(th)+',{'+','.join(map(str,values))+'}},\n'
 cpp+='};\n'+r'void SelectedCases(){printf("[");bool comma=false;for(auto c:selected){D2UnitStrc item;item.weapon=c.weapon;item.throwable=c.throws;basis=c.basis;allocate=true;child.values.clear();trace.clear();item.pSeed.real=true;item.pSeed.value=c.seed;for(auto pair:{std::array{21,11},std::array{22,27},std::array{23,0},std::array{24,0},std::array{159,0},std::array{160,0},std::array{31,51},std::array{72,17},std::array{73,23}})item.base.values[Key(pair[0],0)]=pair[1];for(auto&p:grade[c.row]){if(p.nProperty<0)break;D2COMMON_11292_ItemAssignProperty(1,nullptr,&item,nullptr,0,0,&p,0,0x40,nullptr);}printf("%s[%d,%d,\"%llu\",",comma?",":"",c.index,c.row,(unsigned long long)item.pSeed.value);comma=true;Array(item.base);printf(",");Array(child);D2PropertyStrc next{13,1,5,0};D2COMMON_11292_ItemAssignProperty(0,nullptr,&item,nullptr,0,0,&next,0,0x40,nullptr);printf(",\"%llu\",",(unsigned long long)item.pSeed.value);Array(child);printf("]");}printf("]");}'+'\n'+r'int main(){printf("{\"cases\":");OriginalCases();printf(",\"selected\":");SelectedCases();printf("}\n");}'+'\n'
 for n,digest in prior['completeRoutineHashes'].items():assert hashlib.sha256(function(cpp,n).encode()).hexdigest()==digest,n
 generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with (STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 data=json.loads(run.stdout);assert len(data['cases'])==192 and len(data['selected'])==20;output=STAGE/'cases.json';output.write_text(json.dumps(data,separators=(',',':'))+'\n')
 files=[baseline,source,table,decision,controls,currentpath,mods,ROOT/'data/Sanctuary/Balance/equipment-property-plans.json',Path(__file__).resolve()]
 receipt=dict(referenceVersion='3.3.93847',currentExecutableParity=False,approvedAdaptation='item-superior-table-decision.json',cases=192,selectedCurrentContexts=20,completeRoutineHashes=prior['completeRoutineHashes']|{'ITEMMODS_PropertyFunc13':hashlib.sha256(durability.encode()).hexdigest()},inputs={str(p.relative_to(ROOT)):sha(p)for p in files},
  scope='All8owned current superior rows in listed order,grade mode1,on4supplied bases/3seeds,with optional complete numeric ethereality and nextHP continuation. Twenty inherited complete numeric/seed routines are unchanged and complete property function13 is added;the operative maximum-durability reader is a declared supplied stat service;the incomplete old grade-property dispatcher is deliberately not represented as executed.',
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in (generated,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',output)})
 target=ROOT/'tools/reference/item-finishing-properties-source-evidence.json';encoded=json.dumps(receipt,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Approved finishing property proof stale'
 else:target.write_text(encoded)
 print('Superior adaptation:192current-row numeric source compositions,21complete routines,source seed/raw stats/ethereal/nextHP;ASan+UBSan clean.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
