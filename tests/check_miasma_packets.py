#!/usr/bin/env python3
"""Check Levitation equip/combat and retain earlier native components."""
import check_creeper_stats as retained

if __name__=='__main__':
    retained.STAGE=retained.ROOT/'.local/class-skills/miasma-packets'
    retained.check(extra_commands=['miasma-packets-check','levitation-check','barbarian-masteries-check','levitation-validation-check','warlock-foundation-check','arctic-mode-rate-check','arctic-stream-check','player-inferno-sequence-check','player-inferno-check','recycler-model-check','recycler-body-check','recycler-recovery-check','recycler-world-check','recycler-casting-check'],
        extra_reload_commands=['miasma-packets-reload-check','levitation-reload-check','barbarian-masteries-reload-check','levitation-validation-check','warlock-foundation-reload-check','arctic-mode-rate-check','arctic-mode-reload-check','arctic-stream-reload-check','player-inferno-sequence-reload-check','recycler-body-reload-check','recycler-recovery-reload-check','recycler-world-reload-check','recycler-casting-reload-check'],
        extra_live_commands=['arctic-mode-live','arctic-stream-live','recycler-recovery-live','recycler-world-live','recycler-casting-live'],
        extra_before_save_commands=['arctic-stream-expiry-stage-check'],between_boot_delay=1,
        extra_wait_results={'levitation-validation-check':'levitation-validation-result'},extra_fixtures=['miasma-packet-fixtures.json','levitation-fixtures.json','barbarian-masteries.json','warlock-foundation-fixtures.json','arctic-mode-fixture.json','arctic-stream-fixtures.json','recycler-body-fixtures.json','recycler-recovery-fixtures.json','recycler-world-fixtures.json','recycler-casting-fixtures.json'],
        extra_probes=['miasma-packets','warlock-foundation','player-inferno-rate','arctic-stream-damage','arctic-stream-factory','recycler-vines-birth','recycler-vines-ai','recycler-vines','pet-walk-flags','player-inferno-channel','player-inferno-sequence'])
    print('Miasma numeric suppliers and retained components pass3boots/2reloads; learned Miasma remains gated.')
