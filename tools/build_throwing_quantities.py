#!/usr/bin/env python3
"""Project every pinned weapon-table stack row, including ordinary bases."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];SOURCE=ROOT/'tools/reference/d2r-named-equipment';OUTPUT=ROOT/'data/Sanctuary/Balance/throwing-quantity-bases.json';EVIDENCE=ROOT/'tools/reference/throwing-quantity-source.json'
def build():
 manifest=json.loads((SOURCE/'sources.json').read_text());path=SOURCE/'json/weapons.json';digest=hashlib.sha256(path.read_bytes()).hexdigest();assert digest==manifest['files']['json/weapons.json']['sha256']
 rows=[]
 for code,row in json.loads(path.read_text()).items():
  cap=row.get('maxstack',0)
  if cap<=0:continue
  assert row['code']==code and row.get('stackable')==1 and row['type'] in ('taxe','tkni','jave','ajav','tpot')
  minimum=row.get('minstack',0);spawn=row.get('spawnstack',0);assert 0<minimum<=spawn<=cap<=10000
  rows.append(dict(code=code,itemType=row['type'],minimum=minimum,maximum=cap,spawn=spawn))
 rows.sort(key=lambda r:r['code']);assert len(rows)==36 and sum(r['itemType']=='tpot' for r in rows)==6
 evidence=dict(referenceVersion='3.3.93847',sourceCommit=manifest['commit'],sourcePath=str(path.relative_to(ROOT)),sourceSha256=digest,rows=36,throwingWeapons=30,throwingPotions=6,policy='Complete source stack metadata, separate from named-item eligibility and native appearance support. Runtime throwing-family gate remains in place. Existing saved counts and initialization policy are not rewritten by this projection.',limits=['Source spawnstack is retained losslessly; it is not assumed to be the initialization bound for stackable weapons. Source weapon/misc/quiver and quality/drop-override initialization differ and need their complete callers.','The supplemental older total-max helper caps base plus extra stack at511. This data projection does not by itself certify the modern upper-cap/extra-stack or initial-quantity pipeline.'])
 return rows,evidence
def main():
 parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args();rows,evidence=build()
 for path,value in [(OUTPUT,rows),(EVIDENCE,evidence)]:
  text=json.dumps(value,indent=2)+'\n'
  if args.check:assert path.read_text()==text,'Throwing quantity projection stale: '+str(path)
  else:path.write_text(text)
 print('Pinned throwing quantity projection:30 weapon and6 potion stack rows; no named eligibility or saved quantity changes.')
if __name__=='__main__':main()
