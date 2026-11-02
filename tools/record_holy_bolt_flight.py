#!/usr/bin/env python3
"""Bind Holy Bolt source flight,RNG,natural resume and reference normalization."""
import argparse,hashlib,json,re,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/holy-bolt-flight'
PARENT='0de96d893767354438af0859480a81d216e273b6';sha=inventory.sha
META='Data/Sanctuary/Balance/class-skill-status.json'
def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def passed(path,total,boots):
 text=path.read_text();counts=markers(path)
 assert sum(counts.values())==total and ': failed' not in text and ' ERR]' not in text,path
 assert text.count('Shutting down')==text.count('Writing world save snapshot done')==boots,path
 return text,counts

def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/'pre-build-5.json',STAGE/'birth-inputs.json')
 log,counts=passed(STAGE/'birth-native-checks.log',995,3);assert 'pass across3boots/2reloads' in (STAGE/'full-run-5.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_holy_bolt_flight import configuration
 cfg=configuration(True);assert len(cfg['extra_families'])==71 and len(cfg['extra_checks'])==17 and len(cfg['extra_target_input_commands'])==96
 for name in cfg['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in cfg['extra_checks']:assert counts[name]==3,name
 for name in cfg['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 assert counts['holy-bolt-flight-result']==1 and counts['holy-bolt-flight-reload-result']==2
 phrase='Holy Bolt flight:source50/3840/cells;1178FP current-Prayer heal then4226FP captured magic;beast consumes no RNG;last-collider/deep copy;9lifecycle/wall/slow controls;final49/50;atomic bad source/legacy omission;original paid moving flight staged'
 assert log.count(phrase)==1
 saved=json.loads((STAGE/'holy-bolt-flight-checks.json').read_text());f=saved['Flight']
 assert saved['Generation']==2 and saved['Mana']==17 and saved['DeletedCollider']==f['LastCollision']!=0
 assert f['Motion']['Seed']==1193071124610 and f['Frame']>2 and f['X']>saved['Origin']['X']+.9375 and f['Motion']['Steps']['Cells']
 assert 'Holy Bolt cleared absent saved collider:'+str(f['Id'])+'/' in log
 focus=STAGE/'focused-accepted-4';fi=json.loads((focus/'birth-inputs.json').read_text());passed(focus/'birth-native-checks.log',254,3)
 # Annotation bridge:retain the actually tested focused inputs rather than
 # relabeling them as build5. All compiled/code/project inputs are identical;
 # the sole copied-data difference is the reviewed remaining-note string.
 assert fi['sourceInputs'].items()<=tested['sourceInputs'].items() and fi['artifacts']==tested['artifacts']
 assert {p for p in fi['sourceInputs'] if p.endswith('.cs')}=={p for p in tested['sourceInputs'] if p.endswith('.cs')}
 for path,digest in fi['sourceInputs'].items():assert sha(ROOT/path)==digest,path
 before=json.loads((STAGE/'pre-build-4.json').read_text())['inputs'];after=json.loads((STAGE/'pre-build-5.json').read_text())['inputs']
 assert before['sourceInputs']==after['sourceInputs'] and before['projectInputs']==after['projectInputs']
 assert fi['copiedData'].keys()==tested['copiedData'].keys() and before['copiedData']==fi['copiedData'] and after['copiedData']==tested['copiedData']
 assert {p for p in fi['copiedData'] if fi['copiedData'][p]!=tested['copiedData'][p]}=={META}
 assert fi['copiedData'][META]==hashlib.sha256(historical('data/Sanctuary/Balance/class-skill-status.json')).hexdigest()
 for part in ('content','integration'):
  path=part+'/Sanctuary.'+part.title()+'.dll';assert sha(STAGE/path)==sha(focus/path)
  for build in (4,5):
   text=(STAGE/f'{part}-build-{build}.log').read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
 now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());old=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'));changed=[]
 for a,b in zip(now['skills'],old['skills'],strict=True):
  if a!=b:assert a['talentId']=='paladin.holybolt';changed.append(a['talentId']);b['remaining']=a['remaining']
 assert changed==['paladin.holybolt'] and now==old and not now['completedClasses']
 parent=ROOT/'tools/reference/holy-bolt-damage-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 prior=json.loads((ROOT/'.local/class-skills/holy-bolt-damage/birth-inputs.json').read_text())
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][name]==tested['artifacts'][name]
 assert {p for p in prior['copiedData'] if prior['copiedData'][p]!=tested['copiedData'][p]}=={META}
 negative=STAGE/'negative-offline-replay';ni=json.loads((negative/'birth-inputs.json').read_text());text=(negative/'birth-native-checks.log').read_text()
 assert 'holy-bolt-flight-result: failed' in text and 'Disconnected Holy Bolt replayed combat instead of harmless aging' in text
 assert sha(negative/'content/Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']==prior['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 assert (negative/'HolyBolts.cs').read_bytes()==historical('src/Sanctuary.Content/HolyBolts.cs')
 assert sha(negative/'HolyBoltFlightChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/HolyBoltFlightChecks.cs']
 # The negative inventory describes workspace inputs,not the substituted
 # parent build. Parent DLL and separately archived source prove identity.
 diagnostic=STAGE/'focused-diagnostic-3';di=json.loads((diagnostic/'birth-inputs.json').read_text());text=(diagnostic/'birth-native-checks.log').read_text()
 assert 'Natural Holy Bolt resume timeout:exists=False,lastFrame=49' in text and 'connected=True,candidate=True,fault=,lastSeed=666' in text
 match=re.search(r'lastCollider=(\d+),targetSerial=(\d+)',text);assert match and match[1]==match[2]
 ds=json.loads((diagnostic/'holy-bolt-flight-checks.json').read_text());assert ds['Generation']==1 and ds['Flight']['LastCollision']==int(match[1]) and ds['Flight']['Motion']['Seed']==666
 for name in ('HolyBoltFlights.cs','HolyBolts.cs'):assert sha(diagnostic/name)==di['sourceInputs']['src/Sanctuary.Content/'+name]
 assert sha(diagnostic/'HolyBoltFlightChecks.cs')==di['sourceInputs']['tests/Sanctuary.Integration/HolyBoltFlightChecks.cs']
 assert sha(diagnostic/'content/Sanctuary.Content.dll')==di['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 source=ROOT/'tools/reference/holy-bolt-hit-engine-evidence.json';assert source.read_bytes()==historical(str(source.relative_to(ROOT)))
 sr=json.loads(source.read_text());assert sr['counts']==dict(eligibility=768,healing=16,guards=4)
 for path,digest in sr['artifacts'].items():assert sha(ROOT/path)==digest,path
 hammer=ROOT/'tools/reference/blessed-hammer-engine-evidence.json';assert hammer.read_bytes()==historical(str(hammer.relative_to(ROOT)))
 hr=json.loads(hammer.read_text());hp=ROOT/'.local/class-skills/blessed-hammer-contact/source-probe'
 assert json.loads((hp/'hammer-source-probe.json').read_text())==hr['path']['probe']
 assert len(hr['path']['probe']['points'])==77 and len(hr['path']['probe']['velocities'])==120
 for path,digest in hr['sourceHashes'].items():assert sha(ROOT/path)==digest,path
 audit=json.loads((STAGE/'audit-inputs.json').read_text());assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
 text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and sum(markers(STAGE/'audit-native-checks.log').values())==4
 fingerprints=[]
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  value=json.loads((STAGE/name).read_text());assert value==json.loads((ROOT/'tools/reference'/name).read_text())
  assert value['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for path,digest in value['InputHashes'].items():assert sha(ROOT/path)==digest,path
  previous=json.loads(historical('tools/reference/'+name));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in value.items() if k not in ignore}=={k:v for k,v in previous.items() if k not in ignore};fingerprints.append(len(value['InputHashes']))
 assert fingerprints==[908,21]
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
 text=(STAGE/'catalogue-checks.log').read_text()
 for value in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert value in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
 files=[Path(__file__).resolve(),ROOT/'tests/check_holy_bolt_flight.py',parent,source,ROOT/'tools/reference/blessed-hammer-engine-evidence.json',ROOT/'.local/class-skills/blessed-hammer-contact/source-probe/reproduced.json',*(ROOT/'.local/class-skills/blessed-hammer-contact/source-probe'/n for n in ('hammer-source-probe.cpp','hammer-source-probe','hammer-source-probe.json')),ROOT/'tools/probe_blessed_hammer_reference.py',
  *(ROOT/'docs'/name for name in ('paladin-holy-bolt-flight-checkpoint.md','paladin-holy-bolt-flight-preflight.md','paladin-blessed-hammer-contact-preflight.md','class-skill-reuse-notes.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-effects.md','skill-icons.md')),
  *(STAGE/name for name in ('pre-build-4.json','pre-build-5.json','birth-inputs.json','birth-native-checks.log','full-run-5.log','holy-bolt-flight-checks.json','holy-bolt-damage-checks.json','holy-bolt-checks.json','heaven-fist-checks.json','audit-inputs.json','audit-native-checks.log','audit-run-5.log','python-tests.log','catalogue-checks.log','content-build-4.log','integration-build-4.log','content-build-5.log','integration-build-5.log')),
  *(focus/name for name in ('birth-inputs.json','birth-native-checks.log','focused-run-4.log','holy-bolt-flight-checks.json','holy-bolt-damage-checks.json','holy-bolt-checks.json','heaven-fist-checks.json','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(negative/name for name in ('birth-inputs.json','birth-native-checks.log','run.log','HolyBolts.cs','HolyBoltFlightChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(diagnostic/name for name in ('birth-inputs.json','birth-native-checks.log','holy-bolt-flight-checks.json','HolyBoltFlights.cs','HolyBolts.cs','HolyBoltFlightChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Holy Bolt saved source flight,per-projectile RNG,natural original resumes and absent-collider normalization',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=995,focusedMarkers=254,boots=3,reloads=2,saves=3,newServerWireActions=9,retainedServerWireActions=279,legacySourceCases=788,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  focusedBridge=dict(focusedBuild=4,fullBuild=5,identicalAssemblies=True,identicalCompiledSourceAndProjects=True,onlyCopiedDataChange=META,onlyChangedRow='paladin.holybolt',onlyChangedField='remaining'),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['New ordinary casts store optional v0Motion with FP8velocity,source seed and deep-copied subtile cells. BasicMissileUpdate,source stepping/integer vectors/Slow Missiles/footprints and native cell-wall services are shared. Ally-aware one-collider-per-update piercing excludes caster/last collider;beasts do not consume RNG. Original-rank/current hard Prayer heals1178FP256,then a later enemy takes4226FP256 with the next persisted seed.',
   'The parent DLL fails disconnected harmless-aging regression. Nine lifecycle/wall/slow/fault controls,terminal frame49/50,atomic bad source/velocity/tail and legacy omission pass. New source movement uses no global compatibility RNG. Death/respec/class cleanup and facet removal reuse existing actor services;loaded/offline periods age harmlessly. Snapshots freeze before background serialization.',
   'The same paid original moving flight survives two actual reloads and resumes naturally,with3738then4226FP256damage and evolved seed/path/frame,without another mana payment or state reset. Deleted receiver serial reuse is proved by preserved diagnostic3:frame49/connected/candidate/no fault,unchanged seed666and lastCollider=targetSerial. WorldLoad now clears only absent references before new spawns while preserving live references;the second original resume verifies that normalization.',
   'Focused4passes254markers;final5DLLs/source/projects exactly match it. The only copied-data difference is the reviewed Holy Bolt remaining-note annotation,proved explicitly by the bridge. Combined995markers,nine new/279retained wire actions,four audits/71Python/573named/240catalogue/generators pass.106installed hashes are unchanged. Historical vector/uncaptured/FoH compatibility and previous typed receiver scopes remain separately exercised.'],
  remaining=['Native room/sector/candidate ordering/footprints/height,initial dispatch/zero-point aim,global mixed-flight chronology/active lag catch-up,and modern executable seed/owner/facet/PvP/event/charged/oskill/item/client parity remain explicit. Harmless loaded/offline aging is an existing host policy,not a modern certificate.',
   'Current damage15and complete event/durability/legacy parity remain bounded. Related Sorceress/Necromancer/legacy last-collider references need their own reviewed native serial-reuse rollout;this fix does not silently alter those paths.',
   'Continue Paladin spell acceptance with Blessed Hammer,using shared typed magic/contact services while preserving its distinct curved path and Concentration rule. Final holy travel/healing/impact/expiry/audio and talent/hotkey art remain placeholders. No class completion,deployment or save reset.'])
 output=ROOT/'tools/reference/holy-bolt-flight-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('Holy Bolt flight:995combined/254focused markers,9new/279retained wire actions,2natural original reloads,4audits/71Python;106installed unchanged;no class completion.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
