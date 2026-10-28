#!/usr/bin/env python3
"""Extend pinned Fury rollback vectors to every legal normalized wolf advance."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    priorpath=ROOT/'tools/reference/druid-fury-clock-engine-evidence.json';prior=json.loads(priorpath.read_text())
    for name,digest in prior['artifacts'].items():assert sha(ROOT/name)==digest,name
    model=ROOT/'src/Sanctuary.Content/DruidFormRates.cs';text=model.read_text()
    for exact in ['Math.Clamp(eias,-85L,150L)','input.HumanFrames is not(16 or 17 or 19 or 20 or 21 or 23)','int frames=input.Form==CombatAbility.Werewolf?13:12']:
        assert exact in text,exact
    low=(256+int(256*-85/100))*13//23;high=(256+256*150//100)*13//16;assert (low,high)==(22,520)
    original=BASE/'druid-fury-clock-source-probe.mjs';script=original.read_text();rates=list(range(low,high+1))
    script,count=re.subn(r'const rates=\[[^;\n]*\];','const rates='+json.dumps(rates)+';',script);assert count==1
    source=BASE/'druid-fury-full-clock-source-probe.mjs';source.write_text(script);author=json.loads(subprocess.check_output(['node',str(source)]));assert len(author)==499
    prepared=[]
    for rate,start,action,end,factor,hits,rows,other in author:
        assert (start,action,end,factor,hits)==(0,7,13,70,3) and other==[] and len(rows)==1
        five=None
        for count in [2,3,4,5]:
            at=0;frame=0;events=[];starts=[];until=0
            for _ in range(count):
                starts.append(frame);elapsed=((7-frame)*256+rate-1)//rate;until=at+((13-frame)*256+rate-1)//rate-1;at+=elapsed;events.append(at)
                frame=((frame*256+rate*elapsed)//256)*30//100
            assert starts==[0]+[2]*(count-1) and until>=events[-1]
            prepared.append(dict(advance=rate,count=count,events=events,end=until,starts=starts));five=prepared[-1]
        e=five['events'];assert rows[0]==[e[0],e[1]-e[0],e[2]-e[1],e[3]-e[2],five['end']-e[3]]
    assert len(prepared)==1996
    output=BASE/'druid-fury-full-clock-source-probe.json';output.write_text(json.dumps(dict(authorRows=author,preparedSchedules=prepared),separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',sourcePreparation=True,handlerEnabled=False,nativeAccepted=False,currentExecutableParity=False,
        predecessor=dict(path=str(priorpath.relative_to(ROOT)),sha256=sha(priorpath)),normalizedModelSha256=sha(model),legalAdvance=dict(minimum=22,maximum=520),
        contexts=dict(originalAuthorVectors=499,preparedSchedules=1996,contacts=6986),artifacts={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__).resolve(),priorpath,model,source,output]},
        established=['Original unmodified author functions and complete rollback block execute every integer22..520advance admitted by the existing normalized wolf model. This covers extreme slowing below the earlier7200sampled contexts. All two-to-five-contact preparations preserve frame2rollback and the final whole-cycle convention; five-hit vectors equal every author output.'],
        remaining=['Prepared lower-rank truncation and original-author model limits remain explicit. Actual DruidFury.Schedule/contact/input/gear/recovery/retarget/persistence tests are required; this source script does not certify native gameplay or exact current executable timing. Old327-vector and native Fend-helper proofs remain frozen and unmodified.'])
    target=ROOT/'tools/reference/druid-fury-full-clock-engine-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Fury full clock:499legal22..520advances/1996prepared2..5hit schedules/6986contacts; original author vectors pass,actual Fury native tests next.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
