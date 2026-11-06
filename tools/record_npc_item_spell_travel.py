#!/usr/bin/env python3
"""Bind natural original-NPC projectile contacts and resumed partial travel."""
import argparse,json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-item-spell-travel';WIDE=BASE/'npc-item-spell-travel-retained';ISOLATE=BASE/'npc-item-spell-travel-isolate'
PARENT='439d922da263dbe727b9bfa2468ebd8bbeb60dba'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-2.json');prior=e.load(ROOT/'tools/reference/npc-item-spell-flights-native-evidence.json');extra=Counter({'npc-item-spell-travel-check':1,'npc-item-spell-travel-reload-check':2})
 for previous,stage,count in [(BASE/'npc-item-spell-flights',STAGE,72),(BASE/'npc-item-spell-flights-retained',WIDE,380)]:
  p=previous/'birth-native-checks.log';assert e.sha(p)==prior['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
  assert 'pass across3boots/2reloads'in(stage/'native-run-1.log').read_text()
 base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2});e.cohort(ISOLATE,base+extra,12)
 snapshot(STAGE/'source-snapshot-2.json',ISOLATE/'birth-inputs.json');other=e.load(ISOLATE/'birth-inputs.json');assert other['artifacts']==born['artifacts']and other['copiedData']==born['copiedData']
 for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
 assert 'pass across3boots/2reloads'in(ISOLATE/'native-run-1.log').read_text()
 before=e.load(BASE/'npc-item-spell-flights/source-snapshot-3.json')['inputs'];after=e.load(STAGE/'source-snapshot-2.json')['inputs']
 assert before['copiedData']==after['copiedData']and before['projectInputs']==after['projectInputs']
 assert {p for p in set(before['sourceInputs'])|set(after['sourceInputs'])if before['sourceInputs'].get(p)!=after['sourceInputs'].get(p)}=={'tests/Sanctuary.Integration/NpcItemSpellTravelChecks.cs'}
 assert 'Production source/data/project inputs unchanged.'in(STAGE/'content-rebuild-comparison.log').read_text()
 assert born['artifacts']['Assemblies/Sanctuary.Content.dll']!=prior['artifacts']['Assemblies/Sanctuary.Content.dll']
 for stage in [STAGE,WIDE,ISOLATE]:
  s=e.load(stage/'npc-item-spell-travel-fixtures.json');assert s['Generation']==2 and len(s['Flights'])==133 and len(s['Items'])==7 and len(s['Targets'])==3 and s['Life']>0
  b=json.loads(s['Body']);binding=b['EquipmentWeapons']['Identity'];assert b['Owner']==s['Owner']and s['Random']['Actor']==s['Actor']and s['Random']['Owner']==s['Owner']and s['Random']['ValkyrieBinding']==binding
  resources=json.loads(s['Resources']);assert resources['Actor']==s['Actor']and resources['Owner']==s['Owner']and resources['Valkyrie']['RegenerationBase']==300
  assert Counter(f['Npc']['Packet']['Skill']for f in s['Flights'])=={38:5,48:64,44:64}
  assert Counter(f['Frame']for f in s['Flights'])=={0:4,7:1,1:128}and len({f['Id']for f in s['Flights']})==133
  assert {t['Skill']for t in s['Targets']}=={38,48,44}and len({t['Actor']for t in s['Targets']})==3 and all(t['Life']==65000*256 for t in s['Targets'])
  for f in s['Flights']:
   p=f['Npc']['Packet'];assert f['Owner']==s['Owner']and f['Npc']['Actor']==s['Actor']and f['Npc']['Binding']==binding and p['Rank']==f['Rank']==3
   assert p['FirePierce']==p['ColdPierce']==p['LightningPierce']==0
   if p['Skill']==38:
    assert len(f['Charged']['Path'])==39 and f['AccelerationCounter']==0
    if f['Frame']==7:assert f['Charged']['Next']>1 and(f['X']!=f['Charged']['OriginX']or f['Y']!=f['Charged']['OriginY'])
   else:assert f['Frame']==1 and f['AccelerationCounter']==1 and f['LastCollision']==s['Actor']and f['RadialIndex']in range(64)
  assert all(json.loads(item)['Equipment']['Actor']==s['Actor']for item in s['Items'])
  assert(stage/'birth-native-checks.log').read_text().count('Original NPC spell travel:five independent charged contacts and moving waypoint hit;Nova/Frost Nova natural multi-target/per-cast four-frame leases,independent simultaneous casts,exact child/actor RNG and HP;133partially travelled flights/original items/resources/wounds persist and resume real charged/ring hits through two reloads;proc callbacks/Chain/shield targets/AI/cast remain gated')==3
 for name in ['content-build-1.log','integration-build-1.log']:assert all(t in(STAGE/name).read_text()for t in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and'\nOK\n'in(STAGE/'python-tests.log').read_text()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 assert next(s for s in status['skills']if s['talentId']=='amazon.valkyrie')['implementation']=='blocked'
 routepath=ROOT/'tools/reference/npc-chain-routes-source-evidence.json';route=e.load(routepath)
 assert(route['cases'],route['rootCases'],route['childCases'],len(route['routineHashes']))==(11655,315,11340,2)
 for group in ['inputs','currentTable','artifacts']:
  for p,h in route[group].items():assert e.sha(ROOT/p)==h,p
 for p,h in route['sourceHashes'].items():assert e.sha(BASE/'d2moo-pinned-source'/p)==h,p
 files=e.stage_files(STAGE,WIDE,ISOLATE)+[Path(__file__).resolve(),ROOT/'tests/check_npc_item_spell_travel.py',routepath,ROOT/'tools/probe_npc_chain_routes.py']
 files+=[ROOT/p for p in route['artifacts']]
 files+=[BASE/'npc-chain-routes-source-run-1.log',BASE/'npc-chain-routes-source-run-check-1.log']
 files+=[ROOT/'docs'/n for n in ['npc-item-spell-travel-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,originalProcDispatch=False,currentExecutableParity=False,
  sourceSnapshot=2,contentBuild=1,integrationBuild=1,focusedRun=1,retainedRun=1,isolatedRun=1,auditRun=1,focusedMarkers=72,retainedMarkers=380,isolatedMarkers=12,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,savedPartialFlights=133,originalItems=7,naturalRingGroupsPerBoot=4,npcChainSourceCases=11655,npcChainSourceRoutines=2,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,productionSourceUnchanged=True,packagedDataUnchanged=True,binaryUnchanged=False,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Five charged children make independent once-only opening contacts;one separately advanced seven-frame bolt hits its next retained waypoint while siblings remain unmoved. Native NPC HP notifications and damage come from actual shared flight ticks.','Full Nova/Frost Nova one/two-cast cases hit east/north enemies,leave a far victim untouched,enforce four-frame per-cast delay and159/160ms boundaries,and permit simultaneous independent casts. Observed exact HP/projectile roll sequences and actor intrinsic RNG match;Frost retains actual emitter ownership.','133real partially travelled charged/ring flights and original actor/items/resources/wounds/RNG survive two real reloads. Loaded travel ages harmlessly first,then charged and both ring emissions resume actual HP contacts. All377parent retained markers remain.'],
  limits=['No new production code or data changed. The rebuilt assembly embeds a new Git revision and is separately native-tested;its binary hash differs,so byte identity is not claimed. Natural travel reuses existing kernels;the separate11655-case source proof executes two complete Chain do26/hit12 bodies with declared formula/search/allocation leaves and no runtime Chain or allocator RNG admission.','Bounded native composition,not every terrain/footprint/direction/rank or modern binary callback order. Original proc child-seed allocation/history,Chain Lightning,nonzero shield/player recipients and source-removal acceptance remain unfinished.','Inspection deadlines are held ahead after actual motion;normal lifetime/collision is unchanged. Initializer/world seed,AI/A1/startup/learned placement/payment/caps/replacement/input/client and final assets remain separate. No deployment or completed class.'])
 e.write('npc-item-spell-travel-native-evidence.json',result,files,check)
 print('Original NPC natural travel:72focused/380retained/12isolated markers,two reloads;133partial charged/ring flights resume actual HP,exact RNG and per-cast delays;202handlers,proc/Chain/cast gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
