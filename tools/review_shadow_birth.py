#!/usr/bin/env python3
"""Bind prepared Shadow birth to the captured helper and immutable source cases."""
import argparse,json
from pathlib import Path
import skill_checkpoint_evidence as e
ROOT=e.ROOT
def review(check=False):
    inputs={}
    def pin(p):inputs[str(p.relative_to(ROOT))]=e.sha(p);return p.read_bytes()
    path=ROOT/'tools/reference/shadow-current-birth-source-evidence.json';source=json.loads(pin(path))
    assert source['sourceContexts']==3456 and source['ownedCompiledStatSlots']==8580 and source['helperBytes']==872 and source['completeUnwindFragments']==4
    assert not source['dispatchIdentityVerified'] and not source['handlerEnabled']
    for name,h in source['inputs'].items():
        if name.endswith(('.bin','.asm'))or name in ['.local/class-skills/shadow-current-birth-cases.json','tools/probe_shadow_current_birth.py','tools/probe_generic_pet_decisions.py','tools/probe_skill_do_dispatch.py']:
            assert e.sha(ROOT/name)==h,name;pin(ROOT/name)
    rows=json.loads(pin(ROOT/'data/Sanctuary/Balance/class-skills.json'))
    selected=[r for r in rows if r['talentId']in ['assassin.shadowwarrior','assassin.shadowmaster']]
    assert len(selected)==2 and all(r['sourceFields']['srvdofunc']==49 and r['sourceFields']['Param1']==15 and r['sourceFields']['sumumod']==42 for r in selected)
    for name in ['ShadowCreation.cs','SourcePercentage.cs','EquipmentStatValues.cs','SkillMechanics.cs','ShadowEquipment.cs']:pin(ROOT/'src/Sanctuary.Content'/name)
    pin(ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json');pin(ROOT/'tests/Sanctuary.Integration/ShadowBirthChecks.cs');pin(ROOT/'tests/check_shadow_birth.py');pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',parent='84ca34872e60905ba4519d88093ddb6e2f4f81e7',sourceContexts=3456,ownedCompiledStatSlots=8580,component='Prepared current Shadow birth/write order',preparedOnly=True,handlerEnabled=False,dispatchIdentityVerified=False,inputs=inputs,scope=['ShadowBirth composes the captured helper with existing SourcePercentage and explicit allocation,stat/formula and modifier services. ShadowCreation supplies canonical current skill fields and separate explicit owner/pet contexts;zero writes are retained.','Shared EquipmentStatValues now supplies stat names/count from the existing368row catalogue. No parallel summon stat-ID map,alternate percentage arithmetic,world allocation RNG or generic NPC conversion is added.','Prepared numeric snapshots are not live Shadows or stat-list post-write totals. Actual dispatcher,initialized actor/resources/equipment/AI/NPC skills,combat,casting/payment/input/lifecycle/saves and shared nonzero sunder remain separate acceptance work.'])
    encoded=json.dumps(result,indent=2)+'\n';out=ROOT/'tools/reference/shadow-birth-source-evidence.json'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Prepared Shadow birth:3456current-source contexts,8580owned slots;existing percentage/formula/stat catalogue;outer dispatch/actor still gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
