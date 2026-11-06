#!/usr/bin/env python3
"""Bind the approved landing adaptation and learned Leap Attack acceptance."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'leap-attack';WIDE=BASE/'leap-attack-retained'
PARENT='3c4160fb6d26123c04c9e05504e52d8ff811426a'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-5.json')
 old=e.load(STAGE/'source-snapshot-1.json')['inputs'];new=e.load(STAGE/'source-snapshot-5.json')['inputs']
 for field in ['copiedData','projectInputs']:assert old[field]==new[field]
 prod=lambda d:{p:h for p,h in d['sourceInputs'].items()if p.startswith(('src/','shared/'))}
 assert prod(old)==prod(new),'Production changed during diagnostic fixture iterations'
 prior=BASE/'leap-attack-sequence-retained/birth-native-checks.log'
 receipt=e.load(ROOT/'tools/reference/leap-attack-sequence-native-evidence.json')
 assert e.sha(prior)==receipt['files'][str(prior.relative_to(ROOT))]
 retained=markers(prior);assert sum(retained.values())==284
 delta=Counter()
 for family in ['leap','leap-attack','warcry']:
  delta.update({family+'-check':1,family+'-reload-check':2})
 delta.update({'leap-result':3,'warcry-result':3})
 for mode in ['left','right','quick']:delta.update({'leap-attack-'+mode+'-check':3,'leap-attack-'+mode+'-result':3})
 base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
  'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'leap-attack-sequence-check':3,
  'double-swing-sequence-check':1,'double-swing-sequence-reload-check':2})
 e.cohort(STAGE,base+delta,48);e.cohort(WIDE,retained+delta,317)
 for stage,run in [(STAGE,4),(WIDE,1)]:
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
  log=(stage/'birth-native-checks.log').read_text()
  assert log.count('Leap landing native damage notification:')==9
  assert log.count('Leap Attack SQ14:1620initial and1080absolute')==3
  fixture=e.load(stage/'leap-attack-fixtures.json');assert len(fixture)==3
  assert {s['Flight']['Phase']for s in fixture}=={0,1,2}
  assert all(s['Generation']==2 and s['Mana']==10257 and s['Life']%256==243 for s in fixture)
  assert "denied by connection filter 'auto-denylist'"not in log
 for name in ['content-build-1.log','content-build-2.log','integration-build-5.log']:
  log=(STAGE/name).read_text();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 diagnostics=['failed-1','diagnostic-2','diagnostic-3','unused-damage-override-4']
 for folder in diagnostics:assert (STAGE/folder).is_dir()
 for folder in diagnostics[:3]:assert 'Natural landing area absent'in(STAGE/folder/'birth-native-checks.log').read_text()
 assert 'areaCalls=1'in(STAGE/'diagnostic-3/birth-native-checks.log').read_text() or 'calls=1'in(STAGE/'diagnostic-3/birth-native-checks.log').read_text()
 decision=ROOT/'tools/reference/leap-attack-landing-decision.json';d=e.load(decision)
 assert d['approved']and d['questionId']=='call_c5ec32f09d924894aa7afd72ba193386'and d['answer']=='Approve this Leap Attack landing rule'
 source=ROOT/'tools/reference/leap-attack-sequence-source-evidence.json';proof=e.load(source)
 assert proof['initialContexts']==1620 and proof['absoluteContexts']==1080 and not proof['currentExecutableCodeParity']
 for group in ['inputs','artifacts']:
  for p,h in proof[group].items():assert e.sha(ROOT/p)==h,p
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(r['exitCode']==0 for r in commands)
 log=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in log and '\nOK\n'in log
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
 assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 skill=next(s for s in status['skills']if s['talentId']=='barbarian.leapattack')
 assert skill['implementation']=='handler-present'and skill['verification']=='partial-native-coverage'and not skill['complete']
 files=e.stage_files(STAGE,WIDE)+[decision,source,Path(__file__).resolve(),ROOT/'tools/reference/inferno-held-input-client-evidence.json']
 files.extend(p for folder in diagnostics for p in (STAGE/folder).rglob('*')if p.is_file())
 files += [ROOT/p for p in ['tests/check_leap_attack.py','tests/check_leap_attack_sequence.py','tests/export_runtime_audits.py','tools/build_skill_roadmap.py']]
 files += [ROOT/'docs'/p for p in ['leap-attack-checkpoint.md','leap-attack-sequence-checkpoint.md','leap-attack-landing-proposal.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','barbarian-class-roadmap-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,barbarianHandlers=30,completedClasses=[],
  learnedLeapAttack=True,approvedLandingRule=True,currentExecutableParity=False,
  sourceSnapshot=5,contentBuild=2,unchangedProductionBuild=1,integrationBuild=5,focusedRun=4,retainedRun=1,auditRun=1,
  focusedMarkers=48,retainedMarkers=317,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,
  pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,
  artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Approved circular physical landing hit precedes separate SQ14 weapon contact. Area shares physical-spell mitigation/block/dodge,with no weapon procs,critical,leech,durability or Leap knockback.',
   'Learned paid flight uses shared reservations,network/input/death locks,175percent velocity,minimum12air frames,distinct beyond-target/free-cell placement and absolute point10/16 events. Ordinary contact includes weapon identity/range checks,source knockback/hit32 and successful stun clearing.',
   'Three real input modes exercise natural flight on each boot;original paid Windup/Airborne/Recovery owners retain wounds,fractional mana,target/weapon identity/durability through two reloads. Offline settlement emits no combat or second payment;foreign facet cleanup never pulls back.',
   'All284parent markers retain,plus ordinary Leap/WarCry natural/save controls. All573named decisions and installed build/saves/client remain unchanged. Optional V0payload and immutable validated restore retain ordinary Leap compatibility.'],
  limits=['Modern start41/do78/IAS/WSM/rate/target/callback parity,terrain/room/footprint/subtile/dynamic landing,zero-distance input,delayed-dead area,additional owner/gear/PvP/multiplayer/recipient/client acceptance remain uncertain.',
   'Three failed input observations used net HP on a high-life custom-regenerating zombie;the final allowed OnDamage observer confirms actual positive native damage with regeneration enabled. Saved/synchronous recipients use existing prevent-healing. Production stayed unchanged. Unused Damage-override prototype was replaced before native execution because ordinary admission rejects it.',
   'Saved fixture birth deadlines are held for inspection;natural clock/input is separately tested. Existing unsupported ordinary-contact recipients still refuse explicitly. No class completion,deployment,final effects/audio or talent/hotkey icons.'])
 e.write('leap-attack-native-evidence.json',result,files,check)
 print('Learned Leap Attack:48focused/317retained,three input modes/two reloads,71Python;202handlers/Barbarian30,no class complete;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
