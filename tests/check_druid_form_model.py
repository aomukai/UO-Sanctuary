#!/usr/bin/env python3
"""Pure player-form comparisons plus retained Grizzly/Dire and item consumers."""
import hashlib
import json
import shutil
import export_runtime_audits as native

if __name__ == '__main__':
    base = native.ROOT / '.local/class-skills'
    fixture = base / 'druid-forms-source-contexts.json'
    proof = json.loads((native.ROOT / 'tools/reference/druid-forms-engine-evidence.json').read_text())
    assert hashlib.sha256(fixture.read_bytes()).hexdigest() == proof['fixtureSha256']
    # The shared native exporter expects a -source-probe.json fixture name.
    alias = base / 'druid-forms-source-probe.json'
    if alias.exists():
        assert alias.read_bytes() == fixture.read_bytes()
    else:
        shutil.copy2(fixture, alias)
    native.export(base / 'druid-form-model',
        extra_commands=['druid-form-model-check', 'grizzly-ai-check', 'grizzly-stats-check', 'dire-wolf-stats-check'],
        extra_probes=['druid-forms', 'grizzly-ai', 'grizzly-creation', 'grizzly-attack', 'dire-wolf-creation', 'druid-beast-attack'])
    print('Pure player forms350source comparisons and retained Grizzly/Dire/items pass one disposable boot; no saved player forms or casting installed.')
