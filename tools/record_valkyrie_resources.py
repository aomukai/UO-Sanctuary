#!/usr/bin/env python3
"""Bind mandatory-context Valkyrie resource recipients and shared regressions."""
import argparse,json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-resources';WIDE=BASE/'valkyrie-resources-retained';SOURCE=BASE/'valkyrie-resources-source'
PARENT='2bf74acb5ba9626ce83285a7e5bd932f3e913073'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-7.json')
 old=e.load(ROOT/'tools/reference/valkyrie-numeric-native-evidence.json');extra=Counter()
 for n in ['generated-npc-live-resources','valkyrie-resources']:extra.update({n+'-check':1,n+'-reload-check':2})
 for previous,stage,count in [(BASE/'valkyrie-numeric',STAGE,45),(BASE/'valkyrie-numeric-retained',WIDE,353)]:
  path=previous/'birth-native-checks.log';assert e.sha(path)==old['files'][str(path.relative_to(ROOT))];e.cohort(stage,markers(path)+extra,count)
 for stage,run in [(STAGE,7),(WIDE,1)]:
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
  log=(stage/'birth-native-checks.log').read_text();assert "denied by connection filter 'auto-denylist'"not in log
  assert log.count('Valkyrie resources:228supplied contexts/1320original items;1824complete-source stages')==3
  assert log.count('Generated NPC live resources:5original recipients;source-matched birth')==3
  saved=e.load(stage/'valkyrie-resources-fixtures.json');assert saved['Generation']==2 and len(saved['Bodies'])==228
  assert sum(len(h['Items'])for h in saved['Bodies'])==1320 and len({i for h in saved['Bodies']for i in h['Items']})==1320
  for held in saved['Bodies']:
   body=json.loads(held['Text']);state=json.loads(held['Resource']);ctx=state['Valkyrie']
   assert state['Actor']==held['Body']and state['Owner']==saved['Owner']and ctx['Birth']==body['Birth']and ctx['Difficulty']==body['Difficulty']==0
   assert state['DamageRegeneration']==12 and state['StaminaPercentCache']==0 and body['EquipmentStatProfile']=='valkyrie-equipment-numeric-v1'
   assert set(state['ActiveItems'])==set(held['Items'])and body['Equipment']['Actor']==held['Body']
   assert held['Pools'][0]>0 and all(held['Pools'][i]<=held['Pools'][i+1]for i in [0,2,4])
   for serial,text in zip(held['Items'],held['Payloads']):
    item=json.loads(text);assert item['Equipment']['Actor']==held['Body']and item['Metadata']['Flags']==0x82010 and item['Equipment']['Flags']==0x1000 and item['Allocation']!=None
  assert any(h['Pools'][0]%256 for h in saved['Bodies'])and any(h['Pools'][2]%256 for h in saved['Bodies'])and any(h['Pools'][4]%256 for h in saved['Bodies'])
  assert e.load(stage/'generated-npc-live-resources-fixtures.json')['Generation']==2
 for name in ['content-build-5.log','integration-build-7.log']:
  text=(STAGE/name).read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 before=e.load(STAGE/'source-snapshot-4.json')['inputs'];after=e.load(STAGE/'source-snapshot-7.json')['inputs']
 assert before['copiedData']==after['copiedData']and before['projectInputs']==after['projectInputs']
 assert {p for p,h in before['sourceInputs'].items()if after['sourceInputs'][p]!=h}=={'tests/Sanctuary.Integration/ValkyrieResourceChecks.cs','src/Sanctuary.Content/GeneratedNpcStatGraph.cs'}
 failed=[]
 for i in range(1,7):
  folder=STAGE/f'failed-{i}';failed.append(folder)
  for p,h in e.load(folder/'inventory.json').items():assert e.sha(folder/p)==h,p
  text=(folder/'birth-native-checks.log').read_text();assert 'generated-npc-live-resources-check: passed'in text
  if i<5:assert 'valkyrie-resources-check: failed'in text
  elif i==5:assert 'Server did not reach'in(folder/'native-run-5.log').read_text()
  else:assert 'Writing world save snapshot done'in(folder/'native-run-6.log').read_text()and'valkyrie-resources-check: passed'in text
 assert 'case=0 variant=1 stage=1'in(failed[0]/'birth-native-checks.log').read_text()
 assert 'case=6 variant=0 stage=1'in(failed[1]/'birth-native-checks.log').read_text()
 assert 'case=0 variant=1 stage=5'in(failed[2]/'birth-native-checks.log').read_text()
 assert 'case=5 variant=0 stage=5'in(failed[3]/'birth-native-checks.log').read_text()
 assert e.sha(failed[3]/'Sanctuary.Content.dll')==e.sha(failed[4]/'Sanctuary.Content.dll')==e.sha(failed[5]/'Sanctuary.Content.dll')
 proofpath=ROOT/'tools/reference/valkyrie-resources-source-evidence.json';proof=e.load(proofpath)
 assert proof['contexts']==228 and proof['stagesPerContext']==8 and proof['originalItemReplays']==1320 and len(proof['completeRoutineHashes'])==30 and not proof['currentExecutableParity']
 for group in ['inputs','artifacts']:
  for p,h in proof[group].items():assert e.sha(ROOT/p)==h,p
 assert not(SOURCE/'sanitizer.log').read_text();cases=e.load(SOURCE/'cases.json');assert len(cases)==228 and all(len(c['Stages'])==8 and all(len(s)==3 for s in c['Stages'])for c in cases)
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 log=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in log and '\nOK\n'in log
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 valk=next(s for s in status['skills']if s['talentId']=='amazon.valkyrie');assert valk['implementation']=='blocked'and not valk['complete']
 files=e.stage_files(STAGE,WIDE)+[proofpath,ROOT/'tools/probe_valkyrie_resources.py',ROOT/'tests/check_valkyrie_resources.py',ROOT/'tools/build_skill_roadmap.py',Path(__file__).resolve()]
 files.extend(p for folder in [*failed,SOURCE]for p in folder.rglob('*')if p.is_file())
 files += [ROOT/'docs'/n for n in ['valkyrie-resources-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,currentExecutableParity=False,
  mandatorySuppliedResourceContext=True,sourceSnapshot=7,contentBuild=5,integrationBuild=7,focusedRun=7,retainedRun=1,auditRun=2,focusedMarkers=45,retainedMarkers=353,
  bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,sourceContexts=228,sourceStages=1824,originalResourceItems=1320,pythonTests=71,verificationCommands=7,
  auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Full original Valkyrie graph binds existing resource ledger only with a mandatory captured/supplied birth context. Native FP256 pools/maxima,source callback regeneration,item activation,buff and actual owner-facet transitions,owner gear snapshot,parking and original saves pass;old generic profile/JSON controls retain.',
   'All368scalar and occupied-layer stat-reader results compare with30complete older routines/3unchanged callback branches,including minimum rules. Exact raw original item graphs,metadata,requests and allocation witnesses compare separately;explicit-zero versus absent zero actor-cache slots are not a byte-ABI claim.',
   'Stateful operation1 stamina-percentage cache now persists;external percent writers run after the existing source item frame. Shared summon difficulty transition restores captured mana/stamina even when maxima stay equal,preventing native attribute-setter refills. All347parent cross-class markers retain.'],
  limits=['Zero/fractional/tiny/full starting pools and regeneration are explicit prepared inputs,not modern initializer defaults or world/RNG/allocation provenance. External percentage service is the source base-stat-writer context;complete modern aura/statlist ownership and outer callback parity are not certified.',
   'No weapon/rate/skill/charge/proc,inherited-passive,nonzero-sunder,NecroPet combat or paid casting/placement/replacement/local-delay/input admission. Full terrain/PvP/multiplayer/client and final model/effects/audio/talent/hotkey assets remain separate. No class complete or deployment.'])
 e.write('valkyrie-resources-native-evidence.json',result,files,check)
 print('Valkyrie resources:45focused/353retained,228contexts/1320originalitems,two reloads;71Python,573unchangeditem decisions;202handlers,initializer/combat/cast still gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
