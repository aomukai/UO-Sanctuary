#!/usr/bin/env python3
"""Bind approved Frenzy, shared paired contact, exact respec and native evidence."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'frenzy';WIDE=BASE/'frenzy-retained';PARENT='9efb2eeb8132866239da334ee39565a27c60ffba'
def record(check=False):
 snapshot=STAGE/'source-snapshot-4.json';born=e.inventory(STAGE,WIDE,snapshot);now=e.load(snapshot)['inputs'];old=e.load(BASE/'entropy-clouds/source-snapshot-4.json')['inputs']
 assert now['sourceInputs'].keys()-old['sourceInputs'].keys()=={'src/Sanctuary.Content/BarbarianPairedMelee.cs','src/Sanctuary.Content/BarbarianFrenzy.cs','src/Sanctuary.Content/BarbarianFrenzyEffects.cs','tests/Sanctuary.Integration/FrenzyChecks.cs'}
 assert {p for p,h in old['sourceInputs'].items()if p.startswith(('src/','shared/'))and now['sourceInputs'][p]!=h}=={'src/Sanctuary.Content/ActionTiming.cs','src/Sanctuary.Content/BarbarianCombat.cs','src/Sanctuary.Content/BarbarianDoubleSwing.cs','src/Sanctuary.Content/ClassCombat.cs','src/Sanctuary.Content/PlayerWeaponContact.cs','src/Sanctuary.Content/CharacterProfiles.cs'}
 assert now['projectInputs']==old['projectInputs'] and now['copiedData'].keys()==old['copiedData'].keys()
 assert {p for p,h in old['copiedData'].items()if now['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
 ds=Counter({'double-swing-phase-check':3,'barbarian-cast-clock-check':3,'double-swing-sequence-check':1,'double-swing-sequence-reload-check':2,'weapon-hands-check':1,'weapon-hands-reload-check':2,'double-swing-check':1,'double-swing-reload-check':2,'barbarian-binding-check':1,'barbarian-binding-reload-check':2})
 delta=Counter({'frenzy-release-check':3,'frenzy-check':1,'frenzy-reload-check':2})
 for mode in ['left','right','quick']:
  ds.update({'double-swing-'+mode+'-check':3,'double-swing-'+mode+'-result':3})
  delta.update({'frenzy-'+mode+'-check':3,'frenzy-'+mode+'-result':3})
 e.cohort(STAGE,base+ds+delta,69)
 prior=BASE/'entropy-clouds-retained/birth-native-checks.log';parent=e.load(ROOT/'tools/reference/entropy-clouds-native-evidence.json');assert e.sha(prior)==parent['files'][str(prior.relative_to(ROOT))]
 retained=markers(prior);assert sum(retained.values())==230;e.cohort(WIDE,retained+delta,254)
 for stage,run in [(STAGE,2),(WIDE,1)]:
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
  log=(stage/'birth-native-checks.log').read_text();assert log.count('Approved Frenzy:paid independent normal/throwing hands')==3 and log.count('Frenzy:15024complete legacy contexts')==3
  assert log.count('Approved Double Swing:normal/throwing melee pairs')==3 and "denied by connection filter 'auto-denylist'"not in log
  s=e.load(stage/'frenzy-fixtures.json');assert s['Generation']==2 and s['Mana']%256==128 and s['Speed']['Charges']==1 and s['Speed']['Owner']==s['Owner'] and len({s['First'],s['Second']})==2
 for name in ['content-build-4.log','integration-build-2.log']:
  log=(STAGE/name).read_text();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 failed=STAGE/'failed-1';f=e.load(failed/'birth-inputs.json');assert e.sha(failed/'content/Sanctuary.Content.dll')==f['artifacts']['Assemblies/Sanctuary.Content.dll'] and e.sha(failed/'integration/Sanctuary.Integration.dll')==f['artifacts']['Assemblies/Sanctuary.Integration.dll']
 assert 'Cancellation refunded/charged:respec'in(failed/'birth-native-checks.log').read_text()
 assert 'int hits = player.Hits, mana = player.Mana, stam = player.Stam;'in(failed/'src/Sanctuary.Content/CharacterProfiles.cs').read_text()
 reference=ROOT/'tools/reference/frenzy-rule-reference.json';r=e.load(reference);assert r['referenceVersion']=='3.3.93847'and r['approved']and not r['currentExecutableStartupParity'] and r['sharedSequence']['sequence']==11
 for p,h in r['inputs'].items():assert e.sha(ROOT/p)==h,p
 legacy=ROOT/'tools/reference/frenzy-release-source-evidence.json';r=e.load(legacy);assert r['cases']==15024 and sum(r['byFamily'].values())==15024
 assert e.sha(ROOT/r['fixture']['path'])==r['fixture']['sha256']
 for p,h in r['artifacts'].items():assert e.sha(ROOT/p)==h,p
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==8 and all(r['exitCode']==0 for r in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 protected['acceptedClientPatches']=41
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==200 and not status['completedClasses']
 s=next(s for s in status['skills']if s['talentId']=='barbarian.frenzy');assert s['implementation']=='handler-present'and s['verification']=='partial-native-coverage'and not s['complete']
 files=e.stage_files(STAGE,WIDE)+[reference,legacy,ROOT/'tools/reference/frenzy-start-decision.json',Path(__file__).resolve()]
 files += [p for p in failed.rglob('*')if p.is_file()]
 for k in ['Content','Integration']:files.append(STAGE/k.lower()/f'Sanctuary.{k}.dll')
 files += [ROOT/p for p in ['tests/check_frenzy.py','tests/check_entropy_clouds.py','tests/check_double_swing_sequence.py','tests/check_iron_golem_birth.py','tools/review_frenzy_rules.py','tools/build_skill_roadmap.py']]
 files += [ROOT/'docs'/p for p in ['frenzy-checkpoint.md','frenzy-startup-proposal.md','frenzy-release-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md','skill-mechanical-reference.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,approvedStartup=True,currentExecutableStartupParity=False,handlers=200,completedClasses=[],sourceSnapshot=4,contentBuild=4,integrationBuild=2,focusedRun=2,retainedRun=1,auditRun=2,focusedMarkers=69,retainedMarkers=254,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,legacyFrenzyCases=15024,retainedSq11Comparisons=1890,cancellationCases=11,respecResourceScenes=2,realInputModes=['left','right','quick'],naturalExpiryPerBoot=True,pythonTests=71,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Approved learned Frenzy shares the paired melee/SQ11/hand/geometry/GUID/contact engine with retained Double Swing. Per-release1.5mana includes misses and blocks;underfunded contacts use ordinary damage without new charges. Successful unblocked committed contacts include full absorption.',
   'Pinned charge curves feed actual shared walking/running/attack-rate consumers. Effective rank caps each gain;hard IncreasedStamina sets UTC duration. Stop/movement/weapon swaps preserve earned speed;death/respec/expiry remove it. Hard DoubleSwing/Taunt damage and Berserk magic conversion reuse ordinary services.',
   'Normal/throwing pairs,paid hands,miss/block/absorbed hits,gear caps,physical immunity/conversion,fallback,eleven cancellations,three actual input modes and natural six-second expiry pass. Original pair/gear/durability/wounds/resources and an explicitly held saved speed state survive two reloads without replay.',
   'The initial respec failure exposed whole-point resource capture clearing fractional mana. Shared respec now preserves exact life/mana/stamina capped to new maxima without refill;two actual attribute-reset scenes cover fractions and lower maxima. All230parent markers retain.'],
  limits=['Current start67,exact current charge chronology,paired IAS/WSM quirks,full world/PvP/client and unsupported item/recipient domains remain unverified. The approved contract is explicit and does not certify the old service-bound release oracle as modern code.',
   'Events are planned at sequence entry;new speed feeds subsequent plans. Existing buffs are updated/capped on successful gain rather than silently recomputed on every gear change. The saved-state fixture extends expiry only for retention;natural expiry remains independently tested.',
   'The first Content compile missed a namespace import;the first native run failed preexisting fractional respec. Both are preserved. Content3is an unchanged incremental build after an edit-script assertion;only final Content4/Integration2and snapshot4are accepted.',
   'Installed106artifacts/38saves/20configs and accepted41client patches remain unchanged. Final weapon/speed-charge effects/audio and talent/hotkey icons remain separate identified placeholders;no class complete or deployment.'])
 e.write('frenzy-native-evidence.json',result,files,check)
 print('Approved Frenzy:69focused/254retained,2reloads,3input modes/natural expiry;15024legacy cases;fractional respec fixed;71Python;200handlers/no class complete;installed world preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
