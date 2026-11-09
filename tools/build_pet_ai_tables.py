#!/usr/bin/env python3
"""Publish pinned normal AI descriptors and verified compiled monster AI indices."""
import argparse,csv,hashlib,json,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def build(check=False):
    oldPath=ROOT/'tools/reference/generic-pet-search-tables-data-evidence.json';old=json.loads(oldPath.read_text())
    binary=ROOT/'.local/class-skills/generic-pet-search-services/monstats.bin';text= binary.with_suffix('.txt')
    for p in [binary,text]:assert sha(p)==old['artifacts'][str(p.relative_to(ROOT))]
    monai=STAGE/'monai.txt';extraction=STAGE/'monai-extraction.json';proof=json.loads(extraction.read_text());assert sha(monai)==proof['sha256'] and proof['referenceVersion']=='3.3.93847'
    names=[r['AI']for r in csv.DictReader(monai.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t')];assert len(names)==len(set(names))==155 and names[148]=='GenericPet'
    byName={name:i for i,name in enumerate(names)};b=binary.read_bytes();assert len(b)==4+751*508 and struct.unpack_from('<I',b)[0]==751
    rows={int(r['*hcIdx']):r for r in csv.DictReader(text.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t')if r.get('*hcIdx','').isdigit()};assert set(rows)==set(range(751))
    monsters=[]
    for i in range(751):
        value=struct.unpack_from('<h',b,4+i*508+82)[0];assert value==byName.get(rows[i]['AI'],-1),(i,value,rows[i]['AI'])
        monsters.append(dict(id=i,name=rows[i]['Id'],ai=value))
    image=Image();descriptors=[];raw=image.bytes(0x1d18810,155*32)
    def function(value):
        if value==0:return 0
        rva=value-image.base;assert 0<=rva<2**31 and image.section(rva)['name']=='.text';return rva
    for i,name in enumerate(names):
        kind,padding,init,think,end=struct.unpack_from('<iIQQQ',raw,i*32);assert 0<=kind<=6
        descriptors.append(dict(id=i,name=name,type=kind,word4=padding,function8=function(init),think=function(think),function24=function(end)))
    original=STAGE/'generic-pet-candidate.json';prior=json.loads(original.read_text());assert hashlib.sha256(raw[148*32:149*32]).hexdigest()==next(r['sha256']for r in prior['reads']if r.get('index')==148)
    assert descriptors[148]['type']==0 and descriptors[148]['think']==0x4b60c0
    paths=[]
    for name,data in [('generic-pet-ai-monsters',monsters),('generic-pet-ai-descriptors',descriptors)]:
        p=ROOT/'data/Sanctuary/Balance'/(name+'.json');encoded=json.dumps(data,indent=2)+'\n'
        if check:assert p.read_text()==encoded
        else:p.write_text(encoded)
        paths.append(p)
    artifacts=[oldPath,binary,text,monai,extraction,original,image.reference,ROOT/'tools/probe_warlock_runtime.py',Path(__file__).resolve(),*paths]
    result=dict(referenceVersion='3.3.93847',monsterRows=751,normalDescriptors=155,monsterStride=508,aiOffset=82,aiSigned=True,descriptorRva='0x1d18810',descriptorStride=32,genericPetIndex=148,sourceBinaryRecoveredByHash=True,processMemoryRead=False,processInput=False,specialDescriptorsPublished=False,artifacts={str(p.relative_to(ROOT)):sha(p)for p in sorted(set(artifacts))})
    p=ROOT/'tools/reference/pet-ai-tables-data-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('AI tables:751compiled/text AI indices and155pinned normal descriptors;GenericPet148/type0;special states not invented.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
