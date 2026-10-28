#!/usr/bin/env python3
"""Check automatic recycler seeking/feeding and retain saved component coverage."""
import check_creeper_stats as retained

if __name__ == '__main__':
    retained.STAGE = retained.ROOT / '.local/class-skills/recycler-world'
    retained.check(extra_commands=['recycler-model-check','recycler-body-check','recycler-recovery-check','recycler-world-check'],
        extra_reload_commands=['recycler-body-reload-check','recycler-recovery-reload-check','recycler-world-reload-check'],
        extra_live_commands=['recycler-recovery-live','recycler-world-live'],
        extra_fixtures=['recycler-body-fixtures.json','recycler-recovery-fixtures.json','recycler-world-fixtures.json'],
        extra_probes=['recycler-vines-birth','recycler-vines-ai','recycler-vines','pet-walk-flags'])
    print('Prepared recycler world passes3boots/2reloads with natural seek/move/feed/credit; learned casting/exposure and Solar current-slot acceptance remain open.')
