#!/usr/bin/env python3
"""Accept literal current mask profiles and their retained native composition."""
import argparse,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_snapshot
from probe_warlock_runtime import ROOT,digest
STAGE=ROOT/'.local/class-skills/generic-pet-state-mask'
def sha(p):return digest(p.read_bytes())
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');check_snapshot(STAGE/'pre-build-inputs.json',STAGE/'stats-inputs.json')
 _,prior=frozen_receipt('fe9c6f6','tools/reference/generic-pet-search-producers-native-evidence.json');assert prior['verified']['passMarkers']==278
 artifacts=[Path(__file__).resolve()]
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==281
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/generic-pet-search-producers/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
 assert sum(before.values())==278 and not before-after and after-before==Counter({'generic-pet-state-masks-check':1,'generic-pet-state-masks-reload-check':2})
 assert log.count('GenericPet state masks:2784current mask/corpse contexts,216full search compositions and9selector boundaries;all232states in3explicit profiles match;real world bindings,ordering and actors remain gated')==3
 assert log.count('GenericPet search producers:9058current producer contexts,28explicit boundaries and216full search compositions;current stat lookup/clamping,missile sizes and complete grid trace match;13200retained wall-follow paths share the stepper;actual mask/world/ordering/actor remain gated')==3
 proofs={}
 for name in ['generic-pet-state-mask-data-evidence.json','generic-pet-state-mask-source-evidence.json']:
  path=ROOT/'tools/reference'/name;r=json.loads(path.read_text());proofs[name]=r;artifacts.append(path)
  for n,h in r['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 source=proofs['generic-pet-state-mask-source-evidence.json'];data=proofs['generic-pet-state-mask-data-evidence.json']
 assert source['verified']==dict(maskCorpseContexts=2784,pipelineContexts=216,unsupported=0,selectors=3,stateCount=232)
 assert not source['processMemoryRead'] and source['sourceCodeExecutedInOfflineEmulator'] and not source['handlerEnabled']
 assert data['verified']['readBytes']==4208 and data['verified']['compiledTextUdeadMatches']==232
 assert data['verified']['selector1StateIds']==data['verified']['selector2StateIds']
 assert set(data['verified']['selector3StateIds'])-set(data['verified']['selector1StateIds'])=={207,227}
 lib=ROOT/source['emulatorLibrary']['path'];assert sha(lib)==source['emulatorLibrary']['sha256'];artifacts.append(lib)
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','fe9c6f6:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573
 fingerprints=[len(a['InputHashes']) for a in audits];assert fingerprints==[760,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==184
 for name in ['class-skill-status.json','skill-effects.json','skill-icons.json','generic-pet-skills.json','generic-pet-states.json']:
  p=ROOT/'data/Sanctuary/Balance'/name;assert p.read_bytes()==subprocess.check_output(['git','show','fe9c6f6:'+str(p.relative_to(ROOT))],cwd=ROOT);artifacts.append(p)
 for name in ['pre-build-inputs.json','stats-inputs.json','stats-native-checks.log','run.log','source-run.log','source-recheck.log','capture.log','python-tests.log']:artifacts.append(STAGE/name)
 artifacts += [mainPath,ROOT/'tests/check_generic_pet_state_masks.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/build_property_integration_backlog.py',ROOT/'tools/reference/item-property-integration-backlog.json']
 result=dict(referenceVersion='3.3.93847',component='Current raw mask410 profiles bound to corpse and full search',installed=False,handlerEnabled=False,fullAiVerified=False,realWorldRegistryRoomGraphAccepted=False,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=281,retainedMarkers=278,worldBoots=3,actualWorldReloads=2,maskCorpseContextsPerBoot=2784,fullSearchContextsPerBoot=216,sourceUnsupported=0,selectorBoundariesPerBoot=9,stateProfiles=3,stateCount=232,readOnlyReferenceBytes=4208,wholeRowSamples=24,compiledTextUdeadFields=232,preBuildAndNativeSourceDataMatch=True,pythonTests=71,unchangedNamedDefinitions=573,inputFingerprints=fingerprints,unchangedMainInstallationArtifacts=106,newFullClassRun=False),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='fe9c6f6',receipt='tools/reference/generic-pet-search-producers-native-evidence.json'),
  established=['Known static source slots yielded3literal mask profiles after readable-map,stable-identity,double-read,executable-anchor and24whole-owned-row checks. Only4208bytes were read;no actor/inventory reads,input,attach,security change or game memory write. The first attempt failed before creating a reader;its overly broad guard included an unrelated function. The corrected guard validates the exact referenced complete functions and every parent artifact hash.',
   'Selector3 matches all232compiled byte20/bit2 and owned text udead values. Selectors1/2 omit bind_demon207 and bind_demon_underling227. The catalog retains literal per-selector values instead of aliasing them or guessing a difficulty/game-mode mapping. Its mutable return buffers cannot alter the catalog or other views.',
   'Native state intersection and corpse eligibility match2784current-code cases per boot,including every state under active/inactive stat lists in all3profiles.216complete source search compositions per boot include ordinary and bound-demon corpses.9invalid/mismatched selector boundaries pass. All278predecessor checks are retained across3boots/2reloads.',
   'Clean builds,matching pre-build/native inventories,71Python tests,573unchanged named-item decisions and106unchanged installed artifacts pass. This binds another source producer but enables no new learned handler or complete actor. Earlier skill fixtures survive reloads;no new actor persistence or full-class run is claimed.'],
  remaining=['Actual Britannia room/grid/registry/ownership/stat-list bindings,selector meaning,general ordering4b7100,heap-backed caller storage,extended passives,NPC skills and complete facet/logout/death/respec/save lifecycle still gate learned demons. Snapshot pointer aliases do not prove universal aliases or table-generation rules.',
   'No class is complete. Effects/audio and talent/hotkey icons remain separately tracked placeholders. Continue with actual world bindings or unaffected roadmap mechanics rather than treating this kernel acceptance as skill completion.'])
 out=ROOT/'tools/reference/generic-pet-state-mask-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('State masks:281native/71Python;2784mask/corpse+216full search cases per boot;3literal profiles;world/ordering/actors unfinished.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
