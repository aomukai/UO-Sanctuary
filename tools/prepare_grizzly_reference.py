#!/usr/bin/env python3
"""Corroborate Grizzly/BearSmite data and reviewed source without enabling a handler."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def build(local_root,installation,check=False):
    beasts=json.loads((ROOT/'tools/reference/druid-beasts-preflight.json').read_text())
    assert sha(installation/'D2R.exe')==beasts['executableSha256']
    assert sha(installation/'.build.info')==beasts['buildInfoSha256']
    public=ROOT/'tools/reference/d2r-named-equipment'
    manifest=json.loads((public/'sources.json').read_text())
    rows,pins,compared={}, {},0
    for table,filename,key,names in [
        ('skills','skills.txt','skill',['Summon Grizzly','BearSmite']),
        ('monstats','monstats-sentries-verified.txt','Id',['druidbear']),
        ('monstats2','monstats2-sentries-verified.txt','Id',['druidbear'])]:
        local=list(csv.DictReader((local_root/filename).read_text(encoding='utf-8-sig').splitlines(),delimiter='\t'))
        path=public/'json'/(table+'.json');assert sha(path)==manifest['files']['json/'+table+'.json']['sha256']
        cached=json.loads(path.read_text());rows[table]={}
        for name in names:
            actual=next(r for r in local if r.get(key)==name);prior=next(r for r in cached.values() if r.get(key)==name)
            rows[table][name]={k:v for k,v in actual.items() if k and v!=''}
            for field,value in prior.items():
                if field not in actual or field=='lineNumber':continue
                value=str(value)
                if table=='skills' and value.startswith('"') and value.endswith('"'):value=value[1:-1]
                assert value==actual[field],(table,name,field,value,actual[field]);compared+=1
        pins[table]=dict(filename=filename,sha256=sha(local_root/filename),publicSha256=sha(path))
    monster=rows['monstats']['druidbear'];assert [monster[k] for k in ['noRatio','minHP','MinHP(N)','MinHP(H)','AC','A1TH','DamageRegen','Skill1','Sk1mode','Sk1lvl']]==['1','750','650','650','245','300','5','BearSmite','seq_bearsmite','1']
    summon=rows['skills']['Summon Grizzly'];assert [summon[k] for k in ['srvdofunc','summon','pettype','petmax','mana','calc2']]==['119','druidbear','grizzly','1','40','ulvl']
    smite=rows['skills']['BearSmite'];assert [smite[k] for k in ['*Id','srvstfunc','srvdofunc','SrcDam']]==['326','32','2','128']
    seqpath=local_root/'monseq.txt';sequence=[r for r in csv.DictReader(seqpath.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t') if r.get('sequence')=='seq_bearsmite']
    assert len(sequence)==17 and all(r['mode']=='A2' for r in sequence)
    assert [int(r['frame']) for r in sequence]==list(range(17))
    animations=beasts['animation'];assert sha(local_root/'AnimData.d2')==animations['sha256']
    bear_animation=[r for r in animations['records'] if r['code'].startswith('B7')];assert bear_animation
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==beasts['legacyCommit']
    hashes,routines={},{}
    for relative,names in [
        ('source/D2Game/src/AI/AiThink.cpp',['AITHINK_Fn112_DruidBear']),
        ('source/D2Game/src/SKILLS/SkillDruid.cpp',['SKILLS_SrvDo119_DruidSummon']),
        ('source/D2Game/src/SKILLS/SkillBar.cpp',['SKILLS_SrvSt32_Conversion_Bash_Stun_Concentrate_BearSmite']),
        ('source/D2Game/src/SKILLS/Skills.cpp',['SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws','sub_6FD14D20'])]:
        raw=(BASE/'d2moo-pinned-source'/relative).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==relative)
        hashes[relative]=hashlib.sha256(raw).hexdigest()
        for name in names:routines[name]=hashlib.sha256(function(raw.decode(),name).encode()).hexdigest()
    report=ROOT/'tools/reference/grizzly-tooltip-user-report.json';observed=json.loads(report.read_text())
    assert observed['observed']['life']==5200 and observed['resolution']['nightmareBaseLife']==650
    result=dict(referenceVersion='3.3.93847',handlerEnabled=False,installed=False,scope='Grizzly source preparation; no native body/AI/sequence/handler accepted',
        executableSha256=beasts['executableSha256'],buildInfoSha256=beasts['buildInfoSha256'],primaryDataCommit=manifest['commit'],legacyCommit=tree['sha'],
        localTables=pins,currentRows=rows,corroboratedFields=compared,sequence=dict(filename='monseq.txt',sha256=sha(seqpath),rows=sequence),
        animation=dict(sha256=animations['sha256'],records=bear_animation),sourceHashes=hashes,reviewedRoutineHashes=routines,userReportSha256=sha(report),
        established=[
            'Player247uses right-only town-enabled SC/do119,40mana,cap1and independent grizzly pet type. Birth uses750Normal or650Nightmare/Hell,245defense,300A1AR and regeneration factor5; difficulty is not an interchangeable constant.',
            'User effective28/base20tooltip AR700/defense280,damage295percent and1959..2077match current effective Spirit synergy and raw496..526physical endpoints. Prior Dire28 plus own28give700percent life bonus;650*8=5200corroborates Nightmare/Hell. User current difficulty was not separately stated. No balance deviation is inferred.',
            'BearSmite326uses start32/do2 and SrcDam128with a17-entry seq_bearsmite/A2sequence. Complete shared start prepares ordinary hit flags/weapon packet and adds calc2physical after combat allocation; do2handles overlays/target/self states and calls the shared DrainItemDurability action wrapper. Do not substitute Paladin shield Smite or a plain A1bite.',
            'DruidBear AI112has its own owner-distance branches, fallback visibility,15stall,40run chance,50skill-versus-A1chance and sequence requests. Shared pet movement and prior Bash/start32/source sequence primitives are reuse candidates,not accepted substitutes for this distinct composition.'],
        remaining=[
            'Compile whole birth/passive/live-mode and AI112/BearSmite sequence/combat compositions. Native saved body,physical hit/knockback,timing,capacity/coexistence,casting/payment/input and persistence remain unfinished.',
            'Preserve existing source difficulty helper capitalization and approved owner-facet/proportional-life policy. Effective wolf/bear synergies must not be converted to hard-only ranks. Native equipment/sunder and exact current room/path/mode/event behavior remain separate.',
            'Track final B7body/emergence/walk/run/A1/A2sequence/contact/knockback/hurt/death/audio separately from talent/right/quick icons. No new completed class or current-executable certificate is claimed.'])
    path=ROOT/'tools/reference/grizzly-preflight.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print(f'Grizzly preflight:{compared}current fields,{len(sequence)}sequence rows,{len(bear_animation)}animations,{len(routines)}reviewed routines; no handler enabled.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--local-root',type=Path,required=True);p.add_argument('--installation',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();build(a.local_root,a.installation,a.check)
