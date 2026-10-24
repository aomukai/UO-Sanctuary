#!/usr/bin/env python3
"""Execute complete source missile/actor collider selection and pin throw metadata."""
from pathlib import Path
import argparse,hashlib,json,re,struct,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args();tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
rel='source/D2Common/src/Units/Units.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);text=raw.decode();name='D2Common_10407_Impl';m=re.search(r'^D2UnitStrc\* __stdcall '+name+r'\([^\n]+\)\s*\n\{',text,re.M);assert m;e=text.index('{',m.start())+1;depth=1
while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
body=text[m.start():e]
cpp=r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __stdcall
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_MISSILE=3,PLRMODE_DEATH=0,PLRMODE_DEAD=17,MONMODE_DEATH=12,MONMODE_DEAD=0;
struct D2UnitStrc{int dwUnitType=1,dwAnimMode=1,size=2,x=100,y=100;D2UnitStrc* pRoomNext=nullptr;};struct D2ActiveRoomStrc{D2UnitStrc* pUnitFirst=nullptr;} room;D2ActiveRoomStrc* rooms[1]={&room};
struct D2DrlgCoordsStrc{int nSubtileX=0,nSubtileY=0,nSubtileWidth=1000,nSubtileHeight=1000;};
void DUNGEON_GetAdjacentRoomsListFromRoom(D2ActiveRoomStrc*,D2ActiveRoomStrc*** out,int* count){*out=rooms;*count=1;}void DUNGEON_GetRoomCoordinates(D2ActiveRoomStrc*,D2DrlgCoordsStrc* out){*out={};}
int UNITS_GetUnitSizeX(D2UnitStrc* p){return p->size;}int UNITS_GetXPosition(D2UnitStrc* p){return p->x;}int UNITS_GetYPosition(D2UnitStrc* p){return p->y;}int accept(D2UnitStrc*,void*){return 1;}
'''+body+r'''
int main(){D2UnitStrc target;room.pUnitFirst=&target;bool first=true;std::printf("[");
for(int missile:{1,2,3})for(int size:{0,1,2,3,5})for(int dx=-3;dx<=3;dx++)for(int dy=-3;dy<=3;dy++){target.size=size;auto* hit=D2Common_10407_Impl(&room,100+dx,100+dy,accept,nullptr,missile,nullptr);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d]",missile,size,dx,dy,int(hit!=nullptr));}std::printf("]\n");}
'''
path=STAGE/'poison-contact-source-probe.cpp';path.write_text(cpp);binary=STAGE/'poison-contact-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==735
fixture=STAGE/'poison-contact-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
anim=(ROOT/'.local/research/AnimData.d2').read_bytes();digest=hashlib.sha256(anim).hexdigest();assert digest=='e3a54b9ab71df7ccdf3655bbba5868c9d53cd8716b0aad25e8c3d5621579cecb';pos=0;animations=[]
for bucket in range(256):
 n=struct.unpack_from('<i',anim,pos)[0];pos+=4
 for _ in range(n):
  r=anim[pos:pos+160];pos+=160;code=r[:8].rstrip(b'\0').decode()
  if code in ['AMTHHTH','AMTH1HS','AMTH1HT']:
   frames,speed=struct.unpack_from('<ii',r,8);events=[(i,x) for i,x in enumerate(r[16:]) if x];assert(frames,speed,events)==(16,256,[(9,2)]);animations.append(dict(code=code,frames=frames,speed=speed,event=9,eventKind=2))
assert len(animations)==3
e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(raw).hexdigest()},compiledRoutines=[name],blockHashes={name:hashlib.sha256(body.encode()).hexdigest()},probe=dict(cases=735,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),cachedAnimation=dict(path='.local/research/AnimData.d2',sha256=digest,records=animations,provenanceComplete=False),established=['Full finder combines missile size1..3 with target footprint, clamps target sizes above3 and rejects size0. Current size2 poison cloud gives diamond radius1 for size1 targets, diamond radius2 for size2, and the cross of2x1 arms for size3. Native sectors retain their own enumeration order as the room-list adapter.','The finder returns the first callback-eligible collider. The LastCollide identity check happens later in the generic hit router, not in this finder; an unchanged first collider must not be skipped in order to poison another actor.','Cached Amazon TH records all have16 frames/event9 and event-kind2. Existing full UNITS_GetFrameBonus review returns0 for TH; the native ordinary-repeat minus-one adapter and exact modern animation remain separate from this source metadata.'],limits=['One valid room, alive monster, declared size/coordinates and accepting callback isolate complete finder geometry. Full hostility/collision masks, linked-room/native sector ordering and modern grid/client equivalence remain acceptance work.','Cached animation acquisition provenance is incomplete. The16/event9 TH clock is a declared supplementary source adapter; current3.3 executable animation/IAS/chill/WSM/skill-rate comparison remains open.'])
out=ROOT/'tools/reference/poison-contact-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Poison contact evidence stale'
else:out.write_text(encoded)
print('Poison contact:735 complete finder geometry cases; cached16/event9 TH metadata pinned with explicit provenance limits.')
