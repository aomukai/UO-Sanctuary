#!/usr/bin/env python3
"""Authenticate prepared chance/channel composition without learned admission."""
import argparse
from collections import Counter
import hashlib,json,subprocess
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bind-demon-channel'
PARENT='43c0ac8b2325d246d798f35ec22b2ad3a3ee1fa4'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str) else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text()) if p.suffix=='.json' else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-7.json',STAGE/'birth-inputs.json')
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    parent=pin('tools/reference/bound-demon-life-native-evidence.json');previous=pin(ROOT/'.local/class-skills/bind-demon-life/birth-inputs.json')
    old={n:h for n,h in previous['sourceInputs'].items() if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items() if n.endswith('.cs')}
    new={'src/Sanctuary.Content/BindDemonChance.cs','src/Sanctuary.Content/BindDemonChannel.cs','tests/Sanctuary.Integration/BindDemonChannelChecks.cs'}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ('BoundDemonHosts','MonsterVariantBaselines','SkillMechanics')}
    assert set(now)-set(old)==new and not set(old)-set(now) and {n for n in old if old[n]!=now[n]}==changed and born['copiedData']==previous['copiedData']
    for kind,number in (('content',4),('integration',4)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    built=pin(STAGE/'source-snapshot-5.json')['inputs']['sourceInputs'];assert {n:h for n,h in built.items() if not n.startswith('tests/')}=={n:h for n,h in now.items() if not n.startswith('tests/')}
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for family in ('bound-demon-host','bound-demon-life','bound-demon-aura','bind-demon-channel'):
        expected.update({family+'-check':1,family+'-check-result':1,family+'-reload-check':2,family+'-reload-check-result':2})
    text=e.cohort(STAGE,expected,33);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-4.log');assert text.count('Client: 127.0.0.1: Connected.')==12
    for phrase in ('Bind channel:all171species strict health gates/offcycle/captured chance/maxhp controls;',
        'Bind channel:nonmutating preflight draws no aura RNG/baseline;',
        'Bind channel:insufficient start/attempt mana,movement/paralysis/target range and held lease expiry cancel without new debit;',
        'Bind channel:late cancellation retains spent attempt;'):
        assert text.count(phrase)==1,phrase
    assert text.count('Bind channel:saved original bound snapshot/wounds/resources survive reload;')==2
    for family in ('bound-demon-host','bound-demon-life','bound-demon-aura','bind-demon-channel'):assert pin(STAGE/(family+'-fixtures.json'))['Generation']==2
    saved=pin(STAGE/'bind-demon-channel-fixtures.json');assert saved['State']['Actor']==saved['Bound'] and saved['State']['Birth']['Rank']==20 and saved['State']['Aura'] is not None and saved['Mana']==81*256
    archive_inputs=[]
    for number in (1,2,3):
        archive=STAGE/f'native-{number}';earlier=pin(archive/'birth-inputs.json');archive_inputs.append(earlier)
        log=archive/'birth-native-checks.log';pin(log);pin(archive/f'native-run-{number}.log');message=log.read_text()
        assert ('Unknown Bind chance context: sksrc' if number==1 else 'bind-demon-channel-check-result: passed') in message
        if number==3:assert 'Reload altered owner mana 21760 expected 20736' in message
        for n,h in earlier['sourceInputs'].items():assert e.sha(archive/'source'/n)==h,n
        for kind in ('Content','Integration'):assert e.sha(archive/kind.lower()/f'Sanctuary.{kind}.dll')==earlier['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    assert archive_inputs[2]['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
    early={n:h for n,h in archive_inputs[2]['sourceInputs'].items() if n.endswith('.cs')};assert set(early)==set(now) and {n for n in now if now[n]!=early[n]}=={'tests/Sanctuary.Integration/BindDemonChannelChecks.cs'}
    for i in range(1,8):pin(STAGE/f'source-snapshot-{i}.json')
    for kind,count in (('content',4),('integration',4)):
        for i in range(1,count+1):
            log=STAGE/f'{kind}-build-{i}.log';pin(log);assert all(s in log.read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    review=pin('tools/reference/bind-demon-channel-rule-review.json')
    for n,h in review['inputs'].items():assert e.sha(ROOT/n)==h,n
    for n in (*sorted(new|changed),'tests/check_bind_demon_channel.py','tools/record_bind_demon_channel.py','docs/bind-demon-channel-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert 'CombatAbility.BindDemon' not in code and 'CombatAbility.Consume' not in code
    result=dict(referenceVersion='3.3.93847',component='Prepared Bind chance/channel and read-only original host qualification',handlers=226,warlockHandlers=17,completedClasses=[],learnedBindDemonEnabled=False,
        nativeMarkers=33,markers=dict(expected),actualInputs=0,authenticatedSessions=12,boots=3,reloads=2,saves=3,contentBuild=4,integrationBuild=4,sourceSnapshot=7,nativeRun=4,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,
        actualNativeBodies=True,preparedChanceSpecies=171,currentD2RChanceOrChannelParity=False,fullBindAdaptationApproved=False,
        frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],preservation={**e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43),'installed':False},
        established=['All171rawspecies strict health/offcycle/captured skill-term/maxhp controls pass;named hard20 and champion10/unique-minion15 gates retained. Explicit grouped health term produces85/77/80/85/77 at half life;this is proposed interpretation,not raw compiler parity.',
            'Shared formula remainder supports multiplicative precedence/inactive branch safety. Only isolated captured Bind context maps sksrc. Read-only qualification/Normal preview allocates no ownership/baseline/aura RNG before success.',
            'Prepared one-second/fixednine-mana attempts use native exact mana and actual original host. Free press/heartbeat,failed roll payment,low mana,locked target,range/movement/paralysis/lease cancellation,no repeated deadline/lag catch-up pass.',
            'Late cancellation retains released debit;old callback/renewal cannot remove fresh channel,newer same-cast attempt cannot be overwritten. Invalid RNG refuses. Successful roll captures original wounded body andone aura choice through unchanged host consumers.',
            'Original host/life-buff/aura/item/NPC resource controls retain;original binding/wounds/resources survive two reloads with no held intent/offline roll or debit. C4unchanged afterN3;I4freezes onlyfixture mana regeneration.',
            'N1source-context failure,N2combined reload failure,N3diagnostic owner85vs81normal regeneration archived with exact sources/DLL/logs. All accepted and prior builds clean.'],
        limits=['One-second/fixednine-at-all-ranks/payment/progress12/groupedcomparison/native quality0/world lifecycle contexts are unapproved prepared adaptations;rawshifted costs do not prove them. No learned input or automatic timer/world scheduling installed.',
            'Complete SQ6/input/world/quality/native-named species/sunder/underling composite and tested fullBindproposal remain unfinished. Underling cap inference does not produce packspawns. Four earlier proposals remain pending/frozen.',
            '226handlers/Warlock17/no class certified;zero actual ability socket input. No general PvP/equipment/global-world/full-class certification or new live D2Rcapture.',
            'Reference3.3.93847 unchanged despite newerinstalled3.4.95032;installedserver artifacts/saves/config/clientpatches unchanged;no artwork and separate final effect/icon registers.'],inputs=inputs)
    target=ROOT/'tools/reference/bind-demon-channel-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Bind channel evidence stale'
    else:target.write_text(encoded)
    print('Bind channel:C4/I4/S7/N4,33markers/12connections/2reloads;171species/prepared native action verified;full Bind remains gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
