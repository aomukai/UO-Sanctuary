#!/usr/bin/env python3
"""Authenticate prepared captured bound aura providers and shared regressions."""
import argparse
from collections import Counter
import hashlib,json,subprocess
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bind-demon-auras'
PARENT='cbbbaa994a993746b5da7bf770659b4c781fb346'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str) else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text()) if p.suffix=='.json' else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    parent=pin('tools/reference/bound-demon-spectral-zero-native-evidence.json');previous=pin(ROOT/'.local/class-skills/bind-demon-spectral-zero/birth-inputs.json')
    old={n:h for n,h in previous['sourceInputs'].items() if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items() if n.endswith('.cs')}
    new={'src/Sanctuary.Content/BoundDemonAuras.cs','tests/Sanctuary.Integration/BoundDemonAuraChecks.cs'}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ('BoundDemonHosts','EliteAuras','PaladinAuras','PaladinElements','PaladinHolyFreeze')}
    assert set(now)-set(old)==new and not set(old)-set(now) and {n for n in old if old[n]!=now[n]}==changed and born['copiedData']==previous['copiedData']
    for kind,number in (('content',3),('integration',2)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
        'paladin-aura-check':1,'paladin-aura-result':1,'paladin-aura-reload-check':2,'paladin-aura-reload-result':2})
    for family in ('consume-aura','bound-demon-affix','bound-demon-aura'):
        expected.update({family+'-check':1,family+'-check-result':1,family+'-reload-check':2,family+'-reload-check-result':2})
    text=e.cohort(STAGE,expected,33);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log');assert text.count('Client: 127.0.0.1: Connected.')==9
    for phrase in ('Bound aura:seven original choices become five fixed native providers;',
        'Bound aura:malformed grant refuses atomically;old missing-grant JSON never rerolls;',
        'Bound aura:actual Barbarian BattleCommand raises shared innate aura rank without changing birth;'):
        assert text.count(phrase)==1,phrase
    assert text.count('Bound aura:original saved grant/type/serial/weapon/wounds/resources survive reload;fixed Fanaticism resumes once at zero mana')==2
    for family in ('consume-aura','bound-demon-affix','bound-demon-aura'):assert pin(STAGE/(family+'-fixtures.json'))['Generation']==2
    saved=pin(STAGE/'bound-demon-aura-fixtures.json');grant=saved['State']['Aura'];fanaticism=next(r for r in json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text()) if r['talentId']=='paladin.fanaticism')
    assert grant['Rolled']==7 and grant['Ability']==fanaticism['ability'] and grant['Rank']==max(1,min(99,grant['SourceLevel']//8)) and saved['State']['Birth']['Modifier20']==30
    pin(STAGE/'paladin-aura-checks.json')
    archive=STAGE/'native-1';earlier=pin(archive/'birth-inputs.json');pin(archive/'birth-native-checks.log');pin(archive/'native-run-1.log');e.cohort(archive,expected,33)
    for n,h in earlier['sourceInputs'].items():assert e.sha(archive/'source'/n)==h,n
    for kind in ('Content','Integration'):assert e.sha(archive/kind.lower()/f'Sanctuary.{kind}.dll')==earlier['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    early={n:h for n,h in earlier['sourceInputs'].items() if n.endswith('.cs')}
    assert set(early)==set(now) and {n for n in now if now[n]!=early[n]}==new
    # Only the new provider/test gained live Battle Command after passing C2.
    pin(STAGE/'source-snapshot-1.json');pin(STAGE/'source-snapshot-2.json')
    for n in ('content-build-1.log','content-build-2.log','integration-build-1.log'):
        p=STAGE/n;pin(p);assert all(s in p.read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    for n in (*sorted(new|changed),'tests/check_bound_demon_auras.py','tools/record_bound_demon_auras.py','docs/bind-demon-aura-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/bound-demon-affix-source-review.json','src/Sanctuary.Content/InnateNpcSkills.cs'):pin(n)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert 'CombatAbility.BindDemon' not in code and 'CombatAbility.Consume' not in code
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Prepared native captured bound Aura Enchanted providers',handlers=226,warlockHandlers=17,completedClasses=[],learnedBindDemonEnabled=False,
        nativeMarkers=33,markers=dict(expected),actualInputs=0,authenticatedSessions=9,boots=3,reloads=2,saves=3,contentBuild=3,integrationBuild=2,sourceSnapshot=3,nativeRun=2,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,
        optionalBoundField='Aura',providers=['Concentration','Vigor','Thorns','HolyFreeze','Fanaticism'],actualNativeBodies=True,legacyChoicesInjectedInTests=7,
        zeroUpkeep=True,selectedEmitterIndependent=True,sharedNpcBattleCommandRank=True,legacyAbsentGrantNotAdopted=True,currentD2RAuraSelectionRankSeedParity=False,
        frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Existing encounter pool/level divisor plus qualified replacement form an explicit prepared birth mapping;source choice/base rank/time saved in optional existing profile251/version0field. No load/facet/gear reroll.',
            'Five providers share Paladin standard values/leases/target filtering/priority/lineage and elemental damage/slow/weapon services. Actual bound body emits,Warlock supplies allegiance,zero upkeep and no selected-skill emitter. Full-self/half-allied Fanaticism works atzero mana.',
            'Real original bindings with seven injected legacy choices cover all five providers;HolyFreeze body-centered damage/slow/range/immunity and actual native weapon cold pass;no same-frame/lag bursts.',
            'Replacement/parking stop owned self contribution without deleting ordinary stronger/equal leases;old missing-grant JSON never adopts aura;invalid grant atomic refusal. Immutable choice/source level/base rank,wounds/resources/type/weapon/order survive facets and two reloads.',
            'Shared InnateNpcSkills applies actual Barbarian BattleCommand temporarily. Birth unchanged;lower rank waits for unexpired stronger lease,then returns to base. Original selected Paladin/Consume/affix cohorts retain.',
            'C1compile-only. C2N1passes33and exact archive retained. C3/I2addsNPCCommandandpassesN2. No native/client builds/profile/timer/version/deploy/artwork.'],
        limits=['Choice/rank/RNG is proposed native encounter mapping,not recovered current D2R aura producer/name seed. Include in complete tested Bind adaptation before learned admission. Historical null grants remain explicit legacy boundary.',
            'Full Bind underlings/sunder/life-buff/species/channel/chance/quality/progress/payment/cancel/late-callback/lifecycle/composite approval remain open. Four earlier proposals pending/frozen.',
            'No general item-aura equipment aggregation,current NPC modifier/property/PvP/client/globalworld/full-class certification or new socket ability input claimed.',
            'Installed D2R3.4.95032does not replace3.3.93847. Installed server artifacts/saves/configuration/client patches unchanged;final effects/audio/icons remain separately missing.'],inputs=inputs)
    target=ROOT/'tools/reference/bound-demon-aura-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Bound aura evidence stale'
    else:target.write_text(encoded)
    print('Bound aura:C3/I2/S3/N2,33markers/9connections/2reloads;five shared native providers/captured grant/NPCCommand verified;full Bind remains gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
