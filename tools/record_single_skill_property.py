#!/usr/bin/env python3
"""Bind original individual-skill layers to source and native item preservation."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'single-skill-property';WIDE=BASE/'single-skill-property-retained';IRON=BASE/'single-skill-property-iron';PROBE=BASE/'single-skill-property-source'
PARENT='f9f82390bdd8b3e2a675bb69d7e201afb760af81'
def record(check=False):
 snapshot=STAGE/'source-snapshot-3.json';born=e.inventory(STAGE,WIDE,snapshot)
 old=e.load(BASE/'saved-item-ethereality/source-snapshot-1.json')['inputs'];now=e.load(snapshot)['inputs']
 assert now['sourceInputs'].keys()-old['sourceInputs'].keys()=={'src/Sanctuary.Content/ItemSingleSkillProperty.cs','tests/Sanctuary.Integration/SingleSkillPropertyChecks.cs'}
 assert old['projectInputs']==now['projectInputs'] and old['copiedData'].keys()==now['copiedData'].keys()
 assert {p for p,h in old['copiedData'].items()if now['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 assert {p for p,h in old['sourceInputs'].items()if p.startswith(('src/','shared/'))and h!=now['sourceInputs'][p]}=={'src/Sanctuary.Content/EquipmentPropertyWriter.cs'}
 initial=e.load(STAGE/'failed-1/source-snapshot-1.json')['inputs']['sourceInputs']
 assert all(initial[p]==h for p,h in now['sourceInputs'].items()if p.startswith(('src/','shared/')))
 assert e.sha(STAGE/'content/Sanctuary.Content.dll')==e.sha(STAGE/'failed-1/content/Sanctuary.Content.dll')
 base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
 delta=Counter({'single-skill-property-check':1,'single-skill-property-reload-check':2});e.cohort(STAGE,base+delta,12)
 prior=BASE/'saved-item-ethereality-retained/birth-native-checks.log';receipt=e.load(ROOT/'tools/reference/saved-item-ethereality-native-evidence.json');assert e.sha(prior)==receipt['files'][str(prior.relative_to(ROOT))]
 retained=markers(prior);assert sum(retained.values())==182;e.cohort(WIDE,retained+delta,185)
 e.cohort(IRON,base+Counter({'iron-golem-cast-check':1,'iron-golem-cast-reload-check':2}),12)
 for stage in [STAGE,WIDE,IRON]:
  inputs=e.load(stage/'birth-inputs.json');assert inputs['artifacts']==born['artifacts'] and inputs['copiedData']==born['copiedData'];e.snapshot(snapshot,stage/'birth-inputs.json')
  for p,h in inputs['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  assert 'pass across3boots/2reloads' in(stage/'native-run-2.log').read_text()
  if stage!=IRON:
   saved=e.load(stage/'single-skill-property-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==2
   assert len({x['Actor']for x in saved['Actors']})==2 and sum(x['Parent']!=x['Actor']for x in saved['Actors'])==1
   assert (stage/'birth-native-checks.log').read_text().count('Single skill property:13824complete source controls')==3
 for name in ['content-build-1.log','integration-build-2.log']:
  text=(STAGE/name).read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 protected['acceptedClientPatches']=41
 source_path=ROOT/'tools/reference/single-skill-property-source-evidence.json';source=e.load(source_path);assert source['contexts']==13824 and not source['currentExecutableParity'] and len(source['completeRoutineHashes'])==1
 for group in ['inputs','artifacts']:
  for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
 assert source['completeRoutineHashes']==e.load(PROBE/'failed-1/reference.json')['completeRoutineHashes']
 before=e.load(PROBE/'failed-1/cases.json')['cases'];after=e.load(PROBE/'cases.json')['cases']
 assert len(before)==len(after)==13824 and all(a[:1]+a[2:]==b[:1]+b[2:]for a,b in zip(before,after))
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==8 and all(x['exitCode']==0 for x in commands)
 assert 'Ran 71 tests' in(STAGE/'python-tests.log').read_text() and '\nOK\n' in(STAGE/'python-tests.log').read_text()
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(x['implementation']=='handler-present'for x in status['skills'])==198 and not status['completedClasses']
 files=e.stage_files(STAGE,WIDE,IRON,PROBE)+[source_path]
 for stage in [STAGE,WIDE,IRON,PROBE]:files += [p for p in(stage/'failed-1').rglob('*')if p.is_file()]
 for kind in ['content','integration']:files.append(STAGE/kind/f'Sanctuary.{kind.title()}.dll')
 files += [ROOT/p for p in ['tests/check_single_skill_property.py','tests/check_iron_golem_birth.py','tools/probe_single_skill_property.py','tools/reference/inferno-held-input-client-evidence.json','tools/build_skill_roadmap.py']]
 files += [ROOT/'docs'/p for p in ['single-skill-property-checkpoint.md','necromancer-coverage-matrix.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md','skill-mechanical-reference.md']];files.append(Path(__file__).resolve())
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=198,completedClasses=[],sourceSnapshot=3,contentBuild=1,integrationBuild=2,focusedRun=2,retainedRun=2,ironRun=2,auditRun=2,
  focusedMarkers=12,retainedMarkers=185,learnedIronMarkers=12,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,originalActors=2,frozenRevives=1,callbackSourceContexts=13824,pythonTests=71,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Complete older property22 over existing numeric/list/LCG services;13824sanitized controls and matching native traces/values/seeds. Numeric IDs narrow only at storage;current text keys resolve uniquely and unknown text refuses.',
         'Original Undead Crown graph Iron and frozen Revive contacts retain full item parents/gear/numeric layers/wounds/resources/RNG through two reloads. Retention does not assign skills to an NPC. Generic191-plan numeric admission and player item-skill activation remain unchanged.',
         'All182parent markers remain. Obsolete Crown refusals are replaced by positive coverage;duplicate refusal scenes are removed. Baranar independent saved cold-duration fallback remains explicitly refused.'],
  limits=['Current executable/global factory and recipient skill ownership/activation acceptance remain separate. Ranged null-item source dereference is outside the valid-item contract. Sunder,other specialized properties,complete class/client/asset acceptance remain open.',
          'First producer omitted a printf result argument;first replacement refusal assertions matched absent diagnostic text. Failed source/binaries/tests/logs are preserved. Final producer enables format warnings as errors;all source bodies and every case field except the recorded return match the prior producer. Content1 bytes are unchanged through fixture corrections.',
          'No new effect/audio or talent/hotkey identity. Installed build,saves,configuration and accepted41client patches/shared inputs stay unchanged.'])
 e.write('single-skill-property-native-evidence.json',result,files,check)
 print('Single skill property22:12focused/185retained/12learned-Iron;two reloads each;2original actors;13824source controls;71Python;198handlers/no class complete;installed world preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
