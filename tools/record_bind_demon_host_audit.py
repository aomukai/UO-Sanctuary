#!/usr/bin/env python3
"""Authenticate native host classification and the combined pending Bind rule."""
import argparse
from collections import Counter
import hashlib,json,subprocess
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bind-demon-host-audit';PARENT='2cbbe200e8359bfaeeefb0d894c352d05e8e3d7b'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str) else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    parent=pin('tools/reference/bind-demon-input-native-evidence.json');previous=pin(ROOT/'.local/class-skills/bind-demon-input/birth-inputs.json')
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    new={'tests/Sanctuary.Integration/BindDemonHostAuditChecks.cs'};changed={'src/Sanctuary.Content/'+n+'.cs'for n in ('NamedCombat','ReviveCasts','BoundDemonHosts')}
    assert set(now)-set(old)==new and not set(old)-set(now)and{n for n in old if old[n]!=now[n]}==changed and born['copiedData']==previous['copiedData']
    built=pin(STAGE/'source-snapshot-1.json')['inputs']['sourceInputs'];assert{n:h for n,h in built.items()if not n.startswith('tests/')}=={n:h for n,h in now.items()if not n.startswith('tests/')}
    for kind,number in (('content',1),('integration',2)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'revive-facet-check':1,'revive-facet-reload-check':2,
        'bind-demon-host-audit-check':1,'bind-demon-host-audit-check-result':1,'bind-demon-host-audit-reload-check':2,'bind-demon-host-audit-reload-check-result':2})
    text=e.cohort(STAGE,expected,18);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log');assert text.count('Client: 127.0.0.1: Connected.')==3
    for phrase in ('Bind host audit:native type inventory uses current profiles and exact Slayer type rules without constructing roster;',
        'Bind host audit:real Paragon flag/transform refuses ordinary-quality host/channel without payment,AI/body/baseline mutation or existing pet replacement;'):assert text.count(phrase)==1
    assert text.count('Bind host audit:original binding/resources and real Paragon flag persist;')==2
    saved=pin(STAGE/'bind-demon-host-audit-fixtures.json');assert saved['Generation']==2
    inventory=saved['Inventory'];rows=inventory['Demons'];sunder=inventory['SunderSources']
    assert inventory['CreatureDefinitions']==730 and len(rows)==35 and sum(r['Issue']is None for r in rows)==22 and sum(r['Issue']=='No pinned Bind species row'for r in rows)==13
    assert len(sunder)==18 and not any(r['Available']for r in sunder)and Counter(r['Base']for r in sunder)=={'cm3':12,'cs2':6}and all(not r['RandomSpawnable']for r in sunder if r['Base']=='cs2')
    archive=STAGE/'native-1';earlier=pin(archive/'birth-inputs.json');pin(archive/'birth-native-checks.log');pin(archive/'native-run-1.log')
    assert 'Current named acquisition unexpectedly supplies Sunder'in(archive/'birth-native-checks.log').read_text()
    for n,h in earlier['sourceInputs'].items():assert e.sha(archive/'source'/n)==h,n
    for kind in ('Content','Integration'):assert e.sha(archive/kind.lower()/f'Sanctuary.{kind}.dll')==earlier['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    assert earlier['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
    early={n:h for n,h in earlier['sourceInputs'].items()if n.endswith('.cs')};assert set(early)==set(now)and{n for n in now if now[n]!=early[n]}==new
    for n in ('source-snapshot-2.json','source-snapshot-3.json','integration-build-1.log'):pin(STAGE/n)
    client=pin('tools/reference/bind-demon-client-evidence.json');manifest=pin(ROOT/client['inputManifest']['path']);assert e.sha(ROOT/client['inputManifest']['path'])==client['inputManifest']['sha256']
    for n,h in manifest['patchHashes'].items():assert e.sha(ROOT/n)==h,n
    for n,h in manifest['sharedInputs'].items():assert e.sha(ROOT/n)==h,n
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    for n in (*sorted(new|changed),'tests/check_bind_demon_host_audit.py','tools/record_bind_demon_host_audit.py','docs/bind-demon-host-audit-checkpoint.md','docs/bind-demon-world-rule-proposal.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/necro-sunder-preflight.json'):pin(n)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert'CombatAbility.BindDemon'not in code
    result=dict(referenceVersion='3.3.93847',component='Native Bind host quality and acquisition audit',handlers=226,warlockHandlers=17,completedClasses=[],learnedBindDemonEnabled=False,
        nativeMarkers=18,markers=dict(expected),actualInputs=0,authenticatedSessions=3,boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=2,sourceSnapshot=3,nativeRun=2,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,
        nativeHostInventory=inventory,sourceCompatibilityIsLiveEligibility=False,combinedBindProposal='docs/bind-demon-world-rule-proposal.md',combinedBindProposalApproved=False,
        frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],preservation={**e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43),'installed':False},
        established=['Shared existing NativeQualityIssue preserves Revive Paragon message while Bind refuses its unclassified quality/stat context and guards explicit native elites. Real native Paragon transform refuses host/channel before ownership/payment/baseline mutation and keeps prior ordinary bound demon.',
            '730definitions inspected without constructing roster;exact current profile/Slayer predicate finds35native demon type candidates,22source-compatible and13missing pinned Bind species rows. This doesnot certify all22live actor contexts;explicit boss/summon/controller/gear guards remain.',
            'All18named Sunder variants unavailable:twelvecm3and sixrecipe-onlycs2. The renewed base is not covered by oldcm1/cm2/cm3exclusion;source-recipe acquisition is distinct. No reducer/value300interpretation or acquisition enablement added.',
            'Current encounter class explicitly undead/non-demon. Native ordinary bound identity/wounds/mana and wild Paragon flag survive2reloads;rejection keeps existingpet. OriginalRevive facet/item/NPC resource controls retain. Wild regeneration not falsely treated as parking.',
            'C1unchanged;N1audit wrongly assumed everycharm oldbase;I2corrects bases and capturesavailability. ExactN1sources/DLL/logs retained. Isolated44patch client/shared inputs unchanged;no new client/native build/profile/timer/artwork.'],
        limits=['Combined defined normal/native Bind action/world proposal now ready but pending design decision. Cost/timing/grouped chance/native host policies remain adaptations,not recovered current startup. No new actualBind socket/graphical/class certificate.',
            'Paragon/native elite quality/stat adapters,13unmapped native source categories,named hosts/underlings/nonzero Sunder and other flagged contexts remain open;do not invent clones or silent source remaps.',
            'Four earlier proposals remain pending/frozen. 226handlers/Warlock17/no class certified. Installed newerD2Rdoesnot replace3.3.93847;separate effects/icons remain missing and nonblocking.'],inputs=inputs)
    target=ROOT/'tools/reference/bind-demon-host-audit-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Bind host audit evidence stale'
    else:target.write_text(encoded)
    print('Bind host audit:C1/I2/S3/N2,18markers/3connections/2reloads;35native candidates/22source-compatible;18Sunder sources unavailable;combined Bind proposal pending.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
