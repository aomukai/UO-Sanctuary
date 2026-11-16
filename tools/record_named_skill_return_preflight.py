#!/usr/bin/env python3
"""Bind truthful class-versus-item delivery readiness without enabling acquisition."""
import argparse,json,subprocess,sys
import skill_checkpoint_evidence as e
from build_named_skill_backlog import compile_backlog,DATA,REPORT,render
ROOT=e.ROOT;BASE='a1dfc941'
def record(check=False):
    current=compile_backlog();assert e.load(DATA)==current and REPORT.read_text()==render(current)
    old=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/named-skill-backlog.json'],cwd=ROOT))
    direct=e.load(ROOT/'data/Sanctuary/Balance/named-active-skills.json')
    old_direct=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/named-active-skills.json'],cwd=ROOT))
    assert direct==old_direct and sorted(direct)==current['workingDirectSkills']==old['workingDirectSkills'] and len(direct)==16
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    mechanical={r['talentId'] for r in status['skills'] if r['implementation']=='handler-present'}
    assert set(current['mechanicallyImplementedSkills'])==mechanical and len(mechanical)==240
    for name in ('items','sets','heldSetPartners'):assert [r['id']for r in current[name]]==[r['id']for r in old[name]]
    refs=[s for i in current['items']for d in i['dependencies']+i['setDependencies']for s in d['skills']]
    known=[s for s in refs if s['talentId'] in mechanical];assert len(known)==305 and all(s['implemented']for s in known)
    assert all(s['directGrantQualified']==(s['talentId'] in direct)for s in refs)
    hell=next(i for i in current['items']if i['id']=='unique/254');assert len(hell['dependencies'])==1
    dep=hell['dependencies'][0];assert dep['code']=='att-skill' and dep['prerequisites']==['on-attack proc dispatcher']
    assert dep['skills']==[dict(sourceParameter=47,sourceId=47,name='Fire Ball',sourceKey='Fire Ball',talentId='sorceress.fireball',implemented=True,directGrantQualified=False)]
    changed=subprocess.check_output(['git','diff',BASE,'--name-only','--','src/Sanctuary.Content','shared','tests/Sanctuary.Integration'],cwd=ROOT,text=True)
    assert not changed,'This reporting-only preflight must not change gameplay/shared/test C#'
    result=dict(component='Named skill return preflight and truthful prerequisite ledger',referenceVersion='3.3.93847',baselineCommit=BASE,mechanicalTalentRoutes=240,namedDirectGrantRegistry=16,classBackedItemReferences=305,items=176,sets=4,heldPartners=19,acquisitionGatesChanged=False,directGrantRegistryChanged=False,gameplayChanged=False,newBuilds=0,newNativeRuns=0,hellslayer=hell,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),goalComplete=False,limits='Mechanic implementation and item direct-grant readiness are distinct fields. Item delivery/trigger/charges/auras remain independent;no readiness bypass or whole Hellslayer proof is claimed. Native projection/automatic golem registration/player delivery remain required. This is a source/catalogue audit,not a new runtime execution result.')
    paths=[DATA,REPORT,*[ROOT/p for p in ('tools/record_named_skill_return_preflight.py','tools/build_named_skill_backlog.py','docs/item-skill-return-preflight.md','data/Sanctuary/Balance/named-active-skills.json','data/Sanctuary/Balance/class-skill-status.json','src/Sanctuary.Content/NamedSkills.cs','src/Sanctuary.Content/NamedGear.cs','src/Sanctuary.Content/NamedPropertyRuntime.cs','src/Sanctuary.Content/EquipmentPropertyWriter.cs','src/Sanctuary.Content/ItemSkillPropertyWriter.cs','src/Sanctuary.Content/NpcItemProcGraph.cs','src/Sanctuary.Content/IronGolemCasting.cs','src/Sanctuary.Content/IronGolemFireBall.cs','tools/reference/npc-item-fireball-native-evidence.json','tools/reference/consume-learned-client-evidence.json')]]
    e.write('named-skill-return-preflight.json',result,paths,check)
    print('Item return:240class routes versus16qualified direct grants;305references corrected,176item/4set/19partner inventory and runtime gates unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
