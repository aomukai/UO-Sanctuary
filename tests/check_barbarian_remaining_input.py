#!/usr/bin/env python3
"""Natural actual inputs for fourteen remaining active Barbarian talents."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
SPELLS=('howl','findpotion','taunt','battlecry','finditem','battleorders','warcry')
ATTACKS=('bash','leap','doublethrow','stun','concentrate','whirlwind','berserk')
INPUTS=tuple('remaining-barbarian-'+a+'-'+m for a in (*SPELLS,*ATTACKS) for m in (('right','quick')if a in SPELLS else ('left','right','quick')))
def configuration():
    return dict(stage=ROOT/'.local/class-skills/barbarian-remaining-input',extra_target_input_commands=INPUTS,
        extra_inputs=(Path(__file__).resolve(),ROOT/'tests/check_assassin_planned_acceptance.py'))
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--only',choices=(*SPELLS,*ATTACKS));a=p.parse_args();c=configuration()
    if a.only:c['extra_target_input_commands']=tuple(n for n in INPUTS if n.split('-')[2]==a.only)
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**c)
