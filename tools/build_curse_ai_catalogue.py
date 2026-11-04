#!/usr/bin/env python3
"""Join owned current AI names/descriptors to complete older transition entries."""
import argparse,csv,hashlib,json,re,struct
from pathlib import Path
from probe_warlock_runtime import Image
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    stage=ROOT/'.local/class-skills/warlock-runtime-reference';names_path=stage/'monai.txt';names_proof=stage/'monai-extraction.json'
    proof=json.loads(names_proof.read_text());image=Image()
    assert sha(names_path)==proof['sha256']and proof['referenceVersion']=='3.3.93847'and proof['executableSha256']==image.ref['executable']['sha256']
    names=list(csv.DictReader(names_path.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t'));assert len(names)==155
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    rel='source/D2Game/src/AI/AiThink.cpp';source=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=source.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel)
    table=raw.decode().split('gpAiTable_6FD3F990[] =',1)[1].split('};',1)[0]
    rows=re.findall(r'\{\s*(\d+),\s*(\w+),\s*(\w+),\s*(\w+)\s*\}',table);assert len(rows)==148
    selected=[];changed=[];excluded=[]
    for index,(flags,entry,tick,exit)in enumerate(rows):
        current=struct.unpack('<QQQQ',image.bytes(0x1d18810+index*32,32))
        assert all((pointer==0)==(name=='nullptr')for pointer,name in zip(current[1:],(entry,tick,exit)))
        if current[0]!=int(flags):changed.append(dict(index=index,family=names[index]['AI'],legacy=int(flags),current=current[0]))
        admitted=current[0]==int(flags)==1 and entry==exit=='nullptr'and tick!='nullptr'
        row=dict(index=index,family=names[index]['AI'],legacyFlags=int(flags),currentFlags=current[0],entry=entry,tick=tick,exit=exit)
        (selected if admitted else excluded).append(row)
    assert [(r['index'],r['legacy'],r['current'])for r in changed]==[(15,4,5),(23,1,3),(52,5,6)]
    assert len(selected)==92 and len({r['family'].casefold()for r in selected})==92
    families=sorted((r['family']for r in selected),key=str.casefold)
    destination=ROOT/'data/Sanctuary/Balance/curse-ai-families.json';render=json.dumps(families,indent=2)+'\n'
    for label,index in [('Skeleton',2),('Zombie',3),('Fallen',6),('Brute',7),('Goatman',12),('Megademon',89)]:assert any(r['family']==label and r['index']==index for r in selected)
    result=dict(referenceVersion='D2R Reign of the Warlock 3.3.93847',legacyCommit=tree['sha'],processMemoryRead=False,
        currentExecutableSha256=image.ref['executable']['sha256'],classicTableRva='0x1d18810',classicDescriptors=148,callbackNullPatternsChecked=444,
        selected=selected,excluded=excluded,changedFlags=changed,untracedModernIndices=list(range(148,155)),
        runtimePath=str(destination.relative_to(ROOT)),runtimeSha256=hashlib.sha256(render.encode()).hexdigest(),
        evidence={str(p.relative_to(ROOT)):sha(p)for p in [Path(__file__),names_path,names_proof,image.reference,source]},
        limits=['Owned monai names supply exact indexed family identities. All148 current classic descriptor callback-null patterns match the older table; three changed dispatch flags are excluded. Non-null entry/exit callbacks and new indices148..154 remain unsupported.',
            'This is immutable current-file metadata and the complete older transition contract,not current dispatcher execution. Runtime species walking/boss/NPC/attackable and ordinary curse eligibility still apply. A family entry does not make bosses or owned units eligible.',
            'No native UO monster receives an invented source identity. Native path/scan/ties/room/timing/global RNG and ownership/conversion transitions retain their existing explicit limits.'])
    target=ROOT/'tools/reference/curse-ai-catalogue-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==render and target.read_text()==encoded
    else:destination.write_text(render);target.write_text(encoded)
    print('Curse AI catalogue:148 owned classic descriptors/444 callback-null patterns;92 immediate families;3 flag differences excluded.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
