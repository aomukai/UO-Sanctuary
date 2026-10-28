#!/usr/bin/env python3
"""Bind qualified NPC packet/self-state/health/Stun/deferred-death composition."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/monster-ordered-release'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False,full_class=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
 _,previousReceipt=frozen_receipt('a60c0fc','tools/reference/goatman-stun-recipient-native-evidence.json')
 assert previousReceipt['verified']['passMarkers']==185 and previousReceipt['frozenPredecessor']['fullClassMarkers']==1184
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==190
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 previous=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',(ROOT/'.local/class-skills/goatman-stun-recipient/stats-native-checks.log').read_text()));current=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',log))
 assert sum(previous.values())==185 and current-previous==Counter({'monster-ordered-release-check':1,'monster-ordered-release-reload-check':2,'monster-ordered-release-health-stage-check':2}) and not previous-current
 marker='Ordered monster release:1616native complete-source compositions/848FP health writes/50native deaths/24transient lethal stuns;exact event order and once-only death,full actor/services still pending'
 assert log.count(marker)==1 and log.count('Ordered monster release:saved post-release FP health/stun resumes without replay or double subtraction')==2
 p=ROOT/'tools/reference/goatman-npc-release-source-evidence.json';source=json.loads(p.read_text());artifacts.append(p)
 assert (source['contexts'],source['zeroDamageStuns'],source['lethalHealthFieldStuns'],source['shortenedExistingStuns'])==(3840,72,24,72) and not source['currentExecutableParity']
 for group in ['inputs','artifacts']:
  for n,h in source[group].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 assert not (ROOT/'.local/class-skills/goatman-npc-release/sanitizer.log').read_bytes()
 saved=json.loads((STAGE/'focused-fixtures/monster-ordered-release-fixtures.json').read_text());assert saved['Target']==saved['Stun']['Target'] and saved['Stun']['UntilUtc'].endswith('Z') and saved['Life256']==99984 and saved['MaximumLife']*256>=100001 and saved['BuffedLife256']<saved['BuffedMaximum']*256 and saved['Buff']['Recipient']==saved['Buffed']
 full=None
 if full_class:
  full=inventory.validate_inventory(STAGE/'class-inputs.json');assert full['artifacts']==inputs['artifacts'] and full['copiedData']==inputs['copiedData']
  fullLog=(STAGE/'class-native-checks.log').read_text();assert ': failed' not in fullLog and ' ERR]' not in fullLog and fullLog.count('Shutting down')==3
  assert fullLog.count(': passed')==1184 and fullLog.count('Writing world save snapshot done')==35
  assert 'including two save/reload cycles.' in (STAGE/'full-run.log').read_text()
  for n in ['class-inputs.json','class-native-checks.log','full-run.log']:artifacts.append(STAGE/n)
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text())
  assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  if full_class:
   pfull=STAGE/('full-'+name);assert a==json.loads(pfull.read_text());artifacts.append(pfull)
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','a60c0fc:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions'] and [len(a['InputHashes']) for a in audits]==[716,21]
 py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
 main=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==183 and status['completedClasses']==[]
 for n in ['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log']:artifacts.append(STAGE/n)
 artifacts += list((STAGE/'focused-fixtures').glob('*fixtures.json'))+list((STAGE/'diagnostics').iterdir())
 assert 'Released fractional HP did not persist exactly once' in (STAGE/'diagnostics/initial-stats-native-checks.log').read_text()
 initial=json.loads((STAGE/'diagnostics/initial-stats-inputs.json').read_text())
 assert initial['artifacts']['Assemblies/Sanctuary.Integration.dll']==sha(STAGE/'diagnostics/initial-integration.dll')
 assert initial['sourceInputs']['tests/Sanctuary.Integration/MonsterOrderedReleaseChecks.cs']==sha(STAGE/'diagnostics/initial-fixture.cs')
 assert log.count('monster-ordered-release-health-stage-check: passed')==2
 held=json.loads((STAGE/'diagnostics/held-rate-stats-inputs.json').read_text())
 assert held['artifacts']['Assemblies/Sanctuary.Integration.dll']==sha(STAGE/'diagnostics/held-rate-integration.dll')
 assert held['sourceInputs']['tests/Sanctuary.Integration/MonsterOrderedReleaseChecks.cs']==sha(STAGE/'diagnostics/held-rate-fixture.cs')
 assert 'life=8,balance=1936,fraction=112,expected=99984' in (STAGE/'diagnostics/held-rate-stats-native-checks.log').read_text()
 genuine=json.loads((STAGE/'diagnostics/genuine-max-stats-inputs.json').read_text())
 assert genuine['artifacts']['Assemblies/Sanctuary.Integration.dll']==sha(STAGE/'diagnostics/genuine-max-integration.dll')
 assert genuine['artifacts']['Assemblies/Sanctuary.Content.dll']==sha(STAGE/'diagnostics/initial-content.dll')
 assert genuine['sourceInputs']['tests/Sanctuary.Integration/MonsterOrderedReleaseChecks.cs']==sha(STAGE/'diagnostics/genuine-max-fixture.cs')
 assert genuine['sourceInputs']['src/Sanctuary.Content/CreatureProfiles.cs']==sha(STAGE/'diagnostics/initial-creature-profiles.cs')
 assert genuine['sourceInputs']['src/Sanctuary.Content/EncounterCreature.cs']==sha(STAGE/'diagnostics/initial-encounter-creature.cs')
 assert 'life=8/1364,expectedMax=1364,balance=1936,fraction=112,expected=99984' in (STAGE/'diagnostics/genuine-max-stats-native-checks.log').read_text()
 for n in ['tools/record_monster_ordered_release.py','tests/check_monster_ordered_release.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json','tools/reference/creeper-frame-installation-evidence.json']:artifacts.append(ROOT/n)
 result=dict(referenceVersion='3.3.93847',component='Qualified NPC packet/self-state/health/Stun/deferred death release',installed=False,handlerEnabled=False,currentExecutableParity=False,fullActorReleaseAccepted=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=190,retainedMarkers=185,worldBoots=3,actualWorldReloads=2,qualifiedCompleteSourceCompositions=1616,sourceMatrixContexts=3840,fixedPointHealthWrites=848,nativeDeaths=50,transientLethalStuns=24,healedCachedDeathControls=1,duplicateDeathAndEscapedCallbackGuards=True,loadedEncounterBodies=2,savedLife256=99984,savedMaximumLife=saved['MaximumLife'],savedBuffedLife256=saved['BuffedLife256'],savedBuffedMaximum=saved['BuffedMaximum'],savedLifeRemainder256=112,fullClassMarkers=1184 if full_class else None,fullClassBoots=3 if full_class else None,fullClassReloads=2 if full_class else None,fullClassSaveSnapshots=35 if full_class else None,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[716,21],unchangedMainInstallationArtifacts=106),inputInventory=inputs,fullInputInventory=full,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='a60c0fc',receipt='tools/reference/goatman-stun-recipient-native-evidence.json'),
  established=['Goatman ReleaseQualified composes captured BeginFor packets,Berserk release-state refresh and the shared prepared monster damage bridge.1616qualified contexts from the complete shared3840case source matrix match all captured packet channels,release physical clip/cached total,FP health,RNG,exact service traces and source state rank/deadline. Stored damage is not rebuilt from the changed release rank. The2224outer target/contact/miss contexts remain outside this qualified consumer.',
   'The bridge writes source HP exactly once through the existing FP field projection and does not replay Mobile.Damage or restore old HP.848positive-total writes include50native deaths and24transient Stuns at zero HP while the native recipient is still alive. Stun precedes cold,kill notifications,attempt events,Thorns and the supplied return service. Native death consumes a cached result exactly once;duplicate and escaped callback calls reject. A separate heal-before-return control still consumes the cached death result.',
   'A real native encounter body retains99984FP life,remainder112and the exact recipient Stun deadline over two reloads without replay or double subtraction. Inspection retention applies only to the save fixture. Two pre-save checks retain exact life/state and genuine encounter maxima,including a separately buffed encounter injured above its base maximum. Initial diagnostics retain all three failed runs. They isolated an actual restore bug:CreatureProfiles.Initialize divided saved life by the temporary65000World.Loadingguard. Encounter restoration now preserves its loaded HP field until saved resource states establish true maxima. Ordinary initialization retains proportional rescaling;save formats and source expectations are unchanged. All185previous marker counts remain exact;both builds are clean,70Python tests pass,573named decisions and106installed artifacts remain unchanged.'],
  remaining=['This is the qualified ordinary NPC-to-NPC branch with explicit metadata/event/cold/return services,not a complete Goatman actor. Actual target/room/town/hostility/killable/contact/mode gates,proc subscribers,damage credit,leech/absorption/HP regeneration and full reaction/death-delay return policy must be supplied and verified by its caller. Source fixture event subscribers are no-ops;native cold and source reaction services are not certified by trace equivalence. No current executable parity is claimed.',
   'Reentrant native deletion/death before health commitment and health-setter mutation/death are surfaced as unsupported contexts. Existing generic SkillHealth.Damage and PreparedMonsterStun.CommitSurvivor behavior remains unchanged;the new synchronous kernel uses its own validated zero-HP-before-death phase. Pending death modes are not persisted or given invented timers.',
   'Full Goatman casting/body/AI,current helper/dispatcher linkage,modern extended passives/event16/CrushingBlow/BloodOath/sunder and new Frenzy/Cleave callbacks remain unfinished. No installation,learned handler or class completion is claimed;full-suite acceptance is a separately recorded scope. Final effects/audio and separate talent/hotkey icons remain pending with identified placeholders.'])
 p=ROOT/'tools/reference/monster-ordered-release-native-evidence.json';out=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==out
 else:p.write_text(out)
 print('Ordered monster release:190focused/full='+str(result['verified']['fullClassMarkers'])+';1616source compositions,848FP writes,50native deaths,24transient lethal stuns;70Python.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');p.add_argument('--full-class',action='store_true');args=p.parse_args();record(args.check,args.full_class)
