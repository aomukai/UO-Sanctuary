#!/usr/bin/env python3
"""Reuse immutable cold creator and execute the complete freeze-only child getter."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
subprocess.run([sys.executable,str(ROOT/'tools/probe_cold_arrow_creator_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
subprocess.run([sys.executable,str(ROOT/'tools/probe_exploding_arrow_damage.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
code=(STAGE/'cold-arrow-creator-source-probe.cpp').read_text();anchor='skill.nSrcDam=128;';assert anchor in code
code=code.replace(anchor,'row.nSkill=31;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_MISSILESKILL];missileData.nSkill=31;'+anchor)
def run(name,code):
 path=STAGE/(name+'.cpp');path.write_text(code);binary=STAGE/name;subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True);return json.loads(subprocess.check_output([str(binary)]))
creator=run('freezing-arrow-creator-source-probe',code);fields=json.loads((STAGE/'cold-arrow-creator-source-probe.json').read_text())['fields'];assert len(creator)==360
for lo,added,length,ownerLength,pct,crit,values in creator:
 d=dict(zip(fields,values));assert d['nMinDamage']==lo*256 and d['nMaxDamage']==(lo+5)*256 and d['nDamagePercent']==pct+57
 assert d['nColdMinDamage']==added+7*256 and d['nColdMaxDamage']==added+512+13*256 and d['nColdLength']==length+ownerLength
 assert bool(d['nFlags']&2)==bool(crit)
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Game/src/MISSILES/MissMode.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
text=raw.decode();m=re.search(r'^.*\bMISSMODE_GetDamageValue\([^;\n]*\)\s*\n\{',text,re.M);end=text.index('{',m.start())+1;depth=1
while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
getter=text[m.start():end];base=(STAGE/'magic-arrow-source-probe.cpp').read_text().split('int main()',1)[0]
for enum,prefix in [('Stats','STAT_'),('Elements','ELEMTYPE_')]:
 m=re.search('enum '+enum+r'\{([^}]+)\}',base);old=m.group(1).split(',');extra=sorted(set(re.findall(r'\b'+prefix+r'\w+',getter))-set(old));base=base[:m.start(1)]+','.join(old+extra)+base[m.end(1):]
m=re.search(r'struct D2DamageStrc\{([^}]+)\};',base);old=set(re.findall(r'int (\w+);',m.group(1)));extra=sorted(set(re.findall(r'pDamage->(\w+)',getter))-old);base=base[:m.end(1)]+''.join('int '+x+';' for x in extra)+base[m.end(1):]
anchor='int MISSILE_GetElemTypeFromMissileId(int){return ELEMTYPE_MAGIC;}';assert anchor in base;base=base.replace(anchor,'int MISSILE_GetElemTypeFromMissileId(int){return ELEMTYPE_FREEZE;}')
cpp=base+'\n'+getter+r'''int main(){bool first=true;std::printf("[");for(int cold:{0,257,1024})for(int length:{0,1,670})for(int mastery:{0,25})for(int physical:{1,99})for(int crit:{0,1})for(int kind:{0,2,3}){
 missile={};target={};target.kind=kind;target.dwUnitType=UNIT_MONSTER;missile.stats[STAT_MINDAMAGE]=missile.stats[STAT_MAXDAMAGE]=physical;
 missile.stats[STAT_COLDMINDAM]=cold;missile.stats[STAT_COLDMAXDAM]=cold+512;missile.stats[STAT_COLDLENGTH]=length;missile.stats[STAT_PASSIVE_COLD_MASTERY]=mastery;missile.stats[STAT_ITEM_DEADLYSTRIKE]=crit;missile.stats[STAT_FIREMINDAM]=9999;missile.stats[STAT_FIREMAXDAM]=99999;missile.stats[STAT_ITEM_DEMONDAMAGE_PERCENT]=99;missile.stats[STAT_ITEM_UNDEADDAMAGE_PERCENT]=77;
 D2DamageStrc d={};int result=MISSMODE_GetDamageValue(&game,&missile,kind?&target:nullptr,&d);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",cold,length,mastery,physical,crit,kind,result,d.dwPhysDamage,d.dwFireDamage,d.dwMagDamage,d.dwColdDamage,d.dwColdLen,d.dwFrzLen,d.wResultFlags);
}std::printf("]\n");}
'''
impact=run('freezing-arrow-getter-source-probe',cpp);assert len(impact)==216
for cold,length,mastery,physical,crit,kind,result,phys,fire,magic,out,chill,freeze,flags in impact:assert(result,phys,fire,magic,out,chill,freeze,flags)==(cold+cold*mastery//100,0,0,0,cold+cold*mastery//100,0,length,0)
fixture=STAGE/'freezing-arrow-damage-source-probe.json';fixture.write_text(json.dumps(dict(creatorFields=fields,creator=creator,getter=impact),separators=(',',':'))+'\n')
parent=json.loads((ROOT/'tools/reference/cold-arrow-creator-engine-evidence.json').read_text());e=dict(referenceVersion='3.3.93847',talentId='amazon.freezingarrow',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes={'source/D2Common/src/Units/Missile.cpp':parent['sourceHash'],rel:hashlib.sha256(raw).hexdigest()},creatorBlockHashes=parent['blockHashes'],getterBlockSha256=hashlib.sha256(getter.encode()).hexdigest(),probe=dict(creatorCases=360,getterCases=216,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete unchanged child creator uses explicit Skill31/MissileSkill and skill source128. It captures physical as well as skill/item cold endpoints and additive cold length from current declared owner stats.','The complete unchanged ETypefrze getter delivers only cold damage plus raw freeze length. Captured physical, other elements, critical and species percentages do not enter its output; ordinary chill length stays zero. It returns the same cold amount as its damage output.','These bounded source cases establish the existing cold creator/freeze and typed explosion services as reuse candidates without enabling a new handler.'],limits=['Skill endpoints/length, owner stats/weapon selection, mastery and RNG services retain the earlier explicit harness boundaries. Modern full factory/critical/mastery/stat/seed/item equivalence remains separate.','This harness does not execute parent hit04, child area hit01, scheduler/collision or current skill synergies; shared earlier callback evidence is a reuse candidate. Native parent/child/state/input/reload verification is still pending.'])
out=ROOT/'tools/reference/freezing-arrow-damage-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Freezing Arrow damage evidence stale'
else:out.write_text(encoded)
print('Freezing Arrow:360 complete child creator and216 freeze-only getter cases compiled; handler disabled.')
