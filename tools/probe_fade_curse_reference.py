#!/usr/bin/env python3
"""Execute the unchanged shared curse factory and percentage helper with explicit state/statlist services."""
import argparse,ast,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={};assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^\w[^;\n]*\b'+name+r'\([^;{]*?\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  block=text[m.start():i];blocks[name]=hashlib.sha256(block.encode()).hexdigest();return block+'\n'
 factory=routine(source('source/D2Game/src/SKILLS/Skills.cpp'),'sub_6FD10EC0');percent=routine(source('source/D2Game/src/MONSTER/MonsterUnique.cpp'),'MONSTERUNIQUE_CalculatePercentage')
 # Reuse the already reviewed factory service scaffold, not another state engine.
 scaffold=ROOT/'tools/probe_inner_sight_reference.py';syntax=ast.parse(scaffold.read_text());prefix=next(n.value for n in ast.walk(syntax) if isinstance(n,ast.Constant) and isinstance(n.value,str) and n.value.startswith('#include <algorithm>'))
 prefix=prefix.replace('int armor=0,percent=0;bool attract=false;', 'int armor=0,percent=0,resistance=0;bool attract=false;').replace('int STATES_CheckStateMaskCurseByStateId(int){return 0;}', 'int curseMask=0;int STATES_CheckStateMaskCurseByStateId(int){return curseMask;}').replace('stat==STAT_CURSE_RESISTANCE?100:0','stat==STAT_CURSE_RESISTANCE?p->resistance:0')
 stub='int MONSTERUNIQUE_CalculatePercentage(int a,int b,int d){return int(int64_t(a)*b/d);}'
 assert stub in prefix;prefix=prefix.replace(stub,percent)
 cpp=prefix+factory+r'''
int main(){bool first=true;std::printf("[");
for(int mask:{0,1})for(int resistance:{-50,0,1,47,82,90,99,100,150})for(int duration:{0,1,2,25,125,1000,1048577})for(int attract:{0,1})for(int oldRank:{0,1,4})for(int rank:{1,4,7}){
 D2StatListStrc old;old.rank=oldRank;D2UnitStrc target,owner;target.dwUnitType=UNIT_PLAYER;target.list=oldRank?&old:nullptr;target.attract=attract;target.resistance=resistance;owner.dwUnitType=UNIT_PLAYER;
 curseMask=mask;guard=0;freed=events=0;D2CurseStrc curse;curse.pTarget=&target;curse.pUnit=&owner;curse.nSkillLevel=rank;curse.nDuration=duration;
 auto* result=sub_6FD10EC0(&curse);auto* current=target.list;
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",mask,resistance,duration,attract,oldRank,rank,result!=nullptr,curse.nDuration,current?current->rank:0,current?current->expire:0,freed,events);
}std::printf("]\n");}
'''
 p=STAGE/'fade-curse-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==2268
 for mask,resistance,duration,attract,oldRank,rank,success,adjusted,current,expiry,freed,events in rows:
  blocked=bool(mask and (resistance>=100 or attract)) or oldRank>rank
  assert success==int(not blocked)
  if mask and resistance>=100:assert adjusted==duration and current==oldRank and not freed and not events
  if not mask or resistance==0:assert adjusted==duration
 assert any(r[0]==1 and r[1]==82 and r[2]==1 and r[7]==1 and r[6]==1 for r in rows)
 out=STAGE/'fade-curse-source-probe.json';out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,compiledUnchangedRoutines=blocks,scaffold=dict(path=str(scaffold.relative_to(ROOT)),sha256=hashlib.sha256(scaffold.read_bytes()).hexdigest()),fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=len(rows)),established=['Complete shared factory applies curse resistance only to a curse-mask state, rejects resistance>=100 before touching the old state, and subtracts the source truncated percentage before Attract/rank arbitration.', 'One frame survives any positive resistance below100; unmasked positive states are unchanged. Equal rank refreshes the reduced duration, lower rank preserves the old expiry and rank, and Attract prevents a curse-mask replacement.', 'The complete original percentage helper is included, retaining its large-value early division rather than substituting a uniform64bit multiply.'],services=['Unit type is player; state mask, resistance, Attract, supplied rank/duration, state/owner storage, allocation and clock/event operations use the previously reviewed explicit factory scaffold. Formula evaluation, full stat graph, allocator failures, event scheduler and modern executable behavior are outside this proof.', 'Mask=true on the scaffold state17 is a synthetic receiver input, not a claim that current Inner Sight is a curse. Native lookup uses the pinned actual state mask data.'],limits=['This is a complete older factory isolation with declared services, not a3.3.93847 whole-executable certificate. Native damage, class-specific casting and receiver wiring are independently verified.'])
 path=ROOT/'tools/reference/fade-curse-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert path.read_text()==render
 else:path.write_text(render)
 print(f'Fade curse receiver:{len(rows)} complete source factory/percentage cases, including one-frame and large-value boundaries.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
