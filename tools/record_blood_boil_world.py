#!/usr/bin/env python3
"""Authenticate the prepared Blood Boil rule and original save cohort."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/blood-boil-world'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    a=pin(STAGE/'source-snapshot-1.json')['inputs'];b=json.loads((STAGE/'source-snapshot-2.json').read_text())['inputs']
    production=lambda d:{n:h for n,h in d['sourceInputs'].items()if not n.startswith('tests/')}
    assert production(a)==production(b)and a['projectInputs']==b['projectInputs']and a['copiedData']==b['copiedData']
    parent=pin('tools/reference/engorge-world-native-evidence.json');previous=json.loads((ROOT/'.local/class-skills/engorge-world/birth-inputs.json').read_text())
    current_cs={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')};old_cs={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')}
    new={'src/Sanctuary.Content/BloodBoilPrepared.cs','src/Sanctuary.Content/BloodBoilCasting.cs','tests/Sanctuary.Integration/BloodBoilWorldChecks.cs'}
    changed={'src/Sanctuary.Content/ClassCombat.cs','src/Sanctuary.Content/WarlockGoatmanCasting.cs'}
    assert set(current_cs)-set(old_cs)==new and set(old_cs)-set(current_cs)==set()
    assert {n for n in old_cs if current_cs[n]!=old_cs[n]}==changed,'Unexpected preexisting C# change'
    for kind,number in (('content',1),('integration',2)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    source=pin('tools/reference/blood-boil-source-review.json');assert source['skill']==380 and source['sourceCandidate']['readableCodeBytes']==0
    for name,h in source['inputs'].items():assert e.sha(ROOT/name)==h,name
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'blood-boil-world-check':1,'blood-boil-world-check-result':1,
        'blood-boil-world-reload-check':2,'blood-boil-world-reload-check-result':2})
    text=e.cohort(STAGE,expected,15);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    assert text.count('Client: 127.0.0.1: Connected.')==3
    for phrase in ('mixed three-body roster retains three-demon5percent divisor','actual rank1two-demon cast spends3.75mana once and7percent',
        'rank1Witch with no Engorge spends3.75mana','10independent tier endpoints,255cost/radius contexts'):
        assert text.count(phrase)==1,phrase
    assert text.count('Prepared Blood Boil:original three demon births,fractional wounds,RNG/hosts survive reload without offline recovery or replay,')==2
    saved=pin(STAGE/'blood-boil-world-fixtures.json');assert saved['Generation']==2 and len(saved['Pets'])==3
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0]
    assert 'CombatAbility.BloodBoil'not in code
    assert 'internal static bool Approved=>false;'in(ROOT/'src/Sanctuary.Content/BloodBoilPrepared.cs').read_text()
    for name in (*sorted(new|changed),'tests/check_blood_boil_world.py','tools/review_blood_boil.py','tools/record_blood_boil_world.py',
        'docs/blood-boil-rule-proposal.md','docs/blood-boil-world-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md',
        'docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    old=pin(STAGE/'native-1/birth-inputs.json');pin(STAGE/'native-1/birth-native-checks.log')
    assert old['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Prepared complete Blood Boil action and resource/area rule',handlers=226,warlockHandlers=17,completedClasses=[],
        learnedBloodBoilEnabled=False,completeAdaptationPending=True,nativeMarkers=15,markers=dict(expected),actualInputs=0,connectedSessions=3,
        characterAuthentications=4,characters=2,boots=3,reloads=2,saves=3,originalSavedBodies=3,temporaryControlBodies=2,witchGearControls=True,
        independentTierContexts=10,countCostContexts=255,hardRoundingControls=2,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,newLiveReferenceReads=0,
        artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Owned380suppliers preserve physical pre-shift and fire FP8 independenthard Engorge/Oath synergies;real Tarnhelm own19to20doesnotdonatesoft synergy.',
            'Actual3-body5percent/mixed-roster division,2-bodyrank1each7percent,and1-body15percent lethal cost pass. Engorge-free Witch still erupts captured center after native pet death. Resource charges emit no pet direct attack event.',
            'One transient cast source stream rolls physical/fire per burst without consuming pet RNG. Ordinary typed area mitigation,range,immune contact andshared120ms recipient lease/exclusiveboundary pass.',
            'Empty-area payment,lowmana/no-demon/all-low-life refusals,SC unpaidwindup,stop/movement/late eligibility,cost once/no replay pass.',
            'Original3bodybirths,fractionalwounds,RNG/controllers survivebothparkedreloadswithoutrecovery/replay and releaseanothercostedexplosion afterlogin.'],
        diagnostics=['C1/I1/S1/N1passed;source,DLLs,logs/inputs archived. I2/S2only expandsactualmixed-rosterandtwo-bodycostcoverage;production Content1unchanged. C1/I2/S2/N2passes,zerowarnings/errors. No failed native cohort or broad older matrix rerun.'],
        limits=['Currentdo160remainsprotected;maximum-lifecost/gate/lethalbehavior/roster/seed/roll/order/delayscope/nativearea/payment areexplicitproposedadaptations,notcurrentcallbackequivalence.',
            'Ownedtooltip5/6/7yardradius,raw6/9/12andolder11yardreport differ;proposedsharedareausesrawformulaexplicitly.',
            'Learnedadmissionawaitscompleteproposaldecision. Noactualnewsocketinputs,BindDemon,PvP/hireling/hostileclasssummon/property/globalworld/client/fullclasscertification orfinalassetsclaimed. Engorge/BloodOath decisionsindependent.'],inputs=inputs)
    out=ROOT/'tools/reference/blood-boil-world-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Blood Boil world evidence stale'
    else:out.write_text(encoded)
    print('Prepared complete Blood Boil:C1/I2/S2/N2,15markers/actual1-2-3mixed rosters/lethalcost/2reloads;226handlers;decision pending.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
