#!/usr/bin/env python3
"""Strict bounded source/native receipt for original NPC freeze-target delivery."""
import argparse,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/npc-freeze-hit';RETAINED=ROOT/'.local/class-skills/npc-freeze-hit-retained';DRAINS=ROOT/'.local/class-skills/npc-freeze-hit-drains';FULL=ROOT/'.local/class-skills/npc-freeze-hit-full-fill'
PARENT='c5a45eaf5bcf83bed0c0aa20c40f3c3128814e1d'
sys.path.insert(0,str(ROOT/'tests'))
from check_npc_damage_fill import configuration
from check_npc_freeze_hit_retained import settings
CHANGED=['src/Sanctuary.Content/'+n for n in ('IronGolemContact.cs','NpcCombatEvents.cs','NpcEquipmentImpact.cs','NpcItemProcGraph.cs','PreparedMonsterDamage.cs','ReviveEquipmentEvents.cs','NpcFreezeStatus.cs','NpcItemFreeze.cs')]+['tests/Sanctuary.Integration/'+n for n in ('NpcFreezeHitChecks.cs','ReviveEventChecks.cs')]
def families(names,extra=None):
 wanted=Counter(extra or{});wanted['iron-golem-birth-check']+=3
 for name in ('iron-golem-items','npc-resource-callbacks',*names):wanted[name+'-check']+=1;wanted[name+'-reload-check']+=2
 return wanted

def build(check=False):
 born=e.load(STAGE/'birth-inputs.json')
 for stage,file in ((STAGE,'birth-inputs.json'),(RETAINED,'birth-inputs.json'),(DRAINS,'birth-inputs.json'),(FULL,'birth-inputs.json'),(STAGE,'audit-inputs.json')):
  data=e.load(stage/file);assert data['artifacts']==born['artifacts']and data['copiedData']==born['copiedData'];snapshot(STAGE/'pre-build-8.json',stage/file)
  for path,digest in data['sourceInputs'].items():assert e.sha(ROOT/path)==digest,path
 for kind in ('Content','Integration'):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in(STAGE/(kind.lower()+'-build-8.log')).read_text()for t in('Build succeeded.','0 Warning(s)','0 Error(s)'))
 sourcepath=ROOT/'tools/reference/npc-freeze-hit-source-evidence.json';source=e.load(sourcepath)
 assert source['contexts']==dict(callback=384,status=5760,remove=432)and len(source['completeRoutineHashes'])==6 and not source['currentExecutableParity']
 assert [source['newHeaderBindings'][n]for n in ('STATE_FREEZE','STATE_UNINTERRUPTABLE','EVENTTYPE_REMOVESTATE','EVENTTYPE_AITHINK','STAT_LEVEL','STAT_ITEM_FREEZE')]==[1,54,12,2,12,134]
 for group in ('inputs','artifacts'):
  for path,digest in source[group].items():assert e.sha(ROOT/path)==digest,path
 cases=e.load(ROOT/'.local/class-skills/npc-freeze-hit-source/cases.json');assert {k:len(v)for k,v in cases.items()}==source['contexts']
 assert next(row for row in cases['callback']if row[5])==[1,1,1,0,0,1,1,1,1125,17,25600,1]
 log=e.cohort(STAGE,families(('npc-freeze-hit',)),12)
 assert log.count('NPC freeze:384callback/5760status/432removal source comparisons;')==3
 saved=e.load(STAGE/'npc-freeze-hit-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==2
 assert {a['Revive']for a in saved['Actors']}=={True,False}
 for a in saved['Actors']:
  assert a['Owner']!=a['Source']!=a['Target']and a['SourceLife']>0 and a['TargetLife']>0 and a['Seed']>0
  assert a['Control']['Freeze']['Source']==a['Source']and a['Control']['FreezeUntil']
 c=settings();extra={'monster-contact-check':3,'iron-golem-ordered-contact-check':3,'venom-result':1,'venom-reload-result':2,'revive-native-swing-result':3,'revive-native-swing-save-result':3}
 e.cohort(RETAINED,families(c['extra_families'],extra),45)
 e.cohort(DRAINS,families(('npc-flat-drains',)),12)
 drain=e.load(DRAINS/'npc-flat-drains-fixtures.json');assert drain['Generation']==2 and [drain[n]for n in ('SourceLife','TargetLife','Mana','Stamina')]==[2281,49919,825,1025]
 for name in ('revive-event','revive-native-swing'):assert e.load(RETAINED/(name+'-fixtures.json'))['Generation']==2
 full=configuration(compact=True);e.cohort(FULL,families(full['extra_families']),60)
 original=e.load(FULL/'generated-npc-stats-fixtures.json');assert original['Generation']==2 and len(original['Holders'])==1566 and sum(len(r['Items'])for r in original['Holders'])==1674
 live=e.load(FULL/'generated-npc-live-resources-fixtures.json');assert live['Generation']==2 and len(live['Actors'])==5
 bridge=e.load(FULL/'bridge-evidence.json');assert bridge['contexts']==96 and bridge['generation']==2
 for path,digest in bridge['artifacts'].items():assert e.sha(ROOT/path)==digest,path
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==5 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
 audits=e.audits(STAGE,PARENT,born['artifacts']);protected=e.protection()
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(s['implementation']=='handler-present'for s in status['skills'])==197
 failed=RETAINED/'failed-native-2';assert 'Original drained resource/HP/status replayed or recovered on reload'in(failed/'birth-native-checks.log').read_text()
 timeout=FULL/'failed-native-2';assert "Server did not reach 'generated-npc-physical-check"in(timeout/'native-run-2.log').read_text()
 files=[Path(__file__).resolve(),sourcepath,ROOT/'tools/skill_checkpoint_evidence.py']+[ROOT/p for p in CHANGED]
 files += [ROOT/'tests'/n for n in ('check_npc_freeze_hit.py','check_npc_freeze_hit_retained.py','check_npc_freeze_hit_drains.py','check_npc_freeze_hit_full_fill.py')]
 files += [ROOT/'docs'/n for n in ('npc-freeze-hit-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md','pet-adjustment-review.md')]
 for group in ('inputs','artifacts'):files += [ROOT/p for p in source[group]]
 files += [ROOT/p for p in bridge['artifacts']]+e.stage_files(STAGE,RETAINED,DRAINS,FULL)
 for stage in (STAGE,RETAINED,FULL):
  for directory in stage.iterdir():
   if directory.is_dir()and directory.name.startswith(('failed-','interrupted-')):files += e.stage_files(directory)
 record=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=8,integrationBuild=8,sourceSnapshot=8,newNativeRun=7,retainedRun=3,drainRun=1,fullFillRun=3,sourceContexts=source['contexts'],completeOriginalRoutines=6,newMarkers=12,retainedMarkers=45,drainMarkers=12,fullFillMarkers=60,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,originalNativeHolders=1566,originalNativeItems=1674,originalItemReplayContexts=96,nativePayloadRestoredFromManifest=False,pythonTests=71,verificationCommands=5,auditFingerprints=audits,protection=protected,
 scope='Original Iron and frozen Revive item stat134/function14 share existing chance/RNG/cold/freeze primitives. Zero-result nested status has no invented direct damage,successful hit,return/free or attempt event. Filled NPC packets retain distinct freeze duration with one mitigation pass and HP/stun/cold/freeze/poison ordering. Six complete unchanged older routines execute384callback/5760status/432removal contexts under declared services. Learned original Iceblink Iron and learned original frozen Revive preserve original items,HP,streams,graph subscriptions and active status through two real reloads. Dedicated drain/contact/poison/critical/Venom/frozen Revive event/native A1 AI/saved windup and full generated-item/fill cohorts use identical candidate assemblies/data.',
 limits=['Older-source bounded model with declared room/unit/empty subscribers/RNG/stat-list/scheduler/metadata services;ordinary cold fallback delegates to existing cold. No current executable/global event/world/client parity claim.',
 'Hireling freeze receivers explicitly refuse before item RNG/mitigation. General burn/bypass,Sunder300and unreadable modern Warlock callbacks remain separate gaps.',
 'New freeze-equipped Revive fixture covers learned cast and shared contact;natural pursuit/native A1 with that specific effect still needs a combined gameplay test. Existing native A1 AI/pending-save lifecycle is retained separately.',
 'Combined drain plus longer Revive-AI cohort failed its exact resource/status reload assertion;failed-native-2logs are retained. Dedicated drain12and retained45cohorts pass separately. Combined-world mismatch remains unresolved,not claimed fixed or silently accepted as parity.',
 'Initial retained event fixture assumed four Revives at rank20;its fourth supplier now belongs to a second owner. All four capability cases remain,and the approved cap3is preserved. Earlier callback/online-owner/relative-frame/random-positive fixtures and a compile error have retained diagnostic logs/binaries. Full-fill2timed out waiting for294840rolls;full-fill3uses a180second budget for that command without changing assertions.',
 'No handler/class completion,deployment,installed build/save/config/client mutation,new icon identity or completed effect. Native0x374Ais the identified cold/freeze placeholder;final effect/audio and talent/hotkey assets remain separate.'])
 e.write('npc-freeze-hit-native-evidence.json',record,files,check)
 print('NPC freeze:source384/5760/432;native12+45+12+60/two reloads;71Python/audits/protection;combined-world reload limit explicitly retained.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
