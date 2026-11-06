#!/usr/bin/env python3
"""Bind complete NPC spell numeric source controls to original saved actors."""
import argparse,json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-item-spell-damage';WIDE=BASE/'npc-item-spell-damage-retained';SOURCE=BASE/'npc-item-spell-damage-source'
PARENT='ad9ec9a1c1023616859ae32217fe65b92dd3691b'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-1.json');prior=e.load(ROOT/'tools/reference/valkyrie-contact-native-evidence.json');extra=Counter({'npc-item-spell-damage-check':1,'npc-item-spell-damage-reload-check':2})
 for previous,stage,count in [(BASE/'valkyrie-contact',STAGE,66),(BASE/'valkyrie-contact-retained',WIDE,374)]:
  p=previous/'birth-native-checks.log';assert e.sha(p)==prior['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
  assert 'pass across3boots/2reloads'in(stage/'native-run-2.log').read_text()
  log=(stage/'birth-native-checks.log').read_text();assert log.count('NPC item spell damage:2558 complete source controls at two actor levels;57original bodies/330items/38proc layers,own damage/pierce/rank,unchanged original resources/wounds/RNG across reloads;delivery/callbacks/AI/learned cast remain gated')==3
  saved=e.load(stage/'valkyrie-contact-fixtures.json');assert saved['Generation']==2 and len(saved['Bodies'])==57 and sum(len(h['Items'])for h in saved['Bodies'])==330
  failed=stage/'failed-driver-1';assert 'FileNotFoundError'in(failed/'native-run-1.log').read_text()and'npc-item-spell-damage-fixtures.json'in(failed/'native-run-1.log').read_text()
  assert markers(failed/'birth-native-checks.log')['npc-item-spell-damage-check']==1
  assert e.load(failed/'birth-inputs.json')['artifacts']==born['artifacts']
 for name in ['content-build-1.log','integration-build-1.log']:
  text=(STAGE/name).read_text();assert all(n in text for n in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 proofpath=ROOT/'tools/reference/npc-item-spell-damage-source-evidence.json';proof=e.load(proofpath);assert(proof['cases'],proof['numericMatrix'],proof['originalBodies'],proof['originalProcLayers'])==(2558,2520,57,38)and len(proof['routineHashes'])==16
 for group in ['inputs','currentTable','artifacts']:
  for p,h in proof[group].items():assert e.sha(ROOT/p)==h,p
 assert not(SOURCE/'sanitizer.log').read_bytes();cases=e.load(SOURCE/'cases.json');assert len(cases['Cases'])==2558 and len(cases['Originals'])==38
 assert {c['Skill']for c in cases['Cases']}=={36,38,44,48,53}and {c['Rank']for c in cases['Cases']}==set(range(1,64))
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in text and'\nOK\n'in text
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 assert next(s for s in status['skills']if s['talentId']=='amazon.valkyrie')['implementation']=='blocked'
 files=e.stage_files(STAGE,WIDE)+[proofpath,ROOT/'tools/probe_npc_item_spell_damage.py',ROOT/'tests/check_npc_item_spell_damage.py',Path(__file__).resolve()]
 files+=[ROOT/p for p in proof['artifacts']]
 for stage in [STAGE,WIDE]:files.extend(p for p in(stage/'failed-driver-1').rglob('*')if p.is_file())
 files+=[ROOT/'docs'/n for n in ['npc-item-spell-damage-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,procDelivery=False,currentExecutableParity=False,
  sourceSnapshot=1,contentBuild=1,integrationBuild=1,focusedRun=2,retainedRun=2,auditRun=1,focusedMarkers=66,retainedMarkers=374,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,sourceCases=2558,sourceRoutines=16,originalBodies=57,originalItems=330,originalProcLayers=38,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Actual own-NPC numeric supplier retains explicit proc rank,elemental endpoints/cold duration,count and original own mastery/pierce. Source-zero complete creator/getter/writer proof excludes unrelated weapon/elemental/leech/critical payloads. Existing SkillValues and endpoint arithmetic are shared.','Native controls compare2558source rows at actor levels1/99 and38actual original proc layers across57saved bodies/330unchanged items. Two real reloads retain original capture inputs,wounds,resources and RNG;JSON packet roundtrip is a codec check,not a new saved projectile or pending proc claim. All371parent retained markers remain.','The first driver expected a nonexistent separately-owned numeric manifest after passing its checks. The corrected mapping reuses original contact fixture ownership;Content1/Integration1 remain identical in failed/accepted runs. Failure logs and initial driver are retained.'],
  limits=['Numeric capture only:proc callbacks/emission/missile motion/hit delay/mitigation/HP/source-removal/RNG allocation and full original subscription chronology remain unfinished. Chain count is a hit budget,not eager segment allocation.','Explicit zero hard-synergy context belongs to the original Valkyrie with four captured passive skills. Unmaterialized skill-granting items refuse. Other NPC spell lists and modern executable behavior are not certified.','No AI attack gate,learned cast,class certificate,deployment or final asset is enabled. Existing initializer/world RNG,external elemental lists,nonzero sunder,placement/payment/caps/replacement/input/client and broader skill gaps remain.'])
 e.write('npc-item-spell-damage-native-evidence.json',result,files,check)
 print('NPC item spell damage:66focused/374retained markers,two reloads;2558source packets/38original layers;202handlers,delivery/AI/cast remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
