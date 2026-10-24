#!/usr/bin/env python3
"""Reuse the complete missile factory to resolve poison cloud loop flags/lifetime."""
from pathlib import Path
import argparse,hashlib,json,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
subprocess.run([sys.executable,str(ROOT/'tools/probe_guided_arrow_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
shared=json.loads((ROOT/'tools/reference/guided-arrow-engine-evidence.json').read_text());base=(STAGE/'guided-arrow-source-probe.cpp').read_text().split('int main()',1)[0]
pin=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';manifest=json.loads((pin.parent.parent/'sources.json').read_text());sha=hashlib.sha256(pin.read_bytes()).hexdigest();assert sha==manifest['files']['json/missiles.json']['sha256'];rows=json.loads(pin.read_text());parent=rows['poisonjav'];cloud=rows['poisonjavcloud']
assert (parent['Range'],parent['Vel'],parent['CanSlow'])==(25,24,1)
assert (cloud['Range'],cloud['SubLoop'],cloud['SubStart'],cloud['SubStop'],cloud['CanSlow'])==(60,1,10,19,1)
assert not any(cloud.get(k,0) for k in ('Vel','MaxVel','LevRange','LevVel'))
base+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;std::printf("[");
for(int field:{0,1})for(int rank:{1,2,20,28,255})for(int flag:{5,13,32773})for(int loops:{0,2,38})for(int slowed:{0,1})for(int suppliedRange:{14,60}){
 reset();row={};row.nVel=field?0:24;row.nMaxVel=field?0:24;row.wRange=field?60:25;row.nSubLoop=field?1:0;row.nSubStart=10;row.nSubStop=19;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_CANSLOW]|(field?0:gdwBitMasks[MISSILESFLAGINDEX_PIERCE]);
 D2MissileStrc p;p.pOwner=&owner;p.pOrigin=&target;p.dwFlags=flag;p.nX=41;p.nY=71;p.nMissile=field?39:38;p.nSkill=15;p.nSkillLevel=rank;p.nLoops=loops;p.nRange=suppliedRange;slow=slowed;
 auto* result=MISSILES_CreateMissileFromParams(&game,&p);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",field,rank,flag,loops,slowed,suppliedRange,result!=nullptr,created.total,created.remaining,created.path.velocity,created.x,created.y,created.skill,created.owner==&owner);
}std::printf("]\n");}
'''
# Flags5 includes caller-supplied velocity, exactly as the trail creator does.
# Parent flags1 is a declared factory control; actual generic launch flags are separate.
base=base.replace('for(int flag:{5,13,32773})','for(int flag:{1,5,13,32773})')
path=STAGE/'poison-javelin-factory-source-probe.cpp';path.write_text(base);binary=STAGE/'poison-javelin-factory-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==480
for field,rank,flags,loops,slow,supplied,created,total,remaining,velocity,x,y,skill,owner in data:
 expected=supplied if flags&0x8000 else (60+9*loops*int(bool(flags&8)) if field else 25)
 speed=0 if field or flags&4 else (2304 if slow else 4608)
 assert (created,total,remaining,velocity,x,y,skill,owner)==(1,expected,expected,speed,41,71,15,1)
fixture=STAGE/'poison-javelin-factory-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
name='MISSILES_CreateMissileFromParams';rel='source/D2Game/src/MISSILES/Missiles.cpp'
e=dict(referenceVersion='3.3.93847',talentId='amazon.poisonjavelin',handlerEnabled=False,legacyCommit=shared['legacyCommit'],sourceHashes={rel:shared['sourceHashes'][rel]},compiledRoutines=[name],blockHashes={name:shared['blockHashes'][name]},pinnedMissileHash=sha,probe=dict(cases=len(data),path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete factory gives current cloud39 a60frame/2400ms total/current lifetime at flags5, independent of rank and unflagged nLoops. Trail creator computes2*(rank-1) loops but current formula0 leaves flag8 absent, so it does not extend lifetime.','Flag8 controls SubLoop extension: positive test controls add9frames per loop. Explicit range flag0x8000 overrides the row lifetime. All retain original caller position, skill15/rank and owner; child flags5 supplies zero velocity and remains stationary.','The declared parent flags1 factory control has25frames and velocity4608 after the full factory75percent factor. Declared50percent Slow Missiles reduces it to2304. Flags4 forces supplied zero velocity; parent launch must not accidentally copy the child flags5.'],limits=['Complete factory executes with declared world/room allocation, paths, stat projections, damage creator/writer, seed, owner and event adapters. Separate full damage creator/getters/stat-writer evidence establishes the source0 cloud packet. These fixtures are not a combined current executable certificate.','Outer do-handler decrement/removal timing, source path integration/flag-setting and collision selection/order remain separate. Factory lifetime60frames does not establish exact total damaging contacts or rendered puff lifetime.','Guided Arrow fixture provides generic services; irrelevant guided callbacks are compiled but not called. No native Poison Javelin handler or substitute visual/icon is enabled.'])
out=ROOT/'tools/reference/poison-javelin-factory-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Poison Javelin factory evidence stale'
else:out.write_text(encoded)
print('Poison Javelin:480 complete factory lifetime/loop-flag/velocity cases compiled; unflagged cloud loops do not extend60frames.')
