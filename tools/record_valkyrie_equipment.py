#!/usr/bin/env python3
"""Bind actual private Valkyrie equipment without claiming live skill admission."""
import argparse,json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-loadout';WIDE=BASE/'valkyrie-loadout-retained';SOURCE=BASE/'npc-item-allocation-source'
PARENT='410036042934f33e0d9484ec16bbed3e1b211cdf'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-2.json')
 a=e.load(STAGE/'source-snapshot-1.json')['inputs'];b=e.load(STAGE/'source-snapshot-2.json')['inputs']
 for key in ['copiedData','projectInputs']:assert a[key]==b[key]
 assert {p for p,h in a['sourceInputs'].items()if b['sourceInputs'][p]!=h}=={'tests/Sanctuary.Integration/ValkyrieEquipmentChecks.cs'}
 extra=Counter()
 for n in ['npc-item-allocation','valkyrie-equipment','generated-npc-item']:extra.update({n+'-check':1,n+'-reload-check':2})
 prior=BASE/'valkyrie-body/birth-native-checks.log';old=e.load(ROOT/'tools/reference/valkyrie-body-native-evidence.json')
 assert e.sha(prior)==old['files'][str(prior.relative_to(ROOT))];focus=markers(prior);assert sum(focus.values())==27;e.cohort(STAGE,focus+extra,36)
 prior=BASE/'valkyrie-body-retained/birth-native-checks.log';assert e.sha(prior)==old['files'][str(prior.relative_to(ROOT))]
 retained=markers(prior);assert sum(retained.values())==335;e.cohort(WIDE,retained+extra,344)
 for stage,run in [(STAGE,2),(WIDE,1)]:
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
  log=(stage/'birth-native-checks.log').read_text();assert "denied by connection filter 'auto-denylist'"not in log
  assert log.count('NPC item allocation:960full older allocator contexts/2160successful source items')==3
  assert log.count('Generated NPC items:2580original native serialized items')==3
  state=e.load(stage/'valkyrie-equipment-fixtures.json');rows=state['Bodies'];assert state['Generation']==2 and len(rows)==57
  assert sum(len(s['Items'])for s in rows)==330 and len({i for s in rows for i in s['Items']})==330
  ranks=set();difficulties=set()
  for saved in rows:
   body=json.loads(saved['Text']);s=body['Equipment'];ranks.add(s['Rank']);difficulties.add(s['Difficulty'])
   assert s['Actor']==saved['Body']and s['Owner']==state['Owner']and s['ItemLevel']==25+3*(s['Rank']-1)
   assert [a['Item']for a in s['Attempts']if a['Success']]==saved['Items']and len(saved['Items'])==len(saved['Payloads'])
   assert body['Birth']['Rank']==s['Rank']and saved['Life']%256==255
   for serial,text in zip(saved['Items'],saved['Payloads']):
    item=json.loads(text);assert item['Version']==1 and item['Metadata']['Flags']==0x82010 and item['Equipment']==dict(Actor=s['Actor'],Owner=s['Owner'],Body=item['Equipment']['Body'],Kind=0,Flags=0x1000)
    assert item['Request']['Level']==s['ItemLevel']and item['Request']['Difficulty']==s['Difficulty']and item['Allocation']is not None
  assert len(ranks)==19 and difficulties=={0,1,2}
  assert e.load(stage/'npc-item-allocation-fixtures.json')['Generation']==2
 for name in ['content-build-1.log','integration-build-2.log']:
  text=(STAGE/name).read_text();assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 failed=STAGE/'failed-1';log=(failed/'birth-native-checks.log').read_text()
 assert 'ValkyrieEquipmentChecks+FaultBody'in log and 'does not have a serialization constructor'in log
 assert 'valkyrie-equipment-check: passed'in log and 'generated-npc-item-check: passed'in log
 assert e.sha(failed/'Sanctuary.Content.dll')==born['artifacts']['Assemblies/Sanctuary.Content.dll']
 prior_code=(failed/'tests/Sanctuary.Integration/ValkyrieEquipmentChecks.cs').read_text()
 fixed=prior_code.replace('        private int Added;','''        private int Added;
        public FaultBody(){}
        // Deleted fixture types still appear in the native type index.
        public FaultBody(Serial serial):base(serial){}''')
 assert fixed==(ROOT/'tests/Sanctuary.Integration/ValkyrieEquipmentChecks.cs').read_text()
 proofpath=ROOT/'tools/reference/npc-item-allocation-source-evidence.json';proof=e.load(proofpath)
 assert proof['contexts']==960 and len(proof['completeRoutineHashes'])==10 and not proof['currentExecutableParity']
 for group in ['inputs','artifacts']:
  for p,h in proof[group].items():assert e.sha(ROOT/p)==h,p
 assert not(SOURCE/'sanitizers.log').read_text()and len(e.load(SOURCE/'cases.json'))==960
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 log=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in log and '\nOK\n'in log
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 valk=next(s for s in status['skills']if s['talentId']=='amazon.valkyrie');assert valk['implementation']=='blocked'and not valk['complete']
 files=e.stage_files(STAGE,WIDE)+[proofpath,ROOT/'tools/probe_npc_item_allocation.py',ROOT/'tests/check_valkyrie_equipment.py',ROOT/'tools/build_skill_roadmap.py',Path(__file__).resolve()]
 files.extend(p for folder in [failed,SOURCE]for p in folder.rglob('*')if p.is_file())
 files += [ROOT/'docs'/p for p in ['valkyrie-equipment-checkpoint.md','valkyrie-body-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,
  currentExecutableParity=False,suppliedStreamMaterialization=True,liveItemStats=False,sourceSnapshot=2,contentBuild=1,integrationBuild=2,
  focusedRun=2,retainedRun=1,auditRun=1,focusedMarkers=36,retainedMarkers=344,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,
  sourceContexts=960,sourceAllocations=2160,sourceAllocationRefusals=2160,loadouts=57,rankBoundaries=19,originalEquippedItems=330,newFactoryItems=6,legacyItems=2580,
  pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Existing MonEquip selector,current quality/property generator and physical NPC slot delivery now attach actual private items to the original Valkyrie body. Requests,success/order/slots,original serials,graphs and supplied stream continuations persist through two reloads without rerolling.',
   'Ten complete older allocation/seed routines and independent arithmetic establish two supplied game draws and separate unit/property high666seeds with explicit world services. Native generation carries inspectedINIT/INSTORE/IDENTIFIED plus separateNOSELL/durability context. Old callers retain old flags/layout.',
   '57controlled rank/difficulty loadouts retain330actual items;6new allocated items compare to legacy quality/graph/seed results.2580legacy originals,old bare-body JSON,alias/duplicate/corrupt/foreign-inventory refusal and late native failure cleanup/no RNG rewind pass.',
   'All335parent cross-class markers retain;573named decisions and installed build/saves/client remain unchanged. First reload failure was the deleted fault fixture type missing its native serialization constructor;production binary is unchanged.'],
  limits=['No actual world RNG owner,modern allocator/outer-factory/world callback or interleaving/initializer/component/player-count provenance claim. Named synthetic unit flags and declared world services are not binary ABI proof.',
   'Private storage/raw graphs do not yet deliver live item stat/resource/time/skill/proc bonuses,inherited passive skills,nonzero sunder,NecroPet combat or learned cast/placement/replacement/local delay/input. No source roll filtering or generic stat fallback.',
   'Full owner/gear/PvP/multiplayer/client and final equipment/model/effects/audio/talent/hotkey assets remain unfinished;identified hidden-item/female silhouettes persist. No class certificate or deployment.'])
 e.write('valkyrie-equipment-native-evidence.json',result,files,check)
 print('Valkyrie actual loadouts:36focused/344retained,330original items/57contexts,two reloads;960older allocator cases,71Python;202handlers,learned cast and live stats still gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
