"""Real quick/right-held/left-held Fists input and subsequent charged Attack."""
import itertools,struct,threading

def exercise(session,client,logpath,prefix="fists-claw"):
    sequence=itertools.count(100)
    lock=threading.Lock()
    def send(action,ability=0,target=0,x=0,y=0,z=0):
        with lock:
            n=next(sequence)
            if action in [5,8]:client.sendall(struct.pack('>BHHBBI',0xBF,11,0xF2,7,action,n))
            else:client.sendall(struct.pack('>BHHBBIBIHHb',0xBF,21,0xF2,7,action,n,ability,target,x,y,z))
    session.send(prefix+'-input-check')
    assert session.expect(prefix+r'-input-check: (passed|failed)').group(1)=='passed',logpath.read_text()[-6000:]
    for phase in range(4):
        ready=session.expect(prefix+r'-input: ready action=(\d+) ability=(\d+) target=(\d+) x=(\d+) y=(\d+) z=(-?\d+)')
        args=tuple(map(int,ready.groups()));assert args[0]==[3,4,7,3][phase]
        send(*args)
        if phase==3:break
        done=threading.Event()
        def heartbeat():
            while not done.wait(.2):send(*args)
        thread=threading.Thread(target=heartbeat,daemon=True) if phase>0 else None
        if thread:thread.start()
        try:session.expect(prefix+'-input-stop: phase='+str(phase),timeout=7)
        finally:
            done.set()
            if thread:thread.join(timeout=1)
        send(8 if phase==2 else 5)
    assert session.expect(prefix+r'-input-result: (passed|failed)',timeout=7).group(1)=='passed',logpath.read_text()[-6000:]
