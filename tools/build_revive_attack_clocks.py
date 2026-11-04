#!/usr/bin/env python3
"""Export ordinary source A1 clocks, retaining explicit unsupported rows."""
import argparse
import csv
import hashlib
import json
import struct
from collections import Counter
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    directory=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((directory/'sources.json').read_text())
    observations=json.loads((ROOT/'tools/reference/druid-beasts-user-observations.json').read_text());owned={};inputs={}
    for name in ('monstats','monstats2'):
        path=directory/'json'/(name+'.json');assert sha(path)==manifest['files']['json/'+name+'.json']['sha256']
        local=ROOT/'.local/research/d2r-3.3.93847'/(name+'-sentries-verified.txt');assert sha(local)==observations['localTableChecks']['files'][str(local.relative_to(ROOT))]['sha256']
        with local.open(encoding='utf-8-sig',newline='')as stream:owned[name]={r['Id']:r for r in csv.DictReader(stream,delimiter='\t')if r['Id']not in ('','Expansion')}
        inputs[str(path.relative_to(ROOT))]=sha(path);inputs[str(local.relative_to(ROOT))]=sha(local)
    animation=json.loads((ROOT/'tools/reference/d2r-local-animation-evidence.json').read_text());rawpath=ROOT/animation['localRawPath'];assert sha(rawpath)==animation['sha256']
    inputs[str(rawpath.relative_to(ROOT))]=sha(rawpath);raw=rawpath.read_bytes();cursor=0;records={};total=0
    for bucket in range(256):
        count=struct.unpack_from('<I',raw,cursor)[0];cursor+=4
        for _ in range(count):
            data=raw[cursor:cursor+160];cursor+=160;assert len(data)==160
            code=data[:8].split(b'\0')[0].decode('ascii');frames,speed=struct.unpack_from('<II',data,8)
            records.setdefault(code,[]).append((frames,speed,[dict(Frame=i,Kind=v)for i,v in enumerate(data[16:])if v]));total+=1
    assert cursor==len(raw)and total==3661 and len(records)==3631
    ambiguous={code:entries for code,entries in records.items()if any(entry!=entries[0]for entry in entries)}
    monsters=json.loads((directory/'json/monstats.json').read_text());models=json.loads((directory/'json/monstats2.json').read_text());rows=[]
    for name,row in monsters.items():
        original=owned['monstats'][name];model=models[row['MonStatsEx']];original_model=owned['monstats2'][row['MonStatsEx']]
        code=row.get('Code','');base=model.get('BaseW','')
        assert str(code or'')==original['Code']and str(base or'')==original_model['BaseW'],name
        key=(code+'A1'+base).upper()if isinstance(code,str)and isinstance(base,str)and code and base else''
        variants=records.get(key);entry=variants[0]if variants else None;frames,speed,flags=entry if entry else(0,0,[])
        issue='Missing ordinary A1 animation record'if entry is None else 'A1 requires multiple or nonmelee action events'if len(flags)!=1 or flags[0]['Kind']!=1 else 'A1 clock outside supported frame/rate domain'if not(1<=frames<=144 and 1<=speed<=32767 and flags[0]['Frame']<frames)else None
        if key in ambiguous:issue='Ambiguous owned A1 animation records require current lookup selection'
        if name in ('shadowwarrior','shadowmaster'):issue='Dual-wield source requires its merged-hand attack-rate service'
        rows.append(dict(Source=name,Code=key,Frames=frames,Speed=speed,Flags=flags,Issue=issue))
    assert len(rows)==751
    target=ROOT/'data/Sanctuary/Balance/revive-attack-clocks.json';encoded=json.dumps(rows,indent=2)+'\n'
    receipt=dict(referenceVersion='3.3.93847',inputs=inputs,parsedAnimationRecords=3661,uniqueAnimationCodes=3631,ambiguousRecords=ambiguous,sourceRows=751,supported=sum(r['Issue']is None for r in rows),
        refusals=dict(Counter(r['Issue']for r in rows if r['Issue'])),runtimePath=str(target.relative_to(ROOT)),runtimeSha256=hashlib.sha256(encoded.encode()).hexdigest(),
        scope='Owned animation and source/model token equality; non-dual ordinary A1 only. Native controller and modern runtime selection remain separate.')
    for path,text in ((target,encoded),(ROOT/'tools/reference/revive-attack-clock-source-evidence.json',json.dumps(receipt,indent=2)+'\n')):
        if check:assert path.read_text()==text
        else:path.write_text(text)
    print('Revive A1 metadata:751 source rows/3661 owned animation records;'+str(receipt['supported'])+' ordinary clocks,remaining rows explicitly refused.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
