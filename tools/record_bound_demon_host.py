#!/usr/bin/env python3
"""Authenticate the prepared original living-body host,not learned Bind Demon."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bind-demon-host'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-10.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    a=pin(STAGE/'source-snapshot-7.json')['inputs'];b=pin(STAGE/'source-snapshot-10.json')['inputs']
    production=lambda d:{n:h for n,h in d['sourceInputs'].items()if not n.startswith('tests/')}
    assert production(a)==production(b)and a['projectInputs']==b['projectInputs']and a['copiedData']==b['copiedData']
    parent=pin('tools/reference/bind-demon-suppliers-native-evidence.json');previous=json.loads((ROOT/'.local/class-skills/bind-demon-suppliers/birth-inputs.json').read_text())
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    new={'src/Sanctuary.Content/BoundDemonHosts.cs','tests/Sanctuary.Integration/BoundDemonHostChecks.cs'}
    changed={'src/Sanctuary.Content/'+name+'.cs'for name in ('BattleOrders','ClassStats','CombatAuras','ConsumeEffects','CreatureProfiles','GearEffects','MonsterVariantBaselines',
        'MonsterVariants','NamedElements','PaladinVigor','SigilLethargy','SkillCombat','SkillHealth','SummonDifficulties','TimedEffects','WarlockGoatmanWorld')}
    assert set(now)-set(old)==new and set(old)-set(now)==set()and {n for n in old if now[n]!=old[n]}==changed
    assert born['copiedData']==previous['copiedData']
    for kind,number in (('content',5),('integration',7)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'revive-facet-check':1,'revive-facet-reload-check':2,
        'bound-demon-host-check':1,'bound-demon-host-check-result':1,'bound-demon-host-reload-check':2,'bound-demon-host-reload-check-result':2})
    text=e.cohort(STAGE,expected,18);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-4.log')
    assert text.count('Client: 127.0.0.1: Connected.')==3
    for phrase in ('original Daemon identity and actual native guard acquisition/stop/resume',
        'old baseline JSON without Life256 loads;', 'positive actual Barbarian Battle Orders35plus Consume6joins one41percent life pool;',
        'terminal owner death retires the original body and removes its active roster identity'):
        assert text.count(phrase)==1,phrase
    assert text.count('original Daemon serial,weapon/backpack loot,AI/Magery,captured modifiers/stats,normal baseline,exact wounds/mana/stamina')==2
    saved=pin(STAGE/'bound-demon-host-fixtures.json');assert saved['Generation']==2 and saved['State']['RuntimeType']=='Server.Mobiles.Daemon'
    assert saved['State']['BaseMaximum256']==20000*256 and saved['State']['Birth']['Rank']==2 and saved['State']['Normal']['Life256']==25000*256
    assert saved['Life']==saved['Maximum']//2-17
    oldpass=pin(STAGE/'native-2/birth-inputs.json');pin(STAGE/'native-2/birth-native-checks.log');e.cohort(STAGE/'native-2',expected,18)
    assert oldpass['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
    for n in ('native-1','native-3'):pin(STAGE/n/'birth-inputs.json');pin(STAGE/n/'birth-native-checks.log')
    assert 'Bound body ignored existing Battle Orders maximum-life pool'in(STAGE/'native-3/birth-native-checks.log').read_text()
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert 'CombatAbility.BindDemon'not in code and 'CombatAbility.Consume'not in code
    pending=json.loads((ROOT/'.local/class-skills/current-checkpoint.json').read_text())['pendingAdaptations']['consume']
    assert e.sha(ROOT/pending['proposal'])==pending['proposalSha256']and e.sha(ROOT/pending['evidence'])==pending['evidenceSha256'],'Frozen Consume proposal/evidence changed'
    for name in (*sorted(new|changed),'tests/check_bound_demon_host.py','tools/record_bound_demon_host.py','docs/bind-demon-host-checkpoint.md',
        'docs/consume-world-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Prepared original living bound-NPC host and common stat/lifecycle projection',handlers=226,warlockHandlers=17,completedClasses=[],
        learnedBindDemonEnabled=False,completeBindRuleApproved=False,nativeMarkers=18,markers=dict(expected),actualInputs=0,authenticatedSessions=3,boots=3,reloads=2,saves=3,
        scriptedHostBindings=2,scriptedConsumeTransactions=1,actualNativeGuardAcquisition=True,terminalOwnerDeathCleanup=True,legacyBaselineJsonAccepted=True,
        positiveBattleOrdersReceiver=True,positiveConsumeAdditiveBattleOrders=True,originalBoundNpcSaved=True,retainedReviveFacetRegression=True,
        contentBuild=5,integrationBuild=7,sourceSnapshot=10,nativeRun=4,nativeRebuilt=False,clientRebuilt=False,newTimers=0,
        newPersistenceProfiles=[dict(name='SanctuaryBoundDemons',priority=251,version=0)],existingSerializationVersionsChanged=False,
        optionalExistingJsonFieldsAdded=['MonsterNativeCombat.Life256'],newLiveReferenceReads=0,currentRuntimeBinderParity=False,
        artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Actual original Daemon body/type/serial,AI object/Magery,equipped Dagger/backpack Gold remain;native guard acquires owner enemy and existing suspend/resume stops actions without cloning.',
            'Captured real player gear feeds actual AR/armor,physical damage and health commitment,attack/movement hooks,resistance and source-life cap. One-bound replacement and shared original-demon FIFO pass.',
            'Existing source resource transitions preserve wounds through binding,Normal/Hell/Normal and positive35percent Battle Orders application/removal. Real Barbarian BO35+Consume6 gives one41percent owner life pool;old zero-rank Warlock-supplier overlap claim is explicitly scoped/corrected.',
            'Actual safe region and logout park same original,stop native combat/recovery and retain items/resources. Original owner/body/weapon/loot/AI/skills/captured values/normal baseline retain across two reloads,then terminal owner death deletes original.',
            'Old baseline JSON without optional Life256 loads. Unknown old higher-born Normal life,typed native Imp melee and unqualified Gargoyle reward mapping refuse without owner/resource mutation. Existing Revive facet family retains through two reloads.',
            'Native2passed18markers;Native3old-format control passed then exposed zero-rank Warlock BO supplier. Native4usesactualBarbarian withsameContent5. Native1unlabelled null and all diagnostics are preserved,not counted as acceptance.'],
        limits=['Protected start64/44b240/44b3b0 and actual channel/chance/eligibility/payment/source-context are not recovered. No complete Bind proposal is approved or learned route enabled.',
            'Prepared native physical-body projection is explicit,not verified current binder ordering. Mixed native melee,other UO/source mappings,named encounters,source modifier IDs,underlings,inherited sunder and existing-life-buff capture remain unsupported.',
            'Bound actors still need qualification for Consume/Engorge/BloodOath/BloodBoil/DeathMark recipients and source RNG/whole native-ability/client/PvP/property/fullclass behavior. Standard spawner unlink is reused but not separately exercised here.',
            'No new final effects/audio/icons,native/client build or deployment. Installed D2R is3.4.95032;mechanical reference remains3.3.93847 without new process/data substitution.'],inputs=inputs)
    out=ROOT/'tools/reference/bound-demon-host-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Bound host evidence stale'
    else:out.write_text(encoded)
    print('Bound host:C5/I7/S10/N4,18markers/2reloads;original NPC/common stats/AI/pool/parking/legacy saves/positive buffs;learned Bind still gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
