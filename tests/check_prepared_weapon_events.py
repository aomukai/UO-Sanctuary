#!/usr/bin/env python3
"""Prepared weapon recovery ordering with retained spirit acceptance."""
import check_creeper_stats as retained

if __name__ == '__main__':
    retained.STAGE = retained.ROOT / '.local/class-skills/weapon-event-phase'
    retained.check(extra_commands=['prepared-weapon-event-check', 'weapon-leech-check', 'jab-check', 'power-strike-check', 'wolverine-stats-check', 'wolverine-aura-check', 'wolverine-world-check', 'wolverine-casting-check', 'oak-rank-check', 'oak-casting-check', 'totem-ai-check', 'oak-sage-stats-check', 'oak-sage-body-check', 'oak-world-check', 'battle-orders-check'],
        extra_reload_commands=['prepared-weapon-event-reload-check', 'jab-reload-check', 'power-strike-reload-check', 'wolverine-aura-reload-check', 'wolverine-world-reload-check', 'wolverine-casting-reload-check', 'oak-expired-reload-check', 'oak-rank-reload-check', 'oak-casting-reload-check', 'oak-sage-body-reload-check', 'oak-world-reload-check', 'battle-orders-reload-check'],
        extra_probes=['jab', 'weapon-leech', 'heart-of-wolverine', 'wolverine-aura', 'oak-sage', 'oak-aura', 'totem-ai', 'oak-skill-rank'],
        extra_fixtures=['prepared-weapon-event-fixtures.json', 'jab-fixtures.json', 'power-strike-fixtures.json', 'wolverine-aura-fixtures.json', 'wolverine-world-fixtures.json', 'wolverine-casting-fixtures.json', 'oak-sage-body-fixtures.json', 'oak-world-fixtures.json', 'oak-expired-aura-fixtures.json', 'oak-rank-fixtures.json', 'oak-casting-fixtures.json'],
        extra_probe_files=['power-strike-scalar-probe.json', 'power-strike-flow-probe.json'], extra_live_commands=['oak-world-live', 'wolverine-world-live'], extra_before_save_commands=['oak-expired-stage'], extra_input_commands=['oak-right', 'oak-quick', 'wolverine-right', 'wolverine-quick'])
    print('Prepared recovery phase: native/typed/root healing rescue, ordinary/captured leech and clip controls pass across three boots/two reloads with retained learned spirits; original HP/death chronology and Barbs remain unfinished.')
