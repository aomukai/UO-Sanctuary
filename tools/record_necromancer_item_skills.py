#!/usr/bin/env python3
"""Bind whole Necromancer uniques to real granted casts and save preservation."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'necromancer-item-skills';WIDE=BASE/'necromancer-item-skills-retained';PARENT='91f3ede72ddb8b0902369e86483663cb6d6dec92'
def audits(artifacts,reference):
 counts={}
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  current=e.load(STAGE/name);assert current==e.load(ROOT/'tools/reference'/name)
  prior=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));omit={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  a={k:v for k,v in current.items()if k not in omit};b={k:v for k,v in prior.items()if k not in omit}
  if name.startswith('named'):
   old={r['Id']:r for r in b['Definitions']};changed=[]
   for row in a['Definitions']:
    before=old[row['Id']]
    if row!=before:
     assert before['Available']==False and before['Reasons']==['Effect: skill'] and row['Available']==True and row['Reasons']==[]
     assert {k:v for k,v in before.items()if k not in ['Available','Reasons']}=={k:v for k,v in row.items()if k not in ['Available','Reasons']};changed.append(row['Id'])
   assert set(changed)==set(reference['expectedAvailabilityDelta']) and len(changed)==4
   assert {k:v for k,v in a.items()if k!='Definitions'}=={k:v for k,v in b.items()if k!='Definitions'} and len(a['Definitions'])==len(b['Definitions'])==573
  else:assert a==b
  assert current['ContentAssemblySha256']==artifacts['Assemblies/Sanctuary.Content.dll']
  for p,h in current['InputHashes'].items():assert e.sha(ROOT/p)==h,p
  counts[name]=len(current['InputHashes'])
 assert markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
 return counts

def record(check=False):
 snapshot=STAGE/'source-snapshot-6.json';born=e.inventory(STAGE,WIDE,snapshot);now=e.load(snapshot)['inputs'];old=e.load(BASE/'single-skill-property/source-snapshot-3.json')['inputs']
 assert now['sourceInputs'].keys()-old['sourceInputs'].keys()=={'tests/Sanctuary.Integration/NecromancerItemSkillChecks.cs'}
 assert all(now['sourceInputs'][p]==h for p,h in old['sourceInputs'].items())
 assert now['projectInputs']==old['projectInputs'] and now['copiedData'].keys()==old['copiedData'].keys()
 assert {p for p,h in old['copiedData'].items()if now['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/class-skill-status.json','Data/Sanctuary/Balance/named-active-skills.json','Data/Sanctuary/Balance/named-item-coverage.json','Data/Sanctuary/Balance/named-skill-backlog.json'}
 compiled=e.load(STAGE/'source-snapshot-5.json')['inputs'];assert compiled['sourceInputs']==now['sourceInputs'] and compiled['projectInputs']==now['projectInputs']
 assert {p for p,h in compiled['copiedData'].items()if now['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/named-item-coverage.json','Data/Sanctuary/Balance/named-skill-backlog.json'}
 assert e.sha(STAGE/'content/Sanctuary.Content.dll')==e.sha(STAGE/'failed-1/content/Sanctuary.Content.dll')
 base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2});delta=Counter({'necromancer-item-skill-check':1,'necromancer-item-skill-reload-check':2})
 e.cohort(STAGE,base+delta,12);prior=BASE/'single-skill-property-retained/birth-native-checks.log';receipt=e.load(ROOT/'tools/reference/single-skill-property-native-evidence.json');assert e.sha(prior)==receipt['files'][str(prior.relative_to(ROOT))]
 retained=markers(prior);assert sum(retained.values())==185;e.cohort(WIDE,retained+delta,188)
 for stage,run in [(STAGE,5),(WIDE,3)]:
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text();inputs=e.load(stage/'birth-inputs.json');assert inputs['artifacts']==born['artifacts'] and inputs['copiedData']==born['copiedData'];e.snapshot(snapshot,stage/'birth-inputs.json')
  for p,h in inputs['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  saved=e.load(stage/'necromancer-item-skill-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==4 and len({x['Owner']for x in saved['Actors']})==4 and len({x['Item']for x in saved['Actors']})==4
  assert sum(len(x['Ranks'])for x in saved['Actors'])==14 and len({t for x in saved['Actors']for t in x['Ranks']})==13
  assert(stage/'birth-native-checks.log').read_text().count('Necromancer individual item skills:4whole uniques/14zero-hard quick releases')==3
  assert "denied by connection filter 'auto-denylist'"not in(stage/'birth-native-checks.log').read_text()
 for name in ['content-build-1.log','integration-build-4.log']:
  text=(STAGE/name).read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 sourcePath=ROOT/'tools/reference/necromancer-item-skills-reference.json';reference=e.load(sourcePath);assert reference['wholeItems']==4 and reference['propertyGrants']==14 and reference['distinctSkills']==13 and len(reference['activatedBindings'])==12
 for p,h in reference['inputs'].items():assert e.sha(ROOT/p)==h,p
 fingerprints=audits(born['artifacts'],reference);protected=preservation();client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 protected['acceptedClientPatches']=41
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==8 and all(x['exitCode']==0 for x in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text() and '\nOK\n'in(STAGE/'python-tests.log').read_text()
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(x['implementation']=='handler-present'for x in status['skills'])==198 and not status['completedClasses']
 files=e.stage_files(STAGE,WIDE)+[sourcePath]
 for n in [1,2]:files += [p for p in(STAGE/f'failed-{n}').rglob('*')if p.is_file()]
 files += [p for p in(STAGE/'prior-3').rglob('*')if p.is_file()]
 files += [p for p in(WIDE/'failed-1').rglob('*')if p.is_file()]
 files += [p for p in(STAGE/'prior-4').rglob('*')if p.is_file()]
 files += [p for p in(WIDE/'prior-2').rglob('*')if p.is_file()]
 for kind in ['content','integration']:files.append(STAGE/kind/f'Sanctuary.{kind.title()}.dll')
 files += [ROOT/p for p in ['tests/check_necromancer_item_skills.py','tests/check_iron_golem_birth.py','tests/Sanctuary.Integration/FixtureSpace.cs','.local/class-skills/player-melee-return/native/Projects/Server/Network/NetState/NetState.Network.cs','tools/review_necromancer_item_skills.py','tools/build_skill_roadmap.py','tools/reference/inferno-held-input-client-evidence.json']]
 files += [ROOT/'docs'/p for p in ['necromancer-item-skills-checkpoint.md','class-skills-resume-checkpoint.md','necromancer-coverage-matrix.md','class-skill-roadmap.md','deferred-named-skills.md','named-item-readiness.md','skill-effects.md','skill-icons.md','skill-mechanical-reference.md']];files.append(Path(__file__).resolve())
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=198,completedClasses=[],sourceSnapshot=6,contentBuild=1,integrationBuild=4,focusedRun=5,retainedRun=3,auditRun=5,focusedMarkers=12,retainedMarkers=188,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,wholeItems=4,specificSkillProperties=14,distinctSkills=13,activatedBindings=12,availabilityDelta=reference['expectedAvailabilityDelta'],pythonTests=71,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Twelve additional named-active bindings enable exactly four whole uniques. Production C# and original item rolls/source definitions are unchanged. All14properties produce actual zero-hard quick casts with correct existing start/release payment and no replay.',
         'Real curse,armor,barrier,weapon poison,corpse and missile consumers receive effective ranks. Unidentified/backpack/foreign-class items are inactive;unequip leaves released flights unchanged. Four original items/owners retain parents,gear,ranks,fractional wounds/resources and Bone Armor through two reloads.',
         'Fresh573-item audit changes only unique11/13/170/351 from Effect:skill to available;all other named and first-loop decisions remain. All185parent markers retained.'],
  limits=['This does not certify modern barrier footprint/death ordering,nonzero Sunder,current executable/global/PvP/world/client parity or complete class acceptance. Other class item activations and held proc/charge items remain separate.',
          'Two failed fixtures/binaries/logs are preserved:respec also removed attributes needed to equip Blackbog;generic release-payment assertion ignored the already-established Poison Dagger start payment. The intermediate focused3cohort passed,but retained1exposed its unauthenticated reload socket:the native idle timeout/denylist correctly rejected later loopback tests. Reload now calls existing fixture authentication;server protection is unchanged. Only the new fixture changed;Content1 is unchanged across corrections. Two Python reproduction checks then required regenerated named coverage/backlog data;prior passing native inputs are preserved and final cohorts rerun against snapshot6. Snapshot6 C#/project inputs exactly equal the pre-Integration4 snapshot5;only external generated data changed.',
          'No new effect/audio or talent/hotkey identities. Existing placeholders remain separate;installed build,saves,configuration and accepted41client patches/shared inputs remain unchanged.'])
 e.write('necromancer-item-skills-native-evidence.json',result,files,check)
 print('Necromancer item skills:4whole uniques/14zero-hard actual casts/12activation additions;12focused+188retained,two reloads each;4exact audit changes;71Python;198handlers/no class complete;installed world preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
