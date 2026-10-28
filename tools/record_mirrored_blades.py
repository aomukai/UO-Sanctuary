#!/usr/bin/env python3
"""Bind Mirrored Blades preparation and retained Cleave; no do166 certificate."""
import argparse,ast,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/mirrored-blades'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
 old=ROOT/'tools/record_hex_siphon.py';node=next(n.value for n in ast.walk(ast.parse(old.read_text())) if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='names' for t in n.targets));names=ast.literal_eval(node)+['hex-purge-fixtures.json','burst-speed-fixtures.json','cleave-fixtures.json','mirrored-blades-fixtures.json'];artifacts=[STAGE/n for n in names]+[old]
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==162
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 for n in ['mirrored-blades','cleave','hex-purge','burst-speed','hex-siphon','reward-life','hex-bane','enchant','psychic-ward','cyclone-armor','class-skills','miasma-launch','fissure','miasma-packets','levitation','barbarian-masteries','warlock-foundation']:
  assert log.count(n+'-check: passed')==1 and log.count(n+'-reload-check: passed')==2,n
 assert log.count('burst-speed-result: passed')==1 and log.count('burst-speed-reload-result: passed')==2
 assert log.count('Mirrored Blades:2295supplier contexts,11232attack descriptors,14SQ25profiles,210supplied clocks;emission and packet placement remain gated')==3
 assert log.count('Cleave:6885supplier controls,14current SQ24profiles,210supplied clocks;no arc or modern start body inferred')==3
 saved=json.loads((STAGE/'mirrored-blades-fixtures.json').read_text());assert len(saved)==2 and {x['Female'] for x in saved}=={True,False}
 expected=dict(Rank=15,AttackPercent=310,SpeedPercent=25,TotalAttacks=5,DuplicatePercent=104,Mana256=3072,DuplicateMeleeDivisor=16,DuplicateRangedDivisor=32,DuplicatePlayerDivisor=40,PhysicalPercent=0,FlatPhysical=0,ElementPercent=0,DamageSynergyPercent=0)
 assert all(x['Values']==expected for x in saved)
 proof=ROOT/'tools/reference/mirrored-blades-source-evidence.json';source=json.loads(proof.read_text());assert source['classicDescriptorsMatched']==322 and source['profileCount']==14 and source['sequence']==25 and not source['handlerEnabled'];artifacts.append(proof)
 assert source['processMemoryRead']==source['processInput']==source['modernBodyVerified']==False
 for group in ['inputs','artifacts']:
  for name,digest in source[group].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
 frozen_receipt('89c4c30','tools/reference/cleave-native-evidence.json')
 previous_markers=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',(ROOT/'.local/class-skills/cleave/stats-native-checks.log').read_text()))
 current_markers=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',log))
 assert sum(previous_markers.values())==159 and current_markers-previous_markers==Counter({'mirrored-blades-check':1,'mirrored-blades-reload-check':2}) and not previous_markers-current_markers
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for name,digest in a['InputHashes'].items():assert sha(ROOT/name)==digest,name
  audits.append(a);artifacts.append(p)
 previous=json.loads(subprocess.check_output(['git','show','89c4c30:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==previous['Definitions'] and [len(a['InputHashes']) for a in audits]==[704,21]
 py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
 main=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(main['installedArtifacts'])==106
 for name,digest in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/name)==digest,name
 artifacts += [ROOT/n for n in ['tests/check_mirrored_blades.py','tools/record_mirrored_blades.py','tools/probe_mirrored_blades.py','tools/player_weapon_sequences.py','tools/build_class_skills.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json','tools/reference/creeper-frame-installation-evidence.json']]
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert len([s for s in status['skills'] if s['implementation']=='handler-present'])==183 and status['completedClasses']==[]
 skill=next(s for s in status['skills'] if s['talentId']=='warlock.mirroredblades');assert skill['implementation']=='blocked' and skill['verification']=='partial-native-coverage'
 result=dict(referenceVersion='3.3.93847',component='Mirrored Blades prepared primary/duplicate suppliers and current SQ25 data',installed=False,handlerEnabled=False,currentExecutableParity=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=162,retainedMarkers=159,worldBoots=3,actualWorldReloads=2,currentFileClassicDescriptorMatches=322,currentFileMirrorWeaponProfiles=14,nativeFormulaContextsPerBoot=2295,attackDescriptorsPerBoot=11232,suppliedScalarComparisonsPerBoot=78624,suppliedCrushingComparisonsPerBoot=432,suppliedRateClocksPerBoot=210,savedWarlockWitchActorSupplierSets=2,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[704,21],unchangedMainInstallationArtifacts=106),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='89c4c30',receipt='tools/reference/cleave-native-evidence.json'),
  established=['Owned current skill392and skilldesc corroborate all used suppliers and their comments. Total attacks min(2+rank/5,5),so the cap is reached at effective15;the tooltip shows total-minus1duplicates. The version-pinned primary extractor independently reports this. Older supplied cap20notes are superseded for these suppliers.',
   'The immutable per-attack plan keeps attack0at normal100percent with no duplicate divisor override. Later attacks use separate dm12total damage60at1,101at12,104at15,108at20,120at255. This is not a Cleave-style additive physical ED. Speed uses dm34;AR100+15per extra rank;mana(10+rank-1)/2. calc1/2/4are0and absent Param8makes DmgSymPerCalc0even with hard BladeWarp/EchoingStrike. No damage synergy is invented.',
   'Duplicate raw Crushing Blow divisors change at ranks6/11/16,while attack counts change at5/10/15. Melee/ranged/player values are8/16/20at1and20/40/50at16. Explicit raw-divisor arithmetic reuses NamedCombat.CrushingLoss256without adding another ranged doubling. It does not select a target class,elite/controlled/PvP policy or trigger Crushing Blow. Supplied direct-scalar arithmetic reuses the shared source percentage ordering,including the1048576boundary;the primary stays unchanged. Its placement in the new body remains unknown,so it does not scale or commit whole combat packets.',
   'Shared PlayerWeaponSequencesnow reads current SQ24/SQ25data and retains detached row copies,explicit class/rate/framebonus and existing SourceAnimationEventsplanning. All322classic descriptors corroborate layout;14Mirrored profiles contain16..21points and one melee event. No separate scheduled event is invented for each duplicate. Current getter/auxiliary256/rate/dispatch are not certified. All159earlier markers,including real Cleave contacts and both reloads,pass after the shared-reader refactor.',
   'Native2295independent rank/hard-synergy contexts per boot generate11232immutable attack descriptors,78624supplied-scalar comparisons,432raw-divisor controls and210SQ25clock controls. Actual Warlock/Witch with hard14MirroredBlades have4attacks before Tarnhelm;the real+1item produces effective15,total5,duplicate104percent,AR310,speed25,mana12. Both saved actors/gear/supplier DTOs retain exact values after two real reloads. No cast is paid or attack emitted by these queries,and no saved in-flight skill is claimed.',
   'All573named-item eligibility decisions remain unchanged. Both builds are clean and all70Python tests pass.106main installed artifacts remain byte-identical;the working4c5d3a0build,original saves/configuration/client and frozen predecessor stages are preserved.'],
  remaining=['New start68/do166,mirror emission,target/range/search,melee/bow/throw/ammunition use,hit/critical/mastery/packet scaling placement/rounding,poison/cold/duration and proc chronology remain unsupported. Current table labels and extractor notes do not certify these bodies. Scalar/divisor services are explicitly supplied preparation,not a multi-attack implementation.',
   'Primary extractor reports per-attack Crushing Blow,Open Wounds,on-striking and melee-only on-attack eligibility;actual callback order and target-specific divisor overrides remain unverified. Existing item skill-proc gates are not widened. Older notes claim throwing potion support while current table excludes tpot;the conflict remains open.',
   'Learned input stays disabled;no full regression,installation,current-client acceptance or complete class is claimed. Coverage remains183handlers/Warlock1. Continue unaffected Warlock summon work while the modern shared attack body remains individually gated.',
   'No new visual or actor is created by the prepared suppliers. Final primary/mirror levitated weapons,up to4ghost duplicates,rig/bone attachments,melee/bow/throw trails/projectiles,impact/miss/block/recovery and Warlock_Skill_Blades_06audio plus talent/left/right/quick icons remain separately tracked.'])
 p=ROOT/'tools/reference/mirrored-blades-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Mirrored Blades prepared:162native/3boots/2reloads,70Python;early rank15cap and SQ25data verified,new start/do/emission remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
