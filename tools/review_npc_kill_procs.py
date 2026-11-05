#!/usr/bin/env python3
"""Pin the actual on-kill registration and complete older event/cast bodies."""
import argparse,hashlib,json
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={};bodies={}
 for rel,names in {'source/D2Game/src/UNIT/SUnitDmg.cpp':['SUNITDMG_ExecuteEvents','SUNITDMG_DrainItemDurability'],'source/D2Game/src/SKILLS/SkillItem.cpp':['SKILLITEM_EventFunc20_SkillOnAttackHitKill'],'source/D2Game/src/SKILLS/SkillNec.cpp':['SKILLS_SrvDo030_Curse']}.items():
  p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p)
  for n in names:bodies[n]=function(raw.decode(),n)
 events=bodies['SUNITDMG_ExecuteEvents'];assert events.index('UNITEVENT_DOMELEEDMG')<events.index('UNITEVENT_KILLED')<events.index('UNITEVENT_KILL,')
 assert '!(pDamage->wResultFlags & DAMAGERESULTFLAG_32)' in events
 finish=bodies['SUNITDMG_DrainItemDurability'];assert finish.index('SUNITDMG_ExecuteEvents(')<finish.index('UNITEVENT_DOMELEEATTACK')<finish.index('SUNITDMG_ExecuteMissileDamage(')
 proc=bodies['SKILLITEM_EventFunc20_SkillOnAttackHitKill'];assert proc.index('pDamage && !(pDamage->dwHitFlags & 0x20)')<proc.index('ITEMS_RollRandomNumber')
 current=ROOT/'tools/reference/d2r-named-equipment/json/itemstatcost.json';raw=json.loads(current.read_text())['item_skillonkill'];assert raw['*ID']==196 and raw['itemevent1']=='kill' and raw['itemeventfunc1']==20 and not raw.get('itemevent2')
 registry=ROOT/'data/Sanctuary/Balance/npc-item-combat-events.json';assert next(r for r in json.loads(registry.read_text())if r['Stat']==196)['Events']==[dict(Event='kill',Function=20)]
 reuse=['tools/reference/weapon-event-order-review.json','tools/reference/item-proc-callbacks-source-evidence.json','tools/reference/unit-events-source-evidence.json','tools/reference/item-skill-execution-metadata.json']
 for name in [str(current.relative_to(ROOT)),str(registry.relative_to(ROOT)),*reuse]:inputs[name]=sha(ROOT/name)
 e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,scope='Static review of complete older bodies and current pinned registration. Compiled callback/event controls and native actual delivery are separate evidence.',stat=196,event='kill',function=20,inputs=inputs,completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},
  established=['Current stat196 registers kill/function20,not death/level-up function30. It retains outgoing packet hitflag32 and chance/RNG rules.','Source damage emits killed then kill after HP commitment/will-die selection and before the later ordinary melee-attempt events and native death-mode return. Result32suppresses kill events.','Native Alive remains true during the source pre-death HP0 stage. A successful lethal ordinary contact is distinct from a later cast against a parked/deleted body.'],
  remaining=['Current executable parity,post-death/removed-target or ground aim,incoming/death/level-up/missile producers and arbitrary NPC spells remain unverified or gated.'])
 p=ROOT/'tools/reference/npc-kill-proc-source-review.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==encoded,'NPC kill source review stale'
 else:p.write_text(encoded)
 print('NPC kill source review:current196/kill/function20;complete older event/callback/do30 bodies and pre-death chronology pinned;not current executable proof.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
