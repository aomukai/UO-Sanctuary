#!/usr/bin/env python3
"""Bind Find Potion native verification, approved area gates and retained saves."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/find-potion'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
 _,prior=frozen_receipt('83e9e41','tools/reference/grim-ward-lifecycle-native-evidence.json');assert prior['verified']['passMarkers']==237
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==248
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/grim-ward-lifecycle/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
 assert sum(before.values())==237 and not before-after and after-before==Counter({'find-potion-check':1,'find-potion-reload-check':2,'find-potion-live-check':3,'find-potion-live-result':3,'find-potion-snapshot-check':2})
 assert log.count('Find Potion policy:5004complete legacy-source cases;10existing potion codes;generic fallback explicit;modern RNG/curve parity remains open')==3
 assert log.count('Find Potion reload:saved corpse flags,potion identity,original loot,fractional mana and no transient cast replay pass')==2
 sourcePath=ROOT/'tools/reference/find-potion-native-source-evidence.json';source=json.loads(sourcePath.read_text());artifacts.append(sourcePath)
 assert source['caseCounts']==dict(eligibilityCases=384,chanceCases=80,outputCases=4500,contextCases=32,doGuardCases=8) and not source['currentExecutableParityVerified']
 assert len(source['wholeRoutineHashes'])==8
 for name,h in source['artifacts'].items():assert sha(ROOT/name)==h,name;artifacts.append(ROOT/name)
 # All existing species fields stay intact; softness comes only from pinned
 # monstats -> MonStatsEx -> monstats2, not UO bodies or corpse material.
 oldRules=json.loads(subprocess.check_output(['git','show','83e9e41:data/Sanctuary/Balance/corpse-source-rules.json'],cwd=ROOT));rules=json.loads((ROOT/'data/Sanctuary/Balance/corpse-source-rules.json').read_text())
 assert [{k:v for k,v in r.items() if k!='soft'} for r in rules]==oldRules
 sourceRoot=ROOT/'tools/reference/d2r-named-equipment';mon=json.loads((sourceRoot/'json/monstats.json').read_text());models=json.loads((sourceRoot/'json/monstats2.json').read_text());by={r['Id']:r for r in mon.values() if r.get('Id')}
 assert len(rules)==751 and sum(r['soft'] for r in rules)==505
 for r in rules:assert r['soft']==bool(models.get(by[r['source']].get('MonStatsEx'),{}).get('soft'))
 manifest=json.loads((sourceRoot/'sources.json').read_text())
 for name in ['json/monstats.json','json/monstats2.json','json/skills.json']:
  p=sourceRoot/name;assert sha(p)==manifest['files'][name]['sha256'];artifacts.append(p)
 artifacts.append(sourceRoot/'sources.json')
 assert json.loads((ROOT/'data/Sanctuary/Balance/region-acts.json').read_text())==[]
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text())
  assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 oldAudit=json.loads(subprocess.check_output(['git','show','83e9e41:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
 assert audits[0]['Definitions']==oldAudit['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[741,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for name,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/name)==h,name
 statusPath=ROOT/'data/Sanctuary/Balance/class-skill-status.json';status=json.loads(statusPath.read_text());skill=next(s for s in status['skills'] if s['talentId']=='barbarian.findpotion')
 assert skill['implementation']=='handler-present' and skill['verification']=='partial-native-coverage' and not skill['complete']
 assert sum(s['implementation']=='handler-present' for s in status['skills'])==184 and not any(s['complete'] for s in status['skills'])
 icons=ROOT/'data/Sanctuary/Balance/skill-icons.json';assert icons.read_bytes()==subprocess.check_output(['git','show','83e9e41:data/Sanctuary/Balance/skill-icons.json'],cwd=ROOT)
 artifacts += [statusPath,icons,mainPath]+[STAGE/n for n in ['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','find-potion-fixtures.json']]
 artifacts += [p for p in (STAGE/'diagnostics').rglob('*') if p.is_file()]
 artifacts += [ROOT/n for n in ['tools/record_find_potion.py','tests/check_find_potion.py','tools/build_class_skills.py','tools/build_skill_roadmap.py','tools/reference/find-potion-area-policy.json','tools/reference/barbarian-find-potion-preflight.json','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]
 result=dict(referenceVersion='3.3.93847',component='Find Potion learned casting,soft corpse policy and existing potion delivery',installed=False,handlerEnabled=True,currentExecutableParityVerified=False,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=248,retainedMarkers=237,worldBoots=3,actualWorldReloads=2,legacySourceCasesPerBoot=5004,wholeLegacyRoutines=8,worldActDifficultyTypeCasts=90,naturalConnectedCasts=3,softSpeciesRows=751,softSpecies=505,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[741,21],unchangedMainInstallationArtifacts=106,newFullClassRun=False,frozenPriorFullClassMarkers=1184),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='83e9e41',receipt='tools/reference/grim-ward-lifecycle-native-evidence.json'),
  established=['User-approved explicit caster region Act tags have no default. Closest tagged ancestor overrides parents; invalid/duplicate definitions reject atomically. Difficulty reuses configured facets. Initial world designation list is empty; untagged starts refuse before payment/animation/reservation, and invalidated paid casts produce nothing without refund.',
   'Right/quick learned SC start pays2mana once; release uses live skill values and reserves before chance. Success or failure consumes one attempt while preserving existing corpse contents/appearance. Only mapped soft selectable monster corpses qualify; a treasure-class context is not required. Existing shared corpse access and noselect resurrection exclusions are reused.',
   'Complete unchanged legacy source yields5004bounded cases:384eligibility,80chance/repeat,4500all15row/type,32context/fallback,8guard cases. The managed policy matches those records each boot. Generic code0/quality2 is represented in the policy but explicitly unsupported in the potion factory; approved valid area gates prevent reaching it in learned casts. Native ordinary RNG and current DM curve are reused,not newly certified source streams.',
   'Ninety real corpse casts cover3difficulties*5Acts*6type boundaries,using existing healing/mana/rejuvenation potion classes and drop origins. They retain loot,zero-mana release,failed attempt semantics,insufficient fractional mana,quick bindings,cancellation,untagged/retagged gates and hard-corpse rejection. Three connected natural40ms releases pass. Saved used/pending corpses,potion identity,loot and fractional resources survive two actual reloads;transient paid casts do not replay.',
   'All237predecessor markers retained. Both builds zero warnings/errors,70Python checks,573unchanged named decisions and106unchanged main installation artifacts. Test-only failures and source/DLL/input snapshots are preserved:unavailable fixture corridor,early connection lookup,missing ready handshake,test mana-regeneration hook ordering and two compiler API corrections. The reload resource mismatch was a test regeneration override replaced during server Initialize; the final run retains exact resource assertions and adds before-save controls. No gameplay comparison expectation was weakened. No new full-class regression or installation is claimed.'],
  remaining=['Exact current DM rounding,shared owner RNG/snapshot order,h2h/auto-approach and full SC/FCR/input/client behavior remain uncertain or unverified. Shared8tile corpse access is an explicit current native adapter,not asserted as D2R distance. Unsupported item-skill/PvP/target branches stay separate.',
   'World designers still need explicit region Act assignments; no default or arbitrary tags were introduced. Effects/audio and talent/right/quick icons remain clearly identified placeholders. No class is complete;continue the existing roadmap and individual blockers.'])
 encoded=json.dumps(result,indent=2)+'\n';out=ROOT/'tools/reference/find-potion-native-evidence.json'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Find Potion:248native/70Python;5004source cases per boot;90area/type casts;3natural casts;2reloads;184handlers,no class complete.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
