#!/usr/bin/env python3
"""Record Engorge numeric capture without claiming a complete skill route."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/engorge-suppliers'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    parent=pin('tools/reference/blood-oath-damage-native-evidence.json')
    previous=json.loads((ROOT/'.local/class-skills/blood-oath-damage/birth-inputs.json').read_text())
    new={'src/Sanctuary.Content/EngorgePrepared.cs','tests/Sanctuary.Integration/EngorgeSupplierChecks.cs'}
    current_cs={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    previous_cs={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')}
    assert set(current_cs)-set(previous_cs)==new
    assert all(current_cs[n]==h for n,h in previous_cs.items()),'Preexisting C# source changed'
    for kind in ('content','integration'):
        log=STAGE/f'{kind}-build-1.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    source=pin('tools/reference/engorge-source-review.json');assert source['skill']==379 and source['sourceCandidate']['readableCodeBytes']==0
    for name,h in source['inputs'].items():assert e.sha(ROOT/name)==h,name
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'engorge-suppliers-check':1,'engorge-suppliers-check-result':1,
        'engorge-suppliers-reload-check':2,'engorge-suppliers-reload-check-result':2})
    text=e.cohort(STAGE,expected,15);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    assert text.count('Client: 127.0.0.1: Connected.')==3
    assert text.count('Engorge suppliers:765pinned rank/hard-point contexts;')==1
    assert text.count('Engorge suppliers:original Witch/gear and three parked demon births,wounds,RNG/controllers survive reload;')==2
    assert 'actual Witch/Tarnhelm19to20' in text
    saved=pin(STAGE/'engorge-suppliers-fixtures.json');assert saved['Generation']==2 and len(saved['Pets'])==3
    assert saved['Frozen']['Rank']==19 and saved['Current']['Rank']==20 and saved['Current']['HardBloodOath']==7
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0]
    assert 'CombatAbility.Engorge'not in code
    for name in (*sorted(new),'tests/check_engorge_suppliers.py','tools/review_engorge.py','tools/record_engorge_suppliers.py',
        'docs/engorge-suppliers-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md',
        'docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Engorge immutable numerical suppliers',handlers=226,warlockHandlers=17,completedClasses=[],
        learnedEngorgeEnabled=False,productionBuffStateEnabled=False,productionPersistenceAdded=False,nativeMarkers=15,markers=dict(expected),
        formulaContexts=765,actualInputs=0,authenticatedSessions=3,boots=3,reloads=2,saves=3,originalBodies=3,witchGearControl=True,
        contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceIds=0,
        serializationVersionChanged=False,newLiveReferenceReads=0,artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Owned skill/skilldesc/strings establish healing-percent calc1;existing evaluator captures fractional cost,duration,healing,resistance,raw drain,IAS and hard Oath armor/regeneration.',
            '765rank/hard-point contexts,FP8 healing/missing-life caps,contradictory contexts and malformed payload rejection pass.',
            'Actual Witch/Tarnhelm19to20changes own suppliers but effective Oath7to8does not rewrite hard7armor175/rawregen35;old captures and original summon births stay unchanged.',
            'Original three-species parked body births,fractional wounds,RNG/controllers and actual owner gear survive two reloads. Numerical payload is saved in fixture JSON only;no production Engorge buff save is claimed.',
            'Only two new C# files;all preexisting source unchanged. Prior runtime proofs reused at original scopes,not rerun or relabeled as fresh socket-input evidence.'],
        limits=['Engorge remains gated:corpse/recipient/action/payment/state/stat-consumer/lifecycle contract not complete.',
            'Current do161 inaccessible in seven retained fragments;NPC flat drain versus tooltip percent remains unresolved;raw hpregen not reinterpreted as percent.',
            'No full class,asset,client/global-world or PvP certification. Blood Oath adaptation decision remains independently pending.'],inputs=inputs)
    out=ROOT/'tools/reference/engorge-suppliers-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Engorge supplier evidence stale'
    else:out.write_text(encoded)
    print('Engorge suppliers:C1/I1/S1/N1,15markers/765formula contexts/realgear/3original bodies/2reloads;226handlers;learned Engorge remains incomplete.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
