#!/usr/bin/env python3
"""Validate current stat semantics against existing costs and publish missile sizes."""
import argparse,csv,json,struct
from pathlib import Path
from build_generic_pet_search_tables import ROOT,sha
STAGE=ROOT/'.local/class-skills/generic-pet-search-producers'
def build(check=False):
 pinPath=ROOT/'tools/reference/d2r-local-installation-evidence.json';pin=json.loads(pinPath.read_text());game=Path(pin['installationPath']);reader=ROOT/'.local/class-skills/warlock-foundation/casc-reference-reader';source=reader.with_suffix('.cpp');meta=json.loads((ROOT/'tools/reference/warlock-foundation-preflight.json').read_text())['localExtraction']
 assert pin['referenceVersion']==pin['buildInfo']['version']=='3.3.93847' and sha(reader)==meta['readerSha256'] and sha(source)==meta['readerSourceSha256'];assert sha(game/'.build.info')==pin['buildInfo']['sha256'] and sha(game/pin['executable']['filename'])==pin['executable']['sha256']
 costPath=ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json';costs=json.loads(costPath.read_text());b=(STAGE/'itemstatcost.bin').read_bytes();rows=list(csv.DictReader((STAGE/'itemstatcost.txt').read_text().splitlines(),delimiter='\t'));assert struct.unpack_from('<I',b)[0]==len(rows)==len(costs)==368 and len(b)==4+368*324;differences=[]
 for i,(r,c) in enumerate(zip(rows,costs)):
  o=4+i*324;assert struct.unpack_from('<H',b,o)[0]==i==c['Id'] and r['Stat']==c['Name']
  assert bool(b[o+5]&2)==bool(int(r['fMin'] or 0))==bool(c['Flags']&1024)
  assert b[o+20]==int(r['ValShift'] or 0)==c['Shift'] and struct.unpack_from('<i',b,o+40)[0]==int(r['MinAccr'] or 0)==c['Minimum']
  if int(r['*ID'])!=i:differences.append(dict(compiledId=i,textId=int(r['*ID']),name=r['Stat']))
 assert differences==[dict(compiledId=212,textId=213,name='passive_mastery_gethit_rate')]
 b=(STAGE/'missiles.bin').read_bytes();rows=list(csv.DictReader((STAGE/'missiles.txt').read_text().splitlines(),delimiter='\t'));assert struct.unpack_from('<I',b)[0]==len(rows)==742 and len(b)==4+742*460;missiles=[]
 for i,r in enumerate(rows):
  assert int(r['*ID'])==i==struct.unpack_from('<H',b,4+i*460)[0] and b[4+i*460+434]==int(r['Size'] or 0)
  missiles.append(dict(id=i,name=r['Missile'],size434=b[4+i*460+434]))
 p=ROOT/'data/Sanctuary/Balance/generic-pet-missile-sizes.json';text=json.dumps(missiles,indent=2)+'\n'
 if check:assert p.read_text()==text
 else:p.write_text(text)
 artifacts=[pinPath,costPath,p,reader,source,Path(__file__).resolve(),ROOT/'tools/build_generic_pet_search_tables.py']+[STAGE/(n+s) for n in ['itemstatcost','missiles'] for s in ['.bin','.txt','.bin.extract.log','.txt.extract.log']]
 proof=dict(referenceVersion='3.3.93847',component='Current stat semantics reuse and complete missile sizes',processMemoryRead=False,processInput=False,handlerEnabled=False,worldSelectorBindingVerified=False,
 verified=dict(statRows=368,statStride=324,missileRows=742,missileStride=460,compiledTextFieldMatches=1846,existingStatSemanticMatches=1104,compiledIdChecks=1110,textIdDifferences=1),
 flags=dict(currentCompiledMinimumMask=512,existingSemanticCatalogMinimumMask=1024,meaning='fMin;semantic mapping verified for all368rows,raw layouts are not equated'),identityDifferences=differences,
 artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},remaining=['Native current getters/stat reader/missile size composition required. Explicit table views do not certify real selector aliases. Actual mask410producer and world grids remain separate.'])
 p=ROOT/'tools/reference/generic-pet-producer-data-evidence.json';text=json.dumps(proof,indent=2)+'\n'
 if check:assert p.read_text()==text
 else:p.write_text(text)
 print('Producer data:368existing stat definitions reused/742missile sizes;1846compiled/text matches;one explicit text ID difference.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
