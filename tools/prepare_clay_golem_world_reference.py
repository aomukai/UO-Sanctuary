#!/usr/bin/env python3
"""Corroborate Clay Golem model/animation data from already extracted pinned local files."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import struct
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def build(local_root,check=False):
    reference=ROOT/'tools/reference/d2r-named-equipment'
    manifest=json.loads((reference/'sources.json').read_text())
    assert manifest['commit']=='fa1286949266498b294c40c2942521a7c0d7207a'
    tables={};pins={};compared=0
    for table,filename in [('monstats','monstats-sentries-verified.txt'),('monstats2','monstats2-sentries-verified.txt')]:
        cached=reference/'json'/(table+'.json');assert sha(cached)==manifest['files']['json/'+table+'.json']['sha256']
        row=json.loads(cached.read_text())['claygolem']
        local=next(x for x in csv.DictReader((local_root/filename).read_text(encoding='utf-8-sig').splitlines(),delimiter='\t') if x['Id']=='claygolem')
        for key,value in row.items():
            if key!='lineNumber' and key in local:assert str(value)==local[key],(table,key);compared+=1
        tables[table]=row;pins[table]=dict(cachedSha256=sha(cached),localSha256=sha(local_root/filename),localFilename=filename)
    animation=local_root/'AnimData.d2';pin=json.loads((ROOT/'tools/reference/d2r-local-animation-evidence.json').read_text())
    assert sha(animation)==pin['sha256'] and animation.stat().st_size==pin['size']
    raw=animation.read_bytes();offset=0;records=[]
    for _ in range(256):
        count=struct.unpack_from('<I',raw,offset)[0];offset+=4
        for _ in range(count):
            code=raw[offset:offset+8].split(b'\0')[0].decode('ascii');frames,speed=struct.unpack_from('<II',raw,offset+8)
            if code.startswith('G1'):records.append(dict(code=code,frames=frames,speed=speed,events=[[i,v] for i,v in enumerate(raw[offset+16:offset+160]) if v]))
            offset+=160
    assert offset==len(raw) and len(records)==6
    assert [tables['monstats'][k] for k in ['AI','Code','Velocity','Run','aidel']]==['NecroPet','G1',8,8,15]
    assert [tables['monstats2'][k] for k in ['SizeX','SizeY','MeleeRng','BaseW']]==[2,2,0,'hth']
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());sources={};routines={}
    for relative,names in [
        ('source/D2Game/src/AI/AiGeneral.cpp',['AIGENERAL_AllocAiControl']),
        ('source/D2Game/src/SKILLS/Skills.cpp',['D2GAME_SummonPet_6FD14430']),
        ('source/D2Game/src/SKILLS/SkillNec.cpp',['SKILLS_SrvDo056_Golem']),
        ('source/D2Common/src/Monsters/Monsters.cpp',['MONSTERS_GetSpawnMode_XY']),
        ('source/D2Game/src/AI/AiTactics.cpp',['sub_6FCD0410'])]:
        raw=(ROOT/'.local/class-skills/d2moo-pinned-source'/relative).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        sources[relative]=hashlib.sha256(raw).hexdigest()
        for name in names:routines[name]=hashlib.sha256(function(raw.decode(),name).encode()).hexdigest()
    result=dict(referenceVersion='3.3.93847',primaryDataCommit=manifest['commit'],legacyCommit=tree['sha'],
        localTables=pins,corroboratedFields=compared,currentRows=tables,
        animation=dict(sha256=pin['sha256'],archivePath=pin['archivePath'],extractionEvidence='tools/reference/d2r-local-animation-evidence.json',records=records),
        reviewedSourceHashes=sources,reviewedRoutineHashes=routines,
        established=['G1 has A116frames/speed232/action1at index9,S116/128,WL8/128,NU4/40,DT16/168,DD1/256. No GH,BL,RN or A2 row was found;do not borrow the G4 or SK rows.',
            'Current model size2x2,raw melee range0,base weapon classhth and intrinsicCrit5. Velocity8/run8and AI NecroPet use the existing AI67policy. The current native generic720mscycle/360msaction is not an event-clock certificate.',
            'The source summon allocator first-think25frames and current S1metadata are distinct inputs. Zero-filled AI initialization and approach path13are static source reviews,not complete spawn/AI execution. No new actor or runtime table is changed.'],
        remaining=['Source G1world/attack clock,unarmed damage/critical,seed ownership,initial S1/AI overlap,native motion/contact and full current-executable/event/client equivalence still need integration. Reuse existing actor/random/resource/contact services;do not invent item equipment or animation rows.'])
    path=ROOT/'tools/reference/clay-golem-world-preflight.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==rendered
    else:path.write_text(rendered)
    print(f'Clay Golem world reference:{compared} current table fields,6 owned G1 animation rows,5 static source routines; no actor acceptance claimed.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--local-root',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();build(a.local_root,a.check)
