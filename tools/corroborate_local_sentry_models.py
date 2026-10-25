#!/usr/bin/env python3
"""Compare six trap monster/model rows with strict local pinned-build reads.

Default/check mode uses the bounded retained text files. --extract repeats the
read-only CASC extraction only while the actual installation still has the pin.
No monster-mode binary ABI, animation file or executable behavior is inferred.
"""
import argparse,csv,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'
EXPECTED={'monstats':'8831fce35fc549a4849b13a1bd467eb34913f58b8269c0c1bbfa76aeabb6d32e',
          'monstats2':'a5d0f396d7916299f0925ebfbe13fbf61f1ded6d4b0577fa861cca81bac04c92'}
IDS=['chargeboltsentry','wakeofdestruction','lightningsentry','infernosentry','deathsentry','bladecreeper']


def build(check=False,extract=False):
    installation=json.loads((ROOT/'tools/reference/d2r-local-installation-evidence.json').read_text())
    mode=json.loads((ROOT/'tools/reference/d2r-local-monmode-evidence.json').read_text())
    reader=mode['reader'];assert hashlib.sha256((ROOT/reader['sourcePath']).read_bytes()).hexdigest()==reader['sourceSha256']
    assert installation['referenceVersion']==installation['buildInfo']['version']=='3.3.93847'
    if extract:
        build_info=Path(installation['installationPath'])/'.build.info'
        assert hashlib.sha256(build_info.read_bytes()).hexdigest()==installation['buildInfo']['sha256']
        for name in EXPECTED:
            target=ROOT/f'.local/research/d2r-3.3.93847/{name}-sentries-verified.txt'
            subprocess.run([str(STAGE/'casc-reference-reader'),installation['installationPath'],f'data:data/global/excel/{name}.txt',str(target)],check=True)
        assert hashlib.sha256(build_info.read_bytes()).hexdigest()==installation['buildInfo']['sha256']
    manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text())
    files={};compared={};total=0;important={}
    for name,digest in EXPECTED.items():
        path=ROOT/f'.local/research/d2r-3.3.93847/{name}-sentries-verified.txt';raw=path.read_bytes()
        assert hashlib.sha256(raw).hexdigest()==digest
        rows=list(csv.DictReader(raw.decode('utf-8-sig').splitlines(),delimiter='\t'))
        local={row['Id']:row for row in rows};assert len(local)==len(rows)
        pin=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';pinned_raw=pin.read_bytes()
        pin_hash=hashlib.sha256(pinned_raw).hexdigest();assert pin_hash==manifest['files'][f'json/{name}.json']['sha256'];pinned=json.loads(pinned_raw)
        files[name]=dict(path=str(path.relative_to(ROOT)),archivePath=f'data:data/global/excel/{name}.txt',size=len(raw),sha256=digest,tableRows=len(rows),pinnedJsonSha256=pin_hash)
        compared[name]={}
        for ident in IDS:
            actual=local[ident];expected=pinned[ident];fields=[]
            for field,value in expected.items():
                if field in actual and not field.startswith('*') and field!='lineNumber':
                    assert actual[field]==str(value),(name,ident,field,value,actual[field]);fields.append(field)
            extra={k:v for k,v in actual.items() if k not in expected and not k.startswith('*') and v not in ('','0')}
            assert not extra,(name,ident,extra)
            compared[name][ident]=dict(matchingFields=len(fields),fields=fields,nonzeroFieldsMissingFromPin=extra);total+=len(fields)
        wanted=['AI','Code','aidel','aip1','aip2','aip3','aip4','Align','noRatio','minHP','maxHP','AC','MinHP(N)','MaxHP(N)','AC(N)','MinHP(H)','MaxHP(H)','AC(H)','Skill1','Sk1mode'] if name=='monstats' else ['SizeX','SizeY','mDT','mNU','mS1','mS2','mDD','InfernoLen','InfernoAnim']
        important[name]={key:local['infernosentry'][key] for key in wanted}
    assert total==601
    assert (important['monstats']['aip4'],important['monstats']['Sk1mode'])==('13','seq_infernosentry')
    assert (important['monstats2']['InfernoLen'],important['monstats2']['InfernoAnim'])==('14','9')
    e=dict(referenceVersion='3.3.93847',installationEvidence='tools/reference/d2r-local-installation-evidence.json',buildInfoSha256=installation['buildInfo']['sha256'],reader=reader,files=files,compared=compared,totalMatchingFields=total,infernoFields=important,
        established=['Strict local read-only CASC text extraction while build metadata matched3.3.93847 before/after yielded494490-byte monstats and140282-byte monstats2.',
                     'All601 available nonmetadata pinned fields across six trap monster/model pairs match exactly; no extra nonempty/nonzero local field is missing from any pinned row. Zero/blank defaults and metadata columns are not equated with active fields.',
                     'Current Inferno row confirms AI AssassinSentry, active distance13, seq_infernosentry, model InfernoLen14/InfernoAnim9 and zero SizeX/SizeY. Normal/Nightmare/Hell fixed100life/AC bases and noRatio1 are directly corroborated.'],
        limits=['Text rows only; no model animations, binary monstats compiler/ABI, modern executable/control flow, missile combat, rendering, retirement or learned skill is certified.',
                'Only the six named monster/model rows are compared. Other monsters and the complete current executable remain outside this certificate. Runtime difficulty scaling uses approved Sanctuary owner-facet rules, not a new inference from these rows.'])
    out=ROOT/'tools/reference/d2r-local-sentry-model-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Local sentry model evidence stale'
    else:out.write_text(encoded)
    print('Current sentry monster/models:601 fields across6pairs match strict local text; Inferno13range/14end/9animation confirmed. No runtime certificate.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');p.add_argument('--extract',action='store_true');args=p.parse_args();build(args.check,args.extract)
