#!/usr/bin/env python3
"""Authenticate shared bound recipients and prepared Blood Boil,not admission."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bind-demon-recipients'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];last=pin(STAGE/'source-snapshot-4.json')['inputs']
    production=lambda d:{n:h for n,h in d['sourceInputs'].items()if not n.startswith('tests/')}
    assert production(first)==production(last)and first['projectInputs']==last['projectInputs']and first['copiedData']==last['copiedData']
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    previous=pin(ROOT/'.local/class-skills/bind-demon-typed/birth-inputs.json');parent=pin('tools/reference/bound-demon-typed-native-evidence.json')
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    new={'src/Sanctuary.Content/WarlockDemonRecipients.cs','tests/Sanctuary.Integration/BoundDemonRecipientChecks.cs'}
    changed={'src/Sanctuary.Content/EngorgeCasting.cs','src/Sanctuary.Content/BloodBoilPrepared.cs'}
    assert set(now)-set(old)==new and not set(old)-set(now)and{n for n in old if old[n]!=now[n]}==changed
    assert born['copiedData']==previous['copiedData']
    old_snapshot=pin(ROOT/'.local/class-skills/bind-demon-typed/source-snapshot-3.json')['inputs'];assert first['projectInputs']==old_snapshot['projectInputs']
    for kind,number in(('content',1),('integration',4)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text()for s in('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for name in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for family in('engorge-world','blood-boil-world','bound-demon-typed','bound-demon-recipient'):
        expected.update({family+'-check':1,family+'-check-result':1,family+'-reload-check':2,family+'-reload-check-result':2})
    text=e.cohort(STAGE,expected,33);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-4.log')
    assert text.count('Client: 127.0.0.1: Connected.')==12
    for phrase in('Bound recipients:original Goatman/bound Daemon/original Tainted retain global creation order;',
        'actual mixed three-body Blood Boil pays5percent per body once with one shared area hit and no self attack event',
        'Bound recipients:single original Daemon at10percent pays lethal15percent resource cost;',
        'actual Dagger.OnHit keeps0physical50fire50poison and adds captured75physical once before290percentED'):
        assert text.count(phrase)==1,phrase
    assert text.count('Bound recipients:same injured original/bound mixed roster and creation order survive reload;')==2
    saved=pin(STAGE/'bound-demon-recipient-fixtures.json');assert saved['Generation']==2 and len(saved['Summons'])==2
    assert saved['State']['Actor']==saved['Bound']and saved['State']['RuntimeType']=='Server.Mobiles.Daemon'
    assert len({saved['Bound'],*(p['Serial']for p in saved['Summons'])})==3
    assert 0<saved['BoundLife']<saved['State']['BaseMaximum256']//2
    for name in('engorge-world-fixtures.json','blood-boil-world-fixtures.json','bound-demon-typed-fixtures.json'):
        assert pin(STAGE/name)['Generation']==2
    failures={1:'No independent fixture corridor near 7200,3500',2:'Mixed roster self-cost/area/typed damage/shared delay failed',3:'Mixed roster:selfEvents0,recipients[0,0,0],HP0'}
    for number,phrase in failures.items():
        archive=STAGE/f'native-{number}';failed=pin(archive/'birth-inputs.json');pin(archive/'birth-native-checks.log');pin(archive/f'native-run-{number}.log')
        assert phrase in(archive/'birth-native-checks.log').read_text()
        assert failed['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name,h in failed['sourceInputs'].items():assert e.sha(archive/'source'/name)==h,name
    # Pending proposals/evidence remain their exact historical review scope.
    frozen=[]
    for stem in('blood-oath-damage','engorge-world','blood-boil-world','consume-world'):
        proposal={'engorge-world':'engorge-world-rule','blood-boil-world':'blood-boil-rule','consume-world':'consume-world-rule'}.get(stem,stem)
        for name in(f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(name);old_bytes=subprocess.check_output(['git','show','a123db301c8a9884915b815cd1ae8ac348822826:'+name],cwd=ROOT)
            assert e.sha(ROOT/name)==hashlib.sha256(old_bytes).hexdigest();frozen.append(name)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0]
    assert 'CombatAbility.BindDemon'not in code and 'CombatAbility.BloodBoil'not in code
    assert 'internal static bool Approved=>false;'in(ROOT/'src/Sanctuary.Content/BloodBoilPrepared.cs').read_text()
    for name in(*sorted(new|changed),'tests/check_bound_demon_recipients.py','tools/record_bound_demon_recipients.py',
        'docs/bind-demon-recipients-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md',
        'docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Qualified original/bound demon recipients and prepared mixed Blood Boil',handlers=226,warlockHandlers=17,completedClasses=[],
        learnedBindDemonEnabled=False,learnedBloodBoilEnabled=False,completeBindRuleApproved=False,nativeMarkers=33,markers=dict(expected),actualInputs=0,
        authenticatedSessions=12,boots=3,reloads=2,saves=3,mixedRosterCasts=3,lethalBoundCostCasts=1,retainedEngorgeRegression=True,retainedOriginalBloodBoilRegression=True,
        retainedTypedNativeDamageRegression=True,originalBodyIdentityPreserved=True,globalCreationOrderPreserved=True,
        contentBuild=1,integrationBuild=4,sourceSnapshot=4,nativeRun=4,nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],
        existingSerializationVersionsChanged=False,newLiveReferenceReads=0,currentRuntimeBinderParity=False,
        frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Shared recipient ownership/active/range checks retain distinct original-controller and saved bound-native identity without replacing AI or unchecked body casts.',
            'Actual Witch mixed Goatman/Daemon/Tainted roster preserves global creation order,pays5percent maximum-life per body once and one mana cost,with one typed area hit under shared delay and no self attack event.',
            'Range,blessing,foreign owner,original controller fault and parking guards pass. Slots remain distinct from active recipient eligibility.',
            'Actual lone native Daemon at10percent life dies to15percent maximum-life resource cost;captured explosion still hits and ledger cleanup retires it.',
            'Original bound body/items/resources/birth and original summon snapshots/RNG/wounds/order retain through two reloads and subsequent actual mixed casts.',
            'Retained complete Engorge,original three-species Blood Boil and actual Imp typed native damage/save families pass. Content1unchanged across all runs;three archived fixture diagnostics precede acceptedNative4.'],
        limits=['Prepared extension,not recovered current D2R binder/recipient ordering or approval of pending Blood Boil. Learned routes stay gated.',
            'Bound Engorge buff/storage/recovery/leech,BloodOath incoming,Consume payload and DeathMark placement/commands remain unfinished. Engorge delegation retains original species only.',
            'Full Bind channel/chance/payment,native reward/named mappings,source modifiers/underlings/sunder and active-life-buff capture remain unfinished;no complete Bind proposal yet.',
            'No native/client rebuild,deployment,new artwork or installed-save/config write. Installed D2R3.4.95032does not replace pinned3.3.93847. Property/PvP/client/global-world/fullclass certification remains open.'],inputs=inputs)
    out=ROOT/'tools/reference/bound-demon-recipients-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Bound recipient evidence stale'
    else:out.write_text(encoded)
    print('Bound recipients:C1/I4/S4/N4,33markers/12connections/2reloads;mixed native Blood Boil/lethal cost/shared eligibility;learned routes gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
