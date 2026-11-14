#!/usr/bin/env python3
"""Authenticate the prepared complete Engorge action and recipient candidate."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/engorge-world'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-5.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    parent=pin('tools/reference/engorge-suppliers-native-evidence.json')
    previous=json.loads((ROOT/'.local/class-skills/engorge-suppliers/birth-inputs.json').read_text())
    current_cs={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')};old_cs={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')}
    new={'src/Sanctuary.Content/EngorgeEffects.cs','src/Sanctuary.Content/EngorgeCasting.cs','tests/Sanctuary.Integration/EngorgeWorldChecks.cs'}
    changed={'src/Sanctuary.Content/'+n+'.cs'for n in ('ClassCombat','CreatureProfiles','NamedElements','PreparedMonsterDamage','SourceAttackRate',
        'WarlockDemonRecovery','WarlockDefilerBody','WarlockGoatmanBody','WarlockGoatmanCasting','WarlockTaintedBody')}
    assert set(current_cs)-set(old_cs)==new and set(old_cs)-set(current_cs)==set()
    assert {n for n in old_cs if current_cs[n]!=old_cs[n]}==changed,'Unexpected preexisting source changed'
    assert current_cs['src/Sanctuary.Content/EngorgePrepared.cs']==old_cs['src/Sanctuary.Content/EngorgePrepared.cs']
    for kind,number in (('content',3),('integration',5)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    source=pin('tools/reference/engorge-source-review.json');assert source['skill']==379 and source['sourceCandidate']['readableCodeBytes']==0
    for name,h in source['inputs'].items():assert e.sha(ROOT/name)==h,name
    for number in (1,2,3,4):pin(STAGE/f'source-snapshot-{number}.json')
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'engorge-world-check':1,'engorge-world-check-result':1,
        'engorge-world-reload-check':2,'engorge-world-reload-check-result':2})
    text=e.cohort(STAGE,expected,15);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-3.log')
    assert text.count('Client: 127.0.0.1: Connected.')==3
    assert text.count('Prepared Engorge:actual Goatman ordered melee uses percentage WeaponLeech calculation,')==1
    assert text.count('Prepared Engorge:original optional body buffs,expiry,wounds,birth,RNG/hosts survive reload without offline recovery;')==2
    saved=pin(STAGE/'engorge-world-fixtures.json');assert saved['Generation']==2 and len(saved['Pets'])==3
    assert all(p['Effect']['Owner']==saved['Owner']and p['Effect']['Suppliers']['Rank']==20 and p['Effect']['Suppliers']['HardBloodOath']==8 for p in saved['Pets'])
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0]
    assert 'CombatAbility.Engorge'not in code
    assert 'internal static bool Approved=>false;'in(ROOT/'src/Sanctuary.Content/EngorgeEffects.cs').read_text()
    for name in (*sorted(new|changed),'tests/check_engorge_world.py','tools/record_engorge_world.py',
        'docs/engorge-world-rule-proposal.md','docs/engorge-world-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md',
        'docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    for archive in ('native-1','native-2'):
        pin(STAGE/archive/'birth-inputs.json');pin(STAGE/archive/'birth-native-checks.log')
    pin(STAGE/'integration-build-1.log');pin(STAGE/'compile-1/EngorgeWorldChecks.cs')
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Prepared complete Engorge corpse action and original saved recipient rule',handlers=226,warlockHandlers=17,completedClasses=[],
        learnedEngorgeEnabled=False,completeAdaptationPending=True,originalOptionalBodyBuffs=True,nativeMarkers=15,markers=dict(expected),
        repeatedSupplierMatrix=False,actualInputs=0,authenticatedSessions=3,boots=3,reloads=2,saves=3,originalBodies=3,witchGearControl=True,
        contentBuild=3,integrationBuild=5,sourceSnapshot=5,nativeRun=3,nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceIds=0,
        serializationVersionChanged=False,newLiveReferenceReads=0,artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Shared corpse access/reservation/protected loot and bounded ground targeting compose one unpaid SC windup,release-time capture/once-only payment,stop/movement/late corpse loss and no replay.',
            'Original three learned demon recipients receive exact FP8 healing and one captured optional timed payload;shared armor,resistance,IAS and raw source-frame recovery respect refresh/expiry without rewriting birth or creating another timer.',
            'Actual Witch/Tarnhelm19to20and hard Oath7to8change new captures only;existing buffs keep175armor/35rawregen. Invalid body state restoration is atomic.',
            'Actual Goatman ordered melee uses explicit percentage WeaponLeech adaptation,drain/difficulty and victim-life clipping,healing only its own body. Elemental,flagged,unsuccessful,periodic,expired controls give0;positive raw NPC drains refuse combination.',
            'Original three body buffs,deadlines,wounds,birth,RNG/controllers survive both parked reloads with no offline recovery and feed again after login.'],
        diagnostics=['I1failed compilation at four wrong shared action method names;compile1source retained. I2usesResolveAction.',
            'N1C1/I2passed corpse/stat/recovery/expiry but fixture Train minimum-rank helper didnotrefund points;all source/DLLs/logs retained. Use actual hard7to8training instead.',
            'C2/I4/N2passed with immediate Engorge move-away-and-back cancellation. Source/DLLs/logs retained.',
            'C3/I5/S5/N3adds explicit direct/successful guards and raw-drain conflict controls;passes. No broad older matrix rerun.'],
        limits=['Current do161 remains unavailable;complete proposed corpse/recipient/action/payment/state/regeneration/percentage-drain adaptations need approval before learned admission.',
            'No new actual socket skill inputs or future Bind Demon integration claimed. No full class,asset,PvP/property/global-world/client certification. Blood Oath decision independently pending.'],inputs=inputs)
    out=ROOT/'tools/reference/engorge-world-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Engorge world evidence stale'
    else:out.write_text(encoded)
    print('Prepared complete Engorge:C3/I5/S5/N3,15markers/3original buffed bodies/2reloads;226handlers;adaptation decision pending.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
