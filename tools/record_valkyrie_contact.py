#!/usr/bin/env python3
"""Bind original NPC full-fill/ordered contact,explicit refusals and saves."""
import argparse,json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-contact';WIDE=BASE/'valkyrie-contact-retained';ISOLATE=BASE/'valkyrie-contact-isolate';SOURCE=BASE/'valkyrie-contact-source'
PARENT='b427e31a38739ea991500f9bda79f9e21c760a73'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-4.json');old=e.load(ROOT/'tools/reference/valkyrie-motion-native-evidence.json');extra=Counter({'valkyrie-contact-check':1,'valkyrie-contact-reload-check':2})
 for previous,stage,count,run in [(BASE/'valkyrie-motion',STAGE,63,3),(BASE/'valkyrie-motion-retained',WIDE,371,1)]:
  path=previous/'birth-native-checks.log';assert e.sha(path)==old['files'][str(path.relative_to(ROOT))];e.cohort(stage,markers(path)+extra,count)
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
 wanted=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})+extra
 e.cohort(ISOLATE,wanted,12);snapshot(STAGE/'source-snapshot-4.json',ISOLATE/'birth-inputs.json');isolated=e.load(ISOLATE/'birth-inputs.json');assert isolated['artifacts']==born['artifacts']and isolated['copiedData']==born['copiedData']
 assert 'pass across3boots/2reloads'in(ISOLATE/'native-run-2.log').read_text()
 for stage in [STAGE,WIDE,ISOLATE]:
  assert(stage/'birth-native-checks.log').read_text().count('Valkyrie contact:57original loadouts/330items;30full-fill ordered HP/event consumers and27atomic proc refusals;original pending packets/items/resources/wounds/RNG through two reloads;AI/cast remain gated')==3
  saved=e.load(stage/'valkyrie-contact-fixtures.json');assert saved['Generation']==2 and len(saved['Bodies'])==57 and sum(h['Supported']for h in saved['Bodies'])==30 and sum(len(h['Items'])for h in saved['Bodies'])==330
  assert len({h['Target']for h in saved['Bodies']if h['Supported']})==30
  for h in saved['Bodies']:
   body=json.loads(h['Text']);rng=h['Random'];resources=json.loads(h['Resources']);assert body['Owner']==saved['Owner']==rng['Owner']==resources['Owner']and rng['Actor']==resources['Actor']==h['Body']and h['Life']>0
   assert body['EquipmentWeapons']['Identity']==rng['ValkyrieBinding']and resources['Valkyrie']['RegenerationBase']==300
   if h['Supported']:
    assert body['EquipmentContactProfile']=='valkyrie-original-equipment-contact-v1'and h['TargetLife']>0
    assert h['Pending']['Impact']['Actor']==h['Body']and h['Pending']['Impact']['Target']==h['Target']and h['Pending']['Impact']['Packet']['ResultFlags']&1
   else:assert 'EquipmentContactProfile'not in body and h['Pending']is None and h['Target']==0
   for text in h['Items']:
    item=json.loads(text);assert item['Equipment']['Actor']==h['Body']and item['Equipment']['Owner']==saved['Owner']
 for name in ['content-build-1.log','integration-build-4.log']:
  text=(STAGE/name).read_text();assert all(n in text for n in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 failed=STAGE/'failed-native-2';assert 'valkyrie-contact-check: passed'in(failed/'birth-native-checks.log').read_text()and 'Restored original packet lost ordered HP release'in(failed/'birth-native-checks.log').read_text()
 assert e.sha(failed/'content/Sanctuary.Content.dll')==born['artifacts']['Assemblies/Sanctuary.Content.dll']
 diagnostic=ISOLATE/'failed-native-1';assert 'maps=Internal/Trammel/Trammel,hostile=True'in(diagnostic/'birth-native-checks.log').read_text()
 before=e.load(failed/'source-snapshot-2.json')['inputs'];after=e.load(STAGE/'source-snapshot-4.json')['inputs'];assert before['copiedData']==after['copiedData']and before['projectInputs']==after['projectInputs']
 assert {p for p,h in before['sourceInputs'].items()if after['sourceInputs'][p]!=h}=={'tests/Sanctuary.Integration/ValkyrieContactChecks.cs'}
 proofpath=ROOT/'tools/reference/valkyrie-contact-cases-evidence.json';proof=e.load(proofpath);assert(proof['cases'],proof['supported'],proof['unsupported'],len(proof['propertyStats']))==(57,30,27,33)
 for p,h in proof['inputs'].items():assert e.sha(ROOT/p)==h,p
 fixture=ROOT/proof['fixture']['path'];assert e.sha(fixture)==proof['fixture']['sha256'];cases=e.load(fixture);assert len(cases)==57 and sum(c['Supported']for c in cases)==30
 assert all(bool(c['Unsupported'])!=c['Supported']for c in cases)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and'\nOK\n'in(STAGE/'python-tests.log').read_text()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 assert next(s for s in status['skills']if s['talentId']=='amazon.valkyrie')['implementation']=='blocked'
 files=e.stage_files(STAGE,WIDE,ISOLATE)+[proofpath,ROOT/'tools/build_valkyrie_contact_cases.py',ROOT/'tests/check_valkyrie_contact.py',Path(__file__).resolve()]
 for stage,subs in [(STAGE,['failed-build-1','failed-native-2']),(ISOLATE,['failed-native-1']),(SOURCE,['failed-builder-1'])]:
  for sub in subs:files.extend(p for p in(stage/sub).rglob('*')if p.is_file())
 files.append(fixture)
 files += [ROOT/'docs'/n for n in ['valkyrie-contact-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,currentExecutableParity=False,
  sourceSnapshot=4,contentBuild=1,integrationBuild=4,focusedRun=3,retainedRun=1,isolatedRun=2,auditRun=1,focusedMarkers=63,retainedMarkers=371,isolatedMarkers=12,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,
  originalBodies=57,originalItems=330,qualifiedContactLoadouts=30,atomicProcRefusals=27,originalPendingPacketsPerSave=30,currentPropertyIdentities=33,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Optional saved contact admission binds actual original stat graph to existing numeric/event/duration/absorb/pierce consumers without double magic resistance. Independent current property stat1/source-reader controls retain57loadouts/330items;30compatible loadouts reach full NPC fill and ordered HP/status/event/return;27proc loadouts refuse unchanged before hit/damage RNG.','Actual HP/notification-before-HP/free,stored mitigation after resistance changes,event RNG,miss/range/replay controls pass. Thirty original pending packets/targets,body resources/wounds/profile/items/RNG survive two actual reloads. All368parent broad markers remain;isolated sameContent1 acceptance confirms the fixture correction.','Shared ground storage publishes its cache after the body callback;actual cached mode is validated. The fixture rejects forbidden attack-mode writes without changing saved/cache ownership. No world AI attack or learned cast is enabled.'],
  limits=['Allocation/release phases remain explicit caller services,not current A1 startup/controller/recovery proof. Proc spell delivery,external elemental stat lists,typed monster damage,nonzero inherited sunder,current initializer/world RNG and learned paid placement/replacement/input remain unfinished.','Charged layers remain stored;this melee path does not auto-use charges or certify charge execution. Full original callback registration chronology,all eligible property combinations,client/terrain/PvP and final assets remain separate;no class completion or deployment.','The syntax/alias and pre-server missing-DLL failures are archived. Full native2 and diagnostic isolate1 correctly refused a packet whose caster had no landing space and remained Internal. The fixture now places sources legally one at a time;productionContent1 was unchanged.'])
 e.write('valkyrie-contact-native-evidence.json',result,files,check)
 print('Valkyrie original contact:63focused/371retained/12isolated markers,two reloads;57originals330items,30ordered contacts/27intact proc refusals;202handlers,AI/cast gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
