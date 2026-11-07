#!/usr/bin/env python3
"""Compose complete original item-stat replays with the complete event kernel."""
import hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-original-events-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);inputs={};hashes={}
 def read(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_text()
 numeric=json.loads(read(ROOT/'tools/reference/valkyrie-numeric-source-evidence.json'));p=BASE/'valkyrie-numeric-source/source.cpp';assert sha(p)==numeric['artifacts'][str(p.relative_to(ROOT))];cpp=read(p)
 hashes.update(numeric['completeRoutineHashes']);prefix,main=cpp.split('int main(){Init();',1);setup=main.split('printf("%s[",comma?',1)[0]
 # The retained fixture's fixed storage insert must also maintain the
 # logical capacity consumed by the unchanged source shrink routine.
 old='assert(a->nStatCount<512);memmove';assert cpp.count(old)==1
 cpp=cpp.replace(old,'assert(a->nStatCount<512);if(a->nStatCount>=a->nCapacity)a->nCapacity+=D2StatsArrayStrc::nGrowthAmount;memmove')
 prefix,main=cpp.split('int main(){Init();',1);setup=main.split('printf("%s[",comma?',1)[0]
 cpp=prefix+'void Trace(){printf("[");bool comma=false;for(auto r:trace){if(r[0]!=1)continue;printf("%s[%d,%d]",comma?",":"",r[2],r[4]);comma=true;}printf("]");trace.clear();}\n'
 # Keep the real original item initialization and complete numeric callbacks.
 # Clear between cases, not between original item construction/attachment.
 setup=setup.replace('for(auto c:cases){new(&root)', 'for(auto c:cases){trace.clear();new(&root)')
 cpp+='int main(){Init();'+setup+'''printf("%s[",comma?",":"");comma=true;Trace();
for(int cycle=0;cycle<3;cycle++){
for(int i=0;i<count;i++)D2GAME_ITEMS_UpdateItemStatlist_6FC42310(nullptr,&units[i],&actor,0);printf(",");Trace();
for(int i=count-1;i>=0;i--){D2GAME_ITEMS_UpdateTransferredProperties_6FC424E0(nullptr,&units[i],&actor,1,1);printf(",");Trace();}
for(int i=0;i<count;i++){D2GAME_ITEMS_UpdateItemStatlist_6FC42310(nullptr,&units[i],&actor,0);printf(",");Trace();}
for(int s:{77,76,162})STATLIST_SetBaseStat(&root,s,35,0,&actor);printf(",");Trace();
for(int s:{77,76,162})STATLIST_SetBaseStat(&root,s,0,0,&actor);printf(",");Trace();
D2GAME_ITEMS_UpdateTransferredProperties_6FC424E0(nullptr,&units[0],&actor,1,1);printf(",");Trace();
D2GAME_ITEMS_UpdateItemStatlist_6FC42310(nullptr,&units[0],&actor,0);printf(",");Trace();
}printf("]");}printf("]\\n");}
'''
 for n,h in hashes.items():assert hashlib.sha256(function(cpp,n).encode()).hexdigest()==h,n
 def run(name,source):
  p=STAGE/(name+'.cpp');p.write_text(source);binary=STAGE/name
  with(STAGE/(name+'-compiler.log')).open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(p),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
  r=subprocess.run([str(binary)],capture_output=True);(STAGE/(name+'-sanitizer.log')).write_bytes(r.stderr);assert r.returncode==0 and not r.stderr,r.stderr.decode()[-2500:];return json.loads(r.stdout)
 changes=run('numeric',cpp);assert len(changes)==57
 prior=json.loads(read(ROOT/'tools/reference/unit-events-source-evidence.json'));p=BASE/'unit-events-source/source.cpp';assert sha(p)==prior['artifacts'][str(p.relative_to(ROOT))];events=read(p).split('int main(){printf("[");',1)[0]
 phases=['hitbymissile','damagedinmelee','damagedbymissile','attackedinmelee','doactive','domeleedamage','domissiledamage','domeleeattack','domissileattack','kill','killed','absorbdamage','levelup','death']
 metadata=json.loads(read(ROOT/'data/Sanctuary/Balance/npc-item-combat-events.json'));rows={r['Stat']:r['Events']for r in metadata}
 # Event-only changes are fed unchanged to the complete server callback.
 # All other numeric/resource/skill callback branches remain separate services.
 events+='\nint main(){printf("[");D2GameStrc g;bool comma=false;\n'
 count=0
 for context in changes:
  events+='if(comma)printf(",");comma=true;{D2UnitStrc u;allocated=0;recordExists=true;printf("[");\n'
  for frame,ops in enumerate(context):
   for packed,value in ops:
    row=rows.get(packed>>16)
    if row is None:continue
    a=row+[dict(Event=None,Function=0)]*(2-len(row));count+=1
    events+='meta.wItemEvent[0]='+str(phases.index(a[0]['Event']))+';meta.wItemEvent[1]='+str(phases.index(a[1]['Event'])if a[1]['Event']else -1)+';'
    events+='meta.wItemEventFunc[0]='+str(a[0]['Function'])+';meta.wItemEventFunc[1]='+str(a[1]['Function'])+';'
    events+='D2GAME_ITEMMODE_ServerStatlistCallback_6FC41910(&g,&u,nullptr,'+str(packed)+',0,'+str(value)+');\n'
   events+=('printf(",");'if frame else '')+'dump(&u);\n'
  events+='printf("]");SUNITEVENT_FreeEventList(&g,&u);if(!live.empty())__builtin_trap();}\n'
 events+='printf("]\\n");}\n'
 for n,h in prior['completeRoutineHashes'].items():assert hashlib.sha256(function(events,n).encode()).hexdigest()==h,n
 states=run('events',events);assert len(states)==57
 p=BASE/'valkyrie-numeric-source/cases.json';original=json.loads(read(p));output=[]
 for i,(c,s)in enumerate(zip(original,states)):
  per=5+2*len(c['Items']);assert len(s)==1+3*per
  output.append(dict(Case=i,Rank=c['Rank'],Difficulty=c['Difficulty'],Items=len(c['Items']),StepsPerCycle=per,States=s))
 out=STAGE/'cases.json';out.write_text(json.dumps(output,separators=(',',':'))+'\n')
 artifacts=[STAGE/n for n in ['numeric.cpp','numeric','numeric-compiler.log','numeric-sanitizer.log','events.cpp','events','events-compiler.log','events-sanitizer.log','cases.json']]+[Path(__file__).resolve()]
 receipt=dict(referenceVersion='3.3.93847',currentExecutableParity=False,contexts=57,originalItems=330,cycles=3,stages=sum(len(s)for s in states),eventStatChanges=count,numericRoutineHashes=hashes,eventRoutineHashes=prior['completeRoutineHashes'],inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in artifacts},scope='Complete older numeric/item replay produces actor value changes from57unfiltered original loadouts;actual current21stat registration metadata and complete older event callback/list kernel consume those changes. Initial replay,repeat,all removals/re-additions,percentage changes and shifted reattachment order continue through three cycles. Event IDs/head/order/keys/functions are emitted under ASan/UBSan. Non-event callback effects and actual proc bodies remain separate;current executable chronology not certified.')
 (ROOT/'tools/reference/npc-original-events-source-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n');print(f'Original NPC event source:57loadouts/330items,{receipt["stages"]}stages/{count}eventchanges,3cycles;complete numeric and8event routines;ASan/UBSan clean.')
if __name__=='__main__':main()
