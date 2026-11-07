#!/usr/bin/env python3
"""Bind saved NPC skill ownership, separating source coverage from execution."""
import argparse,hashlib,json
from pathlib import Path
import skill_checkpoint_evidence as e
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills'
def review(check=False):
 inputs={}
 def pin(p):inputs[str(p.relative_to(ROOT))]=e.sha(p);return p.read_bytes()
 for name in ['shadow-skill-metadata-data-evidence.json','shadow-skill-assignment-source-evidence.json','npc-innate-skill-rank-source-evidence.json']:
  proof=json.loads(pin(ROOT/'tools/reference'/name))
  for group in ['inputs','artifacts']:
   for p,h in proof.get(group,{}).items():assert e.sha(ROOT/p)==h,p;pin(ROOT/p)
 metadata=e.load(ROOT/'tools/reference/shadow-skill-metadata-data-evidence.json');assignment=e.load(ROOT/'tools/reference/shadow-skill-assignment-source-evidence.json')
 assert metadata['compiledFieldMatches']==3003 and metadata['shadowMetadataRows']==31 and metadata['passiveClawType']==68
 assert assignment['masterWholeInitializerContexts']==255 and assignment['warriorAssignmentSliceContexts']==1530
 tree=e.load(BASE/'d2moo-source-tree.json');path='source/D2Common/src/D2Skills.cpp';raw=pin(BASE/'d2moo-pinned-source'/path)
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==path)
 for n in ['ShadowSkillBook.cs','ShadowOwnerSnapshot.cs','AssassinShadowBody.cs','NpcInnateSkillRank.cs']:
  pin(ROOT/'src/Sanctuary.Content'/n)
 for n in ['tests/Sanctuary.Integration/ShadowSkillBookChecks.cs','tests/Sanctuary.Integration/ShadowBodyChecks.cs','tests/check_shadow_skills.py','data/Sanctuary/Balance/shadow-skill-metadata.json']:
  pin(ROOT/n)
 pin(Path(__file__).resolve())
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],compiledMetadataMatches=3003,masterWholeInitializerContexts=255,warriorAssignmentSliceContexts=1530,
  currentNpcSkillAssignmentParityVerified=False,passiveStatRefreshImplemented=False,itemGrantCallbacksImplemented=False,npcSkillExecutionEnabled=False,handlerEnabled=False,parkedOnly=True,inputs=inputs,
  scope=['Fresh real Shadow bodies own serialized intrinsic entries with base/bonus/mode and left/right/used IDs. Current owned initial skill requests materialize seven entries;older complete Master initialization expands to29,excluding both pettype16Shadow summons. Reassignment preserves initial linked-list order and custom SQ modes,including Blade Fury.','All30Assassin owner hard/effective ranks snapshot at summon under the approved global policy. Master uses the qualified older hard-point initialization rule;the explicit Warrior assignment service uses captured effective ranks and supplied selection IDs. No controller chooses or executes those skills.','The shared qualified NPC bonus getter reads only the pet own allskills/oskills/single/element layers plus Command,with NPC rank99clamp and learned oskill cap3. Player class/tree bonuses are not imported. Intrinsic zero entries,level bonuses,mode updates,selection/removal Attack fallback and atomic body commits retain source semantics within this adapter.','Optional null JSON fields preserve preexisting parked body/capture save shapes. Old captures do not recapture live owner skills;NPC book access refuses the missing context. Existing items,resources,wounds and random streams are unaffected.'],
  limits=['Current assignment/getter/class-list/AI executable parity remains unverified. Source controls execute a whole older Master initializer and an explicitly bounded Warrior assignment slice with declared services;the rest of either think loop is not executed.','Passive entries/metadata are owned,but no passive stat-list refresh or Claw Mastery/Weapon Block defense is admitted yet. Item oskill grants still need actual callbacks/materialization,including foreign skills outside this31row adapter;charged skills remain separate. No item field is dropped or claimed active.','Skill permission/target/mode/sequence/event execution,combat/item-event producers,AI105/106,modifier42death behavior and learned casting/payment remain unfinished. Intrinsic Attack removal is explicitly unsupported. Global area rescaling retains the captured skill book rather than silently reinitializing it. Effects/appearance/audio and separate tree/hotkey icons remain placeholders.'])
 out=ROOT/'tools/reference/shadow-skills-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Shadow intrinsic ownership:3003current metadata matches;255whole older Master/1530Warrior assignment slices;passives,callbacks,execution and AI remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
