#!/usr/bin/env python3
"""Bind Impale pre-wear physical,post-wear allocation and ordered release."""
import argparse,hashlib,json
from review_weapon_event_order import ROOT,STAGE,function

def record(check=False):
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';records={};bodies={}
    files={'source/D2Game/src/SKILLS/SkillAma.cpp':['SKILLS_SrvSt07_Impale'],'source/D2Game/src/SKILLS/Skills.cpp':['SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws'],'source/D2Game/src/UNIT/SUnitDmg.cpp':['SUNITDMG_FillDamageValues','SUNITDMG_AllocCombat','SUNITDMG_DrainItemDurability'],'source/D2Game/src/ITEMS/Items.cpp':['ITEMS_UpdateDurability']}
    for path,names in files.items():
        raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==path)
        for name in names:
            body=function(raw.decode(),name);bodies[name]=body;records[name]=dict(path=path,sha256=hashlib.sha256(body.encode()).hexdigest())
    start=bodies['SKILLS_SrvSt07_Impale'];anchors=['damage.dwPhysDamage =','damage.dwHitFlags =','ITEMS_HasDurability(','SUNITDMG_AllocCombat('];assert [start.index(x)for x in anchors]==sorted(start.index(x)for x in anchors)
    assert 'if (!(pDamage->dwHitFlags & DAMAGEHITFLAG_1))'in bodies['SUNITDMG_FillDamageValues']
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],functions=records,scope='Complete older start7/fill/drain and existing current/approved controller;not modern executable parity.',established=['Precompute physical before special wear;hitflag1 skips later physical/species-ED/critical recomputation. Allocation fills other weapon lanes after wear.','The existing current forced-success/uninterruptible/slow corrections remain. Paid shield/avoidance is unverified and is not changed or claimed here.','Capture post-wear poison rate/length,drain,cold and mitigation at startup;release through existing ordered damage/attack/wear/return services without rereading the packet.','Keep special wear at startup and separate ordinary expansion wear before attack/return;own break/last-quantity commitment and narrow range remain.','Do2 target slow precedes the drain range check;the existing no-mana ordinary fallback has no skill slow or special wear.'],remaining=['Paid modern shield/avoidance and exact current precomputed damage,critical,species modifiers,break graph,RNG,timing,owner/client/recipient/PvP remain unverified.','Captured lightning thrusts and other item/missile/incoming/full named-admission paths remain separate. No new named item or class certification.'])
    p=ROOT/'tools/reference/impale-contact-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Impale complete pre-wear/fill/drain bodies bound;paid block/avoidance remains explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
