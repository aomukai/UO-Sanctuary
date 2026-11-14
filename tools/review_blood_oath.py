#!/usr/bin/env python3
"""Record pinned Blood Oath data and explicitly separate unresolved behavior."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    ref=ROOT/'tools/reference/d2r-named-equipment';raw=ref/'json/skills.json'
    manifest=json.loads((ref/'sources.json').read_text());assert sha(raw)==manifest['files']['json/skills.json']['sha256']
    owned=json.loads(raw.read_text())['378'];row=next(r for r in json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text())if r['sourceId']==378)
    assert row['sourceFields']=={k:v for k,v in owned.items()if not k.startswith('*')and k!='lineNumber'}
    assert owned['srvstfunc']==66 and owned['auraevent1']=='absorbresisteddamage'and owned['auraeventfunc1']==35
    assert owned['calc1']=='dm12'and owned['calc2']==3 and owned['pettype']=='demon'and owned['passive']==1
    capture=ROOT/'.local/class-skills/warlock-blood-oath-event-current-1/event-bodies-35.json';previous=json.loads(capture.read_text());event=previous['records'][0]
    assert event['slot']==35 and event['totalBytes']==920 and not event['identityVerified']and not event['allUnwindFragmentsCaptured']
    assert not any(f['captured']for f in event['fragments'])
    files=[raw,ref/'sources.json',ROOT/'data/Sanctuary/Balance/class-skills.json',capture,Path(__file__).resolve()]
    result=dict(referenceVersion='3.3.93847',component='Blood Oath source boundary and proposed adaptation inputs',
        skill=378,start=66,event=35,eventName='absorbresisteddamage',redirectFormula=owned['calc1'],rawCalc2=3,
        sourceCandidate=dict(rva=event['rva'],bytes=920,identityVerified=False,readableCodeBytes=0),
        newLiveReferenceReads=0,curvePolicy='Proposed:use effective-rank pinned dm12;not the older hard-point-only observed bug.',
        externalReferences=[dict(url='https://diabloforfans.com/skill/blood-oath?lang=enUS',kind='Author datamined reference',reportedVersion='3.3.93847',observedOn='2026-11-15',scope='Current version/table corroboration and resistance notes;no executable reaction ordering.'),
            dict(url='https://docs.google.com/document/d/1gtM3sQ1gzo8kpmTmiJ8CQAiqp7LLu0SeaWE6fUdVg6Y/edit?tab=t.0',kind='User-provided original tester notes',scope='Google view required JavaScript;PDF copy inspected instead.'),
            dict(url='https://upload3.inven.co.kr/upload/2026/03/02/bbs/WarlockSkillsNotesandFunctionality.pdf',kind='Original Warren/Kabryxis tested notes,PDFcopy',version='Unspecified;older upload',scope='Reports post-resistance forwarding that bypasses demon resistances,and a conflicting hard-point-only redirection bug. Not pinned-current executable evidence.'),
            dict(url='https://news.blizzard.com/en-us/article/24271875/reign-of-the-warlock-3-1-2-patch-notes',kind='Blizzard primary patch notes',scope='Confirms a historical lost-skill-point Blood Oath retention fix;does not establish current transfer formula or splitting.')],
        established=['Owned row supplies diminishing dm12,three-valued calc2,passive summon resistance and existing life/physical-resistance parameters.',
            'The saved candidate35capture has no readable code and no verified dispatcher identity. No fresh closed-client/protected-page read is justified.',
            'Existing summon life/resistance snapshots,post-mitigation SkillHealth and native fixed-damage bridge can be reused;UO BloodOathSpell is unrelated.'],
        unresolved=['Current callback formula/rank getter,meaning of calc2,recipient selection/splitting,low-life handling,damage stages/periodic/proc/lifecycle behavior. A tested explicit proposal must be approved before learned reaction is enabled.',
            'Older notes and pinned table conflict;neither is silently substituted for the other. Future Bind Demon receiver needs its own qualified saved actor.'],
        inputs={str(p.relative_to(ROOT)):sha(p)for p in files})
    path=ROOT/'tools/reference/blood-oath-source-review.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text,'Blood Oath source review stale'
    else:path.write_text(text)
    print('Blood Oath378/table/event35 boundary recorded;920candidate bytes unreadable;no live read or invented callback.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
