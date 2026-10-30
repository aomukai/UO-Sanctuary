#!/usr/bin/env python3
"""Accept historical corpse identity without certifying a complete Revive actor."""
import argparse,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_snapshot
from probe_warlock_runtime import ROOT,digest
STAGE=ROOT/'.local/class-skills/source-corpse-identity'
def sha(p):return digest(p.read_bytes())
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');check_snapshot(STAGE/'pre-build-inputs.json',STAGE/'stats-inputs.json')
 _,prior=frozen_receipt('1738620','tools/reference/generic-pet-world-identity-native-evidence.json');assert prior['verified']['passMarkers']==284
 artifacts=[Path(__file__).resolve()]
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==293
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/generic-pet-world-identity/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
 assert sum(before.values())==284 and not before-after and after-before==Counter({'source-corpse-identity-check':1,'source-corpse-identity-reload-check':2,'corpse-base-check':1,'corpse-base-reload-check':2,'corpse-base-result':3})
 assert log.count('Source corpse identity:751species/1502case-folded IDs,original monster GUID versus item serial,ownerless registry lookup,legacy/unmapped/ambiguous boundaries,reservation/state-independent identity,index replacement/deletion and unchanged-v0 rebuild pass;Revive combat/special abilities and full corpse state projection remain gated')==3
 assert log.count('Corpse base:52100 native packaged/source comparisons passed')==3
 assert log.count('GenericPet world identity:36live role/context lookups,actual player/control/summon ownership,shared search/traversal binding,stale/deleted/equipped target refresh and explicit unsupported registries pass;native geometry,corpse identities,stat lists,ordering and complete actors remain gated')==3
 reference=STAGE/'native-reference.json';ref=json.loads(reference.read_text());assert not ref['compilerSourceIdentityClaimed']
 for n,h in ref['files'].items():assert sha(Path(n))==h,n
 artifacts.append(reference)
 fixture=STAGE/'source-corpse-identity-fixtures.json';saved=json.loads(fixture.read_text());assert len(saved['Rows'])==len(saved['Loot'])==2
 ids=[r['BaseLife']['OriginalMonsterSerial'] for r in saved['Rows']];assert len(set(ids))==2 and all(r['Corpse']!=r['BaseLife']['OriginalMonsterSerial'] for r in saved['Rows'])
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','1738620:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573
 fingerprints=[len(a['InputHashes']) for a in audits];assert fingerprints==[762,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==184
 for name in ['class-skill-status.json','skill-effects.json','skill-icons.json','generic-pet-search-monsters.json','corpse-base-monsters.json','corpse-source-rules.json']:
  p=ROOT/'data/Sanctuary/Balance'/name;assert p.read_bytes()==subprocess.check_output(['git','show','1738620:'+str(p.relative_to(ROOT))],cwd=ROOT);artifacts.append(p)
 diagnostics=STAGE/'diagnostics';assert 'Orphan fixture still has native owner' in (diagnostics/'attempt1/stats-native-checks.log').read_text()
 abort=json.loads((diagnostics/'attempt2-audit-path/reason.json').read_text());assert abort['exitCode']==130 and abort['signal']=='SIGINT'
 assert 'source-corpse-identity-check: passed' in (diagnostics/'attempt2-audit-path/stats-native-checks.log').read_text()
 artifacts += [p for p in diagnostics.rglob('*') if p.is_file()]
 for name in ['pre-build-inputs.json','stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','corpse-base-fixtures.json']:artifacts.append(STAGE/name)
 artifacts += [fixture,mainPath,ROOT/'tests/check_source_corpse_identity.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/build_property_integration_backlog.py',ROOT/'tools/reference/item-property-integration-backlog.json']
 result=dict(referenceVersion='3.3.93847',component='Historical original monster GUID/species and indexed corpse registry projection',installed=False,handlerEnabled=False,reviveActorImplemented=False,sourceGuidReservationVerified=False,fullCorpseStateProjectionAccepted=False,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],processMemoryRead=False,
  verified=dict(passMarkers=293,retainedMarkers=284,worldBoots=3,actualWorldReloads=2,sourceSpecies=751,caseFoldedSpeciesIdQueriesPerBoot=1502,retainedCorpseBaseSourceCasesPerBoot=52100,persistenceVersion=0,newPersistenceFields=0,savedOrphanCorpses=2,preBuildAndNativeSourceDataMatch=True,pythonTests=71,unchangedNamedDefinitions=573,inputFingerprints=fingerprints,unchangedMainInstallationArtifacts=106,newFullClassRun=False),
  nativeReference=ref,inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='1738620',receipt='tools/reference/generic-pet-world-identity-native-evidence.json'),
  established=['Existing corpse records already save original monster serial and species. A reverse index now resolves those GUIDs without scanning every corpse or substituting the native corpse Item serial. Store/replacement/removal and v0deserialization maintain the index;the index itself is not serialized. Current primary metadata supplies751species IDs with case-insensitive lookup.',
   'The actual source registry can resolve known historical monster GUIDs after the original native owner is deleted and after both reloads. Reservations,shattering and NoSelect do not erase identity;eligibility/state membership remain separate producers. Missing legacy snapshots,unmapped species,conflicting owner references,live-actor identity collisions and multiple live corpses with one GUID are explicit unsupported boundaries.',
   'Two real death/corpse fixtures retain original GUID,species,full historical rows and original loot through both reloads. Replacement removes stale index keys,deletion removes entries,returned arrays do not expose index storage,and worker serialization/v0deserialization rebuilds the index. Existing52100corpse-base source cases and legacy optional-field/validation/reload controls pass every boot.',
   'The first fixture incorrectly assumed deleting a native owner immediately nulls its corpse reference. Deleted references can remain before reload;the corrected test distinguishes a deleted object from a live owner and verifies actual null owner after load. The failed run,snapshots,sources and DLLs are preserved. A second run passed the new checks but was stopped for an incorrect audit export path;its log/reason are preserved,with no source/DLL changes before the corrected final run.',
   'All284predecessor markers are retained,plus3new identity and6explicit corpse-base markers,across3boots/2reloads. Clean builds,matching input snapshots,71Python tests,573unchanged named decisions and106unchanged installed artifacts pass. No new learned handler,complete Revive actor,full-class run or installation is claimed. No new D2R process access was needed.'],
  remaining=['Historical source GUID reservation against native serial reuse across arbitrary restart/allocation is not verified. The inspected allocator source initializes from live loaded keys;its compiler/source identity is not claimed. The projection refuses collisions rather than resolving them to the wrong actor. Native corpse identity is not complete actor/target-cache lifecycle acceptance.',
   'Revive still needs original combat/special abilities,excluded species,casting,180second lifetime and full owner/facet/logout/death/respec/save lifecycle. Corpse state/stat/owner data and actual room/grid membership still require producers;the existing actor-only ownership binder does not invent corpse AI data. General demon ordering,extended passives and NPC skills remain separate.',
   'No class is complete. Effects/audio and talent/hotkey icons remain separate placeholders;current handler count stays184.'])
 out=ROOT/'tools/reference/source-corpse-identity-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Corpse identity:293native/71Python;751species,2orphan corpses,2reloads,v0unchanged;GUID reservation and full Revive/state/actor behavior unfinished.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
