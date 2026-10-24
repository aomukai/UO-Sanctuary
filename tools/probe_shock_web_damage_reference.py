#!/usr/bin/env python3
"""Reuse complete skill getters, missile creator and stat writer for Shock Web's two stages."""
import argparse,hashlib,json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run([sys.executable,str(ROOT/'tools/probe_poison_javelin_damage_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 old=json.loads((ROOT/'tools/reference/poison-javelin-damage-engine-evidence.json').read_text());base=(STAGE/'poison-javelin-damage-source-probe.cpp').read_text().split('int main()',1)[0]
 a='dwEMin=32,dwEMax=48,dwEMinLev[5]={16,32,48,64,96},dwEMaxLev[5]={16,36,52,68,100},nToHitShift=0';b='dwEMin=2,dwEMax=20,dwEMinLev[5]={0,0,0,0,0},dwEMaxLev[5]={6,12,20,30,42},nToHitShift=7';assert a in base;base=base.replace(a,b).replace('dwELen=200,dwELevLen[3]={50,50,50}','dwELen=0,dwELevLen[3]={0,0,0}').replace('return hard*12;','return hard*17;')
 fields=json.loads((STAGE/'poison-javelin-damage-source-probe.json').read_text())['fields']
 cpp=base+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int ground:{0,1})for(int rank:{1,3,8,16,20,21,22,23,28,29,40,255})for(int points:{0,1,20,40})for(int mastery:{0,25,50})for(int item:{0,99999}){
 owner={};weapon={};missile={};skill={};row={};missileData={};weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=ground?389:388;missileData.nLevel=rank;missileData.nSkill=256;row.nSkill=ground?256:0;row.nSrcDamage=row.nSrcMissDmg=0;skill.nSrcDam=0;skill.nEType=ELEMTYPE_LTNG;hard=points;critical=true;critCalls=0;skillMin=skillMax=0;
 owner.stats[STAT_MINDAMAGE]=item;owner.stats[STAT_MAXDAMAGE]=item;owner.stats[STAT_FIREMINDAM]=item;owner.stats[STAT_FIREMAXDAM]=item;owner.stats[STAT_LIFEDRAINMINDAM]=item;owner.stats[STAT_POISONMINDAM]=item;owner.stats[STAT_POISONMAXDAM]=item;owner.stats[STAT_PASSIVE_LTNG_MASTERY]=mastery;owner.stats[STAT_LIGHTMINDAM]=item;owner.stats[STAT_LIGHTMAXDAM]=item;
 D2UnitStrc origin;for(int& v:origin.stats)v=99999;D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&d,rank);
 printf("%s[%d,%d,%d,%d,%d,[",first?"":",",ground,rank,points,mastery,item);first=false;
'''
 for i,name in enumerate(fields):cpp+=f'printf("{"," if i else ""}%d",d.{name});\n'
 cpp+='printf("],%d,%d,%d,%d]",missile.stats[STAT_LIGHTMINDAM],missile.stats[STAT_LIGHTMAXDAM],missile.stats[STAT_PASSIVE_LTNG_MASTERY],critCalls);}\nprintf("]\\n");}\n'
 path=STAGE/'shock-web-damage-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O2','-fwrapv',str(path),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==576
 raw=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';pin=json.loads((raw.parent.parent/'sources.json').read_text())['files']['json/skills.json']['sha256'];assert hashlib.sha256(raw.read_bytes()).hexdigest()==pin;skill=json.loads(raw.read_text())['256']
 def tier(maximum,rank):
  value=skill['EMax' if maximum else 'EMin'];left=rank-1
  for i,n in enumerate([7,8,6,6,2**30],1):step=min(left,n);value+=step*skill[('EMaxLev' if maximum else 'EMinLev')+str(i)];left-=step
  return value
 overflow=[]
 def signed(v):return (v+2**31)%2**32-2**31
 def trunc(v,n):return (1 if v>=0 else -1)*(abs(v)//n)
 for ground,rank,hard,mastery,item,values,stored_min,stored_max,stored_mastery,crit_calls in data:
  d=dict(zip(fields,values));ends=[]
  for maximum in [False,True]:
   v=tier(maximum,rank)<<7
   if ground and maximum and v*hard*17>2**31-1:overflow.append([rank,hard,mastery,item,maximum])
   v+=trunc(signed(v*hard*17),100) if maximum else 0;v+=v*mastery//100;ends.append(v if ground else 0)
  assert(d['nLightMinDamage'],d['nLightMaxDamage'],stored_min,stored_max,stored_mastery,crit_calls)==(*ends,*ends,0,0), (ground,rank,hard,mastery,item,d,stored_min,stored_max,stored_mastery,crit_calls,ends)
  assert all(d[name]==0 for name in ['nMinDamage','nMaxDamage','nDamagePercent','nLifeDrainMinDamage','nPoisonMinDamage','nPoisonMaxDamage','nColdMinDamage','nFireMinDamage'])
  assert not d['nFlags']&2
 out=STAGE/'shock-web-damage-source-probe.json';out.write_text(json.dumps(dict(fields=fields,cases=data),separators=(',',':'))+'\n')
 assert not overflow
 e=dict(referenceVersion='3.3.93847',talentId='assassin.shockweb',handlerEnabled=False,legacyCommit=old['legacyCommit'],sourceHashes=old['sourceHashes'],pinnedSkillHash=pin,compiledRoutines=old['compiledRoutines'],blockHashes=old['blockHashes'],creatorBlockHashes=old['creatorBlockHashes'],legacySignedSynergyOverflow=dict(compilerMode='g++ -O2 -fwrapv; explicit legacy signed32 wrapping assumption, not modern executable evidence',cases=overflow,policy='No tested valid two-synergy combination through rank255 overflows legacy signed32 multiplication. Broader invalid/foreign-stat combinations are not certified.'),fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=len(data)),established=['Complete parent388 creator has no damaging packet despite large owner/origin item stats. Ground389 has explicit Skill256 and SrcDam0, so its fresh creator reads retained rank/current two-hard-synergy total/lightning mastery; weapon physical/item elemental/poison/leech/critical and origin damage are excluded.', 'Full minimum getter keeps base2<<7=256 unsynergized because first minimum increment is0, at every tested rank and hard total. Maximum applies17percent per total hard point after HitShift7, then mastery, with truncation per source layer.576 independent cases cover twelve tier/rank boundaries,hard0/1/20/40,mastery0/25/50 and owner item sentinels.', 'Complete setter stores child lightning endpoints without copying owner mastery into missile mastery. No second owner mastery belongs at impact; field damage rolls separately on contacts. No tested valid two-synergy/rank255 combination reaches the legacy signed32 overflow boundary.'],services=['Hard-point/formula lookup, owner/weapon/item stats, ratio and metadata/type helpers and empty monster-type stat copy are declared services inherited from the checked creator harness. Full getters, tier/mastery helpers, creator/add/scale and setter execute unchanged.','These are independent stage invocations. Whole factory/event order, exact landing timing, original seed/roll/mitigation/client/current executable and NPC/item-only suppliers remain separate. No native handler is enabled.'])
 p=ROOT/'tools/reference/shock-web-damage-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Shock Web:576 complete two-stage creator/getter/mastery/stat-writer cases pass; parent packet is empty, child rolls source skill damage.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
