#!/usr/bin/env python3
"""Check original-author Fury rollback vectors against the existing shared clock expression."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    preflight=ROOT/'tools/reference/druid-fury-preflight.json';pin=json.loads(preflight.read_text())
    progresspath=ROOT/'tools/reference/modern-form-progress-source-evidence.json';progress=json.loads(progresspath.read_text());fixture=ROOT/progress['fixture']['path'];assert sha(fixture)==progress['fixture']['sha256']
    contexts=[x for x in json.loads(fixture.read_text()) if x[0]==2];rates=sorted({x[7][8] for x in contexts});assert len(contexts)==7200 and len(rates)==327 and rates[0]==72 and rates[-1]==520
    directory=ROOT/'.local/research/ias-calculator-b7d7910';commit=json.loads((directory/'commit.json').read_text());tree=json.loads((directory/'tree.json').read_text());assert commit['sha']=='b7d791031b73b0c6e56a3659e9198578aed21261' and tree['sha']==commit['commit']['tree']['sha']
    source_hashes={}
    for name in ['README.md','calculator.js','constants.js']:
        raw=(directory/name).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==name);source_hashes[name]=hashlib.sha256(raw).hexdigest()
    text=(directory/'calculator.js').read_text();functions={}
    def jsfunction(name):
        match=re.search(r'\bfunction\s+'+name+r'\([^;\n]*\)\s*\{',text);assert match,name;end=text.index('{',match.start())+1;depth=1
        while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
        functions[name]=text[match.start():end];return functions[name]
    start=text.index('let firstHitLength = 256 *');at=text.index('if (skill == skills.FURY || skill == skills.STRAFE || skill == skills.FEND',start);end=text.index('{',at)+1;depth=1
    while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
    rollback=text[start:end];old=json.loads((ROOT/'tools/reference/fend-engine-evidence.json').read_text());assert hashlib.sha256(rollback.encode()).hexdigest()==old['modernOriginalAuthor']['rollbackBlockHash']
    script=r'''const skills=Object.fromEntries(['FURY','FEND','STRAFE','DRAGON_TALON','ZEAL','WHIRLWIND','STANDARD'].map(x=>[x,{}]));
const skill=skills.FURY,char={AMAZON:0,SORCERESS:1,DRUID:5},character=5,weaponType={};
const trun=Math.trunc;function formatRollbackHitLength(x){return x;}function frameLengthsNotEqual(){return true;}
'''
    script+='\n'.join(jsfunction(name) for name in ['fpd1','fpd2','rbf','rhits','getStartingFrame'])+'\n'
    script+='const rates='+json.dumps(rates)+';let output=[];\nfor(const speedIncrease of rates){\n'
    script+='const startingFrame=getStartingFrame(weaponType),framesPerDirection1=fpd1(weaponType,false,true),framesPerDirection2=fpd2(weaponType),offset=0,acceleration=0,EIAS=0;\nconst previousFrameLengths=[[],[]],results=[[],[]],accelerationTables=results.map(x=>({addBreakpoint:(_,v)=>x.push(v)}));\n'
    script+=rollback+'\noutput.push([speedIncrease,startingFrame,framesPerDirection1,framesPerDirection2,rbf(),rhits(),results[0],results[1]]);}\nprocess.stdout.write(JSON.stringify(output)+"\\n");\n'
    js=BASE/'druid-fury-clock-source-probe.mjs';js.write_text(script);rows=json.loads(subprocess.check_output(['node',str(js)]));assert len(rows)==327
    shared=subprocess.check_output(['git','show','8949002:src/Sanctuary.Content/AmazonFend.cs'],cwd=ROOT,text=True)
    native_expression='(int)((start*256L+advance*(long)elapsed)/256)*(100-factor)/100;';assert native_expression in shared
    def schedule(rate,count):
        at=0;start=0;events=[];starts=[];end=0
        for _ in range(count):
            starts.append(start);frames=((7-start)*256+rate-1)//rate
            end=at+((13-start)*256+rate-1)//rate-1;at+=frames;events.append(at)
            # The expression is already present in the verified Fend consumer.
            # Native invocation and Fury's complete outer dispatcher remain next.
            start=((start*256+rate*frames)//256)*30//100
        return dict(events=events,end=end,starts=starts)
    prepared=[]
    for rate,start,action,end,factor,hits,results,other in rows:
        assert (start,action,end,factor,hits)==(0,7,13,70,3) and other==[] and len(results)==1 and len(results[0])==5
        complete=schedule(rate,5);events=complete['events'];vector=[events[0],events[1]-events[0],events[2]-events[1],events[3]-events[2],complete['end']-events[3]]
        assert vector==results[0] and complete['starts']==[0,2,2,2,2]
        for count in [2,3,4,5]:
            case=schedule(rate,count);assert case['end']>=case['events'][-1] and all(b>a for a,b in zip(case['events'],case['events'][1:]))
            prepared.append(dict(advance=rate,count=count,**case))
    assert len(prepared)==1308
    output=BASE/'druid-fury-clock-source-probe.json';output.write_text(json.dumps(dict(authorRows=rows,preparedSchedules=prepared),separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',sourcePreparation=True,handlerEnabled=False,nativeAccepted=False,currentExecutableParity=False,
        preflightSha256=sha(preflight),progressEvidenceSha256=sha(progresspath),progressFixtureSha256=sha(fixture),
        originalAuthor=dict(repository='https://github.com/Warren1001/IAS_Calculator',commit=commit['sha'],fileHashes=source_hashes,functionHashes={name:hashlib.sha256(body.encode()).hexdigest() for name,body in functions.items()},rollbackBlockSha256=hashlib.sha256(rollback.encode()).hexdigest()),
        contexts=dict(wolfProgressInputs=7200,distinctAdvances=327,originalAuthorVectors=327,preparedTwoToFiveHitSchedules=1308),
        sharedNativeExpression=dict(commit='8949002',file='src/Sanctuary.Content/AmazonFend.cs',fileSha256=hashlib.sha256(shared.encode()).hexdigest(),expression=native_expression,executedByThisProbe=False),
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__).resolve(),js,output]},
        established=[
            'The original unmodified author functions select Fury action7/end13,start0,rollback70 and three displayed rollback intervals. The unchanged complete rollback block executes327distinct advances from the7200prepared wolf contexts. Its final displayed value includes the last full cycle; it is not an extra recovery appended after the last contact.',
            'At the selected72..520progress range,each rate-scaled rollback reaches animation frame2. The existing Fend expression prepares dynamic2..5contact clocks whose five-hit prefix/final-cycle vector equals every author output. Complete current/licensed wolf A1/A2 metadata and source count contracts are pinned separately. Lower count truncation is an explicitly prepared extension using the complete source count; it is not an author UI observation.',
        ],remaining=[
            'This executes primary original-author calculator code with declared UI/table formatting and supplied progress; it is not a current game executable trace. Actual shared native helper invocation,outer transformed event composition,live rate/gear changes,payment,hit packets,interruption and a Fury handler remain next.',
            'The older isolated scheduler works in elapsed-clock units and its missing outer animation anchors must not be silently conflated with this rate-scaled model. Original-author timing is the same supplementary model already used for prepared form progress; exact3.3dispatch remains explicitly uncertain.',
            'No canonical C#,runtime data,installed assembly,saves or final assets are changed by this preparation. Final wolf attack/contact/audio and separate icons remain pending.',
        ])
    target=ROOT/'tools/reference/druid-fury-clock-engine-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Fury clock:327unmodified author vectors/7200wolf inputs/1308prepared2..5hit schedules; Fend rollback expression reused,actual native Fury dispatch still pending.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
