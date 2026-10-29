#!/usr/bin/env python3
"""Bind owned secondary-monster records to their compiled row identities."""
import argparse,csv,json,struct
from pathlib import Path
from build_generic_pet_search_tables import ROOT,sha
STAGE=ROOT/'.local/class-skills/generic-pet-search-nested';PREV=ROOT/'.local/class-skills/generic-pet-search-services'
def build(check=False):
 pinPath=ROOT/'tools/reference/d2r-local-installation-evidence.json';pin=json.loads(pinPath.read_text());game=Path(pin['installationPath']);reader=ROOT/'.local/class-skills/warlock-foundation/casc-reference-reader';source=reader.with_suffix('.cpp');meta=json.loads((ROOT/'tools/reference/warlock-foundation-preflight.json').read_text())['localExtraction']
 assert pin['referenceVersion']==pin['buildInfo']['version']=='3.3.93847' and sha(reader)==meta['readerSha256'] and sha(source)==meta['readerSourceSha256'];assert sha(game/'.build.info')==pin['buildInfo']['sha256'] and sha(game/pin['executable']['filename'])==pin['executable']['sha256']
 b=(STAGE/'monstats2.bin').read_bytes();assert struct.unpack_from('<I',b)[0]==623 and len(b)==4+623*296
 rows=[r for r in csv.DictReader((STAGE/'monstats2.txt').read_text().splitlines(),delimiter='\t') if r['Id']!='Expansion'];assert len(rows)==623
 result=[];differences=[]
 flag4=['noGfxHitTest','noMap','noOvly','isSel','alSel','noSel','shiftSel','corpseSel'];flag6=['compositeDeath','inert','objCol','deadCol','unflatDead','limitCorpses']
 for i,r in enumerate(rows):
  o=4+i*296;assert struct.unpack_from('<I',b,o)[0]==i
  f4=sum(int(r[c] or 0)<<j for j,c in enumerate(flag4));f6=sum(int(r[c] or 0)<<j for j,c in enumerate(flag6));sx,sy=struct.unpack_from('<bb',b,o+8)
  assert b[o+4]==f4 and b[o+6]==f6 and sx==int(r['SizeX'] or 0) and sy==int(r['SizeY'] or 0)
  hc=int(r['*hcIdx']);
  if hc!=i:differences.append(dict(compiledId=i,textHcIdx=hc,name=r['Id']))
  result.append(dict(id=i,name=r['Id'],textHcIdx=hc,flags4=f4,flags6=f6,sizeX=sx,sizeY=sy))
 assert len(differences)==19
 names={r['name']:r['id'] for r in result};mb=(PREV/'monstats.bin').read_bytes();mr={int(r['*hcIdx']):r for r in csv.DictReader((PREV/'monstats.txt').read_text().splitlines(),delimiter='\t') if r.get('*hcIdx','').isdigit()};links=[]
 for i in range(751):
  link=struct.unpack_from('<h',mb,4+i*508+76)[0];assert link==names.get(mr[i]['MonStatsEx'],-1),(i,link,mr[i]['MonStatsEx']);links.append(dict(id=i,secondaryId=link))
 artifacts=[pinPath,reader,source,Path(__file__).resolve(),ROOT/'tools/build_generic_pet_search_tables.py',STAGE/'monstats2.bin',STAGE/'monstats2.txt',STAGE/'monstats2.bin.extract.log',STAGE/'monstats2.txt.extract.log',PREV/'monstats.bin',PREV/'monstats.txt']
 for name,value in [('generic-pet-search-monster-links',links),('generic-pet-search-monster2',result)]:
  p=ROOT/'data/Sanctuary/Balance'/(name+'.json');text=json.dumps(value,indent=2)+'\n'
  if check:assert p.read_text()==text
  else:p.write_text(text)
  artifacts.append(p)
 proof=dict(referenceVersion='3.3.93847',component='Owned secondary-monster links,corpse flags and signed dimensions',processMemoryRead=False,processInput=False,handlerEnabled=False,worldSelectorBindingVerified=False,
 verified=dict(secondaryRows=623,secondaryStride=296,monsterLinks=751,compiledTextFieldMatches=10719,compiledRowIdChecks=623,reservedUpperFlagChecks=623,textIdentityDifferences=19),fields=dict(monsterSecondaryId=76,secondaryFlags4=flag4,secondaryFlags6=flag6,secondarySizeX=8,secondarySizeY=9),
 identityDifferences=differences,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},remaining=['Native current getter/size/corpse composition required. Compiled ordinals,not text hcIdx,are used by current links. Real selector aliases,world membership and state-mask producer remain unverified.'])
 p=ROOT/'tools/reference/generic-pet-secondary-data-evidence.json';text=json.dumps(proof,indent=2)+'\n'
 if check:assert p.read_text()==text
 else:p.write_text(text)
 print('Secondary monster data:623compiled rows/751links;10719field matches;19explicit text hcIdx/compiled ordinal differences.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
