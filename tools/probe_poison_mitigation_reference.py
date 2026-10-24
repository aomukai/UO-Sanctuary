#!/usr/bin/env python3
"""Reuse complete total-damage/resistance routines for poison rate and frame length."""
from pathlib import Path
import argparse,hashlib,json,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
subprocess.run([sys.executable,str(ROOT/'tools/probe_freeze_state_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
shared=json.loads((ROOT/'tools/reference/freeze-state-engine-evidence.json').read_text());base=(STAGE/'freeze-state-source-probe.cpp').read_text().split('int main()',1)[0]
old='bool STATES_CheckState(D2UnitStrc* u,int state){return state==STATE_UNINTERRUPTABLE && u->uninterruptable;}'
assert base.count(old)==1;base=base.replace(old,'bool poisonShrine=false;\nbool STATES_CheckState(D2UnitStrc* u,int state){return state==STATE_SHRINE_RESIST_POISON?poisonShrine:state==STATE_UNINTERRUPTABLE && u->uninterruptable;}')
base+=r'''int main(){bool first=true;std::printf("[");
for(int kind:{0,1})for(int diff:{0,1,2})for(int rate:{1,32,257,1048577})for(int length:{1,3,200,1550,12900})for(int resist:{-100,0,33,100,150})for(int plr:{-100,0,33,75,150})for(int pierce:{0,25,100})for(int cap:{0,20})for(int shrine:{0,1})for(int mdr:{0,100}){
 reset();game.nDifficulty=diff;attacker.dwUnitType=1-kind;defender.dwUnitType=kind;difficulty[diff].dwResistPenalty=diff==0?0:diff==1?-40:-100;poisonShrine=shrine;
 attacker.stats[STAT_PASSIVE_POIS_PIERCE]=pierce;defender.stats[STAT_POISONRESIST]=resist;defender.stats[STAT_ITEM_POISONLENGTHRESIST]=plr;defender.stats[STAT_MAXPOISONRESIST]=cap;defender.stats[STAT_MAGIC_DAMAGE_REDUCTION]=mdr;
 D2DamageStrc d;d.dwPoisDamage=rate;d.dwPoisLen=length;SUNITDMG_CalculateTotalDamage(&game,&attacker,&defender,&d);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",kind,diff,rate,length,resist,plr,pierce,cap,shrine,mdr,d.dwPoisDamage,d.dwPoisLen,d.dwAbsLife);
}std::printf("]\n");}
'''
path=STAGE/'poison-mitigation-source-probe.cpp';path.write_text(base);binary=STAGE/'poison-mitigation-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==72000
pin=ROOT/'tools/reference/weapon-leech-difficulty-source.json';difficulties=json.loads(pin.read_text());penalties=[difficulties[n]['ResistPenalty'] for n in ('Normal','Nightmare','Hell')];assert penalties==[0,-40,-100]
def scale(value,percent):return value*percent//100 if value<=1048576 else (value//100)*percent
for kind,diff,rate,length,resist,plr,pierce,cap,shrine,mdr,outRate,outLength,absorbed in data:
 def effective(raw,maximum):
  value=raw-(pierce if raw<100 or kind==0 else 0)
  if kind==0:value+=penalties[diff]
  if value>0 and kind==0:value=min(value,maximum)
  return min(100,max(-100,value))
 rateRes=effective(resist,min(95,75+cap));lengthRes=effective(plr,75)
 expectedRate=scale(rate,100-rateRes) if rateRes else rate;expectedLength=0 if shrine else scale(length,100-lengthRes) if lengthRes else length
 assert(outRate,outLength,absorbed)==(expectedRate,expectedLength,0),(kind,diff,rate,length,resist,plr,pierce,cap,shrine,mdr,outRate,outLength,expectedRate,expectedLength)
fixture=STAGE/'poison-mitigation-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
names=['MONSTERUNIQUE_CalculatePercentage','SUNITDMG_ApplyResistancesAndAbsorb','SUNITDMG_CalculateTotalDamage']
e=dict(referenceVersion='3.3.93847',handlerEnabled=False,legacyCommit=shared['legacyCommit'],sourceHashes={k:v for k,v in shared['sourceHashes'].items() if k.endswith(('SUnitDmg.cpp','MonsterUnique.cpp'))},compiledRoutines=names,blockHashes={n:shared['blockHashes'][n] for n in names},difficultySource=dict(path=str(pin.relative_to(ROOT)),sha256=hashlib.sha256(pin.read_bytes()).hexdigest(),expansionPenalties=penalties),probe=dict(cases=len(data),path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete PvM total-damage/resistance bodies independently mitigate poison rate and frame length. Poison length uses poison-length resistance and passive poison pierce, with no max-poison-resistance stat; rate uses ordinary poison resistance and its maximum. Neither lane receives flat MDR or absorption.','The declared expansion game difficulty penalty applies to both player poison resistance and poison-length resistance. Positive player length resistance caps75; positive rate resistance caps75+max-res up to95. Negative values floor-100. NPC positive resistance can reach immunity100; passive pierce does not break that lane when its initial resistance is100or more.','Poison-resist shrine zeroes length before resistance, without requiring the rate lane to become zero. Nonzero resistance on large raw rate values above1048576 uses the full percentage helper’s early division; zero resistance bypasses that helper and retains the raw value. Ordinary source frame lengths here multiply before division.'],limits=['Complete source total-damage, resistance and percentage bodies execute with declared stats, room/world, classification, state, difficulty and event services. NPCs here are ordinary non-hireling/non-prime-evil actors; attacks are PvM, without cooperative PvP adaptation. No modern sunder, global event queue, item stat projection or final poison application is certified.','Raw rates/frame lengths are declared prepared packets; this does not exercise skill/item poison preparation or rolls. The selected source poison shrine is supplied as a state service. Native global versus per-monster difficulty context, exact item pierce/length projection and modern executable equivalence remain separate.','Existing aggregate total/duration producers can round differently from per-frame integer rate mitigation. This fixture prepares their deliberate migration; no Poison Javelin handler or changed mitigation caller is enabled here.'])
out=ROOT/'tools/reference/poison-mitigation-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Poison mitigation evidence stale'
else:out.write_text(encoded)
print('Poison mitigation:72000 complete PvM rate/frame-length, player difficulty/caps, NPC immunity/pierce, shrine and MDR controls compiled.')
