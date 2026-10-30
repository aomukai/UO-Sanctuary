#!/usr/bin/env python3
"""Accept current species permission and the approved no-expiration policy."""
import argparse,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_snapshot
from probe_warlock_runtime import ROOT,digest
STAGE=ROOT/'.local/class-skills/revive-species-rules'
def sha(p):return digest(p.read_bytes())
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');check_snapshot(STAGE/'pre-build-inputs.json',STAGE/'stats-inputs.json')
 _,prior=frozen_receipt('bb357b2','tools/reference/source-corpse-identity-native-evidence.json');assert prior['verified']['passMarkers']==293
 artifacts=[Path(__file__).resolve()]
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==296
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/source-corpse-identity/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
 assert sum(before.values())==293 and not before-after and after-before==Counter({'revive-species-check':1,'revive-species-reload-check':2})
 assert log.count('Revive species:623compiled secondary flags,391species permissions,751primary links,6missing-row boundaries and2saved original corpses match;approved no-expiration metadata loaded;full eligibility,in-place actor and lifecycle remain gated')==3
 dataPath=ROOT/'tools/reference/revive-species-data-evidence.json';data=json.loads(dataPath.read_text());artifacts.append(dataPath)
 assert data['verified']==dict(secondaryRows=623,secondaryStride=296,compiledTextReviveMatches=623,allowedSecondaryRows=391,reviveFlagByte=5,reviveFlagMask=1,completeLegacyRoutines=3)
 assert not data['processMemoryRead'] and not data['legacyBodiesExecuted'] and not data['modernCodeEquivalenceClaimed']
 assert data['pinnedSkill']['durationFrames']==4500 and data['sanctuaryLifetimePolicy']==dict(maximumAgeFrames=None,removeOnReviveDeath=True,removeOnNecromancerDeath=True,removeOnReplacement=True)
 for n,h in data['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','bb357b2:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573
 fingerprints=[len(a['InputHashes']) for a in audits];assert fingerprints==[765,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 statusPath=ROOT/'data/Sanctuary/Balance/class-skill-status.json';status=json.loads(statusPath.read_text());oldStatus=json.loads(subprocess.check_output(['git','show','bb357b2:data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT))
 assert sum(s['implementation']=='handler-present' for s in status['skills'])==184
 for current,previous in zip(status['skills'],oldStatus['skills'],strict=True):
  if current['talentId']=='necromancer.revive':
   a=dict(current);b=dict(previous);assert 'no expiration timer' in a.pop('remaining');b.pop('remaining');assert a==b
  else:assert current==previous
 for name in ['skill-effects.json','skill-icons.json','generic-pet-search-monster2.json','generic-pet-search-monster-links.json']:
  p=ROOT/'data/Sanctuary/Balance'/name;assert p.read_bytes()==subprocess.check_output(['git','show','bb357b2:'+str(p.relative_to(ROOT))],cwd=ROOT);artifacts.append(p)
 for name in ['pre-build-inputs.json','stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','source-corpse-identity-fixtures.json']:artifacts.append(STAGE/name)
 artifacts += [statusPath,mainPath,ROOT/'tests/check_revive_species_rules.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/build_property_integration_backlog.py',ROOT/'tools/reference/item-property-integration-backlog.json']
 result=dict(referenceVersion='3.3.93847',component='Revive species permission and approved no-expiration lifetime metadata',installed=False,handlerEnabled=False,reviveActorImplemented=False,fullEligibilityAccepted=False,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],processMemoryRead=False,
  verified=dict(passMarkers=296,retainedMarkers=293,worldBoots=3,actualWorldReloads=2,secondarySpeciesRows=623,allowedSecondaryRows=391,primaryClassLinks=751,explicitMissingRowCasesPerBoot=6,savedOriginalCorpseChecksPerBoot=2,compiledTextReviveMatches=623,completeLegacyRoutineReferences=3,legacyRoutinesExecuted=False,preBuildAndNativeSourceDataMatch=True,pythonTests=71,unchangedNamedDefinitions=573,inputFingerprints=fingerprints,unchangedMainInstallationArtifacts=106,newFullClassRun=False),
  approvedLifetimePolicy=data['sanctuaryLifetimePolicy'],inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='bb357b2',receipt='tools/reference/source-corpse-identity-native-evidence.json'),
  established=['All623current compiled byte5/bit0 fields match owned text revive flags.391secondary rows allow the species gate. The immutable catalog reuses existing compiled primary/secondary links,and native checks cover all751primary classes,all623rules,missing indices and actual saved original corpse metadata every boot. Species permission does not imply full consumability or AI permission.',
   'The user explicitly removed Revive expiration. Runtime policy has no maximum age and retains removal on revive death,Necromancer death and replacement. This overrides the unchanged4500frame/180second source reference. No future actor may schedule that lifetime expiration;AI/combat/status timers are independent. Current checks certify policy loading,not a complete live Revive lifecycle.',
   'Three complete older routines are hash-pinned and analyzed,start21,release58and owner conversion. The release modifies its existing target,rolls inclusive target-seeded base HP,scales HP/level for a lower caster and continues after failed free-room placement. The owner helper sets ownership/revive state,think+15and positive-duration expiry;the approved lifetime overrides only expiry. These bodies were not executed here,and modern executable equivalence is not claimed.',
   'All293predecessor markers,including corpse identity,old save compatibility,live registry and source controls,are retained across3boots/2reloads. Clean builds,matching inventories,71Python tests,573unchanged named decisions and106unchanged installed artifacts pass. No new handler,complete actor,full-class run or installation is claimed. No native comparison failed in this increment.'],
  remaining=['Full start/release/owner policy,native in-place reconstruction,preserved original combat/special abilities,revived-AI permission,complete corpse state/stat/owner producers,GUID reservation and owner/death/replacement/facet/logout/respec/save lifecycle remain required. The old180second deadline is no longer an implementation requirement.',
   'The policy/catalog change does not create an active Revive actor or prove lifetime behavior over elapsed time. Current source tables remain pinned unchanged;approved Sanctuary deviations take precedence. Effects/audio and talent/hotkey icons remain separate placeholders;no class is complete.'])
 out=ROOT/'tools/reference/revive-species-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Revive rules:296native/71Python;623species/751links and approved no-expiration metadata;full actor/lifecycle unfinished.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
