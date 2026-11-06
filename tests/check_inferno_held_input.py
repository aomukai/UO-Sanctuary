#!/usr/bin/env python3
"""Approved Inferno controller, raw held input and independent flight reloads."""
import argparse
import os
import socket
import struct
import threading
import time
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_inferno_held_candidate import configuration as prior


def wires(session,port,boot):
    for source in (0,1,9):
        label=f'inferno-held-wire-{source}'
        with socket.create_connection(('127.0.0.1',port),timeout=5) as client:
            def drain():
                try:
                    while client.recv(65536):pass
                except OSError:pass
            threading.Thread(target=drain,daemon=True).start()
            session.send(label+'-check')
            ready=session.expect(label+r': ready ability=(\d+) x=(\d+) y=(\d+) z=(-?\d+)')
            ability,x,y,z=map(int,ready.groups());sequence=0
            def send(action,input_source=source,replay=False):
                nonlocal sequence
                if not replay:sequence+=1
                if action==11:
                    packet=struct.pack('>BHHBBIB',0xBF,12,0xF2,7,action,sequence,input_source)
                else:
                    packet=struct.pack('>BHHBBIBBIHHb',0xBF,22,0xF2,7,action,sequence,input_source,ability,0,x,y,z)
                client.sendall(packet)
            send(10) # A heartbeat before a press must do nothing.
            send(9);send(9,replay=True);send(9) # Replay and distinct duplicate press.
            session.expect(label+': pressed')
            for _ in range(4):
                time.sleep(.15);send(10)
            session.expect(label+': burning')
            send(11,input_source=(source+1)%14);send(10)
            send(11);send(10) # Release, then a stale heartbeat cannot restart.
            assert session.expect(label+r'-result: (passed|failed)',timeout=10).group(1)=='passed'
    return 3


def configuration(retained=False):
    c=prior(retained)
    c['stage']=ROOT/'.local/class-skills'/('inferno-held-input-retained' if retained else 'inferno-held-input')
    # The historical candidate needs its own connected save press. Its policy
    # cases are retained in the approved fixture; do not claim both owners
    # remain connected through one socket at a save boundary.
    c['extra_families']=tuple(x for x in c['extra_families'] if x!='inferno-held-candidate')+('inferno-held-input',)
    c['extra_connected_checks']=tuple(x for x in c.get('extra_connected_checks',()) if not x.startswith('inferno-held-candidate-'))
    c['extra_connected_checks']=(*c.get('extra_connected_checks',()),'inferno-held-input-check','inferno-held-input-reload-check')
    c['extra_inputs']+=(Path(__file__).resolve(),)
    c.update(save_command='inferno-held-input-save',connected_save=True,extra_wire_callbacks=(wires,))
    return c


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--retained',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.retained))
