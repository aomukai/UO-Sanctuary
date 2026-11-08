#!/usr/bin/env python3
"""Authenticate original live demon placement,commands,control managers and saves."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/death-mark-world'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 for name,expected in [('death-mark-pets-current-evidence',97),('death-mark-cast-current-evidence',122)]:
  proof=json.loads((ROOT/f'tools/reference/{name}.json').read_text());assert proof['cases']==expected
  for rel,h in proof['artifacts'].items():assert sha(ROOT/rel)==h,rel
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==30 and ': failed'not in log and ' ERR]'not in log
 for name in ['iron-golem-birth','pet-path-targets']:assert counts[name+'-check']==3
 for name in ['iron-golem-items','npc-resource-callbacks','goatman-body','pet-native-placement','death-mark-world','death-mark-pets','death-mark-cast']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 assert counts['death-mark-world-check-result']==1 and counts['death-mark-world-reload-check-result']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-1.log').read_text()
 for kind,n in [('content',2),('integration',1)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-3.json').read_text());assert native['copiedData']==snap['inputs']['copiedData']
 for rel,h in native['sourceInputs'].items():assert sha(ROOT/rel)==h,rel
 for rel,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==h,rel
 assert native['copiedData']==json.loads((ROOT/'.local/class-skills/death-mark-pets/birth-inputs.json').read_text())['copiedData']
 fixture=json.loads((STAGE/'death-mark-world-fixtures.json').read_text());assert fixture['Generation']==2 and len(fixture['Entries'])==3 and len({e['Body']for e in fixture['Entries']})==3
 assert all(e['Command']['Type4c']==2 and e['Command']['Mode']==1 and e['Command']['Target48']==fixture['Target']for e in fixture['Entries'])
 assert {json.loads(e['State'])['Profile']for e in fixture['Entries']}=={'prepared-goatman-life-owner-v1','prepared-tainted-life-owner-v1','prepared-defiler-life-owner-v1'}
 inventory=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(r['implementation']=='handler-present'for r in inventory['skills'])==211 and inventory['completedClasses']==[]
 paths=['src/Sanctuary.Content/'+n+'.cs'for n in ['DeathMarkWorld','WarlockDemonPlacement','GenericPetNativePlacement','GenericPetPlacementWorld','ColdStates','FreezeStates']]+['tests/Sanctuary.Integration/DeathMarkWorldChecks.cs','tests/check_death_mark_world.py','tools/record_death_mark_world.py']
 result=dict(referenceVersion='3.3.93847',handlers=211,warlockHandlers=2,completedClasses=[],learnedDeathMarkEnabled=False,automaticGenericPetAiEnabled=False,currentExecutableParity=False,
  nativeMarkers=30,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=1,sourceSnapshot=3,nativeRun=1,newSourceCompilerMatrices=0,priorCompilerMatricesRerun=0,retainedCurrentCases=dict(callback=97,command=165,cast=122),
  artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in paths},preservation=preservation(),
  scope='Live prepared original Goatman,Tainted,Defiler use the shared source native placement/room graph. An explicit controller-path registration preserves original body/owner/source identity and adds saved command48/4c,mode and next-AI request. Callback moves actual native actors once,clears managed source21stun/1freeze/11cold only when requested,and leaves poison/fear/ordinarycurse,wounds,mana,birth snapshots,owner/order unchanged. Both clear=false and true are tested per species;original source paths/commands/clock/body identity survive two actual reloads and resume relocation. Exhausted quota and invalid destination preserve graph/commands;duplicate/missing/invalid command-image restore refuses atomically. Old Goatman body/native placement,path reset,384 current-code cases and required item/resource controls remain passing.',
  worldProjection='GenericPetNativePlacement now admits exactly the3original prepared demon types and validates their logical source IDs. Existing native terrain/room/collision projection is reused,not replaced with D2 map parity. Registration requires a caller-supplied live controller path;this does not recover or generate a D2birth path. New profile246 stores source placement graph and command metadata. No automatic controller/timer is started;neutral/next-AI requests are retained for that future consumer.',
  controlProjection='The live managed status backend exposes its one active source stun/freeze/cold manager per state as the callback lists. Removal uses CombatStuns.Clear and shared ColdStates.Remove/FreezeStates.Remove;freeze follows normal removal/AI-delay settlement. These operations do not settle poison or grant regeneration. Legacy untagged freeze is explicitly unsupported before relocation. Native raw-stat allocators/duplicates and exact client effects are not claimed by this managed projection.',
  persistence='No artificial retention hold was used. Original three demon snapshots,body serials,FP256life,maxlife,native mana,pet order,source path/room graph,target command and natural40msAI-request timestamp are compared through both reloads. The request may be past due;it is not replayed by this prepared bridge. Poison is cured only in fixture cleanup after the immediate preservation assertion,so unrelated background damage cannot invalidate the original-life persistence control.',
  remaining=['Actual GenericPet controller/AI consumption,active intrinsic action cancellation and full event-list neutral transition still need world admission. This bridge stores the source neutral/next-AI request and keeps native fallback AI stopped.','Connect actual recipient armor/normal/magic damage-reduction state,duration/replacement and requirespettype/localdelay20/SCpayment/cancellation/left-right-quick input before learned DeathMark.','Full current-client/room/footprint/allocator parity and interrupted/retiring controller contexts remain scoped;no class certificate.','Final warp/landing/mark/cast/expiry/audio and talent/hotkey icons remain separately tracked;live landing effect0x3728 is an identified placeholder.'])
 p=ROOT/'tools/reference/death-mark-world-native-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Death Mark world:30 native markers/2reloads;three original species move,clear exact managed controls and preserve original body/path/commands/resources.211handlers;automatic AI/learned route still gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
