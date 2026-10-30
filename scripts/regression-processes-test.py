#!/usr/bin/env python3
"""Real process-tree teardown checks; no application data or provider access."""
import subprocess
import sys
import time
from regression_processes import start_group, stop_group

for ignore in (False, True):
    child_code = (
        'import signal,sys,time;'
        + ('signal.signal(signal.SIGINT,signal.SIG_IGN);' if ignore else '')
        + 'print("ready",flush=True);time.sleep(60)'
    )
    parent_code = '''import signal,subprocess,sys,time
child=subprocess.Popen([sys.executable,'-c',sys.argv[1]],stdout=subprocess.PIPE,text=True)
assert child.stdout.readline().strip()=='ready'
def stop(*args):
 child.wait(timeout=5)
 sys.exit(0)
signal.signal(signal.SIGINT,signal.SIG_IGN if sys.argv[2]=='ignore' else stop)
print(child.pid,flush=True)
time.sleep(60)
'''
    process = start_group([sys.executable, '-c', parent_code, child_code,
                           'ignore' if ignore else 'graceful'],
                          stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
    try:
        child_pid = int(process.stdout.readline())
        stop_group(process, timeout=.5 if ignore else 5)
        if not ignore:
            assert process.returncode == 0, 'Graceful launcher did not reap its signalled child.'
        deadline = time.monotonic() + 5
        while True:
            status = subprocess.run(['ps', '-p', str(child_pid), '-o', 'stat='],
                                    capture_output=True, text=True).stdout.strip()
            if not status or status.startswith('Z'):
                break
            assert time.monotonic() < deadline, 'A simulation child remained alive.'
            time.sleep(.05)
        print('PASS: owned launcher and child stop via ' + ('bounded kill fallback.' if ignore else 'SIGINT.'))
    finally:
        stop_group(process, timeout=1)
        process.stdout.close()
