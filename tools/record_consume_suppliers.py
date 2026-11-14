#!/usr/bin/env python3
"""Authenticate imported Consume suppliers without claiming a complete buff or cast."""
import argparse,hashlib,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from build_consume_bonuses import record as imported
from review_consume import record as boundary
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/consume-suppliers'
def record(check=False):
    imported(True);boundary(True);inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    parent=pin('tools/reference/blood-boil-world-native-evidence.json');previous=json.loads((ROOT/'.local/class-skills/blood-boil-world/birth-inputs.json').read_text())
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    new={'src/Sanctuary.Content/ConsumePrepared.cs','tests/Sanctuary.Integration/ConsumeSupplierChecks.cs'}
    changed='src/Sanctuary.Content/WarlockGoatmanWorld.cs'
    assert set(now)-set(old)==new and set(old)-set(now)==set()and {n for n in old if old[n]!=now[n]}=={changed}
    old_comment='    // The source pettype=demon pool is shared by these three species. Bind\n    // Demon has a distinct pettype and does not enter this replacement pool.'
    new_comment='    // These three implemented species share source pool1. Bind Demon has\n    // a distinct pettype,but the same pool1;its unfinished actor/Consume slot\n    // integration must eventually join this replacement pool.'
    code=(ROOT/changed).read_text();assert code.count(new_comment)==1 and hashlib.sha256(code.replace(new_comment,old_comment).encode()).hexdigest()==old[changed]
    assert set(born['copiedData'])-set(previous['copiedData'])=={'Data/Sanctuary/Balance/consume-bonuses.json'}
    assert all(born['copiedData'][n]==h for n,h in previous['copiedData'].items())
    for kind in ('content','integration'):
        log=STAGE/f'{kind}-build-1.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    source=pin('tools/reference/d2r-consume/sources.json');review=pin('tools/reference/consume-source-review.json')
    assert source['checkedFields']==21 and source['differences']==[]and review['skill']==381 and review['sourceCandidate']['readableCodeBytes']==0
    for source_record in (source,review):
        for name,h in source_record['inputs'].items():assert e.sha(ROOT/name)==h,name
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'consume-suppliers-check':1,'consume-suppliers-check-result':1,
        'consume-suppliers-reload-check':2,'consume-suppliers-reload-check-result':2})
    text=e.cohort(STAGE,expected,15);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    assert text.count('Client: 127.0.0.1: Connected.')==3
    assert text.count('Consume suppliers:20literal common/difficulty/typed-bonus/cap contexts and6malformed captures pass;')==1
    assert text.count('Consume suppliers:original Witch/gear and3parked demon births,wounds,RNG/hosts survive reload;')==2
    assert 'soft Death Mark4to5doesnotraiseCB9,hard4to5raisesnewCB10'in text
    saved=pin(STAGE/'consume-suppliers-fixtures.json');assert saved['Generation']==2 and len(saved['Pets'])==3
    assert saved['Frozen']['Rank']==19 and saved['Frozen']['HardDeathMark']==4 and saved['Current']['Rank']==20 and saved['Current']['HardDeathMark']==5
    implemented=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert 'CombatAbility.Consume'not in implemented
    for name in (*sorted(new),changed,'tests/check_consume_suppliers.py','tools/build_consume_bonuses.py','tools/review_consume.py','tools/record_consume_suppliers.py',
        'tools/reference/d2r-consume/monpet.json','tools/reference/d2r-consume/monpet.txt.gz','data/Sanctuary/Balance/consume-bonuses.json',
        'docs/consume-suppliers-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Consume authenticated monpet import and explicit numerical capture',handlers=226,warlockHandlers=17,completedClasses=[],
        learnedConsumeEnabled=False,productionBuffStateEnabled=False,slotOccupancyImplemented=False,productionPersistenceAdded=False,nativeMarkers=15,markers=dict(expected),
        literalNumericContexts=20,malformedContexts=6,actualInputs=0,authenticatedSessions=3,boots=3,reloads=2,saves=3,originalSavedBodies=3,witchGearControl=True,
        importedSpecies=3,corroboratedFields=21,importedRawJsonBytes=source['primary']['bytes'],corroboratingRawBytes=source['corroboration']['bytes'],corroboratingStoredBytes=source['corroboration']['storedBytes'],
        contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceIds=0,
        serializationVersionChanged=False,newLiveReferenceReads=0,artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Exact primary monpet Git blob and independently version-pinned TSV blob corroborate21fields for3original species. Runtime imports3rows;full rawsource retained,TSVlosslesslycompressed. Existing28-file manifest unchanged.',
            'Existing formula evaluator captures commonmana/duration/life/velocity and typed ED/CB/fire/magicmastery/pierce. sksrc-to-skill alias is scoped to explicitsupplied caster/hardrank/1-2-3difficulty context,not global grammar.',
            '20literalcontexts covercommondiminishing/linearvalues,all3difficulties,typedcapsandhardCB.6malformedcapturesrefuse.',
            'Actual Witch/Tarnhelm19to20andsoftDeathMark4to5retainCB9;hard4to5givesnewCB10withoutrewritingfrozencapturesororiginalpetbirths.',
            'Original3parkedpetbirths,fractionalwounds,RNG/controllers,actualownergear/hardpointsandfixture-onlycapturesretainthrough2reloads.No production Consume buff or persistence claimed.',
            'OnlypreexistingC#changeisbyte-normalizedcommentcorrection:bothdemonandbinddemonarepool1.No activepoolbehaviorchanged.'],
        limits=['Currentstart69/do159callers/statwriter/periodic/lifecycle/pool/paymentunverified. Numericcontextmustnotbeclaimedasrecoveredcallback. LearnedConsumeandtypedstatdeliverynotimplemented.',
            'TownInTown1withparkedhiddenarmiesneedsowned-rostertargetmapping. Buffmustoccupysharedpoolratherthanfreefourthpet;futureBindDemonandothermonpetrowsremainunsupported.',
            'ActualweaponCB/stat effects,ownerresource/movementtransitionandcompleteaction/inputverificationremain.Nextcomponentmustreuseexistingservicesratherthanduplicatestats. No fullclass/PvP/property/globalworld/client/assetscertification.'],inputs=inputs)
    out=ROOT/'tools/reference/consume-suppliers-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Consume supplier evidence stale'
    else:out.write_text(encoded)
    print('Consume suppliers:C1/I1/S1/N1,15markers/20literalcontexts/3original bodies/2reloads;226handlers;cast/buff/slot remainunfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
