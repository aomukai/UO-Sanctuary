#!/usr/bin/env python3
"""Static source trace for prepared ordinary-monster Valkyrie event ownership.

This reviews complete pinned source bodies; it does not execute the outer
monster mode dispatcher or establish current executable equivalence.
"""
import argparse,hashlib,json,re
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def review(check=False):
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 inputs={};bodies={}
 def source(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(t['sha']for t in tree['tree']if t['path']==rel)
  inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 mode=source('source/D2Game/src/MONSTER/MonsterMode.cpp');unit=source('source/D2Game/src/UNIT/SUnit.cpp')
 for text,names in [(mode,['sub_6FC645E0','sub_6FC64790','sub_6FC62D90','MONSTERMODE_GetModeChangeInfo','sub_6FC64450','sub_6FC644E0']),(unit,['sub_6FCBC4D0','sub_6FCBCE70'])]:
  for n in names:bodies[n]=function(text,n)
 entry=bodies['sub_6FC645E0'];assert 'SUNIT_SetCombatMode'in entry and 'SUNITDMG_AllocCombat'not in entry
 callback=bodies['sub_6FC64790'];assert callback.index('sub_6FC62D90(pUnit, pGame)')<callback.index('SUNITDMG_DrainItemDurability')
 alloc=bodies['sub_6FC62D90'];assert alloc.index('SUNIT_GetTargetUnit')<alloc.index('SUNITDMG_GetResultFlags')<alloc.index('SUNITDMG_AllocCombat')
 scheduler=bodies['sub_6FCBCE70'];assert 'UNITS_GetFrameBonus'in scheduler and 'EVENTTYPE_MODECHANGE'in scheduler and 'EVENTTYPE_ENDANIM'in scheduler
 callbacks=re.search(r'constexpr D2MonModeCallbackTableStrc gMonModeCallbacks\[16\] =\s*\{(.*?)\n\};',mode,re.S).group(1)
 assert 'sub_6FC645E0, sub_6FC64790' in callbacks.splitlines()[5]
 p=ROOT/'tools/reference/d2r-named-equipment/json/monstats2.json';inputs[str(p.relative_to(ROOT))]=sha(p);current=json.loads(p.read_text())['valkyrie'];assert not current.get('A1mv',0)
 parent=ROOT/'tools/reference/valkyrie-animation-source-evidence.json';inputs[str(parent.relative_to(ROOT))]=sha(parent);animation=json.loads(parent.read_text());assert animation['contexts']==1368 and animation['originalLoadouts']==57
 result=dict(referenceVersion='3.3.93847',currentExecutableParity=False,outerDispatcherExecuted=False,review='Complete unchanged older source bodies inspected; prior independent animation oracle retained.',inputs=inputs,completeRoutineHashes={k:hashlib.sha256(v.encode()).hexdigest()for k,v in bodies.items()},
  established=['Ordinary monster A1 mode entry selects combat mode. Its ordinary event callback calls target lookup, result allocation and combat allocation, then durability release. A prepared windup must not preallocate this packet.', 'Current Valkyrie has no A1 movement-enabled flag. The prior complete transformed-rate/event oracle covers1368contexts and57original loadouts; it supplies the single action marker followed by end animation used by the shared controller.'],
  limits=['Outer dispatcher, special-mode callbacks and current executable parity are not executed/certified by this static review. Native automatic AI and event delivery are tested separately.', 'Post-transform running/base-rate callback ownership remains unresolved; attack/walk/run bases are explicit supplied inputs. Learned summon, nonzero sunder and unsupported contact adapters remain gated.'])
 path=ROOT/'tools/reference/valkyrie-ground-contact-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==encoded,'Source trace receipt stale'
 else:path.write_text(encoded)
 print('Valkyrie source trace:eight complete bodies reviewed;ordinary contact at action event;outer/current executable parity unclaimed.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
