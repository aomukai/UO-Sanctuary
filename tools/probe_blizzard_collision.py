#!/usr/bin/env python3
"""Reuse the Fire Blast ABI for complete Blizzard do3/collision/finder/router/history."""
import argparse,ast,hashlib,json,re,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/blizzard-collision-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);parent=ROOT/'tools/probe_fire_blast_landing_reference.py';fn=next(n for n in ast.parse(parent.read_text()).body if isinstance(n,ast.FunctionDef) and n.name=='build');node=next(n for n in fn.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='cpp' for t in n.targets));cpp=ast.literal_eval(node.value);retained=STAGE/'reused-fire-blast-fixture.cpp';retained.write_text(cpp)
 pairs=[('int UNITS_GetUnitSizeX(D2UnitStrc*){return 1;}','int UNITS_GetUnitSizeX(D2UnitStrc*){return 2;}'),('int defense=0;bool unt=false;','int defense=0;bool unt=false,hostile=true;unsigned dwFlags=3;'),('using Finder=int(*)();int findCallback(){return 0;}','using Finder=int(*)(D2UnitStrc*,void*);int findCallback(D2UnitStrc*,void*){return 0;}'),('bool hasRecord=true,hasSkill=true,hasOwner=true,hasRoom=true,factory=true;','bool hasRecord=true,hasSkill=true,hasOwner=true,hasRoom=true,factory=true;int worldMask=0,maskWrites=0;'),('bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return true;}','bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc* p){return p->hostile;}'),
 ('int COLLISION_CheckMaskWithSize(D2ActiveRoomStrc*,int,int,int,int){return 4;}D2UnitStrc* D2Common_10407(D2ActiveRoomStrc*,int,int,Finder,D2MissileUnitFindArgStrc*,int){finds++;return &target;}',
 'int COLLISION_CheckMaskWithSize(D2ActiveRoomStrc*,int,int,int,int mask){return worldMask&mask;}D2UnitStrc* D2Common_10407(D2ActiveRoomStrc*,int,int,Finder callback,D2MissileUnitFindArgStrc* a,int){finds++;for(auto* v:victims){if(callback(v,a))return v;}return nullptr;}')]
 for a,b in pairs:assert cpp.count(a)==1,a;cpp=cpp.replace(a,b)
 cpp+='\nconstexpr unsigned UNITFLAG_ISVALIDTARGET=1,UNITFLAG_CANBEATTACKED=2;void COLLISION_SetMask(D2ActiveRoomStrc*,int,int,int mask){assert(mask==0x40);maskWrites++;}\n'
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';bodies={};sources={}
 for rel,names in {'source/D2Common/src/Units/Missile.cpp':['MISSILE_SetOwner','MISSILE_CheckUnitIfOwner'],'source/D2Game/src/MISSILES/MissMode.cpp':['MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster','MISSMODE_SrvDmgHitHandler','MISSMODE_HandleMissileCollision','MISSMODE_SrvDo03_PoisonCloud_Blizzard_ThunderStorm_HandOfGod']}.items():
  p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);sources[rel]=sha(p)
  for name in names:bodies[name]=function(raw.decode(),name)
 router=bodies['MISSMODE_SrvDmgHitHandler']
 for name in sorted(set(re.findall(r'\bMISSMODE_Srv(?:Hit|Dmg)\w+',router))-{'MISSMODE_SrvDmgHitHandler'}):
  kind='void' if 'SrvDmg' in name else 'int32_t';args='D2GameStrc*,D2UnitStrc*,D2UnitStrc*'+(',D2DamageStrc*' if kind=='void' else '');cpp+=f'{kind} {name}({args}){{assert(false);'+('' if kind=='void' else 'return 3;')+'}\n'
 for body in bodies.values():cpp+='\n'+body+'\n'
 cpp+=r'''
void reset(){owner={};parent={};target={};other={};owner.dwUnitType=0;owner.dwUnitId=77;target.dwUnitType=other.dwUnitType=1;other.dwUnitId=20;parent.dwClassId=159;parent.dwMissileMode=3;parent.path.velocity=0;parent.path.flags=64;parent.pDynamicPath=&parent.path;parent.pMissileData=&parent.md;row={};row.nAlwaysExplode=1;row.wSrvHitFunc=row.wSrvDmgFunc=0;row.wResultFlags=4;row.dwHitFlags=0;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE];hasOwner=hasRoom=hasRecord=true;worldMask=256;maskWrites=fills=flags=finds=frameWrites=ar=moves=0;victims={&target,&other};stru_6FD2E5F8[3]={MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster,0x184};}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int frame:{1,2,9})for(int path:{0,1,4,64})for(int room:{0,1})for(int mask:{0,1,4,256,260})for(int last:{-1,10,20,77})for(int valid:{0,1,2,3})for(int hostile:{0,1}){
 reset();parent.frame=frame;parent.path.flags=path;hasRoom=room;worldMask=mask;parent.md.nOwnerType=1;parent.md.dwOwnerGUID=last;target.dwFlags=valid;other.hostile=hostile;int result=MISSMODE_SrvDo03_PoisonCloud_Blizzard_ThunderStorm_HandOfGod(&game,&parent);
 printf("%s[[%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d]]",first?"":",",frame,path,room,mask,last,valid,hostile,result,parent.frame,maskWrites,finds,fills,flags,parent.md.dwOwnerGUID,ar,moves);first=false;
}printf("]\n");}
'''
 for name,body in bodies.items():assert function(cpp,name)==body,name
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==3840
 for inputs,out in cases:
  frame,path,room,mask,last,valid,hostile=inputs;search=frame>1 and room and not(path&5) and path!=0;effective=mask&0x184;find=bool(search and effective);selected=10 if find and valid==3 and last!=10 else 20 if find and hostile and last!=20 else -1
  result=2 if frame<=1 or not room or path&5 or find and selected<0 and effective&4 else 1
  assert out==[result,frame-1,1,int(find),int(selected>=0),int(selected>=0),selected if selected>=0 else last,0,0],(inputs,out,selected)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n');pin=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';manifest=json.loads((pin.parent.parent/'sources.json').read_text());assert sha(pin)==manifest['files']['json/missiles.json']['sha256'];row=json.loads(pin.read_text())['blizzard1']
 assert row['pSrvDoFunc']==3 and row['Size']==2 and row['CollideType']==3 and row['LastCollide']==1 and row['AlwaysExplode']==1 and not any(row.get(k,0) for k in ('NextHit','ToHit','CollideKill','pSrvHitFunc','pSrvDmgFunc'))
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(cases),sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},currentTable={str(pin.relative_to(ROOT)):sha(pin)},currentRow=row,sanitizers='ASan/UBSan clean',
 established=['Complete stationary do3 marks0x40 before generic lifetime/collision handling;frame1expires before another recipient lookup. Full mode3 finder rejects invalid/most-recent/nonhostile actors before choosing one eligible candidate. Zero path flags bypass lookup;guarded paths/absent rooms stop;barrier removal occurs only if lookup has no eligible actor.',
 'Complete router and LastCollide setter/checker retain the accepted last actor. Shard159has no NextHit,ToHit,CollideKill or hit/damage function:one accepted contact fills/flags once without AR and leaves the shard active. A repeated candidate exposes the next eligible actor;otherwise history survives empty frames. Multiple overlapping actors can alternate across frames.'],
 declaredServices=['Older engine,not modern executable proof. Existing Fire Blast ABI reused;stationary path/saved cell/world masks,candidate enumeration/footprints,rows/owner/hostility and damage fill/flag/commit remain declared services. Unselected router callbacks assert. No generic damage flag/defense/events/HP/cold or modern geometry/seed/lifecycle/client acceptance is claimed;reuse existing cold damage/return services separately.'],
 artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,retained,p,binary,san,log,fixture)})
 out=ROOT/'tools/reference/blizzard-collision-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Blizzard collision:3840complete stationary do3/lifetime/finder/router/LastCollide cases;ASan/UBSan clean;damage and native world services explicit.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');probe(a.parse_args().check)
