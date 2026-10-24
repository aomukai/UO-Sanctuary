#!/usr/bin/env python3
"""Extend checked complete missile/area wrappers with positive weapon block."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
import probe_amazon_avoid_reference as basis
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 basis.build(check=True)
 original=(STAGE/'amazon-avoid-source-probe.cpp').read_text();cpp=original
 replacements={
  'int SUNITDMG_GetWeaponBlock(D2UnitStrc*){return 0;}':'int SUNITDMG_GetWeaponBlock(D2UnitStrc*){return avoidChance;}',
  'void COMPOSIT_GetWeaponClassId(D2UnitStrc*,void*,int* value,int,int){*value=0;}':'void COMPOSIT_GetWeaponClassId(D2UnitStrc*,void*,int* value,int,int){*value=13;}',
  'int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int){return stat==STAT_PASSIVE_AVOID?avoidChance:0;}':'int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){return 0;}',
 }
 for before,after in replacements.items():assert cpp.count(before)==1;cpp=cpp.replace(before,after)
 path=STAGE/'weapon-block-missile-source-probe.cpp';path.write_text(cpp);binary=STAGE/'weapon-block-missile-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
 data=json.loads(subprocess.check_output([str(binary)]));assert {k:len(v) for k,v in data.items()}==dict(dispatch=1800,flags=7680,area=1440)
 header=(STAGE/'d2moo-pinned-source/source/D2Game/include/UNIT/SUnitDmg.h').read_text();flags={n:int(v,16) for n,v in re.findall(r'^\s*((?:DAMAGERESULTFLAG|BLOCKFLAG)_\w+)\s*=\s*(0x[0-9a-fA-F]+)',header,re.M)}
 for moving,projectile,chance,roll,rate,block,result,draws in data['dispatch']:
  shield=rate//3 if moving==2 else rate;shield_try=block and shield>0;shield_hit=shield_try and roll<shield;weapon_try=not shield_hit and not moving and chance>0
  expected=flags['BLOCKFLAG_BLOCK'] if shield_hit else flags['BLOCKFLAG_WEAPONBLOCK'] if weapon_try and roll<chance else 0
  assert (result,draws)==(expected,int(bool(shield_try))+int(weapon_try))
 for moving,chance,rate,physical,gethit,soft,knock,unt,roll,result,events,commits,draws in data['flags']:
  shield=rate//3 if moving==2 else rate;shield_try=bool(physical and shield>0);shield_hit=shield_try and roll<shield;weapon_try=not shield_hit and not moving and chance>0;weapon_hit=weapon_try and roll<chance
  expected=flags['DAMAGERESULTFLAG_SUCCESSFULHIT']
  if not unt:
   expected|=flags['DAMAGERESULTFLAG_GETHIT'] if gethit else flags['DAMAGERESULTFLAG_SOFTHIT'] if soft else 0
   expected|=flags['DAMAGERESULTFLAG_KNOCKBACK'] if knock and roll<knock else 0
  defense=shield_hit or weapon_hit
  if defense:
   expected&=~flags['DAMAGERESULTFLAG_SUCCESSFULHIT']
   expected|=flags['DAMAGERESULTFLAG_BLOCK'] if gethit or knock else flags['DAMAGERESULTFLAG_SOFTHIT']
  assert (result,events,commits,draws)==(expected,int(bool(expected&1)),1,int(bool(not unt and knock>0))+int(shield_try)+int(weapon_try))
 for moving,chance,roll,initial,missile_owner,unt,result,events,commits,draws,original_flags in data['area']:
  blocked=bool(initial and not moving and roll<chance);expected=initial
  if blocked:expected=(expected|0x8000)&0xFE
  if expected&1 and not unt:expected|=4
  assert (result,events,commits,draws,original_flags)==(expected if initial else 0,int(bool(expected&1)),int(initial!=0),int(initial!=0 and not moving and chance>0),initial),(moving,chance,roll,initial,missile_owner,unt,result,expected,events,commits,draws,original_flags)
 fixture=STAGE/'weapon-block-missile-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n');old=json.loads((ROOT/'tools/reference/amazon-avoid-engine-evidence.json').read_text())
 names=old['compiledRoutines'];evidence=dict(referenceVersion='3.3.93847',talentId='assassin.weaponblock',proofEnablesHandler=False,legacyCommit=old['legacyCommit'],sourceHashes=old['sourceHashes'],compiledUnchangedRoutines={name:old['blockHashes'][name] for name in names},verifiedScaffoldSha256=hashlib.sha256((STAGE/'amazon-avoid-source-probe.cpp').read_bytes()).hexdigest(),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),caseCounts={k:len(v) for k,v in data.items()}),established=['Complete dispatch tries stationary weapon block for both melee and missile selection after shield; moving branches never reach it. Full missile wrapper maps weapon block to BLOCK and clears successful-hit/events. GetHit0/KnockBack0 instead clears success with SOFTHIT, suppressing the block-mode flag.','Complete copied-area wrapper independently blocks each nonzero packet without mutating its shared original. Its WeaponBlock0x8000 result is then removed by the original0xFE mask: damage/events are suppressed but no retained weapon-block reaction flag is established. The supplied repeated draw establishes order/count, not seeded independence.'],services=['Checked unchanged full bodies are inherited from the regenerated Amazon Avoid scaffold. Positive weapon-block stat and composite13 replace only declared services; full layered getter and usable-weapon composition are separately proved. Amazon passives are zero.','Stat supply, RNG draw, tables, uninterruptable state, critical/events/commit and owner lookup remain explicit services. The whole damage actor/stat/event engine is not compiled.'],limits=['Historical source proof alone does not enable a native handler. Real claw equip/live defense, current block reaction/noninterrupt behavior, soft-hit context mapping, persistence and modern client acceptance require separate native evidence.'])
 target=ROOT/'tools/reference/weapon-block-missile-engine-evidence.json';rendered=json.dumps(evidence,indent=2)+'\n'
 if check:assert target.read_text()==rendered
 else:target.write_text(rendered)
 print('Weapon Block:1800 dispatch,7680 missile flag and1440 copied-area complete source cases pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
