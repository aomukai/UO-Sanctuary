#!/usr/bin/env python3
"""Verify player sequence composition and retain channel/recycler components."""
import check_creeper_stats as retained

if __name__ == '__main__':
    retained.STAGE = retained.ROOT / '.local/class-skills/player-inferno-sequence'
    retained.check(extra_commands=['player-inferno-sequence-check','player-inferno-check','recycler-model-check','recycler-body-check','recycler-recovery-check','recycler-world-check','recycler-casting-check'],
        extra_reload_commands=['player-inferno-sequence-reload-check','recycler-body-reload-check','recycler-recovery-reload-check','recycler-world-reload-check','recycler-casting-reload-check'],
        extra_live_commands=['recycler-recovery-live','recycler-world-live','recycler-casting-live'],
        extra_fixtures=['recycler-body-fixtures.json','recycler-recovery-fixtures.json','recycler-world-fixtures.json','recycler-casting-fixtures.json'],
        extra_probes=['recycler-vines-birth','recycler-vines-ai','recycler-vines','pet-walk-flags','player-inferno-channel','player-inferno-sequence'])
    print('Prepared player Inferno/Arctic sequence and retained components pass3boots/2reloads; modern input/FCR/payment frequency and cold flight remain open.')
