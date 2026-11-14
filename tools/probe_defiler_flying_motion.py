#!/usr/bin/env python3
"""Reuse authenticated complete movement routines with explicit per-path masks."""
import argparse,hashlib,json,re,struct,subprocess,csv
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/defiler-flying-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def bodies(c,expected):
    for name,h in expected.items():
        m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',c,re.M);assert m,name
        i=c.index('{',m.start())+1;depth=1
        while depth:depth+=(c[i]=='{')-(c[i]=='}');i+=1
        assert hashlib.sha256(c[m.start():i].encode()).hexdigest()==h,name

def build(check=False):
    STAGE.mkdir(exist_ok=True);inputs={}
    def pin(name):
        p=ROOT/name;inputs[name]=sha(p);return json.loads(p.read_text())if p.suffix=='.json'else p.read_bytes()
    prior=pin('tools/reference/actor-motion-engine-evidence.json')
    cpp=pin('.local/class-skills/actor-motion-source-probe.cpp').decode();bodies(cpp,prior['compiledRoutineHashes'])
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit']
    for rel,h in prior['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/rel)==h
    def replace(a,b):
        nonlocal cpp
        assert cpp.count(a)==1,(a,cpp.count(a));cpp=cpp.replace(a,b)
    replace('int nVelocity=12,nWalkAnimSpeed=128','int nVelocity=6,nWalkAnimSpeed=256')
    replace('for(int route=0;route<6;route++)for(int fraction:{0,32768,57344})for(int percent:{0,25,75,100,150})for(int faster:{0,50})for(int wall:{0,1,2})for(int accel:{0,-256,256})for(int save:{0,1})',
        'for(int mask:{0x3c01,0x3401,0x1804})for(int route:{0,2})for(int fraction:{0,32768})for(int percent:{100})for(int faster:{0})for(int wall:{0,1,4,0x100,0x400,0x800,0x1000,0x2000})for(int accel:{0})for(int save:{0,1})')
    replace('D2UnitStrc u;D2DynamicPathStrc p;u.pDynamicPath=&p;', 'D2UnitStrc u;D2DynamicPathStrc p;p.nMoveTestCollisionMask=mask;u.pDynamicPath=&p;')
    replace('if(wall==1)for(int y=80;y<128;y++)cells[(y-80)*48+23]=1;if(wall==2)for(int x=80;x<128;x++)cells[22*48+x-80]=0x100;',
        'if(wall)for(int y=80;y<128;y++)cells[(y-80)*48+23]=wall;')
    replace('[%d,%d,%d,%d,%d,%d,%d],\\\"velocity', '[%d,%d,%d,%d,%d,%d,%d,%d],\\\"velocity')
    replace('route,fraction,percent,faster,wall,accel,save,p.dwMaxVelocity','route,fraction,percent,faster,wall,accel,save,mask,p.dwMaxVelocity')
    bodies(cpp,prior['compiledRoutineHashes'])
    path=STAGE/'probe.cpp';path.write_text(cpp);binary=STAGE/'probe'
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],check=True,stdout=(STAGE/'compiler.log').open('w'),stderr=subprocess.STDOUT)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr[-2000:]
    cases=json.loads(run.stdout);assert len(cases)==192 and all(len(c['frames'])==20 and c['velocity']==1536 and c['animation']==256 for c in cases)
    (STAGE/'cases.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    owned=pin('tools/reference/d2r-local-sentry-model-evidence.json');rows={}
    for name in ('monstats','monstats2'):
        entry=owned['files'][name];raw=pin(entry['path']);assert sha(ROOT/entry['path'])==entry['sha256']
        rows[name]=next(r for r in csv.DictReader(raw.decode().lstrip('\ufeff').splitlines(),delimiter='\t')if r['Id']=='warputriddefiler')
    first,second=rows['monstats'],rows['monstats2']
    assert first['Code']=='45'and first['Velocity']==first['Run']=='6'and first['flying']=='1'and first['opendoors']=='1'
    assert second['SizeX']==second['SizeY']=='2'and second['BaseW']=='hth'and second['mWL']=='1'and second['mRN']==''
    meta=pin('tools/reference/generic-pet-secondary-data-evidence.json');raw=pin('.local/class-skills/generic-pet-search-services/monstats.bin')
    assert inputs['.local/class-skills/generic-pet-search-services/monstats.bin']==meta['artifacts']['.local/class-skills/generic-pet-search-services/monstats.bin']
    flags=raw[4+744*508+241]
    anim=pin('.local/research/d2r-3.3.93847/AnimData.d2');assert hashlib.sha256(anim).hexdigest()=='61ea70ede691e88a4bf375d872668d74d6cdca4ed5235034188ed1eb0d1e8ee5'
    pos=0;matches={}
    for _ in range(256):
        count=struct.unpack_from('<I',anim,pos)[0];pos+=4
        for _ in range(count):
            row=anim[pos:pos+160];pos+=160;code=row[:8].split(b'\0')[0].decode()
            if code in ('45WLHTH','45S1HTH'):
                frames,speed=struct.unpack_from('<ii',row,8);matches[code]=dict(frames=frames,speed=speed,events=[[i,b]for i,b in enumerate(row[16:])if b])
    assert matches=={'45WLHTH':dict(frames=10,speed=256,events=[]),'45S1HTH':dict(frames=16,speed=128,events=[[12,1]])}
    row=dict(ClassId=744,Size=2,Velocity=6,WalkCode='45WLHTH',WalkFrames=10,WalkSpeed=256,Flags241=flags,Flying=True)
    output=ROOT/'data/Sanctuary/Balance/defiler-flying-motion.json';encoded=json.dumps([row],indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    pin(str(output.relative_to(ROOT)));pin('tools/probe_defiler_flying_motion.py')
    result=dict(referenceVersion='3.3.93847',component='Defiler flying-mask source inputs',legacyCommit=prior['legacyCommit'],cases=192,frames=3840,
        compiledRoutineHashes=prior['compiledRoutineHashes'],source=row,animations=matches,newLiveReferenceReads=0,automaticDefilerEnabled=False,
        established=['All complete retained actor-motion routine bodies unchanged;only synthetic main/grid/path mask and base6/walk256 suppliers differ.',
            'Explicit masks0x3c01/0x3401/0x1804 and isolated collision bits exercise FP16 motion,query ordering,stop/pass-through and saved crossings.',
            'Owned current monstats/monstats2/AnimData establish744,flying2x2,velocity6,walk10frames/speed256,S1sixteenframes/speed128/action12flag1.'],
        limits=['Pattern0 synthetic source grid does not establish size2 occupancy or native world terrain mapping.','Legacy mask selection is not proof of current fresh path constructor.','No automatic host,learned cast or native flying-world admission is enabled.'],
        artifacts={str(p.relative_to(ROOT)):sha(p)for p in (path,binary,STAGE/'cases.json',STAGE/'compiler.log',STAGE/'sanitizer.log')},inputs=inputs)
    p=ROOT/'tools/reference/defiler-flying-motion-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Defiler flying source:192contexts/3840frames;all source routine bodies unchanged,ASan/UBSan clean;current2x2/6/256 and S1animation pinned. Native terrain still unqualified.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
