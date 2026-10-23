#!/usr/bin/env python3
"""Compile unchanged supplementary block/dodge dispatch; pin current Dodge separately."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'

def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text())
 assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 hashes={};blocks={}
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes()
  assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
  hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  match=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert match,name
  end=text.index('{',match.start())+1;depth=1
  while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
  blocks[name]=text[match.start():end];return blocks[name]+'\n'
 dmg=source('source/D2Game/src/UNIT/SUnitDmg.cpp');header=source('source/D2Game/include/UNIT/SUnitDmg.h')
 dodge=routine(dmg,'SUNITDMG_ApplyDodge');block=routine(dmg,'SUNITDMG_ApplyBlockOrDodge')
 flags={n:int(v,16) for n,v in re.findall(r'^\s*(BLOCKFLAG_\w+)\s*=\s*(0x[0-9A-Fa-f]+)',header,re.M)}
 cpp=r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,PLRMODE_WALK=2,PLRMODE_RUN=3,MONMODE_WALK=2,MONMODE_RUN=15,STAT_PASSIVE_EVADE=0,STAT_PASSIVE_DODGE=1,STAT_PASSIVE_AVOID=2;
struct D2UnitStrc{int dwUnitType=0,dwAnimMode=1,pSeed=0;void* pInventory=nullptr;} attacker,defender;
struct D2GameStrc{bool bExpansion=true;} game;
int chances[3]={},shield=0,draw=0,draws=0;
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int){return chances[stat];}
int ITEMS_RollRandomNumber(int*){draws++;return draw;}
int SUNITDMG_GetWeaponBlock(D2UnitStrc*){return 0;}
void COMPOSIT_GetWeaponClassId(D2UnitStrc*,void*,int* value,int,int){*value=0;}
int UNITS_GetBlockRate(D2UnitStrc*,bool){return shield;}
bool UNITS_IsInMovingMode(D2UnitStrc* p){return p->dwAnimMode==PLRMODE_WALK || p->dwAnimMode==PLRMODE_RUN;}
'''
 cpp+='constexpr uint32_t '+','.join(n+'='+str(v) for n,v in flags.items())+';\n'+dodge+block
 cpp+=r'''int main(){bool first=true;std::printf("[");
for(int moving:{0,1,2})for(int projectile:{0,1})for(int chance:{0,18,24,56,65})for(int roll:{0,17,18,23,24,55,56,64,65,99})for(int rate:{0,25,75})for(int block:{0,1}){
defender.dwAnimMode=moving==1?PLRMODE_WALK:moving==2?PLRMODE_RUN:1;
chances[0]=0;chances[1]=chance;chances[2]=0;draw=roll;draws=0;shield=rate;
int result=SUNITDMG_ApplyBlockOrDodge(&game,&attacker,&defender,projectile,block);
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d]",moving,projectile,chance,roll,rate,block,result,draws);
}std::printf("]\n");}
'''
 p=STAGE/'amazon-dodge-source-probe.cpp';p.write_text(cpp);binary=STAGE/'amazon-dodge-source-probe'
 subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
 data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==1800
 for moving,projectile,chance,roll,shield,block,result,draws in data:
  rate=shield//3 if moving==2 else shield
  shield_try=block and rate>0;shield_hit=shield_try and roll<rate
  dodge_try=not shield_hit and not moving and not projectile and chance>0
  expected=flags['BLOCKFLAG_BLOCK'] if shield_hit else flags['BLOCKFLAG_DODGE'] if dodge_try and roll<chance else flags['BLOCKFLAG_NONE']
  assert result==expected and draws==int(shield_try)+int(dodge_try)
 current=json.loads((ROOT/'tools/reference/amazon-avoidance-current-tooltips.json').read_text())
 common=source('source/D2Common/src/D2Skills.cpp');helper=routine(common,'D2Common_11033')
 curves={x['sourceKey']:x['sourceFields'] for x in json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text())}
 cc='#include <cstdio>\n#define __stdcall\n'+helper+'int main(){'
 for row in current['values']:
  fields=curves[row['skill']];cc+=f'printf("%d\\n",D2Common_11033({row["effectiveRank"]},{fields["Param1"]},{fields["Param2"]}));'
 cc+='}\n';curvepath=STAGE/'amazon-avoidance-tooltip-curve-probe.cpp';curvepath.write_text(cc);curvebin=STAGE/'amazon-avoidance-tooltip-curve-probe'
 subprocess.run(['g++','-std=c++20','-O2',str(curvepath),'-o',str(curvebin)],check=True)
 legacy=list(map(int,subprocess.check_output([str(curvebin)],text=True).splitlines()));comparisons=[]
 for row,old in zip(current['values'],legacy,strict=True):
  fields=curves[row['skill']];rank=row['effectiveRank'];a=fields['Param1'];b=fields['Param2'];native=a+(b-a)*min(100,110*rank//(rank+6))//100
  assert native==row['reportedPercent']
  comparisons.append(row|dict(currentModelPercent=native,compiledLegacyPercent=old,distinguishesRounding=native!=old))
 assert [x['compiledLegacyPercent'] for x in comparisons]==[30,37,40]
 fixture=STAGE/'amazon-dodge-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 skill=next(x for x in json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text()) if x['talentId']=='amazon.dodge')
 assert skill['sourceFields']['passivecalc1']=='dm12' and skill['sourceFields']['Param8']==2
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,
  compiledRoutines=list(blocks),blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},pinnedSkill=skill,
  probe=dict(cases=len(data),fixture=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),
  currentBuildTooltips=dict(source='tools/reference/amazon-avoidance-current-tooltips.json',comparisons=comparisons,scope='Dodge3 and Avoid3 distinguish current two-stage rounding from supplementary multiplication-first; Evade6 agrees with both. Other ranks/contexts remain unverified.'),
  officialGuide=dict(url='https://classic.battle.net/diablo2exp/skills/amazon-passive.shtml',rank1To20=[18,24,29,34,37,40,42,44,46,47,49,50,51,52,52,54,54,55,55,56]),
  modernAnimation=dict(url='https://news.blizzard.com/en-gb/article/23788293/diablo-ii-resurrected-patch-2-4-ladder-now-live',scope='Released2.4 Dodge/Avoid animation is interruptible and no longer locks actions. Current Dodge Param8=2 identifies interruptible animation. Do not reproduce old attack cancellation/animation lock.'),
  established=['Unchanged full block/dodge dispatch attempts shield first, then stationary melee Dodge only after failed/no shield block. It draws only for positive chances; equality fails Dodge. Walking/running selects Evade instead; projectile selects Avoid instead. Both are zero in this bounded matrix.',
   'Pinned Dodge is a learned effective-rank unrestricted passive, dm12 parameters10/65, no mana/active binding and current interruptible animation mode2. Official historical20-rank table corroborates the retained shared curve, not modern hidden RNG/stat/event parity.'],
  services=['Declared integer RNG draw reused by each requested draw; trace counts establish calls/order, not seeded independence. Declared block rate, mode/stats and absent Assassin weapon block; full RNG/stat/animation/damage consumers are outside this probe.'],
  limits=['No modern executable/class certificate. Only player dispatch, zero Avoid/Evade/weapon-block and bounded chance/block modes exercised. No native integration established by this source probe.',
   'Sanctuary accepted running steps disable all avoidance as an approved existing deviation; source running block/evade is not copied. Native accepted walking lease is a mode adapter, not exact modern animation proof.',
   'Exact current Dodge animation/seed/owner/PvP/special-skill paths, other curve ranks/contexts and item-only/oskill grants remain separate.'])
 target=ROOT/'tools/reference/amazon-dodge-engine-evidence.json';rendered=json.dumps(evidence,indent=2)+'\n'
 if check:assert target.read_text()==rendered,'Dodge evidence changed'
 else:target.write_text(rendered)
 print('Dodge:1800 compiled block/avoidance dispatch cases pass; current animation and approved running deviation recorded separately.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
