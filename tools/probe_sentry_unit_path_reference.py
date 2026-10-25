#!/usr/bin/env python3
"""Compose the Sentry alternative-target wrapper with the full unit path kernel.

Reuse hash-checked aim/toward ABI and complete routines. No native assembly or
learned skill changes. The general initializer is reconstructed and marked BROKEN.
"""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 import probe_sentry_aim_reference
 probe_sentry_aim_reference.build(True)
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};routines={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def extract(s,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;i=s.index('{',m.start())+1;depth=1
  while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
  body=s[m.start():i];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
 paths=source('source/D2Common/src/Path/Path.cpp');ai=source('source/D2Game/src/AI/AiUtil.cpp')
 cpp=(STAGE/'sentry-aim-source-probe.cpp').read_text().split('int main()',1)[0]
 # ABI fields only; every added routine below is the complete pinned source.
 cpp=cpp.replace('struct D2UnitStrc{int dwUnitType=', 'struct D2GameStrc;struct D2UnitStrc{D2GameStrc* pGame=nullptr;int dwUnitType=')
 cpp=cpp.replace('dwTargetId=0;', 'dwTargetId=0,dwPrevPathType=0,nPreviousVelocity=0;')
 assert 'nPreviousVelocity=0;' in cpp and 'pGame=nullptr' in cpp
 for name in ['gaPathTypeFlags_6FDD2088','gaOffsetForPathType']:
  m=re.search(r'static const int(?:32_t)? '+name+r'\[\]\s*=\s*\{.*?\};',paths,re.S);assert m;cpp+=m[0]+'\n'
 for name in ['PATH_SetType','PATH_GetType','D2COMMON_10170_PathSetTargetPos','PATH_GetNumberOfPathPoints']:
  cpp+=extract(paths,name)
 cpp+=r'''
struct D2AiCallback7ArgStrc{D2UnitStrc* pTarget=nullptr;int nDistance=INT_MAX;D2UnitStrc* unk0x08=nullptr;};
D2UnitStrc third;bool fallback=false;int scans=0;
D2UnitStrc* sub_6FCF1E80(D2GameStrc*,D2UnitStrc*,void* raw,D2UnitStrc*(*)(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,void*),int callback){assert(callback==7);scans++;auto* arg=static_cast<D2AiCallback7ArgStrc*>(raw);if(fallback){arg->pTarget=&third;arg->nDistance=10;}return nullptr;}
'''
 cpp+=extract(ai,'sub_6FCF27B0')
 cpp+=r'''
int main(){printf("[");bool first=true;
for(int wall=0;wall<6;wall++)for(int dx:{-19,-8,-3,0,3,8,19})for(int dy:{-8,0,8})for(int fraction:{0,32768})for(int budget:{5,14})for(int oldType:{13,15})for(int oldTarget:{0,1})for(int other:{0,1}){
 D2GameStrc game;D2UnitStrc actor,target,old;D2DynamicPathStrc p,tp,op;
 actor.pGame=&game;actor.pDynamicPath=&p;p.pUnit=&actor;target.pDynamicPath=&tp;tp.pUnit=&target;target.dwUnitId=19;old.pDynamicPath=&op;op.pUnit=&old;old.dwUnitId=29;
 p.dwPathType=oldType;p.dwFlags=0x1900;p.nDistMax=budget;p.dwSpeed=0;p.dwVelocity=p.dwMaxVelocity=0;p.dwPathPoints=0;
 p.tGameCoords.dwPrecisionX=(100<<16)+fraction;p.tGameCoords.dwPrecisionY=(100<<16)+fraction;
 tp.tGameCoords.dwPrecisionX=((100+dx)<<16)+32768;tp.tGameCoords.dwPrecisionY=((100+dy)<<16)+32768;
 op.tGameCoords.dwPrecisionX=op.tGameCoords.dwPrecisionY=(90<<16)+32768;
 PATH_SetTargetUnit(&p,oldTarget?&old:nullptr);p.tTargetCoord={88,89};registered=oldTarget?&old:nullptr;
 std::fill(std::begin(cells),std::end(cells),0);
 if(wall==1)for(int y=80;y<128;y++)cells[(y-80)*48+23]=1;
 if(wall==2)for(int x=97;x<=103;x++)for(int y=97;y<=103;y++)if(x==97||x==103||y==97||y==103)cells[(y-80)*48+x-80]=1;
 if(wall==3)for(int x=80;x<128;x++)cells[22*48+x-80]=0x100;
 if(wall==4)cells[(100+dy-80)*48+100+dx-80]=1;
 if(wall==5)cells[20*48+20]=1;
 // A point-target control uses the exact post-SetType2 flags. Compare only
 // points/queries/count, not active-target fields whose unit semantics differ.
 D2UnitStrc pointActor;D2DynamicPathStrc q=p;pointActor.pDynamicPath=&q;q.pUnit=&pointActor;q.pTargetUnit=nullptr;q.tTargetCoord={uint16_t(100+dx),uint16_t(100+dy)};PATH_SetType(&q,PATHTYPE_TOWARD);
 assert((q.dwFlags&0x7ff00)==0);queries.clear();int pointCount=D2Common_10142(&q,&pointActor,0);auto pointQueries=queries;
 fallback=other;scans=0;queries.clear();D2UnitStrc* chosen=&target;int distance=30;int result=sub_6FCF27B0(&actor,&chosen,&distance,&old,3);
 assert(p.dwPathType==oldType && p.tTargetCoord.X==88 && p.tTargetCoord.Y==89);
 // The full coordinate setter clears the raw pointer. Retained type/GUID
 // remain present, but the full registry/getter does not recreate a null pointer.
 assert(p.pTargetUnit==nullptr);bool rawNull=p.pTargetUnit==nullptr;auto* resolved=SUNIT_GetTargetUnit(&game,&actor);assert(resolved==nullptr && p.dwTargetId==(oldTarget?29:19));
 assert(p.dwPathPoints==pointCount && queries==pointQueries);
 for(int i=0;i<p.dwPathPoints;i++)assert(p.PathPoints[i]==q.PathPoints[i]);
 assert(scans==(pointCount==0));assert(result==(pointCount==0&&!other));
 assert(chosen==(pointCount==0&&other?&third:&target));assert(distance==(pointCount==0&&other?10:30));
 printf("%s{\"input\":[%d,%d,%d,%d,%d,%d,%d,%d],\"count\":%d,\"result\":%d,\"scans\":%d,\"rawTargetCleared\":%d,\"resolvedOldTarget\":%d,\"flags\":%u,\"points\":",first?"":",",wall,dx,dy,fraction,budget,oldType,oldTarget,other,pointCount,result,scans,rawNull,resolved!=nullptr,p.dwFlags);first=false;
 std::vector<std::array<int,2>> out;for(int i=0;i<p.dwPathPoints;i++)out.push_back({p.PathPoints[i].X,p.PathPoints[i].Y});points(out);printf(",\"queries\":");points(queries);printf("}");
}printf("]\n");}
'''
 p=STAGE/'sentry-unit-path-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 result=subprocess.run([str(binary)],capture_output=True);(STAGE/'sentry-unit-path-sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr.decode()[-4000:];data=json.loads(result.stdout);assert len(data)==4032
 fixture=STAGE/'sentry-unit-path-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',handlerEnabled=False,nativeVerifiedByThisSourceProbe=False,nativeEvidence='tools/reference/sentry-unit-path-native-evidence.json',legacyCommit=tree['sha'],sourceHashes=hashes,routineHashes=routines,reusedAimEvidence='tools/reference/sentry-aim-source-evidence.json',reusedTowardEvidence='tools/reference/toward-path-engine-evidence.json',selectorEvidence='tools/reference/sentry-target-source-evidence.json',fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=4032),established=['Complete alternative chooser now composes with full PATH_SetType tables/setter, target/getter/coordinate setters, general unit initializer, toward kernel and registry refresh. Path2 flags are0, so temporary0x800 target-footprint removal and0x1000 target adjustment are cleared even from stale initial flags. Do not replace PATH_SetType by merely assigning dwPathType.','For declared stationary zero-lead pattern0/mask0x3c01 monster context, the full unit-target probe matches point-control count, every waypoint and ordered collision query across4032 cases. The unit active target is not rewritten to the last waypoint; only the control uses point semantics. This bounds legitimate reuse instead of claiming all unit paths equal point paths.','Restoring coordinates with the complete source setter clears the raw target pointer after restoring old type/GUID. A subsequent full SUNIT_GetTargetUnit returns null: its registry validation does not recreate a missing pointer. Old target type/GUID survive when old target was present; with no old target, the temporary primary identity fields remain stale. The wrapper restores path type and coordinates, not the old target pointer, low moving flags, point count or all path state. Current behavior remains unverified.','Six obstacle grids, offsets, fractions, budgets5/14, old path13/15, old target present/absent and declared callback7 fallback/no-fallback branches execute under clean ASan/UBSan. Callback7 only runs if no path points; fallback distance10 keeps primary replacement, no fallback permits the close alternative.'],limits=['Supplementary reconstructed source; D2Common_10142 is marked BROKEN, and current3.3 executable/path/room equivalence is not asserted. Native integration and current build acceptance are verified separately.','Town/room/grid and zero-footprint services retain the existing synthetic contracts. Target size service returns1, but temporary type2 disables target-footprint removal and target adjustment. Actor lead/speed0, pattern0/mask0x3c01 and monster targets are declared; arbitrary moving players, doors, offsets, masks, room crossing, target death/registry reuse and other path types are not covered.','Callback7 scanning here is a declared checked fallback service (none or third target at10); its full eligibility/nearest selection remains separately covered by4096 selector cases. Native collision adapters and saved actor state consequences require their own tests before binding.','The reused translation unit retains unrelated unexecuted reconstructed motion helpers whose integer-to-pointer casts produce64-bit compiler warnings. The executed probe requires clean ASan/UBSan runtime stderr; this is not a certificate for those unrelated ABI paths.'],restorationDiagnostic='.local/class-skills/sentry-unit-path-restoration-diagnostic.log',sanitizers='ASan/UBSan; exit0 and empty runtime stderr')
 target=ROOT/'tools/reference/sentry-unit-path-source-evidence.json';render=json.dumps(evidence,indent=2)+'\n'
 if check:assert target.read_text()==render
 else:target.write_text(render)
 print('4032 full alternative/unit-path/setter/registry compositions pass; bounded point-kernel reuse proved; native binding is verified separately.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
