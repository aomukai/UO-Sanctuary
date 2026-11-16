#!/usr/bin/env python3
"""Bind Amazon lightning thrust startup allocation and ordered release."""
import argparse,hashlib,json
from review_weapon_event_order import ROOT,STAGE,function

def record(check=False):
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';records={};bodies={}
    files={'source/D2Game/src/SKILLS/SkillAma.cpp':['SKILLS_SrvSt06_PowerStrike_ChargedStrike','SKILLS_SrvSt10_LightningStrike','SKILLS_SrvDo011_ChargedStrike','SKILLS_SrvDo014_LightningStrike'],'source/D2Game/src/SKILLS/Skills.cpp':['SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws'],'source/D2Game/src/UNIT/SUnitDmg.cpp':['SUNITDMG_FillDamageValues','SUNITDMG_AllocCombat','SUNITDMG_DrainItemDurability'],'source/D2Game/src/ITEMS/Items.cpp':['ITEMS_UpdateDurability']}
    for path,names in files.items():
        raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==path)
        for name in names:
            body=function(raw.decode(),name);bodies[name]=body;records[name]=dict(path=path,sha256=hashlib.sha256(body.encode()).hexdigest())
    for name in ('SKILLS_SrvSt06_PowerStrike_ChargedStrike','SKILLS_SrvSt10_LightningStrike'):
        body=bodies[name];assert body.index('SUNITDMG_GetResultFlags(')<body.index('SUNITDMG_AllocCombat(')
    for name in ('SKILLS_SrvDo011_ChargedStrike','SKILLS_SrvDo014_LightningStrike'):
        body=bodies[name];assert body.index('SUNITDMG_DrainItemDurability(')<body.index('MISSILES_CreateMissileFromParams(')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],functions=records,scope='Complete older start6/start10/do11/do14/fill/allocation/drain and existing current/approved controller;not modern executable parity.',established=['Accuracy and shield resolve before successful allocation;PMH precedes shield and armor setup precedes fill. Preserve the released Power Strike AR correction.','Skill lightning rolls before weapon lanes. Capture weapon source poison initial rate and clock,drain,cold,critical and mitigation at startup. Skill lightning is already mastery-scaled and is not enhanced twice.','Release through existing ordered player damage/item/wear/attack/return services;once-only payment,original target,cancellation,range and ordinary low-mana fallback remain.','Charged Strike do11 and Lightning Strike do14 emit after the melee attempt,including miss,block or lethal contact. Approved Lightning Strike opening/solo adaptation remains unchanged.'],remaining=['Charged Strike calc1 physical ED overlap remains the existing older interpretation,not modern corroboration.','Exact modern startup/RNG/animation/payment/owner/client/PvP and exceptional recipient details remain unverified.','Other missile/incoming/full named-item admission and full class certification remain separate.'])
    p=ROOT/'tools/reference/amazon-thrust-contact-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Amazon thrust complete startup/do/fill/drain bodies bound;modern limits explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
