#!/usr/bin/env python3
"""Compile unchanged pinned legacy path/velocity routines with explicit reconstructed Fog float trig.

Requires the immutable cached PathMisc.cpp; does not access the installed world.
The two Fog LUT imports are stubs, so this is not a modern binary comparison.
"""
from pathlib import Path
import subprocess,json
root=Path(__file__).resolve().parents[1];stage=root/'.local/class-skills'
stage.mkdir(parents=True,exist_ok=True)
def function(text,name):
 i=text.index(name+'(');start=text.rfind('\n',0,i)+1;b=text.index('{',i);n=1;e=b+1
 while n:
  n+=(text[e]=='{')-(text[e]=='}');e+=1
 return text[start:e]
pathfile=root/'.local/research/d2moo-PathMisc.cpp'
import hashlib
if hashlib.sha256(pathfile.read_bytes()).hexdigest() != '70ca530b88cdf65b9591b9fd559b82996469034e55501da768876b37d6aa7079':
 raise SystemExit('Pinned legacy PathMisc.cpp cache missing or changed')
path=pathfile.read_text()
source='''#include <algorithm>\n#include <cmath>\n#include <cstdint>\n#include <cstdio>\n#define __fastcall\n#define __int64 long long\nstruct D2CoordStrc { int nX=0,nY=0; };\nstruct D2PathPointStrc {uint16_t X=0,Y=0;bool operator==(const D2PathPointStrc&)const=default;};\nstruct D2DynamicPathStrc {static const int MAXPATHLEN=78;struct{uint32_t dwPrecisionX,dwPrecisionY;}tGameCoords;D2PathPointStrc PathPoints[78];int dwPathPoints=0,dwAcceleration=250,dwAccelerationsCounter=0,dwMaxVelocity=7680,dwVelocity=3456;D2CoordStrc tVelocityVector,tDirectionVector;};\nuint16_t PATH_FromFP16(uint32_t x){return x>>16;}\nfloat FOG_10083_Cos_LUT(int angle){return (float)std::cos((angle&511)*std::acos(-1.0)/256);}\nfloat FOG_10084_Sin_LUT(int angle){return (float)std::sin((angle&511)*std::acos(-1.0)/256);}\n'''
source+=function(path,'PATH_ComputePathBlessedHammer_6FDAB3C0')+'\n'+function(path,'sub_6FD5CEB0')
source+='''\nint main(){D2DynamicPathStrc p;p.tGameCoords={uint32_t((20000<<16)+32768),uint32_t((20000<<16)+32768)};PATH_ComputePathBlessedHammer_6FDAB3C0(&p);std::printf("{\\\"points\\\":[");for(int i=0;i<p.dwPathPoints;i++){if(i)std::printf(",");std::printf("[%d,%d]",int(p.PathPoints[i].X)-20000,int(p.PathPoints[i].Y)-20000);}std::printf("],\\\"velocities\\\":[");p.tDirectionVector={4096,0};for(int i=0;i<120;i++){sub_6FD5CEB0(&p,1024);if(i)std::printf(",");std::printf("%d",p.dwVelocity);}std::printf("]}\\n");}\n'''
# Correct ordinary C++ quote escapes in generated printf literals.
source=source.replace('\\\\\\"','\\"')
p=stage/'hammer-source-probe.cpp';p.write_text(source)
subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(stage/'hammer-source-probe')],check=True)
raw=subprocess.check_output([str(stage/'hammer-source-probe')]);(stage/'hammer-source-probe.json').write_bytes(raw)
x=json.loads(raw);print(len(x['points']),'waypoints;',x['points'][:8],'; velocity frames1/5/85:',x['velocities'][0],x['velocities'][4],x['velocities'][84])
