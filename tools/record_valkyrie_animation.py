#!/usr/bin/env python3
"""Bind original transformed timing preparation to source/native/save evidence."""
import argparse,json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-animation';WIDE=BASE/'valkyrie-animation-retained';SOURCE=BASE/'valkyrie-animation-source'
PARENT='1e432283b501066aeb1bfd5fdcf39bbb6bf44ba1'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-1.json');old=e.load(ROOT/'tools/reference/valkyrie-defense-native-evidence.json');extra=Counter({'valkyrie-animation-check':1,'valkyrie-animation-reload-check':2})
 for previous,stage,count in [(BASE/'valkyrie-defense',STAGE,57),(BASE/'valkyrie-defense-retained',WIDE,365)]:
  path=previous/'birth-native-checks.log';assert e.sha(path)==old['files'][str(path.relative_to(ROOT))];e.cohort(stage,markers(path)+extra,count)
  assert 'pass across3boots/2reloads'in(stage/'native-run-1.log').read_text()
  log=(stage/'birth-native-checks.log').read_text();assert log.count('Valkyrie animation:1368complete source contexts/2736epoch comparisons per boot;57original loadouts;Amazon2HT frame bonus2;read-only preparation only')==3
  saved=e.load(stage/'valkyrie-animation-fixtures.json');assert saved['Generation']==2 and len(saved['Bodies'])==57 and sum(len(h['Items'])for h in saved['Bodies'])==330
  serials=[]
  for h in saved['Bodies']:
   body=json.loads(h['Text']);rng=h['Random'];assert body['Owner']==saved['Owner']==rng['Owner']and rng['Actor']==h['Body']and h['Life']>0
   assert body['EquipmentWeapons']['Identity']==rng['ValkyrieBinding']and 'Controller'not in body
   for text in h['Items']:
    item=json.loads(text);assert item['Equipment']['Actor']==h['Body']and item['Equipment']['Owner']==saved['Owner'];serials.append(item['Equipment']['Body'])
  assert len(serials)==330
 for name in ['content-build-1.log','integration-build-1.log']:
  text=(STAGE/name).read_text();assert all(n in text for n in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 proofpath=ROOT/'tools/reference/valkyrie-animation-source-evidence.json';proof=e.load(proofpath);assert proof['contexts']==1368 and proof['originalLoadouts']==57 and len(proof['completeRoutineHashes'])==9 and not proof['currentExecutableParity']
 for group in ['inputs','artifacts']:
  for p,h in proof[group].items():assert e.sha(ROOT/p)==h,p
 assert not(SOURCE/'sanitizer.log').read_text();cases=e.load(SOURCE/'cases.json');assert len(cases)==1368 and {c['Mode']for c in cases}=={2,4,15}
 assert all(c['Expected'][3]==(2 if c['Mode']==4 else 0)for c in cases)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and'\nOK\n'in(STAGE/'python-tests.log').read_text()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 assert next(s for s in status['skills']if s['talentId']=='amazon.valkyrie')['implementation']=='blocked'
 files=e.stage_files(STAGE,WIDE)+[proofpath,ROOT/'tools/probe_valkyrie_animation.py',ROOT/'tests/check_valkyrie_animation.py',Path(__file__).resolve()]
 files.extend(p for p in SOURCE.rglob('*')if p.is_file())
 files += [ROOT/'docs'/n for n in ['valkyrie-animation-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,currentExecutableParity=False,
  sourceSnapshot=1,contentBuild=1,integrationBuild=1,focusedRun=1,retainedRun=1,auditRun=1,focusedMarkers=57,retainedMarkers=365,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,
  sourceContexts=1368,completeSourceRoutines=9,originalBodies=57,originalItems=330,epochComparisonsPerBoot=2736,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Current state93/Amazon graphics and original2HTspears select AMA12HT18frames/marker11/speed256. Nine complete older routines establish frame bonus2,converted modes,player movement bases and full initial action/end plans with explicit rate bases.','Prepared read-only original graph consumers pass2736epoch comparisons per boot and retain original330items/stats/wounds/attack RNG through parking/two actual reloads. All362parent broad markers retain;no native contact or gameplay route is activated.'],
  limits=['Current executable parity,initializer rate bases,live external/run-state stat-list ownership and world timing are unverified. No zero-bonus bareVK/genericpet timing substitute is admitted.','World controller/moving Evade,original item skill/event delivery,external offensive composition,nonzero inherited sunder,initializer/world RNG and learned paid placement/replacement/input remain unfinished. Final effects/audio/talent/hotkey assets stay separate;no class completion or deployment.'])
 e.write('valkyrie-animation-native-evidence.json',result,files,check)
 print('Valkyrie transformed timing:57focused/365retained markers,two reloads;1368source contexts/2736epoch comparisons per boot;57originals330items;202handlers,world/cast gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
