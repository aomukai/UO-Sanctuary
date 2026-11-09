#!/usr/bin/env python3
"""Pin landed weapon integration; no approval or learned Purge is implied."""
import argparse,json,hashlib,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/weapon-landed'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_text()
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-1.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ['content','integration']:
        log=pin(STAGE/f'{kind}-build-1.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    families=['iron-golem-items','npc-resource-callbacks','weapon-landed','skill-damage-event']
    expected=Counter({'iron-golem-birth-check':3,**{n+'-check':1 for n in families},**{n+'-reload-check':2 for n in families}})
    log=e.cohort(STAGE,expected,15);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    for marker in ['Landed weapon bridge:actual Cleave/ranged transport','Damage event bridge:fractional/carry/native AOS','Ordered damage bridge:nested fractional damage/healing']:assert log.count(marker)==3
    saved=pin(STAGE/'weapon-landed-fixtures.json');assert saved['Generation']==2 and saved['Mark']['Reaction']['RemainingCharges']==1 and saved['Mark']['Reaction']['Seed']>0
    assert saved['Mark']['Target']==saved['Target']and saved['Mark']['Creator']==saved['Owner']
    assert pin(STAGE/'skill-damage-event-fixtures.json')['Generation']==2
    previous=pin('tools/reference/echoing-strike-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert born['artifacts'][name]==previous['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    rows=pin('data/Sanctuary/Balance/class-skill-status.json')['skills'];assert sum(r['implementation']=='handler-present'for r in rows)==218 and sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in rows)==9
    assert next(r for r in rows if r['talentId']=='warlock.hexpurge')['implementation']=='blocked'
    for token in ['BeforeDamage+=','BeforeDirectDamage+=']:assert not any(token in p.read_text()for p in (ROOT/'src/Sanctuary.Content').glob('*.cs'))
    proposal=pin('docs/hex-purge-reaction-proposal.md');assert hashlib.sha256(proposal.encode()).hexdigest()==hashlib.sha256(subprocess.check_output(['git','show','c5e729c95cc14aa8ed082b3de40fbeaaa2cad993:docs/hex-purge-reaction-proposal.md'],cwd=ROOT)).hexdigest()
    assert 'Approved=>false'in pin('src/Sanctuary.Content/HexPurgeReactionProposal.cs')
    for name in ['src/Sanctuary.Content/WeaponLandedEvents.cs','src/Sanctuary.Content/PlayerWeaponContact.cs','src/Sanctuary.Content/PreparedWeaponEvents.cs','tests/Sanctuary.Integration/WeaponLandedChecks.cs','tests/check_weapon_landed.py','tools/record_weapon_landed.py','docs/weapon-landed-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md']:pin(name)
    result=dict(referenceVersion='3.3.93847',component='landed weapon attachment boundary',handlers=218,warlockHandlers=9,completedClasses=[],purgeLearnedEnabled=False,productionSubscribers=0,nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Purge proposal remains unapproved;fixture-scoped attachment is not a learned skill or automatic production subscription.','Ranged check exercises existing damage transport,not launched projectile or authenticated input.','Saved spent-mark persistence uses fixture-held expiry;protected D2R callback order,advanced producers,PvP and graphics remain unqualified.'],inputs=inputs)
    path=ROOT/'tools/reference/weapon-landed-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Landed weapon bridge:15markers/3boots/2reloads;actual Cleave/prepared mark and ranged transport pass;218handlers,Purge gated;installed files preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
