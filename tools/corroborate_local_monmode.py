#!/usr/bin/env python3
"""Compare strictly extracted current monster-mode text and bounded binary rows."""
import argparse,csv,hashlib,json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
EXPECTED={'txt':'2663c45b5ca74916db0114335babe7fc6dd9d357322892df501f7850e4385b52',
          'bin':'71792aaa383158063667adddc1dd22a0ddea1cb03ecb076a80f4bf8db4df6a9d'}


def build(check=False):
    installation=json.loads((ROOT/'tools/reference/d2r-local-installation-evidence.json').read_text())
    parent=json.loads((ROOT/'tools/reference/d2r-local-animation-evidence.json').read_text())
    reader=parent['reader'];assert hashlib.sha256((ROOT/reader['sourcePath']).read_bytes()).hexdigest()==reader['sourceSha256']
    assert installation['buildInfo']['version']=='3.3.93847'
    raw={};files={}
    for suffix in ['txt','bin']:
        p=ROOT/f'.local/research/d2r-3.3.93847/monmode.{suffix}';raw[suffix]=p.read_bytes()
        assert hashlib.sha256(raw[suffix]).hexdigest()==EXPECTED[suffix]
        files[suffix]=dict(path=str(p.relative_to(ROOT)),archivePath=f'data:data/global/excel/monmode.{suffix}',size=len(raw[suffix]),sha256=EXPECTED[suffix])
    rows=list(csv.DictReader(raw['txt'].decode('utf-8-sig').splitlines(),delimiter='\t'))
    count=struct.unpack_from('<I',raw['bin'])[0];assert count==len(rows)==16 and len(raw['bin'])==4+36*count
    parsed=[]
    for i,row in enumerate(rows):
        record=raw['bin'][4+i*36:4+(i+1)*36]
        name=record[:32].split(b'\0')[0].decode('ascii');token=record[32:].rstrip(b'\0 ').decode('ascii')
        assert name==row['name'] and token==row['token']
        parsed.append(dict(index=i,name=name,code=row['code'],token=token))
    assert parsed[14]==dict(index=14,name='sequence',code='xx',token='xx')
    assert parsed[13]==dict(index=13,name='knockback',code='KB',token='GH')
    e=dict(referenceVersion='3.3.93847',installationEvidence='tools/reference/d2r-local-installation-evidence.json',
        buildInfoSha256=installation['buildInfo']['sha256'],reader=reader,files=files,rows=parsed,sequence=parsed[14],
        established=['Strict read-only local CASC extraction yielded current monmode.txt238bytes and monmode.bin580bytes while pinned build metadata still matched3.3.93847.',
                     'All16text row names/tokens match the bounded binary header and36-byte row layout. Sequence is index14, with code/token xx. Current skill311 monanim xx therefore selects the sequence14 branch in the paired reconstructed source probe.',
                     'Code and animation token are distinct: knockback index13 uses code KB but token GH. The binary comparison validates stored name/token, not a guessed stored code field.'],
        limits=['This is a bounded data-layout comparison, not a general modern binary compiler/parser or executable certificate. The modern36-byte binary rows differ from the old four-byte stub and are not parsed using the old ABI.',
                'Sequence identity does not by itself prove live action, rewind/density, AI, rate, event ordering, missile collision or retirement. Those require their own composed source/native evidence.'])
    out=ROOT/'tools/reference/d2r-local-monmode-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Local monster-mode evidence stale'
    else:out.write_text(encoded)
    print('Current monster modes:16text/binary rows match; xx is sequence index14; code and token remain distinct.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
