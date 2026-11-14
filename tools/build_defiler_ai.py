#!/usr/bin/env python3
"""Retain the owned Defiler NPC405 AI scorer row without replacing its policy."""
import argparse,csv,hashlib,json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name;inputs[name]=sha(p);return json.loads(p.read_text())if p.suffix=='.json'else p.read_bytes()
    meta=pin('tools/reference/generic-pet-metadata-data-evidence.json');raw=pin('.local/class-skills/defiler-compiled-formula/skills.bin')
    assert inputs['.local/class-skills/defiler-compiled-formula/skills.bin']in meta['artifacts'].values()
    assert struct.unpack_from('<I',raw)[0]==429 and len(raw)==4+429*748
    r=raw[4+405*748:4+406*748];assert struct.unpack_from('<h',r)[0]==405 and r[728]==2
    rows=[dict(id=405,flags37=r[37],score=dict(flags36=r[36],flags39=r[39],quarter532=struct.unpack_from('<h',r,532)[0],bonus730=struct.unpack_from('<h',r,730)[0],element644=r[644],state162=struct.unpack_from('<h',r,162)[0],class272=struct.unpack_from('<h',r,272)[0],ownerCompare274=struct.unpack_from('<b',r,274)[0],calc276=struct.unpack_from('<i',r,276)[0]))]
    assert rows[0]['score']==dict(flags36=0,flags39=128,quarter532=1,bonus730=0,element644=0,state162=210,class272=-1,ownerCompare274=-1,calc276=-1)and rows[0]['flags37']==4
    owned=pin('tools/reference/d2r-local-sentry-model-evidence.json');entry=owned['files']['monstats'];data=pin(entry['path']);assert sha(ROOT/entry['path'])==entry['sha256']
    mon=next(x for x in csv.DictReader(data.decode().lstrip('\ufeff').splitlines(),delimiter='\t')if x['Id']=='warputriddefiler')
    assert [mon[f'Skill{i}']for i in range(1,9)]==['Health Link','','','','','','','']and mon['Sk1mode']=='S1'
    p=ROOT/'data/Sanctuary/Balance/defiler-ai-scores.json';encoded=json.dumps(rows,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    pin(str(p.relative_to(ROOT)));pin('tools/build_defiler_ai.py')
    result=dict(referenceVersion='3.3.93847',component='Owned Defiler NPC405 type2 AI metadata',sourceClass=744,slots=[405],modes=[8],rows=rows,newLiveReferenceReads=0,inputs=inputs,
        limits=['Actual source root/search/state/capacity/action binding requires native verification.','Base mode4/type2 from the owned parameter row is retained;it is not an authorization for ordinary melee.'])
    p=ROOT/'tools/reference/defiler-ai-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Owned Defiler744:one405/S1/type2 slot;state210,flags0/4/2/128 retained.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
