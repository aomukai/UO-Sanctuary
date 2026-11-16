#!/usr/bin/env python3
"""Bind complete older Jab/Fend allocation,drain and durability functions."""
import argparse,hashlib,json
from review_weapon_event_order import ROOT,STAGE,function

def record(check=False):
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';records={};bodies={}
    paths={'source/D2Game/src/SKILLS/SkillAma.cpp':['SKILLS_SrvSt05_Jab','SKILLS_SrvSt09_Fend','SKILLS_SrvDo007_Jab','SKILLS_SrvDo013_Fend_Zeal_Fury'], 'source/D2Game/src/UNIT/SUnitDmg.cpp':['SUNITDMG_AllocCombat','SUNITDMG_DrainItemDurability'], 'source/D2Game/src/ITEMS/Items.cpp':['ITEMS_UpdateDurability']}
    for path,names in paths.items():
        raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==path)
        for name in names:
            body=function(raw.decode(),name);bodies[name]=body;records[name]=dict(path=path,sha256=hashlib.sha256(body.encode()).hexdigest())
    for name in ('SKILLS_SrvDo007_Jab','SKILLS_SrvDo013_Fend_Zeal_Fury'):
        b=bodies[name];a=['SUNITDMG_GetResultFlags(','damage.dwEnDmgPct =','SUNITDMG_AllocCombat(','SUNITDMG_DrainItemDurability('];assert [b.index(s)for s in a]==sorted(b.index(s)for s in a)
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],functions=records,scope='Complete pinned older bodies plus existing approved/native sequence controllers;not current executable parity.',established=['Jab do7 and Fend do13 allocate ordinary source128 weapon contacts anew at each strike,with current skill AR and calc1/calc2 physical ED respectively.','Jab retains its original target;Fend retains its existing count,cyclic selection and rollback controls. Neither repeats mana at each contact.','Successful contacts use source shield/allocation and initial poison rate/clock,ordered damage/attack callbacks and wear/return. Misses and blocks retain attempts without successful damage.','The existing expansion durability writer remains4percent for ordinary weapons and10percent for throwable weapons,with quantity/reset on depletion. Invoke it once at the existing presentation/wear phase;do not add native weapon wear.','The approved basic fallback retains no skill AR/ED and a single contact.'],remaining=['Exact modern sequence/RNG/stat/geometry/owner and exceptional recipient/PvP equivalence remain unverified.','Impale and captured lightning thrusts,other item/missile/incoming paths and full named-item admission remain separate. No new item or class certification follows.'])
    p=ROOT/'tools/reference/amazon-sequence-contact-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Complete Jab/Fend allocation/drain/wear bodies bound;existing sequence and modern limits retained.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
