#!/usr/bin/env python3
"""Compose complete older Chilling Armor event01 with the shared factory."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/chilling-armor-source'
LEGACY=Path('/home/aomukai/.codex/worktrees/death-sentry-damage/Sanctuary/.local/class-skills/druid-rabies-factory-source-probe.cpp')


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def probe(check=False):
    STAGE.mkdir(parents=True,exist_ok=True)
    parent=ROOT/'tools/reference/druid-rabies-factory-engine-evidence.json';prior=json.loads(parent.read_text())
    expected=prior['artifacts']['.local/class-skills/druid-rabies-factory-source-probe.cpp']
    retained=STAGE/'reused-factory.cpp'
    if not retained.exists():assert sha(LEGACY)==expected;shutil.copy2(LEGACY,retained)
    assert sha(retained)==expected
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    paths={'source/D2Game/src/MISSILES/Missiles.cpp':'MISSILES_CreateMissileFromParams',
           'source/D2Game/src/SKILLS/SkillSor.cpp':'SKILLS_EventFunc01_ChillingArmor'}
    sources,blocks={},{}
    for rel,name in paths.items():
        p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
        sources[rel]=sha(p);blocks[name]=function(raw.decode(),name)
    cpp=retained.read_text().split('int main()',1)[0]
    factory=blocks['MISSILES_CreateMissileFromParams'];assert function(cpp,'MISSILES_CreateMissileFromParams')==factory
    cpp=cpp.replace('D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasSkill?&skill:nullptr;}',
                    'D2MissilesTxt incoming;bool hasIncoming=true;D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasSkill?&skill:nullptr;}')
    cpp=cpp.replace('D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return hasRecord?&row:nullptr;}',
                    'D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int id){return id==991?(hasIncoming?&incoming:nullptr):(hasRecord?&row:nullptr);}')
    wrapper='namespace ArmorFactory{\n'+factory+'\n}\nint factoryCalls=0;D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*g,D2MissileStrc*p){factoryCalls++;captured=*p;return ArmorFactory::MISSILES_CreateMissileFromParams(g,p);}\n'
    cpp=cpp.replace(factory,wrapper)
    cpp+='\nstruct D2DamageStrc{};\n'+blocks['SKILLS_EventFunc01_ChillingArmor']
    cpp+=r'''
int main(){for(int n=0;n<32;n++)gdwBitMasks[n]=1u<<n;bool first=true;printf("[");
for(int guard=0;guard<10;guard++)for(int world=0;world<4;world++)for(int rank:{-1,0,1,20})for(int dx:{-12,0,12})for(int dy:{-12,0,12})for(int slowBit:{0,1}){
owner={};target={};other={};created={};captured={};row={};skill={};incoming={};
owner.dwUnitType=0;owner.dwUnitId=77;owner.x=40;owner.y=70;owner.pDynamicPath=&owner.path;
target.dwUnitType=UNIT_MISSILE;target.dwClassId=991;target.owner=&other;other.x=40+dx;other.y=70+dy;
hasOwner=guard!=3;targetValid=guard!=4;hasSkill=guard!=5;hasIncoming=guard!=8;
hasRecord=world!=2;hasRoom=world!=1;allocated=world!=3;slow=slowBit;
skill.wSrvMissileA=guard==6?-1:guard==7?1000:264;
incoming.dwMissileFlags=guard==9?0:gdwBitMasks[MISSILESFLAGINDEX_RETURNFIRE];
row.nVel=row.nMaxVel=18;row.wRange=25;row.nCollideType=3;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_CANSLOW]|gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE];
creates=draws=factoryCalls=0;
int ret=SKILLS_EventFunc01_ChillingArmor(&game,1,guard==1?nullptr:&owner,guard==2?nullptr:&target,nullptr,60,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",guard,world,rank,dx,dy,slowBit,ret,factoryCalls,creates,captured.dwFlags,captured.nTargetX,captured.nTargetY,created.x,created.y,created.path.x,created.path.y,created.path.velocity,created.total,created.skill,created.rank);first=false;
}printf("]\n");}
'''
    p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe'
    subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
    cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==2880
    for r in cases:
        guard,world,rank,dx,dy,slow=r[:6];accepted=guard==0;made=accepted and world==0
        target=(41,71) if dx==dy==0 else (40+dx,70+dy)
        # The service-created object has default40/70 coordinates even when no
        # allocation happens; successful fields alone establish factory output.
        expected=[int(accepted),int(accepted),int(accepted and world not in (1,2)),32 if accepted else 0,
                  40+dx if accepted else 0,70+dy if accepted else 0,
                  40,70,*(target if made else (0,0)),(1728 if slow else 3456) if made else 0,
                  25 if made else 128,60 if made else 22,(1 if rank<0 else rank) if made else 1]
        assert r[6:]==expected,(r,expected)
    san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True)
    log=STAGE/'sanitizer.log'
    with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
    assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    table=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';manifest=json.loads((table.parent.parent/'sources.json').read_text());assert sha(table)==manifest['files']['json/missiles.json']['sha256']
    row=next(r for r in json.loads(table.read_text()).values() if r.get('Missile')=='chillingarmorbolt')
    fields={'*ID':264,'pSrvDoFunc':1,'Vel':18,'MaxVel':18,'Range':25,'InitSteps':0,'Size':1,'CollideType':3,'CollideKill':1,'LastCollide':1,'CanSlow':1,'ReturnFire':0,'GetHit':1,'ResultFlags':4}
    assert {k:row.get(k) for k in fields}==fields
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=sources,
                routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in blocks.items()},
                currentTable={str(table.relative_to(ROOT)):sha(table)},currentRow=fields,cases=len(cases),sanitizers='ASan/UBSan clean',
                reusedEvidence=str(parent.relative_to(ROOT)),reusedEvidenceSha256=sha(parent),reusedCppSha256=sha(retained),
                established=['Complete event01 resolves the incoming missile owner,requires hostility and ReturnFire metadata,normalizes negative rank,and invokes the complete factory with flags32 and absolute owner target coordinates.',
                             'Current row264 gives velocity3456,or1728at the declared50percent Slow Missiles service,total lifetime25,skill60 and retained rank. Same-point target is nudged(+1,+1)by the actual factory. Factory/room/allocation refusal does not change the outer callback success.'],
                declaredServices=['Pinned older engine,not the modern executable. The existing hash-verified factory service scaffold is reused without rerunning or recreating its historical harness. World/rooms/allocation/stats/path/owner and50percent Slow Missiles are explicit services.',
                                  'Damage creation/writing remains a seam;current formula/typed impact,global actor seed,source Size/cell/movement and modern event/owner/PvP/Sunder/client acceptance require separate evidence.'],
                artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,retained,p,binary,san,log,fixture)})
    out=ROOT/'tools/reference/chilling-armor-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Chilling Armor:2880complete older event01/factory compositions;ASan/UBSan clean;formula/impact/world/modern services separate.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');probe(p.parse_args().check)
