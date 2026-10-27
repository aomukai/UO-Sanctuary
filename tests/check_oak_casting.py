#!/usr/bin/env python3
"""Learned Oak casting and NPC skill rank with retained saved motion/aura acceptance."""
import check_creeper_stats as retained


if __name__ == '__main__':
    retained.STAGE = retained.ROOT / '.local/class-skills/oak-casting'
    retained.check(extra_commands=['oak-rank-check', 'oak-casting-check', 'totem-ai-check', 'oak-sage-stats-check', 'oak-sage-body-check', 'oak-world-check', 'battle-orders-check'],
        extra_reload_commands=['oak-expired-reload-check', 'oak-rank-reload-check', 'oak-casting-reload-check', 'oak-sage-body-reload-check', 'oak-world-reload-check', 'battle-orders-reload-check'],
        extra_probes=['oak-sage', 'oak-aura', 'totem-ai', 'oak-skill-rank'],
        extra_fixtures=['oak-sage-body-fixtures.json', 'oak-world-fixtures.json', 'oak-expired-aura-fixtures.json', 'oak-rank-fixtures.json', 'oak-casting-fixtures.json'],
        extra_live_commands=['oak-world-live'], extra_before_save_commands=['oak-expired-stage'], extra_input_commands=['oak-right', 'oak-quick'])
    print('Learned Oak: three boots/two reloads, three right and three quick casts, NPC Command rank and retained natural motion/expiry pass; no class certificate or installation claimed.')
