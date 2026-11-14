#!/usr/bin/env python3
"""Authenticate the prepared Consume state,combat,pool,town and save cohort."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from build_consume_bonuses import record as imported
from review_consume import record as boundary
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/consume-world'
def record(check=False):
    imported(True);boundary(True);inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    a=pin(STAGE/'source-snapshot-2.json')['inputs'];b=pin(STAGE/'source-snapshot-3.json')['inputs']
    production=lambda d:{n:h for n,h in d['sourceInputs'].items()if not n.startswith('tests/')}
    assert production(a)==production(b)and a['projectInputs']==b['projectInputs']and a['copiedData']==b['copiedData']
    parent=pin('tools/reference/consume-suppliers-native-evidence.json');previous=json.loads((ROOT/'.local/class-skills/consume-suppliers/birth-inputs.json').read_text())
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    new={'src/Sanctuary.Content/ConsumeEffects.cs','src/Sanctuary.Content/ConsumeCasting.cs','src/Sanctuary.Content/ConsumeRosterGump.cs','tests/Sanctuary.Integration/ConsumeWorldChecks.cs'}
    changed={'src/Sanctuary.Content/'+n+'.cs'for n in ('BattleCommand','ClassCombat','ClassResources','ClassStats','NamedCombat','NamedElements','TimedEffects','WarlockGoatmanCasting','WarlockGoatmanWorld')}
    assert set(now)-set(old)==new and set(old)-set(now)==set()and {n for n in old if old[n]!=now[n]}==changed
    assert born['copiedData']==previous['copiedData']
    for kind,number in (('content',3),('integration',3)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'consume-world-check':1,'consume-world-check-result':1,
        'consume-world-reload-check':2,'consume-world-reload-check-result':2})
    text=e.cohort(STAGE,expected,15);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-3.log')
    assert text.count('Client: 127.0.0.1: Connected.')==3
    assert text.count('real Goblin Toe25+skill9makes one34percent roll,not independent or duplicated procs')==1
    assert text.count('actual owner life/movement/weaponED,one aggregateCB,fire/magic damage/pierce,caps/immune,replace/expiry/FIFO/difficulty')==1
    assert text.count('consumed body never returns;reserved logical order survives new summon FIFO')==2
    saved=pin(STAGE/'consume-world-fixtures.json');assert saved['Generation']==2 and len(saved['Pets'])==2
    assert saved['Effect']['Suppliers']['Rank']==20 and saved['Effect']['Suppliers']['Monster']=='warputriddefiler'
    assert saved['Life']==saved['Maximum']-13
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert 'CombatAbility.Consume'not in code
    assert 'internal static bool Approved=>false;'in(ROOT/'src/Sanctuary.Content/ConsumeEffects.cs').read_text()
    for name in (*sorted(new|changed),'tests/check_consume_world.py','tools/record_consume_world.py','docs/consume-world-rule-proposal.md',
        'docs/consume-world-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md',
        'docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    diagnostic=pin(STAGE/'native-1/birth-inputs.json');pin(STAGE/'native-1/birth-native-checks.log')
    assert diagnostic['artifacts']['Assemblies/Sanctuary.Content.dll']==e.sha(STAGE/'native-1/content/Sanctuary.Content.dll')
    pin(STAGE/'native-2/birth-inputs.json');pin(STAGE/'native-2/birth-native-checks.log');pin(STAGE/'source-snapshot-4.json')
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Prepared complete Consume owner-state,shared pool and player action',handlers=226,warlockHandlers=17,completedClasses=[],
        learnedConsumeEnabled=False,completeAdaptationPending=True,nativeMarkers=15,markers=dict(expected),actualInputs=0,authenticatedSessions=3,boots=3,reloads=2,saves=3,
        savedSurvivingRosterSize=2,rosterGumpResponseTested=True,actualWeaponAndSpellConsumers=True,aggregateItemAndSkillCrushingBlow=True,
        contentBuild=3,integrationBuild=3,sourceSnapshot=4,nativeRun=3,nativeRebuilt=False,clientRebuilt=False,newTimers=0,
        newPersistenceProfiles=[dict(name='SanctuaryConsume',priority=250,version=0)],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,
        artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Actual additive max-life/percentage transitions,skill movement,offweapon physical damage and one combined item/Consume Crushing Blow roll use existing consumers.',
            'Actual fire/magic spell mastery and piercing,immunity refusal,species replacement,lower recast,gear snapshot,exact expiry and difficulty-only rescale pass.',
            'Logical buff slot uses retired body original order in shared FIFO;both buff and live-body eviction pass;highest retired order remains reserved after two reloads.',
            'Actual safe region parks original roster;sent menu and actual response select hidden owned body;shared SC release pays once,never early/replayed and stale response cannot resurrect consumed body.',
            'Named optional persistence keeps owner over-base fractional life,deadline and buff while original surviving body birth/wounds/RNG/controllers retain before intentional FIFO replacements. Consumed body stays absent.',
            'Diagnostic first native run reached real aggregate CB then failed a later occupied fixture spawn;archived. Test free-ground selection corrected. Native2 then passed combat,town and first reload;second reload correctly reused retired serial for a new Goatman,exposing an identity assertion and overly strict historical-serial validation. Creation order now distinguishes identities;historical serials may repeat across distinct slots. Content build1 lacked Gump extension namespace;build2 corrected it.'],
        limits=['Protected current start69/do159 context,retirement/payment/state/pool/periodic behavior remains unverified;complete frozen adaptation decision required before learned admission.',
            'Actual menu response and internal cast callbacks are covered,not actual socket ability-input dispatch. No client/native rebuild or deployment.',
            'Only three original learned demons qualify. Bind Demon,all bound-monster rows,PvP/property/globalworld/client/fullclass certification and final assets remain unfinished.',
            'One named persistence profile is added at priority250/version0;priority is not a numeric persistence identity. Existing versions stay unchanged.'],inputs=inputs)
    out=ROOT/'tools/reference/consume-world-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Consume world evidence stale'
    else:out.write_text(encoded)
    print('Consume world:C3/I3/S4/N3,15markers/3sessions/2reloads;actual combat/stats/pool/town UI/saves;learned gate retained.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
