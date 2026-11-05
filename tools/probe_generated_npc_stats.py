#!/usr/bin/env python3
"""Compose complete item update/removal callers with the retained stat engine."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'generated-npc-stats-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);inputs={};bodies={};tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def load(rel):
  p=ROOT/rel;inputs[rel]=sha(p);return json.loads(p.read_text())
 def read(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 prior=load('tools/reference/iron-golem-birth-graph-source-evidence.json');path=BASE/'iron-golem-birth-graph-source/source.cpp';assert sha(path)==prior['artifacts'][str(path.relative_to(ROOT))];inputs[str(path.relative_to(ROOT))]=sha(path)
 cpp=path.read_text().split('D2StatListStrc root,item,bonus;',1)[0]
 for name,h in prior['reusedCompiledUnchangedGraphRoutines'].items():assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==h,name
 for rel,names in {'source/D2Game/src/ITEMS/ItemMode.cpp':['D2GAME_ITEMS_UpdateItemStatlist_6FC42310','D2GAME_ITEMS_UpdateTransferredProperties_6FC424E0'],'source/D2Common/src/Units/Units.cpp':['UNITS_CanDualWield']}.items():
  text=read(rel)
  for name in names:bodies[name]=function(text,name)
 header=read('source/D2Common/include/D2Items.h');assert 'IFLAG_BROKEN = 0x00000100'in header
 # Extend only the explicit fixture unit ABI and body-location service.
 cpp=cpp.replace('MonsterData*pMonsterData=nullptr;','MonsterData*pMonsterData=nullptr;void*pDynamicPath=nullptr;int body=0,quality=2;unsigned flags=0;bool gem=false,rune=false,weapon=false;')
 cpp=cpp.replace('if(owner&&owner->dwUnitType==1)ResourceChanged(owner,packed>>16,old,value);','')
 cpp=cpp.replace('int ITEMS_GetBodyLocation(D2UnitStrc*){return 4;}','int ITEMS_GetBodyLocation(D2UnitStrc*p){return p->body;}')
 cases=[];actors=[(0,0),(0,4),(0,6),(0,7),(1,0),(1,1),(1,2),(1,3),(4,0),(2,0)];full=1|2|32|64|2048|16384
 flags=sorted({0,full,32767,*[1<<i for i in range(15)],*[full^(1<<i)for i in range(15)],*[((i*7919)^0x5A5A)&32767 for i in range(32)]})
 for kind,cls in actors:
  for itemkind in (2,4):
   for f in flags:
    for main in (0,1,2):
     for owner in (0,1,2):
      for mainowner in (0,1):
       for entry in (0,1):cases.append([kind,cls,itemkind,f,main,owner,mainowner,entry])
 raw=load('.local/class-skills/item-quality-current-source/prepared-cases.json');pool=load('data/Sanctuary/Balance/npc-item-affix-catalogue.json');codes={r['code']:r for r in pool['bases']};types={r['code']:r['parents']for r in pool['types']}
 def isa(t,p):return t==p or any(isa(q,p)for q in types[t])
 slots={r['Code']:r['Body1']for r in load('data/Sanctuary/Balance/iron-golem-items.json')}
 itemrows=[]
 for index in raw['native']:
  a=raw['inputs'][index]
  if a[7]or a[2]not in(1,3,6):continue
  itemrows.append(dict(Case=index,Body=slots[a[0]],Weapon=isa(codes[a[0]]['type'],'weap'),Quality=raw['outputs'][index][1],HasProperties=raw['outputs'][index][16],Base=raw['outputs'][index][17],Properties=raw['outputs'][index][18]))
 assert len(itemrows)==1548
 selected=[]
 for code,body in [('ces',4),('btl',5),('cap',1),('qui',3),('rin',6),('rin',7),('amu',2)]:
  index=next(i for i,a in enumerate(raw['inputs'])if a[0]==code and a[1]==37 and a[2]==6 and a[7]==0)
  selected.append(len(itemrows));itemrows.append(dict(Case=index,Body=body,Weapon=isa(codes[code]['type'],'weap'),Quality=raw['outputs'][index][1],HasProperties=raw['outputs'][index][16],Base=raw['outputs'][index][17],Properties=raw['outputs'][index][18]))
 numeric=[dict(Items=[i],Kind=i%3,Level=raw['inputs'][r['Case']][1],Clock=91)for i,r in enumerate(itemrows[:1548])]
 for kind in (1,2):
  for level in (1,37,99):
   for order in (selected,list(reversed(selected)),selected[2:]+selected[:2]):numeric.append(dict(Items=order,Kind=kind,Level=level,Clock=91))
 cpp+=r'''
constexpr int PCLASS_BARBARIAN=4,PCLASS_ASSASSIN=6,MONSTER_SHADOWWARRIOR=1,MONSTER_SHADOWMASTER=2,STATE_PLAYERBODY=99,ITEMTYPE_GEM=96,ITEMTYPE_RUNE=97,ITEMTYPE_WEAPON=45,ITEMQUAL_SET=5,IFLAG_BROKEN=0x100;
struct D2GameStrc{};struct D2ItemsTxt{int dwGemOffset=0;}basis;struct D2GemsTxt{}gemRecord;
const char*gszEmptyString_6FD447EC="";bool gate=true,playerBody=false,hasBase=true,hasGem=true;D2UnitStrc*mainWeapon=nullptr,*itemOwner=nullptr,*mainOwner=nullptr,*controlledItem=nullptr;std::vector<std::array<int,4>>calls;
void C(int code,int a=0,int b=0,int c=0){calls.push_back({code,a,b,c});}
void FOG_TraceF(const char*,const char*){C(1);}bool STATES_CheckState(D2UnitStrc*,int){return playerBody;}
bool ITEMS_CheckItemTypeId(D2UnitStrc*p,int type){return type==ITEMTYPE_GEM?p->gem:type==ITEMTYPE_RUNE?p->rune:type==ITEMTYPE_WEAPON?p->weapon:false;}
D2ItemsTxt*DATATBLS_GetItemsTxtRecord(int){return hasBase?&basis:nullptr;}D2GemsTxt*DATATBLS_GetGemsTxtRecord(int){return hasGem?&gemRecord:nullptr;}
int ITEMS_GetGemApplyType(D2UnitStrc*){return 2;}void ITEMMODS_AssignProperty(int mode,D2UnitStrc*owner,D2UnitStrc*,D2GemsTxt*,int apply,int){C(2,mode,owner?owner->id:0,apply);}
int ITEMS_GetItemQuality(D2UnitStrc*p){return p->quality;}void ITEMS_UpdateSets(D2UnitStrc*,D2UnitStrc*,int remove,int){C(3,remove);}
int sub_6FC42120(D2UnitStrc*,D2UnitStrc*,int add){C(4,add);return 1;}D2UnitStrc*INVENTORY_GetLeftHandWeapon(void*){C(5);return mainWeapon;}
bool ITEMS_CheckItemFlag(D2UnitStrc*p,int flag,int,const char*){assert(flag==IFLAG_BROKEN);return p->flags&flag;}
void MergeService(D2UnitStrc*target,D2UnitStrc*item,BOOL damage){C(6,item->id,damage);if(!gate)STATLIST_MergeStatLists(target,item,damage);}
D2UnitStrc*OwnerService(D2UnitStrc*item,BOOL*full){C(10,item->id);return gate?(item==controlledItem?itemOwner:mainOwner):STATLIST_GetOwner(item,full);}
void STATLIST_ExpireUnitStatlist(D2UnitStrc*,D2UnitStrc*item){C(7,item->id);if(!gate)D2Common_ExpireStatList_6FDB6E30(item->pStatListEx);}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){C(8);}void STATLIST_ClampStaminaManaHP(D2UnitStrc*){C(9);}
#define STATLIST_MergeStatLists MergeService
#define STATLIST_GetOwner OwnerService
'''+ '\n'.join(bodies[n]for n in ('UNITS_CanDualWield','D2GAME_ITEMS_UpdateItemStatlist_6FC42310','D2GAME_ITEMS_UpdateTransferredProperties_6FC424E0'))+r'''
#undef STATLIST_MergeStatLists
#undef STATLIST_GetOwner
void Calls(){printf("[");bool comma=false;for(auto a:calls){printf("%s[%d,%d,%d,%d]",comma?",":"",a[0],a[1],a[2],a[3]);comma=true;}printf("]");}
int controls[][8]={
'''+''.join('{'+','.join(map(str,a))+'},\n'for a in cases)+'};\n'
 cpp+='struct Input{int source,body,quality;bool weapon,has;std::vector<std::array<int64_t,2>>base,props;};\nInput inputs[]={\n'
 for r in itemrows:
  pairs=lambda values:'{'+','.join('{'+str(k)+','+str(v)+'}'for k,v in values)+'}'
  cpp+='{'+str(r['Case'])+','+str(r['Body'])+','+str(r['Quality'])+','+str(int(r['Weapon']))+','+str(int(r['HasProperties']))+','+pairs(r['Base'])+','+pairs(r['Properties'])+'},\n'
 cpp+='};\nstruct Numeric{std::vector<int>items;int kind,level,clock;};\nNumeric numeric[]={\n'+''.join('{{'+','.join(map(str,n['Items']))+'},'+','.join(map(str,(n['Kind'],n['Level'],n['Clock'])))+'},\n'for n in numeric)+'};\n'
 cpp+=r'''
D2StatListStrc root,nodes[10],props[10];D2UnitStrc actor,units[10];
void Raw(D2StatsArrayStrc&a,uint32_t packed,int value){if(value)STATLIST_GetOrInsertStat(nullptr,&a,packed)->nValue=value;}
void Array(D2StatsArrayStrc&a){printf("[");for(int i=0;i<a.nStatCount;i++)printf("%s[%u,%d]",i?",":"",a.pStat[i].nPackedValue,a.pStat[i].nValue);printf("]");}
void Snapshot(int count){printf("[%u,",root.dwFlags);Array(root.Stats);printf(",");Array(root.FullStats);printf(",[");for(int i=0;i<count;i++){auto&n=nodes[i];BOOL full=false;auto owner=STATLIST_GetOwner(&units[i],&full);printf("%s[%u,%d,%d,",i?",":"",n.dwFlags,owner?owner->id:0,n.pUnit?n.pUnit->id:0);Array(n.Stats);printf(",");Array(n.FullStats);printf("]");}printf("]]");}
void Trace(){printf("[");for(size_t i=0;i<trace.size();i++){auto a=trace[i];printf("%s[%d,%d,%d,%d,%d]",i?",":"",a[0],a[1],a[2],a[3],a[4]);}printf("]");}
int main(){Init();printf("{\"gates\":[");bool comma=false;
for(auto c:controls){D2UnitStrc target,item,main,other;target.id=1;target.dwUnitType=c[0];target.dwClassId=c[1];target.pDynamicPath=(c[3]&2048)?&target:nullptr;item.id=2;main.id=3;other.id=4;item.dwUnitType=c[2];item.gem=c[3]&8;item.rune=c[3]&16;item.weapon=c[3]&16384;item.quality=c[3]&128?5:4;item.flags=c[3]&256?IFLAG_BROKEN:0;main.flags=c[3]&512?IFLAG_BROKEN:0;playerBody=c[3]&4;hasBase=c[3]&32;hasGem=c[3]&64;mainWeapon=c[4]==1?&item:c[4]==2?&main:nullptr;itemOwner=c[5]==1?&target:c[5]==2?&other:nullptr;mainOwner=c[6]?&target:nullptr;controlledItem=&item;calls.clear();gate=true;auto t=(c[3]&1)?&target:nullptr;auto p=(c[3]&2)?&item:nullptr;if(c[7])D2GAME_ITEMS_UpdateTransferredProperties_6FC424E0(nullptr,p,t,c[3]&4096,c[3]&8192);else D2GAME_ITEMS_UpdateItemStatlist_6FC42310(nullptr,p,t,c[3]&4096);printf("%s",comma?",":"");Calls();comma=true;}
printf("],\"numeric\":[");comma=false;gate=false;playerBody=false;hasBase=hasGem=true;mainWeapon=nullptr;
for(auto c:numeric){new(&root)D2StatListStrc;actor={};actor.id=1;actor.dwUnitType=1;actor.dwClassId=c.kind;actor.pStatListEx=&root;actor.pDrlgAct=&minute;actor.pDynamicPath=&actor;root.pOwner=&actor;root.dwOwnerType=1;root.pfOnValueChanged=Changed;minute=c.clock;
for(auto a:{std::array{0,100},std::array{1,20},std::array{2,50},std::array{3,30},std::array{7,256000},std::array{9,76800},std::array{11,25600},std::array{12,c.level},std::array{21,7},std::array{22,14},std::array{31,80}}){Raw(root.Stats,uint32_t(a[0])<<16,a[1]);Raw(root.FullStats,uint32_t(a[0])<<16,a[1]);}
trace.clear();calls.clear();int count=c.items.size();for(int i=0;i<count;i++){auto input=inputs[c.items[i]];new(&nodes[i])D2StatListStrc;new(&props[i])D2StatListStrc;units[i]={};auto&u=units[i];u.id=2+i;u.dwUnitType=4;u.pStatListEx=&nodes[i];u.pDrlgAct=&minute;u.body=input.body;u.quality=input.quality;u.weapon=input.weapon;nodes[i].pOwner=&u;nodes[i].pfOnValueChanged=Changed;props[i].dwFlags=0x40;for(auto p:input.base){Raw(nodes[i].Stats,p[0],p[1]);Raw(nodes[i].FullStats,p[0],p[1]);}for(auto p:input.props)Raw(props[i].Stats,p[0],p[1]);if(input.has)D2COMMON_10475_PostStatToStatList(&u,&props[i],TRUE);
// Native physical NPC delivery restores cached operative maximum durability.
int maximum=STATLIST_GetTotalStat_6FDB63E0(&nodes[i],73u<<16,&rows[73]);STATLIST_SetBaseStat(&nodes[i],72,maximum,0,&u);D2GAME_ITEMS_UpdateItemStatlist_6FC42310(nullptr,&u,&actor,0);}
printf("%s[[",comma?",":"");comma=true;Snapshot(count);for(int i=0;i<count;i++)D2GAME_ITEMS_UpdateItemStatlist_6FC42310(nullptr,&units[i],&actor,0);printf(",");Snapshot(count);STATLIST_SetBaseStat(&root,12,c.level+1,0,&actor);printf(",");Snapshot(count);for(int i=count-1;i>=0;i--)D2GAME_ITEMS_UpdateTransferredProperties_6FC424E0(nullptr,&units[i],&actor,1,1);printf(",");Snapshot(count);printf("],");Trace();printf(",");Calls();printf("]");}printf("]}\n");}
'''
 for name,h in prior['reusedCompiledUnchangedGraphRoutines'].items():assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==h,name
 for name,body in bodies.items():assert function(cpp,name)==body,name
 generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True,timeout=120);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
 result=json.loads(run.stdout);assert len(result['gates'])==len(cases)and len(result['numeric'])==len(numeric);out=STAGE/'cases.json';out.write_text(json.dumps(dict(inputs=cases,outputs=result['gates'],items=itemrows,numericInputs=numeric,numericOutputs=result['numeric']),separators=(',',':'))+'\n')
 record=dict(referenceVersion='3.3.93847',currentExecutableParity=False,gateControls=len(cases),numericCompositions=len(numeric),nativeSingleItems=1548,multiItemCompositions=len(numeric)-1548,
  completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},inheritedRoutineHashes=prior['reusedCompiledUnchangedGraphRoutines'],inputs=inputs,
  scope='Complete older item stat add/remove and dual-wield callers compose with23complete retained numeric/stat topology routines and current raw prepared item packets. Gate service traces retain order;numeric cases preserve sibling order,callbacks,flags,base/full fields,repeat,recipient level change and reverse removal.',
  limits=['Synthetic gate callbacks for gem/set/player book/main-weapon/world operations are observed services,not their full bodies. Numeric compositions qualify NPC kind1 with explicit source bases/day angle and fresh unbroken generated item fields. No live Mobile callback synchronization,item mutation persistence,learned actor,current executable/world or allocation seed parity is claimed.'],
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in(generated,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',out,Path(__file__).resolve())})
 target=ROOT/'tools/reference/generated-npc-stats-source-evidence.json';encoded=json.dumps(record,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Generated NPC stats source evidence stale'
 else:target.write_text(encoded)
 print('Generated NPC stats:',len(cases),'gate controls;',len(numeric),'numeric compositions/3new+23retained complete routines;ASan+UBSan clean.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
