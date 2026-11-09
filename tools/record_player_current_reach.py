#!/usr/bin/env python3
"""Verify current class7reach correction and actual basic-attack boundaries."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/player-current-reach';PARENT='a341733cfdaf6a044bf58861b38ef7db55127ec8'
def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_bytes()
    source=pin(ROOT/'tools/reference/player-current-reach-source-evidence.json');assert source['sourceContexts']==60 and source['machineCodeBytes']==401 and source['classes'][7]=='Warlock'and not source['processMemoryRead']
    for name,h in source['artifacts'].items():assert e.sha(ROOT/name)==h,name
    for name in ['WarlockCombat.cs','CleavePrepared.cs','CleaveSweep.cs','SourceMeleeGeometry.cs','PsychicWardReactionProposal.cs','GenericPetStartupProposal.cs']:
        path='src/Sanctuary.Content/'+name;assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    for name in ['PsychicWardReactionProposal.cs','GenericPetStartupProposal.cs']:assert 'internal const bool Approved=false;'in (ROOT/'src/Sanctuary.Content'/name).read_text()
    path='src/Sanctuary.Content/PlayerMeleeGeometry.cs';before=subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT).decode()
    after=before.replace('        return ForCode(code);','        int sourceClass=ClassCreation.Profiles.TryGet(owner.Serial,out var profile)?(int)profile.Class-101:-1;\n        return SourcePlayerMeleeReach.Read(sourceClass,()=>new(true,4,ForCode(code)));')
    after=after.replace('    // Coarse native input/search bound only.','    internal static bool WarlockMelee(PlayerMobile owner)=>ClassCreation.Profiles.TryGet(owner.Serial,out var profile)&&profile.Class==DiabloClass.Warlock\n        &&CombatTiming.Equipped(owner) is{} weapon&&!ClassGear.Broken(weapon)&&PaladinVengeance.WeaponAllowed(owner)&&ClassGear.Base(weapon) is{Code:not null} basis&&Ranges.Value.ContainsKey(basis.Code);\n    // Coarse native input/search bound only.')
    after=after.replace("Within(PlayerMobile owner,BaseCreature target,int bonus)","Within(PlayerMobile owner,BaseCreature target,int bonus,int? preparedReach=null)").replace("        int reach=Reach(owner);","        int reach=preparedReach??Reach(owner);")
    assert pin(ROOT/path).decode()==after
    path='src/Sanctuary.Content/ActionTiming.cs';before=subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT).decode();needle='public static int Range(PlayerMobile from, CombatAbility ability) => '
    assert pin(ROOT/path).decode()==before.replace(needle,needle+'ability==CombatAbility.BasicAttack&&PlayerMeleeGeometry.WarlockMelee(from)?PlayerMeleeGeometry.SearchRange(from):').replace('    private static int WeaponRange(BaseWeapon weapon)','    internal static int WeaponRange(BaseWeapon weapon)')
    path='src/Sanctuary.Content/ClassCombat.cs';before=subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT).decode()
    old='    private static bool MeleeReach(PlayerMobile from, Mobile target) => target != null\n        && from.InRange(target, CombatTiming.Range(from, CombatAbility.BasicAttack)) && from.InLOS(target)\n        && Math.Abs(from.Z - target.Z) < 16;'
    new='    private static bool MeleeReach(PlayerMobile from, Mobile target) => target is BaseCreature creature&&PlayerMeleeGeometry.WarlockMelee(from)\n        ?PlayerMeleeGeometry.Within(from,creature,1)\n        :target != null&&from.InRange(target,PlayerMeleeGeometry.WarlockMelee(from)?CombatTiming.WeaponRange(CombatTiming.Equipped(from)):CombatTiming.Range(from,CombatAbility.BasicAttack))&&from.InLOS(target)&&Math.Abs(from.Z-target.Z)<16;'
    assert pin(ROOT/path).decode()==before.replace(old,new)
    snapshots=[pin(STAGE/f'source-snapshot-{n}.json')['inputs']for n in range(1,10)]
    assert snapshots[6]['projectInputs']==snapshots[8]['projectInputs']
    assert [n for n in snapshots[6]['sourceInputs']if snapshots[6]['sourceInputs'][n]!=snapshots[8]['sourceInputs'][n]]==['tests/Sanctuary.Integration/PlayerCurrentReachChecks.cs']
    assert [n for n in snapshots[6]['copiedData']if snapshots[6]['copiedData'][n]!=snapshots[8]['copiedData'][n]]==['Data/Sanctuary/Balance/class-skill-status.json']
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-9.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind,number in [('content',4),('integration',6)]:
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
        log=pin(STAGE/f'{kind}-build-{number}.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    failed=pin(STAGE/'diagnostics-1/birth-native-checks.log').decode();assert 'No native boundary distinguishes Warlock bonus from base weapon range'in failed
    pin(STAGE/'diagnostics-1/PlayerCurrentReachChecks.cs');pin(STAGE/'diagnostics-1/integration/Sanctuary.Integration.dll')
    for number in [2,3]:
        log=pin(STAGE/f'diagnostics-{number}/birth-native-checks.log').decode();assert 'player-current-reach' in log and ': failed' in log
        pin(STAGE/f'diagnostics-{number}/PlayerCurrentReachChecks.cs');pin(STAGE/f'diagnostics-{number}/integration/Sanctuary.Integration.dll')
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'player-current-reach-check':1,'player-current-reach-reload-check':2})
    log=e.cohort(STAGE,expected,12);assert log.count('Current player reach:60current contexts/401bytes')==3 and log.count('Current reach original:actual Warlock/Witch basic attack damages')==6
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-4.log');fixture=pin(STAGE/'player-current-reach-fixtures.json');assert fixture['Generation']==2 and len(fixture['Owners'])==2
    assert [o['Female']for o in fixture['Owners']]==[False,True]and all(o['Reach']==o['Base']+2 for o in fixture['Owners'])
    # No table edits or broad old source matrix rerun.
    path='data/Sanctuary/Balance/player-melee-ranges.json';raw=pin(ROOT/path);assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
    assert len(raw)==306 and max(row['Range']for row in raw)<254
    approval=pin(ROOT/'tools/reference/psychic-ward-reaction-approval.json');assert approval['approved'] and approval['approval']=='Approve this Psychic Ward rule'
    import hashlib
    assert hashlib.sha256(subprocess.check_output(['git','show',approval['candidateCommit']+':'+approval['proposal']],cwd=ROOT)).hexdigest()==approval['proposalAtApprovalSha256']
    protected=e.protection('tools/reference/ring-cast-client-evidence.json',43)
    for name in ['tools/skill_checkpoint_evidence.py','tools/reference/creeper-frame-installation-evidence.json','.local/class-skills/creeper-frame-installation.json','tools/reference/ring-cast-client-evidence.json']:
        value=pin(ROOT/name)
        if name.endswith('ring-cast-client-evidence.json'):pin(ROOT/value['inputManifest']['path'])
    for name in ['docs/player-current-reach-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_player_current_reach.py']:pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],runtimeCorrection=True,newLearnedSkill=False,sourceContexts=60,machineCodeBytes=401,newReferenceReadBytes=0,oldLargeOfflineMatricesRerun=0,
        nativeMarkers=12,markers=dict(expected),originalBasicAttackChecks=6,savedOriginalWeapons=2,boots=3,reloads=2,saves=3,contentBuild=4,integrationBuild=6,sourceSnapshot=9,nativeRun=4,newTimers=0,newSavedFormats=0,artifacts=born['artifacts'],preservation={**protected,'installed':False},
        verified=['Current player class7bonus2 and item byte reach agree in60captured-code/native contexts;owned compiled charstats identifies Warlock at index7,excluding TXT Expansion marker. Existing306item range table unchanged.',
            'Actual mapped ordinary Warlock/Witch basic attacks use corrected reach plus existing footprint/LOS predicate,with coarse search at entry. Six original native boundary tests damage beyond base reach;far releases remain harmless. Original equipped swords and bonus retain across two reloads.',
            'Classic Paladin adapter,bow range and previous unarmed/broken routes retain. Unmapped weapons avoid source-only adapter. Non-NPC recipients use old native release range. No new skill admission,damage/accuracy/proc policy or item definition changes.'],
        limits=['Current player inventory selection/broken/unarmed semantics and monster/composite reach not certified by supplied player-branch probe. Source range byte255can return257for class7;actual306weapons are below that edge and broader shared geometry bound remains explicit.',
            'Native mapped footprint/LOS reuses its existing qualified/provisional world scope;full current room ordering/PvP/unmapped geometry remains unverified. Cleave protected recipient/heading/start/input/payment remain unfinished. Ward rule approved but production bridge pending;normal startup still pending;no deployment or class certificate.'],inputs=inputs)
    path=ROOT/'tools/reference/player-current-reach-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Current Warlock reach:60source cases,6actual basic-attack boundaries,12markers/two reloads;new skills remain gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
