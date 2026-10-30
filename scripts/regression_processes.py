"""Lifecycle for local simulation process groups owned by regression runners."""
import os
import signal
import subprocess


def start_group(args, **kwargs):
    return subprocess.Popen(args, start_new_session=True, **kwargs)


def stop_group(process, timeout=20):
    # The npm launcher and native CLI are separate processes on Linux. Signal the
    # session we created, then reap the launcher and remove any remaining children.
    try:
        os.killpg(process.pid, signal.SIGINT)
    except ProcessLookupError:
        pass
    try:
        process.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        pass
    finally:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        process.wait(timeout=timeout)
