#!/usr/bin/env python3
"""Execute complete ordinary property22 with pinned numeric/list/RNG services."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
from probe_npc_damage_fill import enum
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'single-skill-property-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);inputs={};tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def read(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 mods=read('source/D2Common/src/Items/ItemMods.cpp');body=function(mods,'ITEMMODS_PropertyFunc22');ids=enum(read('source/D2Common/include/D2StatList.h'),'D2C_ItemStats');assert ids['STAT_ITEM_SINGLESKILL']==107
 for name in ['equipment-stat-costs','equipment-property-plans']:
  p=ROOT/'data/Sanctuary/Balance'/(name+'.json');inputs[str(p.relative_to(ROOT))]=sha(p)
 costs=json.loads((ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json').read_text());cost=next(x for x in costs if x['Id']==107);assert cost['Name']=='item_singleskill' and cost['Shift']==0
 plan=next(x for x in json.loads((ROOT/'data/Sanctuary/Balance/equipment-property-plans.json').read_text())if x['Code']=='skill');assert plan['Ordinal']==124 and plan['Steps']==[dict(Function=22,Stat=107,Set=False,Value=0)]
 priorPath=ROOT/'tools/reference/item-property-creation-source-evidence.json';prior=json.loads(priorPath.read_text());inputs[str(priorPath.relative_to(ROOT))]=sha(priorPath)
 scaffold=BASE/'item-property-creation-source/source.cpp';assert sha(scaffold)==prior['artifacts'][str(scaffold.relative_to(ROOT))];inputs[str(scaffold.relative_to(ROOT))]=sha(scaffold)
 cpp=scaffold.read_text().split('bool first=true;\nvoid Case(')[0]+'\n'+body+r'''
bool comma=false;
void Case(int mode,int present,int set,int layer,int minimum,int maximum,int incoming,int alloc,uint64_t seed){
D2UnitStrc item;item.pSeed.real=true;item.pSeed.value=seed;allocate=alloc;child.values.clear();child.values[Key(107,uint16_t(layer))]=5;trace.clear();
D2PropertyStrc p{124,minimum,maximum,layer};int result=ITEMMODS_PropertyFunc22(mode,nullptr,&item,present?&p:nullptr,set,107,13,incoming,7,0x40,nullptr);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,\"%llu\"],%d,",comma?",":"",mode,present,set,layer,minimum,maximum,incoming,alloc,(unsigned long long)seed,result);comma=true;Array(child);
printf(",\"%llu\",%d,[",(unsigned long long)item.pSeed.value,item.pSeed.count);bool t=false;for(auto x:trace){printf("%s[%d,%d,%d,%d]",t?",":"",x[0],x[1],x[2],x[3]);t=true;}printf("]]");}
int main(){Init();printf("{\"cases\":[");int ranges[][2]={{0,0},{1,1},{3,3},{1,3},{3,1},{-2,2}};
for(int mode:{0,1,3,4})for(int present:{0,1})for(int set:{0,1})for(int layer:{-1,0,69,255,394,428,65535,65536})for(auto r:ranges)for(int incoming:{-1,0,7})for(int alloc:{0,1})for(uint64_t seed:{0ULL,2860448284845ULL,18446744073709551615ULL})Case(mode,present,set,layer,r[0],r[1],incoming,alloc,seed);
printf("]}\n");}
'''
 assert function(cpp,'ITEMMODS_PropertyFunc22')==body
 src=STAGE/'source.cpp';src.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-Wno-literal-suffix','-Wformat=2','-Werror=format','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(src),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 data=json.loads(run.stdout);assert len(data['cases'])==13824
 out=STAGE/'cases.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n');inputs[str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__))
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,contexts=13824,completeRoutineHashes={'ITEMMODS_PropertyFunc22':hashlib.sha256(body.encode()).hexdigest()},inheritedCompleteRoutineHashes=prior['completeRoutineHashes'],inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in [src,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',out]},
  services=['Unchanged property22 and inherited original numeric/list/LCG bodies execute with a valid item, current owned stat107/cost shift0, declared allocation service and prior same-layer value5. Set/add, missing property, zero, fixed/reversed ranges, ushort narrowing, ignored incoming result, four mode values and seed continuation are checked.'],
  limits=['The older routine has no skill lookup/ownership/cast or required-rank policy. Unknown numeric IDs remain addressable layers; textual current source keys require a separate resolver.','Ranged null-item source pointer dereference is excluded. No current executable, global factory, recipient skill consumer, graphical effect or complete class claim.'])
 target=ROOT/'tools/reference/single-skill-property-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==text
 else:target.write_text(text)
 print('Single skill property22:13824 complete writer controls;raw107/ushort layers/set-add/seed continuation;ASan+UBSan clean;skill activation separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
