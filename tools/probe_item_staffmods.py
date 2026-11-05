#!/usr/bin/env python3
"""Execute complete old staffmod bodies against retained current skill/type rows."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'item-staffmods-source'
CLASSES=('ama','sor','nec','pal','bar','dru','ass')
ALL_CLASSES=CLASSES+('war',)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={};bodies={}
 def read(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 for rel,names in {'source/D2Game/src/ITEMS/Items.cpp':['sub_6FC52410','sub_6FC52650'],'source/D2Game/src/ITEMS/ItemsMagic.cpp':['sub_6FC55060'],'source/D2Common/src/Items/Items.cpp':['ITEMS_GetItemSeed'],'source/D2Common/include/D2Seed.h':['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber']}.items():
  text=read(rel)
  for n in names:bodies[n]=function(text,n)
 def enum(rel,name):
  text=read(rel);return text[text.index('enum '+name):text.index('};',text.index('enum '+name))+2]
 enums='\n'.join((enum('source/D2Common/include/Units/Player.h','D2C_PlayerClasses'),enum('source/D2Common/include/DataTbls/SkillsIds.h','D2C_Skills'),enum('source/D2Common/include/D2StatList.h','D2C_ItemStats'),enum('source/D2Common/include/D2Items.h','D2C_ItemDropFlags'),enum('source/D2Common/include/D2Items.h','D2C_ItemQualities')))
 def load(rel):
  p=ROOT/rel;inputs[rel]=sha(p);return json.loads(p.read_text())
 source=load('tools/reference/d2r-named-equipment/sources.json');raw=load('tools/reference/d2r-named-equipment/json/skills.json');assert inputs['tools/reference/d2r-named-equipment/json/skills.json']==source['files']['json/skills.json']['sha256']
 execution=load('data/Sanctuary/Balance/item-skill-execution.json');assert len(execution['Skills'])==429
 for r in execution['Skills']:assert r['Fields']==raw[str(r['Id'])]
 finishing=load('data/Sanctuary/Balance/item-finishing-catalogue.json');pool=load('data/Sanctuary/Balance/npc-item-affix-catalogue.json');types={r['code']:r for r in finishing['types']};hierarchy={r['code']:r['parents']for r in pool['types']}
 groups={c:sorted(int(k)for k,r in raw.items()if r.get('charclass')==c)for c in ALL_CLASSES}
 for c,g in groups.items():assert len(g)==30 and g==list(range(g[0],g[0]+30))
 decision=load('tools/reference/item-staffmods-tier-decision.json');assert decision['status']=='user-approved-sanctuary-adaptation'
 guide=ROOT/decision['guide']['path'];assert sha(guide)==decision['guide']['sha256'];inputs[str(guide.relative_to(ROOT))]=sha(guide)
 tiers={c:[[r['*Id']for r in sorted(raw.values(),key=lambda r:r['*Id'])if r.get('charclass')==c and r.get('reqlevel')==level]for level in (1,6,12,18,24,30)]for c in ALL_CLASSES}
 assert all(all(t)and sum(map(len,t))==30 for t in tiers.values())
 assert all(tiers[c]==list(decision['currentGroups'][c].values())for c in ALL_CLASSES)
 restrictions={int(k):types[r['itypea1']]['ordinal']if r.get('itypea1')else 0 for k,r in raw.items()}
 def isa(t,p):return t==p or any(isa(v,p)for v in hierarchy[t])
 seeds=[(666<<32)|511,(666<<32)|19,(1<<64)-1]
 cases=[]
 def case(entry=0,c=2,level=20,fmt=100,q=2,flags=0,seed=None,lst=1,records=0,match=0,drop=0,code=''):
  first=groups[CLASSES[c]][0]if 0<=c<7 else 66;cases.append([entry,c,level,fmt,q,flags,first,30,str(seeds[0]if seed is None else seed),lst,records,match,drop,code])
 # Exact level/format/quality/count/value thresholds,allocation failure and
 # missing/restricted skill services. Seven old classes remain independent.
 for entry in (0,1,2):
  for c in range(7):
   for level in (1,11,12,18,19,24,25,36,37,99):
    for fmt,q,flags,drop in ((0,1,0,0),(1,1,0,0),(99,1,32,37),(100,1,0,0),(100,2,0,0),(100,3,32,99)):
     for seed in seeds:case(entry,c,level,fmt,q,flags,seed,drop=drop)
 for entry in (0,1,2):
  for lst in (0,1,2):
   for mode in (1,2,3,4):
    for match in (0,1):
     for seed in seeds:case(entry=entry,level=37,flags=32,seed=seed,lst=lst,records=mode,match=match,drop=37)
 for c in (-1,7,8):
  for seed in seeds:case(c=c,seed=seed)
 for count in (-1,0):
  for seed in seeds:case(seed=seed);cases[-1][7]=count
 # A deterministic sweep covers all inclusive/exclusive random thresholds,
 # duplicate exhaustion and the legacy Poison Dagger retry on real rows.
 for n in range(768):case(entry=n%3,c=n%7,level=(12,19,25,37)[n%4],flags=32 if n%2 else 0,seed=(666<<32)|n,lst=n%3,records=n%5,match=n%2,drop=n%100)
 control_count=len(cases)
 for c in range(8):
  for level in (1,11,12,18,19,24,25,36,37,99):
   for quality in (1,3):
    for flags in (0,32):
     for seed in seeds:
      case(entry=3,c=c,level=level,q=quality,flags=flags,seed=seed);cases[-1][6]=groups[ALL_CLASSES[c]][0]
 adaptation_count=len(cases)-control_count;native=[]
 for b in pool['bases']:
  label=types[b['type']]['staffMods']
  if label not in ALL_CLASSES:continue
  c=ALL_CLASSES.index(label)
  for n,seed in enumerate(seeds):
   case(entry=3,c=c,level=(1,25,37)[n],fmt=100,q=1 if n==1 else 3,flags=32 if n==2 else 0,seed=seed,lst=0 if n==1 else 1,code=b['code']);cases[-1][6]=groups[ALL_CLASSES[c]][0];native.append(b['code'])
 assert len(native)==414
 cpp=r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <map>
#include <vector>
#define __fastcall
#define __stdcall
#define D2_MAYBE_UNUSED(x) ((void)x)
constexpr unsigned long long operator""i64(unsigned long long n){return n;}
constexpr int UNIT_ITEM=4;
'''+enums+r'''
union D2SeedStrc{uint64_t lSeed;struct{uint32_t nLowSeed,nHighSeed;};};
struct D2ItemDataStrc{D2SeedStrc pSeed;};
struct D2StatListStrc{std::map<int,int>skills;};
struct D2UnitStrc{int dwUnitId=17;void*pMemoryPool=nullptr;D2ItemDataStrc data;D2StatListStrc list;bool has=false;}item;
struct D2ItemDropStrc{int nItemLvl;uint32_t dwFlags2;}drop;
struct D2SkillsTxt{int nITypeA[1];};
struct Config{int entry,cls,level,format,quality;uint32_t flags;int first,count;uint64_t seed;int listmode,records,match,drop;std::vector<int>allowed;};
Config current;std::vector<std::vector<int>>trace;D2SkillsTxt records[429],alternate;uint32_t ITEMS_RollRandomNumber(D2SeedStrc*);
void T(std::initializer_list<int>v){trace.emplace_back(v);}
D2ItemDataStrc*ITEMS_GetItemData(D2UnitStrc*p){return &p->data;}
int ITEMS_GetStaffMods(D2UnitStrc*){return current.cls;}int SKILLS_GetPlayerSkillCount(int){return current.count;}int SKILLS_GetClassSkillId(int,int){return current.first;}
uint32_t ITEMS_GetItemFormat(D2UnitStrc*){return current.format;}int ITEMS_GetItemQuality(D2UnitStrc*){return current.quality;}
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){
 if(current.records==1)return nullptr;
 if(current.records==2){alternate={{0}};return &alternate;}
 if(current.records==3||current.records==4&&id%2){alternate={{999}};return &alternate;}
 return id>=0&&id<429?records+id:nullptr;
}
int ITEMS_CheckItemTypeId(D2UnitStrc*,int type){return current.allowed.empty()?current.match:std::find(current.allowed.begin(),current.allowed.end(),type)!=current.allowed.end();}
D2StatListStrc*STATLIST_GetStatListFromUnitStateAndFlag(D2UnitStrc*p,int state,int flags){assert(state==0&&flags==0x40);T({1,p->has});return p->has?&p->list:nullptr;}
D2StatListStrc*STATLIST_AllocStatList(void*,int flags,int state,int unit,int id){assert(flags==0x40&&state==0&&unit==4&&id==17);T({2,current.listmode!=2});return current.listmode==2?nullptr:&item.list;}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*p,D2StatListStrc*l,int reset){assert(reset==1);T({3,l!=nullptr});if(l)p->has=true;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc*p,int stat,int value,int layer){assert(p&&stat==STAT_ITEM_SINGLESKILL);p->skills[layer]=value;T({4,layer,value});}
void sub_6FC52650(D2UnitStrc*,int32_t,int32_t,int32_t,int32_t);void sub_6FC55060(D2UnitStrc*,int32_t,int32_t,int32_t);
'''
 cpp+='std::vector<std::vector<int>> tiers[]={'+','.join('{'+','.join('{'+','.join(map(str,t))+'}'for t in tiers[c])+'}'for c in ALL_CLASSES)+'};\n'
 cpp+='int SANCTUARY_StaffmodCandidate(int first,int tier,uint32_t draw){assert(first==current.first&&tier>=0&&tier<6);auto &ids=tiers[current.cls][tier];return ids[draw%ids.size()];}\n'
 order=('SEED_RollRandomNumber','SEED_RollLimitedRandomNumber','ITEMS_GetItemSeed','sub_6FC52650','sub_6FC55060','sub_6FC52410');cpp+='\n'.join(bodies[n]for n in order)
 original=bodies['sub_6FC52650'];needle='nSkillId = nBaseLayer + ITEMS_RollRandomNumber(ITEMS_GetItemSeed(pUnit)) % 5;'
 assert original.count(needle)==1
 adapted=original.replace('sub_6FC52650','SANCTUARY_StaffmodsByRequiredLevel',1).replace(needle,'nSkillId = SANCTUARY_StaffmodCandidate(nClassFirstSkillId, (nBaseLayer - nClassFirstSkillId) / 5, ITEMS_RollRandomNumber(ITEMS_GetItemSeed(pUnit)));')
 cpp+='\n'+adapted+'\n'
 cpp+=r'''
uint32_t ITEMS_RollRandomNumber(D2SeedStrc*p){uint32_t value=SEED_RollRandomNumber(p);T({0,(int)value});return value;}
Config cases[]={
'''
 bybase={b['code']:b for b in pool['bases']}
 for c in cases:
  allowed=[r['ordinal']for r in finishing['types']if isa(bybase[c[13]]['type'],r['code'])]if c[13]else []
  cpp+='{'+','.join(map(str,c[:8]))+','+c[8]+'ULL,'+','.join(map(str,c[9:13]))+',{'+','.join(map(str,allowed))+'}},\n'
 cpp+='};\nint initial[]={'+','.join(str(restrictions[i])for i in range(429))+'};\n'
 cpp+=r'''
int main(){for(int i=0;i<429;i++)records[i]={{initial[i]}};printf("[");bool comma=false;for(auto c:cases){current=c;item={};item.has=c.listmode==0;if(item.has)item.list.skills[c.first]=2;item.data.pSeed.lSeed=c.seed;drop={c.level,c.flags};trace.clear();
switch(c.entry){case 0:sub_6FC52410(&item,&drop);break;case 1:sub_6FC52650(&item,c.level,c.first,c.count,c.drop);break;case 2:sub_6FC55060(&item,c.level,c.first,0);break;case 3:assert(c.format==100&&c.cls>=0&&c.cls<8);SANCTUARY_StaffmodsByRequiredLevel(&item,c.level,c.first,c.count,c.flags&32?c.level:0);break;}
uint64_t saved=item.data.pSeed.lSeed;auto next=item.data.pSeed;int hp=1+SEED_RollLimitedRandomNumber(&next,5);
printf("%s[\"%llu\",%s,[",comma?",":"",(unsigned long long)saved,item.has?"true":"false");comma=true;bool sc=false;for(auto [s,v]:item.list.skills){printf("%s[%d,%d]",sc?",":"",s,v);sc=true;}printf("],[");bool tc=false;for(auto row:trace){printf("%s[",tc?",":"");tc=true;for(size_t i=0;i<row.size();i++)printf("%s%d",i?",":"",row[i]);printf("]");}printf("],\"%llu\",%d]",(unsigned long long)next.lSeed,hp);
}printf("]\n");}
'''
 for n,b in bodies.items():assert function(cpp,n)==b,n
 STAGE.mkdir(exist_ok=True);generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with (STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True,timeout=60);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
 results=json.loads(run.stdout);assert len(results)==len(cases)
 output=STAGE/'cases.json';output.write_text(json.dumps(dict(inputs=cases[:control_count],outputs=results[:control_count],adaptationInputs=cases[control_count:control_count+adaptation_count],adaptationOutputs=results[control_count:control_count+adaptation_count],nativeInputs=cases[control_count+adaptation_count:],nativeOutputs=results[control_count+adaptation_count:],nativeCodes=native,restrictions=restrictions,groups=groups,tiers=tiers),separators=(',',':'))+'\n')
 receipt=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,cases=control_count,nativeContexts=len(native),currentStaffmodBases=len(native)//3,classes=ALL_CLASSES,adaptationCases=adaptation_count,approvedAdaptation='item-staffmods-tier-decision.json',adaptedRoutineSha256=hashlib.sha256(adapted.encode()).hexdigest(),candidateReplacementOnly=True,completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},inputs=inputs,
  scope='Complete older staffmod dispatcher,modern and legacy bodies with complete item-seed getter/two LCG routines. Current raw429skills/105types supply restrictions and approved required-level tiers for all eight classes;138actual staffmod bases have414native contexts. The separately identified adapted body changes only the candidate mapping expression;the six complete original bodies remain unchanged. Ordered random/list/allocation/write trace,duplicate exhaustion,legacy Poison Dagger retry,inferior/value/level/format and item-level flag edges are explicit.',
  limits=['Current executable parity is not established. Item-type checks,class metadata,skill-row lookup and list allocation/post/write services are supplied;allocation failures are controlled service cases,not native allocation-failure proofs.',
   'Required-level tiers are established by the owned current guide. Ascending IDs,full-tier uniform mapping and retained count/value/retry rules are explicitly user-approved Sanctuary behavior,not exact modern executable ordering/RNG parity.',
   'No complete item factory,materialization,NPC graph/skill delivery,summon actor or learned skill is enabled by this proof. Extreme overflow and uninitialized zero-seed looping remain outside actual binding.'],
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in (generated,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',output,Path(__file__).resolve())})
 target=ROOT/'tools/reference/item-staffmods-source-evidence.json';encoded=json.dumps(receipt,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Staffmod source proof stale'
 else:target.write_text(encoded)
 print('Staffmods:',control_count,'legacy controls/6complete routines;',adaptation_count,'approved tier controls;414current contexts/138bases;ASan+UBSan clean.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
