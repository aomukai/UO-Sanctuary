#!/usr/bin/env python3
"""Authenticate actual do155 world effects and approved recipient persistence."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/death-mark-world-release'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 approval=ROOT/'tools/reference/death-mark-recipient-approval.json';assert json.loads(approval.read_text())['approvedByUser']
 source=json.loads((ROOT/'tools/reference/death-mark-cast-current-evidence.json').read_text());assert source['cases']==122
 for p,h in source['artifacts'].items():assert sha(ROOT/p)==h,p
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log))
 assert sum(counts.values())==21 and ': failed'not in log and ' ERR]'not in log
 assert counts['iron-golem-birth-check']==3
 for family in ['iron-golem-items','npc-resource-callbacks','death-mark-debuff','death-mark-cast','death-mark-world-release']:
  assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
 assert counts['death-mark-world-release-check-result']==1 and counts['death-mark-world-release-reload-check-result']==2
 assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-1.log').read_text()
 for kind in ['content','integration']:assert all(s in(STAGE/f'{kind}-build-1.log').read_text()for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-1.json').read_text())
 assert native['copiedData']==snapshot['inputs']['copiedData']
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 f=json.loads((STAGE/'death-mark-world-release-fixtures.json').read_text())
 assert f['Generation']==2 and f['Mark']['Rank']==255 and f['Mark']['CastId']==654321
 assert len(f['Pets'])==4 and len({p['Serial']for p in f['Pets']})==4
 assert {json.loads(p['Birth'])['Profile']for p in f['Pets']}=={'prepared-goatman-life-owner-v1','prepared-tainted-life-owner-v1','prepared-defiler-life-owner-v1'}
 # Ground directive type0 skips the callback's setter;it retains prior command.
 assert all(p['Command']['Type4c']==2 and p['Command']['Target48']==f['Target']for p in f['Pets'][:3])
 assert f['Pets'][3]['Command']['Type4c']==0 and f['Pets'][3]['Command']['Target48']==-1
 assert json.loads((STAGE/'death-mark-debuff-fixtures.json').read_text())['Generation']==2
 assert (STAGE/'death-mark-cast-fixtures.json').read_text()=='2'
 paths=['src/Sanctuary.Content/'+n+'.cs'for n in ['DeathMarkWorldRelease','DeathMarkCast','DeathMarkWorld','DeathMarkDebuffs']]+[
  'tests/Sanctuary.Integration/DeathMarkWorldReleaseChecks.cs','tests/Sanctuary.Integration/DeathMarkWorldChecks.cs','tests/check_death_mark_world_release.py','tools/record_death_mark_world_release.py']
 result=dict(referenceVersion='3.3.93847',handlers=211,warlockHandlers=2,completedClasses=[],learnedDeathMarkEnabled=False,automaticGenericPetAiEnabled=False,currentFullWorldParity=False,
  approval=dict(path=str(approval.relative_to(ROOT)),sha256=sha(approval)),nativeMarkers=21,markers=dict(counts),boots=3,reloads=2,saves=3,
  contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,priorCompilerMatricesRerun=0,retainedCurrentCastCases=122,pinnedRecipientRanks=255,
  files={p:sha(ROOT/p)for p in paths},artifacts=native['artifacts'],preservation=preservation(),
  scope='Current do155 composition now performs real native relocation and commands on the original Goatman,Tainted,Defiler and binds the approved recipient state. Supplied fourth original actor tests the shared three-pet quota without learned summon-cap admission. Empty roster,source range and duplicate roster controls;weaker mark allocation refusal returns0 after completed pet moves without rollback;ground-only relocation leaves prior enemy command and existing target mark unchanged. Skill350/rank351 are the bound effect identity;opaque castid202 is written last and saved. Original demon serials,birth snapshots,wounds,maxlife,mana,order,source commands and recipient natural deadline/castid survive two actual reloads and support further ground relocation. Retains all approved debuff controls and122current cast comparisons per boot plus required original-item/resource checks.',
  compatibility='DeathMarkDebuff gains optional CastId default0;old JSON without it deserializes. No persistence version reset or artificial lifetime hold. Source202value is preserved opaquely,not inferred from native clocks.',
  limits=['Caller must supply qualified source cast context,room/level/target services and ordered registered original demon roster. Native D2level IDs,levelbyte14,pet iteration order and birth/controller paths are not invented.','Automatic controller command consumption/neutral/action interruption,requirespettype startup,localdelay/payment/cancellation and left/right/quick admission remain unfinished.','Prepared recipient scope is hostile ordinary NPC;current numeric binder remains protected and uses explicit approved adaptation. No PvP or full current world parity claim.','Existing target/landing placeholders and separate final effects/audio/icons remain pending.'])
 path=ROOT/'tools/reference/death-mark-world-release-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print('Death Mark world release:21 native markers/2 actual reloads;original actors,quota,partial failure,approved recipient and cast metadata verified.211handlers;learned route gated.')
if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
