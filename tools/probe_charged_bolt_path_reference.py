#!/usr/bin/env python3
"""Prepare complete charged-bolt path/heading/seed reference for sentry reuse.

Source-only proof; no runtime handler or projectile is enabled by this probe.
"""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};routines={}
def source(rel):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
 hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
def extract(s,name):
 m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;i=s.index('{',m.start())+1;depth=1
 while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
 body=s[m.start():i];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body
misc=source('source/D2Common/src/Path/PathMisc.cpp');seed=source('source/D2Common/include/D2Seed.h')
cpp=r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define __stdcall
#define ARRAY_SIZE(x) (sizeof(x)/sizeof((x)[0]))
using DWORD=uint32_t;
struct D2CoordStrc{int nX,nY;};struct D2PathPointStrc{uint16_t X,Y;};
struct D2DynamicPathStrc{D2PathPointStrc tGameCoords,tTargetCoord;uint8_t nDistMax;D2PathPointStrc PathPoints[78];int dwPathPoints;};
struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};};
'''
# Only the coordinate field wrapper differs from the declared source ABI.
cpp=cpp.replace('D2PathPointStrc tGameCoords,tTargetCoord;','struct{uint16_t wPosX,wPosY;}tGameCoords;D2PathPointStrc tTargetCoord;')
cpp+=misc[misc.index('struct D2UnkPathStrc'):misc.index('//D2Common.0x6FDAA720')]
cpp+=extract(seed,'SEED_RollRandomNumber').replace('0x6AC690C5i64','0x6AC690C5ULL')+'\n'
for name in ['sub_6FDAB610','sub_6FDAB6A0','PATH_GetDirections_6FDAB790','getChargedBoltDirOffset','PATH_ComputePathChargedBolt_6FDAB4A0']:cpp+=extract(misc,name)+'\n'
cpp+=r'''
int main(){printf("[");bool first=true;
for(int origin:{100,65530})for(int dx:{-10,-5,-2,-1,0,1,2,5,10})for(int dy:{-10,-1,0,1,10})for(uint32_t low:{0u,19u,0x80000000u,0xFFFFFFFFu})for(int distance:{0,1,2,3,76,77}){
 D2DynamicPathStrc path{};path.tGameCoords={uint16_t(origin),uint16_t(origin)};path.tTargetCoord={uint16_t(origin+dx),uint16_t(origin+dy)};path.nDistMax=distance;
 D2SeedStrc seed;seed.nLowSeed=low;seed.nHighSeed=666;int heading[3];PATH_GetDirections_6FDAB790(heading,{uint16_t(origin),uint16_t(origin)},path.tTargetCoord);
 int count=PATH_ComputePathChargedBolt_6FDAB4A0(&path,&seed);assert(count==(distance>>1)+1&&count==path.dwPathPoints&&count<=78);
 printf("%s[%d,%d,%d,%u,%d,%d,%llu,[",first?"":",",origin,dx,dy,low,distance,heading[0],(unsigned long long)seed.lSeed);first=false;
 for(int i=0;i<count;i++)printf("%s[%u,%u]",i?",":"",path.PathPoints[i].X,path.PathPoints[i].Y);printf("]]");
}printf("]\n");}
'''
path=STAGE/'charged-bolt-path-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('')
subprocess.run(['g++','-std=c++17','-O2',str(path),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==2160
san=STAGE/'charged-bolt-path-source-probe-san';subprocess.run(['g++','-std=c++17','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(path),'-o',str(san)],check=True)
with (STAGE/'charged-bolt-path-sanitizer.log').open('wb') as log:clean=json.loads(subprocess.check_output([str(san)],stderr=log))
assert clean==data and not (STAGE/'charged-bolt-path-sanitizer.log').read_bytes()
fixture=STAGE/'charged-bolt-path-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e={'referenceVersion':'3.3.93847','legacyCommit':tree['sha'],'sourceHashes':hashes,'routineHashes':routines,'cases':len(data),'fixture':{'path':str(fixture.relative_to(ROOT)),'sha256':hashlib.sha256(fixture.read_bytes()).hexdigest()},'sanitizers':'ASan/UBSan clean','nativeEvidence':'tools/reference/sentry-target-native-evidence.json','portableSubstitution':'MSVC hexadecimal i64 suffix changed to ULL without changing the uint64 multiply; ABI structures declared explicitly.','established':['Complete source direction-index/table lookup, charged-bolt random-offset lookup, RNG advance and path generation execute together.','Distances0/1/2/3/76/77 retain floor(distance/2)+1 points; every step uses the fixed initial main direction plus an independent seeded offset and two source cells. Final seed and uint16 coordinate wrap are recorded.','The32-entry offset table deliberately omits its final zero; do not normalize it to an unbiased three-entry roll.'],'limits':['This script supplies source-only preparation; separate native helper comparisons are recorded in sentry-target-native-evidence.json. No handler, live path/motion/collision/damage or whole current executable acceptance follows from this probe alone.','Room/collision, missile lifetime/activation/velocity, native coordinate projection, path rebuilding and source ownership/property consumers remain outside this kernel.']}
p=ROOT/'tools/reference/charged-bolt-path-source-evidence.json'
if args.check:assert json.loads(p.read_text())==e
else:p.write_text(json.dumps(e,indent=2)+'\n')
print('2160 complete charged-bolt heading/seed/path cases; sanitizers clean; native reuse acceptance pending')
