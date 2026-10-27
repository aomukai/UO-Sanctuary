#!/usr/bin/env python3
"""Shape availability/cast guards,header modes and actual saved form lifecycle."""
import check_creeper_stats as retained

if __name__ == '__main__':
    retained.STAGE = retained.ROOT / '.local/class-skills/shape-restrictions'
    retained.check(
        extra_commands=['druid-form-model-check', 'druid-form-world-check', 'shape-restriction-check', 'druid-form-rate-check', 'druid-form-live-check', 'grizzly-ai-check', 'grizzly-stats-check', 'dire-wolf-stats-check', 'battle-orders-check'],
        extra_reload_commands=['druid-form-world-reload-check', 'shape-restriction-reload-check', 'battle-orders-reload-check'],
        extra_before_save_commands=['druid-form-world-snapshot-check', 'druid-form-expiry-stage-check'],
        extra_fixtures=['druid-form-world-fixtures.json', 'druid-form-expired-fixtures.json', 'battle-orders.json'],
        extra_probes=['shape-restrictions', 'druid-form-modes', 'modern-form-progress', 'grizzly-ai', 'grizzly-creation', 'grizzly-attack', 'dire-wolf-creation', 'druid-beast-attack'],
        extra_wait_results={'druid-form-live-check': 'druid-form-live-result', 'battle-orders-check': 'battle-orders-result', 'battle-orders-reload-check': 'battle-orders-result'},
        between_boot_delay=5)
    print('Native shape restrictions and real cast boundaries pass3boots/2reloads with retained saved form/resource/summon consumers; learned forms remain disabled.')
