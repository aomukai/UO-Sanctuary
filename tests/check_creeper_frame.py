#!/usr/bin/env python3
"""Verify corrected Creeper fields and retain accepted Druid combat/forms."""
import check_creeper_stats as retained

if __name__ == '__main__':
    retained.STAGE = retained.ROOT / '.local/class-skills/creeper-frame-correction'
    assert retained.STAGE.name == 'creeper-frame-correction'
    retained.check(
        extra_commands=['druid-rabies-packet-check','druid-rabies-check','druid-form-basic-block-check','druid-hunger-model-check','druid-hunger-check','druid-fire-claws-scalars-check','druid-fire-claws-check','druid-feral-check','druid-feral-live-check','druid-charge-model-check','druid-maul-check','druid-maul-live-check','druid-form-reaction-check','druid-form-interruption-check','druid-form-casting-check','druid-form-model-check','druid-form-world-check','shape-restriction-check','druid-form-rate-check','druid-form-attack-check'],
        extra_reload_commands=['druid-rabies-reload-check','druid-hunger-reload-check','druid-fire-claws-reload-check','druid-feral-reload-check','druid-maul-reload-check','druid-form-reaction-reload-check','druid-form-interruption-reload-check','druid-form-casting-reload-check','druid-form-world-reload-check','shape-restriction-reload-check','druid-form-attack-reload-check'],
        extra_before_save_commands=['druid-rabies-snapshot-check','druid-hunger-snapshot-check','druid-fire-claws-snapshot-check','druid-feral-snapshot-check','druid-maul-snapshot-check','druid-maul-expiry-stage-check','druid-form-reaction-snapshot-check','druid-form-interruption-snapshot-check','druid-form-world-snapshot-check','druid-form-expiry-stage-check'],
        extra_fixtures=['druid-rabies-fixtures.json','druid-hunger-fixtures.json','druid-fire-claws-fixtures.json','druid-feral-fixtures.json','druid-maul-fixtures.json','druid-form-reaction-fixtures.json','druid-form-interruption-fixtures.json','druid-form-casting-fixtures.json','druid-form-world-fixtures.json','druid-form-expired-fixtures.json'],
        extra_probes=['druid-rabies-factory','druid-rabies-spread','druid-hunger','druid-s3-clock','druid-fire-claws','druid-charged-attacks','druid-form-recovery-clock','druid-form-cast-clock','shape-restrictions','druid-form-attack-clock','druid-form-modes','modern-form-progress','grizzly-ai','grizzly-creation','grizzly-attack','dire-wolf-creation','druid-beast-attack'],
        extra_wait_results={'druid-feral-live-check':'druid-feral-live-result','druid-maul-live-check':'druid-maul-live-result'},
        extra_input_commands=['bear-interrupt-quick','form-wolf-right','form-bear-quick','form-wolf-unshift-quick','form-bear-unshift-right'],
        extra_target_input_commands=['rabies-right','rabies-quick','hunger-right','hunger-quick','fire-claws-right','fire-claws-quick','feral-right','feral-quick','maul-right','maul-quick'],between_boot_delay=5)
    print('Creeper frame candidate: corrected fields and retained Rabies/Hunger/Fire Claws/Feral/Maul/forms pass3boots/2actual reloads and30targeted skill inputs; source/native limits remain explicit; no completed class or installation claimed.')
