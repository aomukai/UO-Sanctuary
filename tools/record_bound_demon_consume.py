#!/usr/bin/env python3
"""Authenticate prepared numeric bound Consume,with aura recipes still gated."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bind-demon-consume'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];last=pin(STAGE/'source-snapshot-3.json')['inputs'];production=lambda x:{n:h for n,h in x['sourceInputs'].items()if not n.startswith('tests/')}
    assert production(first)==production(last)and first['projectInputs']==last['projectInputs']and first['copiedData']==last['copiedData']
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    previous=pin(ROOT/'.local/class-skills/bind-demon-oath/birth-inputs.json');parent=pin('tools/reference/bound-demon-oath-native-evidence.json')
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    new={'tests/Sanctuary.Integration/BoundDemonConsumeChecks.cs'};changed={'src/Sanctuary.Content/'+n+'.cs'for n in('BoundDemonHosts','ClassGear','ConsumeCasting','ConsumeEffects','ConsumePrepared','ConsumeRosterGump','NamedElements','NamedSkills')}
    assert set(now)-set(old)==new and not set(old)-set(now)and{n for n in old if old[n]!=now[n]}==changed
    assert born['copiedData']==previous['copiedData']and first['projectInputs']==pin(ROOT/'.local/class-skills/bind-demon-oath/source-snapshot-1.json')['inputs']['projectInputs']
    for kind,number in(('content',1),('integration',2)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text()for s in('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for name in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    rows=pin('data/Sanctuary/Balance/bind-demon-monsters.json');assert len(rows)==171
    kinds={s for row in rows for s in row['consumeStats']if s};assert len(kinds)==18
    auras=[row['monster']for row in rows if 'item_aura'in row['consumeStats']];assert auras==['Baal Subject 5','The Feature Creep','The Smith','megademon6']
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for family in('consume-suppliers','consume-world','bound-demon-oath','bound-demon-consume'):
        expected.update({family+'-check':1,family+'-check-result':1,family+'-reload-check':2,family+'-reload-check-result':2})
    text=e.cohort(STAGE,expected,33);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log');assert text.count('Client: 127.0.0.1: Connected.')==12
    for phrase in('Bound Consume:513captures across171source rows/three difficulties;16independent literal contexts cover17numeric stats;',
        'Bound Consume:actual native resistance/cache/caps including magic85,learned-only allskills and four real typed pierce hits pass;',
        'Bound Consume:actual wounded blessed parked Daemon selected through town gump/shared SC,retired with native item graph and bound ledger once,',
        'stale menu/cast creation identities refuse without payment;',
        'Bound Oath:actual mixed Goatman/Daemon/Tainted one25percent budget,whole and FP8 ordered shares,'):
        assert text.count(phrase)==1,phrase
    assert text.count('Bound Consume:saved logical original order/rank/fire25bonus/deadline/owner wounds and remaining summon birth/RNG survive reload;')==2
    saved=pin(STAGE/'bound-demon-consume-fixtures.json');assert saved['Generation']==2 and saved['Effect']['Suppliers']['Rank']==20
    assert saved['Effect']['Suppliers']['Monster']=='megademon1'and saved['Effect']['Suppliers']['Bonus']['FireResist']==25 and saved['Effect']['Suppliers']['Bonus']['AuraRank']==0
    assert saved['Life']==saved['Maximum']-513
    for name in('consume-suppliers-fixtures.json','consume-world-fixtures.json','bound-demon-oath-fixtures.json'):assert pin(STAGE/name)['Generation']==2
    archive=STAGE/'native-1';failed=pin(archive/'birth-inputs.json');pin(archive/'birth-native-checks.log');pin(archive/'native-run-1.log')
    assert 'Allskills failed learned-only/hard-point rules'in(archive/'birth-native-checks.log').read_text()
    assert failed['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
    for name,h in failed['sourceInputs'].items():assert e.sha(archive/'source'/name)==h,name
    frozen=[]
    for stem,proposal in(('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for name in(f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(name);assert e.sha(ROOT/name)==hashlib.sha256(subprocess.check_output(['git','show','b69410020db51d40e72f6a85dd254aeb42868b10:'+name],cwd=ROOT)).hexdigest();frozen.append(name)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert 'CombatAbility.BindDemon'not in code and 'CombatAbility.Consume'not in code
    assert 'internal static bool Approved=>false;'in(ROOT/'src/Sanctuary.Content/ConsumeEffects.cs').read_text()
    for name in(*sorted(new|changed),'tests/check_bound_demon_consume.py','tools/record_bound_demon_consume.py','docs/bind-demon-consume-checkpoint.md','docs/class-skill-roadmap.md',
        'docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Prepared numeric bound Consume and original native retirement',handlers=226,warlockHandlers=17,completedClasses=[],
        learnedBindDemonEnabled=False,learnedConsumeEnabled=False,nativeMarkers=33,markers=dict(expected),actualInputs=0,authenticatedSessions=12,boots=3,reloads=2,saves=3,
        catalogRows=171,catalogCaptureContexts=513,capturedStatKinds=18,numericKinds=17,numericCatalogRows=167,literalContexts=16,unsupportedAuraRecipes=auras,
        actualTownGumpResponse=True,actualNativeRetirement=True,immediateBoundLedgerRemoval=True,staleMenuAndCastOrderRefused=True,oldSixFieldJsonAccepted=True,
        numericWriterFixturesDistinctFromActorEligibility=True,retainedOriginalConsumeRegression=True,retainedOriginalSuppliers=True,retainedBoundOathRegression=True,
        contentBuild=1,integrationBuild=2,sourceSnapshot=3,nativeRun=2,nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,
        existingBonusOptionalFieldsAdded=16,newLiveReferenceReads=0,currentRuntimeBinderParity=False,frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['All171catalogue rows/18statkinds captured in513contexts;16literal cases exercise17numeric kinds,quotes/whitespace/caps and old6fieldJSON. Item aura names/ranks preserved but four recipes explicitly gated.',
            'Prepared numeric writer fixtures exercise actual native resists/cache/caps,magic85,learned-only allskills/hardpoint preservation,four actual typed pierce hits,atomic aura refusal and clear. Not a claim of named actor binding.',
            'Actual fresh Witch consumes wounded blessed parked native Daemon through townGump/sharedSC:44mana once,fire25 frozen,original body/items retired and ledger cleared,relative life retained.',
            'Menu and windup retain source creation order as well as serial;stale identities refuse unpaid. Remaining pet birth/RNG survives.',
            'Two reloads retain consumed logical slot/order/rank/bonus/deadline/owner wounds/cache and remaining pet;Hell65/Normal25rescale doesnot resurrect body or reset expiry. Original Consume/suppliers/bound Oath retain.',
            'Native1all-trained convenience player couldnot test unlearned skill. Exact source/DLL/log archive retained. I2fresh requiredtalents;Content1unchanged;Native2acceptance.'],
        limits=['Four item-aura recipes(HolyFire/Meditation/Salvation) remain mechanically gated. Implement shared Paladin provider next;do not admit numeric portion alone.',
            'Prepared numeric/world projection,not recovered current do159/Bind binder ordering. Maxmagic uses existing-style75/additive/cap95policy;current D2R getter not recovered. Named actors/source mappings still separately unqualified.',
            'Prior proposals remain pending/frozen;no full Bind proposal or learned admission. Native bound DeathMark,full channel/chance/payment/mappings/modifiers/underlings/sunder/life-buff capture remain unfinished.',
            'No native/client build,deployment,new artwork or installed-save/config write. Installed D2R3.4.95032does not replace pinned3.3.93847. Whole actor/property/PvP/client/globalworld/fullclass certification remains open.'],inputs=inputs)
    out=ROOT/'tools/reference/bound-demon-consume-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Bound Consume evidence stale'
    else:out.write_text(encoded)
    print('Bound Consume:C1/I2/S3/N2,33markers/12connections/2reloads;513captures/17numeric kinds/actual town retirement;four aura recipes remain gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
