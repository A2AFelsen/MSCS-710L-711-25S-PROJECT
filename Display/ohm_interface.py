###########################################################################################
# File: ohm_interface.py (Open Hardware Monitor Interface)                                #
# Purpose: The actual calls to the database will be run through this file                 #
#                                                                                         #
# v0.0.1 Initial version. mthuffer 2025-03-11                                             #
# v1.0.0 Initial Production version. mthuffer 2025-04-30                                  #
#            call_executable() and prune_now() are separated because we do the checking   #
#            if the process is running in the web server. That could be moved here in the #
#            future if we wanted. But we may want to keep them separate anyhow            #
###########################################################################################

import psutil
import os
import subprocess
import db_interface


def is_metrics_running():
    """Checks to see if the metrics collector is running. Kills the process if it is."""
    # Get a list of all processes.
    for proc in psutil.process_iter(['name']):
        try:
            # If we find the process try and kill it and return True saying the metrics was running.
            if "OpenHardwareMonitor.exe" == proc.info['name']:
                proc.kill()
                return True
        # If somehow the process is missing after finding it then it's still gone, so we can return False.
        except psutil.NoSuchProcess:
            return False
        # If we can't kill the process we still found it, so we should return true.
        except (psutil.AccessDenied, psutil.ZombieProcess):
            return True

    # If the process isn't found then return false.
    return False


def get_metrics_exe():
    """Finds the metrics executable and checks to see if we need a backup."""
    # Creates a new backup if needed.
    db_interface.create_backup()

    # The executable should be in the directory above the python scripts.
    parent_dir = os.path.dirname(os.getcwd())
    return os.path.join(parent_dir, "OpenHardwareMonitor.exe")


def call_executable(years="1", months="0", weeks="0", days="0"):
    """Calls the executable to collect metrics."""
    # Check each user input to sanitize. If user didn't use digits then use default values.
    years  = years  if years.isdigit()  else "1"
    months = months if months.isdigit() else "0"
    weeks  = weeks  if weeks.isdigit()  else "0"
    days   = days   if days.isdigit()   else "0"

    # Find the metrics executable.
    metrics_exe = get_metrics_exe()

    # Set up the call to the executable including the user inputs.
    metrics_call = [metrics_exe, "--lifetime", f"{str(years)}y{str(months)}m{str(weeks)}w{str(days)}d"]

    # Check to make sure the executable exists.
    if os.path.exists(metrics_exe):
        # Run the executable from its own directory.
        parent_dir = os.path.dirname(os.getcwd())
        output = subprocess.run(metrics_call, cwd=parent_dir, capture_output=True, text=True, check=True)

        # If we get an error return the error, otherwise return the stdout.
        # Whatever is returned here will be shown back to the user.
        if output.stderr:
            return output.stderr
        else:
            return output.stdout

    # Tell the user we couldn't find the executable.
    else:
        return "Can't Find Metrics Executable"


def prune_data(years="1", months="0", weeks="0", days="0"):
    """Calls the executable to prune old metrics."""
    # Check each user input to sanitize. If user didn't use digits then use default values.
    years  = years  if years.isdigit()  else "1"
    months = months if months.isdigit() else "0"
    weeks  = weeks  if weeks.isdigit()  else "0"
    days   = days   if days.isdigit()   else "0"

    # Find the metrics executable.
    metrics_exe = get_metrics_exe()

    # Set up the call to the executable including the user inputs.
    metrics_call = [metrics_exe, "prune-now", "--lifetime", f"{str(years)}y{str(months)}m{str(weeks)}w{str(days)}d"]

    # Check to make sure the executable exists.
    if os.path.exists(metrics_exe):
        # Run the executable from its own directory.
        parent_dir = os.path.dirname(os.getcwd())
        output = subprocess.run(metrics_call, cwd=parent_dir, capture_output=True, text=True, check=True)

        # If we get an error return the error, otherwise return the stdout.
        # Whatever is returned here will be shown back to the user.
        if output.stderr:
            return output.stderr
        else:
            return output.stdout

    # Tell the user we couldn't find the executable.
    else:
        return "Can't Find Metrics Executable"
