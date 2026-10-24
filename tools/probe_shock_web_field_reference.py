#!/usr/bin/env python3
"""Execute full stationary field update/router/finder gates; keep historical NHD scope explicit."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
import probe_fire_blast_landing_reference as basis
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 basis.build(check=True);old=json.loads((ROOT/'tools/reference/fire-blast-landing-engine-evidence.json').read_text());cpp=(STAGE/'fire-blast-landing-source-probe.cpp').read_text().split('void reset(int kind)',1)[0]
 cpp=cpp.replace('int defense=0;bool unt=false;','int defense=0;bool unt=false,justHit=false,hostile=true;unsigned dwFlags=3;')
 cpp=cpp.replace('using Finder=int(*)();int findCallback(){return 0;}', 'using Finder=int(*)(D2UnitStrc*,void*);int findCallback(D2UnitStrc*,void*){return 0;}')
 cpp=cpp.replace('bool hasRecord=true,hasSkill=true,hasOwner=true,hasRoom=true,factory=true;', 'bool hasRecord=true,hasSkill=true,hasOwner=true,hasRoom=true,factory=true,allocate=true;int allocations=0,delay=-1,worldMask=1;')
 cpp=cpp.replace('return state==STATE_UNINTERRUPTABLE && p->unt;', 'return state==STATE_JUSTHIT?p->justHit:state==STATE_UNINTERRUPTABLE && p->unt;')
 cpp=cpp.replace('bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return true;}', 'bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc* p){return p->hostile;}')
 cpp=cpp.replace('D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int,int,int){return nullptr;}', 'D2StatListStrc allocated;D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int until,int,int){allocations++;delay=until;return allocate?&allocated:nullptr;}')
 cpp=cpp.replace('void STATES_ToggleState(D2UnitStrc*,int,int){}', 'void STATES_ToggleState(D2UnitStrc* p,int s,int v){assert(s==STATE_JUSTHIT);p->justHit=v;}')
 cpp=cpp.replace('return 4;}D2UnitStrc* D2Common_10407', 'return worldMask;}D2UnitStrc* D2Common_10407')
 cpp=cpp.replace('D2UnitStrc* D2Common_10407(D2ActiveRoomStrc*,int,int,Finder,D2MissileUnitFindArgStrc*,int){finds++;return &target;}', 'D2UnitStrc* D2Common_10407(D2ActiveRoomStrc*,int,int,Finder callback,D2MissileUnitFindArgStrc* a,int){finds++;for(auto* v:victims){if(callback(v,a))return v;}return nullptr;}')
 # Compile the actual mode3 finder before the unchanged update/router bodies.
 rel='source/D2Game/src/MISSILES/MissMode.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha256(raw).hexdigest()==old['sourceHashes'][rel];text=raw.decode();name='MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster';m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);i=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
 body=text[m.start():i];at=cpp.index('int32_t __fastcall MISSMODE_HandleMissileCollision(');cpp=cpp[:at]+'constexpr unsigned UNITFLAG_ISVALIDTARGET=1,UNITFLAG_CANBEATTACKED=2;\n'+body+'\n'+cpp[at:]
 cpp+=r'''
void reset(){owner={};target={};other={};parent={};owner.dwUnitType=0;target.dwUnitType=other.dwUnitType=1;other.dwUnitId=20;parent.dwClassId=389;parent.dwMissileMode=3;parent.path.velocity=0;parent.path.flags=64;parent.pDynamicPath=&parent.path;parent.pMissileData=&parent.md;parent.frame=90;parent.activation=90;row={};row.dwMissileFlags=0;row.nCollideKill=0;row.nAlwaysExplode=0;row.wSrvHitFunc=0;row.nNextHit=1;row.nNextDelay=25;allocations=0;delay=-1;finds=fills=flags=areas=creates=frameWrites=moves=0;hasOwner=hasRoom=allocate=true;hasRecord=true;worldMask=1;victims={&target,&other};stru_6FD2E5F8[3].pfUnitFindCallback=MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster;}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int frame:{0,1,2,90})for(int path:{0,1,2,4,64})for(int room:{0,1})for(int mask:{0,1,4})for(int firstHit:{0,1})for(int secondHit:{0,1})for(int alloc:{0,1}){
 reset();parent.frame=frame;parent.path.flags=path;hasRoom=room;worldMask=mask;target.justHit=firstHit;other.justHit=secondHit;allocate=alloc;int result=MISSMODE_SrvDo01_BasicMissile(&game,&parent);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",frame,path,room,mask,firstHit,secondHit,alloc,result,parent.frame,finds,fills,flags,allocations,delay,int(target.justHit),int(other.justHit));first=false;
}printf("]\n");}
'''
 p=STAGE/'shock-web-field-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==960
 for frame,path,room,mask,hit1,hit2,alloc,result,after,finds,fills,flags,allocations,delay,now1,now2 in data:
  search=frame>1 and room and not path&5 and path!=0
  victim=search and mask and not(hit1 and hit2)
  ending=frame<=1 or search and mask&4 and not victim
  expected_result=2 if ending or frame>1 and (not room or path&5) else 1
  assert(result,after,finds,fills,flags,allocations,delay,now1,now2)==(expected_result,frame-1,int(bool(search and mask)),int(bool(victim)),int(bool(victim)),int(bool(victim)),1025 if victim else -1, int(bool(hit1 or victim and not hit1 and alloc)),int(bool(hit2 or victim and hit1 and alloc))), (frame,path,room,mask,hit1,hit2,alloc,result,after,finds,fills,flags,allocations,delay,now1,now2)
 out=STAGE/'shock-web-field-source-probe.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',talentId='assassin.shockweb',handlerEnabled=False,legacyCommit=old['legacyCommit'],sourceHashes=old['sourceHashes'],compiledUnchangedRoutines={k:v for k,v in old['compiledUnchangedRoutines'].items() if k in ['MISSMODE_SrvDmgHitHandler','MISSMODE_HandleMissileCollision','MISSMODE_SrvDo01_BasicMissile','MISSILE_SetOwner','MISSILE_CheckUnitIfOwner']}|{name:hashlib.sha256(body.encode()).hexdigest()},fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=len(data)),established=['Complete mode3 finder filters JUSTHIT before choosing the first eligible collider. Complete generic update expires on frame90 without another hit; ordinary frames remain and can hit other eligible recipients on later frames. Field row has no LastCollide/CollideKill/ToHit/hit function, so a successful contact does not delete it or permanently skip the previous recipient.','Complete router starts25-frame NHD before damage flags, even when later mitigation/defenses suppress health loss. Failed declared state allocation does not prevent the current damage callback. Native dictionary allocation failure/old global state are not modern behavior claims.','Terrain barrier removal happens only when the declared finder found no eligible collider. A valid actor has priority over mask4. Frame/room/path guard ordering and first/second target NHD filtering are checked independently.'],services=['World collision mask and ordered candidate enumeration are declared services; actual mode3 filter executes.735 separate source finder geometry cases establish Size2 footprints. Damage-reader and damage-flag/commit service sentinels isolate collision/router order; existing complete missile defense/soft-hit wrapper evidence is separate.','Synthetic source JUSTHIT is intentionally historical/global. Current D2R2.7 origin/cast scoping must replace that state for native integration; this probe does not certify current overlapping-cast scope or full seed/stat/event/world behavior.'])
 p=ROOT/'tools/reference/shock-web-field-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Shock Web field:960 complete update/router/filter cases pass; stationary expiry, recipient/NHD ordering and persistent contact established within declared scope.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
