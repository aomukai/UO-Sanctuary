#!/usr/bin/env python3
"""Bind approved learned Ward, original totems/corpses and retained mechanics."""
import argparse,json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'grim-ward-actor';WIDE=BASE/'grim-ward-actor-retained'
PARENT='72902d18b4cd5a34165cb5298e7d360e824febc6'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-3.json')
 base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
 delta=Counter({'grim-ward-world-pulse-check':3,'grim-ward-curse-check':3,'grim-ward-lifecycle-check':3,
  'grim-ward-actor-check':1,'grim-ward-actor-check-result':1,'grim-ward-actor-reload-check':2,'grim-ward-actor-reload-check-result':2})
 for mode in ['right','quick']:delta.update({'grim-ward-'+mode+'-check':3,'grim-ward-'+mode+'-result':3})
 e.cohort(STAGE,base+delta+Counter({'barbarian-cast-clock-check':3}),39)
 prior=BASE/'frenzy-retained/birth-native-checks.log';receipt=e.load(ROOT/'tools/reference/frenzy-native-evidence.json')
 assert e.sha(prior)==receipt['files'][str(prior.relative_to(ROOT))]
 retained=markers(prior);assert sum(retained.values())==254;e.cohort(WIDE,retained+delta,281)
 for stage,run in [(STAGE,4),(WIDE,1)]:
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
  log=(stage/'birth-native-checks.log').read_text()
  assert log.count('Ward curse:31134complete legacy')==3 and log.count('Ward lifecycle:3144')==3
  assert ' ERR]'not in log and "denied by connection filter 'auto-denylist'"not in log
  fixture=e.load(stage/'grim-ward-actor-fixtures.json');assert fixture['Generation']==2 and len(fixture['Actors'])==1
  actor=fixture['Actors'][0];assert actor['Owner']==fixture['Owner']and actor['Rank']==3 and actor['FindPotion']==1 and actor['LastPulse']==0
 for name in ['content-build-2.log','integration-build-3.log']:
  log=(STAGE/name).read_text();assert all(v in log for v in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 assert 'Foreign facet Ward travelled or kept fighting'in(STAGE/'failed-1/birth-native-checks.log').read_text()
 assert 'FileNotFoundException'in(STAGE/'failed-3/birth-native-checks.log').read_text()
 assert 'barbarian-cast-clock-source-probe.json'in(STAGE/'failed-3/birth-native-checks.log').read_text()
 assert sum(markers(STAGE/'prepared-pass-2/birth-native-checks.log').values())==24
 assert 'Stale named runtime evidence'in(STAGE/'pre-audit-python-tests.log').read_text()
 before=e.load(STAGE/'client-evidence-before-order-fix.json');client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json')
 assert [k for k in before if before[k]!=client[k]]==['artifacts']
 assert [p for p in before['artifacts']if before['artifacts'][p]!=client['artifacts'][p]]==['tools/review_inferno_held_client.py']
 assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs','clientInputs']:
  assert before[group]==client[group]
  if group!='clientInputs':
   for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 decision=ROOT/'tools/reference/grim-ward-context-decision.json';answer=e.load(decision)
 assert answer['approved']and answer['answer']=='Approve this Grim Ward context rule'and answer['questionId']=='call_5d7243db15234c1b92d600751fc6a029'
 monsters=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json';modes=ROOT/'tools/reference/d2r-named-equipment/json/monstats2.json'
 rows=e.load(monsters);sizes=e.load(modes);data=ROOT/'data/Sanctuary/Balance/grim-ward-species.json';exported=e.load(data)
 assert len(exported)==751
 for r in exported:
  row=sizes[rows[r['Source']]['MonStatsEx']];assert r==dict(Source=r['Source'],Large=bool(row.get('large',0)),Small=bool(row.get('small',0)))
 for family in ['curse','lifecycle']:
  proof=ROOT/f'tools/reference/grim-ward-{family}-source-evidence.json';p=e.load(proof)
  # These are unchanged historical,service-bound source comparisons. Their
  # current native execution is in this cohort,not a modern code certificate.
  fixture=BASE/f'grim-ward-{family}-source-probe.json';assert fixture.exists()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==8 and all(r['exitCode']==0 for r in commands)
 log=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in log and '\nOK\n'in log
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==201 and not status['completedClasses']
 ward=next(s for s in status['skills']if s['talentId']=='barbarian.grimward');assert ward['implementation']=='handler-present'and ward['verification']=='partial-native-coverage'and not ward['complete']
 files=e.stage_files(STAGE,WIDE)+[ROOT/f'tools/reference/grim-ward-{n}-source-evidence.json' for n in ['curse','lifecycle']]+[decision,data,monsters,modes,ROOT/'tools/reference/inferno-held-input-client-evidence.json',Path(__file__).resolve()]
 for folder in ['failed-1','failed-3','prepared-pass-2']:files.extend(p for p in (STAGE/folder).rglob('*')if p.is_file())
 files += [ROOT/p for p in ['tests/check_grim_ward_actor.py','tests/check_frenzy.py','tests/export_runtime_audits.py','tools/build_grim_ward_species.py','tools/build_skill_roadmap.py','tools/review_inferno_held_client.py']]
 files += [ROOT/'docs'/p for p in ['grim-ward-actor-checkpoint.md','grim-ward-context-proposal.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,currentExecutableContextParity=False,approvedContext=True,
  handlers=201,completedClasses=[],sourceSnapshot=3,contentBuild=2,integrationBuild=3,focusedRun=4,retainedRun=1,
  focusedMarkers=39,retainedMarkers=281,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,speciesRows=751,
  legacyCurseCasesPerBoot=31134,legacyLifecycleCasesPerBoot=3144,pythonTests=71,verificationCommands=8,auditFingerprints=fingerprints,
  protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Approved context captures effective Ward rank/hard FindPotion at release,uses totem-centered pulses and caster-directed initial shared Terror. Prepared source services retain their explicit legacy scope.',
   'Learned start33 spends4mana once through shared SC timing. Cancellation does not refund;release claims its token once,reserves the actual soft corpse and preserves loot. Right/quick packets and natural startup are exercised on every boot.',
   'Source size flags,shared free-cell search,distinct active marker,first/last pulse and stop expiry,physical immunity reduction,captured ranks,offline/foreign-facet suppression and no replay are checked. Original paid actor/owner life and mana,body identity,used corpse and loot survive two reloads.',
   'All254parent markers retain. All573named decisions stay unchanged. The migration-only verifier sort leaves all client/patch/shared/binary hashes unchanged except its own script hash.'],
  limits=['Current executable context/callback/clock and exact cursor/h2h input are unverified. Native ground/occupancy/room/two-subtile scale are port adapters;blocked native ground refuses. Lag processes the latest due pulse without replay bursts.',
   'Nonattack other_animrate,full species/multiplayer/PvP/owner/history and rendered client acceptance remain open. The saved actor birth epoch is explicitly held for inspection;natural startup and ordinary duration boundaries are separate.',
   'No class completion,deployment,final effects/audio or talent/hotkey icons. Diagnostics preserve the wrong-facet fixture,missing oracle,stale pre-audit tests and directory-order verification failure.'])
 e.write('grim-ward-actor-native-evidence.json',result,files,check)
 print('Approved learned Ward:39focused/281retained;two reloads,right/quick natural startup;751species,71Python,201handlers,no class complete;working build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
