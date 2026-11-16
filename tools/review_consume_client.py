#!/usr/bin/env python3
"""Qualify the one shared metadata change without repeating44patch roundtrips."""
import argparse,json,re
from collections import Counter
import skill_checkpoint_evidence as e
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/consume-learned';PARENT=ROOT/'.local/class-skills/blood-boil-learned'
def review(check=False):
    path=STAGE/'client-inputs-1.json';m=e.load(path);old=e.load(PARENT/'client-inputs-1.json');client=STAGE/'client-root/upstream/ClassicUO'
    assert m['intentionalSharedChanges']==['shared/ClassAbilities.cs']and m['addedPatches']==[]and m['patchHashes']==old['patchHashes']and m['clientInputs']==old['clientInputs']
    assert m['baseReceiptSha256']==e.sha(ROOT/m['baseReceipt'])and len(m['patchHashes'])==44
    for n,h in m['patchHashes'].items():assert e.sha(ROOT/n)==h,n
    for n,h in m['clientInputs'].items():assert e.sha(client/n)==h,n
    for n,h in m['sharedInputs'].items():assert e.sha(ROOT/n)==e.sha(STAGE/'client-root'/n)==h,n
    assert [n for n,h in m['sharedInputs'].items()if h!=old['sharedInputs'][n]]==m['intentionalSharedChanges']
    before=(PARENT/'client-root/shared/ClassAbilities.cs').read_text();after=(ROOT/'shared/ClassAbilities.cs').read_text()
    assert after==before.replace('AllyTarget(CombatAbility value) => value is','AllyTarget(CombatAbility value) => value is CombatAbility.Consume or',1)
    log=STAGE/'client-build-1.log';text=log.read_text();assert'Build succeeded.'in text and'0 Error(s)'in text
    def warnings(s,root):
        return Counter(line.replace(str((root/'client-root').resolve()),'<stage>/client-root').replace(str(root),'<stage>')for line in s.splitlines()if re.search(r'warning [A-Z]+\d+:',line))
    oldtext=(PARENT/'client-build-2.log').read_text()
    current_warnings=warnings(text,STAGE);old_warnings=warnings(oldtext,PARENT)
    def source_warnings(values):return Counter({k:v for k,v in values.items()if not re.search(r'warning NU190[23]:',k)})
    def audit_messages(values):return {re.search(r'warning (NU190[23]): (.*?)(?: \[.*\])?$',k).groups()for k in values if re.search(r'warning NU190[23]:',k)}
    assert source_warnings(current_warnings)==source_warnings(old_warnings)
    assert audit_messages(current_warnings)==audit_messages(old_warnings)
    count=int(re.findall(r'(\d+) Warning\(s\)',text)[-1])
    baseline_count=int(re.findall(r'(\d+) Warning\(s\)',oldtext)[-1]);assert count==44 and baseline_count==44
    result=dict(referenceVersion='3.3.93847',component='Consume shared ally-target client classification',inputManifest=dict(path=str(path.relative_to(ROOT)),sha256=e.sha(path)),
        patches=44,changedClientPaths=[],addedPatches=[],intentionalSharedChanges=m['intentionalSharedChanges'],buildErrors=0,baselineWarningsIdentical=current_warnings==old_warnings,baselineSourceWarningsIdentical=True,packageAuditMessagesIdentical=True,baselineInheritedWarnings=baseline_count,
        inheritedWarningMessages=count,graphicalInputVerified=False,installed=False,
        scope='All accepted client paths/44patches unchanged;only shared AllyTarget adds Consume. Source warnings and audit-message sets are identical;both builds have44warning occurrences. No patch roundtrip replay or installation.',
        files={str(p.relative_to(ROOT)):e.sha(p)for p in (path,log,STAGE/'client/cuo.dll',STAGE/'client-source-1.tar.gz',ROOT/'tools/prepare_consume_client.py',ROOT/'tools/review_consume_client.py')})
    target=ROOT/'tools/reference/consume-learned-client-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Consume client evidence stale'
    else:target.write_text(encoded)
    print('Consume client:one shared AllyTarget change,44unchanged patches/client sources,identical source warnings/audit messages,44total versus44baseline/0errors;not installed.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
