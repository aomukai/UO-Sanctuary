#!/usr/bin/env python3
"""Audit reference dependency closures without claiming unrelated class completion."""
from pathlib import Path
import argparse,hashlib,json
ROOT=Path(__file__).resolve().parents[1];p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
source=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((source/'sources.json').read_text());rawPath=source/'json/skills.json';assert hashlib.sha256(rawPath.read_bytes()).hexdigest()==manifest['files']['json/skills.json']['sha256']
raw=json.loads(rawPath.read_text());rows=json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text());ids={r['sourceKey']:r['talentId'] for r in rows}
expected={r['talentId']:[ids[raw[str(r['sourceId'])]['reqskill'+str(i)]] for i in range(1,4) if raw[str(r['sourceId'])].get('reqskill'+str(i))] for r in rows}
actual={t['id']:t['prerequisites'] for c in json.loads((ROOT/'data/Sanctuary/talents.json').read_text()) for tree in c['trees'] for t in tree['skills']}
def closure(graph,key,path=()):
 assert key not in path,'Cyclic prerequisite';out=set()
 for parent in graph[key]:out.add(parent);out.update(closure(graph,parent,path+(key,)))
 return out
results=[]
for row in rows:
 key=row['talentId'];a,b=closure(actual,key),closure(expected,key)
 results.append(dict(talentId=key,characterClass=row['characterClass'],sourceId=row['sourceId'],currentDirect=actual[key],referenceDirect=expected[key],currentClosure=sorted(a),referenceClosure=sorted(b),missing=sorted(b-a),extra=sorted(a-b)))
assert len(results)==240 and all(not r['missing'] and not r['extra'] for r in results if r['characterClass']=='Amazon')
differences=[r for r in results if r['missing'] or r['extra']]
x=dict(referenceVersion='3.3.93847',scope='Dependency closure, including redundant indirect prerequisites; not skill-mechanics or class-completion verification.',inputHashes={str(path.relative_to(ROOT)):hashlib.sha256(path.read_bytes()).hexdigest() for path in [rawPath,ROOT/'data/Sanctuary/talents.json',ROOT/'data/Sanctuary/Balance/class-skills.json']},talents=240,matching=240-len(differences),amazonMatching=30,correctedEntry=dict(talentId='amazon.immolationarrow',referenceSourceId=27,previous=['amazon.magicarrow','amazon.firearrow','amazon.multipleshot'],current=['amazon.explodingarrow'],reason='The legacy list omitted Exploding Arrow; raw current reqskill1 requires it. This is not an approved deviation.'),pending=[dict(talentId=r['talentId'],missing=r['missing'],extra=r['extra']) for r in differences],results=results,limits=['Entries in pending remain separate class-pass dependency differences. This audit does not establish any class completion.','Correcting Immolation adds a prerequisite and could reject an older inconsistent history. A copied existing-world load and final matching installed smoke must verify current saves without rewriting ranks, resetting histories or inventing points.'])
out=ROOT/'tools/reference/talent-prerequisite-audit.json';text=json.dumps(x,indent=2)+'\n'
if args.check:assert out.read_text()==text,'Talent dependency audit stale'
else:out.write_text(text)
print(f'Talent dependency audit:30/30 Amazon closures match; {x["matching"]}/240 overall, {len(differences)} later class-pass differences remain.')
