#!/usr/bin/env python3
"""Execute the complete reconstructed point-target path initializer with PathWF.

The reconstruction labels 10142 BROKEN. This is bounded supplementary evidence,
not a claim of equivalence to the current D2R executable. Other path types,
target-unit paths and successful repair are deliberately outside this probe.
"""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    # Reuse the exact, independently verified motion ABI and complete routines.
    # Its builder checks pinned source blobs and leaves a reproducible C++ unit.
    import probe_actor_motion_reference
    probe_actor_motion_reference.build(True)
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    hashes, blocks = {}, {}

    def source(rel):
        raw = (STAGE / 'd2moo-pinned-source' / rel).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == rel)
        hashes[rel] = hashlib.sha256(raw).hexdigest()
        return raw.decode()

    def routine(text, name):
        m = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert m, name
        i, depth = text.index('{', m.start()) + 1, 1
        while depth:
            depth += (text[i] == '{') - (text[i] == '}')
            i += 1
        body = text[m.start():i]
        blocks[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    paths = source('source/D2Common/src/Path/Path.cpp')
    wf = source('source/D2Common/src/Path/PathWF.cpp')
    collision = source('source/D2Common/src/D2Collision.cpp')
    header = source('source/D2Common/include/Path/Path.h')
    base = (STAGE / 'actor-motion-source-probe.cpp').read_text()
    base = base[:base.index('int main()')]
    # ABI declarations only: source function bodies below remain unchanged.
    base = base.replace('#include <array>', '#include <array>\n#include <climits>\n#include <cstring>\n#define __vectorcall\n#define D2_VERSION_MAJOR 1\n#define D2_VERSION_MINOR 13')
    base = base.replace('MONMODE_RUN=15,', 'UNIT_OBJECT=2,UNIT_ITEM=4,MONMODE_RUN=15,')
    base = base.replace('struct D2ActiveRoomStrc{} room;', 'struct D2DrlgCoordsStrc{int nSubtileX=80,nSubtileY=80,nSubtileWidth=48,nSubtileHeight=48;};struct D2ActiveRoomStrc{D2DrlgCoordsStrc tCoords;} room;')
    base = base.replace('bool operator==(const D2PathPointStrc&)const=default;', 'bool operator==(const D2PathPointStrc&)const=default;int SquaredDistance(const D2PathPointStrc& o)const{int x=o.X-X,y=o.Y-Y;return x*x+y*y;}')
    base = base.replace('static constexpr int PATH_MAX_STEP_LEN=10;', 'static constexpr int MAXPATHLEN=78,PATH_MAX_STEP_LEN=10;')
    base = base.replace('tFinalTargetCoord,tPrevTargetCoord;', 'tFinalTargetCoord,tPrevTargetCoord,tTargetCoord;')
    base = base.replace('nStepNum=0;', 'nStepNum=0,nDistMax=20,nIDAStarInitFScore=0,dwSpeed=0;')
    # Record room admission separately from masked cell queries.
    base = base.replace('queries.push_back({x,y});return x>=80', 'return x>=80')
    base = base.replace('int repaths=0;', 'int repaths=0;')
    base += re.search(r'struct D2PathInfoStrc\s*\{.*?^\};', header, re.M | re.S)[0] + '\n'
    base += r'''
int roomQueries=0;
int COLLISION_CheckMask(D2ActiveRoomStrc* r,int x,int y,uint16_t mask){queries.push_back({x,y});return COLLISION_CheckCollisionMask(r,x,y,mask);}
int COLLISION_CheckAnyCollisionWithAdjacentCells(D2ActiveRoomStrc*,int,int,uint16_t){assert(false);return 0;}
int COLLISION_CheckAnyCollisionForBoundingBoxRecursively(D2ActiveRoomStrc*,D2BoundingBoxStrc*,uint16_t){assert(false);return 0;}
int UNITS_GetXPosition(D2UnitStrc* u){return u->pDynamicPath->tGameCoords.wPosX;}
int UNITS_GetYPosition(D2UnitStrc* u){return u->pDynamicPath->tGameCoords.wPosY;}
int UNITS_GetUnitSizeX(D2UnitStrc*){assert(false);return 0;}
int PATH_MissileToTarget(D2DynamicPathStrc*,D2UnitStrc*){assert(false);return 0;}
void PATHUtil_AdvanceTowardsTarget_6FDAB890(D2DynamicPathStrc*){assert(false);}
int UNITS_IsDoor(D2UnitStrc*){assert(false);return 0;}
uint8_t PATH_AdvanceToDoor(D2PathInfoStrc*){assert(false);return 0;}
int MONSTERS_CanBeInTown(D2UnitStrc*){return 1;}
int DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){assert(false);return 0;}
int PATH_RemoveCollisionFootprintForUnit(D2UnitStrc* u,int){assert(u->pDynamicPath->dwCollisionPattern==0);return 1;}
void PATH_AddCollisionFootprintForUnit(D2UnitStrc* u){assert(u->pDynamicPath->dwCollisionPattern==0);}
int PATH_PreparePathTargetForPathUpdate(D2PathInfoStrc*){assert(false);return 0;}
'''
    # Entire PathWF translation unit, includes supplied by the ABI above.
    base += routine(collision, 'COLLISION_CheckAnyCollisionWithPattern')
    base += re.sub(r'^#include[^\n]*\n', '', wf, flags=re.M)
    base += 'using PathFunctionType=int(*)(D2PathInfoStrc*);\nstatic const PathFunctionType scpfnPathFunction[18]={nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,PATH_ComputePathOrSlideAlongObstacles,nullptr,nullptr};\n'
    for name in ['sub_6FDA8220', 'PATH_IsTargetDestinationAllowed', 'PATH_UpdateTargetUnit', 'D2Common_10142']:
        base += routine(paths, name)
    base += r'''
int main(){bool first=true;printf("[");
for(int wall:{0,1,2,3,4})for(int dx:{-101,-21,-20,-19,-8,-3,-2,0,2,3,8,19,20,21,101})for(int dy:{-8,-3,0,3,8})for(int fraction:{0,32768,57344})for(int budget:{8,20}){
 D2UnitStrc u;D2DynamicPathStrc p;u.pDynamicPath=&p;p.pUnit=&u;p.dwFlags=0x800;p.nDistMax=budget;p.dwVelocity=p.dwMaxVelocity=2304;p.dwPathPoints=1;p.dwCurrentPointIdx=0;p.PathPoints[0]={99,99};p.tFinalTargetCoord={99,99};p.tPrevTargetCoord={98,98};p.tGameCoords.dwPrecisionX=(100<<16)+fraction;p.tGameCoords.dwPrecisionY=(100<<16)+fraction;p.tTargetCoord={uint16_t(100+dx),uint16_t(100+dy)};
 std::fill(std::begin(cells),std::end(cells),0);
 if(wall==1)for(int y=80;y<128;y++)cells[(y-80)*48+23]=1;
 if(wall==2)for(int x=80;x<128;x++)cells[22*48+x-80]=0x100; // raw monster bit is outside the movement mask
 if(wall==3)for(int y=98;y<=102;y++)cells[(y-80)*48+23]=0x1000;
 if(wall==4)for(int x=97;x<=103;x++)for(int y=97;y<=103;y++)if(x==97||x==103||y==97||y==103)cells[(y-80)*48+x-80]=1;
 queries.clear();int count=D2Common_10142(&p,&u,0);assert(count==p.dwPathPoints);
 if(!first)printf(",");first=false;printf("{\"input\":[%d,%d,%d,%d,%d],\"state\":[%d,%u,%u,%u,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],\"points\":",wall,dx,dy,fraction,budget,count,p.tGameCoords.dwPrecisionX,p.tGameCoords.dwPrecisionY,p.dwFlags,p.dwCurrentPointIdx,p.dwVelocity,p.tDirectionVector.nX,p.tDirectionVector.nY,p.tVelocityVector.nX,p.tVelocityVector.nY,p.nCollidedWithMask,p.tTargetCoord.X,p.tTargetCoord.Y,p.tPrevTargetCoord.X,p.tPrevTargetCoord.Y,p.tFinalTargetCoord.X,p.tFinalTargetCoord.Y);
 std::vector<std::array<int,2>> out;for(int i=0;i<p.dwPathPoints;i++)out.push_back({p.PathPoints[i].X,p.PathPoints[i].Y});points(out);printf(",\"queries\":");points(queries);printf("}");
}printf("]\n");}
'''
    p = STAGE / 'actor-path-source-probe.cpp'
    p.write_text(base)
    binary = p.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(p), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'actor-path-sanitizer.log').write_bytes(result.stderr)
    assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-4000:]
    data = json.loads(result.stdout)
    assert len(data) == 2250
    for row in data:
        wall, dx, dy, fraction, budget = row['input']
        distance = max(abs(dx), abs(dy))
        if distance <= 2 or distance >= budget:
            assert row['state'][0] == 0
        if wall == 0 and 2 < distance < budget:
            assert row['state'][0] > 0
            assert row['state'][11:13] == row['points'][-1]
            assert row['state'][15:17] == [100 + dx, 100 + dy]
    fixture = STAGE / 'actor-path-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', talentId='assassin.bladesentinel', handlerEnabled=False,
                    legacyCommit=tree['sha'], sourceHashes=hashes, compiledRoutineHashes=blocks,
                    motionEvidence='tools/reference/actor-motion-engine-evidence.json',
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), contexts=len(data)),
                    sanitizers='ASan/UBSan; successful exit and zero stderr required',
                    established=['Complete unchanged D2Common_10142, target-unit updater, destination admission and room-crossing flag updater run with the complete unchanged PathWF translation unit and previously verified direction routine. Point-target paths retain final/previous/requested target distinctions and exact state/query ordering.',
                                 'No far-target clipping is present: initializer rejects offsets greater than100 per axis; PathWF rejects Chebyshev distance<=2 or >=distance budget. Native port must not replace this with an invented clamp.',
                                 'Successful point paths replace the active target with their final computed waypoint while preserving the original request as final target. Failed paths clear point count/index and moving flag.'],
                    limits=['D2MOO explicitly marks this general initializer BROKEN. Unchanged reconstructed execution is supplementary evidence; current executable acceptance remains open.',
                            'Only server monster, point target, path15, pattern0, mask0x3c01 and a town-allowed blade context are exercised. Other path dispatch entries and unreachable target-unit/target-adjustment branches assert. Room/grid services are explicit synthetic bounds; footprint removal/addition is a checked zero-footprint service.',
                            'Successful repair switches to path2/3 before falling back to15 in the source; it remains unsupported until those complete kernels are executed/ported. This probe does not certify the AI/mode/event clock, full actor lifetime/combat/input or modern room geometry.'])
    p = ROOT / 'tools/reference/actor-path-engine-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert p.read_text() == rendered
    else:
        p.write_text(rendered)
    print(f'Actor path initializer: {len(data)} exact source contexts pass; reconstruction and repair limits retained.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
