#!/usr/bin/env python3
"""Bind approved Double Swing to current sequence data and actual shared combat."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'double-swing-sequence';WIDE=BASE/'double-swing-sequence-retained';PARENT='618eb68ed23d8398c2abafc87f2c878c98d3988c'
def record(check=False):
 snapshot=STAGE/'source-snapshot-13.json';born=e.inventory(STAGE,WIDE,snapshot);now=e.load(snapshot)['inputs'];old=e.load(BASE/'necromancer-item-skills/source-snapshot-6.json')['inputs']
 previous=e.load(STAGE/'source-snapshot-12.json')['inputs']
 assert previous['projectInputs']==now['projectInputs'] and previous['copiedData']==now['copiedData']
 assert {p for p,h in previous['sourceInputs'].items()if now['sourceInputs'][p]!=h}=={'tests/Sanctuary.Integration/DoubleSwingChecks.cs'}
 assert now['sourceInputs'].keys()-old['sourceInputs'].keys()=={'src/Sanctuary.Content/BarbarianDoubleSwing.cs','tests/Sanctuary.Integration/DoubleSwingSequenceChecks.cs','tests/Sanctuary.Integration/DoubleSwingChecks.cs'}
 assert {p for p,h in old['sourceInputs'].items()if p.startswith(('src/','shared/'))and now['sourceInputs'][p]!=h}=={'src/Sanctuary.Content/ActionTiming.cs','src/Sanctuary.Content/BarbarianCombat.cs','src/Sanctuary.Content/ClassCombat.cs','src/Sanctuary.Content/PlayerWeaponSequences.cs'}
 assert now['projectInputs']==old['projectInputs']
 assert now['copiedData'].keys()-old['copiedData'].keys()=={'Data/Sanctuary/Balance/double-swing-sequence.json'}
 assert {p for p,h in old['copiedData'].items()if now['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
 delta=Counter({'double-swing-phase-check':3,'barbarian-cast-clock-check':3,'double-swing-sequence-check':1,'double-swing-sequence-reload-check':2,'weapon-hands-check':1,'weapon-hands-reload-check':2,'double-swing-check':1,'double-swing-reload-check':2,'barbarian-binding-check':1,'barbarian-binding-reload-check':2})
 for mode in ['left','right','quick']:delta.update({'double-swing-'+mode+'-check':3,'double-swing-'+mode+'-result':3})
 e.cohort(STAGE,base+delta,45);prior=BASE/'necromancer-item-skills-retained/birth-native-checks.log';receipt=e.load(ROOT/'tools/reference/necromancer-item-skills-native-evidence.json');assert e.sha(prior)==receipt['files'][str(prior.relative_to(ROOT))]
 retained=markers(prior);assert sum(retained.values())==188;e.cohort(WIDE,retained+delta,224)
 for stage,run in [(STAGE,6),(WIDE,1)]:
  inputs=e.load(stage/'birth-inputs.json');assert inputs['artifacts']==born['artifacts'] and inputs['copiedData']==born['copiedData'];e.snapshot(snapshot,stage/'birth-inputs.json')
  for p,h in inputs['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
  log=(stage/'birth-native-checks.log').read_text();assert log.count('Approved Double Swing:normal/throwing melee pairs')==3 and log.count('Double Swing SQ11:14current profiles/1890source')==3
  assert "denied by connection filter 'auto-denylist'"not in log
  saved=e.load(stage/'double-swing-fixtures.json');assert saved['Generation']==2 and len({saved['First'],saved['Second']})==2 and saved['Mana']%256!=0
 for name in ['content-build-6.log','integration-build-9.log']:
  text=(STAGE/name).read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 sourcePath=ROOT/'tools/reference/double-swing-sequence-source-evidence.json';source=e.load(sourcePath)
 assert source['referenceVersion']=='3.3.93847' and source['sourceContexts']==135 and source['profiles']==14 and source['classicDescriptorsMatched']==322 and source['sourcePlayerClass']==4
 assert len(source['completeRoutineHashes'])==5 and source['processMemoryRead']==False and source['currentExecutableCodeParity']==False
 for group in ['inputs','artifacts']:
  for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
 phasePath=ROOT/'tools/reference/double-swing-phase-source-evidence.json';phase=e.load(phasePath);assert phase['byFamily']==dict(find=1470,phase=6066);assert e.sha(ROOT/phase['fixture']['path'])==phase['fixture']['sha256']
 decisionPath=ROOT/'tools/reference/double-swing-start-decision.json';decision=e.load(decisionPath);assert decision['approved'] and decision['answer']=='Approve this explicit Double Swing rule' and decision['questionId']=='call_9156a4e3206248f3a18956c97a08ba01' and len(decision['rules'])==5
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==8 and all(x['exitCode']==0 for x in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text() and '\nOK\n'in(STAGE/'python-tests.log').read_text()
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 protected['acceptedClientPatches']=41
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(x['implementation']=='handler-present'for x in status['skills'])==199 and not status['completedClasses'];row=next(x for x in status['skills']if x['talentId']=='barbarian.doubleswing');assert row['verification']=='partial-native-coverage' and not row['complete']
 files=e.stage_files(STAGE,WIDE,BASE/'double-swing-sequence-source')+[sourcePath,phasePath,decisionPath]
 for name in ['before-handler-1','failed-1','failed-2','failed-3','prior-4','failed-5']:files += [p for p in(STAGE/name).rglob('*')if p.is_file()]
 for kind in ['content','integration']:files.append(STAGE/kind/f'Sanctuary.{kind.title()}.dll')
 files += [ROOT/p for p in ['tests/check_double_swing_sequence.py','tests/check_iron_golem_birth.py','tools/probe_double_swing_sequence.py','tools/player_weapon_sequences.py','tools/build_skill_roadmap.py','tools/reference/inferno-held-input-client-evidence.json']]
 files += [ROOT/'docs'/p for p in ['approved-double-swing-rule.md','double-swing-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md','skill-mechanical-reference.md']];files.append(Path(__file__).resolve())
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=199,completedClasses=[],approvedStartup=True,decisionSha256=e.sha(decisionPath),sourceSnapshot=13,snapshotChronology='Snapshot13 records final Content6/Integration9 inputs without a pre-build chronology claim;only the wider-search fixture differs from snapshot12. Production,projects and data match snapshot12.',contentBuild=6,integrationBuild=9,focusedRun=6,retainedRun=1,auditRun=6,focusedMarkers=45,retainedMarkers=224,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,sourceContexts=135,sequenceProfiles=14,nativeSequenceComparisons=1890,legacyPhaseControls=7536,realInputModes=['left','right','quick'],cancellationCases=11,pythonTests=71,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Approved explicit startup/payment/timing rule enables learned Double Swing. Two eligible melee weapons use shared SQ11 scheduling and ordinary PlayerWeaponContact,with per-event scoped hands,AR,hard Bash damage and independently paid fractional costs. Low mana falls back to ordinary contact;rank9+is free.',
         'Current immutable executable data anchors322classic descriptors and14SQ11profiles. Five unchanged older scheduler/reader/callback/frame bodies cover135supplied speed/epoch/fraction contexts;1890native profile comparisons retain action ordinals0/1. Legacy do70/GUID controls7536remain separate.',
         'Pinned player weapon reach,shared creature footprints/collision and a wider reach+4search prevent projectile range leakage and premature candidate filtering. Final contact checks retain the shared geometry predicate. GUID successor/wrap remains distinct from nearest-target QoL.',
         'Normal/throwing melee pairs,paid misses,low/free/fractional and mid-series funding,GUID/wider-search behavior,eleven cancellation cases,original saved pair/gear/durability/wounds/resources and3real left/right/quick modes pass. All188parent markers remain;Barbarian bindings/SC clocks are retained.'],
  limits=['Current compiled start68,paired IAS/WSM quirks and full geometry/room/PvP/special-recipient/client parity remain unverified;the explicit approved startup contract takes precedence. Crafted/rune/material pair domains retain their existing refusals. No class completion or installed deployment.',
          'Four failed fixtures and an intermediate passing version are preserved. Ready-before-ack dropped input metadata;subsequent diagnostics identified managed target regeneration despite native CanRegenHits=false. The fixture now applies existing PMH. Review also corrected wider target search and reused pinned melee geometry instead of projectile reach;the final cohorts bind that code.',
          'Identified native weapon animation/sound placeholders remain. Final alternating-hand presentation/effects/audio and talent/hotkey icon are separate asset tasks. Installed106artifacts/38saves/20configs and accepted41client patches/shared inputs remain unchanged.'])
 e.write('double-swing-native-evidence.json',result,files,check)
 print('Approved Double Swing:45focused/224retained,two reloads and3real input modes;135source/1890SQ/7536legacy phase controls;71Python;199handlers/no class complete;installed world preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
