#!/usr/bin/env python3
"""Execute complete source target filter for initial versus child chain flags."""
import argparse,ast,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'chain-child-filter-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    parent=ROOT/'tools/probe_lightning_fury_reference.py'
    cpp=next(n.value for n in ast.walk(ast.parse(parent.read_text()))if isinstance(n,ast.Constant)and isinstance(n.value,str)
        and n.value.startswith('#include <algorithm>')and 'bool COLLISION_RayTrace'in n.value)
    skills=BASE/'d2moo-pinned-source/source/D2Game/src/SKILLS/Skills.cpp';miss=BASE/'d2moo-pinned-source/source/D2Game/src/MISSILES/MissMode.cpp'
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    for p in(skills,miss):
        raw=p.read_bytes();rel=str(p.relative_to(BASE/'d2moo-pinned-source'))
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel)
    body=function(skills.read_text(),'sub_6FD0FA00');cpp+=body
    hit=function(miss.read_text(),'MISSMODE_SrvHit12_ChainLightning_LightningStrike')
    assert 'nRange, 0x88583u, pUnit->dwUnitId, 0)'in hit
    declaration='bool COLLISION_RayTrace(D2ActiveRoomStrc*,D2CoordStrc*,D2CoordStrc*,unsigned){return wall;}'
    assert declaration in cpp
    cpp=cpp.replace(declaration,'int rayCalls=0;'+declaration.replace('return wall;','rayCalls++;return wall;'))
    cpp+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool comma=false;printf("[");
for(int filter:{0x88583,0x8A783})for(int blocked:{0,1})for(int hostile:{0,1})for(int justHit:{0,1})for(int town:{0,1}){
caster={};caster.dwUnitType=0;caster.room=&rooms[0];targets[0]={};auto&t=targets[0];t.dwUnitType=1;t.dwAnimMode=1;t.dwFlags=1152;t.room=&rooms[1];t.hostile=hostile;t.justHit=justHit;rooms[1].town=town;monsters[0]={};wall=blocked;rayCalls=0;
int result=sub_6FD0FA00(&caster,&t,filter);
printf("%s[%d,%d,%d,%d,%d,%d,%d]",comma?",":"",filter,blocked,hostile,justHit,town,result,rayCalls);comma=true;
}printf("]\n");}
'''
    STAGE.mkdir(exist_ok=True);source=STAGE/'probe.cpp';source.write_text(cpp)
    compiled=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(source),'-o',str(STAGE/'probe')],capture_output=True)
    (STAGE/'compiler.log').write_bytes(compiled.stdout+compiled.stderr);assert compiled.returncode==0,compiled.stderr.decode()[-3000:]
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
    rows=json.loads(run.stdout);assert len(rows)==32
    for f,blocked,hostile,justHit,town,result,rays in rows:
        eligible=hostile and not justHit and not town
        assert result==int(eligible and(not(f&0x200)or not blocked))
        assert rays==int(eligible and bool(f&0x200))
    cases=STAGE/'cases.json';cases.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    result=dict(referenceVersion='D2R Reign of the Warlock 3.3.93847',legacyCommit=tree['sha'],cases=len(rows),completeFilterSha256=hashlib.sha256(body.encode()).hexdigest(),reviewedSharedHit12Sha256=hashlib.sha256(hit.encode()).hexdigest(),
        evidence={str(p.relative_to(ROOT)):sha(p)for p in [Path(__file__),parent,skills,miss,source,cases,STAGE/'compiler.log',STAGE/'sanitizer.log']},
        established=['Shared complete hit12 selects children with0x88583. Complete filter makes no caster-to-target raytrace for this mask,including when the supplied ray would be blocked. Initial0x8A783control does request LOS and rejects a blocked ray.',
            'Both masks retain supplied hostility,JustHit and town rejection in the covered living/valid/attackable monster domain. Child segment terrain collision is a separate travel consumer.'],
        limits=['Types/modes/flags,hostility,room,state and raytrace results are declared services. Hit12 identity/filter constant is reviewed and also covered by the retained2560compiled child-flow cases; full source world traversal is not executed here.',
            'This does not resolve Lightning Strike initial Y/Y aim,primary re-hit/solo damage,modern NHD identity,full room/filter/current-code/owner/client parity.'])
    target=ROOT/'tools/reference/chain-child-filter-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Chain child filter:32 complete source filter contexts;child0x88583has no caster LOS;initial0x8A783control keeps LOS.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
