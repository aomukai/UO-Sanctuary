#!/usr/bin/env python3
"""Reuse complete sentry death entry for nonempty barrier owner/minion graphs.

Animation scheduling, modifier subscribers and initial spawn flags remain explicit
boundaries. This does not equate a source mode transition with native deletion.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path
import subprocess
def function(text,name):
    found=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\{',text,re.M)
    assert found,name
    end=text.index('{',found.start())+1;depth=1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[found.start():end]

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'bone-barrier-death-source'

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def build(check=False):
    STAGE.mkdir(exist_ok=True)
    parent_path=ROOT/'tools/reference/sentry-death-entry-source-evidence.json'
    parent=json.loads(parent_path.read_text())
    fixture=ROOT/parent['fixture']['path']
    assert sha(fixture)==parent['fixture']['sha256']
    cpp_path=BASE/'sentry-death-entry-source-probe.cpp'
    cpp=cpp_path.read_text().split('int main()',1)[0]
    for name,digest in parent['compiledRoutineHashes'].items():
        assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==digest,name
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    assert tree['sha']==parent['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs={str(parent_path.relative_to(ROOT)):sha(parent_path),str(cpp_path.relative_to(ROOT)):sha(cpp_path),str(fixture.relative_to(ROOT)):sha(fixture)}
    def source(relative):
        p=BASE/'d2moo-pinned-source'/relative;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    general=source('source/D2Game/src/AI/AiGeneral.cpp')
    allocate=function(general,'AIGENERAL_AllocMinionList')
    old='void AIGENERAL_AllocMinionList(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){assert(false);}'
    assert cpp.count(old)==1;cpp=cpp.replace(old,allocate)
    tables={};manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text())['files']
    for name in ['monstats','monstats2']:
        p=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json'
        assert sha(p)==manifest[f'json/{name}.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);tables[name]=json.loads(p.read_text())['bonewall']
    row=tables['monstats'];model=tables['monstats2']
    assert not row.get('deathDmg') and not row.get('SplEndDeath') and not model.get('deadCol')
    source_id=row['*hcIdx']
    cpp=cpp.replace('MONSTER_BONEWALL=902',f'MONSTER_BONEWALL={source_id}')
    cpp=cpp.replace('#include <cstdlib>','#include <cstdlib>\n#include <set>\n#include <vector>')
    cpp=cpp.replace('int hoverFrees=0,','int collisionOwner=-9;int hoverFrees=0,')
    old='void PATH_SetUnitDeadCollision(D2UnitStrc* u,int value){assert(value==1);u->stats[30]++;}'
    assert cpp.count(old)==1;cpp=cpp.replace(old,'void PATH_SetUnitDeadCollision(D2UnitStrc* u,int value){assert(value==1);u->stats[30]++;collisionOwner=u->ai->dwOwnerGUIDEx;}')
    compiled=dict(parent['compiledRoutineHashes']);compiled['AIGENERAL_AllocMinionList']=hashlib.sha256(allocate.encode()).hexdigest()
    for name,digest in compiled.items():assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==digest,name
    cpp+=r'''
void List(D2MinionListStrc* p){printf("[");bool first=true;for(;p;p=p->pNext){printf("%s%d",first?"":",",p->dwMinionGUID);first=false;}printf("]");}
void Collect(D2MinionListStrc*p,std::set<D2MinionListStrc*>& nodes){for(;p;p=p->pNext)nodes.insert(p);}
int main(int argc,char**){bool first=true;printf("[");
for(int kind:{78,88})for(int flags:{0,1,2,3})for(int count:{0,1,2,3})for(int mask=0;mask<(1<<count);mask++)for(int direction:{0,7}){
 if(argc==1&&flags==1&&mask!=(1<<count)-1)continue;
 if(argc>1&&!(kind==78&&flags==1&&count==1&&mask==0&&direction==0))continue;
 game={};owner={};pet={};root={};room={};region={};monster={};model={};D2PathStrc path;D2MonsterRegionStrc* regions[]={&region};game.pMonReg=regions;
 owner.dwUnitType=0;owner.dwUnitId=1;pet.dwUnitType=1;pet.dwClassId=MONSTER_BONEWALL;pet.dwUnitId=17;pet.room=&room;pet.pDynamicPath=&path;
 pet.dwFlags=UNITFLAG_NOTC|UNITFLAG_TARGETABLE;pet.dwFlagEx=UNITFLAGEX_STOREOWNERINFO;pet.dwOwnerType=0;pet.dwOwnerGUID=1;
 game.pUnitList[0][1]=&owner;game.pUnitList[1][17]=&pet;
 pet.ai=AIGENERAL_AllocAiControl(&game);AIGENERAL_SetOwnerData(&game,&pet,1,0,0,1);assert(pet.ai->nAiFlags==2);pet.ai->nAiFlags=flags;
 D2UnitStrc children[3];std::set<D2MinionListStrc*> nodes;
 for(int i=0;i<count;i++){
  auto& child=children[i];child.dwUnitId=18+i;child.ai=AIGENERAL_AllocAiControl(&game);AIGENERAL_SetOwnerData(&game,&child,17,1,0,0);
  if(mask&(1<<i))game.pUnitList[1][18+i]=&child;
  AIGENERAL_AllocMinionList(&game,&pet,&child);
 }
 Collect(pet.ai->pMinionList,nodes);
 root.values=pet.stats;root.dwFlags=STATLIST_EXTENDED;pet.pStatListEx=&root;pet.stats[STAT_HITPOINTS]=257;pet.stats[STAT_MAXHP]=25600;
 hoverFrees=refreshes=eventDeletes=lootAttempts=deathEvents=freed=0;collisionOwner=-9;arrayPresent=true;boss=false;
 D2ModeChangeStrc change{&pet,{direction}};int entered=sub_6FC63B30(&game,&change);int modeAtEntry=pet.dwAnimMode;
 assert(entered==1&&modeAtEntry==0&&pet.stats[30]==1&&eventDeletes==1&&lootAttempts==0&&region.dwMonKillCount==0);
 assert(pet.stats[STAT_HITPOINTS]==257&&pet.stats[STAT_MAXHP]==25600&&path.direction==direction);
 // Death-end subscriber is deliberately empty, exactly as the retained sentry fixture.
 sub_6FC641D0(&game,&pet);
 assert(pet.dwAnimMode==12&&deathEvents==1&&SUNIT_GetServerUnit(&game,1,17)==&pet);
 printf("%s{\"input\":[%d,%d,%d,%d,%d],\"root\":[%d,%d,%d,%d,%d,%d,%d],\"rootList\":",first?"":",",kind,flags,count,mask,direction,modeAtEntry,pet.dwAnimMode,pet.ai->dwOwnerTypeEx,pet.ai->dwOwnerGUIDEx,pet.ai->nAiFlags,collisionOwner,pet.stats[30]);first=false;
 List(pet.ai->pMinionList);printf(",\"children\":[");
 for(int i=0;i<count;i++){
  auto*a=children[i].ai;printf("%s[%d,%d,%d,",i?",":"",a->dwOwnerTypeEx,a->dwOwnerGUIDEx,a->nAiFlags);List(a->pMinionList);printf("]");Collect(a->pMinionList,nodes);
 }
 printf("]}");for(auto*n:nodes)delete n;for(int i=0;i<count;i++)delete children[i].ai;delete pet.ai;
}printf("]\n");}
'''
    path=STAGE/'probe.cpp';path.write_text(cpp)
    with (STAGE/'compiler.log').open('w') as out:
        subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(STAGE/'probe')],stdout=out,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(STAGE/'probe')],capture_output=True)
    (STAGE/'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
    negative=subprocess.run([str(STAGE/'probe'),'missing-child'],capture_output=True)
    (STAGE/'missing-child-sanitizer.log').write_bytes(negative.stderr)
    assert negative.returncode!=0 and b"member access within null pointer of type 'struct D2AiControlStrc'" in negative.stderr
    cases=json.loads(run.stdout);assert len(cases)==196
    transfers=detaches=0
    for item in cases:
        kind,flags,count,mask,direction=item['input'];present=[i for i in range(count) if mask&(1<<i)];chosen=max(present) if present and flags==3 else None
        owner=18+chosen if chosen is not None else 1;owner_type=1 if chosen is not None else 0
        assert item['root']==[0,12,owner_type,owner,flags,owner,1]
        assert item['rootList']==list(range(17+count,17,-1))
        for i,(actual_type,actual_owner,actual_flags,actual_list) in enumerate(item['children']):
            live=i in present
            expected_owner=owner if chosen is not None and live else -1 if flags==1 and live else 17
            expected_flags=3 if chosen==i else 0
            expected_list=[18+j for j in present if j!=chosen]+[17] if chosen==i else []
            assert [actual_type,actual_owner,actual_flags,actual_list]==[1,expected_owner,expected_flags,expected_list],item
        transfers+=chosen is not None;detaches+=flags==1 and bool(present)
    output=STAGE/'cases.json';encoded=json.dumps(cases,separators=(',',':'))+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    native_receipt=ROOT/'tools/reference/mage-world-native-evidence.json'
    native=json.loads(native_receipt.read_text());frozen=native['inputInventories']['birth']
    assert sha(ROOT/frozen['path'])==frozen['sha256']
    from snapshot_skill_build import check as snapshot
    snapshot(BASE/'mage-world/pre-build-2.json',ROOT/frozen['path'])
    inputs[str(native_receipt.relative_to(ROOT))]=sha(native_receipt)
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest,name
    inputs[str(Path(__file__).relative_to(ROOT))]=sha(Path(__file__))
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],runtimeChanged=False,currentExecutableParity=False,unchangedInstalledArtifacts=106,lastNativeEvidence=str(native_receipt.relative_to(ROOT)),
        reusedEvidence=str(parent_path.relative_to(ROOT)),inputs=inputs,compiledRoutineHashes=compiled,
        verified=dict(contexts=len(cases),unsupportedMissingChildFlag1Contexts=44,negativeSanitizerControls=1,successorTransfers=transfers,detachedGraphContexts=detaches,sourceBodySpecies=source_id,entryMode=0,completedMode=12),
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [path,STAGE/'compiler.log',STAGE/'sanitizer.log',STAGE/'missing-child-sanitizer.log',output]},
        established=[
            'Complete regular death-entry/cleanup/death-end and AI owner/update bodies are reused from the accepted sentry proof; complete minion allocation replaces its unused stub. 196 safe controlled Wall/Prison graph contexts pass with ASan/UBSan.44 flag1 contexts with missing child registry entries are excluded because the complete legacy function dereferences null; an explicit negative sanitizer control reproduces that unsupported source path without editing its body.',
            'With flag1+flag2 and any registered child, UpdateMinionList promotes the first registered list node, transfers the dying root and surviving siblings to it, and preserves ordered list links before dead collision is requested. Flag1 alone detaches registered children. Flags0/2 leave this graph unchanged. The flag3 successor search skips missing children; the flag1 detach branch does not and has a reproduced source null-dereference path.',
            'Pinned bonewall has deadCol0,deathDmg0,SplEndDeath0. Entry requests dead collision and mode0; completion changes to12 without deleting the registered actor or changing its supplied fractional HP. Empty death subscribers do not imply an empty real barrier modifier path.'],
        remaining=[
            'Initial low-level spawn flags remain uncertain: source summon/skill calls OR bits and do not clear an existing flag1. The null spawn stub cannot prove whether actual barrier roots start at2 or3. The graph matrix deliberately varies flags rather than inventing their live distribution.',
            'KillMinions modifier dispatch/subscribers, full death scheduler/DT15 timing, hidedead/GUID removal, natural world events, exact collision masks and modern executable parity remain separate. Existing native immediate deletion/root cleanup remains unchanged until the phase and graph contract can be connected honestly.',
            'SearchEnemyNear client flag alone does not establish Prison empty-ground radius/facing/tie policy. Reviewed FoH and Psychic Hammer implementations have different observed contracts; neither is copied into Prison without supporting evidence.'])
    out=ROOT/'tools/reference/bone-barrier-death-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print(f'Barrier death entry:196 complete graph contexts/1negative missing-child sanitizer control;{transfers} successor transfers/{detaches} detach contexts;collision follows graph update,mode12 retains identity. Native phase remains open.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
