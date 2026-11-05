#!/usr/bin/env python3
"""Pin every current skill execution row without dropping unknown/NPC fields."""
import argparse,hashlib,json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];REF=ROOT/'tools/reference/d2r-named-equipment'
MODES=('DT','NU','WL','RN','GH','TN','TW','A1','A2','BL','SC','TH','KK','S1','S2','S3','S4','DD','SQ','KB')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 pins=json.loads((REF/'sources.json').read_text());tables={};inputs={}
 for name in ('skills','missiles'):
  p=REF/'json'/(name+'.json');assert sha(p)==pins['files']['json/'+name+'.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);tables[name]=json.loads(p.read_text())
 header=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Common/include/Units/Player.h';tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());relative='source/D2Common/include/Units/Player.h';raw=header.read_bytes()
 assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==relative)
 enum=raw.decode().split('enum D2C_PlayerModes',1)[1].split('NUMBER_OF_PLRMODES',1)[0];codes=re.findall(r'PLRMODE_\w+\s*,\s*//\s*(\w+)',enum);assert tuple(codes)==MODES;inputs[str(header.relative_to(ROOT))]=sha(header)
 missiles={name:r['*ID'] for name,r in tables['missiles'].items()};assert len(missiles)==742 and set(missiles.values())==set(range(742))
 rows=[]
 for key,r in sorted(tables['skills'].items(),key=lambda v:int(v[0])):
  assert int(key)==r['*Id'];number=lambda name:r.get(name,0)
  for name in ('srvdofunc','ItemEffect'):assert isinstance(number(name),int) and 0<=number(name)<=65535
  for name in ('srvstfunc',):assert isinstance(number(name),int) and -32768<=number(name)<=32767
  for name in ('ItemTgtDo','ItemCheckStart','InTown','UseManaOnDo','lob','decquant'):assert number(name) in (0,1)
  assert 0<=number('ItemTarget')<=255
  missile=missiles[r['srvmissile']] if 'srvmissile' in r else -1
  anim=MODES.index(r['anim']) if 'anim' in r else None
  rows.append(dict(Id=int(key),Name=r.get('skill',''),Do=number('srvdofunc'),Start=number('srvstfunc'),ItemEffect=number('ItemEffect'),ItemTarget=number('ItemTarget'),ItemTargetDo=bool(number('ItemTgtDo')),ItemCheckStart=bool(number('ItemCheckStart')),InTown=bool(number('InTown')),UseManaOnDo=bool(number('UseManaOnDo')),Lob=bool(number('lob')),DecrementQuantity=bool(number('decquant')),Missile=missile,Animation=anim,Fields=r))
 assert len(rows)==429 and [r['Id'] for r in rows]==list(range(429)) and [r['Id'] for r in rows if not r['Name']]==[362]
 unresolved=[r['Id'] for r in rows if r['Animation'] is None];assert len(unresolved)==43
 data=dict(ReferenceVersion='3.3.93847',SourceHashes=inputs,MissileIds=missiles,UnresolvedAnimationIds=unresolved,Skills=rows)
 p=ROOT/'data/Sanctuary/Balance/item-skill-execution.json';encoded=json.dumps(data,indent=2)+'\n'
 if check:assert p.read_text()==encoded,'Item execution metadata stale'
 else:p.write_text(encoded)
 e=dict(referenceVersion='3.3.93847',inputs=inputs,legacyAnimationEnum=tree['sha'],rows=429,missiles=742,blankSkill=362,missingAnimationRows=43,allFieldsRetained=True,do30Curses=[r['Id'] for r in rows if r['Do']==30 and r['Fields'].get('charclass')=='nec'],directMissileRows=sum(r['Missile']>=0 for r in rows),artifacts={str(p.relative_to(ROOT)):sha(p),str(Path(__file__).resolve().relative_to(ROOT)):sha(Path(__file__).resolve())},limits=['Animation is explicitly null when absent,not invented as mode0. All raw fields including localdelay/globaldelay/perdelay remain. The older single-delay field is absent in current rows;do not replace current delays with zero or merge them.','This catalogue does not admit arbitrary skill bodies or prove current executable/animation/default parsing parity. Native profiles must supply their supported contexts and reject unsupported routes.'])
 p=ROOT/'tools/reference/item-skill-execution-metadata.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==encoded,'Item execution metadata receipt stale'
 else:p.write_text(encoded)
 print('Item execution metadata:429complete current rows,742missile links,blank362,43explicit unresolved animations;raw fields/current delays retained.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
