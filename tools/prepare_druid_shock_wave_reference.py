#!/usr/bin/env python3
"""Pin Shock Wave's shared fan, physical spell and stun routes without runtime changes."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    previous=ROOT/'tools/reference/creeper-preflight.json';pin=json.loads(previous.read_text())
    public_root=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((public_root/'sources.json').read_text())
    assert pin['referenceVersion']==manifest['mechanicsReferenceVersion']=='3.3.93847'
    rows={};tables={};compared=0
    for table,key,name in [('skills','skill','Shock Wave'),('missiles','Missile','shockwave'),('states','state','bear')]:
        table_pin=pin['localTables'][table];local=ROOT/'.local/research/d2r-3.3.93847'/table_pin['localFilename']
        assert sha(local)==table_pin['sha256']
        primary=list(csv.DictReader(local.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t'))
        public=public_root/'json'/(table+'.json');assert sha(public)==manifest['files']['json/'+table+'.json']['sha256']
        row=next(x for x in primary if x.get(key)==name);other=next(x for x in json.loads(public.read_text()).values() if x.get(key)==name)
        for field,value in other.items():
            if field not in row or field=='lineNumber' or field.startswith('*') and field not in ['*Id','*ID']:continue
            text=str(value)
            if table=='skills' and text.startswith('"') and text.endswith('"'):text=text[1:-1]
            assert row[field]==text,(table,field,row[field],text);compared+=1
        rows[table]={name:{field:value for field,value in row.items() if field and value!=''}}
        tables[table]=dict(localFilename=table_pin['localFilename'],sha256=sha(local),publicSha256=sha(public))
    skill=rows['skills']['Shock Wave'];missile=rows['missiles']['shockwave']
    assert [skill[k] for k in ['*Id','srvdofunc','srvmissilea','srvmissileb','anim','seqtrans','UseAttackRate','mana','restrict','State1','calc1','calc2','calc3','calc4','HitShift','MinDam','MaxDam']]==['243','8','shockwave','shockwave','SC','SC','1','7','2','bear','5','0','0','ln12','8','10','20']
    assert not any(skill.get(k) for k in ['srvstfunc','SrcDam','EType','State2'])
    assert skill['DmgSymPerCalc']=="skill('Maul'.blvl)*par8" and [skill[k] for k in ['Param1','Param2','Param8']]==['40','15','10']
    assert [missile[k] for k in ['*ID','pSrvDoFunc','pSrvDmgFunc','dParam1','Vel','MaxVel','Range','Activate','CollideType','LastCollide','NextHit','NextDelay','Size','CanSlow','ReturnFire','GetHit','Skill','ResultFlags','HitClass']]==['511','1','7','0','20','20','14','0','3','1','1','4','1','1','1','1','Shock Wave','4','64']
    assert not any(missile.get(k) for k in ['pSrvHitFunc','SrcDamage','SrcMissDmg','ToHit','CollideKill','Pierce'])
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==pin['legacyCommit'];hashes={};routines={}
    for relative,names in [
        ('source/D2Game/src/SKILLS/SkillAma.cpp',['SKILLS_SrvDo008_MultipleShot_Teeth_ShockWave']),
        ('source/D2Game/src/SKILLS/Skills.cpp',['sub_6FD140D0','sub_6FD14120','D2GAME_SKILLS_Handler_6FD12BA0']),
        ('source/D2Common/src/D2Skills.cpp',['SKILLS_CalculateDamageBonusByLevel','SKILLS_GetMinPhysDamage','SKILLS_GetMaxPhysDamage','SKILLS_GetManaCosts']),
        ('source/D2Common/src/Units/Missile.cpp',['MISSILE_CalculateDamageData','MISSILE_SetDamageStats']),
        ('source/D2Game/src/MISSILES/Missiles.cpp',['MISSILES_CreateMissileFromParams']),
        ('source/D2Game/src/MISSILES/MissMode.cpp',['MISSMODE_SrvDo01_BasicMissile','MISSMODE_HandleMissileCollision','MISSMODE_SrvDmg07_Warcry_ShockWave','MISSMODE_SrvDmgHitHandler','MISSMODE_SetDamageFlags']),
        ('source/D2Game/src/UNIT/SUnitDmg.cpp',['SUNITDMG_ApplyBlockOrDodge','SUNITDMG_ExecuteEvents']),
    ]:
        path=BASE/'d2moo-pinned-source'/relative;raw=path.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=sha(path)
        for name in names:routines[name]=hashlib.sha256(function(raw.decode(),name).encode()).hexdigest()
    forms_path=ROOT/'tools/reference/druid-forms-preflight.json';forms=json.loads(forms_path.read_text())
    animation=ROOT/'.local/research/d2r-3.3.93847/AnimData.d2';assert sha(animation)==forms['animation']['sha256']
    cast=next(x for x in forms['animation']['records'] if x['code']=='TGSCHTH');assert cast==dict(code='TGSCHTH',frames=10,speed=152,events=[[5,2]])
    reused=['FanGeometry.cs','BarbarianWarCry.cs','CombatStuns.cs','SourceMissileSteps.cs','WeaponMissiles.cs','DruidFormCastClock.cs','ShapeRestrictions.cs','SkillCombat.cs']
    result=dict(referenceVersion='3.3.93847',sourcePreparation=True,handlerEnabled=False,nativeAccepted=False,executedContexts=0,currentExecutableParity=False,
        legacyCommit=tree['sha'],publicCommit=manifest['commit'],primaryPinSha256=sha(previous),localTables=tables,currentRows=rows,publicFieldsCompared=compared,
        animation=dict(sha256=sha(animation),bearSC=cast,formPreflightSha256=sha(forms_path)),sourceHashes=hashes,completeRoutineHashes=routines,
        reuseCommit='9ea1312',reuseCandidates={name:hashlib.sha256(subprocess.check_output(['git','show','9ea1312:src/Sanctuary.Content/'+name],cwd=ROOT)).hexdigest() for name in reused},
        established=[
            'Current skill243 is bear-only SC,7mana,no start routine and generic server do8. Both missile selections are511. Fixed calc1=5,central calc3=0 and activation calc2=0 reuse the existing Teeth/Multiple Shot source fan; cursor distance affects aim separation. Do not invent a fixed-angle cone or substitute a radial War Cry pulse.',
            'Shockwave511 is a travelling size1 physical spell with velocity20/range14,source-zero weapon collection,LastCollide1,NextHit1/NextDelay4,ReturnFire/GetHit1 and no ToHit/CollideKill/Pierce. Row ResultFlags4/HitClass64 and callback7 must remain distinct from the ordinary weapon pipeline. Full factory75percent/slow/path/activation and generic end/contact order need composed acceptance.',
            'Physical endpoints10..20 use tiers3/5/7/7/7 and hard Maul10percent per point. The shared physical getter truncates synergy before HitShift8; no weapon/critical/leech/Enchant or Werebear/Maul-charge weapon ED is implied by source0. Damage-versus-monster-type stat transport needs a separate explicit scope.',
            'Current dParam1=0 uses skill calc4=ln12 in the description. The older callback directly uses skill Param1/2,which currently yields the same40+15per-rank raw stun frames,then sets hit-class0x60. Shared execute-events stun caps,unique chance,boss/stationary/hireling eligibility and block/dodge still need native path verification. Physical immunity alone must not be assumed to suppress the source stun event.',
            'Pinned bear SC is10frames,speed152,action event5. Reuse the existing form cast clock only after checking current SC/UseAttackRate composition and interruption. The already accepted bear concentration channel protects active cast/tail without preventing health damage; exact current dispatch remains separately bounded.',
        ],remaining=[
            'Compose actual shared fan/factory/physical-stat writer,callback7and missile damage router with current row flags before enabling a handler. Pin modern NextHit identity across rays/casts/suppliers; do not silently reuse a cast-only or legacy global justhit gate as exact modern behavior.',
            'Then connect saved travelling waves,source-zero physical mitigation,blocking/stun/return/hurt behavior,7mana after-do transaction,real empty-ground/target input,bear casting/interruption/cancellation and actual save/reload. Item-only/foreign/PvP/unmapped targets and exact modern world/seed/event/geometry remain explicit limits.',
            'Final fan-wave sprite/directional frames,shockwave travel/cast/contact audio and stunned overlay are missing. Current source CelFile expansion\\shockwave is asset provenance,not an installed asset. Talent and left/right/quick icons remain separate placeholders. No runtime source/data or installed build is changed by this preparation.',
        ])
    path=ROOT/'tools/reference/druid-shock-wave-preflight.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print(f'Shock Wave preflight:{compared}current fields,{len(routines)}complete routine hashes,shared fan/War Cry candidates; no native handler.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
