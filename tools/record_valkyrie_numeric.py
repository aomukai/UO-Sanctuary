#!/usr/bin/env python3
"""Bind full original Valkyrie numeric recipients without admitting other consumers."""
import argparse,json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-numeric';WIDE=BASE/'valkyrie-numeric-retained';SOURCE=BASE/'valkyrie-numeric-source'
PARENT='f15761315b08e968e31bc23b5571ce6089cc1b9e'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-3.json')
 extra=Counter({'valkyrie-numeric-check':1,'valkyrie-numeric-reload-check':2})
 old=e.load(ROOT/'tools/reference/valkyrie-equipment-native-evidence.json')
 for prior,stage,total in [(BASE/'valkyrie-loadout',STAGE,39),(BASE/'valkyrie-loadout-retained',WIDE,347)]:
  path=prior/'birth-native-checks.log';assert e.sha(path)==old['files'][str(path.relative_to(ROOT))]
  e.cohort(stage,markers(path)+extra,total)
 for stage,run in [(STAGE,4),(WIDE,2)]:
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
  log=(stage/'birth-native-checks.log').read_text();assert "denied by connection filter 'auto-denylist'"not in log
  assert log.count('Valkyrie numeric:57full original loadouts/330items match complete older stat composition')==3
  saved=e.load(stage/'valkyrie-numeric-fixtures.json');assert saved['Generation']==2 and len(saved['Bodies'])==57
  assert sum(len(b['Items'])for b in saved['Bodies'])==330
  assert len({i for b in saved['Bodies']for i in b['Items']})==330
  for held in saved['Bodies']:
   state=json.loads(held['Text']);assert state['EquipmentStatProfile']=='valkyrie-equipment-numeric-v1'
   assert state['Owner']==saved['Owner']and state['Equipment']['Actor']==held['Body']and held['Life']%256==255
   assert [a['Item']for a in state['Equipment']['Attempts']if a['Success']]==held['Items']
   for serial,text in zip(held['Items'],held['Payloads']):
    item=json.loads(text);assert item['Equipment']['Actor']==held['Body']and item['Metadata']['Flags']==0x82010 and item['Equipment']['Flags']==0x1000
 for name in ['content-build-3.log','integration-build-2.log']:
  text=(STAGE/name).read_text();assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 # The final review change adds only a guarded native resistance read and
 # its missing-original control. Preserve the earlier passing binaries/run.
 intermediate=STAGE/'intermediate-1'
 for name in ['Content','Integration']:
  assert e.sha(intermediate/f'Sanctuary.{name}.dll')==e.load(intermediate/'focused/birth-inputs.json')['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
 for directory in [intermediate,STAGE/'failed-1']:
  for p,h in e.load(directory/'inventory.json').items():assert e.sha(directory/p)==h,p
 assert sum(markers(intermediate/'focused/birth-native-checks.log').values())==39
 assert sum(markers(intermediate/'retained/birth-native-checks.log').values())==347
 before=e.load(STAGE/'source-snapshot-2.json')['inputs'];after=e.load(STAGE/'source-snapshot-3.json')['inputs']
 assert before['copiedData']==after['copiedData']and before['projectInputs']==after['projectInputs']
 assert {p for p,h in before['sourceInputs'].items()if after['sourceInputs'][p]!=h}=={'src/Sanctuary.Content/AmazonValkyrieBody.cs','tests/Sanctuary.Integration/ValkyrieNumericChecks.cs'}
 failed=STAGE/'failed-1';assert 'Inner Sight flat loss used the wrong side of the item/passive percentage'in(failed/'birth-native-checks.log').read_text()
 assert e.sha(failed/'Sanctuary.Content.dll')==e.sha(intermediate/'Sanctuary.Content.dll')and e.sha(failed/'Sanctuary.Integration.dll')==e.sha(intermediate/'Sanctuary.Integration.dll')
 proofpath=ROOT/'tools/reference/valkyrie-numeric-source-evidence.json';proof=e.load(proofpath)
 assert proof['contexts']==57 and proof['originalItems']==330 and len(proof['completeRoutineHashes'])==7 and not proof['currentExecutableParity']
 for group in ['inputs','artifacts']:
  for p,h in proof[group].items():assert e.sha(ROOT/p)==h,p
 assert not(SOURCE/'sanitizer.log').read_text()
 cases=e.load(SOURCE/'cases.json');assert len(cases)==57
 assert {p>>16 for c in cases for p,v in c['Stats']if v}>={83,188,201,204}
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in text and '\nOK\n'in text
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 valk=next(s for s in status['skills']if s['talentId']=='amazon.valkyrie');assert valk['implementation']=='blocked'and not valk['complete']
 files=e.stage_files(STAGE,WIDE)+[proofpath,ROOT/'tools/probe_valkyrie_numeric.py',ROOT/'tests/check_valkyrie_numeric.py',ROOT/'tools/build_skill_roadmap.py',Path(__file__).resolve()]
 files.extend(p for folder in [failed,intermediate,SOURCE]for p in folder.rglob('*')if p.is_file())
 files += [ROOT/'docs'/p for p in ['valkyrie-numeric-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,
  currentExecutableParity=False,liveNumericRecipients=['attack rating','defense','physical/magic/fire/lightning/cold/poison resistance'],liveItemResources=False,
  sourceSnapshot=3,contentBuild=3,integrationBuild=2,focusedRun=4,retainedRun=2,auditRun=2,focusedMarkers=39,retainedMarkers=347,
  bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,sourceLoadouts=57,sourceDefenseContexts=171,originalItems=330,
  pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Optional existing-body JSON profile replays all original items/layers in source attachment order;old bare/loadout JSON and generic resource-only guards retain. Source-name birth bases and existing numeric readers supply five-Dexterity NPC accuracy,flat defense/global percent and six resistance recipients.',
   '57controlled birth rank/difficulty loadouts match independent complete older source graph/defense contexts;330original items including class/tree/charge/proc layers remain unfiltered. Native Shout,Inner Sight and typed mitigation readers,missing-original defense/resistance refusal,no resource change and two original reloads pass.',
   'All344parent cross-class markers retain;fresh573item decisions and installed build/saves/client remain unchanged. First native failure was oracle base-vs-total armor double counting;unchanged intermediate DLLs pass39/347 before the separately frozen native-cache validation guard.'],
  limits=['Complete older stat/defense routines with explicit services are not current executable parity. Accuracy branch is inspected only;no actual attack-roll/controller/contact certificate. No Holy Shield nested service or time-dependent roll without explicit source clock.',
   'Resource,weapon/rate,reduction/absorb/leech,skill/charge/proc,inherited passive,nonzero sunder,initializer/world RNG,NecroPet and paid cast/placement/replacement/local-delay/input remain unfinished. Numeric field presence does not grant execution. No native UO pool/rate bases inferred.',
   'Full difficulty-transition,owner/gear/PvP/multiplayer/client and final model/effects/audio/talent/hotkey assets remain separate;no class complete or deployment.'])
 e.write('valkyrie-numeric-native-evidence.json',result,files,check)
 print('Valkyrie numeric:39focused/347retained,330original items,two reloads;71Python,573unchangeditem decisions;202handlers,resources/weapon/skills/procs/AI/cast still gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
