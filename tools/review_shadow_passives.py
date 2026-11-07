#!/usr/bin/env python3
"""Review actual layered Shadow passives without claiming NPC combat admission."""
import argparse,json
from pathlib import Path
import skill_checkpoint_evidence as e
ROOT=e.ROOT
def review(check=False):
 inputs={}
 def pin(p):inputs[str(p.relative_to(ROOT))]=e.sha(p);return p.read_bytes()
 for n in ['shadow-passives-source-evidence.json','shadow-skill-metadata-data-evidence.json','weapon-block-rank20-deviation.json']:
  proof=json.loads(pin(ROOT/'tools/reference'/n))
  for group in ['inputs','artifacts']:
   for p,h in proof.get(group,{}).items():assert e.sha(ROOT/p)==h,p;pin(ROOT/p)
 source=e.load(ROOT/'tools/reference/shadow-passives-source-evidence.json');assert source['sourceContexts']==1600 and source['currentPassiveStatMatches']==source['currentPassiveCalcPresenceMatches']==28 and source['currentParameterMatches']==12
 rule=e.load(ROOT/'tools/reference/weapon-block-rank20-deviation.json');assert rule['effectiveRank']==20 and rule['chancePercent']==58 and rule['status']=='user-approved Sanctuary adaptation'
 for n in ['ShadowPassiveGraph.cs','GeneratedNpcStatGraph.cs','GeneratedNpcLiveResources.cs','AssassinShadowBody.cs','BattleCommand.cs','SkillMechanics.cs','ShadowSkillBook.cs','EquipmentStatGraph.cs','EquipmentStatValues.cs']:
  pin(ROOT/'src/Sanctuary.Content'/n)
 for n in ['ShadowBodyChecks.cs','ShadowPassiveChecks.cs','WeaponBlockChecks.cs']:pin(ROOT/'tests/Sanctuary.Integration'/n)
 pin(ROOT/'tests/check_shadow_passives.py');pin(Path(__file__).resolve())
 result=dict(referenceVersion='3.3.93847',sourceContexts=1600,passiveStatRefreshImplemented=True,passiveStates=[152,155],passiveLayer=68,allAndElementCallbacksImplemented=True,commandRefreshImplemented=True,
  oskillMaterializationImplemented=False,foreignSkillsImplemented=False,shadowDefenseOrMeleeEnabled=False,handlerEnabled=False,currentExecutablePassiveParityVerified=False,parkedOnly=True,approvedWeaponBlockRank20Percent=58,inputs=inputs,
  scope=['Fresh real Shadow bodies post Claw Mastery and Weapon Block lists into the existing original-item numeric graph. Own effective ranks set source layer68values and modifierlist_skill/level caches;unchanged cached rank skips stat writes and gfx notifications. Rank0removes the list while an owned zero-base entry retains its intrinsic state;removing the entry removes its state.','Source-element126/allskills127callbacks refresh existing passive lists at the existing root value-callback boundary. Explicit book changes and Battle Command application/removal refresh through the same manager. NPC single-skill107doesnot refresh immediately,as in the older callback;no per-read refresh hides that difference. Oskill97materialization and transformed class/tree callbacks remain separate.','Saved numeric lists retain their cached rank/values and original attachment order among real item lists. Reconstruction restores only validated independent numeric passive fields and original links,without replaying formula writes,item callbacks or transient gfx notifications. Unknown lists and corrupt/aliased/item-mismatched caches refuse admission.','Atomic book/passive candidates validate before commit. The existing FP256 ledgers,raw item payloads,actor/world RNG and source difficulty owner remain unchanged. Optional null frame fields preserve old resource-only saves;legacy parked contexts do not silently acquire passives.','Approved Sanctuary adaptation sets Weapon Block effective20to58percent at the shared dm12value boundary,covering player and NPC evaluation/reference consumers. Other ranks and all other diminishing skills retain the existing curve;the pinned data and rank22firsthand evidence are unchanged.'],
  limits=['Complete older per-skill refresh lifecycle uses explicitly declared current formula services;current executable callback/getter/refresh parity and unverified Weapon Block rounding ranks are not claimed. The full original Master assignment/add-refresh chronology remains separately unverified.','Only the two actual Assassin passive rows are admitted. Source states and gfx-change notifications exist in the numeric manager;actual client graphics and native combat admission remain pending. No defense/block/Claw damage consumer is enabled for Shadows yet.','NPC skill permission/target/mode/sequence/events,original ordinary/item combat,AI105/106,modifier42death and learned casting/payment/input remain unfinished. Foreign/charged/oskill materialization and transformed callback contexts stay gated;all original raw item fields are preserved.','Current protected stat recalculator parity and the separate Valkyrie birth-regeneration audit remain open. No class completion or deployment. Appearance,effects,audio and talent/hotkey icons remain separate placeholders.'])
 out=ROOT/'tools/reference/shadow-passives-review-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Layered Shadow passive lists/cache/order and source126/127plus Command refresh reviewed;WeaponBlock20=58approved;NPC combat/casts stay gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
