#!/usr/bin/env python3
"""Freeze the tested Bane damage/timing candidate before learned integration."""
import argparse,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bane-damage-proposal';PARENT='26c2269fd96b344411e2d60cc7b33925872cd8f3'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_text()
    review=pin('tools/reference/hex-bane-damage-source-review.json')
    for name,h in review['inputs'].items():assert e.sha(ROOT/name)==h,name
    capture=pin(ROOT/'.local/class-skills/warlock-runtime-reference/bane-mastery-selector-20261109.json');assert not capture['allUnwindFragmentsCaptured'] and capture['bytesRead']==560
    for name,h in capture['artifacts'].items():assert e.sha(ROOT/name)==h,name
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-5.json',STAGE/'birth-inputs.json')
    for i in range(1,6):pin(STAGE/f'source-snapshot-{i}.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind,number in [('content',3),('integration',4)]:
        log=pin(STAGE/f'{kind}-build-{number}.log');assert all(x in log for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
    # Exact behavior-preserving extraction,including the original early zero
    # return before looking up actor mastery. All other damage code is intact.
    name='src/Sanctuary.Content/NamedElements.cs';old=subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT).decode()
    expected=old.replace('        return damage * Math.Max(0,100+MasteryPercent(source,element)) / 100.0;','''        return EnhanceAtMastery(damage,MasteryPercent(source,element));
    }
    internal static double EnhanceAtMastery(double damage,int mastery)
    {
        if (damage <= 0) { return 0; }
        return damage * Math.Max(0,100+mastery) / 100.0;''');assert pin(name)==expected
    for name in ['HexBane','WarlockCombat','HexSiphonEvents','WeaponHexGroups','GearEffects','ClassStats']:
        path='src/Sanctuary.Content/'+name+'.cs';assert pin(path).encode()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'bane-damage-proposal-check':1,'bane-damage-proposal-reload-check':2})
    log=e.cohort(STAGE,expected,12);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-4.log')
    assert log.count('Bane damage candidate:27focused')==3 and log.count('Bane damage candidate:actual post-roll miss debuff')==8
    fixture=pin(STAGE/'bane-damage-proposal-fixtures.json');assert fixture['Generation']==2 and [r['Female']for r in fixture['Owners']]==[False,True]
    for i in [1,2,3]:
        d=STAGE/f'diagnostics-{i}';log=pin(d/'birth-native-checks.log');assert 'bane-damage-proposal-check: failed'in log
        for name in ['birth-inputs.json',f'native-run-{i}.log','HexBaneDamageProposalChecks.cs']:pin(d/name)
    parent=pin('tools/reference/hex-siphon-cast-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    assert 'Approved=>false' in pin('src/Sanctuary.Content/HexBaneDamageProposal.cs')
    for name in ['tests/Sanctuary.Integration/HexBaneDamageProposalChecks.cs','tests/check_hex_bane_damage_proposal.py','tools/record_hex_bane_damage_proposal.py','tools/capture_bane_mastery_reference.py','docs/hex-bane-damage-proposal.md']:pin(name)
    result=dict(referenceVersion='3.3.93847',proposal=True,learnedEnabled=False,currentMagicMasteryParity=False,handlers=216,warlockHandlers=7,completedClasses=[],nativeMarkers=12,boots=3,reloads=2,saves=3,castCasesPerBoot=27,preparedContactChecks=8,contentBuild=3,integrationBuild=4,sourceSnapshot=5,nativeRun=4,newTimers=0,newSavedFormats=0,newReferenceReadBytes=560,currentSelectorCodeBytes=0,nativeRebuilt=False,clientRebuilt=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Supplied mastery percentages exercise shared arithmetic and prepared damage through actual weapon mitigation/HP;no live mastery-granting gear test. Entropy Locket stays gated by its unfinished hit-skill.','Explicit post-result receiver is prepared,not bound to automatic/public Bane casting. Current selector/callback remains unqualified.','First two diagnostics were incorrect test expectations;third was unavailable named gear. No item gate was bypassed.'],inputs=inputs)
    path=ROOT/'tools/reference/hex-bane-damage-proposal-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Bane candidate:12markers/2reloads,27cast cases per boot,8prepared contacts;mastery gear/public route remain unqualified;installed files preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
