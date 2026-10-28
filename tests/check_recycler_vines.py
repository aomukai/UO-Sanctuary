#!/usr/bin/env python3
"""Check prepared recycler models, real corpses/resources, saved bodies and automatic credit."""
import check_creeper_stats as retained

if __name__=='__main__':
    retained.STAGE=retained.ROOT/'.local/class-skills/recycler-vines'
    assert retained.STAGE.name=='recycler-vines'
    retained.check(extra_commands=['recycler-model-check','recycler-body-check','recycler-recovery-check'],
        extra_reload_commands=['recycler-body-reload-check','recycler-recovery-reload-check'],
        extra_live_commands=['recycler-recovery-live'],
        extra_fixtures=['recycler-body-fixtures.json','recycler-recovery-fixtures.json'],
        extra_probes=['recycler-vines-birth','recycler-vines-ai','recycler-vines'])
    print('Prepared recycler components pass3boots/2actual reloads and natural delayed credit; learned controller/casting/exposure and Solar current-slot acceptance remain open.')
