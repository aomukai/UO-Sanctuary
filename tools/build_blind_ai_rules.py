#!/usr/bin/env python3
"""Pin current timing/range fields for the three traced ordinary AI families."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    source=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json'
    manifest=json.loads((source.parent.parent/'sources.json').read_text())
    assert sha(source)==manifest['files']['json/monstats.json']['sha256']
    data=json.loads(source.read_text());rows=[]
    for row in data.values():
        if row.get('AI') not in ['Skeleton','Zombie','Megademon']:continue
        def fields(key):return [int(row.get(key+suffix,0) or 0) for suffix in ['', '(N)', '(H)']]
        rows.append(dict(Source=row['Id'],Delay=fields('aidel'),Range=fields('aidist')))
    rows.sort(key=lambda r:r['Source']);assert len({r['Source'] for r in rows})==len(rows)>0
    assert all(all(0<=v<=32767 for key in ['Delay','Range'] for v in row[key]) for row in rows)
    dest=ROOT/'data/Sanctuary/Balance/blind-ai-rules.json';render=json.dumps(rows,indent=2)+'\n'
    proof=dict(referenceVersion='3.3.93847',sourcePath=str(source.relative_to(ROOT)),sourceSha256=sha(source),producerSha256=sha(Path(__file__)),
        ruleCount=len(rows),families=['Skeleton','Zombie','Megademon'],runtimePath=str(dest.relative_to(ROOT)),runtimeSha256=hashlib.sha256(render.encode()).hexdigest(),
        interpretation='Raw current monstats aidel/aidist values retained per difficulty. Reviewed legacy neutral entry uses15ifdelay<=0;target acquisition uses35ifrange0. These fields do not certify all source AI callbacks or modern executable scheduling.')
    receipt=ROOT/'tools/reference/blind-ai-rule-evidence.json';evidence=json.dumps(proof,indent=2)+'\n'
    if check:assert dest.read_text()==render and receipt.read_text()==evidence
    else:dest.write_text(render);receipt.write_text(evidence)
    print('Pinned blind AI timing/range rules:',len(rows))
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
