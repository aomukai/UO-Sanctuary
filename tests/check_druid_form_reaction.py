#!/usr/bin/env python3
"""Form GH/BL source clocks, live FHR/FBR gear, saved identity and natural recovery."""
import check_creeper_stats as retained

if __name__ == '__main__':
    retained.STAGE = retained.ROOT / '.local/class-skills/druid-form-reaction'
    assert retained.STAGE.name == 'druid-form-reaction', 'Refuse to overwrite another accepted package'
    retained.check(
        extra_commands=['druid-form-reaction-check', 'druid-form-reaction-live-check', 'druid-form-interruption-check', 'druid-form-casting-check', 'druid-form-delay-live-check', 'druid-form-model-check', 'druid-form-world-check', 'shape-restriction-check', 'druid-form-rate-check', 'druid-form-attack-check', 'druid-form-live-check', 'grizzly-ai-check', 'grizzly-stats-check', 'dire-wolf-stats-check', 'battle-orders-check'],
        extra_reload_commands=['druid-form-reaction-reload-check', 'druid-form-interruption-reload-check', 'druid-form-casting-reload-check', 'druid-form-world-reload-check', 'shape-restriction-reload-check', 'druid-form-attack-reload-check', 'battle-orders-reload-check'],
        extra_before_save_commands=['druid-form-reaction-snapshot-check', 'druid-form-interruption-snapshot-check', 'druid-form-world-snapshot-check', 'druid-form-expiry-stage-check'],
        extra_fixtures=['druid-form-reaction-fixtures.json', 'druid-form-interruption-fixtures.json', 'druid-form-casting-fixtures.json', 'druid-form-world-fixtures.json', 'druid-form-expired-fixtures.json', 'battle-orders.json'],
        extra_probes=['druid-form-recovery-clock', 'druid-form-cast-clock', 'shape-restrictions', 'druid-form-attack-clock', 'druid-form-modes', 'modern-form-progress', 'grizzly-ai', 'grizzly-creation', 'grizzly-attack', 'dire-wolf-creation', 'druid-beast-attack'],
        extra_wait_results={'druid-form-reaction-live-check':'druid-form-reaction-live-result', 'druid-form-delay-live-check':'druid-form-delay-result', 'druid-form-live-check': 'druid-form-live-result', 'battle-orders-check': 'battle-orders-result', 'battle-orders-reload-check': 'battle-orders-result'},
        extra_input_commands=['bear-interrupt-quick', 'form-wolf-right','form-bear-quick','form-wolf-unshift-quick','form-bear-unshift-right'],
        between_boot_delay=5)
    print('Form reaction clocks/gear/natural movement pass3boots/2reloads alongside retained casting/interruption inputs; source ENDANIM and provisional native reentry remain distinct.')
