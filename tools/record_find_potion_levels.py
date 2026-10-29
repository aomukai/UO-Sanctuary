#!/usr/bin/env python3
"""Bind the approved corpse-level potion bands to real casts and saved levels."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_build_snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/find-potion-levels'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');check_build_snapshot(STAGE/'pre-build-inputs.json',STAGE/'stats-inputs.json');artifacts=[]
 _,prior=frozen_receipt('2ddc0a7','tools/reference/generic-pet-movement-native-evidence.json');assert prior['verified']['passMarkers']==251
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==251
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/generic-pet-movement/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log));assert before==after and sum(after.values())==251
 assert log.count('Find Potion monster tiers:144real boundary/facet/type casts;recorded death levels,legacy fallback/conflict,low-level owner,no area tags,payment,cancel,loot and pending-save fixtures pass')==1
 assert log.count('Find Potion monster-tier reload:recorded levels,corpse flags,potion identity,loot,fractional mana and no transient cast replay pass')==2
 assert log.count('Find Potion policy:5004complete legacy-source cases;10existing potion codes;generic fallback explicit;modern RNG/curve parity remains open')==3
 bandsPath=ROOT/'data/Sanctuary/Balance/find-potion-level-bands.json';bands=json.loads(bandsPath.read_text());assert bands==[dict(minLevel=1,healingTier=2,manaTier=2,rejuvenationTier=1),dict(minLevel=20,healingTier=3,manaTier=3,rejuvenationTier=1),dict(minLevel=40,healingTier=4,manaTier=4,rejuvenationTier=2),dict(minLevel=60,healingTier=5,manaTier=5,rejuvenationTier=2)]
 assert not (ROOT/'data/Sanctuary/Balance/region-acts.json').exists()
 code=(ROOT/'src/Sanctuary.Content/BarbarianFindPotion.cs').read_text();assert 'PotionAreas' not in code and 'TryMonsterLevel' in code and 'PotionLevelBands.For(level)' in code
 sourcePath=ROOT/'tools/reference/find-potion-native-source-evidence.json';source=json.loads(sourcePath.read_text());artifacts.append(sourcePath)
 for n,h in source['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 policyPath=ROOT/'tools/reference/find-potion-level-policy.json';policy=json.loads(policyPath.read_text());assert policy['approvedDate']=='2026-10-30' and policy['supersedes']=='tools/reference/find-potion-area-policy.json';artifacts += [policyPath,bandsPath]
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','2ddc0a7:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[742,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 oldStatus=json.loads(subprocess.check_output(['git','show','2ddc0a7:data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));statusPath=ROOT/'data/Sanctuary/Balance/class-skill-status.json';status=json.loads(statusPath.read_text())
 changes=[b for a,b in zip(oldStatus['skills'],status['skills'],strict=True) if a!=b];assert len(changes)==1 and changes[0]['talentId']=='barbarian.findpotion' and not changes[0]['complete']
 assert sum(s['implementation']=='handler-present' for s in status['skills'])==184
 for name in ['skill-effects.json','skill-icons.json']:
  p=ROOT/'data/Sanctuary/Balance'/name;assert p.read_bytes()==subprocess.check_output(['git','show','2ddc0a7:'+str(p.relative_to(ROOT))],cwd=ROOT);artifacts.append(p)
 artifacts += [statusPath,mainPath]+[STAGE/n for n in ['pre-build-inputs.json','stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','find-potion-fixtures.json']]
 artifacts += [ROOT/n for n in ['tools/record_find_potion_levels.py','tests/check_find_potion_levels.py','tools/build_skill_roadmap.py','tools/snapshot_skill_build.py','tools/reference/barbarian-find-potion-preflight.json','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]
 result=dict(referenceVersion='3.3.93847',component='Approved Find Potion corpse-monster-level potion strength',approvedDeviation=True,installed=False,handlerEnabled=True,currentExecutableParityVerified=False,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=251,retainedMarkerNames=251,findPotionTierTestsUpdatedForApprovedDeviation=True,worldBoots=3,actualWorldReloads=2,legacySourceCasesPerBoot=5004,realBoundaryFacetTypeCasts=144,monsterBoundaryLevels=[1,19,20,39,40,59,60,99],naturalConnectedCasts=3,pythonTests=71,preBuildAndNativeSourceDataMatch=True,unchangedNamedDefinitions=573,inputFingerprints=[742,21],unchangedMainInstallationArtifacts=106,newFullClassRun=False),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='2ddc0a7',receipt='tools/reference/generic-pet-movement-native-evidence.json'),
  established=['User-approved monster-level policy supersedes the earlier Act/facet rule:1–19light,20–39regular,40–59greater,60+super;full rejuvenation from40. Character level and caster map/facet do not select strength. Region tagging and the empty region-acts configuration are removed from the runtime path.',
   'Corpse metadata adds optional MonsterLevel without changing persistence ID122/version0. Death capture records the existing effective CreatureProfiles level. Missing old fields remain null;consistent preexisting BaseLife/Treasure levels are compatible. Missing or conflicting old context refuses before payment/reservation. No current player/owner/species-level reconstruction is used.',
   'Mana2,successful-start payment/no refund,single attempt on success or failure,existing loot/appearance and type odds remain. The reference15row legacy policy remains independently compared against5004source cases each boot;custom level bands are expressly a Sanctuary deviation,not asserted as D2R source behavior.',
   '144real corpse casts cover8boundary levels*3caster facets*6type boundaries using actual scoped creature profiles and production death capture,then restore profiles. A level2Barbarian finding a level60corpse receives super potions. Unknown/conflicting legacy data,old snapshot fallback,insufficient mana,cancellation,release invalidation,repeat attempts and retained loot pass. Three connected natural casts and both actual reloads preserve recorded levels60/40,potion identity,corpse flags,loot,fractional mana and no transient cast replay.',
   'All251existing marker names are retained;Find Potion tier assertions are intentionally replaced to cover the new approved policy. Existing unrelated components still pass. Both clean builds zero warnings/errors,71Python tests,573unchanged named decisions and106unchanged main installation artifacts. Source/data snapshots match native inputs. No new full-class run or installation claimed.'],
  remaining=['Exact modern chance rounding/shared owner RNG,h2h/auto-approach,current SC/FCR/input/client and unsupported item/target paths remain open. Shared corpse access remains an explicit current native adapter. Missing final effects/audio and talent/hotkey icons stay separately listed placeholders.',
   'Resume the saved unverified search-state draft and class roadmap after this policy change. Full demon/world integration and other individual source gaps remain;no class is complete.'])
 out=ROOT/'tools/reference/find-potion-level-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Find Potion corpse-level policy:251native/71Python;144real boundary/facet/type casts;3natural casts/2reloads;installed runtime unchanged.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
