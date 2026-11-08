#!/usr/bin/env python3
"""Exercise the existing complete non-curse factory/receiver with owned Sigil data.

One small new branch probe;no old compiler matrix is repeated.
"""
import argparse,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/sigil-lethargy-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 parent=ROOT/'tools/reference/amplify-damage-source-evidence.json';proof=json.loads(parent.read_text());ancestor=ROOT/'.local/class-skills/amplify-damage-source/probe.cpp';assert sha(ancestor)==proof['artifacts'][str(ancestor.relative_to(ROOT))]
 folder=ROOT/'tools/reference/d2r-named-equipment/json';skills=json.loads((folder/'skills.json').read_text());states=json.loads((folder/'states.json').read_text());stats=json.loads((folder/'itemstatcost.json').read_text());row=skills['393'];state=states['sigillethargy']
 assert state['*ID']==192 and not state.get('curse',0)and row['aurafilter']==3 and row['auralencalc']==12
 assert [row['aurastat'+str(i)]for i in [1,2,3]]==['attackrate','item_armor_percent','velocitypercent']and [stats[k]['*ID']for k in ['attackrate','item_armor_percent','velocitypercent']]==[68,16,67]
 assert [row['aurastatcalc'+str(i)]for i in [1,2,3]]==['ln78','ln56','ln78']and [row['Param'+str(i)]for i in [5,6,7,8]]==[-33,-1,-50,0]
 cpp='#include <vector>\nstd::vector<int> trace;\n'+ancestor.read_text().split('int main(){')[0]
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a;cpp=cpp.replace(a,b)
 replace('D2StatListStrc* list=nullptr;','D2StatListStrc* list=nullptr;D2StatListStrc* curse=nullptr;')
 replace('nItemStatCostTxtRecordCount=64','nItemStatCostTxtRecordCount=512')
 replace('D2StatListStrc* STATLIST_GetStatListFromUnitAndFlag(D2UnitStrc* p,unsigned){return p->list;}','D2StatListStrc* STATLIST_GetStatListFromUnitAndFlag(D2UnitStrc* p,unsigned){return p->curse;}')
 replace('void STATLIST_SetStatIfListIsValid(D2StatListStrc* p,int s,int v,int){p->stats[s]=v;}','void STATLIST_SetStatIfListIsValid(D2StatListStrc* p,int s,int v,int){p->stats[s]=v;trace.push_back(s);trace.push_back(v);}')
 cpp+=r'''
int main(){bool first=true;std::printf("{\"cases\":[");
for(int kind:{0,1})for(int oldRank:{0,1,20,255})for(int rank:{1,10,20,255})for(int resist:{0,100})for(int attract:{0,1}){
 D2UnitStrc owner,body,target;owner.dwUnitType=0;owner.dwUnitId=7;owner.pvp=true;owner.room=&room;
 body.dwUnitType=3;body.dwUnitId=9;body.owner=&owner;body.room=&room;target.dwFlags=7;target.room=&room;target.dwUnitType=kind;target.attract=attract;target.stats[STAT_CURSE_RESISTANCE]=resist;
 D2StatListStrc old,curse;old.skill=393;old.state=192;old.rank=oldRank;old.creator=999;old.expire=777;old.flags=2;old.stats[68]=-17;old.stats[16]=-900;old.stats[67]=-23;
 curse.skill=72;curse.state=19;curse.rank=77;curse.creator=555;curse.expire=888;curse.flags=34;curse.stats[16]=-66;target.list=oldRank?&old:nullptr;target.curse=&curse;
 curseMask=false;removals=events=updates=0;trace.clear();D2UnkNecSkillStrc a;a.pUnit=&body;a.pGame=&game;a.nSkillId=393;a.nSkillLevel=rank;a.nAuraTargetState=192;a.nDuration=12;a.nAuraStat[0]=68;a.nAuraStat[1]=16;a.nAuraStat[2]=67;a.nAuraStat[3]=-1;a.nAuraStatCalc[0]=-50;a.nAuraStatCalc[1]=-33-(rank-1);a.nAuraStatCalc[2]=-50;a.bUpdateAnimRate=1;
 int result=sub_6FD0B450(&target,&a);auto*p=target.list;int unchanged=target.curse==&curse&&curse.skill==72&&curse.state==19&&curse.rank==77&&curse.creator==555&&curse.expire==888&&curse.flags==34&&curse.stats[16]==-66;
 if(!first)std::printf(",");first=false;std::printf("{\"input\":[%d,%d,%d,%d,%d],\"output\":[%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,%d,%d],\"writes\":[",kind,oldRank,rank,resist,attract,result,p?p->rank:0,p?p->creator:0,p?p->expire:0,p?p->stats[68]:0,p?p->stats[16]:0,p?p->stats[67]:0,p?p->flags:0,removals,events,updates,unchanged);
 for(unsigned i=0;i<trace.size();i++){if(i)std::printf(",");std::printf("%d",trace[i]);}std::printf("]}");
}std::printf("]}\n");}
'''
 STAGE.mkdir(exist_ok=True);p=STAGE/'probe.cpp';exe=STAGE/'probe';p.write_text(cpp);subprocess.run(['g++','-std=c++17','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(p),'-o',str(exe)],check=True,capture_output=True)
 output=subprocess.check_output([str(exe)],text=True);cases=json.loads(output)['cases'];assert len(cases)==128 and all(r['output'][-1]==1 for r in cases);out=STAGE/'cases.json';encoded=json.dumps(dict(cases=cases),separators=(',',':'))+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 artifacts=[p,out,exe,ancestor,parent,Path(__file__).resolve(),*[folder/n for n in ['skills.json','states.json','itemstatcost.json']]]
 result=dict(referenceVersion='3.3.93847',legacyCommit=proof['legacyCommit'],currentExecutableParity=False,handlerEnabled=False,cases=len(cases),compiledUnchangedRoutines=['sub_6FD0B450','sub_6FD10EC0','sub_6FCBD900'],sanitizers='ASan/UBSan',processMemoryRead=False,established=['Owned state192has no curse flag:12frames ignore100curse resistance/Attract and keep the independent ordinary curse pool unchanged.','Same-state lower rank refuses,equal rank retains creator/primary stat while refreshing expiry and rewriting secondary stats,higher rank replaces;ordered writes/flags/events/rate update are recorded.'],services=['Current numeric skill/row packet is supplied to the complete unchanged older receiver/factory. State-mask lookup is supplied from pinned current flags.','State allocation/stat storage,owner/alignment/room services are declared;world radial geometry and full current executor are not executed.','Unusual old primary/secondary values intentionally distinguish retention from rewriting;native saved statuses accept only qualified generated values.'],artifacts={str(q.relative_to(ROOT)):sha(q)for q in artifacts});dest=ROOT/'tools/reference/sigil-lethargy-source-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert dest.read_text()==s
 else:dest.write_text(s)
 print('Lethargy non-curse receiver:128focused complete-source cases;coexistence,rank/creator/primary retention and duration;ASan/UBSan clean.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
