#!/usr/bin/env python3
"""Current Thunder child tables through complete source creator/getters/writer."""
import argparse,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run(['python3',str(ROOT/'tools/probe_immolation_damage_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 parent=json.loads((ROOT/'tools/reference/immolation-damage-engine-evidence.json').read_text());raw=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';pin=json.loads((raw.parent.parent/'sources.json').read_text())['files']['json/missiles.json']['sha256'];assert hashlib.sha256(raw.read_bytes()).hexdigest()==pin;tables=json.loads(raw.read_text());names=['clawsofthundernova','clawsofthunderbolt']
 base=(STAGE/'immolation-damage-source-probe.cpp').read_text().split('int main()',1)[0]
 base=base.replace('enum Stats{','enum Stats{STAT_PASSIVE_LTNG_MASTERY,')
 base+='\nvoid table(int kind){switch(kind){'
 for index,key in enumerate(names):
  r=tables[key];assert r.get('ApplyMastery')==1 and not r.get('Skill') and not r.get('SrcDamage',0) and r['HitShift']==8;base+=f'case {index}:row.dwElemMin={r["EMin"]};row.dwElemMax={r["EMax"]};row.nHitShift=8;'
  for i in range(5):base+=f'row.dwMinElemLev[{i}]={r.get("MinELev"+str(i+1),0)};row.dwMaxElemLev[{i}]={r.get("MaxELev"+str(i+1),0)};'
  base+='break;'
 base+='}}\n'
 base+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool comma=false;printf("[");
for(int kind:{0,1})for(int rank:{1,3,8,16,20,21,22,23,28,29,40,255})for(int hard:{0,1,20})for(int boost:{0,25,100,125})for(int item:{0,99999}){
owner={};weapon={};missile={};row={};missileData={};missileData.nLevel=rank;missileData.nSkill=269;weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;row.nSkill=0;row.nSrcDamage=row.nSrcMissDmg=0;row.nElemType=ELEMTYPE_LTNG;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_APPLYMASTERY];table(kind);percentage=hard*8;mastery=boost;critical=true;criticalCalls=masteryCalls=0;skillMin=skillMax=0;
for(int& n:owner.stats)n=item;D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,nullptr,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&d,rank);
if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",kind,rank,hard,boost,item,d.nLightMinDamage,d.nLightMaxDamage,d.nMinDamage,d.nMaxDamage,d.nFireMinDamage,d.nLifeDrainMinDamage,missile.stats[STAT_LIGHTMINDAM],missile.stats[STAT_PASSIVE_LTNG_MASTERY],criticalCalls);
}printf("]\n");}
'''
 path=STAGE/'thunder-damage-source-probe.cpp';path.write_text(base);binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O2','-fwrapv',str(path),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==576
 for kind,rank,hard,boost,item,minimum,maximum,physical,maxphysical,fire,leech,stored,storedMastery,crits in data:
  def endpoint(maximum):
   row=tables[names[kind]];v=row['EMax' if maximum else 'EMin'];left=rank-1
   for i,n in enumerate([7,8,6,6,2**30],1):step=min(left,n);v+=step*row.get(('MaxELev' if maximum else 'MinELev')+str(i),0);left-=step
   v=(v+v*hard*8//100)<<8;return v+v*boost//100
  assert(minimum,maximum,physical,maxphysical,fire,leech,stored,storedMastery,crits)==(endpoint(False),endpoint(True),0,0,0,0,minimum,0,0)
 fixture=STAGE/'thunder-damage-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n');e=dict(referenceVersion='3.3.93847',talentId='assassin.clawsofthunder',sourceComposition='tools/reference/immolation-damage-engine-evidence.json',sourceHashes=parent['sourceHashes'],blockHashes=parent['blockHashes'],creatorBlockHashes=parent['creatorBlockHashes'],pinnedMissilesSha256=pin,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(path.read_bytes()).hexdigest(),cases=len(data)),established=['Complete child creator/min-max getters/tier/stat writer execute current Thunder nova/bolt rows with no weapon source. All supplied owner item/physical/fire/leech/critical controls are excluded.','Current hard Phoenix synergy8percent applies to integer missile damage before HitShift8; ApplyMastery adds lightning mastery once afterward. Full stat writer stores the enhanced endpoints without copying owner mastery into the missile.','Both children retain minimum1before synergy,distinct maximum/growth/range data and source0. This is separate from inline skill damage and its after-HitShift synergy ordering.'],services=['Synergy formula returns supplied hard*8,mastery percentage helper returns supplied percentage; current lookup and full creator/getters/writer are reused from the referenced complete composition.','Prepared owner/item/weapon stats and actual target/health/proc/random roll/missile movement are outside this numeric fixture.'],limits=['Scoped child endpoint/stat proof,not a learned handler or full modern executable certificate.','Inline weapon progressive lightning,modern charge retention,NHD/collision/lifecycle/input and final effects/icons remain separate work.']);out=ROOT/'tools/reference/thunder-damage-source-evidence.json';s=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==s,'Thunder child damage evidence stale'
 else:out.write_text(s)
 print('Thunder:576 complete current child creator/getter/mastery/stat-writer cases pass; weapon/item/critical excluded.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
