#!/usr/bin/env python3
"""Run the frozen full SC scheduler with pinned Assassin weapon animation rows."""
import argparse,hashlib,json,struct,subprocess,sys
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'assassin-cast-clock-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def prepare(check=False):
    prior=json.loads((ROOT/'tools/reference/druid-form-cast-clock-engine-evidence.json').read_text())
    original=BASE/'druid-form-cast-clock-source-probe.cpp';assert sha(original)==prior['generatedSourceSha256']
    animation=ROOT/'.local/research/d2r-3.3.93847/AnimData.d2';assert sha(animation)==prior['animationSha256']
    raw=animation.read_bytes();offset=0;rows=[]
    for _ in range(256):
        count=struct.unpack_from('<I',raw,offset)[0];offset+=4
        for _ in range(count):
            code=raw[offset:offset+8].split(b'\0')[0].decode();frames,speed=struct.unpack_from('<II',raw,offset+8)
            if code.startswith('AISC'):
                flags=[(i,f) for i,f in enumerate(raw[offset+16:offset+160]) if f]
                assert frames==17 and speed==256 and len(flags)==1
                rows.append(dict(WeaponClass=code[4:],Frames=frames,Speed=speed,Action=flags[0][0],Flag=flags[0][1]))
            offset+=160
    assert offset==len(raw);rows.sort(key=lambda r:r['WeaponClass']);assert len(rows)==10
    assert [r['WeaponClass'] for r in rows]==['1HS','1HT','2HS','2HT','BOW','HT1','HT2','HTH','STF','XBW']
    catalogue=json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text())
    skills=[r for r in catalogue if r['characterClass']=='Assassin' and not r['passive'] and r['sourceFields'].get('anim')=='SC' and r['talentId'] not in ('assassin.shadowwarrior','assassin.shadowmaster')]
    assert len(skills)==7 and all(r['sourceFields']['anim']==r['sourceFields']['seqtrans']=='SC' for r in skills)
    old=original.read_text();prefix=old.split('struct Input{',1)[0]
    assert prefix.count('nSkillId=223')==1 and prefix.count('assert(id==223)')==1 and prefix.count('dwClassId=PCLASS_DRUID')==1
    prefix=prefix.replace('nSkillId=223','nSkillId=253').replace('assert(id==223)','assert(id==253)').replace('dwClassId=PCLASS_DRUID','dwClassId=PCLASS_ASSASSIN')
    cpp=prefix+'struct Input{int type,cls,frames,speed,action,event;};\nInput inputs[]={\n'
    cpp+=',\n'.join('{0,6,%d,%d,%d,%d}'%(r['Frames'],r['Speed'],r['Action'],r['Flag']) for r in rows)+'\n};\n'+old[old.index('int main(){'):]
    for symbol,h in prior['completeRoutineHashes'].items():assert hashlib.sha256(function(cpp,symbol).encode()).hexdigest()==h,symbol
    path=ROOT/'tools/reference/assassin-cast-clock-engine-evidence.json'
    if check:
        proof=json.loads(path.read_text());assert (STAGE/'probe.cpp').read_text()==cpp
        for name,h in proof['artifacts'].items():assert sha(ROOT/name)==h,name
        assert proof['animations']==rows and proof['contexts']==2590
        print('Assassin SC source recipe/2590cases verified read-only.');return
    STAGE.mkdir(exist_ok=False);(STAGE/'probe.cpp').write_text(cpp)
    with (STAGE/'compiler.log').open('wb') as output:
        subprocess.run(['g++','-std=c++20','-Wall','-Wextra','-Werror','-Wno-parentheses','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(STAGE/'probe.cpp'),'-o',str(STAGE/'probe')],stdout=output,stderr=subprocess.STDOUT,check=True)
    p=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(p.stderr)
    assert p.returncode==0 and not p.stderr,p.stderr.decode()[-2000:]
    cases=json.loads(p.stdout);assert len(cases)==2590
    for index,fcr,speed,events in cases:
        row=rows[index];assert speed==row['Speed']*min(175,100+120*fcr//(120+fcr))//100
        assert events==[[1,(row['Action']*256+speed-1)//speed,row['Flag'],0],[2,(row['Frames']*256+speed-1)//speed,0,0]]
    fixture=BASE/'assassin-cast-clock-source-probe.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    runtime=ROOT/'data/Sanctuary/Balance/assassin-cast-clock.json';runtime.write_text(json.dumps(rows,indent=2)+'\n')
    artifacts=[Path(__file__).resolve(),original,animation,ROOT/'tools/reference/druid-form-cast-clock-engine-evidence.json',fixture,runtime,*(STAGE/n for n in ('probe.cpp','probe','compiler.log','sanitizer.log'))]
    proof=dict(referenceVersion='3.3.93847',legacyCommit=prior['legacyCommit'],contexts=2590,animations=rows,completeRoutineHashes=prior['completeRoutineHashes'],currentExecutableParity=False,
        scope='Seven unchanged full source routines from the accepted Druid SC recipe;declared Assassin class/skill/metadata and event insertion services. Owned3.3.93847animation records;not full mode conversion or current event scheduler proof.',
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=sha(fixture)),
        limits=['Native first/last-frame projection,reentry/current client and generic UO weapon classification require separate acceptance.','This timing reference does not certify Assassin NPC AI,initializers or remaining class mechanics.'],
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts})
    path.write_text(json.dumps(proof,indent=2)+'\n')
    print('Assassin SC:2590 complete-source scheduling contexts across10owned weapon records;ASan/UBSan pass.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');prepare(parser.parse_args().check)
