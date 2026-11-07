#!/usr/bin/env python3
"""Bind NPC oskill ownership and qualified pre-equipment Shadow skill order."""
import argparse,json
from pathlib import Path
import skill_checkpoint_evidence as e
ROOT=e.ROOT
def review(check=False):
 inputs={}
 def pin(p):inputs[str(p.relative_to(ROOT))]=e.sha(p);return p.read_bytes()
 for n in ['npc-item-skill-grants-source-evidence.json','shadow-skill-metadata-data-evidence.json','shadow-pre-equipment-skills-order.json']:
  proof=json.loads(pin(ROOT/'tools/reference'/n))
  for group in ['inputs','artifacts']:
   for p,h in proof.get(group,{}).items():assert e.sha(ROOT/p)==h,p;pin(ROOT/p)
 source=e.load(ROOT/'tools/reference/npc-item-skill-grants-source-evidence.json');metadata=e.load(ROOT/'tools/reference/shadow-skill-metadata-data-evidence.json')
 assert source['sourceContexts']==4096 and metadata['npcMetadataRows']==429 and metadata['compiledFieldMatches']==3003
 order=e.load(ROOT/'tools/reference/shadow-pre-equipment-skills-order.json');assert order['masterAiInitializationBeforeDo49HelperAndEquipment']and not order['currentConstructorParityVerified']
 for n in ['ShadowSkillBook.cs','ShadowPassiveGraph.cs','GeneratedNpcStatGraph.cs','GeneratedNpcLiveResources.cs','AssassinShadowBody.cs','ShadowBirthGraph.cs','CurrentItemQuality.cs']:
  pin(ROOT/'src/Sanctuary.Content'/n)
 for n in ['NpcItemSkillChecks.cs','ShadowBodyChecks.cs','ShadowSkillBookChecks.cs']:pin(ROOT/'tests/Sanctuary.Integration'/n)
 for n in ['tests/check_npc_item_skills.py','tools/build_shadow_skill_metadata.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/npc-skill-metadata.json','data/Sanctuary/Balance/class-skill-status.json']:pin(ROOT/n)
 pin(Path(__file__).resolve())
 result=dict(referenceVersion='3.3.93847',sourceContexts=4096,npcMetadataRows=429,compiledFieldMatches=3003,oskillMaterializationImplemented=True,originalNpcItemCallbackImplemented=True,originalUniqueOskillProducerVerified=False,
  foreignNonpassiveEntriesSupported=True,foreignPassiveEffectsSupported=False,chargedSkillEntriesImplemented=False,activeAuraOrChannelFallbackImplemented=False,
  currentExecutableParityVerified=False,currentConstructorParityVerified=False,parkedOnly=True,handlerEnabled=False,inputs=inputs,
  scope=['Current compiled metadata now supplies all429NPC intrinsic rows,including raw missing-animation255and explicitly null unknown names. The existing31row Shadow view is byte-for-byte unchanged;old intrinsic saves retain their schema and list order. Owning a raw mode255entry doesnot admit its execution.','NPC97callbacks execute zero-base materialization,source add/assign/refresh ordering,level queries,removal and Attack fallback at the original root value-change boundary. NPC107doesnot materialize or refresh entries;the source packet has no NPC client and its summon-limit helper is player-only. The older freed-pointer tail is represented by pre-unlink fallback rather than dereferencing freed storage.','New Shadow births run the older owner-bound Master initializer before do49helper/equipment. Only registered independent Claw/WeaponBlock passive lists may precede the birth helper. New frames record its exact position among passive/item nodes;source item callbacks can then create/remove entries as original items attach/detach.','An optional persisted callback capability and birth-order marker leave existing passive frames in their historical mode. Reconstruction restores the saved book/cache/order without rerunning item grants. Real item update candidates synchronize the body book and numeric/resource frame before exposing the commit.','Generated roadmap blockers now distinguish completed bodies,resources,books and passives from remaining skill execution/AI/casting;coverage stays203with no class completion.'],
  limits=['The attempted original Widowmaker fixture is blocked by the existing NPC unique-generation guard. Native grant persistence uses a clearly declared source97input alongside supported original equipment;it doesnot certify an original generated oskill item.','Only inactive aura/channel contexts are admitted;full D2GAME_AssignSkill activation/cleanup remains a separate execution service. Current callback/getter/constructor ABI parity and allocator reuse are not certified by the older branch harness.','Foreign passive grants refuse explicitly before owned-book mutation,as the existing independent-passive manager only admits the two actual Assassin rows. Charged skills remain a distinct item-owned/player branch. Nonpassive foreign entries retain metadata without gaining NPC casting or AI selection.','Master initializer ordering is established from complete older routines and AI table linkage;current captured birth-helper/dispatch evidence is separate. The full current constructor,AI105/106,ordinary/item combat,modifier42death and learned Shadow cast/payment/input remain gated.','Actual client effects/audio/icons and public Shadow activation remain unfinished. The parked appearance placeholder is unchanged;no class completion or deployment.'])
 out=ROOT/'tools/reference/npc-item-skills-review-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('NPC oskill ownership/429metadata and source Shadow pre-equipment ordering reviewed;foreign passives,active aura/channel and casting remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
