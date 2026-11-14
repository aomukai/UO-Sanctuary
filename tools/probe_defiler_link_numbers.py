#!/usr/bin/env python3
"""Evaluate unchanged Fog against the exact owned Health Link expressions."""
import argparse,csv,hashlib,json,shutil,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/defiler-link-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(exist_ok=True);inputs={}
    def pin(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_text()
    base=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads(pin(base/'sources.json'));p=base/'json/skills.json'
    assert sha(p)==manifest['files']['json/skills.json']['sha256'];row=json.loads(pin(p))['405']
    parent=json.loads(pin(ROOT/'tools/reference/goatman-source-evidence.json'))
    for name in ('Calc.cpp','Calc.h','D2BasicTypes.h','Fog.h'):
        p=ROOT/'.local/class-skills/tainted-foundation-source'/name;assert sha(p)==parent['artifacts']['.local/class-skills/goatman-foundation/'+name];pin(p);shutil.copy2(p,STAGE/name)
    codes=list(csv.DictReader(pin(ROOT/'.local/class-skills/warlock-foundation/skillcalc.txt').splitlines(),delimiter='\t'))
    ids={r['code']:i for i,r in enumerate(codes)};assert ids['lvl']==16
    cpp='''#include "Calc.h"
#include <cstdio>
int rank;
int keyword(char*name){assert(!strcmp(name,"skill"));return 3;}
int parameters(int key){assert(key==3);return 2;}
int link(char*name,BOOL*constant,int ast,int key){if(!strcmp(name,"Summon Defiler")){assert(ast==1&&key==3);*constant=1;return 377;}*constant=0;
'''
    for name in ['lvl','par1','par2','par3','par4']:cpp+=f'if(!strcmp(name,{json.dumps(name)}))return {ids[name]};\n'
    cpp+='assert(false);return -1;}\nint parent(int code,void*){switch(code){\n'
    for i in range(1,5):cpp+=f'case {ids["par"+str(i)]}:return {row["Param"+str(i)]};\n'
    cpp+='default:assert(false);return 0;}}\nint skill(int id,int property,int extra,void*){assert(id==377&&property==16&&extra==0);return rank;}\n'
    cpp+='int main(){D2CalcCallbackInfoStrc table[4]={};table[3]={skill,2};bool first=true;printf("[");\n'
    for index,expression in enumerate([row['calc1'],row['calc2']]):
        cpp+=f'''for(rank=1;rank<=99;rank++){{FOGASTNodeStrc buffer[1024]={{}};int size=DATATBLS_CompileExpression({json.dumps(expression)},buffer,1024,keyword,parameters,link);assert(size>0);int v=DATATBLS_CalcEvaluateExpression(buffer,size,parent,table,4,nullptr);printf("%s[{index},%d,%d,%d]",first?"":",",rank,size,v);first=false;}}\n'''
    cpp+='printf("]\\n");}\n';p=STAGE/'formula.cpp';p.write_text(cpp)
    binary=STAGE/'formula';log=STAGE/'compiler.log';r=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize=alignment','-fno-sanitize-recover=all','-I'+str(STAGE),str(p),str(STAGE/'Calc.cpp'),'-o',str(binary)],capture_output=True);log.write_bytes(r.stdout+r.stderr);assert r.returncode==0
    r=subprocess.run([str(binary)],capture_output=True);san=STAGE/'sanitizer.log';san.write_bytes(r.stderr);assert r.returncode==0 and not r.stderr
    cases=json.loads(r.stdout);assert len(cases)==198
    assert next(v for i,n,size,v in cases if i==0 and n==25)==55 and next(v for i,n,size,v in cases if i==1 and n==25)==5
    out=STAGE/'formula.json';out.write_bytes(r.stdout);pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',component='Exact NPC405 Health Link table expressions',fogCases=198,rawExpressions=[row['calc1'],row['calc2']],
        runtimeCallbackVerified=False,productionHandlerEnabled=False,
        established=['Unchanged pinned Fog compiler/evaluator accepts both exact raw expressions,including calc2 EOF imbalance,without source repair;ASan/UBSan clean.',
            'Ranks1..99 retain original multiply/divide order;rank25 gives55percent/5rawlinks,corroborating user tooltip.',
            'Uncapped raw expressions reach61percent/6rawlinks at99;actual do162/event38 caps remain unverified. Do not substitute an assumed capped diminishing curve.'],
        inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in (p,binary,log,san,out,*(STAGE/n for n in ('Calc.cpp','Calc.h','D2BasicTypes.h','Fog.h')))})
    target=ROOT/'tools/reference/defiler-link-formula-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Health Link expression evidence stale'
    else:target.write_text(encoded)
    print('HealthLink:198exactrawFogcases/ASanUBSan pass;rank25=55percent/5rawlinks;execution/callback caps remain unverified.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
