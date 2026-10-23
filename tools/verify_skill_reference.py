#!/usr/bin/env python3
"""Verify the immutable D2R skill reference offline; optionally repeat the extraction comparison."""
import argparse
import csv
import hashlib
import io
import json
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'tools/reference/d2r-named-equipment'
PIN = ROOT / 'data/Sanctuary/skill-reference.json'
EXCEL_COMMIT = '1f16064e09b97e3e65abd6943662207cff00b07f'
TABLE_KEYS = {'skills':'skill','charstats':'class','monstats':'Id','monlvl':'Level','missiles':'Missile','monstats2':'Id','pettype':'pet type','monequip':'monster'}


def compare():
    result = {}
    for name, key in TABLE_KEYS.items():
        url = f'https://raw.githubusercontent.com/pinkufairy/D2R-Excel/{EXCEL_COMMIT}/{name}.txt'
        raw = urllib.request.urlopen(url, timeout=30).read()
        rows = list(csv.DictReader(io.StringIO(raw.decode('utf-8-sig')), delimiter='\t'))
        source = json.loads((SOURCE / f'json/{name}.json').read_text())
        # Some extraction table headers differ in case; normalize only whitespace/header case.
        clean = lambda row: {str(k).strip().casefold():str(v).strip().strip('"').strip() for k,v in row.items() if k is not None}
        normalized = [clean(r) for r in rows]
        k = key.casefold()
        if k not in normalized[0]:
            if name == 'pettype': k = 'pet type' if 'pet type' in normalized[0] else 'pettype'
        by_key = {r.get(k):r for r in normalized}
        differences = []
        checked = 0
        annotations = {}
        for index, row in source.items():
            if name == 'skills' and not row.get('charclass'): continue
            src = clean(row)
            # MonEquip repeats monster identities; source order determines which
            # rank threshold fills a slot first. Never collapse those rows.
            other = normalized[int(index)] if name == 'monequip' else by_key.get(src.get(k))
            if other is None: differences.append([src.get(k),'missing row']); continue
            for field,value in src.items():
                if (field.startswith('*') and field not in ('*id','*hcidx')) or field == 'linenumber': continue
                if field in ('expansion','areaid') and field not in other:
                    annotations[field] = annotations.get(field,0) + 1
                    continue
                checked += 1
                if value != other.get(field): differences.append([src.get(k),field,value,other.get(field)])
        result[name] = dict(url=url,sha256=hashlib.sha256(raw).hexdigest(),checkedFields=checked,extractionAnnotationsNotInTsv=annotations,differences=differences)
    return result


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--cross-check',action='store_true');parser.add_argument('--record',action='store_true');args=parser.parse_args()
    meta=json.loads((SOURCE/'sources.json').read_text())
    for relative,pin in meta['files'].items():
        if hashlib.sha256((SOURCE/relative).read_bytes()).hexdigest()!=pin['sha256']:raise SystemExit('Changed source: '+relative)
    if args.cross_check:
        result=compare()
        for name,row in result.items(): print(name,row['checkedFields'],'fields;',len(row['differences']),'differences',row['differences'][:4])
        if args.record:
            if any(r['differences'] for r in result.values()):raise SystemExit('Reference mismatch; refusing to pin an unsupported version')
            data=dict(schemaVersion=1,game='Diablo II: Resurrected',edition='Reign of the Warlock',version='3.3.93847',
                sourceRepository=meta['repository'],sourceCommit=meta['commit'],sourceManifest='tools/reference/d2r-named-equipment/sources.json',
                corroboratingRepository='https://github.com/pinkufairy/D2R-Excel',corroboratingCommit=EXCEL_COMMIT,
                versionEvidence='Merge commit message: 3.3.93847; table contents compared with immutable TSV extraction.',
                comparison=result,precedence='Explicit Sanctuary decisions override this reference. Legacy D2MOO documents engine behavior only; it is not the D2R numeric baseline.',
                warlock=dict(sourceSkillIds=list(range(373,403)),classCode='war',sourceTabs=[21,22,23],femaleDisplayName='Witch'),
                unknowns=['The source snapshot is a data extraction, not an executable behavioral oracle; engine-only rules need separate verification.'])
            PIN.write_text(json.dumps(data,indent=2)+'\n')
    else:
        pin=json.loads(PIN.read_text())
        if pin['sourceCommit']!=meta['commit'] or pin['version']!='3.3.93847':raise SystemExit('Reference pin mismatch')
        for row in pin['comparison'].values():
            if row['differences']:raise SystemExit('Unresolved reference table mismatch')
        print('D2R 3.3.93847 source hashes and recorded extraction comparison verified offline.')


if __name__=='__main__':main()
