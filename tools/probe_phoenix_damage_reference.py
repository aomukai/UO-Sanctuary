#!/usr/bin/env python3
"""Run Phoenix's four damaging rows through complete source creators/getters/stat writers."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run(['python3',str(ROOT/'tools/probe_immolation_damage_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 parent=json.loads((ROOT/'tools/reference/immolation-damage-engine-evidence.json').read_text());base=(STAGE/'immolation-damage-source-probe.cpp').read_text().split('int main()',1)[0];raw=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';pin=json.loads((raw.parent.parent/'sources.json').read_text())['files']['json/missiles.json']['sha256'];assert hashlib.sha256(raw.read_bytes()).hexdigest()==pin;tables=json.loads(raw.read_text());names=['royalstrikemeteor','royalstrikechainlightning','royalstrikechaosice','royalstrikemeteorfire'];rates=[10,13,10,6]
 base=base.replace('return damage*mastery/100;','return int(int64_t(damage)*mastery/100);')
 base=base.replace('enum Stats{','enum Stats{STAT_PASSIVE_LTNG_MASTERY,STAT_PASSIVE_COLD_MASTERY,').replace('#define __stdcall','#define D2_MAYBE_UNUSED(x) (void)(x)\n#define __stdcall')
 base=base.replace('int dwElemMin=12,','int dwElemLen=0,dwElemLevLen[3]={};int dwElemMin=12,').replace('int MISSILE_GetElementalLength(int,D2UnitStrc*,int,int){return 0;}','int MISSILE_GetElementalLength(int,D2UnitStrc*,int,int);')
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Common/src/Units/Missile.cpp';src=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(src)).encode()+b'\0'+src).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);s=src.decode();m=re.search(r'^.*\bMISSILE_GetElementalLength\([^;\n]*\)\s*\n\{',s,re.M);i=s.index('{',m.start())+1;depth=1
 while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
 body=s[m.start():i];base+='\n'+body+'\nvoid table(int kind){switch(kind){'
 for index,key in enumerate(names):
  r=tables[key];assert r['ApplyMastery']==1 and not r.get('Skill') and not r.get('SrcDamage',0);base+=f'case {index}:row.nElemType=ELEMTYPE_{dict(fire="FIRE",ltng="LTNG",cold="COLD")[r["EType"]]};row.dwElemMin={r["EMin"]};row.dwElemMax={r["EMax"]};row.nHitShift={r["HitShift"]};row.dwElemLen={r.get("ELen",0)};'
  for j in range(5):base+=f'row.dwMinElemLev[{j}]={r.get("MinELev"+str(j+1),0)};row.dwMaxElemLev[{j}]={r.get("MaxELev"+str(j+1),0)};'
  for j in range(3):base+=f'row.dwElemLevLen[{j}]={r.get("ELevLen"+str(j+1),0)};'
  base+='break;'
 base+='}}\n'+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool comma=false;printf("[");for(int kind:{0,1,2,3})for(int rank:{1,3,8,16,20,21,22,23,28,29,40,255})for(int hard:{0,1,20})for(int boost:{0,25,100,125})for(int item:{0,99999}){
owner={};weapon={};missile={};row={};missileData={};missileData.nLevel=rank;missileData.nSkill=280;weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;row.nSkill=row.nSrcDamage=row.nSrcMissDmg=0;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_APPLYMASTERY];table(kind);percentage=hard*(kind==0?10:kind==1?13:kind==2?10:6);mastery=boost;critical=true;criticalCalls=masteryCalls=0;skillMin=skillMax=0;for(int& n:owner.stats)n=item;
D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,nullptr,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&d,rank);int lo=kind==1?d.nLightMinDamage:kind==2?d.nColdMinDamage:d.nFireMinDamage,hi=kind==1?d.nLightMaxDamage:kind==2?d.nColdMaxDamage:d.nFireMaxDamage;int stored=missile.stats[kind==1?STAT_LIGHTMINDAM:kind==2?STAT_COLDMINDAM:STAT_FIREMINDAM];
if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",kind,rank,hard,boost,item,lo,hi,d.nColdLength,d.nMinDamage,d.nLifeDrainMinDamage,stored,missile.stats[STAT_PASSIVE_FIRE_MASTERY],missile.stats[STAT_PASSIVE_COLD_MASTERY],missile.stats[STAT_PASSIVE_LTNG_MASTERY],criticalCalls);
}printf("]\n");}'''
 cpp=STAGE/'phoenix-damage-source-probe.cpp';cpp.write_text(base);binary=cpp.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(cpp),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==1152
 for kind,rank,hard,boost,item,lo,hi,length,physical,leech,stored,fm,cm,lm,crits in rows:
  r=tables[names[kind]]
  def endpoint(maximum):
   value=r['EMax' if maximum else 'EMin'];left=rank-1
   for j,n in enumerate([7,8,6,6,2**30],1):step=min(left,n);value+=step*r.get(('MaxELev' if maximum else 'MinELev')+str(j),0);left-=step
   value=(value+value*hard*rates[kind]//100)<<r['HitShift'];return value+value*boost//100
  frames=r.get('ELen',0)+min(rank-1,7)*r.get('ELevLen1',0)+min(max(rank-8,0),8)*r.get('ELevLen2',0)+max(rank-16,0)*r.get('ELevLen3',0)
  assert(lo,hi,length,physical,leech,stored,fm,cm,lm,crits)==(endpoint(False),endpoint(True),frames if kind==2 else 0,0,0,lo,0,0,0,0)
 fixture=STAGE/'phoenix-damage-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n');e=dict(referenceVersion='3.3.93847',talentId='assassin.phoenixstrike',installed=False,handlerEnabled=False,sourceComposition='tools/reference/immolation-damage-engine-evidence.json',sourceHashes=parent['sourceHashes'],blockHashes=parent['blockHashes'],creatorBlockHashes=parent['creatorBlockHashes'],additionalLengthSourceHash=hashlib.sha256(src).hexdigest(),additionalLengthRoutineHash=hashlib.sha256(body.encode()).hexdigest(),pinnedMissilesSha256=pin,rows=names,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(cpp.read_bytes()).hexdigest(),cases=len(rows),sanitizers='ASan+UBSan'),established=['Complete missile creator/endpoints/tier/stat writer execute the four current damaging Phoenix rows with source0. Owner weapon/item damage,critical and leech controls do not enter any packet.','Meteor fire/lightning/chaos cold/meteor ground fire apply their separate hard Fists10percent,Claws13percent,Blades10percent,Fists6percent formulas before HitShift8/8/8/3,then mastery once. The full stat writer does not copy mastery for a second impact enhancement.','Complete raw cold length getter uses100base and10/15/20per-level tiers for chaos ice. Source damage2and final cold/freeze application remain separate. Ground fire is raw HitShift3damage; this probe does not assert its collision frequency or displayed DPS.'],services=['Synergy formula and mastery percentage are explicit supplied services. The mastery service uses a64bit intermediate so rank255controls do not overflow the harness;this does not certify the current engine percentage implementation. Complete creator/getters/writer and raw cold length getter execute,not a duplicated runtime formula.','Rank,item/weapon stats and unconditional critical control are supplied. Factory inheritance,target impact,cold/freeze,field cadence,trajectory/NextHit/event/owner/client are not executed.'],next='Compose real meteor factory/impact/fire cadence and chaos-ice damage2/movement; then native learned action and child lifecycle. No handler or class certificate.')
 out=ROOT/'tools/reference/phoenix-damage-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Phoenix damage evidence stale'
 else:out.write_text(encoded)
 print('Phoenix damage:1152 complete creator/getter/length/mastery/stat-writer cases pass;meteor/chain/cold/ground rows remain source preparation.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
