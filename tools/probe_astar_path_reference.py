#!/usr/bin/env python3
"""Complete pinned AStar kernel and straight-path fallback, with ordered queries."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/astar-path-source'
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True)
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());hashes={}
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    def source(rel):
        raw=(ROOT/'.local/class-skills/d2moo-pinned-source'/rel).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
        hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
    def routine(text,name):
        m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
        i=text.index('{',m.start())+1;depth=1
        while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
        return text[m.start():i]+'\n'
    astar=source('source/D2Common/src/Path/AStar.cpp')
    header=source('source/D2Common/include/Path/AStar.h')
    misc=source('source/D2Common/src/Path/PathMisc.cpp')
    straight=routine(misc,'PATH_Straight_Compute')
    parent=ROOT/'.local/class-skills/toward-path-source-probe.cpp'
    assert parent.exists(),'Run probe_toward_path_reference.py --check to reconstruct the checked parent first'
    cpp=parent.read_text().split('int main()')[0]
    stripped=lambda t:re.sub(r'^#(?:include|pragma)[^\n]*\n','',t,flags=re.M)
    marker='using PathFunctionType=int(*)(D2PathInfoStrc*);'
    cpp=cpp.replace(marker,stripped(header)+'\n'+stripped(astar)+'\n'+straight+'\n'+marker)
    old='scpfnPathFunction[18]={nullptr,nullptr,PATH_Toward_6FDAA9F0,nullptr,nullptr,nullptr,nullptr,nullptr,'
    assert old in cpp
    cpp=cpp.replace(old,'scpfnPathFunction[18]={nullptr,nullptr,PATH_Toward_6FDAA9F0,nullptr,nullptr,nullptr,nullptr,PATH_Straight_Compute,')
    cpp+=r'''
int main(){printf("[");bool first=true;
for(int kind:{1,7})for(int wall=0;wall<8;wall++)for(int dx:{-16,-8,-3,0,3,8,16})for(int dy:{-8,-3,0,3,8})for(int fraction:{0,32768})for(int budget:{1,5,20}){
 D2UnitStrc u;D2DynamicPathStrc p;u.pDynamicPath=&p;p.pUnit=&u;p.dwFlags=0;p.dwPathType=7;p.nDistMax=budget;p.dwVelocity=p.dwMaxVelocity=2304;
 p.dwPathPoints=1;p.dwCurrentPointIdx=0;p.PathPoints[0]={99,99};p.tFinalTargetCoord={99,99};p.tPrevTargetCoord={98,98};
 p.tGameCoords.dwPrecisionX=(100<<16)+fraction;p.tGameCoords.dwPrecisionY=(100<<16)+fraction;p.tTargetCoord={uint16_t(100+dx),uint16_t(100+dy)};
 std::fill(std::begin(cells),std::end(cells),0);
 if(wall==1)for(int y=80;y<128;y++)cells[(y-80)*48+23]=1;
 if(wall==2)for(int x=80;x<128;x++)cells[22*48+x-80]=0x100;
 if(wall==3)for(int y=98;y<=102;y++)cells[(y-80)*48+23]=0x1000;
 if(wall==4)for(int x=97;x<=103;x++)for(int y=97;y<=103;y++)if(x==97||x==103||y==97||y==103)cells[(y-80)*48+x-80]=1;
 if(wall==5)for(int y=80;y<128;y++)cells[(y-80)*48+18]=1;
 if(wall==6)for(int x=80;x<128;x++)cells[23*48+x-80]=1;
 if(wall==7){uint32_t seed=19;for(int x=80;x<128;x++)for(int y=80;y<128;y++){seed=1664525u*seed+1013904223u;if(seed%100<22)cells[(y-80)*48+x-80]=1;}cells[20*48+20]=0;}
 queries.clear();int count;
 if(kind==1){D2PathInfoStrc info{};info.pDynamicPath=&p;info.pStartRoom=&room;info.tStartCoord={100,100};info.tTargetCoord=p.tTargetCoord;info.nCollisionPattern=0;info.nCollisionMask=0x3c01;count=PATH_AStar_ComputePath(&info);}
 else{count=D2Common_10142(&p,&u,0);assert(count==p.dwPathPoints);}
 assert(count>=0&&count<78);
 if(!first)printf(",");first=false;printf("{\"input\":[%d,%d,%d,%d,%d,%d],\"state\":[%d,%u,%u,%u,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],\"points\":",kind,wall,dx,dy,fraction,budget,count,p.tGameCoords.dwPrecisionX,p.tGameCoords.dwPrecisionY,p.dwFlags,p.dwCurrentPointIdx,p.dwVelocity,p.tDirectionVector.nX,p.tDirectionVector.nY,p.tVelocityVector.nX,p.tVelocityVector.nY,p.nCollidedWithMask,p.tTargetCoord.X,p.tTargetCoord.Y,p.tPrevTargetCoord.X,p.tPrevTargetCoord.Y,p.tFinalTargetCoord.X,p.tFinalTargetCoord.Y);
 std::vector<std::array<int,2>> out;for(int i=0;i<count;i++)out.push_back({p.PathPoints[i].X,p.PathPoints[i].Y});points(out);printf(",\"queries\":");points(queries);printf("}");
}printf("]\n");}
'''
    path=STAGE/'source.cpp';path.write_text(cpp)
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(STAGE/'reference')],check=True)
    result=subprocess.run([str(STAGE/'reference')],capture_output=True)
    (STAGE/'sanitizer.log').write_bytes(result.stderr)
    assert result.returncode==0 and not result.stderr,result.stderr.decode()[-4000:]
    data=json.loads(result.stdout);assert len(data)==3360
    fixture=STAGE/'cases.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,sourceHashes=hashes,
        completeAStarTranslationUnitSha256=hashlib.sha256(astar.encode()).hexdigest(),straightRoutineSha256=hashlib.sha256(straight.encode()).hexdigest(),
        parentTranslationUnitSha256=hashlib.sha256(parent.read_bytes()).hexdigest(),cases=len(data),kernelCases=1680,straightCases=1680,
        collisionQueries=sum(len(x['queries']) for x in data),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),
        artifacts={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in [Path(__file__).resolve(),path,STAGE/'reference',STAGE/'sanitizer.log']},
        limits=['Complete older AStar translation unit and straight routine; no current executable certificate. The general initializer is marked BROKEN upstream.',
            'Point target, synthetic room/grid, pattern0/mask0x3c01 and no target adjustment/footprint are explicit. Do not enable general path1/unit-target flags from these cases.',
            'Direction/motion side effects and ordered collision queries are compared by native tests separately. No room recache, arbitrary unit footprint, AI/global seed or graphical-client certificate.'],
        sanitizers='ASan/UBSan, exit0 and empty runtime stderr')
    out=ROOT/'tools/reference/astar-path-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print(f'AStar/straight:{len(data)}complete source contexts;{evidence["collisionQueries"]}ordered collision queries;ASan/UBSan clean.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
