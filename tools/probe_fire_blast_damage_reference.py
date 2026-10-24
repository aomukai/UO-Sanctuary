#!/usr/bin/env python3
"""Reuse complete skill getters, missile creator and stat writer for Fire Blast's two stages."""
import argparse,hashlib,json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run([sys.executable,str(ROOT/'tools/probe_poison_javelin_damage_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 old=json.loads((ROOT/'tools/reference/poison-javelin-damage-engine-evidence.json').read_text());base=(STAGE/'poison-javelin-damage-source-probe.cpp').read_text().split('int main()',1)[0]
 a='dwEMin=32,dwEMax=48,dwEMinLev[5]={16,32,48,64,96},dwEMaxLev[5]={16,36,52,68,100},nToHitShift=0';b='dwEMin=6,dwEMax=8,dwEMinLev[5]={3,8,20,38,58},dwEMaxLev[5]={5,11,24,44,66},nToHitShift=7';assert a in base;base=base.replace(a,b).replace('dwELen=200,dwELevLen[3]={50,50,50}','dwELen=0,dwELevLen[3]={0,0,0}').replace('return hard*12;','return hard*11;')
 fields=json.loads((STAGE/'poison-javelin-damage-source-probe.json').read_text())['fields']
 cpp=base+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int ground:{0,1})for(int rank:{1,3,8,16,20,21,22,23,28,29,40,255})for(int points:{0,1,20,100})for(int mastery:{0,25,50})for(int item:{0,99999}){
 owner={};weapon={};missile={};skill={};row={};missileData={};weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=ground?386:385;missileData.nLevel=rank;missileData.nSkill=251;row.nSkill=ground?251:0;row.nSrcDamage=row.nSrcMissDmg=0;skill.nSrcDam=0;skill.nEType=ELEMTYPE_FIRE;hard=points;critical=true;critCalls=0;skillMin=skillMax=0;
 owner.stats[STAT_MINDAMAGE]=item;owner.stats[STAT_MAXDAMAGE]=item;owner.stats[STAT_FIREMINDAM]=item;owner.stats[STAT_FIREMAXDAM]=item;owner.stats[STAT_LIFEDRAINMINDAM]=item;owner.stats[STAT_POISONMINDAM]=item;owner.stats[STAT_POISONMAXDAM]=item;owner.stats[STAT_PASSIVE_FIRE_MASTERY]=mastery;
 D2UnitStrc origin;for(int& v:origin.stats)v=99999;D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&d,rank);
 printf("%s[%d,%d,%d,%d,%d,[",first?"":",",ground,rank,points,mastery,item);first=false;
'''
 for i,name in enumerate(fields):cpp+=f'printf("{"," if i else ""}%d",d.{name});\n'
 cpp+='printf("],%d,%d,%d,%d]",missile.stats[STAT_FIREMINDAM],missile.stats[STAT_FIREMAXDAM],missile.stats[STAT_PASSIVE_FIRE_MASTERY],critCalls);}\nprintf("]\\n");}\n'
 path=STAGE/'fire-blast-damage-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O2','-fwrapv',str(path),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==576
 raw=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';pin=json.loads((raw.parent.parent/'sources.json').read_text())['files']['json/skills.json']['sha256'];assert hashlib.sha256(raw.read_bytes()).hexdigest()==pin;skill=json.loads(raw.read_text())['251']
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
   if ground and v*hard*11>2**31-1:overflow.append([rank,hard,mastery,item,maximum])
   v+=trunc(signed(v*hard*11),100);v+=v*mastery//100;ends.append(v if ground else 0)
  assert(d['nFireMinDamage'],d['nFireMaxDamage'],stored_min,stored_max,stored_mastery,crit_calls)==(*ends,*ends,0,0), (ground,rank,hard,mastery,item,d,stored_min,stored_max,stored_mastery,crit_calls,ends)
  assert all(d[name]==0 for name in ['nMinDamage','nMaxDamage','nDamagePercent','nLifeDrainMinDamage','nPoisonMinDamage','nPoisonMaxDamage','nColdMinDamage','nLightMinDamage'])
  assert not d['nFlags']&2
 out=STAGE/'fire-blast-damage-source-probe.json';out.write_text(json.dumps(dict(fields=fields,cases=data),separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',talentId='assassin.fireblast',handlerEnabled=False,legacyCommit=old['legacyCommit'],sourceHashes=old['sourceHashes'],pinnedSkillHash=pin,compiledRoutines=old['compiledRoutines'],blockHashes=old['blockHashes'],creatorBlockHashes=old['creatorBlockHashes'],legacySignedSynergyOverflow=dict(compilerMode='g++ -O2 -fwrapv; explicit legacy signed32 wrapping assumption, not modern executable evidence',cases=overflow,policy='Extreme combinations exceed signed32 multiplication in legacy getters. Modern behavior unverified; native must not claim equivalence beyond that boundary.'),fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=len(data)),established=['Complete parent385 creator has neither Skill nor MissileSkill and no missile damage: it carries no damaging packet, even with large owner/origin item stats. Ground386 has explicit Skill251 and SrcDam0, so its fresh creator uses retained rank and current supplied five-hard-synergy total/fire mastery; weapon physical/item elemental/poison/leech/critical and origin damage are excluded.', 'Below the explicitly enumerated signed32 overflow boundary, full skill endpoints apply HitShift7, then11percent per total hard synergy point, then fire mastery, with integer truncation at each source layer. Twelve tier-boundary/rank255 contexts are checked with0/1/20/100 total hard points and0/25/50 mastery.', 'Complete setter stores the child endpoints but does not copy owner fire mastery into a missile mastery stat. Later full impact roll therefore must not apply the owner mastery a second time. Landing factory creates a fresh child; parent packet is not inherited.'],services=['Hard-point/formula lookup, owner/weapon/item stats, ratio and metadata/type helpers and empty monster-type stat copy are declared services inherited from the checked creator harness. Full getters, tier/mastery helpers, creator/add/scale and setter execute unchanged.','These are independent stage invocations. Whole factory/event order, exact landing timing, original seed/roll/mitigation/client/current executable and NPC/item-only suppliers remain separate. No native handler is enabled.'])
 p=ROOT/'tools/reference/fire-blast-damage-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Fire Blast:576 complete two-stage creator/getter/mastery/stat-writer cases pass; parent packet is empty, child rolls source skill damage.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
