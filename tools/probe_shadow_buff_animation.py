#!/usr/bin/env python3
"""Owned SC rows over the already-qualified complete Shadow clock routines."""
import hashlib,itertools,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-buff-animation-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);parent=json.loads((ROOT/'tools/reference/shadow-animation-source-evidence.json').read_text());template=BASE/'shadow-animation-source/source.cpp'
 assert sha(template)==parent['artifacts'][str(template.relative_to(ROOT))];cpp=template.read_text().split('int main(){')[0]
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());bodies={}
 for rel,names in [('source/D2Common/src/D2Skills.cpp',['D2COMMON_11013_ConvertMode']),('source/D2Common/src/Units/Units.cpp',['UNITS_GetFrameBonus','UNITS_UpdateAttackAnimRateAndVelocity']),('source/D2Game/src/UNIT/SUnit.cpp',['sub_6FCBCE70'])]:
  raw=(BASE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel)
  for name in names:bodies[name]=function(raw.decode(),name);assert function(cpp,name)==bodies[name]
 ref=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((ref/'sources.json').read_text());p=ref/'json/skills.json';assert sha(p)==manifest['files']['json/skills.json']['sha256'];skills=json.loads(p.read_text())
 metadata=json.loads((ROOT/'data/Sanctuary/Balance/npc-skill-metadata.json').read_text());assert all(metadata[id]['Mode']==7 for id in (258,267))
 for id in ('258','267'):
  r=skills[id];assert r['monanim']=='SC'and r['UseAttackRate']==1 and r['srvdofunc']==18
 local=json.loads((ROOT/'tools/reference/d2r-local-animation-evidence.json').read_text());rawpath=ROOT/local['localRawPath'];assert sha(rawpath)==local['sha256'];raw=rawpath.read_bytes();cursor=0;rows=[]
 for _ in range(256):
  count=int.from_bytes(raw[cursor:cursor+4],'little');cursor+=4
  for _ in range(count):
   r=raw[cursor:cursor+160];cursor+=160;code=r[:8].split(b'\0')[0].decode('ascii')
   if code in ['AISCHT1','AISCHT2','AISCHTH']:rows.append(dict(Code=code,Frames=int.from_bytes(r[8:12],'little'),Speed=int.from_bytes(r[12:16],'little'),Events=[[i,v]for i,v in enumerate(r[16:])if v]))
 assert len(rows)==3 and all(r['Frames']==17 and r['Speed']==256 and r['Events']==[[9,1]]for r in rows)
 data=ROOT/'data/Sanctuary/Balance/shadow-buff-animation.json';data.write_text(json.dumps(dict(ReferenceVersion='3.3.93847',Rows=sorted(rows,key=lambda r:r['Code'])),indent=2)+'\n')
 cpp+=r'''
int main(){D2StatesTxt states[120];tables.pStatesTxt=states;int count;assert(scanf("%d",&count)==1);printf("[");for(int i=0;i<count;i++){
D2UnitStrc actor;D2GameStrc game;actor.dwAnimMode=7;assert(scanf("%d%d%d%d%d%d",&actor.dwClassId,&ias,&attack,&rightRate,&leftRate,&game.dwGameFrame)==6);int type=1,cls=actor.dwClassId,mode=actor.dwAnimMode;
D2COMMON_11013_ConvertMode(&actor,&type,&cls,&mode,__FILE__,__LINE__);anim={};anim.dwAnimSpeed=256;anim.pFrameFlags[9]=1;actor.dwFrameCountPrecise=17*256;merges=0;events.clear();int bonus=UNITS_GetFrameBonus(&actor);UNITS_UpdateAttackAnimRateAndVelocity(&actor);sub_6FCBCE70(&game,&actor);
printf("%s[%d,%d,%d,%d,%d,%d,[",i?",":"",type,cls,mode,bonus,actor.wAnimSpeed,merges);bool sep=false;for(auto e:events){printf("%s[%d,%d,%d,%d]",sep?",":"",e.type,e.frame,e.kind,e.param);sep=true;}printf("]]");}printf("]\n");}
'''
 cases=[dict(Actor=c,Rates=[ias,attack,r,l,frame])for c,ias,attack,r,l,frame in itertools.product([417,418],[0,40,120],[50,100,160],[-30,0,20],[-30,0,20],[0,37])]
 path=STAGE/'source.cpp';path.write_text(cpp);binary=STAGE/'probe';r=subprocess.run(['g++','-std=c++20','-O1','-fsanitize=address,undefined','-fno-omit-frame-pointer','-no-pie',str(path),'-o',str(binary)],capture_output=True,text=True);(STAGE/'compiler.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stderr[-1500:]
 request=' '.join(map(str,[len(cases)]+[v for c in cases for v in [c['Actor'],*c['Rates']]]))+'\n';r=subprocess.run([str(binary)],input=request,capture_output=True,text=True);(STAGE/'sanitizer.log').write_text(r.stderr);assert r.returncode==0 and not r.stderr,r.stderr
 for c,v in zip(cases,json.loads(r.stdout),strict=True):assert v[:4]==[0,6,10,0]and v[5]==1;c['Expected']=v
 fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',contexts=len(cases),completeRoutineHashes={n:hashlib.sha256(s.encode()).hexdigest()for n,s in bodies.items()},inputs={str(p.relative_to(ROOT)):sha(p)for p in [Path(__file__),template,data,rawpath,ref/'json/skills.json',ROOT/'tools/reference/assassin-buff-engine-evidence.json']},artifacts={str(p.relative_to(ROOT)):sha(p)for p in [path,fixture]},scope='Owned transformed SC17frame/speed256/flag1at9 with four complete older conversion/bonus/dual-hand rate/event routines. Existing do18/group2/aura/passive source evidence is reused. Current executable/input/visual parity remains separate.')
 (ROOT/'tools/reference/shadow-buff-animation-source-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n');print(f'Shadow SC:{len(cases)} whole source contexts;ASan/UBSan clean')
if __name__=='__main__':main()
