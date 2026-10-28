#!/usr/bin/env python3
"""Check saved Arctic cold emissions and retain player/recycler components."""
import check_creeper_stats as retained

if __name__=='__main__':
    retained.STAGE=retained.ROOT/'.local/class-skills/arctic-stream'
    retained.check(extra_commands=['arctic-stream-check','player-inferno-sequence-check','player-inferno-check','recycler-model-check','recycler-body-check','recycler-recovery-check','recycler-world-check','recycler-casting-check'],
        extra_reload_commands=['arctic-stream-reload-check','player-inferno-sequence-reload-check','recycler-body-reload-check','recycler-recovery-reload-check','recycler-world-reload-check','recycler-casting-reload-check'],
        extra_live_commands=['arctic-stream-live','recycler-recovery-live','recycler-world-live','recycler-casting-live'],
        extra_before_save_commands=['arctic-stream-expiry-stage-check'],between_boot_delay=1,
        extra_fixtures=['arctic-stream-fixtures.json','recycler-body-fixtures.json','recycler-recovery-fixtures.json','recycler-world-fixtures.json','recycler-casting-fixtures.json'],
        extra_probes=['arctic-stream-damage','arctic-stream-factory','recycler-vines-birth','recycler-vines-ai','recycler-vines','pet-walk-flags','player-inferno-channel','player-inferno-sequence'])
    print('Prepared Arctic cold stream passes3boots/2reloads with real damage/chill and supplied-sequence emission; modern learned input/cadence remains open.')
