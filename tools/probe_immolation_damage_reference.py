#!/usr/bin/env python3
"""Execute complete parent/field damage creator, missile endpoints and stat setter."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
subprocess.run([sys.executable,str(ROOT/'tools/probe_bow_launch_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
def routine(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(i['sha'] for i in tree['tree'] if i['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();text=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
 body=text[m.start():e];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
rel='source/D2Common/src/Units/Missile.cpp';tier=routine('source/D2Common/src/D2Skills.cpp','SKILLS_CalculateDamageBonusByLevel');bodies={name:routine(rel,name) for name in ['MISSILE_GetMinElemDamage','MISSILE_GetMaxElemDamage','MISSILE_SetDamageStats']}
base=(STAGE/'bow-launch-source-probe.cpp').read_text().split('int main()',1)[0]
base=base.replace('#define __fastcall','#define ARRAY_SIZE(x) (sizeof(x)/sizeof((x)[0]))\n#define __fastcall')
base=base.replace('int nSkill=6;','int nSkill=6,nLevel=1,fFlags=0;')
base=base.replace('unsigned dwMissileFlags=0;','int dwElemMin=12,dwElemMax=24,dwMinElemLev[5]={4,5,6,6,6},dwMaxElemLev[5]={4,5,7,7,7},dwElemDmgSymPerCalc=1,nHitShift=2;unsigned dwMissileFlags=0;')
base=base.replace('bool critical=false;int skillMin=0,skillMax=0;','bool critical=false;int skillMin=0,skillMax=0,percentage=0,mastery=0,criticalCalls=0,masteryCalls=0;')
base=base.replace('bool MISSILE_HasBonusStats(D2UnitStrc*,D2UnitStrc*){return critical;}','bool MISSILE_HasBonusStats(D2UnitStrc*,D2UnitStrc*){criticalCalls++;return critical;}')
base=base.replace('int SKILLS_GetMinElemDamage(D2UnitStrc*,int,int,int){return 0;}','int SKILLS_GetMinElemDamage(D2UnitStrc*,int,int,int){return 3072+3072*mastery/100;}')
base=base.replace('int SKILLS_GetMaxElemDamage(D2UnitStrc*,int,int,int){return 0;}','int SKILLS_GetMaxElemDamage(D2UnitStrc*,int,int,int){return 5888+5888*mastery/100;}')
base=base.replace('int MISSILE_CalculateMasteryBonus(D2UnitStrc*,int,int){return 0;}','int MISSILE_CalculateMasteryBonus(D2UnitStrc*,int,int damage){masteryCalls++;return damage*mastery/100;}')
for name in ['MISSILE_GetMinElemDamage','MISSILE_GetMaxElemDamage']:
 old=f'int {name}(D2UnitStrc*,D2UnitStrc*,int,int){{return 0;}}';assert old in base;base=base.replace(old,'int '+name+'(D2UnitStrc*,D2UnitStrc*,int,int);')
extraStats=sorted(set(re.findall(r'\bSTAT_\w+',bodies['MISSILE_SetDamageStats']))|{'STAT_PASSIVE_FIRE_MASTERY'});m=re.search(r'enum Stats\{([^}]+)\}',base);old=m.group(1).split(',');base=base[:m.start(1)]+','.join(old+[s for s in extraStats if s not in old])+base[m.end(1):]
base+='\nstruct D2StatStrc{int nValue=0,nLayer=0;};\nvoid STATLIST_SetUnitStat(D2UnitStrc* p,int stat,int value,int){p->stats[stat]=value;}\nint STATLIST_CopyStats(D2UnitStrc*,int,D2StatStrc*,int){return 0;}\nint MISSILE_EvaluateMissileFormula(D2UnitStrc*,D2UnitStrc*,int,int,int){return percentage;}\n'+tier+'\n'+'\n'.join(bodies.values())
fields=json.loads((STAGE/'bow-launch-source-probe.json').read_text())['fields'];cpp=base+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;std::printf("[");
for(int kind:{0,1,2})for(int rank:{1,3,20,28,40})for(int pct:{0,5,100})for(int lo:{1,9})for(int itemFire:{0,10,100})for(int boost:{0,15,40})for(int crit:{0,1}){
 owner={};weapon={};missile={};row={};missileData={};missileData.nLevel=rank;weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;row.nElemType=ELEMTYPE_FIRE;row.nSkill=kind==0?27:0;row.nSrcDamage=0;row.dwMissileFlags=kind==2?gdwBitMasks[MISSILESFLAGINDEX_APPLYMASTERY]:0;
 if(kind==2){row.dwElemMin=15;row.dwElemMax=25;int growth[5]={4,5,6,6,6};for(int i=0;i<5;i++)row.dwMinElemLev[i]=row.dwMaxElemLev[i]=growth[i];row.nHitShift=3;}
 skill.nSrcDam=128;skill.nEType=ELEMTYPE_FIRE;skillMin=skillMax=0;critical=crit;percentage=pct;mastery=boost;criticalCalls=masteryCalls=0;
 owner.stats[STAT_MINDAMAGE]=lo;owner.stats[STAT_MAXDAMAGE]=lo+5;owner.stats[STAT_DEXTERITY]=42;owner.stats[STAT_DAMAGEPERCENT]=37;owner.stats[STAT_FIREMINDAM]=itemFire;owner.stats[STAT_FIREMAXDAM]=itemFire+31;owner.stats[STAT_PASSIVE_FIRE_MASTERY]=boost;
 D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,nullptr,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&d,rank);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,[",kind,rank,pct,lo,itemFire,boost,crit);
'''
for i,name in enumerate(fields):cpp+=f'std::printf("{"," if i else ""}%d",d.{name});\n'
cpp+='std::printf("],%d,%d,%d]",missile.stats[STAT_PASSIVE_FIRE_MASTERY],criticalCalls,masteryCalls);}\nstd::printf("]\\n");}\n'
path=STAGE/'immolation-damage-source-probe.cpp';path.write_text(cpp);binary=STAGE/'immolation-damage-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==1620
rows=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/missiles.json').read_text())
def tiered(r,maximum,rank):
 value=r['EMax' if maximum else 'EMin'];left=max(rank-1,0)
 for i,length in enumerate([7,8,6,6,2**30],1):n=min(left,length);value+=n*r.get(('MaxELev' if maximum else 'MinELev')+str(i),0);left-=n
 return value
for kind,rank,pct,lo,fire,boost,crit,values,stored,critCalls,masteryCalls in data:
 d=dict(zip(fields,values));assert stored==0
 if kind==0:
  assert(d['nMinDamage'],d['nMaxDamage'],d['nDamagePercent'])==(lo*256,(lo+5)*256,79)
  assert(d['nFireMinDamage'],d['nFireMaxDamage'])==(3072+3072*boost//100+fire*256,5888+5888*boost//100+(fire+31)*256) and critCalls==1 and masteryCalls==0
 else:
  row=rows['immolationfire' if kind==1 else 'meteorfire'];expected=[]
  for maximum in [False,True]:
   v=tiered(row,maximum,rank);v=(v+v*pct//100)<<row['HitShift'];expected.append(v+(v*boost//100 if kind==2 else 0))
  assert(d['nFireMinDamage'],d['nFireMaxDamage'])==tuple(expected) and(d['nMinDamage'],d['nMaxDamage'])==(0,0) and critCalls==0 and masteryCalls==(2 if kind==2 else 0)
fixture=STAGE/'immolation-damage-source-probe.json';fixture.write_text(json.dumps(dict(fields=fields,cases=data),separators=(',',':'))+'\n')
parent=json.loads((ROOT/'tools/reference/bow-launch-engine-evidence.json').read_text());e=dict(referenceVersion='3.3.93847',talentId='amazon.immolationarrow',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks)+parent['compiledRoutines'],blockHashes=blocks,creatorBlockHashes=parent['blockHashes'],probe=dict(cases=len(data),path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete parent creator/field creator/stat setter distinguish source128 Skill27 weapon-plus-skill fire from unskilled source0 ImmolationFire. Field inherits no item fire/physical/critical or owner passive mastery stat. Parent skill endpoints are declared already-mastered providers.','Complete missile min/max elemental helpers and tier routine apply percentage synergy to integer table damage BEFORE HitShift. ImmolationFire rank1/hard Fire1 therefore yields48..100 FP256 rather than50..100. This is not skill-endpoint after-shift synergy arithmetic.','Current ImmolationFire lacks ApplyMastery: declared owner mastery0/15/40 does not alter its field packet. Positive control MeteorFire has ApplyMastery and executes both mastery calls. Complete stat setter does not copy owner passive fire mastery into the saved missile.','The shared native Meteor residual helper currently applies synergy after HitShift and differs at odd values (rank1/Inferno1:123..206 versus source120..200). Record and correct this shared arithmetic with compiled differentials when introducing the missile endpoint kernel.'],limits=['Owner/table/stat/formula/skill endpoints, weapon selection, critical result and mastery-percentage helper are declared services. Empty monster-type bonus copying is explicit. This is complete creator/helper/setter execution, not full modern stat/mastery/seed or damage mitigation acceptance.','Current mastery behavior follows the pinned row plus supplemental source; direct modern-game item-%fire/burning corroboration remains open. Full parent routing/field collision/cadence/localdelay/geometry/owner/client verification is separate. No handler enabled.'])
out=ROOT/'tools/reference/immolation-damage-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Immolation damage evidence stale'
else:out.write_text(encoded)
print('Immolation:1620 complete creator/missile endpoint/stat-setter cases; pre-shift synergy and field mastery flag distinguished.')
