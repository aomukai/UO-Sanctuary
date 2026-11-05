#!/usr/bin/env python3
"""Strict full-source and native acceptance of ordinary NPC flat-drain delivery."""
import argparse,sys
from pathlib import Path
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/npc-flat-drains';RETAINED=ROOT/'.local/class-skills/npc-flat-drains-retained';FULL=ROOT/'.local/class-skills/npc-flat-drains-full-fill';PARENT='24aa42e2c173ee4b2df8de52f1324c894414b52a'
sys.path.insert(0,str(ROOT/'tests'))
from check_npc_damage_fill import configuration
CHANGED=['src/Sanctuary.Content/'+n for n in('NpcFlatDrains.cs','NpcEquipmentImpact.cs','PreparedMonsterDamage.cs','IronGolemContact.cs')]+['tests/Sanctuary.Integration/NpcFlatDrainChecks.cs']
def families(names,extra=None):
 wanted=Counter(extra or{});wanted['iron-golem-birth-check']+=3
 for name in('iron-golem-items','npc-resource-callbacks',*names):wanted[name+'-check']+=1;wanted[name+'-reload-check']+=2
 return wanted
def build(check=False):
 born=e.load(STAGE/'birth-inputs.json');audit=e.load(STAGE/'audit-inputs.json')
 for stage,file in((STAGE,'birth-inputs.json'),(RETAINED,'birth-inputs.json'),(FULL,'birth-inputs.json'),(STAGE,'audit-inputs.json')):
  data=e.load(stage/file);assert data['artifacts']==born['artifacts']and data['copiedData']==born['copiedData'];snapshot(STAGE/'pre-build-3.json',stage/file)
  for p,h in data['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
 for kind in('Content','Integration'):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in(STAGE/(kind.lower()+'-build-3.log')).read_text()for t in('Build succeeded.','0 Warning(s)','0 Error(s)'))
 sourcepath=ROOT/'tools/reference/npc-flat-drains-source-evidence.json';source=e.load(sourcepath)
 assert source['cases']==11664 and len(source['completeRoutineHashes'])==5 and not source['currentExecutableParity']
 assert len(source['boundStatAndEventIds'])==87 and [source['boundStatAndEventIds'][n]for n in('STAT_HITPOINTS','STAT_MANA','STAT_STAMINA')]==[6,8,10]
 for group in('inputs','artifacts'):
  for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
 assert len(e.load(ROOT/'.local/class-skills/npc-flat-drains-source/cases.json'))==11664
 assert source['fullFillerNativeComposition']==[2281,49919,825,1025,257,1281,1024,512,512,40,1040,1]
 e.cohort(STAGE,families(('npc-flat-drains',)),12)
 retained=families(('npc-equipment-elements','npc-equipment-poison','npc-equipment-critical','venom','monster-ordered-release'),{'monster-contact-check':3,'iron-golem-ordered-contact-check':3,'venom-result':1,'venom-reload-result':2})
 e.cohort(RETAINED,retained,33)
 config=configuration(compact=True);e.cohort(FULL,families(config['extra_families']),60)
 log=(STAGE/'birth-native-checks.log').read_text();assert log.count('NPC flat drains:11664full-source contexts;12actual full-fill/native HP/resource contacts;')==3
 saved=e.load(STAGE/'npc-flat-drains-fixtures.json');assert saved['Generation']==2 and saved['Actor']!=saved['Target']
 assert [saved[n]for n in('SourceLife','TargetLife','Mana','Stamina')]==[2281,49919,825,1025]and saved['Stun']['Target']==saved['Target']
 original=e.load(FULL/'generated-npc-stats-fixtures.json');assert original['Generation']==2 and len(original['Holders'])==1566 and sum(len(r['Items'])for r in original['Holders'])==1674
 live=e.load(FULL/'generated-npc-live-resources-fixtures.json');assert live['Generation']==2 and len(live['Actors'])==5
 bridge=e.load(FULL/'bridge-evidence.json');assert bridge['contexts']==96 and bridge['generation']==2
 for p,h in bridge['artifacts'].items():assert e.sha(ROOT/p)==h,p
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==5 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
 audits=e.audits(STAGE,PARENT,born['artifacts']);protected=e.protection()
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 files=[Path(__file__).resolve(),sourcepath,ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tests/check_npc_flat_drains.py',ROOT/'tests/check_npc_flat_drains_retained.py',ROOT/'tests/check_npc_flat_drains_full_fill.py']+[ROOT/p for p in CHANGED]
 for group in('inputs','artifacts'):files += [ROOT/p for p in source[group]]
 files += [ROOT/p for p in bridge['artifacts']]
 files += [ROOT/'docs'/n for n in('npc-flat-drains-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md','pet-adjustment-review.md')]
 files += e.stage_files(STAGE,RETAINED,FULL)
 record=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=3,integrationBuild=3,sourceSnapshot=3,newNativeRun=2,retainedRun=1,fullFillRun=1,sourceContexts=11664,completeOriginalRoutines=5,headerBoundStatAndEventIds=87,newMarkers=12,retainedMarkers=33,fullFillMarkers=60,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,directNativeContacts=12,fullFillerToNativeContacts=1,retainedPhysicalRollsPerBoot=294840,originalNativeHolders=1566,originalNativeItems=1674,originalItemReplayContexts=96,nativePayloadRestoredFromManifest=False,pythonTests=71,verificationCommands=5,auditFingerprints=audits,protection=protected,
 scope='Full ordinary allocated NPC packet now reaches existing mitigation and ordered HP/resource/status receiver. Distinct FP256 life,mana,stamina drain fields preserve literal NPC recovery gate and capped source recovery before HP;mana/stamina deduction after HP and before status/death. Life drain joins direct HP total.11664contexts match five complete unchanged older routines under declared services,with independently pinned87stat/event IDs.12direct contacts and one real full-Fill-to-mitigation/native drain/stun contact,explicit receiver refusal before mutation,and original wounded resources/stun survive two actual reloads. Shared HP/contact/poison/critical/Venom and full item/hand/resource suites pass identical assemblies/data.',
 limits=['Ordinary non-hireling,non-transformed NPC branch only. Source kind/room/metadata/resource/overlay and empty-event services in the source model are explicit;this is not current executable/global RNG/full stat or world parity.',
 'Actual learned controller/generated-item hand admission and existing original/frozen item preflights remain separate;no unsupported supplier is silently enabled. A retired attacker with positive drains needs a retained-resource receiver and currently refuses.',
 'Positive freeze/burn and bypass-resistance metadata refuse explicitly in FromFill until recipient adapters exist. General shield,boss multiplier,source registry/client parity is not established by the bounded ordinary matrix. Sunder300and unreadable Warlock callbacks remain nonblocking.',
 'Native fixture1used default crypt archetype with0drain;corrected fixture uses existing authored Tank profile with100drain. Production was not changed for that failure;logs/binaries are retained. Content3/Integration3/snapshot3andnew native2are accepted.',
 'No handler/class completion,deployment,installed build/save/config/client change or new icon identity. Overlay151uses identified existing native leech placeholder;final art/audio remains separate.'])
 e.write('npc-flat-drains-native-evidence.json',record,files,check)
 print('NPC flat drains:11664full-source contexts;12+33+60native markers/two reloads;96original-item source replay;71Python/audits/protection pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
