#!/usr/bin/env python3
"""Authenticate the prepared cross-Hex policy without enabling learned skills."""
import argparse,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/weapon-hex-groups';PARENT='434b91ea91dfda1bd2e6a4eb0d7db15cd30035f5'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_text()
    manifest=pin('tools/reference/d2r-named-equipment/sources.json');states=pin('tools/reference/d2r-named-equipment/json/states.json')
    assert e.sha(ROOT/'tools/reference/d2r-named-equipment/json/states.json')==manifest['files']['json/states.json']['sha256']
    groups={}
    for name,group in [('hexbane',6),('hexpurge',6),('hexsiphon',6),('hexbanedebuff',7),('hexpurgedebuff',7),('hexsiphondebuff',7)]:
        row=states[name];assert row['group']==group and not row.get('curse',0);groups[name]=group
    for cls in ['HexBane','HexPurge','HexSiphon']:
        name=f'src/Sanctuary.Content/{cls}.cs';old=subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT).decode();target='_targets'if cls=='HexPurge'else'_debuffs';ability='CombatAbility.'+cls
        anchor='    internal static void Prune()'
        expected=old.replace(anchor,f'    internal static void ClearBuff(Mobile p){{if(p!=null){{_buffs=_buffs.Remove(p.Serial.Value);}}}}\n    internal static void ClearDebuff(Mobile p){{if(p!=null){{{target}={target}.Remove(p.Serial.Value);}}}}\n'+anchor)
        expected=expected.replace('internal static bool CastPrepared(PlayerMobile p)','internal static bool CastPrepared(PlayerMobile p,bool replaceGroup=false)')
        expected=expected.replace(f'if(!WeaponHexes.CanCast(p)){{return false;}}WeaponHexes.GuardBuff(p,{ability});',f'if(!WeaponHexes.CanCast(p)){{return false;}}if(!replaceGroup){{WeaponHexes.GuardBuff(p,{ability});}}')
        expected=expected.replace('        if(!NecromancerEffects.Pay(p,values.Mana256)){return false;}','        if(!NecromancerEffects.Pay(p,values.Mana256)){return false;}'+f'\n        if(replaceGroup){{WeaponHexGroups.ReplaceBuff(p,{ability});}}')
        expected=expected.replace('bool eligible,DateTime at)','bool eligible,DateTime at,bool replaceGroup=false)')
        expected=expected.replace(f'WeaponHexes.GuardTarget(target,{ability});',f'if(!replaceGroup){{WeaponHexes.GuardTarget(target,{ability});}}')
        store=f'{target}={target}.SetItem(next.Target,next);';expected=expected.replace(store,f'if(replaceGroup){{WeaponHexGroups.ReplaceTarget(target,{ability});}}'+store)
        assert pin(name)==expected,name
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    before=pin(STAGE/'source-snapshot-1.json')['inputs'];after=pin(STAGE/'source-snapshot-2.json')['inputs']
    assert before['projectInputs']==after['projectInputs'] and before['copiedData']==after['copiedData']
    assert {n for n in before['sourceInputs']if before['sourceInputs'][n]!=after['sourceInputs'][n]}=={'tests/Sanctuary.Integration/WeaponHexGroupChecks.cs'}
    for name,digest in born['sourceInputs'].items():assert e.sha(ROOT/name)==digest,name
    for kind,number in [('content',1),('integration',2)]:
        log=pin(STAGE/f'{kind}-build-{number}.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
    failed=pin(STAGE/'integration-build-1.log');assert 'error CS1061' in failed and '1 Error(s)' in failed;pin(STAGE/'WeaponHexGroupChecks-build1.cs')
    parent=pin('tools/reference/blade-warp-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'hex-group-check':1,'hex-group-reload-check':2})
    log=e.cohort(STAGE,expected,12);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    assert log.count('Hex group candidate:9buff transitions,6cross-family target transitions')==6
    assert log.count('Hex group original:Warlock/Witch independent owner/target Hex family')==3
    fixture=pin(STAGE/'hex-group-fixtures.json');assert fixture['Generation']==2 and [r['Female']for r in fixture['Rows']]==[False,True]
    source=pin('src/Sanctuary.Content/WeaponHexGroups.cs');assert 'Approved=>false;'in source
    report=pin('data/Sanctuary/Balance/class-skill-status.json');rows=report['skills'];assert sum(r['implementation']=='handler-present'for r in rows)==215
    assert all(next(r for r in rows if r['talentId']==key)['implementation']=='blocked'for key in ['warlock.hexbane','warlock.hexpurge','warlock.hexsiphon'])
    protected=e.protection('tools/reference/psychic-ward-cast-client-evidence.json',43)
    client=pin('tools/reference/psychic-ward-cast-client-evidence.json');pin(client['inputManifest']['path'])
    for name,digest in client['files'].items():assert e.sha(ROOT/name)==digest,name
    for name in ['tests/Sanctuary.Integration/WeaponHexGroupChecks.cs','tests/check_weapon_hex_groups.py','tools/record_weapon_hex_groups.py','docs/weapon-hex-groups-proposal.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md']:pin(name)
    result=dict(referenceVersion='3.3.93847',proposal=True,approved=False,currentGroupParity=False,learnedEnabled=False,handlers=215,warlockHandlers=6,completedClasses=[],groups=groups,nativeMarkers=12,markers=dict(expected),boots=3,reloads=2,saves=3,savedOriginalOwners=2,buffTransitionsPerExercise=9,targetTransitionsPerExercise=6,exercises=6,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=1,nativeRebuilt=False,clientRebuilt=False,newTimers=0,newSavedFormats=0,newReferenceReadBytes=0,oldLargeOfflineMatricesRerun=0,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Explicit proposed group replacement awaits approval;all default guarded calls retain prior behavior.','No automatic melee/missile/hextrigger delivery,learned input,full current callback parity or class completion.','Same-Hex rank behavior retains existing helpers;Purge charge/reapplication and Bane magic mastery remain separate.','Prepared group candidate adds no effects/icons;existing separate placeholders remain.'],inputs=inputs)
    path=ROOT/'tools/reference/weapon-hex-groups-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Hex group candidate:12markers/3boots/2reloads,9buff+6target transitions per exercise;215handlers unchanged;approval/public integration pending.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
