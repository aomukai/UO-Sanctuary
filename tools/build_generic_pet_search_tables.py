#!/usr/bin/env python3
"""Validate owned search monster/level bytes and publish explicit table inputs."""
import argparse,csv,hashlib,json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-search-services'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 pinPath=ROOT/'tools/reference/d2r-local-installation-evidence.json';pin=json.loads(pinPath.read_text());game=Path(pin['installationPath']);reader=ROOT/'.local/class-skills/warlock-foundation/casc-reference-reader';readerSource=reader.with_suffix('.cpp')
 meta=json.loads((ROOT/'tools/reference/warlock-foundation-preflight.json').read_text())['localExtraction']
 assert pin['referenceVersion']==pin['buildInfo']['version']=='3.3.93847' and sha(reader)==meta['readerSha256'] and sha(readerSource)==meta['readerSourceSha256']
 assert sha(game/'.build.info')==pin['buildInfo']['sha256'] and sha(game/pin['executable']['filename'])==pin['executable']['sha256']
 artifacts=[pinPath,reader,readerSource,Path(__file__).resolve()];result={};matches=0
 for name,stride,count in [('monstats',508,751),('levels',404,138)]:
  binary=STAGE/(name+'.bin');txt=STAGE/(name+'.txt');b=binary.read_bytes();assert struct.unpack_from('<I',b)[0]==count and len(b)==4+count*stride
  rows=list(csv.DictReader(txt.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t'));key='*hcIdx' if name=='monstats' else 'Id';byId={int(r[key]):r for r in rows if (r[key] or '').isdigit()};assert set(byId)==set(range(count))
  output=[]
  for i in range(count):
   r=byId[i];off=4+i*stride
   if name=='monstats':
    flags=sum((int(r[c] or 0)&1)<<j for j,c in enumerate(['isSpawn','isMelee','noRatio','opendoors','SetBoss','BossXfer','boss','primeevil']));assert b[off+60]==flags and b[off+137]==int(r['threat'] or 0)
    output.append(dict(id=i,name=r['Id'],flags60=flags,threat137=b[off+137]));matches+=9
   else:
    assert b[off+63]==int(r['MonSpcWalk'] or 0);output.append(dict(id=i,name=r['Name'],walk63=b[off+63]));matches+=1
  runtime=ROOT/'data/Sanctuary/Balance'/('generic-pet-search-'+('monsters' if name=='monstats' else 'levels')+'.json');encoded=json.dumps(output,indent=2)+'\n'
  if check:assert runtime.read_text()==encoded
  else:runtime.write_text(encoded)
  result[name]=dict(rows=count,stride=stride);artifacts += [binary,txt,runtime,STAGE/(name+'.bin.extract.log'),STAGE/(name+'.txt.extract.log')]
 proof=dict(referenceVersion='3.3.93847',component='Owned search monster/level metadata',processMemoryRead=False,processInput=False,handlerEnabled=False,worldSelectorBindingVerified=False,
  verified=dict(tables=result,compiledToTextFieldMatches=matches),fields=dict(monsterFlags60=['isSpawn','isMelee','noRatio','opendoors','SetBoss','BossXfer','boss','primeevil'],monsterThreat137='threat',levelWalk63='MonSpcWalk'),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},
  remaining=['Native explicit-view catalog loading and source helper comparison required. These tables establish selected compiled fields,not real source-selector aliases,difficulty mapping or Britannia level membership. No learned handler is enabled.'])
 p=ROOT/'tools/reference/generic-pet-search-tables-data-evidence.json';encoded=json.dumps(proof,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Owned search tables:751monsters/138levels;'+str(matches)+'compiled/text field matches.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
