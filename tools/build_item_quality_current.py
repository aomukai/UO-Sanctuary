#!/usr/bin/env python3
"""Retain owned automagic/type-quality/ratio facts without binary-ID claims."""
import argparse,csv,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'item-quality-current-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 pin=json.loads((ROOT/'tools/reference/d2r-local-installation-evidence.json').read_text());game=Path(pin['installationPath']);assert pin['referenceVersion']=='3.3.93847'
 assert sha(game/'D2R.exe')==pin['executable']['sha256']and sha(game/'.build.info')==pin['buildInfo']['sha256']
 files=[STAGE/'automagic.txt',STAGE/'itemratio.txt',BASE/'item-finishing-source/itemtypes.txt'];raw={p.stem:list(csv.DictReader(p.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t'))for p in files}
 types=[]
 for r in raw['itemtypes']:
  if r.get('Code'):types.append(dict(Code=r['Code'],Magic=r.get('Magic')=='1',Rare=r.get('Rare')=='1',Normal=r.get('Normal')=='1'))
 currentpath=ROOT/'data/Sanctuary/Balance/npc-item-affix-catalogue.json';current=json.loads(currentpath.read_text());known={r['code']:r for r in current['types']}
 assert len(types)==105
 for r in types:assert (r['Magic'],r['Rare'])==(known[r['Code']]['alwaysMagic'],known[r['Code']]['rare'])
 rows=[]
 for i,r in enumerate(raw['automagic']):
  number=lambda key:int(r.get(key)or'0')
  mods=[]
  for n in range(1,4):
   if not r['mod'+str(n)+'code']:break
   mods.append(dict(Code=r['mod'+str(n)+'code'],Parameter=r['mod'+str(n)+'param']or'0',Min=number('mod'+str(n)+'min'),Max=number('mod'+str(n)+'max'),Mode='Numeric'))
  typeset=lambda prefix,count:[r[prefix+str(n)].strip()[:4]for n in range(1,count+1)if r.get(prefix+str(n))]
  include=typeset('itype',7);exclude=typeset('etype',5);assert all(t in known for t in include+exclude)
  rows.append(dict(Row=i,Name=r['Name'],Version=number('version'),Spawnable=bool(number('spawnable')),Rare=bool(number('rare')),Level=number('level'),MaxLevel=number('maxlevel'),Frequency=number('frequency'),Group=number('group'),ClassSpecific=r.get('classspecific')or'',Include=include,Exclude=exclude,Modifiers=mods))
 assert len(rows)==44 and all(r['Modifiers']and r['Frequency']>0 for r in rows)
 plans={r['Code']:r for r in json.loads((ROOT/'data/Sanctuary/Balance/equipment-property-plans.json').read_text())}
 codes=sorted({m['Code']for r in rows for m in r['Modifiers']});assert all(code in plans for code in codes)
 for r in rows:
  for m in r['Modifiers']:
   funcs={s['Function']for s in plans[m['Code']]['Steps']};m['Mode']='Proc'if 11 in funcs else'Charges'if 19 in funcs else'DamageRange'if 15 in funcs else'Parameter'if 17 in funcs else'Numeric'
 data=dict(ReferenceVersion='3.3.93847',Types=types,AutoMagic=rows,RatioReference=raw['itemratio'])
 target=ROOT/'data/Sanctuary/Balance/item-quality-current.json';encoded=json.dumps(data,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Owned current quality facts stale'
 else:target.write_text(encoded)
 reader=BASE/'casc-reference-reader';animation=ROOT/'tools/reference/d2r-local-animation-evidence.json';assert sha(reader.with_suffix('.cpp'))==json.loads(animation.read_text())['reader']['sourceSha256']
 files += [currentpath,ROOT/'data/Sanctuary/Balance/equipment-property-plans.json',reader,reader.with_suffix('.cpp'),animation,ROOT/'tools/reference/d2r-local-installation-evidence.json',Path(__file__).resolve()]
 record=dict(referenceVersion='3.3.93847',source='Strict read-only/offline CASC extraction from pinned owned installation',types=105,automagicRows=44,ratioReferenceRows=6,automagicPropertyCodes=codes,inputs={str(p.relative_to(ROOT)):sha(p)for p in files},installation=dict(executableSha256=sha(game/'D2R.exe'),buildInfoSha256=sha(game/'.build.info')),outputSha256=sha(target),limits=['Rows/offsets are reference identities,not proved current compiled/binary/save IDs. Ratio data is retained as reference;this does not admit unforced qualities,unique/set/world drop services or certify current executable parity.'])
 target=ROOT/'tools/reference/item-quality-current-table-evidence.json';encoded=json.dumps(record,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Current quality table evidence stale'
 else:target.write_text(encoded)
 print('Owned quality facts:105type policies/44automagic rows/6reference ratios;properties:',','.join(codes))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
