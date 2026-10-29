#!/usr/bin/env python3
"""Bind current movement policy and regenerated routing to native regression."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_build_snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-movement'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');check_build_snapshot(STAGE/'pre-build-inputs.json',STAGE/'stats-inputs.json');artifacts=[]
 _,prior=frozen_receipt('bd5bad9','tools/reference/find-potion-native-evidence.json');assert prior['verified']['passMarkers']==248
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==251
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/find-potion/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
 assert sum(before.values())==248 and not before-after and after-before==Counter({'generic-pet-movement-check':1,'generic-pet-movement-reload-check':2})
 assert log.count('GenericPet movement:6144complete current-code cases,2446unsupported source boundaries;preparation,four ordered approach attempts,mode payloads,RNG,event removal/50frame deadline and action bindings pass;world metadata/modes/clock/seed remain required')==3
 sourcePath=ROOT/'tools/reference/generic-pet-movement-source-evidence.json';source=json.loads(sourcePath.read_text());artifacts.append(sourcePath)
 assert source['verified']==dict(contexts=6144,retainedEarlierSourceResults=5760,byKind=dict(prepare=1980,think=144,execute=3200,approach=514,bound=286,**{'bound-range':20}),sourceFunctions=6,machineCodeBytes=1905,leafCodeBytes=174,leafPaddingBytes=2,fatalBoundaries=2446,realApproachCompositions=514,realDispositionApproachCompositions=286,realRangedPreparationThinkCompositions=20)
 for n,h in source['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 routingPath=ROOT/'tools/reference/skill-build-routing-evidence.json';routing=json.loads(routingPath.read_text());artifacts.append(routingPath)
 assert len(routing['frozenSourceCorpseTargetIds'])==6 and len(routing['acceptedFrozenDllCorpseTargetIds'])==7
 assert routing['currentDllCorpseTargetIds']==routing['pinnedSourceCorpseTargetIds'] and len(routing['currentDllCorpseTargetIds'])==9
 for n,h in routing['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 captures=[]
 for name in ['callees-live-approach-core-20261029.json','callees-live-search-preparation-20261029.json','callees-live-movement-core-20261029.json','movement-prepare-leaf-20261029.json','movement-prepare-leaf-complete-20261029.json']:
  p=ROOT/'.local/class-skills/warlock-runtime-reference'/name;r=json.loads(p.read_text());artifacts.append(p)
  assert r['bytesRead']<=8192 and not any(r.get(k,False) for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
  for n,h in r['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
  captures.append(r)
 assert sum(r['bytesRead'] for r in captures)==6782 and captures[3]['completeFunctionVerified']==False
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','bd5bad9:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[742,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 for n in ['class-skill-status.json','skill-effects.json','skill-icons.json']:
  p=ROOT/'data/Sanctuary/Balance'/n;assert p.read_bytes()==subprocess.check_output(['git','show','bd5bad9:'+str(p.relative_to(ROOT))],cwd=ROOT);artifacts.append(p)
 barbPath=ROOT/'tools/reference/barbarian-current-callback-evidence.json';barb=json.loads(barbPath.read_text());assert not barb['processMemoryRead'] and not barb['dispatcherIndexLinkVerified'] and not barb['callbackSemanticsVerified']
 for n,h in barb['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 artifacts += [barbPath,mainPath]+[STAGE/n for n in ['pre-build-inputs.json','stats-inputs.json','stats-native-checks.log','run.log','source-run.log','python-tests.log','routing-proof.log','corpse-routing-tests.log']]
 artifacts += [p for p in (STAGE/'diagnostics').rglob('*') if p.is_file()]
 artifacts += [ROOT/n for n in ['tools/record_generic_pet_movement.py','tools/snapshot_skill_build.py','tests/check_generic_pet_movement.py','tests/test_class_skills.py','tools/build_class_skills.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]
 result=dict(referenceVersion='3.3.93847',component='Current demon movement/approach policy,action bindings and generated corpse routing correction',installed=False,handlerEnabled=False,completeAiVerified=False,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=251,retainedMarkers=248,worldBoots=3,actualWorldReloads=2,currentCodeContextsPerBoot=6144,retainedEarlierSourceResults=5760,sourceFunctions=6,currentCodeBytes=1905,leafPaddingBytes=2,approachCompositions=514,actionApproachCompositions=286,rangedPreparationThinkCompositions=20,unsupportedSourceBoundaries=2446,pythonTests=71,compiledPinnedCorpseFlags=9,preBuildAndNativeSourceDataMatch=True,unchangedNamedDefinitions=573,inputFingerprints=[742,21],unchangedMainInstallationArtifacts=106,newFullClassRun=False,frozenPriorFullClassMarkers=1184),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='bd5bad9',receipt='tools/reference/find-potion-native-evidence.json'),
  established=['Six complete captured current bodies match6144cases each boot:preparation,think-event maintenance,target mode wrapper,approach-core/four ordered retries and existing disposition. Source/native agree on raw controls,cap77,option1to7,path byte normalization,mode initialization payload preservation,integer results,failure event removal/AI flag,RNG and signed50frame deadlines. Actor slot88 is independent of path slot38;null/missing data is explicitly unsupported.',
   'Movement binds existing action approach,preparation and finish services,sharing the supplied seed and queue identities. Real source compositions execute514full approaches,286disposition/approach paths and20ranged preparation/finish paths. Actual mode effects/pathfinding,table values and world event/clock/seed owners remain explicit services. This adds no learned demon actor.',
   'The first port conflated independent slots38and88;the native comparison exposed it. The corrected oracle retains all5760earlier results and expands to6144with independent-slot controls. Failed source/native logs,input inventories,code snapshots and both DLLs remain preserved. No expectations were weakened.',
   'Fresh rebuilding exposed an older generated-code mismatch:the accepted Find Potion DLL contained7corpse flags while its regenerated source/template retained6. Independent reflection confirms both old artifacts. The generator now derives9flags from pinned data;fresh source and DLL match. Disabled Grim Ward/Engorge metadata does not enable their handlers. An independent Python regression and pre-build/post-build/native source/data snapshots guard recurrence. Historical staged behavior remains evidence;the old post-build inventory alone did not prove compiler-source identity.',
   'Five bounded read-only captures total6782bytes. Current movement dependencies became readable;the complete leaf has174code bytes and2padding bytes,with a prior64byte incomplete sample preserved. A further846byte search-preparation helper is cached without mechanical acceptance. Candidate Barbarian start-table bounds are corroborated from the pinned file;its selected pages are protected,so dispatcher/index/semantics remain unverified and no skill is inferred.',
   'All248predecessor markers retained;both clean builds zero warnings/errors;71Python checks;573named decisions and106installed artifacts unchanged. Existing world fixtures survive two actual reloads. This detached policy has no actor persistence claim. Frozen417cd39retains1184full-class checks separately;no new full-class run or installation claimed.'],
  remaining=['Real NPC metadata for distinct slots38/88,movement/AI controls and global table field;mode initialization/execution,pathfinding,event queue/native clock and shared persistent actor RNG still need binding. Current target search callback/sorter,ordering,follow executor,dispatcher/owner/proc/passive and learned demon lifecycle remain incomplete.',
   'Barbarian current start67/68and modern Leap Attack remain individual source gaps;candidate table metadata is not a callback certificate. Continue cached search preparation/follow/world adapters and unaffected roadmap skills. Effects/audio and talent/hotkey icons remain separately identified placeholders. No class is complete.'])
 out=ROOT/'tools/reference/generic-pet-movement-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Current movement:251native/71Python;6144current-code cases;9compiled/pinned corpse flags;184handlers,no class complete.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
