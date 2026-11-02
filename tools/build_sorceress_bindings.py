#!/usr/bin/env python3
"""Pin Sorceress mouse flags plus Sanctuary's documented Teleport exception."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def build():
 source=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';rows=json.loads(source.read_text());catalog=json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text())
 skills=[]
 for item in catalog:
  if item['characterClass']!='Sorceress':continue
  row=rows[str(item['sourceId'])];left=bool(row.get('leftskill',0));right=bool(row.get('rightskill',0))
  skills.append(dict(talentId=item['talentId'],sourceId=item['sourceId'],name=item['name'],sourceLeft=left,sourceRight=right,left=left or item['talentId']=='sorceress.teleport',right=right))
 assert len(skills)==30
 return dict(referenceVersion='3.3.93847',source=str(source.relative_to(ROOT)),sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),approvedException='docs/t33-teleport.md',skills=skills)
def main():
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args();out=ROOT/'tools/reference/sorceress-binding-reference.json';text=json.dumps(build(),indent=2)+'\n'
 if args.check:assert out.read_text()==text
 else:out.write_text(text)
 print('Sorceress:30pinned mouse rows;approved Teleport left exception retained.')
if __name__=='__main__':main()
