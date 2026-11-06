#!/usr/bin/env python3
"""Compose original source-reader values, innate getters and full NPC fill."""
import hashlib,json,re,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-damage-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);inputs={};tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def read(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_text()
 def load(p):return json.loads(read(p))
 def source(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);return read(p)
 prior=load(ROOT/'tools/reference/npc-damage-fill-source-evidence.json');path=BASE/'npc-damage-fill-source/source.cpp';assert sha(path)==prior['artifacts'][str(path.relative_to(ROOT))]
 cpp=read(path).split('int main(){printf("[");',1)[0];hashes=prior['completeRoutineHashes'].copy()
 for n,h in hashes.items():assert hashlib.sha256(function(cpp,n).encode()).hexdigest()==h,n
 seed=source('source/D2Common/include/D2Seed.h');items=source('source/D2Game/src/ITEMS/Items.cpp');skills=source('source/D2Common/src/D2Skills.cpp')
 added={n:function(s,n)for s,names in [(seed,['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber']),(items,['ITEMS_RollRandomNumber','ITEMS_RollLimitedRandomNumber']),(skills,['SKILLS_GetBonusSkillLevel','SKILLS_GetSkillLevel'])]for n in names}
 cpp=cpp.replace('struct D2UnitStrc{int dwUnitType=1,dwClassId=0,pSeed=0;', 'struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};};\nstruct D2UnitStrc{int dwUnitType=1,dwClassId=0;D2SeedStrc pSeed{};')
 cpp=cpp.replace('bool demon=false,undead=false;','std::vector<D2StatStrc>typed,layered;bool demon=false,undead=false;')
 for n in ['ITEMS_RollRandomNumber','ITEMS_RollLimitedRandomNumber']:
  cpp,count=re.subn(r'^.*\b'+n+r'\(int\*[^\n]*\n','',cpp,flags=re.M);assert count==1,n
 for n in ['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber']:
  # Only the MSVC integer-literal suffix is normalized for this compiler.
  body=added[n].replace('0x6AC690C5i64','0x6AC690C5ULL');cpp=cpp.replace('int STATLIST_UnitGetStatValue',body+'\nint STATLIST_UnitGetStatValue',1)
 pos=cpp.index('int STATLIST_UnitGetStatValue');cpp=cpp[:pos]+added['ITEMS_RollRandomNumber']+'\n'+added['ITEMS_RollLimitedRandomNumber']+'\n'+cpp[pos:]
 cpp=cpp.replace('int STATLIST_UnitGetStatValue(const D2UnitStrc*u,int stat,int){return u->stats[stat];}','int STATLIST_UnitGetStatValue(const D2UnitStrc*u,int stat,int layer){if(!layer)return u->stats[stat];for(auto r:layered)if(r.nLayer==((stat<<16)|layer))return r.nValue;return 0;}')
 cpp=cpp.replace('for(int i=0;i<128;i++)out[i]={i+1,i%3==0?-5:10};return 128;','int n=std::min<int>(typed.size(),cap);for(int i=0;i<n;i++)out[i]=typed[i];return n;')
 cpp+='''
#define __stdcall
using BOOL=int;
constexpr int D2UnitInvalidGUID=-1,STATE_SHRINE_SKILL=50,STAT_ITEM_ALLSKILLS=127,STAT_ITEM_ADDCLASSSKILLS=83,STAT_ITEM_ADDSKILL_TAB=188,STAT_ITEM_NONCLASSSKILL=97,STAT_ITEM_ELEMSKILL=126,STAT_ITEM_SINGLESKILL=107;
struct D2SkillsTxt{int nSkillId=9,nCharClass=0,wSkillDesc=0,nEType=0;};struct D2SkillDescTxt{int nSkillPage=1;};
struct D2SkillStrc{D2SkillsTxt*pSkillsTxt;int nSkillLevel,nLevelBonus=0,nOwnerGUID=-1;};
D2SkillsTxt passive;struct Tables{D2SkillsTxt*pSkillsTxt=nullptr;}table;Tables*sgptDataTables=&table;
D2SkillsTxt*DATATBLS_GetSkillsTxtRecord(int){return &passive;}
D2SkillDescTxt*DATATBLS_GetSkillDescTxtRecord(int){assert(false);return nullptr;}
bool STATES_CheckState(D2UnitStrc*,int){return false;}
int DATATBLS_GetMaxLevel(int kind){assert(kind==0);return 99;}
'''+added['SKILLS_GetBonusSkillLevel']+'\n'+added['SKILLS_GetSkillLevel']+r'''
// Declared current formula service: retained two-stage dm12, corroborated
// by current Dodge3/Avoid3 and independently reported Critical Strike12=59.
int RetainedDm12(int rank,int lo,int hi){return rank<=0?0:lo+(hi-lo)*std::min(100,110*rank/(rank+6))/100;}
void Packet(D2DamageStrc&p){printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,%d,%d,%d,%u,%d]",p.dwPhysDamage,p.dwFireDamage,p.dwLtngDamage,p.dwColdDamage,p.dwMagDamage,p.dwPoisDamage,p.dwColdLen,p.dwPoisLen,p.dwStunLen,p.dwLifeLeech,p.dwManaLeech,p.dwStamLeech,p.dwEnDmgPct,p.dwHitFlags,p.wResultFlags,p.nConvType,p.dwConvPct,p.dwFrzLen,p.dwBurnLen,p.dwHitClass,overlay);}
int main(){int count;assert(scanf("%d",&count)==1);printf("[");bool comma=false;
for(int index=0;index<count;index++){
int hard,sc;unsigned long long start;assert(scanf("%d%llu%d",&hard,&start,&sc)==3);std::array<int,368>base{};layered.clear();typed.clear();
for(int i=0;i<sc;i++){uint32_t key;int value;assert(scanf("%u%d",&key,&value)==2);if(!(key&65535))base[key>>16]=value;else layered.push_back({int(key),value});if((key>>16)==180)typed.push_back({int(key&65535),value});}
int str,dex;assert(scanf("%d%d",&str,&dex)==2);actor={};actor.dwUnitType=1;actor.pSeed.lSeed=start;actor.pInventory=&actor;
for(int generation=0;generation<3;generation++)for(int command=0;command<2;command++)for(int variant=0;variant<5;variant++){
actor.stats=base;actor.stats[127]+=command;target={};target.dwClassId=10000;target.dwUnitType=1;target.stats[31]=20;mon={5,0};targetMon={0,variant==3?7:0};
alignment=UNIT_ALIGNMENT_GOOD;hire=0;armed=1;two=1;strength=str;dexterity=dex;masteryDamage=masteryCrit=0;blunt=0;mutate=eventChange=0;demon=variant==1||variant==3;undead=variant==2||variant==3;overlay=0;
uint64_t before=actor.pSeed.lSeed;int ids[]={9,13,18,29},bases[]={hard,1,3,20},stats[]={337,338,339,340},lo[]={5,10,15,10},hi[]={80,65,75,65};
int ranks[4],values[4];for(int i=0;i<4;i++){passive.nSkillId=ids[i];D2SkillStrc entry{&passive,bases[i]};ranks[i]=SKILLS_GetSkillLevel(&actor,bases[i]?&entry:nullptr,1);values[i]=RetainedDm12(ranks[i],lo[i],hi[i]);}
actor.stats[337]+=values[0];D2DamageStrc p;int share[]={0,64,128,255,128};
if(variant==1){p.dwPhysDamage=257;p.dwFireDamage=129;p.dwLtngDamage=37;p.dwMagDamage=53;p.dwColdDamage=71;p.dwPoisDamage=769;p.dwColdLen=7;p.dwPoisLen=11;p.dwStunLen=13;p.dwLifeLeech=5;p.dwManaLeech=7;p.dwStamLeech=9;p.dwEnDmgPct=17;p.nConvType=10;p.dwConvPct=37;p.dwHitClass=0x63;}
if(variant==2){p.dwPhysDamage=256;p.dwHitFlags=1|4|8|16;p.wResultFlags=32;}
if(variant==3){p.nConvType=12;p.dwConvPct=37;p.dwHitClass=0x63;}
printf("%s{\"Body\":%d,\"Generation\":%d,\"Command\":%d,\"Variant\":%d,\"Before\":\"%llu\",\"Passives\":[",comma?",":"",index,generation,command,variant,(unsigned long long)before);comma=true;
for(int i=0;i<4;i++)printf("%s[%d,%d,%d,%d]",i?",":"",ids[i],ranks[i],stats[i],values[i]);printf("],\"Initial\":");Packet(p);
SUNITDMG_FillDamageValues(nullptr,&actor,variant==4?nullptr:&target,&p,0,share[variant]);printf(",\"Output\":");Packet(p);printf(",\"Armor\":%d,\"After\":\"%llu\"}",target.stats[31],(unsigned long long)actor.pSeed.lSeed);
}}printf("]\n");}
'''
 # Verify all six existing bodies and all added bodies except the declared
 # compiler-only suffix change. No caller/arithmetic body is simplified.
 for n,h in hashes.items():assert hashlib.sha256(function(cpp,n).encode()).hexdigest()==h,n
 for n,b in added.items():assert function(cpp,n)==b.replace('0x6AC690C5i64','0x6AC690C5ULL'),n
 costs=load(ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json');ids={r['Name']:r['Id']for r in costs};assert [ids[k]for k in ['item_allskills','item_nonclassskill','item_singleskill']]==[127,97,107]
 originals=load(BASE/'valkyrie-numeric-source/cases.json');reader=load(BASE/'valkyrie-resources-source/cases.json');catalog=load(ROOT/'data/Sanctuary/Balance/npc-item-affix-catalogue.json');facts={r['code']:r['sourceFields']for r in catalog['bases']}
 observation=load(ROOT/'tools/reference/valkyrie-inherited-critical-observation.json');assert observation['distinguishingReading']['effectiveRank']==12 and observation['distinguishingReading']['reportedPercent']==59
 cases=[];numbers=[]
 for i,c in enumerate(originals):
  sourcecase=next(r for r in reader if r['Case']==i and r['Variant']==0);operative=sourcecase['Stages'][0][2];assert not any(k>>16 in range(342,348)for k,v in operative)
  weapon=next(item for item in c['Items']if item['Equipment']['Body']==4);f=facts[weapon['Metadata']['Code']];assert f.get('2handed')==1 and not f.get('1or2handed',0)
  for hard in [0,1,20]:
   index=len(cases);seed=(666<<32)|(7000+index);entry=dict(Case=i,Rank=c['Rank'],Difficulty=c['Difficulty'],Critical=hard,Seed=str(seed));cases.append(entry)
   numbers += [hard,seed,len(operative)]+[n for pair in operative for n in pair]+[100 if f.get(k,0)==1 else f.get(k,0)for k in ['StrBonus','DexBonus']]
 request=STAGE/'input.txt';request.write_text(' '.join(map(str,[len(cases)]+numbers))+'\n');path=STAGE/'source.cpp';path.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(path),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],input=request.read_bytes(),capture_output=True,timeout=60);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 output=STAGE/'output.json';output.write_bytes(run.stdout);rows=json.loads(run.stdout);assert len(rows)==5130
 for index,c in enumerate(cases):c['Packets']=[r for r in rows if r['Body']==index];assert len(c['Packets'])==30
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 result=dict(referenceVersion='3.3.93847',currentExecutableParity=False,bodies=171,originalLoadouts=57,originalItems=990,packets=5130,packetsPerBoot=1710,completeRoutineHashes={**hashes,**{n:hashlib.sha256(b.encode()).hexdigest()for n,b in added.items()}},inputs=inputs,
  declaredServices=['Original scalar/layered source-reader values from the independent30-routine resource oracle;original57loadout field proofs remain separate. Spear2handed/strength/dexterity metadata comes from pinned current table;no weapon mastery entries occur in this corpus,existing42-routine controls retain that general service.',
   'Captured innate hard ranks;NPC item stat readers;class0maximum99;current two-stage dm12 formula service,with direct current Critical12=59corroboration. Source passive-stat-list allocation/refresh and complete current NPC formula dispatch remain unclaimed.',
   'Explicit target categories,type-match modulo service,armor20,ordinary good-aligned nonhireling hand service;no native target species mapping,AI,accuracy,HP/status/proc/contact delivery claimed. RNG seed is mandatory supplied context,not world ownership.'],
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in [path,binary,request,output,out,STAGE/'compiler.log',STAGE/'sanitizer.log',Path(__file__).resolve()]})
 (ROOT/'tools/reference/valkyrie-damage-source-evidence.json').write_text(json.dumps(result,indent=2)+'\n');print('Valkyrie damage:171original bodies/5130packets,12complete source routines,actual source RNG;ASan/UBSan clean.')
if __name__=='__main__':main()
