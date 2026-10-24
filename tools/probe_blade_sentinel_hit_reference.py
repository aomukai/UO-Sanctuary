#!/usr/bin/env python3
"""Execute complete blade follow, collision finder, hit router and hit37 callback."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
import probe_shock_web_field_reference as basis
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 basis.build(check=True);old=json.loads((ROOT/'tools/reference/shock-web-field-engine-evidence.json').read_text());cpp=(STAGE/'shock-web-field-source-probe.cpp').read_text().split('void reset()',1)[0]
 raw=(STAGE/'d2moo-pinned-source/source/D2Game/src/MISSILES/MissMode.cpp').read_bytes();assert hashlib.sha256(raw).hexdigest()==old['sourceHashes']['source/D2Game/src/MISSILES/MissMode.cpp'];text=raw.decode();blocks={}
 def routine(name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  b=text[m.start():i];blocks[name]=hashlib.sha256(b.encode()).hexdigest();return b+'\n'
 stub='int32_t MISSMODE_SrvHit37_BladeCreeper(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return 3;}'
 assert cpp.count(stub)==1;cpp=cpp.replace(stub,routine('MISSMODE_SrvHit37_BladeCreeper'))
 cpp=cpp.replace('int allocations=0,delay=-1,worldMask=1;','int allocations=0,delay=-1,worldMask=1;bool deadOwner=false,successful=true;int teleports=0,hitEvents=0,nullEvents=0;')
 stub='bool SUNITDMG_IsHitSuccessful(D2UnitStrc*,D2UnitStrc*,int,int){ar++;return true;}'
 assert cpp.count(stub)==1;cpp=cpp.replace(stub,'bool SUNITDMG_IsHitSuccessful(D2UnitStrc* p,D2UnitStrc*,int tohit,int missile){assert(p==&owner && p->dwUnitType==UNIT_MONSTER && tohit==37 && missile==1);ar++;return successful;}')
 stub='void SUNITEVENT_Trigger(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,void*){}'
 assert cpp.count(stub)==1;cpp=cpp.replace(stub,'void SUNITEVENT_Trigger(D2GameStrc*,int event,D2UnitStrc* victim,D2UnitStrc* missile,void*){assert(event==UNITEVENT_HITBYMISSILE && (victim==&target || victim==nullptr) && missile==&parent);if(victim){hitEvents++;}else{nullEvents++;}}')
 cpp+='''bool SUNIT_IsDead(D2UnitStrc* p){assert(p==&owner);return deadOwner;}
void D2Common_10229(D2DynamicPathStrc*,D2UnitStrc* p,D2ActiveRoomStrc*,int x,int y){assert(p==&parent);teleports++;p->x=x;p->y=y;}
'''+routine('MISSMODE_SrvDo20_BladeCreeper')
 cpp+=r'''void reset(){owner={};owner.dwUnitType=UNIT_MONSTER;owner.x=80;owner.y=95;target={};target.dwUnitType=UNIT_MONSTER;parent={};parent.dwClassId=392;parent.dwMissileMode=3;parent.path.velocity=0;parent.path.flags=64;parent.pDynamicPath=&parent.path;parent.pMissileData=&parent.md;parent.frame=50;parent.activation=50;parent.stats[STAT_TOHIT]=37;row={};row.dwMissileFlags=0;row.nCollideKill=0;row.nAlwaysExplode=0;row.wSrvHitFunc=37;row.nToHit=1;row.nNextHit=1;row.nNextDelay=25;allocations=0;delay=-1;finds=fills=flags=areas=creates=frameWrites=moves=ar=hitEvents=nullEvents=teleports=0;hasOwner=hasRoom=allocate=true;hasRecord=true;deadOwner=false;successful=true;worldMask=1;victims={&target};stru_6FD2E5F8[3].pfUnitFindCallback=MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster;}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int own:{0,1})for(int dead:{0,1})for(int room:{0,1})for(int nhd:{0,1})for(int hostile:{0,1})for(int hit:{0,1})for(int alloc:{0,1})for(int path:{0,1,64})for(int mask:{0,1,4}){
 reset();hasOwner=own;deadOwner=dead;hasRoom=room;target.justHit=nhd;target.hostile=hostile;successful=hit;allocate=alloc;parent.path.flags=path;worldMask=mask;
 int result=MISSMODE_SrvDo20_BladeCreeper(&game,&parent);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",own,dead,room,nhd,hostile,hit,alloc,path,mask,result,teleports,parent.x,parent.y,parent.frame,finds,ar,fills,flags,allocations,delay,hitEvents,int(target.justHit),nullEvents);first=false;
}printf("]\n");}
'''
 p=STAGE/'blade-sentinel-hit-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==1152
 for row in rows:
  own,dead,room,nhd,hostile,hit,alloc,path,mask,*actual=row;live=bool(own and not dead);search=live and room and path==64 and mask!=0;contact=bool(search and not nhd and hostile)
  expected=[1 if live else 2,int(live),80 if live else 40,95 if live else 70,9 if live else 50,int(bool(search)),int(contact),int(contact and hit),int(contact and hit),int(contact),1025 if contact else -1,int(contact),int(nhd or contact and alloc),int(not live or search and mask==4 and not contact)]
  assert actual==expected,(row,expected)
 out=STAGE/'blade-sentinel-hit-source-probe.json';out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',talentId='assassin.bladesentinel',handlerEnabled=False,legacyCommit=old['legacyCommit'],sourceHashes=old['sourceHashes'],compiledUnchangedRoutines=old['compiledUnchangedRoutines']|blocks,fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=1152),established=[
 'Complete follow invokes complete collision/finder/router/hit37 with a monster blade as the actual missile owner. Follow teleports to the pet, resets frame10 and collision decrements it to9. Missing/dead pet returns2. Actual Size3 geometry/world traversal remains a declared finder service.',
 'NHD/friendly gates run in the finder before selecting a contact. The router calls hit-success using blade owner and explicit missile tohit37, starts25-frame NHD even on a missed AR roll, and emits hit-by-missile once per contact. Successful AR then hit37 returns2 and reaches damage-reader/flag services. Failed AR skips those services.',
 'Crucially, follow ignores the collision-handler return value and itself returns1 while pet lives. A missed AR roll or terrain barrier therefore does not independently delete this following missile through the generic collision return. AI/actor death controls its lifetime. No permanent LastCollide target is recorded by the pinned row.',
 'Router also calls the declared event service with a null target on teardown/barrier paths; those call counts are checked without claiming actual null-target event dispatch. Legacy global JUSTHIT and failed allocation behavior are historical controls only. Modern origin/cast-scoped leases and exact AR/stat/event/damage/defense behavior still require an inherited native blade context.'
 ],services=['World masks, fixed ordered target list, path teleport and candidate geometry, AR decision, state allocation, packet reader and damage flags are declared services. The full collision/finder/router/hit37/follow execute unchanged. Full hit-success formula and full SetDamageFlags/mitigation are separate evidence, not executed in this harness.','No native actor handler, full player supplier/AI/seed/stat ordering, PvP/NPC passive acceptance or current executable equivalence is claimed.'])
 p=ROOT/'tools/reference/blade-sentinel-hit-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Blade Sentinel:1152 full follow/collision/finder/router/hit37 cases pass; misses consume NHD while live following actor retains missile.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
