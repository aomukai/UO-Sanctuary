#!/usr/bin/env python3
"""Bind real parked Shadow bodies/resources without admitting NPC combat/casts."""
import argparse,json,subprocess
from pathlib import Path
import skill_checkpoint_evidence as e
ROOT=e.ROOT;PARENT='73f41bc7cee507d6f60facf10ca1572d75775538'
def review(check=False):
 inputs={}
 def pin(p):inputs[str(p.relative_to(ROOT))]=e.sha(p);return p.read_bytes()
 proof=json.loads(pin(ROOT/'tools/reference/shadow-resource-birth-source-evidence.json'))
 assert proof['referenceVersion']=='3.3.93847'and proof['sourceContexts']==510 and not proof['currentSetterAndCallbackParityVerified']
 for group in ['inputs','artifacts']:
  for p,h in proof[group].items():assert e.sha(ROOT/p)==h,p;pin(ROOT/p)
 dataPath=ROOT/'data/Sanctuary/Balance/necromancer-summons.json';now={r['key']:r['fields']for r in json.loads(pin(dataPath))}
 old={r['key']:r['fields']for r in json.loads(subprocess.check_output(['git','show',PARENT+':'+str(dataPath.relative_to(ROOT))],cwd=ROOT))}
 assert set(now)-set(old)=={'shadowwarrior','shadowmaster'}and all(now[k]==v for k,v in old.items())
 monPath=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json';mon=json.loads(pin(monPath))
 for k in ['shadowwarrior','shadowmaster']:
  assert now[k]=={n:v for n,v in mon[k].items()if isinstance(v,int)}
 for n in ['AssassinShadowBody.cs','ShadowOwnerSnapshot.cs','ShadowLoadout.cs','ShadowEquipmentResources.cs','GeneratedNpcHands.cs','GeneratedNpcLiveResources.cs',
           'NpcEquipmentRandom.cs','PersistentMinions.cs','CreatureProfiles.cs','ValkyrieEquipmentWeapons.cs','ShadowInitializer.cs','ShadowBirthGraph.cs','NpcItemResourceCallbacks.cs']:
  pin(ROOT/'src/Sanctuary.Content'/n)
 for n in ['tools/build_class_skills.py','tests/Sanctuary.Integration/ShadowBodyChecks.cs','tests/check_shadow_body.py']:
  pin(ROOT/n)
 pin(Path(__file__).resolve())
 result=dict(referenceVersion='3.3.93847',parent=PARENT,sourceResourceContexts=510,realShadowBodies=True,parkedOnly=True,handlerEnabled=False,currentSetterAndCallbackParityVerified=False,
  sourceRowsAdded=['shadowwarrior','shadowmaster'],inputs=inputs,scope=['Dedicated serialized AssassinShadowBody captures trained effective rank,level,owner bases and raw sunder stats before actual shared-world unit allocation. Original equipment is generated and retained with exact selection/allocation witnesses and hand identity;the owner items remain original and equipped.','The existing stat graph composes current-helper birth lists with the qualified older resource callback before original item replay. FP256 life/mana/stamina and saved base regeneration use existing resource ledgers and source numeric writers;reconstruction repeats no native pool setters or birth resource callbacks.','One explicit actor stream uses the existing NPC RNG ledger with an additive species binding. Missing Shadow/Valkyrie streams refuse automatic reseeding. A shared original-hand utility preserves the Valkyrie witness shape;old saves omit the additive null fields unchanged.','The optional CanActivate capability defaults true for existing minions. The incomplete Shadow controller opts out,so ordinary roster restore/resume/guarding cannot expose an inert partial fighter. Its original pools still support saves,source difficulty rescale,explicit roster healing and no parked recovery.'],limits=['No NPC skill-list assignment/mode/permission/event execution,AI105/106,UMod42 registration/death effects,ordinary contact/item procs,learned casting/payment/input or active-world controller. InitialSkillRequests are source metadata,not executed NPC skills. Source item skill/event/aura/charge fields are retained without activating those consumers.','Resource admission requires the original parked body and exact birth/loadout;it does not weaken the generic resource-only unsupported-stat guard or activate incomplete item events. Time-dependent items still require an explicit clock and may refuse whole candidate creation without filtering rolls.','Current unit-stat setter/callback ABI parity and full outer current initializer remain separately unverified. Nonzero inherited sunder is retained but has no admitted offensive consumer. The Valkyrie pre-equipment regeneration recipe merits its own follow-up audit;its existing birth/resource values are unchanged. Effects/appearance/audio and tree/hotkey icons remain separate placeholders.'])
 encoded=json.dumps(result,indent=2)+'\n';out=ROOT/'tools/reference/shadow-body-source-evidence.json'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Real parked Shadow bodies/resources:510whole resource/percentage source contexts;two owned source rows;original items/stream/lifecycle reused,NPC combat/casts remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
