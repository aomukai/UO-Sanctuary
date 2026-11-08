#!/usr/bin/env python3
"""Reuse captured do154/scaler probes with pinned Ring inputs and supplied fnum."""
import json,hashlib
from pathlib import Path
from probe_ring_of_fire_launch import Launch
from probe_missile_damage_scale import Scale
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build():
 skills=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';missiles=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json'
 s=json.loads(skills.read_text())['394'];m=json.loads(missiles.read_text())['ringoffire'];assert s.get('srvstfunc',0)==0 and s['srvdofunc']==154 and m['*ID']==689 and m['Param1']==5
 launch=Launch();scale=Scale();lc=[];sc=[]
 for rank in [1,9,10,20]:
  for fail in [0,1]:
   c=[1,1,689,742,1,0,1,180,220,394,rank,25,0,32 if rank>=10 else 16,173,0,fail];lc.append(dict(input=c,output=launch.run(c)))
 for frame in range(12):
  for values in [[1,99,100,255,256,257,65535],[-1,-99,-100,-255,-256,-257,-65535],[16777215,16777216,16777217,2147483520,2147483647,-2147483648,-2147483520]]:
   c=dict(present=True,row=True,id=689,kind=3,data=True,word0e=1,ownerFlag=True,owner=True,selector=1,token=123,percent=100+frame*5,values=values)
   sc.append(dict(frame=frame,input=c,output=scale.run(c)))
 stage=ROOT/'.local/class-skills/ring-profile-source';stage.mkdir(exist_ok=True);fixture=stage/'cases.json';fixture.write_text(json.dumps(dict(launch=lc,scale=sc),separators=(',',':'))+'\n')
 paths=[Path(__file__).resolve(),skills,missiles,fixture,ROOT/'data/Sanctuary/Balance/ring-of-fire-world.json',ROOT/'tools/probe_ring_of_fire_launch.py',ROOT/'tools/probe_missile_damage_scale.py',*launch.code,*scale.code]
 r=dict(referenceVersion='3.3.93847',launchCases=len(lc),scaleCases=len(sc),currentMachineCodeExecutedOffline=True,processMemoryRead=False,scope='Primary16/32 count,velocity25 and current do154 request binding. Actual candidate scaling body runs with supplied100+5*fnum result. Does not establish live fnum supplier or dispatcher identity;no old compiler matrix rerun.',artifacts={str(p.relative_to(ROOT)):sha(p)for p in paths})
 (ROOT/'tools/reference/ring-profile-current-evidence.json').write_text(json.dumps(r,indent=2)+'\n');print('Current Ring primary bindings:',len(lc),'launch contexts;',len(sc),'supplied-frame packet comparisons;live fnum remains unresolved.')
if __name__=='__main__':build()
