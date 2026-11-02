#!/usr/bin/env python3
"""Reuse the ordinary Frozen Orb factory composition for HydraMissile337/247."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/hydra-bolt-creation-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);parent=ROOT/'tools/reference/frozen-orb-creation-source-evidence.json';prior=json.loads(parent.read_text());original=ROOT/'.local/class-skills/frozen-orb-creation-source/probe.cpp';retained=STAGE/'reused-frozen-orb.cpp';expected=prior['artifacts'][str(original.relative_to(ROOT))]
 if not retained.exists():assert sha(original)==expected;shutil.copy2(original,retained)
 assert sha(retained)==expected;cpp=retained.read_text();tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit'];bodies={}
 for rel,digest in prior['sourceHashes'].items():
  p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes();assert sha(p)==digest and hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel)
  for name,h in prior['routineHashes'].items():
   try:b=function(raw.decode(),name)
   except AssertionError:continue
   assert hashlib.sha256(b.encode()).hexdigest()==h and function(cpp,name)==b;bodies[name]=b
 assert len(bodies)==9
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a;cpp=cpp.replace(a,b)
 replace('int id=64,rank=1,flags=0;','int id=337,rank=1,flags=0;')
 replace('row.nVel=row.nMaxVel=10','row.nVel=row.nMaxVel=16')
 replace('calc=25;world=w;','calc=0;world=w;')
 replace('guard==8?1000:260','guard==8?1000:247')
 replace('guard==9?-1:0;','guard==9?-1:0;skill.wItemEffect=0;')
 replace('nullptr:&owner,64,rank,pay,0,0)','nullptr:&owner,337,rank,pay,0,0)')
 replace('row.nCollideType=3;row.dwMissileFlags=0;row.dwDamageRate=0;','row.nCollideType=3;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE]|gdwBitMasks[MISSILESFLAGINDEX_CANSLOW]|gdwBitMasks[MISSILESFLAGINDEX_CANDESTROY];row.dwDamageRate=0;')
 replace('unsigned fFlags=0,dwOwnerGUID=0;','unsigned fFlags=0,dwOwnerGUID=0;int nOwnerType=0;')
 replace('void MISSILE_SetOwner(D2UnitStrc*,D2UnitStrc*){}','void __stdcall MISSILE_SetOwner(D2UnitStrc*,D2UnitStrc*);')
 rel='source/D2Common/src/Units/Missile.cpp';b=function((ROOT/'.local/class-skills/d2moo-pinned-source'/rel).read_text(),'MISSILE_SetOwner');bodies['MISSILE_SetOwner']=b
 replace('void PrintRequests(){','D2MissilesTxt* DATATBLS_GetMissilesTxtRecord(int id){assert(id==247);return &row;}\n'+b+'\nvoid PrintRequests(){')
 replace('PrintRequests();printf("]");','PrintRequests();printf(",[%d,%d,%d,%d]]",made?(int)created.pMissileData->dwOwnerGUID:-999,made?created.pMissileData->nOwnerType:-999,made?created.dwFlags:-999,made?created.owner==&owner:0);')
 for name,b in bodies.items():assert function(cpp,name)==b,name
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==5760
 for inputs,out,requests,owner in cases:
  guard,w,rank,selected,point,pay,kind=inputs;x,y=[(50,70),(40,70),(40,90),(-5,70),(0,70),(50,0)][point]
  if selected:x+=3;y+=7
  admitted=guard==0;requested=admitted and x!=0 and y!=0;made=requested and w==0;tx,ty=(41,71) if (x,y)==(40,70) else (x,y)
  expected=[[33,247,40,70,x,y,337,rank,int(made),*([40,70,tx,ty,3072,30,rank] if made else [0]*7)]] if requested else []
  assert out==[int(admitted),0x100000|(64 if admitted else 0),int(requested),int(requested and w not in(1,2)),int(admitted and pay),0,0,0 if made else -999,0 if made else -999,3 if made else -999,0] and requests==expected and owner==([77,kind,4,1] if made else [-999,-999,-999,0]),(inputs,out,requests,owner,expected)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 pins={};manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text());tables={}
 for name in ('skills','missiles'):
  q=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';assert sha(q)==manifest['files'][f'json/{name}.json']['sha256'];pins[str(q.relative_to(ROOT))]=sha(q);tables[name]=json.loads(q.read_text())
 skill=next(r for r in tables['skills'].values() if r.get('skill')=='HydraMissile');row=tables['missiles']['hydra'];assert skill['*Id']==337 and skill['srvmissile']=='hydra' and not any(skill.get(k) for k in ('srvstfunc','srvdofunc','localdelay','delay','ItemEffect'))
 assert row['*ID']==247 and row['Vel']==row['MaxVel']==16 and row['Range']==30 and row['InitSteps']==2 and row['CollideType']==3 and row['LastCollide']==row['CanDestroy']==row['CanSlow']==1
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(cases),sourceHashes=prior['sourceHashes'],routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},currentTable=pins,sanitizers='ASan/UBSan clean',
  established=['Complete ordinary wrapper/launcher/factory/initializer/data/accessors plus full MISSILE_SetOwner compose. HydraMissile337requests flags33/missile247at the head position,velocity3072FP8/range30/mode3. Stored target coordinates remain zero while physical aim is independent;self aim receives the full factory(+1,+1)nudge.',
  'Current LastCollide initializes owner-type/GUID history to the emitter,while the separate unit creator pointer is retained. CanDestroy marks the missile attackable. Once admitted,the older generic wrapper reports success and invokes requested payment service even if allocation fails;no localdelay is requested. Player-kind rows are generic controls,not an accessible HydraMissile player talent.'],
  declaredServices=['Older source/current tables,not modern executable proof. Exact Frozen Orb factory ABI and nine routines are retained;the tenth owner-history routine is added unchanged. Target/path/world/allocation/event/stat writing,payment service and creator registry are explicit boundaries.',
  'Numeric capture has its separate41310case composition. InitSteps2is pinned metadata but actual initial path advancement/global scheduling remains unproven here. Slow Missiles is enabled in the row but its owner state is inactive in these controls. NPC mana arithmetic,interception,collision/HP/return,creator lifetime/removal,actual native input/save/client remain separate. No C#/runtime/install/save change.'],
  artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,retained,p,binary,san,log,fixture)})
 target=ROOT/'tools/reference/hydra-bolt-creation-source-evidence.json';render=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==render
 else:target.write_text(render)
 print('Hydra bolt creation:5760complete wrapper/factory/initializer/owner-history cases;337to247,velocity3072/range30,creator retained/no localdelay;ASan/UBSan clean;runtime unchanged.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');probe(p.parse_args().check)
