#!/usr/bin/env python3
"""Compile complete socket property/dimension/cap bodies against owned tables."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'saved-socket-property-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);inputs={};tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 old=ROOT/'tools/reference/item-property-creation-source-evidence.json';prior=json.loads(old.read_text());inputs[str(old.relative_to(ROOT))]=sha(old)
 source=BASE/'item-property-creation-source/source.cpp';assert sha(source)==prior['artifacts'][str(source.relative_to(ROOT))];inputs[str(source.relative_to(ROOT))]=sha(source)
 cpp=source.read_text().split('bool first=true;\nvoid Case(')[0];bodies={}
 for rel,names in {'source/D2Common/src/Items/ItemMods.cpp':['ITEMMODS_PropertyFunc14'],'source/D2Common/src/Items/Items.cpp':['ITEMS_GetDimensions','ITEMS_GetMaxSockets']}.items():
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p)
  for n in names:bodies[n]=function(raw.decode(),n)
 refs=ROOT/'tools/reference/d2r-named-equipment';pins=json.loads((refs/'sources.json').read_text());tables={}
 for name in ('weapons','armor','misc','itemtypes'):
  p=refs/'json'/(name+'.json');assert sha(p)==pins['files']['json/'+name+'.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);tables[name]=json.loads(p.read_text())
 poolPath=ROOT/'data/Sanctuary/Balance/npc-item-affix-catalogue.json';pool=json.loads(poolPath.read_text());inputs[str(poolPath.relative_to(ROOT))]=sha(poolPath)
 typesPath=ROOT/'data/Sanctuary/Balance/item-finishing-catalogue.json';catalog=json.loads(typesPath.read_text());inputs[str(typesPath.relative_to(ROOT))]=sha(typesPath)
 items={r['code']:r for name in ('weapons','armor','misc')for r in tables[name].values()};types={r['code']:r for r in catalog['types']};rawtypes={r['Code']:r for r in tables['itemtypes'].values()if r.get('Code')}
 bases=[]
 for b in pool['bases']:
  r=items[b['code']];t=types[b['type']];rt=rawtypes[b['type']]
  a=[r.get('invwidth',0),r.get('invheight',0),t['maxSockets1'],t['maxSockets25'],t['maxSockets40'],r.get('gemsockets',0)]
  assert a[:2]==[b['sourceFields'].get(k,0)for k in ('invwidth','invheight')]
  assert a[2:5]==[rt.get(k,0)for k in ('MaxSockets1','MaxSockets2','MaxSockets3')]
  assert (rt.get('MaxSocketsLevelThreshold1',25),rt.get('MaxSocketsLevelThreshold2',40))==(25,40)
  assert a[5]==b['sourceFields'].get('gemsockets',0);bases.append(dict(Code=b['code'],Values=a))
 assert len(bases)==692
 cpp=cpp.replace('struct D2UnitStrc{','struct D2ItemDataStrc{int dwItemLevel=1;};\nstruct D2UnitStrc{D2ItemDataStrc data;D2ItemDataStrc*pItemData=&data;uint32_t flags=0;')
 cpp=cpp.replace('dwMaxAc=0;}basis;','dwMaxAc=0;uint8_t nInvWidth=0,nInvHeight=0,nGemSockets=0;short wType[2]{0,0};}basis;')
 cpp=cpp.replace('struct Tables{int nPropertiesTxtRecordCount','struct D2ItemTypesTxt{uint8_t nMaxSock1=0,nMaxSock25=0,nMaxSock40=0;};D2ItemTypesTxt typeRow;\nstruct Tables{int nItemTypesTxtRecordCount=1;D2ItemTypesTxt*pItemTypesTxt=&typeRow;int nPropertiesTxtRecordCount')
 oldflag='void ITEMS_SetItemFlag(D2UnitStrc*u,int,int value){u->ethereal=value;trace.push_back({1,0,value,0});}'
 assert oldflag in cpp;cpp=cpp.replace(oldflag,'void ITEMS_SetItemFlag(D2UnitStrc*u,int flag,int value){u->flags=value?u->flags|flag:u->flags&~flag;trace.push_back({1,flag,value,0});}')
 cpp+='\nconstexpr int IFLAG_SOCKETED=0x800,STAT_ITEM_NUMSOCKETS=194;\n'+bodies['ITEMS_GetDimensions']+'\n'+bodies['ITEMS_GetMaxSockets']+'\n'+bodies['ITEMMODS_PropertyFunc14']+'\n'
 cpp+=r'''
void Case(int kind,int present,int prop,int w,int h,int c1,int c25,int c40,int gems,int level,int first,int lo,int hi,int parameter,int data,int direct,uint64_t seed){
 D2UnitStrc item;item.dwUnitType=kind;item.data.dwItemLevel=level;if(!data)item.pItemData=nullptr;item.pSeed.real=true;item.pSeed.value=seed;
 basis.nInvWidth=w;basis.nInvHeight=h;basis.nGemSockets=gems;basis.wType[0]=0;typeRow={uint8_t(c1),uint8_t(c25),uint8_t(c40)};item.base.values.clear();trace.clear();child.values.clear();
 D2PropertyStrc p{134,lo,hi,parameter};int result=direct?ITEMMODS_PropertyFunc14(3,nullptr,present?&item:nullptr,prop?&p:nullptr,0,194,0,first,0,0x40,nullptr):0;
 if(!direct){D2COMMON_11292_ItemAssignProperty(3,nullptr,&item,nullptr,0,0,&p,0,0x40,nullptr);result=Read(item.base,194);}
 static bool comma=false;printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,\"%llu\"],%d,%u,%d,%d,\"%llu\",%d,[",comma?",":"",kind,present,prop,w,h,c1,c25,c40,gems,level,first,lo,hi,parameter,data,direct,(unsigned long long)seed,result,item.flags,Read(item.base,194),item.data.dwItemLevel,(unsigned long long)item.pSeed.value,item.pSeed.count);comma=true;
 bool t=false;for(auto a:trace){printf("%s[%d,%d,%d,%d]",t?",":"",a[0],a[1],a[2],a[3]);t=true;}printf("]]");
}
int main(){Init();off_6FDE3920[14]=ITEMMODS_PropertyFunc14;printf("[");
'''
 cpp+='int bases[][6]={'+','.join('{'+','.join(map(str,b['Values']))+'}'for b in bases)+'};\n'
 cpp+='for(auto b:bases)for(int level:{1,25,26,40,41,99})for(int n:{0,1,8})for(int direct:{0,1})Case(4,1,1,b[0],b[1],b[2],b[3],b[4],b[5],level,0,n,n,0,1,direct,2860448284845ULL);\n'
 cpp+=r'''
 int ranges[][3]={{0,0,0},{0,0,3},{-2,2,0},{5,1,0},{1,7,0}};
 for(int w:{0,1,2,4})for(int h:{0,1,3})for(int cap:{0,1,3,6})for(int data:{0,1})for(int first:{-1,0,4})for(auto r:ranges)for(uint64_t seed:{0ULL,2860448284845ULL,18446744073709551615ULL})Case(4,1,1,w,h,cap,cap,cap,6,0,first,r[0],r[1],r[2],data,1,seed);
 for(int kind:{0,4})for(int present:{0,1})for(int prop:{0,1})Case(kind,present,prop,2,4,3,4,6,6,99,0,1,7,0,1,1,0ULL);
 printf("]\n");}
'''
 for n,b in bodies.items():assert function(cpp,n)==b,n
 file=STAGE/'source.cpp';file.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(file),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 cases=json.loads(run.stdout);assert len(cases)==24912+4320+8,len(cases)
 output=STAGE/'cases.json';output.write_text(json.dumps(dict(bases=bases,cases=cases),separators=(',',':'))+'\n')
 receipt=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,ownedBases=692,cases=len(cases),completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},inputs=inputs,
  services=['Inherited complete property dispatcher/stat writers/source LCG;declared current-table record lookup,item flag/stat setters and valid property table. Current dimensions and all level caps independently match owned3.3.93847 tables.'],
  scope='Complete property14,dimension and max-socket helpers;all692current bases across level boundaries and fixed rolls,dispatcher/direct agreement,source first-result/range/fallback/flag/stat/RNG traces and invalid-item guards.',
  remaining=['Saved projection never rerolls or mutates the original item. Gem/rune contents,fresh socket materialization,invalid legacy counts and current executable parity remain separate.'],
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in [file,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',output,Path(__file__).resolve()]})
 out=ROOT/'tools/reference/saved-socket-property-source-evidence.json';encoded=json.dumps(receipt,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print(f'Socket property:692owned bases/{len(cases)}source cases;3complete unchanged routines;ASan+UBSan clean.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
