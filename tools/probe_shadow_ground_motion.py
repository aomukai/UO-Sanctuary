#!/usr/bin/env python3
"""Reuse the qualified converted-NPC locomotion source for Assassin graphics."""
import hashlib,itertools,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-ground-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);parent=json.loads((ROOT/'tools/reference/shadow-animation-source-evidence.json').read_text());template=BASE/'shadow-animation-source/source.cpp'
 assert sha(template)==parent['artifacts'][str(template.relative_to(ROOT))]
 cpp=template.read_text().split('int main(){')[0];assert cpp.count('assert(cls==0)')==1;cpp=cpp.replace('assert(cls==0)','assert(cls==6)')
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());bodies={}
 for rel,names in [('source/D2Common/src/Units/Units.cpp',['UNITS_GetAnimStatBasePercentage','UNITS_GetBaseAnimSpeed','UNITS_GetBaseVelocity','UNITS_UpdateRunWalkAnimRateAndVelocity']),('source/D2Common/src/D2Skills.cpp',['D2COMMON_11013_ConvertMode'])]:
  raw=(BASE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel)
  for n in names:bodies[n]=function(raw.decode(),n);assert function(cpp,n)==bodies[n]
 ref=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((ref/'sources.json').read_text());chars=ref/'json/charstats.json';assert sha(chars)==manifest['files']['json/charstats.json']['sha256']
 c=next(r for r in json.loads(chars.read_text()).values()if r.get('class')=='Assassin');assert[c['WalkVelocity'],c['RunVelocity']]==[6,9]
 local=json.loads((ROOT/'tools/reference/d2r-local-animation-evidence.json').read_text());rawpath=ROOT/local['localRawPath'];assert sha(rawpath)==local['sha256'];raw=rawpath.read_bytes();cursor=0;rows=[]
 for _ in range(256):
  count=int.from_bytes(raw[cursor:cursor+4],'little');cursor+=4
  for _ in range(count):
   r=raw[cursor:cursor+160];cursor+=160;code=r[:8].split(b'\0')[0].decode('ascii')
   if code in ['AIWLHT2','AIRNHT2']:rows.append(dict(Code=code,Frames=int.from_bytes(r[8:12],'little'),Speed=int.from_bytes(r[12:16],'little'),Events=[[i,v]for i,v in enumerate(r[16:])if v]))
 assert len(rows)==2 and all(r['Frames']==8 and r['Speed']==256 and not r['Events']for r in rows)
 data=ROOT/'data/Sanctuary/Balance/shadow-ground-animation.json';data.write_text(json.dumps(dict(ReferenceVersion='3.3.93847',WalkVelocity=6,RunVelocity=9,Rows=rows),indent=2)+'\n')
 cpp+=r'''
int main(){D2StatesTxt states[120];tables.pStatesTxt=states;int count;assert(scanf("%d",&count)==1);printf("[");for(int i=0;i<count;i++){
D2UnitStrc actor;D2DynamicPathStrc path;actor.pDynamicPath=&path;assert(scanf("%d%d%d%d",&actor.dwClassId,&actor.dwAnimMode,&move,&fasterMove)==4);int type=1,cls=actor.dwClassId,mode=actor.dwAnimMode;
D2COMMON_11013_ConvertMode(&actor,&type,&cls,&mode,__FILE__,__LINE__);UNITS_UpdateRunWalkAnimRateAndVelocity(&actor,mode,type,cls,__FILE__,__LINE__);
printf("%s[%d,%d,%d,%d,%d]",i?",":"",type,cls,mode,path.velocity,actor.wAnimSpeed);}printf("]\n");}
'''
 cases=[dict(Actor=cls,Mode=mode,Percent=percent,Faster=faster)for cls,mode,percent,faster in itertools.product([417,418],[2,15],[-100,0,25,75,125,175,300],[0,10,40,100,300])]
 path=STAGE/'source.cpp';path.write_text(cpp);binary=STAGE/'probe';result=subprocess.run(['g++','-std=c++20','-O1','-fsanitize=address,undefined','-fno-omit-frame-pointer','-no-pie',str(path),'-o',str(binary)],capture_output=True,text=True);(STAGE/'compiler.log').write_text(result.stdout+result.stderr);assert result.returncode==0,result.stderr[-1500:]
 request=' '.join(map(str,[len(cases)]+[v for c in cases for v in c.values()]))+'\n';result=subprocess.run([str(binary)],input=request,capture_output=True,text=True);(STAGE/'sanitizer.log').write_text(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr
 for c,v in zip(cases,json.loads(result.stdout),strict=True):c['Expected']=v
 fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',contexts=len(cases),completeRoutineHashes={n:hashlib.sha256(s.encode()).hexdigest()for n,s in bodies.items()},inputs={str(p.relative_to(ROOT)):sha(p)for p in [Path(__file__),template,chars,rawpath,data]},artifacts={str(p.relative_to(ROOT)):sha(p)for p in [path,fixture]},scope='Complete older converted Assassin motion rate/velocity routines;owned Walk6/Run9 and locomotion rows. Native path transport/collision and current executable parity are distinct.')
 (ROOT/'tools/reference/shadow-ground-source-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n');print(f'Shadow movement: {len(cases)} complete source contexts; ASan/UBSan clean')
if __name__=='__main__':main()
