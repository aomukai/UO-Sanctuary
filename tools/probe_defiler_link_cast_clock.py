#!/usr/bin/env python3
"""Reuse complete source predicates/Other writer/scheduler for owned Defiler S1."""
import argparse,csv,hashlib,json,re,struct,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/defiler-link-cast-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(exist_ok=True);inputs={}
    def pin(p):
        inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_text()
    tree=json.loads(pin(ROOT/'.local/class-skills/d2moo-source-tree.json'));assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    def source(rel):
        p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel)
        return pin(p)
    units=source('source/D2Common/src/Units/Units.cpp');game=source('source/D2Game/src/UNIT/SUnit.cpp');ids=source('source/D2Common/include/DataTbls/MonsterIds.h')
    owned=json.loads(pin(ROOT/'tools/reference/d2r-local-sentry-model-evidence.json'));tables={}
    for key in ('monstats','monstats2'):
        entry=owned['files'][key];p=ROOT/entry['path'];assert sha(p)==entry['sha256']
        tables[key]=next(r for r in csv.DictReader(pin(p).lstrip('\ufeff').splitlines(),delimiter='\t')if r['Id']=='warputriddefiler')
    assert tables['monstats']['Code']=='45'and tables['monstats']['Skill1']=='Health Link'and tables['monstats']['Sk1mode']=='S1'
    assert tables['monstats2']['mS1']=='1'and not tables['monstats2']['S1mv']
    skills=json.loads(pin(ROOT/'tools/reference/d2r-named-equipment/json/skills.json'));row=skills['405']
    assert row['skill']=='Health Link'and row['monanim']=='S1'and not row.get('UseAttackRate',0)and not row.get('passive',0)
    flying=json.loads(pin(ROOT/'tools/reference/defiler-flying-motion-source-evidence.json'));assert flying['animations']['45S1HTH']==dict(frames=16,speed=128,events=[[12,1]])
    parent=pin(ROOT/'tools/probe_spirit_wolf_rate_reference.py');prefix=parent.split("    cpp=r'''",1)[1].split("'''+ids",1)[0]
    prefix=prefix.replace('int nSkillId=0;','int nSkillId=0,nSeqTrans=0;').replace('pSkillsTxt=nullptr;};','pSkillsTxt=nullptr;unsigned dwFlags=0;};')
    prefix=prefix.replace('assert(false);return nullptr;}\nD2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){assert(false);return nullptr;}',
        'extern D2SkillStrc used;return &used;}\nD2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){extern D2SkillsTxt skill;assert(id==405);return &skill;}')
    prefix=prefix.replace('assert(stat==STAT_ATTACKRATE);return attack;','extern int other;assert(stat==STAT_OTHER_ANIMRATE);return other;')
    cpp=prefix.replace('constexpr int EVENTTYPE_MODECHANGE=1,EVENTTYPE_ENDANIM=2;',
        'constexpr int EVENTTYPE_MODECHANGE=1,EVENTTYPE_ENDANIM=2,STAT_OTHER_ANIMRATE=2,PLRMODE_CAST=10,MONMODE_CAST=7,MONMODE_WALK=2,MONMODE_RUN=15,SKFLAG_PASSIVE=1,SKFLAG_STUN=2,MONSTATSFLAG_NPC=1;')
    cpp+='#include <limits>\n'+ids.replace('#pragma once','')+'\n'
    cpp+='D2SkillsTxt skill;D2SkillStrc used;int other=100;struct D2MonStatsTxt{unsigned dwMonStatsFlags=0;}mon;D2MonStatsTxt* DATATBLS_GetMonStatsTxtRecord(int id){assert(id==744);return &mon;}\n'
    routines={}
    for name in ('gaPlayerModesAnimModulators','gaMonModesAnimModulators','gaMonModesAnimModulatorsExpansion'):
        m=re.search(r'^(?:static )?D2AnimUnkStrc '+name+r'\[[^]]+\]\s*=\s*\{',units,re.M);assert m
        block=units[m.start():units.index('};',m.start())+2];cpp+=block+'\n';routines[name]=hashlib.sha256(block.encode()).hexdigest()
    for text,name in ((units,'UNITS_SetAnimationSpeed'),(units,'UNITS_IsSeqAnimSpeedModulatedByFCR'),(units,'UNITS_CanAnimModeUseVelocityModifier'),(units,'UNITS_CanAnimModeUseAttackRate'),(units,'UNITS_AnimModeAllowsAnimSpeed'),(units,'UNITS_UpdateOtherAnimRateAndVelocity'),(game,'sub_6FCBCE70')):
        block=function(text,name);cpp+=block+'\n';routines[name]=hashlib.sha256(block.encode()).hexdigest()
    cpp+=r'''
int main(){printf("[");bool first=true;for(int rate=-100;rate<=300;rate++){
owner={};anim={};skill={};skill.nSkillId=405;used={};used.pSkillsTxt=&skill;other=rate;owner.dwClassId=744;owner.dwAnimMode=8;owner.dwFrameCountPrecise=16*256;anim.dwAnimSpeed=128;anim.pFrameFlags[12]=1;events.clear();
assert(!UNITS_IsSeqAnimSpeedModulatedByFCR(&owner,UNIT_MONSTER,8));assert(!UNITS_CanAnimModeUseVelocityModifier(UNIT_MONSTER,8,&owner,744));assert(!UNITS_CanAnimModeUseAttackRate(UNIT_MONSTER,8,&owner,744));assert(!UNITS_AnimModeAllowsAnimSpeed(UNIT_MONSTER,8,744));
UNITS_UpdateOtherAnimRateAndVelocity(&owner,8,UNIT_MONSTER,744);sub_6FCBCE70(&game,&owner);printf("%s[%d,%d,[",first?"":",",rate,owner.wAnimSpeed);first=false;bool sep=false;for(auto e:events){printf("%s[%d,%d,%d,%d]",sep?",":"",e.type,e.frame-1000,e.kind,e.param);sep=true;}printf("]]");}printf("]\n");}
'''
    p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe'
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],capture_output=True);(STAGE/'compiler.log').write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr[-3000:]
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr[-3000:]
    cases=json.loads(run.stdout);assert len(cases)==401
    for rate,speed,events in cases:assert speed==128*max(15,min(175,rate))//100 and len(events)==2 and events[0][0]==1 and events[0][2]==1 and events[1][0]==2
    fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    clock=[dict(Skill=405,Mode=8,Frames=16,Speed=128,Action=12,Flag=1)]
    p=ROOT/'data/Sanctuary/Balance/defiler-link-cast.json';encoded=json.dumps(clock,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    pin(p);pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],inputs=inputs,compiledUnchangedRoutineHashes=routines,cases=401,
        clock=clock[0],baseline=next(c for c in cases if c[0]==100),currentExecutableParity=False,newLiveReferenceReads=0,
        established=['OwnedNPC405isactiveS1/UseAttackRate0;current45S1HTH16frames/speed128/action12kind1.','Complete unchanged source FCR/velocity/attack/allow-speed predicates select Other branch for normal active744S1.','Complete Other writer and initial event scheduler compare401 signed total rates with15..175clamping;no masteryIAS/ownerFCR invented.'],
        limits=['Normal active used-skill flags0 and synthetic skill/monstats suppliers are explicit;not current executable/global clock equivalence.','Own actual casting/selection/lifecycle and Other-stat receiver coverage require native checks.'],
        artifacts={str(p.relative_to(ROOT)):sha(p)for p in (STAGE/'probe.cpp',binary,fixture,STAGE/'compiler.log',STAGE/'sanitizer.log')})
    p=ROOT/'tools/reference/defiler-link-cast-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Defiler S1:401unchanged-source predicate/Other-rate/scheduler cases;baseline '+str(result['baseline']))
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
