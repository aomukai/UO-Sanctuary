#!/usr/bin/env python3
"""Import authenticated monpet Consume rows without changing old source manifests."""
import argparse,csv,gzip,hashlib,io,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];REF=ROOT/'tools/reference/d2r-consume'
KEYS=('wargoatman','warbighead','warputriddefiler')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def payload(p):return gzip.decompress(p.read_bytes())if p.suffix=='.gz'else p.read_bytes()
def blob(p):
    b=payload(p);return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def record(check=False):
    source=REF/'monpet.json';corroboration=REF/'monpet.txt.gz';owned=ROOT/'tools/reference/d2r-named-equipment';reference=json.loads((ROOT/'data/Sanctuary/skill-reference.json').read_text())
    assert reference['version']=='3.3.93847'and reference['sourceCommit']=='fa1286949266498b294c40c2942521a7c0d7207a'
    assert blob(source)=='3ceda4619056670e5be2344ecab826e80eb9cbe7'and blob(corroboration)=='141c0734c780878edf4bb4edfe4aef4a3330f3ad'
    pinned=json.loads((ROOT/'.local/class-skills/d2data-pinned-tree.json').read_text())
    assert next(r['sha']for r in pinned['tree']if r['path']=='json/monpet.json')==blob(source)
    raw=json.loads(source.read_text());tsv={r['monster']:r for r in csv.DictReader(io.StringIO(payload(corroboration).decode()),delimiter='\t',quoting=csv.QUOTE_NONE)}
    monsters=json.loads((owned/'json/monstats.json').read_text());rows=[];checked=0
    for key in KEYS:
        r=raw[key]
        for name,value in r.items():
            if name=='lineNumber':continue
            assert str(value)==tsv[key][name],(key,name,value,tsv[key][name]);checked+=1
        assert r['*index']==next(v['*hcIdx']for v in monsters.values()if v['Id']==key)
        assert r['numunderlingcalc']==0 and not any(r.get('consumestat'+str(i))for i in range(3,7))
        rows.append(dict(monster=key,sourceIndex=r['*index'],firstStat=r['consumestat1'],firstFormula=r['consumecalc1'],
            secondStat=r['consumestat2'],secondFormula=r['consumecalc2'],underlingCount=0))
    manifest=dict(referenceVersion='3.3.93847',referenceManifest='data/Sanctuary/skill-reference.json',
        primary=dict(repository=reference['sourceRepository'],commit=reference['sourceCommit'],path='json/monpet.json',blob=blob(source),sha256=sha(source),bytes=source.stat().st_size),
        corroboration=dict(repository=reference['corroboratingRepository'],commit=reference['corroboratingCommit'],path='monpet.txt',blob=blob(corroboration),sha256=hashlib.sha256(payload(corroboration)).hexdigest(),bytes=len(payload(corroboration)),stored='monpet.txt.gz',storedSha256=sha(corroboration),storedBytes=corroboration.stat().st_size),
        license='../d2r-named-equipment/LICENSE.txt',importedMonsters=list(KEYS),checkedFields=checked,differences=[],
        established=['Both exact immutable Git blobs authenticated. Every non-lineNumber field in all three selected rows matches the independently pinned TSV.',
            'Three original monster indices match the existing pinned monstats. Original28-file source manifest remains unchanged.',
            'Normal demon and Bind Demon pet types both name pool1 in pinned pettype data;future binding/Consume occupancy must not use separate pools.'],
        limits=['Source data,not recovered start69/do159. sksrc caller,difficulty context/stat binding/slot/action/town/lifecycle need independent verification or explicit adaptation.',
            'Other monpet rows retained for provenance/future binding;only these three rows are imported into runtime balance data. No bound-monster bonus dispatch is claimed.'],
        inputs={str(p.relative_to(ROOT)):sha(p)for p in [ROOT/'data/Sanctuary/skill-reference.json',owned/'json/monstats.json',owned/'json/pettype.json',Path(__file__).resolve()]})
    outputs={ROOT/'data/Sanctuary/Balance/consume-bonuses.json':rows,REF/'sources.json':manifest}
    for p,value in outputs.items():
        text=json.dumps(value,indent=2)+'\n'
        if check:assert p.read_text()==text,str(p)+' stale'
        else:p.parent.mkdir(parents=True,exist_ok=True);p.write_text(text)
    print(f'Consume monpet:3original demon rows/{checked}fields match both pinned Git blobs;source provenance kept separate;no world activation.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
