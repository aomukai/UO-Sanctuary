#!/usr/bin/env python3
"""Bind town-safe original registration,approved overlap and saved Valkyries."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-town';WIDE=BASE/'valkyrie-town-retained';ISO=BASE/'valkyrie-town-isolate';SHARED=BASE/'valkyrie-town-shared';PARENT='d27231bf8ffa3fd700ac143e6d7a505f6c44729b'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-5.json');sys.path.insert(0,str(ROOT/'tests'));from check_valkyrie_town import configuration
 assert e.sha(STAGE/'content/Sanctuary.Content.dll')==e.sha(STAGE/'failed-focused-1/Sanctuary.Content.dll'),'Fixture correction changed production Content4'
 parent=e.load(ROOT/'tools/reference/valkyrie-startup-native-evidence.json');extra=Counter({'valkyrie-town-check':1,'valkyrie-town-reload-check':2})
 for previous,stage,count in [(BASE/'valkyrie-startup',STAGE,117),(BASE/'valkyrie-startup-retained',WIDE,425)]:
  p=previous/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
 e.cohort(ISO,expected(configuration(isolate=True)),24);e.cohort(SHARED,expected(configuration(shared=True)),255)
 phrase='Valkyrie town:two unfiltered initialized original rosters created and parked in a real safe region;structural registration with zero live callback/cast/allocation admission,normal effect expiry/no recovery,original wounds/items/RNG on return and two reloads;resumed39/49/66effects reuse actual execution;learned casting/sunder/contact gaps remain separate'
 counts={}
 for stage,run in [(STAGE,2),(WIDE,2),(ISO,3),(SHARED,2)]:
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text();snapshot(STAGE/'source-snapshot-5.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json');assert other['artifacts']==born['artifacts']and other['copiedData']==born['copiedData']
  for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  if stage==SHARED:continue
  assert(stage/'birth-native-checks.log').read_text().count(phrase)==3
  saved=e.load(stage/'valkyrie-town-fixtures.json');assert saved['Generation']==2 and len(saved['Bodies'])==2
  n=0
  for h in saved['Bodies']:
   b=json.loads(h['Text']);r=json.loads(h['Resources']);c=b['GroundMotion']['World']['Controller']
   assert b['Owner']==saved['Owner']==r['Owner']and r['Actor']==h['Body']==h['Random']['Actor']
   assert b['Initialization'] is not None and b['GroundMotion']['StartupProfile']=='source-valkyrie-mode-entry-v1'and b['EquipmentContactProfile']=='valkyrie-original-proc-contact-v1'
   assert c['Mode']==1 and c['ActionAt']==c['EndAt']==0 and h['Life']>=256 and r['ItemEvents']is not None
   assert len(h['Items'])==sum(a['Success']for a in b['Equipment']['Attempts']);n+=len(h['Items'])
  assert n==14;counts[stage.name]=n
 for n in ['content-build-1.log','content-build-2.log','content-build-3.log','content-build-4.log','integration-build-1.log','integration-build-2.log','integration-build-3.log']:assert all(x in(STAGE/n).read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and'\nOK\n'in(STAGE/'python-tests.log').read_text()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==10 and all(c['exitCode']==0 for c in commands)
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for g in ['patchHashes','sharedInputs']:
  for p,h in client[g].items():assert e.sha(ROOT/p)==h,p
 source=ROOT/'tools/reference/valkyrie-startup-source-evidence.json';s=e.load(source);assert s['transitions']==5016 and len(s['completeRoutineHashes'])==24
 for g in ['inputs','artifacts']:
  for p,h in s[g].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 files=[p for p in e.stage_files(STAGE,WIDE,ISO,SHARED)if not p.name.startswith('receipt-run-')]+[p for folder in [ISO/'failed-native-1',STAGE/'failed-focused-1',WIDE/'failed-native-1',ISO/'prior-passed-2',SHARED/'prior-passed-1'] for p in folder.iterdir() if p.is_file()]
 files += list((STAGE/'frozen-c4-i3').iterdir())
 files += [Path(__file__).resolve(),ROOT/'tests/check_valkyrie_town.py',ROOT/'tools/probe_valkyrie_startup.py',source,STAGE/'content/Sanctuary.Content.dll',STAGE/'integration/Sanctuary.Integration.dll']
 files += [ROOT/'docs'/n for n in ['valkyrie-town-checkpoint.md','zero-distance-knockback-proposal.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,currentExecutableParity=False,structuralOriginalRegistration=True,liveOriginalGuardsPreserved=True,safeAreaInitializedCreation=True,immediateResumePose=True,approvedCoincidentKnockback='attacker facing;normal distance/chance/damage;no RNG',overlapCasesPerFixtureBoot=16,sourceSnapshot=5,contentBuild=4,integrationBuild=3,focusedRun=2,retainedRun=2,isolatedRun=3,sharedRun=2,focusedMarkers=117,retainedMarkers=425,isolatedMarkers=24,sharedMarkers=255,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,originalParkedBodiesPerCohort=2,originalItemsByCohort=counts,pythonTests=71,verificationCommands=10,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],scope=['Original graph/owner/resource/weapon/passive/RNG/event registration is separate from attached outside-town execution. World/Chain providers remain required;parked/in-town/inactive actors gain no callback/cast/contact/allocation permissions.', 'Source-initialized town creation parks immediately using existing actual safe regions. Two original bodies/14items retain wounds/resources/full event history/RNG through return and two reloads. Normal effect expiry and masked restoration/replenish grant no recovery. Immediate source pose binding precedes resumed defense;already-owned live pose remains untouched.', 'User-approved same-tile knockback uses masked attacker facing and existing source offset/trajectory,without chance/damage/RNG changes. Actual ordered return verifies16facing/frozen cases per boot. It reuses prepared movement after source mode selection instead of an additional native frozen veto.'],limits=['Current executable/full outer source parity is not claimed. This implements the approved native safe-area/overlap adaptations and existing actual source controller interfaces.', 'Inherited sunder has no shared resistance consumer yet;several pet births explicitly refuse it. Its pinned reduction/curse/Conviction ordering,external elemental stat lists,monster-type contact data and learned/client acceptance remain unfinished. No class completion or deployment;effects/icons remain separately identified placeholders.', 'Initial isolation failed a real unfiltered Knockback roll against co-located fixture units. Failure sources/inputs/binaries/logs remain archived. The user approved the direction fallback;the fixture retains overlap and adds16explicit return checks. Focused1 and retained1 then exposed a test wound legitimately filled by the deliberate live regeneration frame;Integration3 suppresses healing only during live fixture observation,asserts the original wound through repark and clears its test effect before save. Production Content4 is unchanged;failed exact versions and prior isolated/shared passes remain archived. Generator blank-line cleanup precedes these fresh native runs;C++/input/output/cases were independently shown byte-identical while the source receipt binds the current generator.'])
 e.write('valkyrie-town-native-evidence.json',result,files,check);print('Valkyrie town:structural/live separation,parking/return/2reloads,16approved overlap controls;117focused/425retained/24isolated/255shared;71Python/10checks;202handlers/no deployment.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
