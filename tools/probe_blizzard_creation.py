#!/usr/bin/env python3
"""Reuse Meteor's complete do28/admission/creator/factory composition for Blizzard158."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/blizzard-creation-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);parent=ROOT/'tools/reference/meteor-creation-source-evidence.json';prior=json.loads(parent.read_text());original=ROOT/'.local/class-skills/meteor-creation-source/probe.cpp';retained=STAGE/'reused-meteor.cpp';expected=prior['artifacts'][str(original.relative_to(ROOT))]
 if not retained.exists():assert sha(original)==expected;shutil.copy2(original,retained)
 assert sha(retained)==expected;cpp=retained.read_text();bodies={};sources={};tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit']
 for rel,names in {'source/D2Game/src/SKILLS/SkillSor.cpp':['SKILLS_SrvDo028_Meteor_Blizzard_Eruption_BaalTaunt_Catapult'],'source/D2Game/src/SKILLS/Skills.cpp':['sub_6FD15340','D2GAME_CreateMissile_6FD115E0'],'source/D2Game/src/MISSILES/Missiles.cpp':['MISSILES_CreateMissileFromParams']}.items():
  path=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=path.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel);sources[rel]=sha(path)
  for name in names:bodies[name]=function(raw.decode(),name);assert function(cpp,name)==bodies[name]
 for a,b in [('row.wRange=60;','row.wRange=100;'),('guard==3?1000:101;','guard==3?1000:158;'),('&owner,56,rank);','&owner,59,rank);')]:assert cpp.count(a)==1,a;cpp=cpp.replace(a,b)
 for name,body in bodies.items():assert function(cpp,name)==body
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==5184
 positions=[(50,70),(40,70),(0,60),(150,70)]
 for inputs,out,requests in cases:
  guard,world,rank,select,point,distance,kind=inputs;x,y=positions[point]
  if select:x+=3;y+=7
  else:
   if guard==6:x=0
   if guard==7:y=0
  admitted=guard not in (1,2,3,4,5,8) and x!=0 and y!=0;requested=admitted and distance<=100;made=requested and world==0
  expected=[[1,158,x,y,0,0,59,rank,int(made),*([x,y,x,y,0,100,rank] if made else [0]*7)]] if requested else []
  flags=0x100000|(64 if guard not in (1,2,3,8) else 0)
  assert out==[int(made),flags,int(requested),int(requested and world not in (1,2)),int(admitted)] and requests==expected,(inputs,out,requests)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n');pins={};tables={};manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text())
 for name in ('skills','missiles'):
  path=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';assert sha(path)==manifest['files'][f'json/{name}.json']['sha256'];pins[str(path.relative_to(ROOT))]=sha(path);tables[name]=json.loads(path.read_text())
 skill=next(r for r in tables['skills'].values() if r.get('skill')=='Blizzard');center=tables['missiles']['blizzardcenter'];assert skill['*Id']==59 and skill['srvdofunc']==28 and skill['srvmissilea']=='blizzardcenter' and center['*ID']==158 and center['Range']==100 and not center.get('Vel',0) and not center.get('CollideType',0)
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(cases),sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},currentTable=pins,currentRows={'skill':skill,'center':center},sanitizers='ASan/UBSan clean',
 established=['The exact retained Meteor factory scaffold and four complete canonical routines produce one flags1center158 with59/retained rank,captured target coordinates,stationary100frames. Selected-unit aim precedes path aim;mask5 admission,nonzero axes and source-distance100/101 boundary precede allocation. Factory refusal returns failure;caster flag64 is set after valid rows and before admission.'],
 declaredServices=['Older engine,not modern binary proof. The retained scaffold changes only declared fixture missile/skill IDs and center row lifetime. Target/room/distance/collision/path/allocation/damage writer services remain explicit. Numeric capture,do10/scatter/expiry,shard collision/HP/return/global seeds,outer payment/animation/LOS/town/client and native save/input remain separate. Reuse the existing Blizzard/Fissure scatter and cold spell services.'],
 artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,retained,p,binary,san,log,fixture)})
 out=ROOT/'tools/reference/blizzard-creation-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Blizzard creation:5184complete reused do28/admission/ground creator/factory cases;ASan/UBSan clean;center158stationary100frames;runtime unchanged.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');probe(a.parse_args().check)
