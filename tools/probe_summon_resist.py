#!/usr/bin/env python3
"""Reuse complete summon resistance ABI; keep the two curve models distinct."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'summon-resist-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True);inputs={};tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    def source(rel):
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel)
        inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    nec=source('source/D2Game/src/SKILLS/SkillNec.cpp');common=source('source/D2Common/src/D2Skills.cpp')
    bodies={n:function(s,n) for s,n in [(nec,'D2GAME_SetSummonResistance_6FD0C2E0'),(common,'D2Common_11033'),(common,'D2Common_11034')]}
    names=['SKILLS_SrvDo031_RaiseSkeleton_Mage','SKILLS_SrvDo056_Golem','SKILLS_SrvDo057_IronGolem','SKILLS_SrvDo058_Revive','SKILLS_SrvDo060_BoneWall','SKILLS_SrvDo062_BonePrison']
    reviewed={n:function(nec,n) for n in names}
    for n in names[:3]:assert 'D2GAME_SetSummonResistance_6FD0C2E0' in reviewed[n]
    for n in names[3:]:assert 'D2GAME_SetSummonResistance_6FD0C2E0' not in reviewed[n]
    refs=ROOT/'tools/reference/d2r-named-equipment';p=refs/'json/skills.json';assert sha(p)==json.loads((refs/'sources.json').read_text())['files']['json/skills.json']['sha256']
    inputs[str(p.relative_to(ROOT))]=sha(p);row=json.loads(p.read_text())['89']
    assert (row['skill'],row['Param1'],row['Param2'],row['passivestat1'],row['passivecalc1'])==('Summon Resist',20,75,'passive_summon_resist','dm12')
    parent=BASE/'iron-golem-birth-source/source.cpp';parent_receipt=ROOT/'tools/reference/iron-golem-birth-source-evidence.json'
    proof=json.loads(parent_receipt.read_text());assert sha(parent)==proof['artifacts'][str(parent.relative_to(ROOT))]
    inputs[str(parent.relative_to(ROOT))]=sha(parent);inputs[str(parent_receipt.relative_to(ROOT))]=sha(parent_receipt)
    cpp=parent.read_text().split('int main(){',1)[0];assert bodies['D2GAME_SetSummonResistance_6FD0C2E0'] in cpp
    def replace(a,b):
        nonlocal cpp
        assert cpp.count(a)==1,a;cpp=cpp.replace(a,b)
    replace('#define __fastcall','#define __stdcall\n#define __fastcall')
    replace('struct D2SkillsTxt{','struct D2SkillsTxt{int dwParam[2]={20,75};')
    replace('struct D2StatListStrc{}list;','struct D2StatListStrc{}list;bool allocation=true;int allocations=0,posts=0;')
    replace('D2StatListStrc*STATLIST_AllocStatList(void*,int,int,int,int){return &list;}',
        'D2StatListStrc*STATLIST_AllocStatList(void*,int,int,int,int){allocations++;return allocation?&list:nullptr;}')
    replace('void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){}',
        'void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){posts++;}')
    cpp+='D2SkillsTxt*DATATBLS_GetSkillsTxtRecord(int id){assert(id==89);return &skill;}\n'+bodies['D2Common_11033']+'\n'+bodies['D2Common_11034']
    cpp+=r'''int main(){bool first=true;printf("[");
for(int r=0;r<=255;r++)for(int f:{-1,0,10})for(int l:{-1,0,10})for(int c:{-1,0,10})for(int alloc:{0,1}){
 // This is the retained port model, explicitly separate from compiled11033.
 int retained=r==0?0:20+55*std::min(100,110*r/(r+6))/100,legacy=D2Common_11034(r,89);
 owner={};pet={};owner.stats[STAT_PASSIVE_SUMMON_RESIST]=retained;
 pet.stats[STAT_ITEM_ABSORBFIRE_PERCENT]=f;pet.stats[STAT_ITEM_ABSORBLIGHT_PERCENT]=l;pet.stats[STAT_ITEM_ABSORBCOLD_PERCENT]=c;
 allocation=alloc;allocations=posts=resMask=0;D2GAME_SetSummonResistance_6FD0C2E0(&owner,&pet);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",r,retained,legacy,f,l,c,alloc,resMask,pet.stats[39],pet.stats[41],pet.stats[43],pet.stats[45],allocations,posts);first=false;
}printf("]\n");}'''
    assert all(body in cpp for body in bodies.values());p=STAGE/'probe.cpp';p.write_text(cpp)
    with (STAGE/'compiler.log').open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(p.with_suffix(''))],stdout=log,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(p.with_suffix(''))],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
    cases=json.loads(run.stdout);assert len(cases)==13824
    for r,retained,legacy,f,l,c,alloc,mask,fire,light,cold,poison,calls,posts in cases:
        assert retained==(0 if r==0 else 20+55*min(100,110*r//(r+6))//100)
        assert legacy==(0 if r==0 else min(75,20+(55*110*r//(r+6))//100))
        active=retained!=0 and alloc!=0
        expected=[retained if active and f<=0 else 0,retained if active and l<=0 else 0,retained if active and c<=0 else 0,retained if active else 0]
        assert [fire,light,cold,poison]==expected and mask==sum(1<<i for i,v in enumerate(expected) if v)
        assert (calls,posts)==(int(retained!=0),int(active))
    (STAGE/'cases.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    observations=ROOT/'tools/reference/diminishing-reference-conflict.json';inputs[str(observations.relative_to(ROOT))]=sha(observations)
    observed=json.loads(observations.read_text())['pinnedBuildObservations']
    assert observed['referenceVersion']=='3.3.93847' and observed['skill']=='Summon Resist'
    # Keep the existing record intact: these three ranks agree with both models.
    tips=[(r,20+55*min(100,110*r//(r+6))//100,min(75,20+55*110*r//(r+6)//100)) for r in [7,13,19]]
    assert tips==[(7,52,52),(13,61,61),(19,65,65)]
    assert [(r['effectiveRank'],r['reportedPercent'],r['currentNativePercent'],r['decompiledHelperPercent']) for r in observed['values']]==[(r,a,a,b) for r,a,b in tips]
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=13824,acceptedAllocationContexts=6912,
        inputs=inputs,executed={n:hashlib.sha256(s.encode()).hexdigest() for n,s in bodies.items()},staticallyReviewed={n:hashlib.sha256(s.encode()).hexdigest() for n,s in reviewed.items()},
        currentObservations=tips,
        established=['Complete resistance helper executes all256retained-rank values with27signed absorb combinations and accepted/refused allocation. Nonzero bonus allocates once; only accepted nodes attach/write. Positive elemental absorption suppresses that elemental bonus, nonpositive admits it; poison is unconditional. No physical/magic resistance writes.',
            'Three complete birth callbacks invoke the helper for warrior/Mage,Clay/Blood/Fire andIron. Reviewed Revive/Wall/Prison callbacks do not invoke it; that absence is not a complete modern passive/event proof.',
            'Full unchanged legacy11033/11034 also execute. Retained and legacy curve outputs are kept separate in every row; neither helper is rewritten to disguise rounding conflicts.'],
        remaining=['User7/13/19readings corroborate52/61/65 but agree with both curves. Current Dodge3/Avoid3 calibrate retained shared two-stage rounding; applying that same dm12 callback to Summon Resist is still a cross-skill inference, not a direct current-executable proof at distinguishing ranks3/21.',
            'Formula/stat-list services and retained bonus supplier are declared; full passive stat registration/allocator/current executable/events/Sunder/item/rank grants and recipient runtime require separate evidence. No new gameplay implementation or class completion from this source-only probe.'],
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__).resolve(),STAGE/'probe.cpp',STAGE/'cases.json',STAGE/'compiler.log',STAGE/'sanitizer.log']})
    out=ROOT/'tools/reference/summon-resist-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Summon Resist:13824 complete allocation/channel contexts; both curve models retained separately; current tooltip gap explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
