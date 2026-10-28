#!/usr/bin/env python3
"""Bind current SQ24 file data and prepared contacts without certifying do164."""
import argparse,ast,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/cleave'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
 old=ROOT/'tools/record_hex_siphon.py';node=next(n.value for n in ast.walk(ast.parse(old.read_text())) if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='names' for t in n.targets));names=ast.literal_eval(node)+['hex-purge-fixtures.json','burst-speed-fixtures.json','cleave-fixtures.json'];artifacts=[STAGE/n for n in names]+[old]
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==159
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 for n in ['cleave','hex-purge','burst-speed','hex-siphon','reward-life','hex-bane','enchant','psychic-ward','cyclone-armor','class-skills','miasma-launch','fissure','miasma-packets','levitation','barbarian-masteries','warlock-foundation']:
  assert log.count(n+'-check: passed')==1 and log.count(n+'-reload-check: passed')==2,n
 assert log.count('burst-speed-result: passed')==1 and log.count('burst-speed-reload-result: passed')==2
 assert log.count('Cleave:6885supplier controls,14current SQ24profiles,210supplied clocks;no arc or modern start body inferred')==3
 saved=json.loads((STAGE/'cleave-fixtures.json').read_text());assert len(saved)==2 and {x['Female'] for x in saved}=={True,False}
 assert all(x['Values']==dict(Rank=21,DamagePercent=150,AttackPercent=190,SpeedPercent=27,RawSlices=20,TooltipArcDegrees=240,Mana256=768) for x in saved)
 proof=ROOT/'tools/reference/cleave-source-evidence.json';source=json.loads(proof.read_text());assert source['classicDescriptorsMatched']==322 and source['profileCount']==14 and source['legacyStart68IsNull'] and not source['handlerEnabled'];artifacts.append(proof)
 assert source['processMemoryRead']==source['processInput']==source['modernStartOrDoBodyVerified']==source['auxiliaryWordSemanticsVerified']==False
 for group in ['inputs','artifacts']:
  for name,digest in source[group].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
 frozen_receipt('7c665d5','tools/reference/hex-purge-native-evidence.json')
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for name,digest in a['InputHashes'].items():assert sha(ROOT/name)==digest,name
  audits.append(a);artifacts.append(p)
 previous=json.loads(subprocess.check_output(['git','show','7c665d5:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==previous['Definitions'] and [len(a['InputHashes']) for a in audits]==[701,21]
 py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
 main=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(main['installedArtifacts'])==106
 for name,digest in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/name)==digest,name
 artifacts += [ROOT/n for n in ['tests/check_cleave.py','tools/record_cleave.py','tools/probe_cleave.py','tools/build_class_skills.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json','tools/reference/creeper-frame-installation-evidence.json']]
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert len([s for s in status['skills'] if s['implementation']=='handler-present'])==183 and status['completedClasses']==[]
 cleave=next(s for s in status['skills'] if s['talentId']=='warlock.cleave');assert cleave['implementation']=='blocked' and cleave['verification']=='partial-native-coverage'
 result=dict(referenceVersion='3.3.93847',component='Cleave prepared physical suppliers,current SQ24 data and qualified single contacts',installed=False,handlerEnabled=False,currentExecutableParity=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=159,retainedMarkers=156,worldBoots=3,actualWorldReloads=2,currentFileClassicDescriptorMatches=322,currentFileCleaveWeaponProfiles=14,nativeFormulaControlsPerBoot=6885,suppliedRateClocksPerBoot=210,savedWarlockWitchActorSupplierSets=2,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[701,21],unchangedMainInstallationArtifacts=106),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='7c665d5',receipt='tools/reference/hex-purge-native-evidence.json'),
  established=['Pinned current executable file data provides14player SQ24weapon rows. All322classic descriptors match older point counts,frame counts and exact6-byte rows,including zero-initialized weapon slots. Cleave has one melee event,CAST10pose rows,and16..22sequence points. The raw extra word256is retained;its runtime meaning is unverified. This is file-data corroboration,not execution of the current getter/dispatcher.',
   'Native all255ranks and independent0/1/20hard MirroredBlades/HexPurge/EldritchBlast controls verify6885supplier contexts per boot. ED20+5per extra rank+10per each hard synergy;AR50+7per extra rank;speed uses dm34between10and30;mana3. Effective bonuses to synergies do not add hard points.',
   'Prepared event planner reuses SourceAnimationEvents with explicit source weapon class,advance and framebonus;210clock controls per boot verify independent point/event/end expectations. Detached row copies cannot mutate loaded profiles. CASTposes do not select FCR. Neither automatic rate selection nor Warlock basic provisional clocks are certified.',
   'Explicitly qualified single contacts reuse ordinary melee CheckHit and SrcDam128weapon damage:unqualified contacts do nothing,forced misses do no damage,separate targets get independent hit checks,and actual damage includes captured physical ED while ordinary Enchant fire remains unscaled. No learned route,arc selection,mana payment or wear cadence is introduced. The caller must supply eligibility,capture instant and wear intent. Unarmed and unsupported throwing-base melee are refused. Ordinary weapon hooks remain the declared adapter contract,not proof of new do164order.',
   'Actual trained Warlock/Witch with hard20Cleave,hard1in each synergy and real Tarnhelm retain rank21/ED150/AR190/speed27/raw20slices/tooltip240degrees across two real world reloads. Contact consumers pass after each reload. The fixture JSON stores inspection DTOs;no saved in-flight Cleave attack is claimed.',
   'The first supplier check caught an incorrect numeric-field lookup for toht;Capture now invokes the formula symbol. Its failed diagnostic is retained locally. A reload fixture initially attempted contact while its offline player was internalized;the fixture now explicitly places the retained actor back into the world before testing contacts. Production eligibility guards are unchanged. Final159markers retain all156earlier controls,including Burst of Speed,Purge,Siphon,Enchant and supplied hex/pool mechanics. All573item eligibility decisions remain unchanged;106main installed artifacts remain byte-identical.'],
  remaining=['Older start68is null,so neither DoubleSwing do70nor unrelated do68BasicShout establishes modern start68. Cleave do164is new. Current start/payment/windup,input/release,actual target/range/arc/hit chronology,weapon qualification and durability cadence remain unsupported. No learned handler is enabled.',
   'Actual rank20arc conflicts:owned current skills/skilldesc agrees with cached20slices=240degrees,while supplied primary research says30slices=360degrees. Raw numeric/tooltip data is retained without implementing either geometry as verified. User has no confirmed Cleave;BladeWarp prerequisites do not imply it is trained.',
   'Prepared contacts/clocks are services with explicitly supplied modern context. Auxiliary256,modern sequence getter/rate/framebonus,mode/stat/seed/item/owner/PvP/lifecycle/client acceptance remain separate. No complete class,full regression or installation is claimed;main4c5d3a0and original saves/configuration/client and frozen predecessors remain preserved.',
   'Ordinary weapon sound0x238is an identified placeholder. Final levitated-weapon sweep/arc,contacts,miss/block/recovery audio and talent/left/right/quick icons remain separate asset work.'])
 p=ROOT/'tools/reference/cleave-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Cleave prepared:159native/3boots/2reloads,70Python;322classic rows corroborate SQ24data,new start/do and actual arc remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
