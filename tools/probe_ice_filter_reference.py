#!/usr/bin/env python3
"""Execute the unchanged source aura filter for Ice and Fists control flags."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 parent=ROOT/'tools/reference/lightning-fury-engine-evidence.json';p=json.loads(parent.read_text());path=STAGE/'lightning-fury-search-source-probe.cpp';base=path.read_text().split('int main()',1)[0]
 rel='source/D2Game/src/SKILLS/Skills.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha256(raw).hexdigest()==p['sourceHashes'][rel]
 s=raw.decode();m=re.search(r'^.*\bsub_6FD0FA00\([^;\n]*\)\s*\n\{',s,re.M);i=s.index('{',m.start())+1;depth=1
 while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
 body=s[m.start():i];assert body in base and hashlib.sha256(body.encode()).hexdigest()==p['blockHashes']['sub_6FD0FA00']
 main=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool comma=false;printf("[");for(int filter:{0x870B,0x583})for(int type:{0,1,2,3,4})for(int mode:{0,1,12,17})for(int flags:{0,128,1024,1152})for(int town:{0,1})for(int blocked:{0,1})for(int hostile:{0,1})for(int special:{0,1}){
 caster={};caster.dwUnitType=0;caster.room=&rooms[0];targets[0]={};auto& t=targets[0];t.dwUnitType=type;t.dwAnimMode=mode;t.dwFlags=flags;t.room=&rooms[1];t.hostile=hostile;rooms[1].town=town;row={};row.dwMissileFlags=special?2:0;monsters[0]={};monsters[0].dwMonStatsFlags=special?1:0;wall=blocked;int result=sub_6FD0FA00(&caster,&t,filter);if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d]",filter,type,mode,flags,town,blocked,hostile,special,result);
}printf("]\n");}'''
 cpp=STAGE/'ice-filter-source-probe.cpp';cpp.write_text(base+main);binary=cpp.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-fsanitize=address,undefined','-fno-pie','-no-pie',str(cpp),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==2560
 for f,t,m,flags,town,blocked,hostile,special,result in rows:
  allowed=t in ([0,1,3] if f==0x870B else [0,1]) and not(t==0 and m in [0,17] or t==1 and m in [0,12] or t==3 and special) and bool(flags&1024) and not town
  if f==0x870B:allowed=allowed and not blocked and hostile
  else:allowed=allowed and bool(flags&128)
  assert result==int(allowed),(f,t,m,flags,town,blocked,hostile,special,result,allowed)
 fixture=STAGE/'ice-filter-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',talentId='assassin.bladesofice',reusedSourceComposition='tools/reference/lightning-fury-engine-evidence.json',sourceHash=hashlib.sha256(raw).hexdigest(),filterFunctionHash=hashlib.sha256(body.encode()).hexdigest(),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(cpp.read_bytes()).hexdigest(),cases=len(rows),sanitizers='ASan+UBSan'),established=['Complete0x870Bfilter selects living valid players/monsters and separately eligible missiles,excludes town/nonhostiles,and tests caster-to-recipient LOS.','Unlike Fists0x583,it does not require CANBEATTACKED. Its missile branch still requires ISVALIDTARGET; the reviewed generic factory clears that flag,so ordinary player missiles are not automatically area targets.'],services=['Types/modes/flags,town,hostility,monster metadata and raytrace are explicit supplied services; complete filter executes unchanged.','Fists control0x583does not have hostile0x8000or LOS0x200; native shared allegiance remains an existing Sanctuary projection.'],limits=['NOAURA does not enter this filter; callback38supplies bCheckMonAuraFlag0to the outer enumeration. Native hostile creature adapter and actual wall LOS are tested separately; full PvP/NPC and exceptional flagged missile recipients remain unsupported acceptance contexts.'])
 out=ROOT/'tools/reference/ice-filter-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Ice filter evidence stale'
 else:out.write_text(encoded)
 print('Ice filter:2560 complete Ice/Fists flag/type/mode/LOS/hostility/town/metadata cases pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
