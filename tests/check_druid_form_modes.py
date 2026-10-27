#!/usr/bin/env python3
"""Header-backed pure form modes plus retained arithmetic and real item consumers."""
import export_runtime_audits as native

if __name__ == '__main__':
    native.export(native.ROOT / '.local/class-skills/druid-form-modes',
        extra_commands=['druid-form-model-check', 'grizzly-ai-check', 'grizzly-stats-check', 'dire-wolf-stats-check'],
        extra_probes=['druid-form-modes', 'grizzly-ai', 'grizzly-creation', 'grizzly-attack', 'dire-wolf-creation', 'druid-beast-attack'])
    print('Header-backed player CAST10and350pure form comparisons pass one disposable boot; saved world acceptance remains separate.')
