#!/usr/bin/env python3
"""Import all pinned Bind Demon rows without interpreting opaque calc contexts."""
import argparse,csv,io,json
from pathlib import Path
from build_consume_bonuses import ROOT,REF,blob,payload,sha
def record(check=False):
    reference=json.loads((ROOT/'data/Sanctuary/skill-reference.json').read_text())
    assert reference['version']=='3.3.93847'
    primary=REF/'monpet.json';secondary=REF/'monpet.txt.gz'
    assert blob(primary)=='3ceda4619056670e5be2344ecab826e80eb9cbe7'and blob(secondary)=='141c0734c780878edf4bb4edfe4aef4a3330f3ad'
    raw=json.loads(primary.read_text());tsv={r['monster']:r for r in csv.DictReader(io.StringIO(payload(secondary).decode()),delimiter='\t',quoting=csv.QUOTE_NONE)}
    monsters=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json';indices={r['Id']:r['*hcIdx']for r in json.loads(monsters.read_text()).values()}
    rows=[];checked=0;syntax=[]
    for name,r in raw.items():
        if not r.get('bindchancecalc'):continue
        assert r['monster']==name
        for key,value in r.items():
            if key=='lineNumber':continue
            assert str(value)==tsv[name][key],(name,key);checked+=1
        assert all(not value or key in r for key,value in tsv[name].items()),name
        ordinary=name in indices
        if ordinary:assert indices[name]==r['*index'],name
        assert r['BoundStat1']=='maxhp'and not any(r.get('BoundStat'+str(i))for i in range(2,6))
        fields={key:str(r.get(key,''))for key in ('calc1','calc2','calc3','calc4','calc5','bindchancecalc','BoundCalc1')}
        for key,text in fields.items():
            if text.count('(')!=text.count(')'):syntax.append(dict(monster=name,field=key,text=text))
        rows.append(dict(monster=name,sourceIndex=r['*index'],indexScope='MonsterClass'if ordinary else'NamedEncounter',
            calcs=[str(r.get('calc'+str(i),''))for i in range(1,6)],bindChance=str(r['bindchancecalc']),
            boundStat=r['BoundStat1'],boundFormula=r['BoundCalc1'],underlingCount=int(r['numunderlingcalc']),
            consumeStats=[r.get('consumestat'+str(i),'')for i in range(1,6)],consumeParameters=[str(r.get('consumepar'+str(i),''))for i in range(1,6)],
            consumeFormulas=[str(r.get('consumecalc'+str(i),''))for i in range(1,6)]))
    assert len(rows)==171 and sum(r['indexScope']=='MonsterClass'for r in rows)==149
    assert sorted(set(r['underlingCount']for r in rows))==[0,5,7]
    manifest=dict(referenceVersion='3.3.93847',source='tools/reference/d2r-consume/sources.json',primaryGitBlob=blob(primary),corroboratingGitBlob=blob(secondary),
        rows=171,monsterClassRows=149,namedEncounterRows=22,checkedFields=checked,differences=[],unbalancedFields=syntax,
        indexPolicy='Preserve monster-class versus named-encounter namespace;do not treat a named encounter index as a monstats class ID.',
        limits=['Raw current source data,not a recovered runtime bind gate/chance/underling/HP writer or UO species mapping.',
            'Delimiter balance is recorded independently from parser/runtime support. Repeated equality in calc4 remains verbatim;quality/sctk/hpct/pcl/stat producer semantics remain unverified.',
            'The full Consume bonus fields are retained for future bound actors. Existing three-species Consume runtime dispatch remains unchanged.'],
        inputs={str(p.relative_to(ROOT)):sha(p)for p in (ROOT/'data/Sanctuary/skill-reference.json',REF/'sources.json',primary,secondary,monsters,Path(__file__).resolve())})
    for p,obj in ((ROOT/'data/Sanctuary/Balance/bind-demon-monsters.json',rows),(ROOT/'tools/reference/bind-demon-monsters-source.json',manifest)):
        encoded=json.dumps(obj,indent=2)+'\n'
        if check:assert p.read_text()==encoded,str(p)+' stale'
        else:p.write_text(encoded)
    print(f'Bind Demon:171rows/149class+22named/{checked}fields corroborated;{len(syntax)}raw unbalanced fields retained;no gate/host activation.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
