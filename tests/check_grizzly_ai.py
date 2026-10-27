#!/usr/bin/env python3
"""Prepared AI112 plus Grizzly/Dire arithmetic and real item consumers in one boot."""
import export_runtime_audits as native

if __name__ == '__main__':
    native.export(native.ROOT / '.local/class-skills/grizzly-ai',
        extra_commands=['grizzly-ai-check', 'grizzly-stats-check', 'dire-wolf-stats-check'],
        extra_probes=['grizzly-ai', 'grizzly-creation', 'grizzly-attack', 'dire-wolf-creation', 'druid-beast-attack'])
    print('Grizzly AI112:2016native comparisons; retained Grizzly/Dire arithmetic and item consumers pass one isolated boot. No new actor/sequence/save/input acceptance claimed.')
