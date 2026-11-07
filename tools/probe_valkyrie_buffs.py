#!/usr/bin/env python3
"""Reuse complete original NPC fill/RNG bodies with external stat-list services."""
import argparse,csv,hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-buffs-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);inputs={}
 def load(p):inputs[str(p.relative_to(ROOT))]=sha(p);return json.loads(p.read_text())
 previous=load(ROOT/'tools/reference/valkyrie-damage-source-evidence.json');p=BASE/'valkyrie-damage-source/source.cpp';assert sha(p)==previous['artifacts'][str(p.relative_to(ROOT))];inputs[str(p.relative_to(ROOT))]=sha(p)
 cpp=p.read_text().split('int main(){int count;',1)[0]
 for name,h in previous['completeRoutineHashes'].items():assert hashlib.sha256(function(cpp,name).replace('0x6AC690C5ULL','0x6AC690C5i64').encode()).hexdigest()==h,name
 tree=load(BASE/'d2moo-source-tree.json');assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 reviewed={}
 for rel,name in [('source/D2Common/src/D2Skills.cpp','SKILLS_GetSpecialParamValue'),('source/D2Game/src/SKILLS/SkillSor.cpp','SKILLS_SrvDo025_Enchant')]:
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);reviewed[name]=hashlib.sha256(function(raw.decode(),name).encode()).hexdigest()
  if name=='SKILLS_GetSpecialParamValue':assert 'case 49:'in function(raw.decode(),name)and'SKILLS_GetMinElemDamage(pUnit, nSkillId, nSkillLevel, 1) >> 8'in function(raw.decode(),name)
 calc=BASE/'fire-golem-mode-source/skillcalc.txt';receipt=load(ROOT/'tools/reference/hex-bane-source-evidence.json');assert sha(calc)==receipt['inputs']['.local/class-skills/warlock-foundation/skillcalc.txt'];inputs[str(calc.relative_to(ROOT))]=sha(calc)
 assert [(i,r['code'])for i,r in enumerate(csv.DictReader(calc.read_text().splitlines(),delimiter='\t'))if r['code']in('enma','exma')]==[(49,'enma'),(50,'exma')]
 row=next(r for r in load(ROOT/'data/Sanctuary/Balance/class-skills.json')if r['talentId']=='sorceress.enchant')['sourceFields'];assert [row[k]for k in ['aurastat1','aurastatcalc1','aurastat2','aurastatcalc2']]==['firemindam','enma','firemaxdam','exma']
 # Enchant FP256 services,three whole-point elemental aura ranges,active flag.
 buffs=[[0,0,0,0,0,0,0,0,1],[1001,2001,0,0,0,0,0,0,1],[0,0,12,24,0,0,0,0,1],[0,0,0,0,4,9,0,0,1],[0,0,0,0,0,0,1,45,1],[1001,2001,12,24,4,9,1,45,1],[7,255,0,0,0,0,0,0,1],[1001,2001,12,24,4,9,1,45,0]]
 cpp+=r'''
int main(){int count;assert(scanf("%d",&count)==1);printf("[");bool comma=false;
int buffs[8][9];for(auto&b:buffs)for(int&n:b)assert(scanf("%d",&n)==1);
for(int index=0;index<count;index++){
int sc,str,dex;unsigned long long start;assert(scanf("%llu%d",&start,&sc)==2);std::array<int,368>base{};layered.clear();typed.clear();
for(int i=0;i<sc;i++){uint32_t key;int value;assert(scanf("%u%d",&key,&value)==2);if(!(key&65535))base[key>>16]=value;else layered.push_back({int(key),value});if((key>>16)==180)typed.push_back({int(key&65535),value});}
assert(scanf("%d%d",&str,&dex)==2);actor={};actor.dwUnitType=1;actor.pSeed.lSeed=start;actor.pInventory=&actor;
for(int generation=0;generation<3;generation++)for(int buff=0;buff<8;buff++)for(int share:{0,64,128,255}){
actor.stats=base;target={};target.dwClassId=10000;target.dwUnitType=1;target.stats[31]=20;mon={5,0};targetMon={};
alignment=UNIT_ALIGNMENT_GOOD;hire=0;armed=1;two=1;strength=str;dexterity=dex;masteryDamage=masteryCrit=0;blunt=mutate=eventChange=0;demon=undead=false;overlay=0;
// Reviewed enma/exma writer projection;buff storage/publishing is a declared
// service. All12existing fill/innate/RNG routines below remain unchanged.
if(buffs[buff][8]){actor.stats[48]+=(buffs[buff][0]>>8)+buffs[buff][2];actor.stats[49]+=(buffs[buff][1]>>8)+buffs[buff][3];actor.stats[54]+=buffs[buff][4];actor.stats[55]+=buffs[buff][5];actor.stats[50]+=buffs[buff][6];actor.stats[51]+=buffs[buff][7];}
passive.nSkillId=9;D2SkillStrc entry{&passive,1};actor.stats[337]+=RetainedDm12(SKILLS_GetSkillLevel(&actor,&entry,1),5,80);
D2DamageStrc p;p.wResultFlags=1;uint64_t before=actor.pSeed.lSeed;SUNITDMG_FillDamageValues(nullptr,&actor,&target,&p,0,share);
printf("%s{\"Body\":%d,\"Generation\":%d,\"Buff\":%d,\"Share\":%d,\"Before\":\"%llu\",\"After\":\"%llu\",\"Armor\":%d,\"Output\":",comma?",":"",index,generation,buff,share,(unsigned long long)before,(unsigned long long)actor.pSeed.lSeed,target.stats[31]);Packet(p);printf("}");comma=true;
}}printf("]\n");}
'''
 for name,h in previous['completeRoutineHashes'].items():assert hashlib.sha256(function(cpp,name).replace('0x6AC690C5ULL','0x6AC690C5i64').encode()).hexdigest()==h,name
 originals=load(BASE/'valkyrie-numeric-source/cases.json');reader=load(BASE/'valkyrie-resources-source/cases.json');catalog=load(ROOT/'data/Sanctuary/Balance/npc-item-affix-catalogue.json');facts={r['code']:r['sourceFields']for r in catalog['bases']}
 cases=[];numbers=[len(originals)]+[v for b in buffs for v in b]
 for i,c in enumerate(originals):
  values=next(r for r in reader if r['Case']==i and r['Variant']==0)['Stages'][0][2];weapon=next(r for r in c['Items']if r['Equipment']['Body']==4);f=facts[weapon['Metadata']['Code']];seed=(666<<32)|(9000+i)
  cases.append(dict(Case=i,Rank=c['Rank'],Difficulty=c['Difficulty'],Critical=1,Seed=str(seed)))
  numbers += [seed,len(values)]+[v for pair in values for v in pair]+[100 if f.get(k,0)==1 else f.get(k,0)for k in ['StrBonus','DexBonus']]
 source=STAGE/'source.cpp';source.write_text(cpp);request=STAGE/'input.txt';request.write_text(' '.join(map(str,numbers))+'\n');binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(source),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],input=request.read_bytes(),capture_output=True,timeout=60);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 output=STAGE/'output.json';output.write_bytes(run.stdout);rows=json.loads(run.stdout);assert len(rows)==5472
 for i,c in enumerate(cases):c['Packets']=[r for r in rows if r['Body']==i];assert len(c['Packets'])==96
 out=STAGE/'cases.json';out.write_text(json.dumps(dict(Buffs=buffs,Bodies=cases),separators=(',',':'))+'\n')
 result=dict(referenceVersion='3.3.93847',currentExecutableParity=False,originalLoadouts=57,originalItems=330,packets=5472,packetsPerBoot=1824,completeRoutineHashes=previous['completeRoutineHashes'],reviewedWriterHashes=reviewed,inputs=inputs,scope=['Twelve complete prior fill/innate/RNG bodies remain unchanged.57original source-reader loadouts,8external contexts,4shares,3continuations compare whole packets/armor/exact actor RNG. No-buff,subpoint and expired services retain their original source graph.','Current Enchant table uses enma/exma;owned aliases49/50and complete reviewed getter establish whole-point mastered endpoints. Source do25 writes those formula results. This harness supplies external integer stat-list values;it does not execute a complete current buff publisher or claim current executable chronology.','Aura leases are explicit existing native services. Own item mastery is applied once by full fill after combining endpoints. No owner item/mastery inheritance or additional buff RNG. Actual learned/pet Enchant and live contact/saves are native checks,not implied by this source matrix.'],artifacts={str(p.relative_to(ROOT)):sha(p)for p in [source,request,output,out,STAGE/'compiler.log',STAGE/'sanitizer.log',Path(__file__).resolve()]})
 encoded=json.dumps(result,indent=2)+'\n';dest=ROOT/'tools/reference/valkyrie-buffs-source-evidence.json'
 if check:assert dest.read_text()==encoded
 else:dest.write_text(encoded)
 print('Valkyrie buffs:57original loadouts/5472source packets;12unchanged fill/innate/RNG bodies;whole-point Enchant projection;ASan/UBSan clean.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
