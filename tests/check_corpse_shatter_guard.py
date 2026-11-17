#!/usr/bin/env python3
"""Already-shattered corpse exclusion reaches wild Lich and both native animation gates."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_lightning_strike_contact import configuration as prior
STAGE=ROOT/'.local/class-skills/corpse-shatter-guard'
def configuration():
    c=prior();c.update(stage=STAGE)
    c['extra_wait_results'].update({'corpse-shatter-guard-check':'corpse-shatter-guard-result','corpse-shatter-guard-reload-check':'corpse-shatter-guard-result'})
    c.update(extra_families=('corpse-shatter-guard',*c['extra_families']),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/review_corpse_shatter_guard.py',ROOT/'tools/reference/corpse-shatter-guard-source-review.json',ROOT/'tools/reference/lightning-strike-contact-native-evidence.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
