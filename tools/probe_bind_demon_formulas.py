#!/usr/bin/env python3
"""Reuse the retained compiler for bounded numeric analogues,not game execution."""
import argparse,hashlib,json,re,struct,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/bind-demon-formulas'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    parent=ROOT/'tools/reference/modern-passive-compiler-evidence.json';proof=json.loads(parent.read_text());prior=ROOT/'.local/class-skills/modern-passive-compiler'
    compiler=prior/'compile';seeds=prior/'seeds.tsv'
    for p in (compiler,seeds):assert sha(p)==proof['artifacts'][str(p.relative_to(ROOT))]
    source=ROOT/'tools/reference/d2r-consume/monpet.json';raw=json.loads(source.read_text());rows=[r for r in raw.values()if r.get('bindchancecalc')];ordinary=raw['fallen1'];cases=[]
    def add(kind,text,**context):cases.append(dict(kind=kind,text=text,context=context))
    for phase in (0,1,11,12,13,24):add('progress-modulo',ordinary['calc1'].replace("sksrc('Bind Demon'.sctk)",str(phase)),phase=phase)
    for quality in range(5):
        for hard in (0,9,10,14,15,20):add('quality-gate',ordinary['calc2'].replace("sksrc('Bind Demon'.blvl)",str(hard)).replace('elte',str(quality)),quality=quality,hard=hard)
        for health in (50,95):add('health-term',ordinary['calc4'].replace('hpct',str(health)).replace('elte',str(quality)),quality=quality,health=health)
    for text in sorted({r['bindchancecalc']for r in rows}):
        threshold=int(re.search(r'hpct<(\d+)',text).group(1))
        for health in (threshold-1,threshold,threshold+1):
            numeric=text.replace('hpct',str(health))
            for key,value in (('pcl1',1),('pcl2',1),('pcl3',50),('pcl4',10)):numeric=numeric.replace(key,str(value))
            add('chance-health-gate',numeric,threshold=threshold,health=health)
    for text in sorted({r['BoundCalc1']for r in rows}):
        cap=int(re.search(r'pcl5>(\d+)',text).group(1))
        for players in (1,2):
            for maximum in (cap-1,cap+2000):add('bound-life',text.replace("stat('monster_playercount'.accr)",str(players)).replace('pcl5',str(maximum)),cap=cap,players=players,maximum=maximum)
    assert len(cases)==89
    STAGE.mkdir(exist_ok=True);expressions=STAGE/'expressions.tsv';encoded=''.join(str(i)+'\t'+c['text']+'\n'for i,c in enumerate(cases))
    if check:assert expressions.read_text()==encoded
    else:
        assert not expressions.exists(),'Preserve previous compiler probes'
        expressions.write_text(encoded)
        run=subprocess.run([str(compiler),str(seeds),str(expressions)],capture_output=True)
        (STAGE/'compiled.tsv').write_bytes(run.stdout);(STAGE/'sanitizer.log').write_bytes(run.stderr)
        assert run.returncode==0 and not run.stderr
    lines=(STAGE/'compiled.tsv').read_text().splitlines();assert len(lines)==89 and not(STAGE/'sanitizer.log').read_bytes()
    for i,line in enumerate(lines):
        index,hexcode=line.split('\t');assert int(index)==i;code=bytes.fromhex(hexcode);value=None
        formats={7:(1,'<b'),8:(2,'<h'),9:(4,'<i')}
        if code[0]in formats:
            width,fmt=formats[code[0]]
            if len(code)==width+2 and code[-1]==0:value=struct.unpack_from(fmt,code,1)[0]
        cases[i].update(compiledHex=hexcode,constant=value)
    result=dict(referenceVersion='3.3.93847',component='Bind Demon retained-compiler numeric analogues',numericAnalogues=89,
        constants=sum(c['constant']is not None for c in cases),compilerRebuilt=False,gameFilesRead=False,processMemoryRead=False,sourceCodeExecutedInOfflineCompiler=True,
        gameBytecodeExecuted=False,ownedMonpetBytecodeCompared=False,currentRuntimeParity=False,cases=cases,
        limits=['Uses the unchanged retained modern compiler previously checked against owned skill passive bytes;constant-folded numeric analogues do not establish live monpet callback/variable semantics.',
            'The installed client now identifies3.4.95032 rather than pinned3.3.93847. Extraction was stopped before reading CASC data;no new data is relabeled as93847.',
            'No repaired equality,quality gate,progress counter,health projection,success roll or bound-life writer is installed. Exact runtime/owned-bytecode evidence or an explicit full adaptation is still required.'],
        inputs={str(p.relative_to(ROOT)):sha(p)for p in (parent,compiler,seeds,source,Path(__file__).resolve(),expressions,STAGE/'compiled.tsv',STAGE/'sanitizer.log')})
    out=ROOT/'tools/reference/bind-demon-formula-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Bind Demon:89retained-compiler numeric analogues,'+str(result['constants'])+'constant outputs;no ownedmonpet/runtime equivalence claimed.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
