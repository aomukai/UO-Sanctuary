#!/usr/bin/env python3
"""Candidate ordinary-chill shattering, retained corpse guards and saves."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_necromancer_planned_acceptance import planned_configuration
STAGE=ROOT/'.local/class-skills/cold-corpse-proposal'
def configuration():
    files=dict(planned_configuration()['extra_files']);files['hellslayer-before-readiness.json']=ROOT/'tools/reference/named-item-pre-proc-baseline.json'
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    return dict(stage=STAGE,extra_families=('cold-corpse-proposal','corpse-shatter-guard','hellslayer-readiness'),extra_files=files,extra_inputs=tuple(dict.fromkeys((Path(__file__).resolve(),ROOT/'tools/reference/cold-corpse-reference-gap.json',ROOT/'tools/reference/mage-projectile-lifetime-native-evidence.json',*helpers))),extra_wait_results={'corpse-shatter-guard-check':'corpse-shatter-guard-result','corpse-shatter-guard-reload-check':'corpse-shatter-guard-result'})
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
