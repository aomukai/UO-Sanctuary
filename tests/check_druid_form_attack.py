#!/usr/bin/env python3
"""Prepared form A1clock/melee consumers with retained shape and saved form checks."""
import check_creeper_stats as retained

if __name__ == '__main__':
    retained.STAGE = retained.ROOT / '.local/class-skills/druid-form-attack'
    retained.check(
        extra_commands=['druid-form-model-check', 'druid-form-world-check', 'shape-restriction-check', 'druid-form-rate-check', 'druid-form-attack-check', 'druid-form-live-check', 'grizzly-ai-check', 'grizzly-stats-check', 'dire-wolf-stats-check', 'battle-orders-check'],
        extra_reload_commands=['druid-form-world-reload-check', 'shape-restriction-reload-check', 'druid-form-attack-reload-check', 'battle-orders-reload-check'],
        extra_before_save_commands=['druid-form-world-snapshot-check', 'druid-form-expiry-stage-check'],
        extra_fixtures=['druid-form-world-fixtures.json', 'druid-form-expired-fixtures.json', 'battle-orders.json'],
        extra_probes=['shape-restrictions', 'druid-form-attack-clock', 'druid-form-modes', 'modern-form-progress', 'grizzly-ai', 'grizzly-creation', 'grizzly-attack', 'dire-wolf-creation', 'druid-beast-attack'],
        extra_wait_results={'druid-form-live-check': 'druid-form-live-result', 'battle-orders-check': 'battle-orders-result', 'battle-orders-reload-check': 'battle-orders-result'},
        between_boot_delay=5)
    print('Prepared form A1and melee consumers pass3boots/2reloads with retained shape/resource/summon checks; learned forms remain disabled.')
