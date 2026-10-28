#!/usr/bin/env python3
"""Check shared player channel helpers while retaining recycler/Druid components."""
import check_creeper_stats as retained

if __name__ == '__main__':
    retained.STAGE = retained.ROOT / '.local/class-skills/player-inferno-channel'
    retained.check(extra_commands=['player-inferno-check','recycler-model-check','recycler-body-check','recycler-recovery-check','recycler-world-check','recycler-casting-check'],
        extra_reload_commands=['recycler-body-reload-check','recycler-recovery-reload-check','recycler-world-reload-check','recycler-casting-reload-check'],
        extra_live_commands=['recycler-recovery-live','recycler-world-live','recycler-casting-live'],
        extra_fixtures=['recycler-body-fixtures.json','recycler-recovery-fixtures.json','recycler-world-fixtures.json','recycler-casting-fixtures.json'],
        extra_probes=['recycler-vines-birth','recycler-vines-ai','recycler-vines','pet-walk-flags','player-inferno-channel'])
    print('Prepared player Inferno/Arctic helpers and retained components pass3boots/2reloads; modern scheduling/payment frequency/input and cold flight remain open.')
