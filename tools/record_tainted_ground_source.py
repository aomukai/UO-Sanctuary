#!/usr/bin/env python3
"""Authenticate Tainted species inputs from the already-owned pinned files."""
import argparse,csv,hashlib,json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    inputs={}
    def pin(p):
        inputs[str(p.relative_to(ROOT))]=sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else p.read_bytes()
    owned=pin(ROOT/'tools/reference/d2r-local-sentry-model-evidence.json');rows={}
    for name in ('monstats','monstats2'):
        entry=owned['files'][name];p=ROOT/entry['path'];raw=pin(p);assert sha(p)==entry['sha256']
        rows[name]=next(r for r in csv.DictReader(raw.decode().lstrip('\ufeff').splitlines(),delimiter='\t')if r['Id']=='warbighead')
    first,second=rows['monstats'],rows['monstats2']
    assert first['Code']=='BH'and first['Velocity']==first['Run']=='6'and first['flying']==''and first['opendoors']=='1'
    assert second['SizeX']==second['SizeY']=='3'and second['BaseW']=='hth'and second['mWL']=='1'and second['mRN']==''
    metadata=pin(ROOT/'tools/reference/generic-pet-secondary-data-evidence.json')
    p=ROOT/'.local/class-skills/generic-pet-search-services/monstats.bin';raw=pin(p)
    assert sha(p)==metadata['artifacts'][str(p.relative_to(ROOT))]and len(raw)==4+751*508 and struct.unpack_from('<I',raw)[0]==751
    flags=raw[4+743*508+241];assert flags==0
    p=ROOT/'.local/research/d2r-3.3.93847/AnimData.d2';raw=pin(p)
    assert sha(p)=='61ea70ede691e88a4bf375d872668d74d6cdca4ed5235034188ed1eb0d1e8ee5'
    pos=0;animation=None
    for bucket in range(256):
        count=struct.unpack_from('<I',raw,pos)[0];pos+=4
        for _ in range(count):
            row=raw[pos:pos+160];pos+=160
            if row[:8].split(b'\0')[0]==b'BHWLHTH':
                frames,speed=struct.unpack_from('<ii',row,8);animation=dict(code='BHWLHTH',frames=frames,speed=speed,events=[[i,b]for i,b in enumerate(row[16:])if b])
    assert pos==len(raw)and animation==dict(code='BHWLHTH',frames=8,speed=176,events=[])
    expected=dict(ClassId=743,Size=3,Velocity=6,WalkCode='BHWLHTH',WalkFrames=8,WalkSpeed=176,Flags241=flags)
    assert pin(ROOT/'data/Sanctuary/Balance/tainted-ground-motion.json')==[expected]
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',component='Tainted ground-motion species inputs',liveReferenceReads=0,source=expected,
        animation=animation,ground=True,opensDoors=True,runAnimation=False,
        reuse='Existing SourceActorMotion,SourceActorPath,Goatman native walking/follow/warp and PetGroundWorld adapters;Tainted owns its capture and movement stat suppliers.',
        limits=['Does not certify D2 constructor/global clock or UO terrain/crowd projection.','No learned Tainted cast,automatic AI admission or Blood Boil is enabled by this species input.','Flying Defiler is excluded.'],inputs=inputs)
    path=ROOT/'tools/reference/tainted-ground-motion-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Tainted ground source evidence stale'
    else:path.write_text(encoded)
    print('Owned3.3.93847Tainted:ground3x3,velocity6,BHWLHTH8frames/speed176,Flags241=0;no fresh process reads.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
