#!/usr/bin/env python3
"""Bind Defiler independent vitals and original Warlock/Witch Gazer bodies."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/defiler-body';FOCUS=ROOT/'.local/class-skills/defiler-body-focus'
PARENT='095b19b9dcf117045a696693311a4b51f0529d5a';sha=inventory.sha

def record(check=False):
 inventory.STAGE=STAGE;native=inventory.validate_inventory(STAGE/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json');focused=inventory.validate_inventory(FOCUS/'birth-inputs.json')
 for folder,name in ((STAGE,'birth-inputs.json'),(STAGE,'audit-inputs.json'),(FOCUS,'birth-inputs.json')):snapshot(STAGE/'pre-build-1.json',folder/name)
 assert native['artifacts']==audit['artifacts']==focused['artifacts'] and native['copiedData']==audit['copiedData']==focused['copiedData']
 prior=json.loads((ROOT/'.local/class-skills/demon-incoming/birth-inputs.json').read_text())
 assert {n for n in set(prior['sourceInputs'])|set(native['sourceInputs']) if n.endswith('.cs') and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}=={
  'src/Sanctuary.Content/DemonBirthVitals.cs','src/Sanctuary.Content/DefilerPrepared.cs','src/Sanctuary.Content/WarlockDefilerBody.cs',
  'src/Sanctuary.Content/TaintedPrepared.cs','src/Sanctuary.Content/CreatureProfiles.cs','src/Sanctuary.Content/PreparedMonsterNativeDamage.cs','tests/Sanctuary.Integration/DefilerBodyChecks.cs'}
 assert {n for n in set(prior['copiedData'])|set(native['copiedData']) if prior['copiedData'].get(n)!=native['copiedData'].get(n)}=={'Data/Sanctuary/Balance/class-skill-status.json','Data/Sanctuary/Balance/necromancer-summons.json'}
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][n]==prior['artifacts'][n]==sha(STAGE/'native/Distribution'/n)
 proofpath=ROOT/'tools/reference/defiler-source-evidence.json';proof=json.loads(proofpath.read_text())
 assert not any(proof[k] for k in ('handlerEnabled','currentExecutableParity','rawExactFormulaVerified')) and proof['wrappedControlFogCases']==10710
 for name,digest in {**proof['inputs'],**proof['artifacts']}.items():assert sha(ROOT/name)==digest,name
 diagnostic=ROOT/'.local/class-skills/defiler-source/raw-diagnostic-1/sanitizer.log';assert 'index -1 out of bounds' in diagnostic.read_text()
 rows=json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text());assert len([r for r in rows if r['key']=='warputriddefiler'])==1
 current={r['key']:r for r in rows};old={r['key']:r for r in json.loads(subprocess.check_output(['git','show',PARENT+':data/Sanctuary/Balance/necromancer-summons.json'],cwd=ROOT))}
 assert current.keys()-old.keys()=={'warputriddefiler'} and all(current[k]==v for k,v in old.items())
 parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/demon-incoming-native-evidence.json'],cwd=ROOT));baseline=ROOT/'.local/class-skills/demon-incoming/birth-native-checks.log'
 assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
 for folder,previous,count in ((STAGE,ROOT/'.local/class-skills/demon-incoming',393),(FOCUS,ROOT/'.local/class-skills/demon-incoming-focus',61)):
  expected=markers(previous/'birth-native-checks.log');expected['defiler-body-check']+=1;expected['defiler-body-reload-check']+=2
  log=(folder/'birth-native-checks.log').read_text();assert sum(expected.values())==count and markers(folder/'birth-native-checks.log')==expected
  assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
  assert log.count('Defiler:2040independent supplier/18360noRatio vital contexts;raw damage/Health Link remain disabled')==1
  saved=json.loads((folder/'defiler-body-fixtures.json').read_text());assert len(saved)==2 and {r['Female'] for r in saved}=={True,False}
  for row in saved:
   assert row['Life']==128767 and row['Maximum']==129024 and row['Mana']==22784 and len({row[n] for n in ('Owner','Body','Helm')})==3
   body=json.loads(row['State']);assert body['Profile']=='prepared-defiler-life-owner-v1' and body['Owner']==row['Owner']
   snap=body['Snapshot'];assert snap['MasteryRank']==17 and snap['HardMastery']==16 and snap['OathRank']==6 and snap['Birth']['Suppliers']['Rank']==2
   assert 'DamagePercent' not in snap['Birth']['Suppliers'] and snap['Birth']['Vitals']['MaximumLife256']==129024
 for kind in ('content','integration'):assert all(s in (STAGE/f'{kind}-build-1.log').read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 for name,count in (('named-item-runtime-audit.json',983),('first-loop-affix-audit.json',21)):
  a=json.loads((STAGE/name).read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text()) and len(a['InputHashes'])==count and a['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,digest in a['InputHashes'].items():assert sha(ROOT/n)==digest,n
  b=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in a.items() if k not in skip}=={k:v for k,v in b.items() if k not in skip}
 assert (STAGE/'audit-native-checks.log').read_text().count(': passed')==2
 assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
 for name,text in (('source-check.log','10710 explicitly wrapped Fog controls'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in (STAGE/name).read_text()
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest,name
 protected=json.loads((ROOT/'.local/class-skills/creeper-frame-installation.json').read_text())['protected']
 for key,rel in (('saves','.local/server/Saves'),('configuration','.local/server/Configuration')):
  p=Path(installed['runtimeRoot'])/rel;assert {str(f.relative_to(p)):sha(f) for f in p.rglob('*') if f.is_file()}==protected[key]
 state=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not state['completedClasses'] and sum(s['implementation']=='handler-present' for s in state['skills'])==191
 files=[Path(__file__).resolve(),proofpath,ROOT/'tools/probe_defiler_foundation.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/build_class_skills.py',baseline,
  ROOT/'tests/check_defiler_body.py',ROOT/'tests/check_defiler_body_focus.py',
  *(ROOT/'docs'/n for n in ('defiler-body-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
  *(STAGE/n for n in ('pre-build-1.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-1.log','audit-native-checks.log','audit-run-1.log','content-build-1.log','integration-build-1.log','python-tests.log','source-check.log','readiness-check.log','catalogue-check.log','roadmap-check.log','defiler-body-fixtures.json')),
  *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','defiler-body-fixtures.json'))]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,component='Defiler independent vital suppliers and saved Gazer body',handlerEnabled=False,goalComplete=False,completedClasses=[],
  verified=dict(nativeMarkers=393,retainedNativeMarkers=390,focusedNativeMarkers=61,boots=3,reloads=2,saves=3,independentSupplierContexts=2040,vitalProjections=18360,wrappedControlFogCases=10710,rawDamageExpressionVerified=False,originalBodies=2,pythonTests=71,namedDecisions=573,auditFingerprints=[983,21],handlers=191,protectedInstalledArtifactsUnchanged=106),tested=native,
  limitations=['Life/stat/owner body only. Raw damage,ordinary offense,Health Link,GenericPet world/controller/mode/skill-list/extended-passive,learned placement/payment/shared cap and full lifecycle remain unfinished.',
   'The exact raw expression produced an old compiler leading-symbol bounds failure. Two WRAPPED controls are distinct inputs and do not prove modern raw damage. No production parser repair or multiplier is enabled;the diagnostic remains pinned.',
   'Tainted uses the shared noRatio helper but keeps its existing DTO and all predecessor numerical/body/flight/aura/rank checks. New Defiler source data adds one numeric row without changing other summon rows.',
   'Original Warlock/Witch bodies and helm,wounds,mana retain through both reloads. Recovery inspection clocks are held;this is not unheld downtime or automatic AI acceptance. Nonzero inherited sunder remains refused.',
   'No native/build diagnostics,deployment,installed-save write,new learned handler,class completion or generated art. Gazer is the approved existing appearance;feedback placeholders and pending effects/audio/icons remain separate.'],evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
 p=ROOT/'tools/reference/defiler-body-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Defiler body:393 retained/61 focused checks;two original Gazer bodies;raw damage/offense/Health Link remain unverified;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
