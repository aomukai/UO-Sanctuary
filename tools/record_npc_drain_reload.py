#!/usr/bin/env python3
"""Bind the resolved cached-timer fixture error to exact production/native artifacts."""
import argparse,json,re,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/npc-drain-reload';FREEZE=ROOT/'.local/class-skills/npc-drain-reload-freeze';PARENT='727bfa9d514d681c995e8ae61d6d1ac8c5539031'

def families(names,extra=None):
 result=Counter(extra or{});result['iron-golem-birth-check']+=3
 for name in ('iron-golem-items','npc-resource-callbacks',*names):result[name+'-check']+=1;result[name+'-reload-check']+=2
 return result

def build(check=False):
 born=e.load(STAGE/'birth-inputs.json');old=e.load(ROOT/'.local/class-skills/npc-freeze-hit/birth-inputs.json')
 for path in (STAGE/'birth-inputs.json',FREEZE/'birth-inputs.json',STAGE/'audit-inputs.json'):
  data=e.load(path);snapshot(STAGE/'pre-build-2.json',path)
  assert data['artifacts']==born['artifacts']and data['copiedData']==born['copiedData']==old['copiedData']
  for name,h in data['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
 before={n:h for n,h in old['sourceInputs'].items()if n.endswith('.cs')};after={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
 assert before.keys()==after.keys()and{n for n in before if before[n]!=after[n]}=={'tests/Sanctuary.Integration/NpcFlatDrainChecks.cs'}
 parentpath=ROOT/'tools/reference/npc-freeze-hit-native-evidence.json';prior=e.load(parentpath)
 assert prior==json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/npc-freeze-hit-native-evidence.json'],cwd=ROOT))
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll','Assemblies/Sanctuary.Content.dll'):
  assert born['artifacts'][name]==old['artifacts'][name]==prior['artifacts'][name],name
 assert e.sha(STAGE/'content/Sanctuary.Content.dll')==born['artifacts']['Assemblies/Sanctuary.Content.dll']
 assert e.sha(STAGE/'integration/Sanctuary.Integration.dll')==born['artifacts']['Assemblies/Sanctuary.Integration.dll']
 assert all(t in(STAGE/'integration-build-2.log').read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 log=e.cohort(STAGE,families(('npc-equipment-elements','npc-equipment-poison','npc-equipment-critical','venom','monster-ordered-release','npc-flat-drains','revive-event','revive-native-swing'),{'monster-contact-check':3,'iron-golem-ordered-contact-check':3,'npc-flat-drains-timer-check':3,'npc-flat-drains-timer-result':3,'venom-result':1,'venom-reload-result':2,'revive-native-swing-result':3,'revive-native-swing-save-result':3}),54)
 assert log.count('constructor delay 50000000 ticks,current rate 36000000000 ticks;ordinary1025->1281,explicit no-recovery fixture1025->1025;')==3
 assert log.count('NPC flat drains:11664full-source contexts;12actual full-fill/native HP/resource contacts;')==3
 e.cohort(FREEZE,families(('npc-freeze-hit',)),12)
 saved=e.load(STAGE/'npc-flat-drains-fixtures.json');assert saved['Generation']==2 and [saved[n]for n in ('SourceLife','TargetLife','Mana','Stamina')]==[2281,49919,825,1025]
 for name in ('revive-event','revive-native-swing'):assert e.load(STAGE/(name+'-fixtures.json'))['Generation']==2
 assert e.load(FREEZE/'npc-freeze-hit-fixtures.json')['Generation']==2
 late=STAGE/'late-drain-control-1';e.cohort(late,families(('npc-equipment-elements','npc-equipment-poison','npc-equipment-critical','venom','monster-ordered-release','revive-event','revive-native-swing','npc-flat-drains'),{'monster-contact-check':3,'iron-golem-ordered-contact-check':3,'venom-result':1,'venom-reload-result':2,'revive-native-swing-result':3,'revive-native-swing-save-result':3}),48)
 failed=STAGE/'failed-native-2';text=(failed/'birth-native-checks.log').read_text();match=re.search(r'expected=(\{[^\n]+\}), actual=(\{[^\n]+\})\n',text);assert match
 expected,actual=map(json.loads,match.groups());assert actual['SourceAlive']and actual['TargetAlive']
 assert [actual[n]for n in ('SourceLife','TargetLife','Mana','Stamina')]==[2281,49919,825,1281]
 assert [expected[n]for n in ('SourceLife','TargetLife','Mana','Stamina')]==[2281,49919,825,1025]and actual['Stun']==[expected['Stun']]
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==5 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
 audits=e.audits(STAGE,PARENT,born['artifacts']);protected=e.protection()
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(s['implementation']=='handler-present'for s in status['skills'])==197
 native=ROOT/'.local/class-skills/player-melee-return/native/Projects';source=native/'Server/Mobiles/Mobile.cs';raw=source.read_text()
 assert 'm_StamTimer ??= new StamTimer(this);'in raw and 'public StamTimer(Mobile m) : base(GetStamRegenRate(m), GetStamRegenRate(m))'in raw
 timer=raw[raw.index('private class StamTimer : Timer'):raw.index('private void Logout()',raw.index('private class StamTimer : Timer'))]
 assert timer.index('m_Owner.Stam++;')<timer.index('Delay = Interval = GetStamRegenRate(m_Owner);')
 files=[Path(__file__).resolve(),parentpath,source,native/'UOContent/Misc/RegenRates.cs',ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tests/Sanctuary.Integration/NpcFlatDrainChecks.cs',ROOT/'tests/check_npc_drain_reload.py',ROOT/'tests/check_npc_drain_reload_freeze.py']
 files += [ROOT/'docs'/n for n in ('npc-drain-reload-checkpoint.md','npc-freeze-hit-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','class-skill-reuse-notes.md','pet-adjustment-review.md')]
 files += e.stage_files(STAGE,FREEZE,late,failed)
 record=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],productionAssemblyReusedExactly=True,productionSourcesAndDataUnchanged=True,changedCSharp=['tests/Sanctuary.Integration/NpcFlatDrainChecks.cs'],artifacts=born['artifacts'],copiedData=born['copiedData'],sourceSnapshot=2,integrationBuild=2,combinedNativeRun=3,focusedFreezeRun=1,combinedMarkers=54,freezeMarkers=12,lateDrainControlMarkers=48,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,directTimerControls=3,constructorDelayTicks=50000000,currentRateTicks=36000000000,ordinaryStamina=[1025,1281],noRecoveryStamina=[1025,1025],retainedResourceVector=[2281,49919,825,1025],pythonTests=71,verificationCommands=5,auditFingerprints=audits,protection=protected,
 scope='Resolved the longer combined drain/native Revive reload mismatch as test-fixture native stamina recovery. Diagnostic original-order failure isolates one FP256 stamina point;late-drain control passes. Read-only cached-timer inspection plus six-second ordinary/no-recovery comparisons reproduce the first five-second tick despite an hour-long current rate on all three boots. Explicit no-recovery encounter fixtures retain the original source fields,Tank/drain100profile,12contacts/full-filler composition and exact HP/mana/stamina/stun assertions in the former failing order across two real reloads. Frozen Revive events,native AI/saved A1 and learned original freeze equipment retain. Exact accepted parent production DLL/native/data reused;only the integration fixture changes.',
 limits=['This changes test fixtures,not production regeneration or class mechanics. It adds no handler or completed class;remaining Necromancer equipment/controller/class acceptance continues.',
 'The resolved test mismatch does not remove hireling,burn/bypass,Sunder300or modern Warlock source gaps,or certify global current-executable/world/client parity. Parent mechanical/source evidence retains its own bounded scope.',
 'Name-based rate hooks remain only to demonstrate the stale constructor timer in the ordinary control. The exact-resource fixture forbids recovery from construction,including after native deserialization;no expected wounded balance is reset or relaxed.',
 'No deployment,installed artifact/save/config/client mutation,visual/icon identity or completed asset. Read-only blind-source preparation is local next-work context,not admitted mechanics.'])
 e.write('npc-drain-reload-native-evidence.json',record,files,check)
 print('Combined drain/Revive mismatch resolved:54+12native markers/two reloads,3cached-timer controls,exact parent production,71Python/audits/protection.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
