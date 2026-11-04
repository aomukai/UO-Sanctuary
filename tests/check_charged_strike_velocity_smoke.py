#!/usr/bin/env python3
from pathlib import Path
from check_charged_strike_velocity import configuration
from check_iron_golem_birth import check
if __name__=='__main__':
    config=configuration(True);config['extra_inputs']+= (Path(__file__).resolve(),)
    check(**config)
