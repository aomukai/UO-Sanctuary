#!/usr/bin/env python3
"""Authenticate Bind Demon source/stat foundation without claiming a bound actor."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from build_bind_demon_monsters import record as imported
from probe_bind_demon_formulas import record as formulas
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bind-demon-suppliers'
def record(check=False):
    imported(True);formulas(True);inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    a=pin(STAGE/'source-snapshot-1.json')['inputs'];b=pin(STAGE/'source-snapshot-2.json')['inputs']
    production=lambda d:{n:h for n,h in d['sourceInputs'].items()if not n.startswith('tests/')}
    assert production(a)==production(b)and a['projectInputs']==b['projectInputs']and a['copiedData']==b['copiedData']
    parent=pin('tools/reference/consume-world-native-evidence.json');previous=json.loads((ROOT/'.local/class-skills/consume-world/birth-inputs.json').read_text())
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    new={'src/Sanctuary.Content/BindDemonPrepared.cs','tests/Sanctuary.Integration/BindDemonSupplierChecks.cs'}
    assert set(now)-set(old)==new and set(old)-set(now)==set()and all(now[n]==h for n,h in old.items())
    assert set(born['copiedData'])-set(previous['copiedData'])=={'Data/Sanctuary/Balance/bind-demon-monsters.json'}
    assert all(born['copiedData'][n]==h for n,h in previous['copiedData'].items())
    for kind,number in (('content',1),('integration',2)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    source=pin('tools/reference/bind-demon-monsters-source.json');review=pin('tools/reference/bind-demon-formula-review.json');observation=pin('tools/reference/d2r-installed-version-observation-20261115.json')
    assert source['rows']==171 and source['checkedFields']==3248 and source['differences']==source['unbalancedFields']==[]
    assert review['numericAnalogues']==89 and review['constants']==73 and not review['currentRuntimeParity']and not review['ownedMonpetBytecodeCompared']
    for proof in (source,review):
        for name,h in proof['inputs'].items():assert e.sha(ROOT/name)==h,name
    assert observation['mechanicalReferenceRemains']=='3.3.93847'and observation['installedVersions']==['3.4.95032']
    assert not any(observation[n]for n in ('matchesPinnedExecutable','matchesPinnedManifest','cascExtractionPerformed','processMemoryRead','gameFilesWritten'))
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'bind-demon-suppliers-check':1,'bind-demon-suppliers-check-result':1,
        'bind-demon-suppliers-reload-check':2,'bind-demon-suppliers-reload-check-result':2})
    text=e.cohort(STAGE,expected,15);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    assert text.count('Client: 127.0.0.1: Connected.')==3
    assert text.count('171raw monster rows,149class/22named,18underling-bearing entries;')==1
    assert text.count('original Witch,backpacked real helm,hard/soft ranks and frozen/current captures retain through reload;')==2
    saved=pin(STAGE/'bind-demon-suppliers-fixtures.json');assert saved['Generation']==2 and saved['Frozen']['Rank']==20 and saved['Current']['Rank']==19
    assert saved['Frozen']['SharedMaximum']==1 and saved['Current']['SharedMaximum']==2 and saved['Frozen']['HardDeathMark']==4 and saved['Current']['HardDeathMark']==5
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert 'CombatAbility.BindDemon'not in code
    for name in (*sorted(new),'tests/check_bind_demon_suppliers.py','tools/build_bind_demon_monsters.py','tools/probe_bind_demon_formulas.py','tools/record_bind_demon_suppliers.py',
        'data/Sanctuary/Balance/bind-demon-monsters.json','docs/bind-demon-suppliers-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md',
        'docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Bind Demon complete monpet catalog and captured common/stat suppliers',handlers=226,warlockHandlers=17,completedClasses=[],
        learnedBindDemonEnabled=False,boundActorImplemented=False,productionPersistenceAdded=False,nativeMarkers=15,markers=dict(expected),actualInputs=0,
        authenticatedSessions=3,boots=3,reloads=2,saves=3,importedBindingRows=171,monsterClassRows=149,namedEncounterRows=22,corroboratedFields=3248,
        retainedCompilerNumericAnalogues=89,retainedCompilerConstantOutputs=73,ownedMonpetBytecodeParity=False,currentRuntimeFormulaParity=False,
        contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=1,nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],serializationVersionChanged=False,
        processMemoryRead=False,cascExtractionPerformed=False,installedReferenceChanged=True,referenceRepinned=False,artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['171current monpet binding rows/3248fields match exact pinned JSON/TSV Git blobs;149class versus22named IDs remain distinct. All raw formula text and underling/Consume payloads retained,not enabled.',
            'Existing SkillValues captures effective/hard ranks,Mastery/Oath passives,separate sunder fields,bound/shared limits,raw mana data,modifier IDs and chance skill term without inventing its caller or applying NPC stats.',
            'Native literal endpoints/caps/hard slots/refusals,actual Witch/Tarnhelm training changes,frozen captures and original owner/helmet/rank saves pass two reloads. No bound-monster actor is allocated.',
            'Unchanged retained compiler produces89numeric analogues/73constants;raw equality results flag an adaptation question rather than silently repairing source. Sixteen bound-life controls are not single folded constants.',
            'Installed executable/manifest are now3.4.95032. Guard stopped before extraction;the mechanical reference remains3.3.93847. No protected/live process reads or game-file writes.'],
        limits=['Full start64/channel/payment/eligibility/binding helper,monpet progress/quality/health/stat contexts,actual success rolls,underlings and bound-life/stat writers remain unverified and unimplemented.',
            'Native UO species mapping,complete bound actor/original AI/abilities/shared world/pool/lifecycle and learned socket inputs remain unfinished. Do not substitute temporary Conversion or corpse Revive wholesale.',
            'No new production persistence/state consumer/native/client build/deployment or final assets. Full class/property/PvP/globalworld/client certification remains open.'],inputs=inputs)
    out=ROOT/'tools/reference/bind-demon-suppliers-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Bind Demon supplier evidence stale'
    else:out.write_text(encoded)
    print('Bind Demon:C1/I2/S2/N1,15markers/171rows/3248fields/2reloads;reference pin retained;binding/channel/actor still unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
