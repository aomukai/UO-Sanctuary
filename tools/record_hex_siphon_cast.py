#!/usr/bin/env python3
"""Authenticate learned Siphon,approved Hex groups and focused native input."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/hex-siphon-cast';PARENT='a7f70bcbde809275bfba4245da72c926d4d3f7ae'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_text()
    approval=pin('tools/reference/weapon-hex-groups-approval.json');assert approval['approved'] and approval['approval']=='Approve the shared Hex replacement rule'
    raw=subprocess.check_output(['git','show',approval['candidateCommit']+':'+approval['proposal']],cwd=ROOT);assert hashlib.sha256(raw).hexdigest()==approval['proposalAtApprovalSha256']
    # Existing formulas,stored record formats,kill returns and generic action
    # engine were reused rather than forked for this learned skill.
    for file in ['HexSiphon','HexBane','HexPurge','SkillMechanics','NamedCombat','NecromancerEffects','ClassCombat','SkillCombat']:
        name='src/Sanctuary.Content/'+file+'.cs';assert pin(name).encode()==subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT),name
    group=pin('src/Sanctuary.Content/WeaponHexGroups.cs');assert 'Approved=>true;'in group
    event=pin('src/Sanctuary.Content/HexSiphonEvents.cs');assert all(x in event for x in ['HexSiphon.ApplyPrepared(owner,npc,registered,true','replaceGroup:true','PlayerMeleeGeometry.Within','qualified&&Apply'])
    attempt=pin('src/Sanctuary.Content/PaladinThorns.cs');assert 'HexSiphonEvents.MeleeAttempt(attacker,defender);'in attempt
    contact=pin('src/Sanctuary.Content/PreparedWeaponEvents.cs');assert contact.index('HexSiphonEvents.MissileDamage')<contact.index('IronMaiden.MeleeDamage')
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    before=pin(STAGE/'source-snapshot-1.json')['inputs'];after=pin(STAGE/'source-snapshot-2.json')['inputs']
    assert before['sourceInputs']==after['sourceInputs'] and before['projectInputs']==after['projectInputs']
    assert {n for n in before['copiedData']if before['copiedData'][n]!=after['copiedData'][n]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for name,digest in born['sourceInputs'].items():assert e.sha(ROOT/name)==digest,name
    for kind in ['content','integration']:
        log=pin(STAGE/f'{kind}-build-1.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
    parent=pin('tools/reference/weapon-hex-groups-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'siphon-cast-check':1,'siphon-cast-reload-check':2,'hex-group-check':1,'hex-group-reload-check':2,**{f'siphon-cast-{mode}-{suffix}':3 for mode in ['right','quick']for suffix in ['check','result']}})
    log=e.cohort(STAGE,expected,27);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    for message,count in [('Siphon learned:self SC/right/quick',2),('Siphon events:native/class melee misses',6),('Siphon original:Warlock/Witch',3),('Hex group candidate:9buff transitions,6cross-family target transitions',6)]:assert log.count(message)==count,message
    fixture=pin(STAGE/'siphon-cast-fixtures.json');assert fixture['Generation']==2 and [r['Female']for r in fixture['Rows']]==[False,True]
    assert all(r['Buff']['Rank']==r['Debuff']['Rank']==2 for r in fixture['Rows'])
    groups=pin(STAGE/'hex-group-fixtures.json');assert groups['Generation']==2 and len(groups['Rows'])==2
    report=pin('data/Sanctuary/Balance/class-skill-status.json');rows=report['skills']
    assert sum(r['implementation']=='handler-present'for r in rows)==216
    assert sum(r['implementation']=='handler-present' and r['talentId'].startswith('warlock.')for r in rows)==7
    row=next(r for r in rows if r['talentId']=='warlock.hexsiphon');assert row['implementation']=='handler-present' and not row['complete']
    assert all(next(r for r in rows if r['talentId']==key)['implementation']=='blocked'for key in ['warlock.hexbane','warlock.hexpurge'])
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    client=pin('tools/reference/hex-siphon-cast-client-evidence.json');pin(client['inputManifest']['path'])
    for name,digest in client['files'].items():assert e.sha(ROOT/name)==digest,name
    for name in ['src/Sanctuary.Content/WarlockCombat.cs','src/Sanctuary.Content/WeaponHexes.cs','shared/ClassAbilities.cs','tests/Sanctuary.Integration/HexSiphonCastChecks.cs','tests/Sanctuary.Integration/WeaponHexGroupChecks.cs','tests/check_hex_siphon_cast.py','tools/record_hex_siphon_cast.py','tools/prepare_hex_siphon_client.py','tools/review_hex_siphon_client.py','tools/build_skill_roadmap.py','docs/hex-siphon-cast-checkpoint.md','docs/weapon-hex-groups-proposal.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md']:pin(name)
    result=dict(referenceVersion='3.3.93847',hexGroupsApproved=True,learnedEnabled=True,automaticMeleeMissileDelivery=True,currentEvent36Parity=False,handlers=216,warlockHandlers=7,completedClasses=[],nativeMarkers=27,markers=dict(expected),boots=3,reloads=2,saves=3,connectedInputs=6,savedOriginalOwners=2,registeredRanks=[2,2],contentBuild=1,integrationBuild=1,sourceSnapshot=2,nativeRun=1,nativeRebuilt=False,clientBuild=1,newTimers=0,newSavedFormats=0,newReferenceReadBytes=0,oldLargeOfflineMatricesRerun=0,optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Protected current event36sibling order,special overrides,exact registration/fully-mitigated-hit parity and advanced hextrigger producers remain unqualified.','Uses existing registered-rank recipient/state and common weapon event phases;not a recovered current callback body.','Full PvP,legacy overlapping manual Hex saves and actual graphical client interaction remain unqualified.','Bane mastery,Purge explosions/charges,normal demon startup and final effects/icons remain separate.'],inputs=inputs)
    path=ROOT/'tools/reference/hex-siphon-cast-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Siphon admitted:216handlers/Warlock7;approved shared Hex groups;27markers/6connected casts/2reloads;installed106/saves38/config20 preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
