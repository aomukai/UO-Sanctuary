#!/usr/bin/env python3
"""Pin the owned current-build guide; documentation contracts are not executable proof."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/research/d2r-3.3.93847/dataguide'
FILES=('data/files/monstats2.js','data/files/skills.js','data/files/monai.js','docs/damage.html','docs/genericpetai.html','about.html','changelog.html','data/dataguidefieldexport.json')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def model(name):
 text=(BASE/'data/files'/ (name+'.js')).read_text();return json.loads(text[text.index('=')+1:].strip().removesuffix(';'))
def build(check=False,extract=False):
 pin=json.loads((ROOT/'tools/reference/d2r-local-installation-evidence.json').read_text());reader=ROOT/'.local/class-skills/casc-reference-reader';code=reader.with_suffix('.cpp')
 expected=json.loads((ROOT/'tools/reference/d2r-local-sentry-model-evidence.json').read_text())['reader'];assert sha(code)==expected['sourceSha256']
 if extract:
  install=Path(pin['installationPath']);assert sha(install/'.build.info')==pin['buildInfo']['sha256'] and sha(install/'D2R.exe')==pin['executable']['sha256']
  for name in FILES:
   path=BASE/name;path.parent.mkdir(parents=True,exist_ok=True);subprocess.run([str(reader),str(install),'data:data/global/dataguide/'+name,str(path)],check=True)
  assert sha(install/'.build.info')==pin['buildInfo']['sha256'] and sha(install/'D2R.exe')==pin['executable']['sha256']
 fields={r['name']:r for r in model('monstats2')['fields']};description=fields['limitCorpses']['description']
 assert 'pool' in description and '(50)' in description and 'beginning' in description and fields['limitCorpses']['type']['type']=='boolean'
 tables={r['name']:r.get('table') for r in model('skills')['fields']}
 start={r[0]:r for r in tables['srvstfunc'][1:]};assert 'BarStartFrenzy' in str(start['67']) and 'BarStartDoubleSwing' in str(start['68'])
 source=ROOT/'tools/reference/d2r-named-equipment/json/monstats2.json';rows=json.loads(source.read_text());limited=sorted(r['Id'] for r in rows.values() if r.get('limitCorpses'))
 assert len(limited)==15 and 'chargeboltsentry' in limited and 'necromage' not in limited
 data=dict(referenceVersion='3.3.93847',installationEvidence='tools/reference/d2r-local-installation-evidence.json',buildInfoSha256=pin['buildInfo']['sha256'],executableSha256=pin['executable']['sha256'],
  reader=dict(**expected,binarySha256=sha(reader)),files={name:dict(path=str((BASE/name).relative_to(ROOT)),archivePath='data:data/global/dataguide/'+name,bytes=(BASE/name).stat().st_size,sha256=sha(BASE/name)) for name in FILES},
  documentedCorpsePool=dict(capacity=50,sharedAcrossFlaggedSpecies=True,eviction='oldest first',timerSpecified=False,flaggedSpecies=limited,monstats2Sha256=sha(source)),
  documentedStartNames={'67':'BarStartFrenzy','68':'BarStartDoubleSwing'},
  established=['The pinned installation includes its guide under data/global/dataguide, rather than the older excel/_diabloiidatafileguide filename. Strict read-only CASC extraction matches executable and build manifest before and after reading.',
   'The owned current guide documents a common oldest-first corpse pool with capacity50 for the limitCorpses flag. Exactly15current model rows set it,including all five stationary sentries,Blade Sentinel,golems,vines and three Warlock demons;Mages and ordinary skeletons must not be silently added.',
   'Current guide names starts67/68 and describes frame/attack bookkeeping reset and an attack-rate calculation. This supplements the null older dispatcher slots;it does not establish exact current operation/payment/hand order.'],
  limits=['Documentation contracts are primary shipped reference material,not a captured execution or full code proof. Pool insertion phase,level-unload/GUID removal and all cross-family death adapters still require integration and verification.',
   'The guide calls the maximum50,but exact append-versus-evict callback ordering needs its own current-code or observable check. No elapsed corpse timer is inferred.',
   'No complete skill,class certificate,game process launch,debugger,game modification or asset completion follows from this extraction. Raw licensed files remain local and ignored.'])
 out=ROOT/'tools/reference/d2r-local-dataguide-evidence.json';text=json.dumps(data,indent=2)+'\n'
 if check:assert out.read_text()==text
 else:out.write_text(text)
 rules=dict(referenceVersion='3.3.93847',capacity=50,sources=limited)
 return rules
def main():
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');p.add_argument('--extract',action='store_true');a=p.parse_args();rules=build(a.check,a.extract)
 out=ROOT/'data/Sanctuary/Balance/limited-summon-corpses.json';text=json.dumps(rules,indent=2)+'\n'
 if a.check:assert out.read_text()==text
 else:out.write_text(text)
 print('Owned current guide:8strict local files;documented shared50corpse pool/15species;modern start names;no executable or completed-skill claim.')
if __name__=='__main__':main()
