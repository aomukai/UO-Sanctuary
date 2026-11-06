#!/usr/bin/env python3
"""Bind original hand identity and explicit stream evidence to tested binaries."""
import argparse,json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-weapons';WIDE=BASE/'valkyrie-weapons-retained';SOURCE=BASE/'inventory-weapon-identity-source'
PARENT='bc655d53d4772915ceceab43351b113fb72edcf9'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-3.json');old=e.load(ROOT/'tools/reference/valkyrie-resources-native-evidence.json');extra=Counter({'valkyrie-weapons-check':1,'valkyrie-weapons-reload-check':2})
 for previous,stage,count in [(BASE/'valkyrie-resources',STAGE,48),(BASE/'valkyrie-resources-retained',WIDE,356)]:
  path=previous/'birth-native-checks.log';assert e.sha(path)==old['files'][str(path.relative_to(ROOT))];e.cohort(stage,markers(path)+extra,count)
  assert 'pass across3boots/2reloads'in(stage/'native-run-2.log').read_text()
  log=(stage/'birth-native-checks.log').read_text();assert log.count('Valkyrie weapons:8192source contexts/24576stages;57unfiltered original loadouts/330items')==3
  saved=e.load(stage/'valkyrie-weapons-fixtures.json');assert saved['Generation']==2 and len(saved['Bodies'])==57 and sum(len(h['Items'])for h in saved['Bodies'])==330
  assert len({i for h in saved['Bodies']for i in h['Items']})==330
  for h in saved['Bodies']:
   body=json.loads(h['Text']);binding=body['EquipmentWeapons'];rng=h['Random'];assert body['Owner']==saved['Owner']==rng['Owner']and rng['Actor']==h['Body']
   assert binding['Primary']in h['Items']and binding['Secondary']==0 and rng['Item']==binding['Primary']and rng['ValkyrieBinding']==binding['Identity']
   assert body['EquipmentStatProfile']=='valkyrie-equipment-numeric-v1'and body['Difficulty']==0 and h['Life']>0
   assert set(rng)=={'Actor','Owner','Item','Seed','ValkyrieBinding'}and len(binding['Identity'])==64
   for serial,payload in zip(h['Items'],h['Payloads']):
    item=json.loads(payload);assert item['Equipment']['Actor']==h['Body']and item['Equipment']['Owner']==saved['Owner']and item['Metadata']['Flags']==0x82010
 for name in ['content-build-2.log','integration-build-2.log']:
  text=(STAGE/name).read_text();assert all(n in text for n in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 before=e.load(STAGE/'source-snapshot-2.json')['inputs'];after=e.load(STAGE/'source-snapshot-3.json')['inputs']
 assert before['copiedData']==after['copiedData']and before['projectInputs']==after['projectInputs']
 assert {p for p,h in before['sourceInputs'].items()if after['sourceInputs'][p]!=h}=={'tests/Sanctuary.Integration/ValkyrieWeaponChecks.cs'}
 failed=[STAGE/'failed-build-1',STAGE/'failed-1',WIDE/'failed-1']
 for folder in failed:
  for p,h in e.load(folder/'inventory.json').items():assert e.sha(folder/p)==h,p
 assert 'error CS0136'in(STAGE/'failed-build-1/.local/class-skills/valkyrie-weapons/content-build-1.log').read_text()
 assert 'Equip Tarnhelm'in(STAGE/'failed-1/birth-native-checks.log').read_text()and'valkyrie-weapons-check: passed'in(STAGE/'failed-1/birth-native-checks.log').read_text()
 assert 'KeyboardInterrupt'in(WIDE/'failed-1/native-run-1.log').read_text()
 assert e.sha(STAGE/'failed-1/Sanctuary.Content.dll')==born['artifacts']['Assemblies/Sanctuary.Content.dll']
 proofpath=ROOT/'tools/reference/inventory-weapon-identity-source-evidence.json';proof=e.load(proofpath)
 assert proof['contexts']==8192 and len(proof['completeRoutineHashes'])==6 and len(proof['inlineServiceHashes'])==2 and not proof['currentExecutableParity']
 for group in ['inputs','artifacts']:
  for p,h in proof[group].items():assert e.sha(ROOT/p)==h,p
 assert not(SOURCE/'sanitizer.log').read_text();assert len(e.load(SOURCE/'cases.json'))==8192
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and'\nOK\n'in(STAGE/'python-tests.log').read_text()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 valk=next(s for s in status['skills']if s['talentId']=='amazon.valkyrie');assert valk['implementation']=='blocked'and not valk['complete']
 files=e.stage_files(STAGE,WIDE)+[proofpath,ROOT/'tools/probe_inventory_weapon_identity.py',ROOT/'tests/check_valkyrie_weapons.py',Path(__file__).resolve()]
 files.extend(p for folder in [*failed,SOURCE]for p in folder.rglob('*')if p.is_file())
 files += [ROOT/'docs'/n for n in ['valkyrie-weapons-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,currentExecutableParity=False,
  sourceSnapshot=3,contentBuild=2,integrationBuild=2,focusedRun=2,retainedRun=2,auditRun=2,focusedMarkers=48,retainedMarkers=356,
  bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,sourceContexts=8192,sourceStages=24576,completeSourceRoutines=6,originalLoadouts=57,originalItems=330,drawsPerActorPerBoot=64,pythonTests=71,verificationCommands=7,
  auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Saved optional original primary/secondary identity is derived from original successful source placements and complete older GUID routines;raw item graphs and captured birth/recipe/stream witnesses are retained without filtering.',
   'Shared persisted NPC RNG gains a mutually exclusive original-Valkyrie binding with mandatory supplied seed. Missing/stale bindings refuse without automatic reseeding. Three real boots/two reloads retain original item/payload/wound/seed identity and continuation;owner gear,actual owner facets,parking,atomic invalid restore/activation and detached/removed hand refusal pass.',
   'All353parent retained markers remain;old bare/numeric/resource body and legacy RNG JSON remain compatible. First compile failure and native fixture failure/stopped broad cohort are preserved;accepted production DLL unchanged by the fixture correction.'],
  limits=['Supplementary complete older routines have explicit grid/type/body services,not current executable parity. Starting attack seed is a supplied context;world/AI allocation and draw interleaving provenance remains unresolved.',
   'Weapon damage allocation/delivery,inherited skill execution,original item procs/charges,nonzero Sunder,rate/AI/contact,current initializer and paid learned casting/placement/replacement/input remain unfinished. Full gameplay/client/multiplayer and final body/equipment,effects/audio/talent/hotkey artwork remain separate;no class complete or deployment.'])
 e.write('valkyrie-weapons-native-evidence.json',result,files,check)
 print('Valkyrie hands/RNG:48focused/356retained,two reloads,8192complete-source contexts,57original loadouts/330items;71Python,573unchanged item decisions;202handlers,contact/cast still gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
