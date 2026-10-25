#!/usr/bin/env python3
"""Compose complete generic missile fill/roll with Ice cube damage10."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run(['python3',str(ROOT/'tools/probe_magic_arrow_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Game/src/MISSILES/MissMode.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);text=raw.decode();m=re.search(r'^.*\bMISSMODE_SrvDmg10_BladesOfIceCubes\([^;\n]*\)\s*\n\{',text,re.M);i=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
 body=text[m.start():i];base=(STAGE/'magic-arrow-source-probe.cpp').read_text().split('int main(){',1)[0];base=base.replace('struct D2DamageStrc{','struct D2DamageStrc{int dwFrzLen;')+'\n'+body+r'''
int main(){bool comma=false;printf("[");for(int damage:{256,320,2560})for(int length:{0,1,100,2640})for(int mastery:{0,25})for(int known:{0,1})for(int kind:{0,1,2,3}){
missile={};target={};target.dwUnitType=UNIT_MONSTER;target.kind=kind;validRow=known;missile.stats[STAT_COLDMINDAM]=missile.stats[STAT_COLDMAXDAM]=damage;missile.stats[STAT_COLDLENGTH]=length;missile.stats[STAT_PASSIVE_COLD_MASTERY]=mastery;
D2DamageStrc d={};MISSMODE_FillDamageParams(&missile,&target,&d);MISSMODE_SrvDmg10_BladesOfIceCubes(&game,&missile,&target,&d);if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",damage,length,mastery,known,kind,d.dwColdDamage,d.dwColdLen,d.dwFrzLen,d.dwPhysDamage,d.dwLifeLeech);
}printf("]\n");}
''';path=STAGE/'ice-impact-source-probe.cpp';path.write_text(base);binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(path),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==192
 for damage,length,mastery,known,kind,cold,clen,freeze,physical,leech in rows:assert(cold,clen,freeze,physical,leech)==(damage+damage*mastery//100,length,length if known else 0,0,0)
 fixture=STAGE/'ice-impact-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n');e=dict(referenceVersion='3.3.93847',talentId='assassin.bladesofice',reusedCompleteComposition='tools/reference/magic-arrow-engine-evidence.json',sourceHashes={rel:hashlib.sha256(raw).hexdigest()},cubeDamage10FunctionHash=hashlib.sha256(body.encode()).hexdigest(),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(path.read_bytes()).hexdigest(),cases=len(rows),sanitizers='ASan+UBSan'),established=['Complete generic fill/roll reads the saved cold endpoints and cold length. Full cube damage10copies that length into the freeze lane without clearing the cold lane.','A cube therefore has both ordinary chill and freeze lengths after this route; source state consumers must run in cold-then-freeze order. The complete creator/setter separately proves no stored mastery,avoiding the positive-control second enhancement.'],services=['Prepared missile stat endpoints/length/mastery and target species are explicit inputs. Same min/max means no damage RNG variation; complete generic roll/fill executes.','This composes fill and damage10; outer collision/SetDamageFlags/health/event services remain separate shared evidence.'],limits=['No native Ice handler or full current executable certificate. Preserve the separate cold/freeze duration,eligibility,corpse and lifecycle acceptance gaps.']);out=ROOT/'tools/reference/ice-impact-source-evidence.json';s=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==s,'Ice impact evidence stale'
 else:out.write_text(s)
 print('Ice impact:192 complete generic fill/roll/damage10cases pass; both chill and freeze lanes retained.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
