###########################################################################################
# File: database_manager.py                                                               #
# Purpose: The actual calls to the database will be run through this file                 #
#                                                                                         #
# v0.0.1 Initial version. Mostly a skeleton.                                              #
###########################################################################################
# TODO:                                                                                   #
###########################################################################################

def create_mtg_database(conn):
    cursor = conn.cursor()

    cursor.execute("""CREATE TABLE IF NOT EXISTS component (
                          serial_number TEXT,
                          device_type TEXT NOT NULL,
                          v_ram FLOAT,
                          stock_core_speed FLOAT,
                          stock_memory_speed FLOAT,
                          PRIMARY KEY (serial_number)
                    )
    """)

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

    cursor.execute("""CREATE TABLE IF NOT EXISTS process (
                          pid INT,
                          timestamp DATETIME,
                          cpu_usage FLOAT NOT NULL,
                          memory_usage FLOAT NOT NULL,
                          end_of_life DATETIME NOT NULL,
                          PRIMARY KEY (pid, timestamp)
                    )
    """)
