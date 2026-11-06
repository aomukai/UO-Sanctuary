#!/usr/bin/env python3
"""Bind separate Entropy cloud channels, current native checks and preservation."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'entropy-clouds';WIDE=BASE/'entropy-clouds-retained';PARENT='1fa76978fd1e90974ffea8a0b99e8f5603007647'
def record(check=False):
 snapshot=STAGE/'source-snapshot-4.json';born=e.inventory(STAGE,WIDE,snapshot);now=e.load(snapshot)['inputs'];old=e.load(BASE/'double-swing-sequence/source-snapshot-13.json')['inputs']
 assert now['sourceInputs'].keys()-old['sourceInputs'].keys()=={'src/Sanctuary.Content/WarlockEntropyClouds.cs','tests/Sanctuary.Integration/EntropyCloudChecks.cs'}
 assert {p for p,h in old['sourceInputs'].items()if now['sourceInputs'][p]!=h}=={'src/Sanctuary.Content/MiasmaPackets.cs','src/Sanctuary.Content/GraveyardUndead.cs','tests/Sanctuary.Integration/AttractCastChecks.cs'}
 assert now['projectInputs']==old['projectInputs'] and now['copiedData'].keys()==old['copiedData'].keys()
 assert {p for p,h in old['copiedData'].items()if now['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/necromancer-summons.json','Data/Sanctuary/Balance/class-skill-status.json'}
 first=e.load(STAGE/'source-snapshot-1.json')['inputs'];assert {p for p,h in first['sourceInputs'].items()if now['sourceInputs'][p]!=h}=={'src/Sanctuary.Content/GraveyardUndead.cs','tests/Sanctuary.Integration/AttractCastChecks.cs'} and first['projectInputs']==now['projectInputs']
 assert {p for p,h in first['copiedData'].items()if now['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 import json,subprocess
 oldRows={r['key']:r['fields']for r in json.loads(subprocess.check_output(['git','show',PARENT+':data/Sanctuary/Balance/necromancer-summons.json'],cwd=ROOT))}
 newRows={r['key']:r['fields']for r in e.load(ROOT/'data/Sanctuary/Balance/necromancer-summons.json')}
 assert newRows.keys()-oldRows.keys()=={'miasmachainscloud'} and all(newRows[k]==v for k,v in oldRows.items())
 base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
 delta=Counter({'entropy-cloud-check':1,'entropy-cloud-reload-check':2,'miasma-packets-check':1,'miasma-packets-reload-check':2})
 focused=delta+Counter({'attract-cast-check':1,'attract-cast-reload-check':2,'attract-cast-reload-result':2,'attract-cast-right-check':3,'attract-cast-right-result':3,'attract-cast-quick-check':3,'attract-cast-quick-result':3});e.cohort(STAGE,base+focused,32)
 isolation=BASE/'entropy-clouds-attract-isolated/prior-1';attract=focused-delta;e.cohort(isolation,base+attract,26)
 original=e.load(STAGE/'before-attract-1/birth-inputs.json');isolated=e.load(isolation/'birth-inputs.json');failed=e.load(WIDE/'failed-1/birth-inputs.json')
 assert original['artifacts']==isolated['artifacts']==failed['artifacts'] and original['copiedData']==isolated['copiedData']==failed['copiedData']
 assert 'attract-cast-right-result: failed'in(WIDE/'failed-1/birth-native-checks.log').read_text()
 prior=BASE/'double-swing-sequence-retained/birth-native-checks.log';receipt=e.load(ROOT/'tools/reference/double-swing-native-evidence.json');assert e.sha(prior)==receipt['files'][str(prior.relative_to(ROOT))]
 retained=markers(prior);assert sum(retained.values())==224;e.cohort(WIDE,retained+delta,230)
 for stage in [STAGE,WIDE]:
  assert 'pass across3boots/2reloads'in(stage/'native-run-2.log').read_text()
  log=(stage/'birth-native-checks.log').read_text();assert log.count('Entropy clouds:distinct effective Range/Radius')==3 and log.count('Miasma packets:9180full-source comparisons')==3
  saved=e.load(stage/'entropy-cloud-fixtures.json');assert len(saved)==3 and {s['Female']for s in saved}=={False,True}and {s['Hard']for s in saved}=={4,9,20}
  for s in saved:
   assert s['Bolt']['EffectiveRank']==s['Chain']['EffectiveRank']==s['Hard']+1 and s['Bolt']['HardRank']==s['Chain']['HardRank']==s['Hard']
   assert s['Bolt']['ServerCalculation']is None and s['Chain']['ServerCalculation']==(4 if s['Hard']==20 else 2) and s['Mana']%256!=0
 for name in ['content-build-3.log','integration-build-3.log']:
  text=(STAGE/name).read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==8 and all(x['exitCode']==0 for x in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text() and '\nOK\n'in(STAGE/'python-tests.log').read_text()
 source=ROOT/'tools/reference/entropy-clouds-reference.json';proof=e.load(source);assert proof['referenceVersion']=='3.3.93847'and not proof['handlerEnabled']and not proof['processMemoryRead']
 for p,h in proof['inputs'].items():assert e.sha(ROOT/p)==h,p
 packet=ROOT/'tools/reference/miasma-packets-source-evidence.json';p=e.load(packet);assert p['cases']==9180
 fixture=BASE/'miasma-packets-source-probe.json';assert e.sha(fixture)==p['artifacts'][str(fixture.relative_to(ROOT))]
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for g in ['patchHashes','sharedInputs']:
  for p,h in client[g].items():assert e.sha(ROOT/p)==h,p
 protected['acceptedClientPatches']=41
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(r['implementation']=='handler-present'for r in status['skills'])==199 and not status['completedClasses']
 for talent in ['warlock.enhancedentropy','warlock.miasmachain']:
  r=next(r for r in status['skills']if r['talentId']==talent);assert r['verification']=='partial-native-coverage' and r['implementation']!='handler-present'and not r['complete']
 files=e.stage_files(STAGE,WIDE,BASE/'entropy-clouds-attract-isolated')+[source,packet,fixture,Path(__file__).resolve()]
 for a in [STAGE/'before-attract-1',WIDE/'failed-1',STAGE/'before-context-2',BASE/'entropy-clouds-attract-isolated/prior-1']:files += [p for p in a.rglob('*')if p.is_file()]
 for k in ['Content','Integration']:files.append(STAGE/k.lower()/f'Sanctuary.{k}.dll')
 files += [ROOT/p for p in ['tests/check_entropy_clouds.py','tests/check_double_swing_sequence.py','tests/check_iron_golem_birth.py','tools/review_entropy_clouds.py','tools/build_class_skills.py','tools/build_skill_roadmap.py']]
 files += [ROOT/'docs'/p for p in ['entropy-clouds-checkpoint.md','attract-cast-checkpoint.md','necromancer-coverage-matrix.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md','skill-mechanical-reference.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlerEnabled=False,handlers=199,completedClasses=[],unchangedAttractIsolationMarkers=26,attractForcedMissThenHit=True,sourceSnapshot=4,contentBuild=3,integrationBuild=3,focusedRun=2,retainedRun=2,auditRun=2,focusedMarkers=32,retainedMarkers=230,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,thresholdControls=14,retainedMiasmaSourceComparisons=9180,pythonTests=71,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Shared captured cloud shape evaluates owned Range/Radius through effective Entropy rank while retaining Chain hard-only SrvCalc1 separately. Existing Bolt packets reuse the shape with unchanged damage. Only missile696is added;all existing source rows remain exact.',
   'Threshold goldens,actual original Tarnhelm equip/removal,hard20training,immutable earlier contexts and three Warlock/Witch actors retain ranks/gear/fractional wounds/mana across two reloads. All224parent markers and9180Miasma packet comparisons retain.'],
  limits=['No current do41 geometry,hit identity,first/last contact or complete Entropy/Chain cast is certified. Abyss sksrc owner/source context remains unresolved;no skill()alias is inferred. Handler coverage stays199,no class complete.',
   'Content3/Integration3use pre-build snapshot4;explicit weapon ranged-context detection preserves the former public CheckHit route. Earlier Content1/Integration1focused15passed,then retained1failed Attract with no damage. The unchanged-build Attract isolation passed26markers. New per-actor ordinary melee RNG suppliers preserve defaults;Attract input/reload tests force a miss then a hit without bypassing AI,accuracy or damage. The original failure root cause remains untraced.',
   'The failed retained1,earlier focused15and unchanged-build isolation are preserved. Installed106artifacts/38saves/20configs and accepted41client patches remain unchanged. Final effects/audio and icons are separate missing assets.'])
 e.write('entropy-clouds-native-evidence.json',result,files,check)
 print('Entropy cloud channels:32focused/230retained,2reloads;9180retained packet comparisons;71Python;199handlers/no class complete;installed world preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
