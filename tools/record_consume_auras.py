#!/usr/bin/env python3
"""Authenticate prepared Consume aura providers and retained Paladin behavior."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/consume-auras'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-11.json',STAGE/'birth-inputs.json')
    first=pin(STAGE/'source-snapshot-9.json')['inputs'];last=pin(STAGE/'source-snapshot-11.json')['inputs'];production=lambda x:{n:h for n,h in x['sourceInputs'].items()if not n.startswith('tests/')}
    assert production(first)==production(last)and first['projectInputs']==last['projectInputs']and first['copiedData']==last['copiedData']
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    previous=pin(ROOT/'.local/class-skills/bind-demon-consume/birth-inputs.json');parent=pin('tools/reference/bound-demon-consume-native-evidence.json')
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    new={'src/Sanctuary.Content/ConsumeAuras.cs','tests/Sanctuary.Integration/ConsumeAuraChecks.cs'}
    changed={'src/Sanctuary.Content/'+n+'.cs'for n in('ConsumeEffects','ConsumePrepared','PaladinAuras','PaladinElements')}|{'tests/Sanctuary.Integration/BoundDemonConsumeChecks.cs'}
    assert set(now)-set(old)==new and not set(old)-set(now)and{n for n in old if old[n]!=now[n]}==changed
    assert born['copiedData']==previous['copiedData']and first['projectInputs']==pin(ROOT/'.local/class-skills/bind-demon-consume/source-snapshot-3.json')['inputs']['projectInputs']
    for kind,number in(('content',3),('integration',9)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text()for s in('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for name in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
        'paladin-aura-check':1,'paladin-aura-result':1,'paladin-aura-reload-check':2,'paladin-aura-reload-result':2})
    for family in('consume-world','bound-demon-consume','consume-aura'):
        expected.update({family+'-check':1,family+'-check-result':1,family+'-reload-check':2,family+'-reload-check-result':2})
    text=e.cohort(STAGE,expected,33);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-6.log');assert text.count('Client: 127.0.0.1: Connected.')==9
    for phrase in('Consume aura:all four recipes share fixed15Salvation/fixed15Meditation650/no Prayer healing/HolyFire21and25 existing damage/range/weapon math;',
        'Consume aura:stronger/equal ordinary selected aura ownership survives provider cleanup;optional old JSON/atomic invalid provenance controls pass;',
        'Bound Consume:513captures across171source rows/three difficulties;16independent literal contexts cover17numeric stats;'):
        assert text.count(phrase)==1,phrase
    assert text.count('Consume aura:actual saved provider tags parse through unchanged aura profile;fixed11NormalMeditation550resumes once at zero mana')==2
    saved=pin(STAGE/'consume-aura-fixtures.json');assert saved['Generation']==2 and saved['State']['Suppliers']['Bonus']['AuraRank']==11 and saved['State']['Suppliers']['Bonus']['AuraSkill']=='Meditation'
    assert saved['Life']>0 and saved['AllyLife']>0
    for name in('consume-world-fixtures.json','bound-demon-consume-fixtures.json'):assert pin(STAGE/name)['Generation']==2
    pin(STAGE/'paladin-aura-checks.json')
    c2=None
    for number in range(1,6):
        a=STAGE/f'native-{number}';failed=pin(a/'birth-inputs.json');pin(a/'birth-native-checks.log');pin(a/f'native-run-{number}.log')
        assert ': failed'in(a/'birth-native-checks.log').read_text()
        for name,h in failed['sourceInputs'].items():assert e.sha(a/'source'/name)==h,name
        content=failed['artifacts']['Assemblies/Sanctuary.Content.dll']
        if number<=3:
            if c2 is None:c2=content
            assert content==c2
        else:assert content==born['artifacts']['Assemblies/Sanctuary.Content.dll']
    assert 'stateFalse,ownerHP79103/79103,allyHP63999/63999'in(STAGE/'native-3/birth-native-checks.log').read_text()
    assert 'Resume aura:self550,ally0,mana0,rank11,currentTrue'in(STAGE/'native-5/birth-native-checks.log').read_text()
    pin(STAGE/'integration-build-4.log');assert 'Build FAILED.'in(STAGE/'integration-build-4.log').read_text()
    frozen=[]
    for stem,proposal in(('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for name in(f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(name);assert e.sha(ROOT/name)==hashlib.sha256(subprocess.check_output(['git','show','2e3eb877df25a59d410acd2956381f44cc44a423:'+name],cwd=ROOT)).hexdigest();frozen.append(name)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert 'CombatAbility.BindDemon'not in code and 'CombatAbility.Consume'not in code
    for name in(*sorted(new|changed),'tests/check_consume_auras.py','tools/record_consume_auras.py','docs/consume-aura-checkpoint.md','docs/bind-demon-consume-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Prepared fixed Consume aura providers through shared Paladin delivery',handlers=226,warlockHandlers=17,completedClasses=[],
        learnedBindDemonEnabled=False,learnedConsumeEnabled=False,nativeMarkers=33,markers=dict(expected),actualInputs=0,authenticatedSessions=9,boots=3,reloads=2,saves=3,
        completedAuraRecipes=['Baal Subject 5','The Feature Creep','The Smith','megademon6'],remainingAuraRecipeGates=[],allCatalogKindsMechanicallyConsumed=18,
        actorMappingsSeparatelyQualified=True,generalItemAuraEquipmentCertified=False,actualNativeWeaponFire=True,selectedAuraEmitterIndependent=True,zeroPulseMana=True,
        oldAuraJsonDefaults=True,actualSavedProviderLineage=True,ordinaryStrongEqualAuraPreserved=True,retainedSelectedPaladinRegression=True,retainedOriginalConsumeRegression=True,retainedNumericConsumeRegression=True,
        contentBuild=3,integrationBuild=9,sourceSnapshot=11,nativeRun=6,nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,
        optionalSavedAuraFields=['ProviderOwner','ProviderOrder'],transientProviderScheduleSaved=False,newLiveReferenceReads=0,currentRuntimeItemAuraCallerParity=False,
        frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['All four aura recipes reuse Paladin PulseCore/Elements,allied filters,rank/list priority,weapon damage/immunity and Meditation recovery. Fixed captured aura ranks ignore general skills;Warlock hard Paladin synergies zero.',
            'Pinned Salvation immediate1;Meditation/HolyFire delayed50frames. Zero pulse mana,independent selection,no same-frame or lag catch-up bursts pass;HolyFire actual native weapon fire and immunity pass;wounded Meditation targets gain no Prayer healing.',
            'Provider provenance is optional in existing version0aura profile. Stronger ordinary and equal actual selected Paladin refresh survive cleanup;old JSON and atomic malformed lineage refusal pass.',
            'Actual serialized allied provider tags load through native profile. Normal11Meditation550resumes twice atzero mana with originalstate/deadline/wounds;stop/expiry clears self. Offline ally is restored to map before reception,like existing Paladin fixture.',
            'Original selected Paladin/original Consume/numeric Consume families retain. Five exact native diagnostic archives and one diagnostic compile failure retained;N6isacceptance. C3adds orphan transient cleanup;later fixes are fixtures.'],
        limits=['Prepared borrowed Paladin world rules,not recovered current D2R item-aura startup/group caller or general equipment aura aggregation. Named/native actor mapping and full Bind remain separately unfinished.',
            'Four prior complete proposals pending/frozen;no complete Bind proposal or learned admission. Native bound DeathMark,full Bind channel/chance/payment/mappings/modifiers/underlings/sunder/life-buff capture remain next.',
            'No native/client build,deployment,new artwork or installed-save/config write. Installed D2R3.4.95032does not replace pinned3.3.93847. Whole actor/RNG/property/PvP/client/globalworld/fullclass certification remains open.'],inputs=inputs)
    out=ROOT/'tools/reference/consume-aura-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Consume aura evidence stale'
    else:out.write_text(encoded)
    print('Consume auras:C3/I9/S11/N6,33markers/9connections/2reloads;four recipes/shared Paladin delivery/provider lineage;learned admission still gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
