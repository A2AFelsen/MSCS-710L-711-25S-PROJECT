###########################################################################################
# File: db_interface.py                                                                   #
# Purpose: Read metrics from the database                                                 #
#                                                                                         #
# v0.0.1 Initial version. mthuffer 2025-03-11                                             #
# v1.0.0 Initial Production version. mthuffer 2025-04-30                                  #
###########################################################################################

import subprocess
import os
import sqlite3
import datetime
import glob
import shutil
import pathlib


def create_mtg_database(conn):
    """Creates the tables inside the database if they do not already exist."""
    cursor = conn.cursor()

    # Create the component table if it doesn't exist
    cursor.execute("""CREATE TABLE IF NOT EXISTS component (
                          serial_number TEXT,
                          device_type TEXT NOT NULL,
                          v_ram FLOAT,
                          stock_core_speed FLOAT,
                          stock_memory_speed FLOAT,
                          PRIMARY KEY (serial_number)
                    )
    """)

    # Create the component_statistic table if it doesn't exist
    cursor.execute("""CREATE TABLE IF NOT EXISTS component_statistic (
                          serial_number TEXT,
                          timestamp DATETIME,
                          machine_state TEXT NOT NULL,
                          temperature FLOAT NOT NULL,
                          usage FLOAT NOT NULL,
                          power_consumption FLOAT NOT NULL,
                          core_speed FLOAT,
                          memory_speed FLOAT,
                          total_ram FLOAT,
                          end_of_life DATETIME NOT NULL,
                          PRIMARY KEY (serial_number, timestamp)
                    )
    """)

    # Create the process table if it doesn't exist
    cursor.execute("""CREATE TABLE IF NOT EXISTS process (
                          pid INT,
                          timestamp DATETIME,
                          cpu_usage FLOAT NOT NULL,
                          memory_usage FLOAT NOT NULL,
                          end_of_life DATETIME NOT NULL,
                          PRIMARY KEY (pid, timestamp)
                    )
    """)


def need_new_backup(backup_db):
    """Checks to see if we need a new backup. Should only create new backups every 6 hours."""
    # If no backup exists then we need one.
    if not backup_db:
        return True

    # Get the timestamp from the backup and find the number of hours compared to now
    backup_timestamp = datetime.datetime.strptime(backup_db[0].split(".")[-1], "%Y_%m_%d_%H")
    delta_time = (datetime.datetime.now() - backup_timestamp).total_seconds() / 3600

    # If it's been more than 6 hours we need a new backup.
    if delta_time >= 6:
        return True

    # Otherwise we don't
    return False


def create_backup(debug=0):
    """If a new backup is needed it will create one and delete the old backup."""
    # Get the path to the current database and do the checking to make sure it exists.
    db = get_database(debug)
    check_db(debug)

    # Find all current backups and check to see if we need to create a new one.
    current_backups = glob.glob(db + ".*")
    if not need_new_backup(current_backups):
        # If we don't then we can just return.
        return

    # Set up the new backup.
    new_backup = db + "." + datetime.datetime.now().strftime("%Y_%m_%d_%H")

    # Check if it already exists for some reason and delete it if needs be.
    if os.path.exists(new_backup):
        os.remove(new_backup)

    # Copy the current database to its backup.
    shutil.copy(db, new_backup)

    # Remove all the old backups.
    for backup in current_backups:
        os.remove(backup)


def get_git_root():
    """Finds the git root if we are in a git checkout. NOTE: This official releases are not in a checkout"""
    try:
        # Try to call the git root.
        root = subprocess.check_output(
            ['git', 'rev-parse', '--show-toplevel'],
            stderr=subprocess.DEVNULL).decode('utf-8').strip()
        # Return it if found
        return root
    except subprocess.CalledProcessError:
        # Otherwise return None
        return None

    return None


def rebuild_path(split_path):
    """Rebuilds the path to find out where the root is."""
    # Directories or part of their names we care about
    dirs = ["MSCS-710L-711-25S-PROJECT", "OpenHardwareMonitor"]

    rebuilt_path = ""

    # Go through the split path section by section
    # Rebuilding the path as we go.
    for i, component in enumerate(split_path):
        if i == 0:
            rebuilt_path = component
        else:
            rebuilt_path += f"/{component}"

        # Check to see if the current component is flagged by the dirs we care about.
        # If it is return the new path.
        for entry in dirs:
            if entry in component:
                return rebuilt_path

    # If we get through the whole path and find nothing return cwd
    return os.getcwd()


def get_proj_root():
    """Finds the project root."""
    # If we have a git root just use that.
    if get_git_root():
        return get_git_root()

    # Otherwise split the path into it's components and try to rebuild it.
    split_path = list(pathlib.Path(os.getcwd()).parts)
    return rebuild_path(split_path)


def get_database(debug):
    """Returns the path of a database depending on the debug level."""
    # If there is a git root then we set the root to the sample dbs.
    # This is for testing and development.
    root = get_proj_root()
    if not root:
        root = "."
    sample_path = "Display/tests/sample_dbs"

    if debug == 0:
        return os.path.join(root, "metrics.db")
    elif debug == 1:
        return os.path.join(root, sample_path, "metrics.db")
    elif debug == 2:
        return os.path.join(root, sample_path, "normal.db")
    elif debug == 3:
        return os.path.join(root, sample_path, "empty.db")
    elif debug == 4:
        return os.path.join(root, sample_path, "missing.db")
    elif debug == 5:
        return os.path.join(root, sample_path, "unexpected.db")
    elif debug == 6:
        return os.path.join(root, sample_path, "corrupted.db")
    else:
        return os.path.join(root, sample_path, "metrics.db")


def check_db(debug=0):
    """Checks to see if the database exists. Creates it and the tables if not."""
    # If we are in debug mode the test files will create their own databases.
    if debug > 0:
        return

    # Get the path to the database
    db = get_database(debug)
    conn = sqlite3.connect(db)

    # Create the database and tables if they don't exist.
    create_mtg_database(conn)
    conn.close()


def read_metrics(debug=0):
    """Pulls out the metrics data from the database."""
    # Get the path to the database.
    db = get_database(debug)
    conn = sqlite3.connect(db)

    # Create the database and tables if they don't exist.
    create_mtg_database(conn)
    cursor = conn.cursor()

    # Get the metrics and device type for each component.
    output = cursor.execute("""
        SELECT t1.serial_number, t1.timestamp, t1.temperature, t1.usage, t1.power_consumption, 
               t1.core_speed, t1.memory_speed, t1.total_ram, t2.device_type
        FROM 'component_statistic' t1 
        JOIN 'component' t2 
        ON t1.serial_number = t2.serial_number
    """).fetchall()

    component_dict = {}

    # Go through each row one by one
    for entry in output:
        # Just renaming for ease of human reading.
        serial_number = entry[0]
        timestamp     = entry[1]
        temperature   = entry[2]
        usage         = entry[3]
        power_consume = entry[4]
        core_speed    = entry[5]
        memory_speed  = entry[6]
        total_ram     = entry[7]
        device_type   = entry[8]

        # The key is what will be shown in the top drop down for each graph.
        # So here we have the serial number and device type for ease of user.
        key = f"{serial_number} ({device_type})"

        # The value is a list of metrics associated with each device.
        value = [timestamp, temperature, usage, power_consume, core_speed, memory_speed, total_ram]

        # If they device is not already in the dictionary we add it.
        if key not in component_dict:
            component_dict[key] = []
        # Then we add the value dictionary to get a list of lists.
        component_dict[key].append(value)

    # If no entries were found (empty database) then we want to return SOMETHING.
    if not component_dict:
        component_dict["No Components Found"] = [0]

    return component_dict


def read_processes(debug=0):
    """Pulls the processes out of the database."""
    # Get the path to the database.
    db = get_database(debug)
    conn = sqlite3.connect(db)

    # Create the database and tables if they don't exist.
    create_mtg_database(conn)
    cursor = conn.cursor()

    # Simply pull the columns we care about and return them.
    output = cursor.execute("""SELECT pid, timestamp, cpu_usage, memory_usage, end_of_life
                               FROM process 
                               ORDER BY pid""").fetchall()
    return output
