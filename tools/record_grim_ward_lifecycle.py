#!/usr/bin/env python3
"""Bind prepared Grim Ward lifecycle and explicit remaining blockers to native checks."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/grim-ward-lifecycle'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
 _,prior=frozen_receipt('10e9c27','tools/reference/generic-pet-target-scoring-native-evidence.json');assert prior['verified']['passMarkers']==234
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==237
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 marker=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(marker,(ROOT/'.local/class-skills/generic-pet-target-scoring/stats-native-checks.log').read_text()));after=Counter(re.findall(marker,log))
 assert sum(before.values())==234 and not before-after and after-before==Counter({'grim-ward-lifecycle-check':1,'grim-ward-lifecycle-reload-check':2})
 assert log.count('Grim Ward lifecycle:3144complete legacy-source comparisons,167elapsed pulse values;placement factory/no-draw/no-select order,owner-evaluated child and missile-context curse dispatch pass;modern curse/terrain/clock and learned cast remain gated')==3
 sourcepath=ROOT/'tools/reference/grim-ward-lifecycle-source-evidence.json';source=json.loads(sourcepath.read_text());artifacts.append(sourcepath)
 assert source['verified']==dict(compiledWholeRoutines=6,lifecycleCases=3144,placementCases=1024,childCases=800,periodCases=1320,pulseValues=167,ownedStartFrames=[5,8,8],ownedBodyFrames=1000,ownedPeriodFrames=6,ownedDispatch=30)
 assert not source['currentExecutableParityVerified'] and not source['handlerEnabled'] and source['canonicalGeneratorCommit']=='10e9c27'
 for name,h in source['artifacts'].items():assert sha(ROOT/name)==h,name;artifacts.append(ROOT/name)
 assert (STAGE/'source-build.log').read_text()==''
 # Current known helper pages were unavailable in the restarted owned process.
 # Capture receipts read only pinned anchors;no code page was force-read.
 for name,targets in [('callees-live-search-core-20261029.json',{'0x4b3210','0x4b6dd0','0x4b42a0'}),('callees-live-formula-context-20261029.json',{'0x2bae90'})]:
  p=ROOT/'.local/class-skills/warlock-runtime-reference'/name;r=json.loads(p.read_text());artifacts.append(p)
  assert r['directCallTargetsVerified'] and r['bytesRead']==560 and {x['rva'] for x in r['records']}==targets
  assert all(not x['allUnwindFragmentsCaptured'] and all(not f['captured'] for f in x['fragments']) for x in r['records'])
  assert all(x['kind'] in ['header','sequence-table'] for x in r['reads'])
  assert not any(r[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
  for n,h in r['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 for p in (STAGE/'diagnostics').rglob('*'):
  if p.is_file():artifacts.append(p)
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text())
  assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','10e9c27:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
 assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[739,21]
 python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
 mainpath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainpath.read_text());assert len(main['installedArtifacts'])==106
 for name,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/name)==h,name
 statuspath=ROOT/'data/Sanctuary/Balance/class-skill-status.json';oldStatus=json.loads(subprocess.check_output(['git','show','10e9c27:data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));status=json.loads(statuspath.read_text())
 assert {k:v for k,v in oldStatus.items() if k!='skills'}=={k:v for k,v in status.items() if k!='skills'}
 changes=[n for o,n in zip(oldStatus['skills'],status['skills'],strict=True) if o!=n]
 assert len(changes)==1 and changes[0]['talentId']=='barbarian.grimward' and changes[0]['implementation']=='blocked' and not changes[0]['complete']
 assert sum(x['implementation']=='handler-present' for x in status['skills'])==183
 artifacts.append(statuspath)
 for name in ['skill-effects.json','skill-icons.json']:
  p=ROOT/'data/Sanctuary/Balance'/name;assert sha(p)==hashlib.sha256(subprocess.check_output(['git','show','10e9c27:'+str(p.relative_to(ROOT))],cwd=ROOT)).hexdigest();artifacts.append(p)
 artifacts += [STAGE/n for n in ['stats-inputs.json','stats-native-checks.log','run.log','source-run.log','source-recheck.log','python-tests.log']]
 artifacts += [p for p in (STAGE/'focused-fixtures').iterdir() if p.is_file()]
 artifacts += [ROOT/n for n in ['tools/record_grim_ward_lifecycle.py','tests/check_grim_ward_lifecycle.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]+[mainpath]
 result=dict(referenceVersion='3.3.93847',component='Prepared Grim Ward placement,start-child and periodic lifecycle',installed=False,handlerEnabled=False,currentExecutableParityVerified=False,modernCurseOwnerSynergyVerified=False,realWorldTerrainClockCastingAccepted=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=237,retainedMarkers=234,worldBoots=3,actualWorldReloads=2,legacyWholeRoutines=6,lifecycleCasesPerBoot=3144,elapsedPulseValues=167,ownerEvaluatedStartChild=True,bodyMissilePeriodicContextPreserved=True,allocationFailureStillReserves=True,knownGrimWardBlockerNowVisible=True,frozenPriorFullClassMarkers=1184,newFullClassRun=False,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[739,21],unchangedMainInstallationArtifacts=106),inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='10e9c27',receipt='tools/reference/generic-pet-target-scoring-native-evidence.json'),established=[
  'Six complete unchanged legacy source/frame routines are rebuilt from the canonical tracked generator in a fresh stage. Native comparisons match3144cases per boot:1024placement,800start-child and1320periodic,plus167elapsed pulse values. Current owned tables corroborate5/8/8start frames,1000body frames,period6and dispatch30;this is not current-executable parity.',
  'Placement preserves eligibility/room/free-coordinate/monster-size/row guards,large-before-small selection and allocation-before-no-draw/no-select/refresh order. Allocation failure still marks the corpse,as in the reviewed source. No native loot container is hidden/deleted by this kernel;that approved presentation adapter remains required.',
  'Start hit evaluates nonpositive range override against the original owner and clamps calculated duration to5;positive overrides remain direct. It retains child flags32769,original owner,skill/rank and position. Body tick uses elapsed total-minus-current before common collision;period matches dispatch with the body missile itself. A mandatory supplied curse service is not a substitute fear/slow/vulnerability implementation.',
  'Grim Ward was generically pending despite a known owner/hard-synergy/Terror-context ambiguity in its older preflight. The roadmap now shows this specific individual blocker. Source messages/indices and separate effects/icons remain preserved;no class or learned skill is enabled.',
  'The restarted pinned owned process exposes no readable executable fragments for four requested search/formula helpers. Two bounded captures read1120anchor bytes only. There is no forced read,input,attach,security change or memory write. Their unavailable bodies are recorded while independent lifecycle work proceeds. The absent historical ignored C++ file was reconstructed from the unchanged generator and complete routine hashes;the old fixture remains byte-for-byte equivalent in parsed output.',
  'All234prior native markers remain. Both builds have zero warnings/errors;70Python checks,573unchanged named eligibility decisions and106unchanged main installation hashes pass. Existing saved fixtures survive two actual reloads;this kernel introduces no actor persistence. Frozen417cd39retains1184full-class checks separately;no new full-suite run claimed. No native failure occurred.'
 ],remaining=[
  'Modern periodic curse formula owner/hard-Find-Potion synergy and special Terror target/creator context remain unresolved. Learned Grim Ward stays gated. Exact terrain/size/corpse/resource/input/world scheduling,child insertion/global-clock behavior,lifecycle persistence and retained-loot no-draw presentation still need dedicated adapters and acceptance.',
  'Target-search callback/selection and current formula pages remain unavailable in the restarted process;no mechanics are inferred. Continue cached source/other skills. The full eight-class objective remains active;effects/audio and talent/hotkey icons stay separate placeholders.'
 ])
 out=ROOT/'tools/reference/grim-ward-lifecycle-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Grim Ward lifecycle:237native/70Python;3144legacy-source cases per boot;modern curse and learned cast unfinished.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
