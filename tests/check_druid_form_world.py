#!/usr/bin/env python3
"""Prepared saved player-form stats plus retained summon/resource consumers."""
import check_creeper_stats as retained

if __name__ == '__main__':
    retained.STAGE = retained.ROOT / '.local/class-skills/druid-form-world'
    retained.check(
        extra_commands=['druid-form-model-check', 'druid-form-world-check', 'druid-form-live-check', 'grizzly-ai-check', 'grizzly-stats-check', 'dire-wolf-stats-check', 'battle-orders-check'],
        extra_reload_commands=['druid-form-world-reload-check', 'battle-orders-reload-check'],
        extra_before_save_commands=['druid-form-world-snapshot-check', 'druid-form-expiry-stage-check'],
        extra_fixtures=['druid-form-world-fixtures.json', 'druid-form-expired-fixtures.json', 'battle-orders.json'],
        extra_probes=['druid-forms', 'grizzly-ai', 'grizzly-creation', 'grizzly-attack', 'dire-wolf-creation', 'druid-beast-attack'],
        extra_wait_results={'druid-form-live-check': 'druid-form-live-result', 'battle-orders-check': 'battle-orders-result', 'battle-orders-reload-check': 'battle-orders-result'},
        between_boot_delay=5)
    print('Prepared saved Druid player-form pools and retained consumers pass3boots/2actual reloads; learned forms and modern rate/restriction/FPmaximum acceptance remain pending.')
