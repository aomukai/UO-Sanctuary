#!/usr/bin/env python3
"""Double Throw travelling flights with focused shared-missile and item controls."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_hellslayer import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/double-throw-missiles'
def configuration():
    c=prior();c.update(stage=STAGE);c.setdefault('extra_manifest_names',{});c.setdefault('extra_wait_results',{})
    for f in ('DoubleThrow','MagicArrow','BladeFuryMissile','LightningBolt'):add_fixture(c,f)
    for suffix in ('-check','-reload-check'):
        n='double-throw'+suffix;c['extra_wait_results'][n]='double-throw-result';c['extra_connected_checks']=(*c.get('extra_connected_checks',()),n)
        n='mirrored-world'+suffix;c['extra_wait_results'][n]=n+'-result';c['extra_connected_checks']=(*c['extra_connected_checks'],n)
    c.update(extra_families=('double-throw-missile',*c['extra_families'],'mirrored-world'),extra_target_input_commands=(*c['extra_target_input_commands'],'double-throw-missile-right','double-throw-missile-quick'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/prepare_double_throw_missiles.py',ROOT/'tools/reference/whirlwind-contact-native-evidence.json',ROOT/'tools/reference/double-throw-missile-source-review.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
