#!/usr/bin/env python3
"""Authenticate current Death Mark composition without claiming world admission."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/death-mark-cast'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 source=ROOT/'tools/reference/death-mark-cast-current-evidence.json';proof=json.loads(source.read_text())
 assert proof['cases']==122 and proof['sourceBytes']==1431 and proof['skillId']==375 and proof['dispatcherIndexLinked']and proof['currentMachineCodeExecutedOffline']
 assert not proof['processMemoryRead']and not proof['targetProcessCodeExecuted']
 for rel,h in proof['artifacts'].items():assert sha(ROOT/rel)==h,rel
 capture=ROOT/'.local/class-skills/warlock-runtime-reference/death-mark-callback-20261108-initial.json';c=json.loads(capture.read_text())
 assert c['callbackRva']=='0x44aa70'and c['sourceReferencesVerified']and not c['allUnwindFragmentsCaptured']and not c['callbackSemanticsVerified']
 assert len(c['fragments'])==3 and all(f['permissions']=='---s'and not f['captured']for f in c['fragments'])and c['bytesRead']==560
 assert not any(c[k]for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
 for rel,h in c['artifacts'].items():assert sha(ROOT/rel)==h,rel
 latest=ROOT/'.local/class-skills/warlock-runtime-reference/death-mark-callback-20261108-after-user-cast.json';current=json.loads(latest.read_text())
 assert current['allUnwindFragmentsCaptured']and current['callbackRva']=='0x44aa70'and current['sourceReferencesVerified']
 assert sum(int(f['endRva'],16)-int(f['rva'],16)for f in current['fragments'])==546 and all(f['captured']and f['invalidInstructionMarkers']==0 for f in current['fragments'])
 helpers=ROOT/'.local/class-skills/warlock-runtime-reference/callees-death-mark-pet-services-20261108.json';services=json.loads(helpers.read_text())
 for proof in [current,services]:
  for rel,h in proof['artifacts'].items():assert sha(ROOT/rel)==h,rel
 byRva={r['rva']:r for r in services['records']};assert all(byRva[r]['allUnwindFragmentsCaptured']for r in ['0x3739a0','0x425550','0x27ab60'])and not byRva['0x398a80']['allUnwindFragmentsCaptured']
 placement=ROOT/'tools/reference/generic-pet-monster-placement-source-evidence.json';reuse=json.loads(placement.read_text());assert reuse['cases']==5229
 assert all(f['sha256']in reuse['artifacts'].values()for f in byRva['0x3739a0']['fragments'])
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==12 and ': failed'not in log and ' ERR]'not in log
 for name in ['iron-golem-birth','iron-golem-items','npc-resource-callbacks','death-mark-cast']:
  if name=='iron-golem-birth':assert counts[name+'-check']==3
  else:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
 for kind in ['content','integration']:assert all(x in(STAGE/f'{kind}-build-2.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-4.json').read_text());assert native['copiedData']==snap['inputs']['copiedData']
 for rel,h in native['sourceInputs'].items():assert sha(ROOT/rel)==h,rel
 for rel,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==h,rel
 assert native['copiedData']==json.loads((ROOT/'.local/class-skills/sigil-family/birth-inputs.json').read_text())['copiedData']
 assert int((STAGE/'death-mark-cast-fixtures.json').read_text())==2
 inventory=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[s for s in inventory['skills']if s['implementation']=='handler-present'];assert len(handlers)==211 and inventory['completedClasses']==[]
 import build_skill_roadmap
 assert build_skill_roadmap.build()==inventory,'Generator would downgrade accepted verification'
 paths=['tools/build_skill_roadmap.py','src/Sanctuary.Content/DeathMarkCast.cs','src/Sanctuary.Content/GenericPetDecisions.cs','tests/Sanctuary.Integration/DeathMarkCastChecks.cs','tests/check_death_mark_cast.py','tools/probe_death_mark_cast.py','tools/capture_death_mark_callback.py','tools/record_death_mark_cast.py','tools/reference/death-mark-cast-current-evidence.json']
 result=dict(referenceVersion='3.3.93847',handlers=211,warlockHandlers=2,completedClasses=[],learnedDeathMarkEnabled=False,currentExecutableParity=False,
  sourceCases=122,sourceBytes=1431,sourceProofSha256=sha(source),nativeMarkers=12,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=2,sourceSnapshot=4,nativeRun=2,newSourceCompilerMatrices=0,priorCompilerMatricesRerun=0,
  artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in paths},preservation=preservation(),
  scope='Prepared translation of complete current do155 for DeathMark375. Native C# matches all122 captured-machine-code returns,flags and ordered external calls/directives/state requests on each boot. Shared GenericPetDecisionUnit supplies source coordinate widths. Cases include own row binding/effective ranks,town and level gates,two separate range evaluations and signed overflow,typed/all-pet iteration,clear-control directive,remaining-count condition,failed state allocation after pet commands,bind/aura/stat350/351/202order and explicit source assertion boundaries. Full target identity is separate from a numeric GUID that can be reused across unit types. No native Death Mark world actor,debuff,teleport or learned input is created by these pure checks.',
  initialCallbackGap=dict(capture=str(capture.relative_to(ROOT)),sha256=sha(capture),rva='0x44aa70',all3FragmentsProtected=True,semanticsVerified=False,process=c['process'],bytesRead=560),
  currentCallbackReference=dict(capture=str(latest.relative_to(ROOT)),sha256=sha(latest),rva='0x44aa70',sourceBytes=546,allFragmentsCaptured=True,callbackTranslationVerified=False,helperCapture=str(helpers.relative_to(ROOT)),helperSha256=sha(helpers),completeHelpers=['0x3739a0','0x425550','0x27ab60'],protectedHelper='0x398a80',placementReusesExistingSourceCases=5229,placementProofSha256=sha(placement),userAction='User confirmed casting Death Mark;new bounded capture afterward established availability;no automated game input'),
  roadmapRepair='Generator now retains accepted partial-native coverage/caveats for learned Shadow Warrior,Shadow Master and Lethargy. Accepted status JSON is byte-identical;regenerated roadmap is current and --check passes.',
  diagnostic='Initial121case C1/I1/native1passed12 markers and is archived. Review then found numeric GUID equality could misidentify a different unit type as the caster. Explicit IsCaster identity and typed state target retain source pointer identity;the122nd current-code case covers the alias. Signed row field types also retain byte/word widths. FinalC2/I2/native2passes12 markers. No old compiler matrix or unrelated retained cohort was rerun.',
  correction='The prior Sigil next-action suggestion incorrectly associated MiasmaBolt with do155/poison. Owned row395 has ordinary srvmissile launch and magic damage;do155 belongs to DeathMark375. Miasma hit41/cloud-do41/world/mastery gaps remain explicit;no poison substitute was implemented.',
  remaining=['Callback44aa70 became readable after the user cast Death Mark;all546bytes/3fragments are preserved. Translate/compare its relocation,overlay,target-command,idle and21/1/11state clearing next. It is not yet connected to this composition. Existing1128byte monster-placement helper matches retained5229case proof;reuse it. Target-command398a80 remains protected;idle204byte/overlay326byte bodies are captured but unqualified.','Actual original demon roster/owner/facet/placement/state binding and recipient stat/duration/replacement consumers need native world acceptance.','Startup requirespettype,localdelay20,ordinary SC cancellation/payment and learned left/right/quick routes remain unfinished. Prepared composition does not add another handler.','Death Mark cast,pet warp/landing overlay,target mark/aura/expiry and audio remain final asset work;talent/hotkey icons stay in separate inventory.'])
 p=ROOT/'tools/reference/death-mark-cast-native-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Death Mark composition:122 current-code cases per boot,12 native markers/2reloads;actual pet callback/world/payment/learned route still gated.211handlers;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
