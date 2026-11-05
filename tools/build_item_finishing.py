#!/usr/bin/env python3
"""Retain current owned superior/inferior and type finishing facts read-only."""
import argparse,csv,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/item-finishing-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 installation=json.loads((ROOT/'tools/reference/d2r-local-installation-evidence.json').read_text());assert installation['referenceVersion']=='3.3.93847'
 game=Path(installation['installationPath']);assert sha(game/'D2R.exe')==installation['executable']['sha256']and sha(game/'.build.info')==installation['buildInfo']['sha256']
 animation=json.loads((ROOT/'tools/reference/d2r-local-animation-evidence.json').read_text());reader=ROOT/'.local/class-skills/casc-reference-reader';assert sha(reader.with_suffix('.cpp'))==animation['reader']['sourceSha256']
 tables={n:list(csv.DictReader((STAGE/(n+'.txt')).read_text(encoding='utf-8-sig').splitlines(),delimiter='\t'))for n in ('qualityitems','lowqualityitems','itemtypes')}
 flags=('armor','weapon','shield','scepter','wand','staff','bow','boots','gloves','belt');high=[]
 for i,r in enumerate(tables['qualityitems']):
  mods=[]
  for n in (1,2):
   if r['mod'+str(n)+'code']:mods.append(dict(code=r['mod'+str(n)+'code'],parameter=r['mod'+str(n)+'param']or'0',min=int(r['mod'+str(n)+'min']),max=int(r['mod'+str(n)+'max']),mode='Numeric'))
  high.append(dict(row=i,**{k:bool(int(r[k]))for k in flags},modifiers=mods))
 assert len(high)==8 and len(tables['lowqualityitems'])==4
 reference=ROOT/'tools/reference/d2r-named-equipment';meta=json.loads((reference/'sources.json').read_text());p=reference/'json/itemtypes.json';assert sha(p)==meta['files']['json/itemtypes.json']['sha256'];current={r['Code']:r for r in json.loads(p.read_text()).values()if r.get('Code')}
 types=[]
 for i,r in enumerate(tables['itemtypes']):
  if not r.get('Code'):continue
  assert current[r['Code']]['lineNumber']==i
  assert int(r['MaxSocketsLevelThreshold1'])==25 and int(r['MaxSocketsLevelThreshold2'])==40
  types.append(dict(code=r['Code'],ordinal=i,staffMods=r['StaffMods'],maxSockets1=int(r['MaxSockets1']),maxSockets25=int(r['MaxSockets2']),maxSockets40=int(r['MaxSockets3'])))
 assert len(types)==105
 result=dict(referenceVersion='3.3.93847',superior=high,inferior=[r['Name']for r in tables['lowqualityitems']],types=types)
 target=ROOT/'data/Sanctuary/Balance/item-finishing-catalogue.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Finishing catalogue stale'
 else:target.write_text(encoded)
 files=[STAGE/(n+'.txt')for n in tables]+[reader,reader.with_suffix('.cpp'),ROOT/'tools/reference/d2r-local-installation-evidence.json',ROOT/'tools/reference/d2r-local-animation-evidence.json',p,Path(__file__).resolve()]
 evidence=dict(referenceVersion='3.3.93847',source='Read-only offline CASC extraction from pinned owned installation;strict checks,no runtime behavior claim.',superior=8,inferior=4,types=105,allSocketThresholds=[25,40],staffmodClasses=sorted({r['staffMods']for r in types if r['staffMods']}),inputs={str(p.relative_to(ROOT)):sha(p)for p in files},installation=dict(path=str(game),executableSha256=sha(game/'D2R.exe'),buildInfoSha256=sha(game/'.build.info')),outputSha256=sha(target),limits=['Raw current data does not prove current executable parity. Old seven-class staffmod dispatch must not silently supply the Warlock binding.'])
 target=ROOT/'tools/reference/item-finishing-table-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Finishing table provenance stale'
 else:target.write_text(encoded)
 print('Owned3.3.93847finishing data:8superior rows,4inferior names,105type ordinals corroborated;socket thresholds25/40;Warlock staffmods explicit.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
