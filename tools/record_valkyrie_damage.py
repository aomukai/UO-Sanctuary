#!/usr/bin/env python3
"""Bind native original damage, inherited ranks and changed shared regressions."""
import argparse,json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-damage';WIDE=BASE/'valkyrie-damage-retained';SHARED=BASE/'valkyrie-damage-shared';SOURCE=BASE/'valkyrie-damage-source';RANK=BASE/'npc-innate-skill-rank-source'
PARENT='f6912231bddab84ebbdcae22309cf5d777ce4432'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-2.json');old=e.load(ROOT/'tools/reference/valkyrie-weapons-native-evidence.json');extra=Counter({'valkyrie-damage-check':1,'valkyrie-damage-reload-check':2})
 for previous,stage,count,run in [(BASE/'valkyrie-weapons',STAGE,51,2),(BASE/'valkyrie-weapons-retained',WIDE,359,1)]:
  path=previous/'birth-native-checks.log';assert e.sha(path)==old['files'][str(path.relative_to(ROOT))];e.cohort(stage,markers(path)+extra,count)
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
  log=(stage/'birth-native-checks.log').read_text();assert log.count('Valkyrie damage:8192complete NPC rank controls;171original bodies/990items;1710packets per boot/5130across actual reloads')==3
  saved=e.load(stage/'valkyrie-damage-fixtures.json');assert saved['Generation']==2 and len(saved['Bodies'])==171 and sum(len(h['Items'])for h in saved['Bodies'])==990
  assert len({i for h in saved['Bodies']for i in h['Items']})==990
  for h in saved['Bodies']:
   body=json.loads(h['Text']);rng=h['Random'];binding=body['EquipmentWeapons'];assert body['Owner']==rng['Owner']==saved['Owner']and rng['Actor']==h['Body']and rng['Item']==binding['Primary']
   assert rng['ValkyrieBinding']==binding['Identity']and body['Birth']['Critical']in [0,1,20]and h['Pools'][0]>0
   for text in h['Payloads']:
    item=json.loads(text);assert item['Equipment']['Actor']==h['Body']and item['Equipment']['Owner']==saved['Owner']and item['Metadata']['Flags']==0x82010
 shared=e.load(SHARED/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',SHARED/'birth-inputs.json');assert shared['artifacts']==born['artifacts']and shared['copiedData']==born['copiedData']
 for p,h in shared['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
 oldshared=e.load(ROOT/'tools/reference/npc-damage-fill-native-evidence.json');path=BASE/'npc-damage-fill-compact/birth-native-checks.log';assert e.sha(path)==oldshared['files'][str(path.relative_to(ROOT))]
 log=e.cohort(SHARED,markers(path),60);assert log.count('NPC full damage fill:5120source contexts;96original-item bridge exports;')==3
 assert 'pass across3boots/2reloads'in(SHARED/'native-run-1.log').read_text()
 bridge=e.load(SHARED/'bridge-evidence.json');assert bridge['contexts']==96 and bridge['generation']==2 and not bridge['currentExecutableParity']
 for p,h in bridge['artifacts'].items():assert e.sha(ROOT/p)==h,p
 assert not(SHARED/'bridge-sanitizer.log').read_text()
 for name in ['content-build-1.log','integration-build-2.log']:
  text=(STAGE/name).read_text();assert all(n in text for n in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 before=e.load(STAGE/'source-snapshot-1.json')['inputs'];after=e.load(STAGE/'source-snapshot-2.json')['inputs'];assert before['copiedData']==after['copiedData']and before['projectInputs']==after['projectInputs']
 assert {p for p,h in before['sourceInputs'].items()if after['sourceInputs'][p]!=h}=={'tests/Sanctuary.Integration/ValkyrieDamageChecks.cs'}
 failed=STAGE/'failed-1'
 for p,h in e.load(failed/'inventory.json').items():assert e.sha(failed/p)==h,p
 assert 'Inherited own-item/Command passive rank/value mismatch'in(failed/'birth-native-checks.log').read_text()and e.sha(failed/'Sanctuary.Content.dll')==born['artifacts']['Assemblies/Sanctuary.Content.dll']
 proofs=[ROOT/'tools/reference/valkyrie-damage-source-evidence.json',ROOT/'tools/reference/npc-innate-skill-rank-source-evidence.json'];proof,rank=map(e.load,proofs)
 assert (proof['bodies'],proof['originalLoadouts'],proof['originalItems'],proof['packets'],proof['packetsPerBoot'])==(171,57,990,5130,1710)
 assert len(proof['completeRoutineHashes'])==12 and rank['contexts']==8192 and len(rank['completeRoutineHashes'])==2
 for record in [proof,rank]:
  assert not record['currentExecutableParity']
  for group in ['inputs','artifacts']:
   for p,h in record[group].items():assert e.sha(ROOT/p)==h,p
 assert not(SOURCE/'sanitizer.log').read_text()and not(RANK/'sanitizer.log').read_text()
 cases=e.load(SOURCE/'cases.json');assert len(cases)==171 and sum(len(c['Packets'])for c in cases)==5130
 for held,c in zip(e.load(STAGE/'valkyrie-damage-fixtures.json')['Bodies'],cases):assert held['Random']['Seed']==int(c['Packets'][-1]['After'])
 observations=e.load(ROOT/'tools/reference/valkyrie-inherited-critical-observation.json');assert observations['reportedPercent']==25 and observations['distinguishingReading']['reportedPercent']==59
 assert next(r for r in observations['additionalUserReadings']if r['effectiveRank']==8)['reportedPercent']==54
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in text and'\nOK\n'in text
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 valk=next(s for s in status['skills']if s['talentId']=='amazon.valkyrie');assert valk['implementation']=='blocked'and not valk['complete']
 files=e.stage_files(STAGE,WIDE,SHARED)+proofs+[ROOT/'tools/reference/valkyrie-inherited-critical-observation.json',ROOT/'tools/probe_npc_innate_skill_rank.py',ROOT/'tools/probe_valkyrie_damage.py',ROOT/'tests/check_valkyrie_damage.py',ROOT/'tests/check_valkyrie_damage_shared.py',Path(__file__).resolve()]
 files.extend(p for folder in [failed,SOURCE,RANK]for p in folder.rglob('*')if p.is_file())
 files += [ROOT/'docs'/n for n in ['valkyrie-damage-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,currentExecutableParity=False,
  sourceSnapshot=2,contentBuild=1,integrationBuild=2,focusedRun=2,retainedRun=1,sharedRun=1,auditRun=2,focusedMarkers=51,retainedMarkers=359,sharedMarkers=60,
  bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,sourcePackets=5130,packetsPerBoot=1710,sourceRankContexts=8192,originalBodies=171,originalItems=990,sharedSourceContextsPerBoot=5120,sharedPhysicalRollsPerBoot=294840,sharedOriginalItemSourceBridge=96,
  pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Prepared original Valkyrie hand/full item graph and persisted actor RNG now feed existing full ordinary NPC melee allocation:all21fields,target armor and exact source seed continuation match across5130packets/two actual reloads;no raw item filtering or owner gear recapture.',
   'Shared NPC innate rank helper matches8192complete getter controls. Captured Critical Strike contributes through a read-only allocation view with own gear/Command;Dodge/Avoid/Evade values computed but defensive consumption remains separate. Unknown item-skill entries refuse before draw/target write;actual graph/pools stay unchanged. Current Critical12=59corroborates retained rounding;rank8=54remains recorded outlier.',
   'All356parent markers retain;additional original shared60marker cohort preserves5120full-fill contexts,294840physical rolls per boot and96original-item full-source bridge exports. Production DLL unchanged by fixture ordering repair;failed native/source generation/compiler evidence preserved.'],
  limits=['Twelve complete older fill/rank/RNG routines have explicit hand/stat-reader/formula/target services,not complete current executable parity. Derived passive view does not establish persistent passive-stat-list registration/refresh,all current skill ranks or current inherited dispatch. Starting seed provenance/world AI interleaving remains unresolved.',
   'Actual target metadata/accuracy,HP/status/proc/charge/avoidance delivery,nonzero Sunder,source rate/NecroPet AI/contact,current initializer and learned payment/placement/replacement/input remain unfinished. Full gameplay/client/multiplayer and final body/equipment,effects/audio/talent/hotkey artwork remain separate;no class complete or deployment.'])
 e.write('valkyrie-damage-native-evidence.json',result,files,check)
 print('Valkyrie damage:51focused/359retained/60shared,two reloads;5130packets,8192rank controls,96original source bridge;71Python,573unchanged decisions;202handlers,contact/cast still gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
