###########################################################################################
# File: ohm_interface.py (Open Hardware Monitor Interface)                                #
# Purpose: The actual calls to the database will be run through this file                 #
#                                                                                         #
# v0.0.1 Initial version.                                                                 #
###########################################################################################

import psutil
import os
import subprocess


def is_metrics_running():
    for proc in psutil.process_iter(['pid', 'name', 'username']):
        try:
            if "OpenHardwareMonitor.exe" == proc.info['name']:
                proc.kill()
                return True
        except (psutil.NoSuchProcess, psutil.AccessDenied, psutil.ZombieProcess):
            pass
    return False


def get_metrics_exe():
    parent_dir = os.path.dirname(os.getcwd())
    return os.path.join(parent_dir, "OpenHardwareMonitor.exe")


def call_executable(years="1", months="0", weeks="0", days="0"):
    if not years.isdigit():
        years = "1"
    if not months.isdigit():
        months = "0"
    if not weeks.isdigit():
        weeks = "0"
    if not days.isdigit():
        days = "0"

    metrics_exe = get_metrics_exe()
    #metrics_call = [metrics_exe, "--lifetime", f"{str(years)}y", f"{str(months)}m", f"{str(weeks)}w", f"{str(days)}d"]
    #metrics_call = [metrics_exe, "--lifetime", f"{str(years)}y{str(months)}m{str(weeks)}w{str(days)}d"]
    #metrics_call = [metrics_exe, "--lifetime", f"{str(days)}d"]
    metrics_call = [metrics_exe]
    print(metrics_call)
    if os.path.exists(metrics_exe):
        parent_dir = os.path.dirname(os.getcwd())
        output = subprocess.run(metrics_call, cwd=parent_dir, capture_output=True, text=True, check=True)
        print(output.stdout)
        return "Metrics Started"
    else:
        return "Can't Find Metrics Executable"


def prune_data():
    metrics_exe = get_metrics_exe()
    metrics_call = [metrics_exe, "prune-now"]
    if os.path.exists(metrics_exe):
        parent_dir = os.path.dirname(os.getcwd())
        output = subprocess.run(metrics_call, cwd=parent_dir, capture_output=True, text=True, check=True)
        if output.stderr:
            return output.stderr
        else:
            return output.stdout
    else:
        return "Can't Find Metrics Executable"
