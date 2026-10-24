#!/usr/bin/env python3
"""Execute unchanged parent/child creator selection and element-only explosion getter."""
from pathlib import Path
import argparse,contextlib,hashlib,importlib.util,io,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
for name in ['bow_launch','magic_arrow']:
 path=ROOT/f'tools/probe_{name}_reference.py';spec=importlib.util.spec_from_file_location(name,path);mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
 with contextlib.redirect_stdout(io.StringIO()):mod.build(True)
fields=json.loads((STAGE/'bow-launch-source-probe.json').read_text())['fields'];base=(STAGE/'bow-launch-source-probe.cpp').read_text().split('int main()',1)[0]
base=base.replace('bool critical=false;int skillMin=0,skillMax=0;','bool critical=false;int skillMin=0,skillMax=0,elementMin=0,elementMax=0;')
base=base.replace('int SKILLS_GetMinElemDamage(D2UnitStrc*,int,int,int){return 0;}','int SKILLS_GetMinElemDamage(D2UnitStrc*,int,int,int){return elementMin;}').replace('int SKILLS_GetMaxElemDamage(D2UnitStrc*,int,int,int){return 0;}','int SKILLS_GetMaxElemDamage(D2UnitStrc*,int,int,int){return elementMax;}')
cpp=base+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;std::printf("[");
for(int child:{0,1})for(int lo:{1,9})for(int fire:{0,10,100})for(int added:{256,1280,53760})for(int crit:{0,1}){
 owner={};weapon={};missile={};D2UnitStrc origin={};origin.dwUnitType=UNIT_MISSILE;origin.stats[STAT_MINDAMAGE]=999999;origin.stats[STAT_FIREMINDAM]=777777;
 weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;row={};row.nSkill=child?16:0;row.nSrcDamage=child?0:128;row.nElemType=ELEMTYPE_FIRE;row.dwMissileFlags=child?gdwBitMasks[MISSILESFLAGINDEX_MISSILESKILL]:0;missileData.nSkill=16;
 skill.nSrcDam=128;skill.nEType=ELEMTYPE_FIRE;skillMin=skillMax=0;elementMin=added;elementMax=added+512;critical=crit;
 owner.stats[STAT_MINDAMAGE]=lo;owner.stats[STAT_MAXDAMAGE]=lo+5;owner.stats[STAT_DEXTERITY]=42;owner.stats[STAT_DAMAGEPERCENT]=37;owner.stats[STAT_FIREMINDAM]=fire;owner.stats[STAT_FIREMAXDAM]=fire+31;
 D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,&origin,&missile,1);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,[",child,lo,fire,added,crit);
'''
for i,field in enumerate(fields):cpp+=f'std::printf("{"," if i else ""}%d",d.{field});\n'
cpp+='std::printf("]]");}std::printf("]\\n");}\n'
def run(name,code):
 path=STAGE/(name+'.cpp');path.write_text(code);binary=STAGE/name;subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True);return json.loads(subprocess.check_output([str(binary)]))
creator=run('exploding-arrow-creator-source-probe',cpp);assert len(creator)==72
for child,lo,fire,added,critical,values in creator:
 d=dict(zip(fields,values));assert d['nMinDamage']==lo*256 and d['nMaxDamage']==(lo+5)*256 and d['nDamagePercent']==79
 assert d['nFireMinDamage']==fire*256+(added if child else 0) and d['nFireMaxDamage']==(fire+31)*256+(added+512 if child else 0)
 assert bool(d['nFlags']&2)==bool(critical)
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Game/src/MISSILES/MissMode.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
text=raw.decode();name='MISSMODE_GetDamageValue';m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);end=text.index('{',m.start())+1;depth=1
while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
getter=text[m.start():end];base=(STAGE/'magic-arrow-source-probe.cpp').read_text().split('int main()',1)[0]
for enum,prefix in [('Stats','STAT_'),('Elements','ELEMTYPE_')]:
 m=re.search('enum '+enum+r'\{([^}]+)\}',base);old=m.group(1).split(',');extra=sorted(set(re.findall(r'\b'+prefix+r'\w+',getter))-set(old));base=base[:m.start(1)]+','.join(old+extra)+base[m.end(1):]
m=re.search(r'struct D2DamageStrc\{([^}]+)\};',base);old=set(re.findall(r'int (\w+);',m.group(1)));extra=sorted(set(re.findall(r'pDamage->(\w+)',getter))-old);base=base[:m.end(1)]+''.join('int '+x+';' for x in extra)+base[m.end(1):]
base=base.replace('int MISSILE_GetElemTypeFromMissileId(int){return ELEMTYPE_MAGIC;}','int MISSILE_GetElemTypeFromMissileId(int){return ELEMTYPE_FIRE;}')
cpp=base+'\n'+getter+r'''int main(){bool first=true;std::printf("[");for(int fire:{0,257,1024})for(int mastery:{0,25})for(int physical:{1,99})for(int crit:{0,1})for(int kind:{0,2,3}){
 missile={};target={};target.kind=kind;target.dwUnitType=UNIT_MONSTER;missile.stats[STAT_MINDAMAGE]=missile.stats[STAT_MAXDAMAGE]=physical;
 missile.stats[STAT_FIREMINDAM]=fire;missile.stats[STAT_FIREMAXDAM]=fire+512;missile.stats[STAT_PASSIVE_FIRE_MASTERY]=mastery;missile.stats[STAT_ITEM_DEADLYSTRIKE]=crit;missile.stats[STAT_ITEM_DEMONDAMAGE_PERCENT]=99;missile.stats[STAT_ITEM_UNDEADDAMAGE_PERCENT]=77;
 D2DamageStrc d={};int result=MISSMODE_GetDamageValue(&game,&missile,kind?&target:nullptr,&d);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",fire,mastery,physical,crit,kind,result,d.dwPhysDamage,d.dwFireDamage,d.dwMagDamage,d.dwColdDamage,d.wResultFlags);
}std::printf("]\n");}
'''
impact=run('exploding-arrow-getter-source-probe',cpp);assert len(impact)==72
for fire,mastery,physical,crit,kind,result,phys,out,magic,cold,flags in impact:assert (result,phys,out,magic,cold,flags)==(fire+fire*mastery//100,0,fire+fire*mastery//100,0,0,0)
data=dict(creatorFields=fields,creator=creator,getter=impact);fixture=STAGE/'exploding-arrow-damage-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
parent=json.loads((ROOT/'tools/reference/bow-launch-engine-evidence.json').read_text());e=dict(referenceVersion='3.3.93847',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes={'source/D2Common/src/Units/Missile.cpp':parent['sourceHash'],rel:hashlib.sha256(raw).hexdigest()},creatorBlockHashes=parent['blockHashes'],getterBlockSha256=hashlib.sha256(getter.encode()).hexdigest(),probe=dict(creatorCases=72,getterCases=72,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete unchanged creator distinguishes current parent missile-derived source128 weapon/item endpoints from child skill-derived source128 weapon/item-plus-skill endpoints. Supplied origin stats do not replace the declared current owner stats in these rows.','Child fire-only getter executes unchanged and emits only fire, ignoring captured physical, critical and target species percentages. Combined with the independently compiled area callbacks, one fire packet is copied per target without independent damage rerolls.','Parent weapon impact needs the ordinary full damage path, whereas the child explosion must select only its fire channel. No Fire Arrow physical conversion is applied.'],limits=['Owner/table/stat/skill/RNG services retain the original explicit harness boundaries. Item/skill endpoints are declared already-calculated inputs. This does not certify all current mastery/stat/seed paths or full factory/collision/child scheduling.','Factory/collision/scheduler caller chain is reviewed separately in exploding-arrow-preflight.json; it is not compiled by this damage harness. Live owner-stat reads at child creation follow the compiled creator, while parent skill/rank propagation follows the separate callback evidence. Native checks and modern acceptance are recorded separately.'])
out=ROOT/'tools/reference/exploding-arrow-damage-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Exploding Arrow damage evidence stale'
else:out.write_text(encoded)
print('Exploding Arrow:72 complete creator/72 fire-only getter cases compiled; parent/child damage channels distinguished.')
