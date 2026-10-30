#!/usr/bin/env python3
"""Accept complete older Revive sequencing with explicit approved expiry omission."""
import argparse,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_snapshot
from probe_warlock_runtime import ROOT,digest
STAGE=ROOT/'.local/class-skills/revive-source-policy'
def sha(p):return digest(p.read_bytes())
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');check_snapshot(STAGE/'pre-build-inputs.json',STAGE/'stats-inputs.json')
 _,prior=frozen_receipt('a04befd','tools/reference/revive-species-native-evidence.json');assert prior['verified']['passMarkers']==296
 artifacts=[Path(__file__).resolve()]
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==299
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/revive-species-rules/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
 assert sum(before.values())==296 and not before-after and after-before==Counter({'revive-source-policy-check':1,'revive-source-policy-reload-check':2})
 assert log.count('Revive source policy:8930complete older cast/start contexts,64owner contexts and8929approved no-expiry comparisons pass;in-place target,HP/level/seed,ordered services,failed placement and ignored mode/passive failures retained;full native actor/passives/AI/lifecycle remain gated')==3
 sourcePath=ROOT/'tools/reference/revive-source-policy-evidence.json';source=json.loads(sourcePath.read_text());artifacts.append(sourcePath)
 assert source['verified']==dict(castContexts=8930,ownerContexts=64,completeRoutines=4,sanitizers='ASan/UBSan exit0,empty stderr')
 assert not source['processMemoryRead'] and not source['handlerEnabled'] and not source['modernCodeEquivalenceClaimed']
 for n,h in source['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','a04befd:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573
 fingerprints=[len(a['InputHashes']) for a in audits];assert fingerprints==[766,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==184
 for name in ['class-skill-status.json','skill-effects.json','skill-icons.json','revive-policy.json','revive-species-rules.json']:
  p=ROOT/'data/Sanctuary/Balance'/name;assert p.read_bytes()==subprocess.check_output(['git','show','a04befd:'+str(p.relative_to(ROOT))],cwd=ROOT);artifacts.append(p)
 diagnostic=STAGE/'diagnostics/compile1';assert 'unused parameter' in (diagnostic/'reproduced-unused-parameter.log').read_text()
 artifacts += [p for p in diagnostic.iterdir() if p.is_file()]
 for name in ['pre-build-inputs.json','stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','source-run.log','source-recheck.log']:artifacts.append(STAGE/name)
 artifacts += [mainPath,ROOT/'tests/check_revive_source_policy.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/build_property_integration_backlog.py',ROOT/'tools/reference/item-property-integration-backlog.json']
 result=dict(referenceVersion='3.3.93847',component='Complete older Revive start/release/owner sequencing with approved no-expiration runtime lane',installed=False,handlerEnabled=False,reviveActorImplemented=False,modernCodeEquivalenceClaimed=False,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],processMemoryRead=False,
  verified=dict(passMarkers=299,retainedMarkers=296,worldBoots=3,actualWorldReloads=2,castStartContextsPerBoot=8930,ownerContextsPerBoot=64,approvedNoExpiryComparisonsPerBoot=8929,completeOlderRoutines=4,sourceSanitizersClean=True,preBuildAndNativeSourceDataMatch=True,pythonTests=71,unchangedNamedDefinitions=573,inputFingerprints=fingerprints,unchangedMainInstallationArtifacts=106,newFullClassRun=False),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='a04befd',receipt='tools/reference/revive-species-native-evidence.json'),
  established=['Four complete older routines execute unchanged under ASan/UBSan:start21,release58,owner conversion and the shared percentage helper. Native start/release results,original target mutation,HP/level,seed,flags,coordinates,right skill and ordered service traces match8930contexts per boot,plus64owner contexts including null owner/target and skill0. Source casting comparisons use valid nonnull casters and bounded signed arithmetic.',
   'The runtime entry requires skill95and the approved null-age policy. Across8929cases it retains the reference results and effects except unique-mod21and its MONUMOD expiration event. Positive,zero and negative source durations retain AI think+15,ownership,revive state,cap and AI/finalization effects. No lifetime timer is scheduled by this policy. Functional timers remain independent.',
   'Source behavior includes inclusive target-seeded FP256HP,lower-caster HP/level scaling with minimum1FPunit,off-class Paladin self-damage request,pet-type fallback,conditional right-skill clearing and continued passive/owner/registration/AI processing after no free room,mode failure or passive failure. No generic replacement actor is allocated. Existing SourcePercentage and SourceRandom supply shared numerical services.',
   'Geometry,neutral mode initialization/entry,base HP lookup,complete passive/stat/event effects,original AI/special abilities and cap registration remain explicit mandatory services. This acceptance does not claim an actual reconstructed Revive actor or modern executable equivalence. Source-only expiry is exposed only through explicitly named reference methods;the runtime entry always uses the approved deviation.',
   'The initial harness compilation rejected two unused parameters in the unchanged older start routine under -Werror. The original tool/source/trace and reproduced compiler diagnostic are preserved. Only unused-parameter warnings are explicitly disabled for source compilation;the reference bodies remain unchanged,and ASan/UBSan still require exit0/empty stderr. There was no native comparison failure.',
   'All296predecessor markers survive3boots/2reloads;clean builds,matching inventories,71Python tests,573unchanged named decisions and106unchanged installed artifacts pass. No new learned handler,actor persistence,full-class run or installation is claimed. No new D2R process access occurred.'],
  remaining=['Bind actual original-unit reconstruction,complete current passive/stat/sunder/event transfer,revived-AI permission and original special abilities. Historical GUID reservation,full corpse state/owner/room data and owner/death/replacement/facet/logout/respec/save lifecycle remain open. Source-invalid null-caster release,corrupt pointers and arithmetic outside the bounded source domain are unsupported.',
   'Prepared sequencing is not skill completion. Current source metadata and older control flow remain distinct from verified modern executable behavior. No class is complete;effects/audio and talent/hotkey icons remain separate placeholders.'])
 out=ROOT/'tools/reference/revive-source-policy-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Revive policy:299native/71Python;8930source casts+64owner+8929no-expiry comparisons per boot;full native actor/current passives/AI/lifecycle unfinished.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
