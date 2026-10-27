#!/usr/bin/env python3
"""Shared spirit birth/body/motion and Wolverine combat-aura acceptance."""
import check_creeper_stats as retained

if __name__ == '__main__':
    retained.STAGE = retained.ROOT / '.local/class-skills/wolverine'
    retained.check(extra_commands=['wolverine-stats-check', 'wolverine-aura-check', 'wolverine-world-check', 'oak-rank-check', 'oak-casting-check', 'totem-ai-check', 'oak-sage-stats-check', 'oak-sage-body-check', 'oak-world-check', 'battle-orders-check'],
        extra_reload_commands=['wolverine-aura-reload-check', 'wolverine-world-reload-check', 'oak-expired-reload-check', 'oak-rank-reload-check', 'oak-casting-reload-check', 'oak-sage-body-reload-check', 'oak-world-reload-check', 'battle-orders-reload-check'],
        extra_probes=['heart-of-wolverine', 'wolverine-aura', 'oak-sage', 'oak-aura', 'totem-ai', 'oak-skill-rank'],
        extra_fixtures=['wolverine-aura-fixtures.json', 'wolverine-world-fixtures.json', 'oak-sage-body-fixtures.json', 'oak-world-fixtures.json', 'oak-expired-aura-fixtures.json', 'oak-rank-fixtures.json', 'oak-casting-fixtures.json'],
        extra_live_commands=['oak-world-live', 'wolverine-world-live'], extra_before_save_commands=['oak-expired-stage'], extra_input_commands=['oak-right', 'oak-quick'])
    print('Wolverine prepared body/motion and two-stat aura pass three boots/two reloads; learned handler remains pending, installed main unchanged.')
