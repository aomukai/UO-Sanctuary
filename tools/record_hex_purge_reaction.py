#!/usr/bin/env python3
"""Authenticate an unapproved Purge reaction candidate and its saved stream."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/purge-reaction-proposal'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_text()
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];final=pin(STAGE/'source-snapshot-3.json')['inputs']
    assert first['projectInputs']==final['projectInputs']
    assert {n for n in first['sourceInputs']if first['sourceInputs'][n]!=final['sourceInputs'][n]}=={'tests/Sanctuary.Integration/HexPurgeReactionChecks.cs'}
    assert {n for n in first['copiedData']if first['copiedData'][n]!=final['copiedData'][n]}=={'Data/Sanctuary/Balance/skill-effects.json'}
    for kind,number in [('content',1),('integration',3)]:
        log=pin(STAGE/f'{kind}-build-{number}.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'purge-reaction-check':1,'purge-reaction-reload-check':2,'skill-damage-event-check':1,'skill-damage-event-reload-check':2})
    log=e.cohort(STAGE,expected,15);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    assert log.count('Purge candidate:chance77/78,cap100,charge claim/deplete/refresh/rank competition')==3
    assert log.count('Damage event bridge:fractional/carry/native AOS/immune/direct fire/field/aura/return/self-cost/suppression/nested/lethal controls pass')==3
    fixture=pin(STAGE/'purge-reaction-fixtures.json');assert fixture['Generation']==2 and fixture['Life256']%256==127
    r=fixture['Mark']['Reaction'];assert r['RemainingCharges']==2 and r['MagicMastery']==50 and r['Modifiers']==dict(Potion=25,FireMastery=10,ColdMastery=20,LightningMastery=30,PoisonMastery=40)
    assert pin(STAGE/'skill-damage-event-fixtures.json')['Generation']==2
    prior=pin(STAGE/'native-1/birth-native-checks.log');assert prior.count('purge-reaction-reload-check: passed')==2 and ': failed'not in prior
    for name in ['birth-inputs.json','native-run-1.log','purge-reaction-fixtures.json','HexPurgeReactionChecks.cs']:pin(STAGE/'native-1'/name)
    parent=pin('tools/reference/skill-damage-events-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    rows=pin('data/Sanctuary/Balance/class-skill-status.json')['skills'];assert sum(r['implementation']=='handler-present'for r in rows)==217
    assert sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in rows)==8
    assert next(r for r in rows if r['talentId']=='warlock.hexpurge')['implementation']=='blocked'
    source=pin('src/Sanctuary.Content/HexPurgeReactionProposal.cs');assert 'Approved=>false'in source and 'Configure('not in source and 'BeforeDirectDamage+='not in source
    hp=pin('src/Sanctuary.Content/HexPurge.cs');assert 'WhenWritingNull'in hp and 'base("SanctuaryHexPurge",185)'in hp and 'writer.Write(0)'in hp
    assert not [p for p in (ROOT/'src/Sanctuary.Content').glob('*.cs')if 'BeforeDirectDamage+='in p.read_text()]
    for name in ['src/Sanctuary.Content/SkillCombat.cs','src/Sanctuary.Content/SkillDamageEvents.cs','tests/Sanctuary.Integration/HexPurgeReactionChecks.cs','tests/check_hex_purge_reaction.py','tools/record_hex_purge_reaction.py','docs/hex-purge-reaction-proposal.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json','tools/reference/hex-purge-source-evidence.json']:pin(name)
    result=dict(referenceVersion='3.3.93847',approved=False,learnedEnabled=False,automaticHooksEnabled=False,currentCallbackParity=False,handlers=217,warlockHandlers=8,completedClasses=[],nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=3,sourceSnapshot=3,nativeRun=2,savedRemainingCharges=2,savedMastery=50,oldSchemaJson=True,atomicInvalidRestore=True,newPersistenceIds=0,persistenceVersionChanged=False,newTimers=0,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Unapproved adaptation for protected current callbacks36/39;learned/automatic routes remain disabled.','Explicit supplied registrations/modifiers and scoped fixture listener;not authenticated player input or live mastery-gear acquisition.','Global D2R RNG ordering,special receivers,PvP and graphical acceptance remain unqualified.'],inputs=inputs)
    path=ROOT/'tools/reference/hex-purge-reaction-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Purge candidate:15markers/3boots/2reloads;actual spent charges/RNG/modifiers and wounds persist;217handlers,no learned/automatic admission;installed files preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
