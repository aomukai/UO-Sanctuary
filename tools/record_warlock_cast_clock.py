#!/usr/bin/env python3
"""Record the owned Warlock SC projection and reused source timing controls."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from build_warlock_cast_clock import build
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/warlock-cast-clock'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 build(True)
 data=json.loads((ROOT/'tools/reference/warlock-cast-clock-data-evidence.json').read_text())
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==15 and ': failed'not in log and ' ERR]'not in log
 for name in ['warlock-cast-clock','shadow-cast']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-1.log').read_text()
 for kind,n in [('content',1),('integration',2)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-3.json').read_text());assert native['copiedData']==snap['inputs']['copiedData']
 for rel,h in native['sourceInputs'].items():assert sha(ROOT/rel)==h,rel
 for rel,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==h,rel
 assert int((STAGE/'warlock-cast-clock-fixtures.json').read_text())==2
 paths=['src/Sanctuary.Content/WarlockCastClock.cs','src/Sanctuary.Content/ActionTiming.cs','data/Sanctuary/Balance/warlock-cast-clock.json','tests/Sanctuary.Integration/WarlockCastClockChecks.cs','tests/check_warlock_cast_clock.py','tools/build_warlock_cast_clock.py','tools/record_warlock_cast_clock.py']
 result=dict(referenceVersion='3.3.93847',handlers=210,warlockHandlers=1,completedClasses=[],currentExecutableParity=False,newCompilerMatrices=0,retainedSourceContexts=2072,ownedWeaponRows=8,ordinaryScCandidates=22,excluded=data['excluded'],nativeMarkers=15,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=2,sourceSnapshot=3,artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in paths},preservation=preservation(),
 scope='Eight owned WKSC records match the existing Necromancer source scheduling fields16/256/8/1 exactly. Shared SourceCastClock reused;no clock arithmetic duplicated and no old compiler matrix rerun. All2072complete source controls compare directly;22ordinary SC/FCR candidates have5698native timing projections with weapon/IAS/chill isolation. Warlock/Witch prepared real-player windup/cancel with zero/20FCR leaves mana and world allocation unchanged;public missing spells stay unavailable. Existing learned Shadow transactions/town/cohorts/original saves retained through two reloads. Handler count unchanged;this is the common clock prerequisite,not a newly completed spell.',
 remaining=['Miasma Chain SC carries UseAttackRate1;its caller/rate context remains separately unqualified','Bind Demon/Cleave/Mirrored Blades retain their own SQ clocks;passives do not enter SC','Ordinary Warlock skill startup/do/missile/event/AI/world/learned payment and public inputs remain separate dependencies','Exact current executable mode/rate/event/input parity remains unverified beyond this owned-data/shared-source projection','Effects/audio and talent/hotkey icons remain separately recorded;clock emits no new asset'])
 p=ROOT/'tools/reference/warlock-cast-clock-native-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Warlock ordinary SC:15native markers/two reloads,22timing candidates,2072existing source cases reused;210handlers unchanged;installed artifacts/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
