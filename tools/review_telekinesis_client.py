#!/usr/bin/env python3
"""Bind the isolated client build and exact complete patch-stack round trip."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sorceress-telekinesis-world'
NATIVE=Path('/media/aomukai/FILES/Sanctuary/upstream/ClassicUO')


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def review(check=False):
    client=STAGE/'client-root/upstream/ClassicUO'
    pin=subprocess.check_output(['git','-C',str(NATIVE),'rev-parse','HEAD'],text=True).strip()
    assert pin=='ee79d7ebc1cc0e53ff84fe6cae17d389e2737f93'
    patches=sorted((ROOT/'patches/ClassicUO').glob('*.patch'))
    assert len(patches)==38
    names=set()
    for p in patches:
        for r in subprocess.check_output(['git','apply','--numstat','-z',str(p)]).split(b'\0'):
            if r:names.add(r.decode().split('\t',2)[2])
    current={n:sha(client/n) for n in sorted(names) if (client/n).exists()}
    with tempfile.TemporaryDirectory(prefix='sanctuary-tk-client-') as tmp:
        tmp=Path(tmp);subprocess.run(['git','init','-q'],cwd=tmp,check=True)
        for n in current:
            (tmp/n).parent.mkdir(parents=True,exist_ok=True);shutil.copy2(client/n,tmp/n)
        for p in reversed(patches):subprocess.run(['git','apply','-R',str(p)],cwd=tmp,check=True)
        for n in names:
            base=subprocess.run(['git','-C',str(NATIVE),'show',pin+':'+n],capture_output=True)
            if base.returncode==0:assert (tmp/n).read_bytes()==base.stdout,n
            else:assert not (tmp/n).exists(),n
        for p in patches:subprocess.run(['git','apply',str(p)],cwd=tmp,check=True)
        assert {n:sha(tmp/n) for n in names if (tmp/n).exists()}==current
    log=STAGE/'client-build.log';text=log.read_text()
    assert 'Build succeeded.' in text and '74 Warning(s)' in text and '0 Error(s)' in text
    result=dict(nativeCommit=pin,patches=38,affectedPaths=len(names),roundTrip='reverse to exact pin, then forward to exact built input',
                graphicalInputVerified=False,buildWarnings=74,buildErrors=0,
                limitation='Build validates the complete patched client. Server wire fixtures are separate; no graphical selection/cursor verification or fresh warning baseline is claimed.',
                patchHashes={str(p.relative_to(ROOT)):sha(p) for p in patches},clientInputs=current,
                artifacts={str(p.relative_to(ROOT)):sha(p) for p in (Path(__file__).resolve(),log,STAGE/'client/cuo.dll')})
    output=ROOT/'tools/reference/telekinesis-client-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print(f'ClassicUO:{len(patches)}patches/{len(names)}paths round-trip exactly; client build succeeds with74warnings/0errors; graphical input remains unverified.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');review(parser.parse_args().check)
