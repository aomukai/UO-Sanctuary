#!/usr/bin/env python3
"""Learned Maul and shared source charge model with retained form/summon coverage."""
import check_creeper_stats as retained

if __name__ == '__main__':
    retained.STAGE = retained.ROOT / '.local/class-skills/druid-maul'
    assert retained.STAGE.name == 'druid-maul'
    retained.check(
        extra_commands=['druid-charge-model-check','druid-maul-check','druid-maul-live-check','druid-form-reaction-check','druid-form-interruption-check','druid-form-casting-check','druid-form-model-check','druid-form-world-check','shape-restriction-check','druid-form-rate-check','druid-form-attack-check'],
        extra_reload_commands=['druid-maul-reload-check','druid-form-reaction-reload-check','druid-form-interruption-reload-check','druid-form-casting-reload-check','druid-form-world-reload-check','shape-restriction-reload-check','druid-form-attack-reload-check'],
        extra_before_save_commands=['druid-maul-snapshot-check','druid-maul-expiry-stage-check','druid-form-reaction-snapshot-check','druid-form-interruption-snapshot-check','druid-form-world-snapshot-check','druid-form-expiry-stage-check'],
        extra_fixtures=['druid-maul-fixtures.json','druid-form-reaction-fixtures.json','druid-form-interruption-fixtures.json','druid-form-casting-fixtures.json','druid-form-world-fixtures.json','druid-form-expired-fixtures.json'],
        extra_probes=['druid-charged-attacks','druid-form-recovery-clock','druid-form-cast-clock','shape-restrictions','druid-form-attack-clock','druid-form-modes','modern-form-progress','grizzly-ai','grizzly-creation','grizzly-attack','dire-wolf-creation','druid-beast-attack'],
        extra_wait_results={'druid-maul-live-check':'druid-maul-live-result'},
        extra_input_commands=['bear-interrupt-quick','form-wolf-right','form-bear-quick','form-wolf-unshift-quick','form-bear-unshift-right'],
        extra_target_input_commands=['maul-right','maul-quick'],between_boot_delay=5)
    print('Maul focused mechanics/real right+quick inputs and saved charge consumers pass3boots/2actual reloads;20second natural duration verified before inspection hold; no complete class or modern executable parity claim.')
