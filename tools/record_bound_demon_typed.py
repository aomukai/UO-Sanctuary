#!/usr/bin/env python3
"""Authenticate typed original bound-NPC packets, not full learned Bind Demon."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from prepare_bound_typed_native import prepare

ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bind-demon-typed'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    prepare(check=True)
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    pin(STAGE/'source-snapshot-3.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    previous=pin(ROOT/'.local/class-skills/bind-demon-host/birth-inputs.json');parent=pin('tools/reference/bound-demon-host-native-evidence.json')
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    new={'tests/Sanctuary.Integration/BoundDemonTypedChecks.cs'}
    changed={'src/Sanctuary.Content/BoundDemonHosts.cs','src/Sanctuary.Content/SkillCombat.cs','tests/Sanctuary.Integration/BoundDemonHostChecks.cs'}
    assert set(now)-set(old)==new and not set(old)-set(now)and{n for n in old if old[n]!=now[n]}==changed
    assert born['copiedData']==previous['copiedData']
    old_snapshot=pin(ROOT/'.local/class-skills/bind-demon-host/source-snapshot-10.json')['inputs']
    assert old_snapshot['projectInputs']==json.loads((STAGE/'source-snapshot-3.json').read_text())['inputs']['projectInputs']
    for kind,number in (('content',2),('integration',3)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text()for s in('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    native=pin(STAGE/'native-source-evidence.json');pin(native['patch']);build=STAGE/'native-build-1.log';pin(build)
    assert all(s in build.read_text()for s in('Build succeeded.','0 Warning(s)','0 Error(s)'))
    before=ROOT/native['parent'];after=STAGE/'native'
    def sources(root):
        return{str(p.relative_to(root)):e.sha(p)for p in(root/'Projects').rglob('*.cs')if not{'obj','bin'}.intersection(p.parts)}
    a,b=sources(before),sources(after);assert a.keys()==b.keys()
    assert{n:dict(before=a[n],after=b[n])for n in a if a[n]!=b[n]}==native['changedSources']
    assert list(native['changedSources'])==['Projects/UOContent/Misc/AOS.cs']
    assert not native['serverEngineModified']and not native['apiChanged']and native['newSavedFormats']==0
    for name in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert e.sha(after/'Distribution'/name)==born['artifacts'][name]
        assert(e.sha(before/'Distribution'/name)==born['artifacts'][name])==(name!='Assemblies/UOContent.dll')
        if name!='Assemblies/UOContent.dll':assert born['artifacts'][name]==parent['artifacts'][name]
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'ward-cast-check':1,'ward-cast-reload-check':2,
        'revive-facet-check':1,'revive-facet-reload-check':2,'bound-demon-host-check':1,'bound-demon-host-check-result':1,
        'bound-demon-host-reload-check':2,'bound-demon-host-reload-check-result':2,'bound-demon-typed-check':1,
        'bound-demon-typed-check-result':1,'bound-demon-typed-reload-check':2,'bound-demon-typed-reload-check-result':2})
    text=e.cohort(STAGE,expected,27);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    assert text.count('Client: 127.0.0.1: Connected.')==6
    for phrase in('Bound typed:10literal native AOS contexts/physical and elemental immunity/armor-ignore/ranged/nonweapon spell/zero base;',
        'pure-poison packets gain recovery only with surviving added physical;',
        'actual Dagger.OnHit keeps0physical50fire50poison and adds captured75physical once before290percentED',
        'positive actual Barbarian Battle Orders35plus Consume6joins one41percent life pool;'):
        assert text.count(phrase)==1,phrase
    assert text.count('Bound typed:original Imp/native0-50-50split/base endpoints/weapon/Magery/immutable bonuses and injured resources survive reload;')==2
    saved=pin(STAGE/'bound-demon-typed-fixtures.json');assert saved['Generation']==2 and saved['State']['RuntimeType']=='Server.Mobiles.Imp'
    assert saved['State']['Birth']['FlatDamage']==75 and saved['State']['Birth']['DamagePercent']==290
    assert saved['State']['Normal']['PhysicalDamage']==0 and saved['State']['Normal']['FireDamage']==50 and saved['State']['Normal']['PoisonDamage']==50
    assert saved['Life']==saved['State']['BaseMaximum256']//2-17
    assert saved['Minimum']==saved['State']['Normal']['Minimum']and saved['Maximum']==saved['State']['Normal']['Maximum']
    host=pin(STAGE/'bound-demon-host-fixtures.json');assert host['Generation']==2 and host['State']['RuntimeType']=='Server.Mobiles.Daemon'
    failed=pin(STAGE/'native-1/birth-inputs.json');pin(STAGE/'native-1/birth-native-checks.log');pin(STAGE/'native-1/native-run-1.log')
    assert 'bound-demon-typed-check-result: failed'in(STAGE/'native-1/birth-native-checks.log').read_text()
    assert failed['artifacts']['Assemblies/UOContent.dll']!=born['artifacts']['Assemblies/UOContent.dll']
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0]
    assert 'CombatAbility.BindDemon'not in code and 'CombatAbility.Consume'not in code
    pending=json.loads((ROOT/'.local/class-skills/current-checkpoint.json').read_text())['pendingAdaptations']['consume']
    assert e.sha(ROOT/pending['proposal'])==pending['proposalSha256']and e.sha(ROOT/pending['evidence'])==pending['evidenceSha256']
    for name in(*sorted(new|changed),'tests/check_bound_demon_typed.py','tools/prepare_bound_typed_native.py','tools/record_bound_demon_typed.py',
        'docs/bind-demon-typed-checkpoint.md','docs/bind-demon-host-checkpoint.md','docs/class-skill-roadmap.md',
        'docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Prepared bound original NPC typed native weapon damage',handlers=226,warlockHandlers=17,completedClasses=[],
        learnedBindDemonEnabled=False,completeBindRuleApproved=False,nativeMarkers=27,markers=dict(expected),actualInputs=0,authenticatedSessions=6,boots=3,reloads=2,saves=3,
        literalNativeDamageContexts=10,actualNativeDaggerHit=True,originalMixedImpSaved=True,legacyDerivedEndpointsReconstructed=True,
        retainedWardIncomingRegression=True,retainedReviveFacetRegression=True,retainedOriginalHostRegression=True,
        contentBuild=2,integrationBuild=3,sourceSnapshot=3,nativeRun=2,nativeRebuilt=True,clientRebuilt=False,
        nativeChangedFiles=list(native['changedSources']),serverEngineModified=False,apiChanged=False,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,
        newTimers=0,newLiveReferenceReads=0,currentRuntimeBinderParity=False,artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Original Imp melee retains0physical/50fire/50poison and base endpoints;captured flat physical is added once before physical ED,not split into native elements.',
            'Ten literal actual AOS contexts verify physical/elemental immunity,ranged,nonweapon spell,armor-ignore,and poison-only recovery;actual Dagger.OnHit verifies original native roll commitment.',
            'One AOS game-content line admits existing hit recovery for surviving prepared physical;engine/API/save formats unchanged.',
            'Same injured Imp,original weapon/Magery/native split/endpoints,immutable birth and mana/stamina survive both reloads. Older physical-host derived endpoints reconstruct without double addition.',
            'Ward/Revive/original bound host/positive Barbarian BO35plus Consume6/shared FIFO/town/offline controls pass in same disposable cohort.',
            'Native1fixture incorrectly expected no fully-resisted contact;archived sources/DLLs/logs preserve failure. Native2is acceptance;Integration2diagnostics were not run.'],
        limits=['Prepared UO projection,not recovered current binder ordering or full D2R ability parity. No complete Bind proposal or learned channel/chance/payment/input.',
            'Native chaos/direct/malformed splits,unmapped/native named actors,source modifiers/underlings/sunder,active-life-buff capture and other bound demon recipients remain unfinished.',
            'No new effects/audio/icons,client build,deployment or installed-save/config write. Installed D2R3.4.95032does not replace pinned3.3.93847.',
            'Property/PvP/client/global-world/fullclass certification and actual learned Bind socket inputs remain unqualified.'],inputs=inputs)
    out=ROOT/'tools/reference/bound-demon-typed-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Bound typed evidence stale'
    else:out.write_text(encoded)
    print('Bound typed:C2/I3/S3/N2,27markers/6sessions/2reloads;actual Imp weapon split,physical addition,recovery,old endpoints;learned Bind remains gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
