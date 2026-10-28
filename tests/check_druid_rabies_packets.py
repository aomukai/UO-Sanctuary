#!/usr/bin/env python3
"""Prepared Rabies packet comparisons in one disposable native server boot."""
import hashlib
import json
import export_runtime_audits as native

if __name__ == '__main__':
    base = native.ROOT / '.local/class-skills'
    for name in ['factory', 'spread']:
        evidence = json.loads((native.ROOT / f'tools/reference/druid-rabies-{name}-engine-evidence.json').read_text())
        fixture = native.ROOT / evidence['fixture']['path']
        assert hashlib.sha256(fixture.read_bytes()).hexdigest() == evidence['fixture']['sha256']
    native.export(base / 'druid-rabies-packets', extra_commands=['druid-rabies-packet-check'],extra_probes=['druid-rabies-factory','druid-rabies-spread'])
    print('Rabies prepared packet comparisons pass one disposable boot; no native casting/spread handler enabled or installed.')
