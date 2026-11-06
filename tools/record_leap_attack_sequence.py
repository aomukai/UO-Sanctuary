#!/usr/bin/env python3
"""Bind SQ14/absolute reset preparation without claiming learned Leap Attack."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'leap-attack-sequence';WIDE=BASE/'leap-attack-sequence-retained';SOURCE=BASE/'leap-attack-sequence-source'
PARENT='0e71677e5c1f4ca83a5eff2bb6580225cdb962c4'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-2.json')
 base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
 extra=Counter({'leap-attack-sequence-check':3})
 e.cohort(STAGE,base+extra+Counter({'double-swing-sequence-check':1,'double-swing-sequence-reload-check':2}),15)
 parent=e.load(ROOT/'tools/reference/grim-ward-actor-native-evidence.json');prior=BASE/'grim-ward-actor-retained/birth-native-checks.log'
 assert e.sha(prior)==parent['files'][str(prior.relative_to(ROOT))]
 retained=markers(prior);assert sum(retained.values())==281;e.cohort(WIDE,retained+extra,284)
 for stage,run in [(STAGE,2),(WIDE,1)]:
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
  log=(stage/'birth-native-checks.log').read_text();assert log.count('Leap Attack SQ14:1620initial and1080absolute')==3
  assert log.count('Double Swing SQ11:14current profiles/1890')==3
 for name in ['content-build-2.log','integration-build-2.log']:
  log=(STAGE/name).read_text();assert all(v in log for v in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 failed=STAGE/'failed-1';assert 'Initial SQ14 complete source scheduler mismatch'in(failed/'birth-native-checks.log').read_text()
 assert (failed/'src/Sanctuary.Content/SourceAnimationEvents.cs').read_bytes()==(ROOT/'src/Sanctuary.Content/SourceAnimationEvents.cs').read_bytes()
 assert (failed/'src/Sanctuary.Content/PlayerWeaponSequences.cs').read_bytes()==(ROOT/'src/Sanctuary.Content/PlayerWeaponSequences.cs').read_bytes()
 source=ROOT/'tools/reference/leap-attack-sequence-source-evidence.json';proof=e.load(source)
 assert proof['referenceVersion']=='3.3.93847'and proof['sequence']==14 and proof['classicDescriptorsMatched']==322
 assert proof['initialContexts']==1620 and proof['absoluteContexts']==1080 and len(proof['completeRoutineHashes'])==6
 assert not proof['currentExecutableCodeParity']and proof['emptyProfiles']==['BOW','XBW']
 for group in ['inputs','artifacts']:
  for p,h in proof[group].items():assert e.sha(ROOT/p)==h,p
 cases=e.load(SOURCE/'cases.json');assert len(cases['initial'])==1620 and len(cases['absolute'])==1080
 for args,events,phases,fractions,word,epoch in cases['initial']:
  assert epoch==(args[2]*256 if args[1]else 0)
 before=SOURCE/'prior-reader-receipts'
 expected={'dragon-flight-source-evidence.json':['tools/player_weapon_sequences.py'],
  'double-swing-sequence-source-evidence.json':['tools/player_weapon_sequences.py','tools/reference/dragon-flight-source-evidence.json'],
  'frenzy-rule-reference.json':['tools/reference/double-swing-sequence-source-evidence.json']}
 for name,changes in expected.items():
  old=e.load(before/name);new=e.load(ROOT/'tools/reference'/name)
  assert [k for k in old if old[k]!=new[k]]==['inputs']
  assert [k for k in old['inputs']if old['inputs'][k]!=new['inputs'][k]]==changes
 assert 'cleave/skilldesc.txt'in(SOURCE/'mirrored-reader-rebind.log').read_text()
 assert 'FileNotFoundError'in(SOURCE/'mirrored-reader-rebind.log').read_text()
 assert e.sha(before/'mirrored-blades-source-evidence.json')==e.sha(ROOT/'tools/reference/mirrored-blades-source-evidence.json')
 decision=ROOT/'tools/reference/leap-attack-landing-decision.json';d=e.load(decision)
 assert d['approved']and d['questionId']=='call_c5ec32f09d924894aa7afd72ba193386'and d['answer']=='Approve this Leap Attack landing rule'
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(r['exitCode']==0 for r in commands)
 log=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in log and '\nOK\n'in log
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==201 and not status['completedClasses']
 assert next(s for s in status['skills']if s['talentId']=='barbarian.leapattack')['implementation']=='pending'
 files=e.stage_files(STAGE,WIDE)+[source,decision,Path(__file__).resolve()]
 files.extend(p for folder in [failed,SOURCE]for p in folder.rglob('*')if p.is_file())
 files += [ROOT/p for p in ['tools/probe_leap_attack_sequence.py','tools/player_weapon_sequences.py','tests/check_leap_attack_sequence.py','tests/check_grim_ward_actor.py','tools/reference/dragon-flight-source-evidence.json','tools/reference/double-swing-sequence-source-evidence.json','tools/reference/frenzy-rule-reference.json','tools/reference/mirrored-blades-source-evidence.json']]
 files += [ROOT/'docs'/p for p in ['leap-attack-sequence-checkpoint.md','leap-attack-landing-proposal.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=201,completedClasses=[],learnedLeapAttack=False,approvedLandingRule=True,currentExecutableParity=False,
  sourceSnapshot=2,contentBuild=2,integrationBuild=2,focusedRun=2,retainedRun=1,auditRun=2,focusedMarkers=15,retainedMarkers=284,
  bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,initialContextsPerBoot=1620,absoluteContextsPerBoot=1080,
  retainedSq11ComparisonsPerBoot=1890,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,
  artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Current file SQ14 retains14descriptors/12active/2empty ranged profiles and three weapon-dependent action markers. All322classic descriptor rows anchor file layout. No actual weapon rate or flight clock is selected.',
   'Six complete older source bodies cover initial schedules/player ordinal+fraction behavior and absolute phase resets. Shared native AtPoint preserves unrelated events,sets its absolute epoch and uses parameter0 for future actions;relative Renew is unchanged.',
   'All281parent markers retain,including learned Ward,Frenzy,DoubleSwing,input,resources and original saved state. All573named decisions remain unchanged;installed world/client are untouched.',
   'The user approved one circular landing area hit followed by the separate post-landing weapon strike. That mechanical integration is still next;the exact decision is recorded without calling it current D2R proof.'],
  limits=['Current start41/do78,actual IAS/WSM/flight/target/death/input and landing integration remain unverified or unfinished;no learned handler/class certificate is enabled by this preparation.',
   'The first fixture incorrectly expected initial zero-speed rebasing. Production matched the source;the oracle now records the observed epoch. Failed inputs,binaries and source are preserved.',
   'Shared reader extension refreshed only the input hashes of existing Flight/Swing/Frenzy source bindings;all prior artifacts and cases remain byte-identical. Missing historical Mirrored skilldesc prevents a full rerun;its unchanged receipt remains historical. Current SQ25 profile plans are retained separately.',
   'Source units,queues,table services and ordinary dispatcher are explicit harness boundaries. No current process memory,graphical client,real Leap Attack flight or final effects/icons are certified.'])
 e.write('leap-attack-sequence-native-evidence.json',result,files,check)
 print('SQ14/absolute reset:15focused/284retained,2700source comparisons per boot,71Python;201handlers/no class complete;approved landing integration next.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
