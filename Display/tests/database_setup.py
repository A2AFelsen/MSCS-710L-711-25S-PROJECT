import os.path
import sqlite3
import datetime

SMILEY = "😄"
CHECK = "✅"
SPYGLASS = "🔍"
BULB = "💡"
WRENCH = "🔧"


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


def create_mtg_database_allow_nulls(conn):
    cursor = conn.cursor()

    cursor.execute("""CREATE TABLE IF NOT EXISTS component (
                          serial_number TEXT,
                          device_type TEXT,
                          v_ram FLOAT,
                          stock_core_speed FLOAT,
                          stock_memory_speed FLOAT,
                          PRIMARY KEY (serial_number)
                    )
    """)

    cursor.execute("""CREATE TABLE IF NOT EXISTS component_statistic (
                          serial_number TEXT,
                          timestamp DATETIME,
                          machine_state TEXT,
                          temperature FLOAT,
                          usage FLOAT,
                          power_consumption FLOAT,
                          core_speed FLOAT,
                          memory_speed FLOAT,
                          total_ram FLOAT,
                          end_of_life DATETIME,
                          PRIMARY KEY (serial_number, timestamp)
                    )
    """)

    cursor.execute("""CREATE TABLE IF NOT EXISTS process (
                          pid INT,
                          timestamp DATETIME,
                          cpu_usage FLOAT,
                          memory_usage FLOAT,
                          end_of_life DATETIME,
                          PRIMARY KEY (pid, timestamp)
                    )
    """)


def inject_base_values(conn, allow_nulls=False, emojis=False, empty_db=False):
    if empty_db:
        return

    cpu_comp = ("test_cpu", "CPU", 0, 2.55, 0)
    ram_comp = ("test_ram", "RAM", 0, 2.55, 2400)
    gpu_comp = ("test_gpu", "GPU", 0, 2.55, 7000)
    hdd_comp = ("test_hdd", "HDD", 0, 2.55, 0)

    if emojis:
        cpu_comp = ("test_cpu", SMILEY, 0, 2.55, 0)
        ram_comp = ("test_ram", WRENCH, 0, 2.55, 2400)
        gpu_comp = ("test_gpu", BULB, 0, 2.55, 7000)
        hdd_comp = ("test_hdd", CHECK, 0, 2.55, 0)

    statistic_time = datetime.datetime.strptime("2025-01-01 00:00:00.000", "%Y-%m-%d %H:%M:%S.%f")
    end_of_life_time = datetime.datetime.strptime("2026-01-01 00:00:00.000", "%Y-%m-%d %H:%M:%S.%f")

    cursor = conn.cursor()

    if allow_nulls:
        cpu_comp = ("test_cpu", 0)
        ram_comp = ("test_ram", 0)
        gpu_comp = ("test_gpu", 0)
        hdd_comp = ("test_hdd", 0)
        cursor.execute("INSERT INTO component (serial_number, v_ram) VALUES (?, ?)", cpu_comp)
        cursor.execute("INSERT INTO component (serial_number, v_ram) VALUES (?, ?)", ram_comp)
        cursor.execute("INSERT INTO component (serial_number, v_ram) VALUES (?, ?)", gpu_comp)
        cursor.execute("INSERT INTO component (serial_number, v_ram) VALUES (?, ?)", hdd_comp)
    else:
        cursor.execute(f"""INSERT INTO component VALUES {cpu_comp}""")
        cursor.execute(f"""INSERT INTO component VALUES {ram_comp}""")
        cursor.execute(f"""INSERT INTO component VALUES {gpu_comp}""")
        cursor.execute(f"""INSERT INTO component VALUES {hdd_comp}""")

    for i in range(1, 26):
        stat_str = statistic_time.strftime("%Y-%m-%d %H:%M:%S.%f")
        eol_str = end_of_life_time.strftime("%Y-%m-%d %H:%M:%S.%f")

        cpu_tuple = (cpu_comp[0], stat_str, "ACTIVE", i, i*1.0, i*1.5, i*2.0, i*2.5, i*3.0, eol_str)
        ram_tuple = (ram_comp[0], stat_str, "ACTIVE", i, i*1.0, i*1.5, i*2.0, i*2.5, i*3.0, eol_str)
        gpu_tuple = (gpu_comp[0], stat_str, "ACTIVE", i, i*1.0, i*1.5, i*2.0, i*2.5, i*3.0, eol_str)
        hdd_tuple = (hdd_comp[0], stat_str, "ACTIVE", i, i*1.0, i*1.5, i*2.0, i*2.5, i*3.0, eol_str)
        if emojis:
            if i % 4 == 0:
                cpu_tuple = (cpu_comp[0], stat_str, "ACTIVE", i, SMILEY,  i * 1.5, i * 2.0,  i * 2.5, i * 3.0, eol_str)
                ram_tuple = (ram_comp[0], stat_str, "ACTIVE", i, i * 1.0, CHECK,   i * 2.0,  i * 2.5, i * 3.0, eol_str)
                gpu_tuple = (gpu_comp[0], stat_str, "ACTIVE", i, i * 1.0, i * 1.5, SPYGLASS, i * 2.5, i * 3.0, eol_str)
                hdd_tuple = (hdd_comp[0], stat_str, "ACTIVE", i, i * 1.0, i * 1.5, i * 2.0,  BULB,    i * 3.0, eol_str)

        if allow_nulls and i % 5 == 0:
            cpu_tuple = (cpu_comp[0], stat_str)
            ram_tuple = (ram_comp[0], stat_str)
            gpu_tuple = (gpu_comp[0], stat_str)
            hdd_tuple = (hdd_comp[0], stat_str)
            cursor.execute("INSERT INTO component_statistic (serial_number, timestamp) VALUES (?, ?)", cpu_tuple)
            cursor.execute("INSERT INTO component_statistic (serial_number, timestamp) VALUES (?, ?)", ram_tuple)
            cursor.execute("INSERT INTO component_statistic (serial_number, timestamp) VALUES (?, ?)", gpu_tuple)
            cursor.execute("INSERT INTO component_statistic (serial_number, timestamp) VALUES (?, ?)", hdd_tuple)
        else:
            cursor.execute(f"INSERT INTO component_statistic VALUES {cpu_tuple}")
            cursor.execute(f"INSERT INTO component_statistic VALUES {ram_tuple}")
            cursor.execute(f"INSERT INTO component_statistic VALUES {gpu_tuple}")
            cursor.execute(f"INSERT INTO component_statistic VALUES {hdd_tuple}")

        for j in range(1, 10):
            process_tuple = (j, stat_str, i * 2.5, i * 2.5, eol_str)
            if emojis:
                process_tuple = (j, stat_str, WRENCH, i * 2.5, eol_str)

            if allow_nulls and i % 5 == 0:
                process_tuple = (j, stat_str)
                cursor.execute("INSERT INTO process (pid, timestamp) VALUES (?, ?)", process_tuple)
            else:
                cursor.execute(f"""INSERT INTO process VALUES {process_tuple}""")

        statistic_time = statistic_time + datetime.timedelta(hours=1)
        end_of_life_time = end_of_life_time + datetime.timedelta(hours=1)


def inject_values(conn, table, values):
    cursor = conn.cursor()
    cursor.execute(f"""INSERT INTO {table} VALUES {values}""")
    conn.commit()


def create_normal_database(db="normal.db"):
    conn = sqlite3.connect(db)

    create_mtg_database(conn)
    conn.commit()

    inject_base_values(conn)
    conn.commit()
    conn.close()


def corrupt_db(conn):
    conn.execute("PRAGMA writable_schema = 1;")
    conn.execute("UPDATE sqlite_master SET sql = 'corrupt' WHERE type='table';")


def create_corrupted_database(db="corrupted.db"):
    conn = sqlite3.connect(db)

    create_mtg_database(conn)
    conn.commit()

    inject_base_values(conn)
    conn.commit()

    corrupt_db(conn)
    conn.commit()
    conn.close()


def create_unexpected_database(db="unexpected.db"):
    conn = sqlite3.connect(db)

    create_mtg_database(conn)
    conn.commit()

    inject_base_values(conn, emojis=True)
    conn.commit()
    conn.close()


def create_missing_values_database(db="missing.db"):
    conn = sqlite3.connect(db)

    create_mtg_database_allow_nulls(conn)
    conn.commit()

    inject_base_values(conn, allow_nulls=True)
    conn.commit()
    conn.close()


def create_empty_database(db="empty.db"):
    conn = sqlite3.connect(db)

    create_mtg_database(conn)
    conn.commit()

    inject_base_values(conn, empty_db=True)
    conn.commit()
    conn.close()


def create_all_databases():
    create_normal_database()
    create_corrupted_database()
    create_unexpected_database()
    create_missing_values_database()
    create_empty_database()


def delete_all_databases():
    if os.path.exists("normal.db"):
        os.remove("normal.db")

    if os.path.exists("corrupted.db"):
        os.remove("corrupted.db")

    if os.path.exists("unexpected.db"):
        os.remove("unexpected.db")

    if os.path.exists("missing.db"):
        os.remove("missing.db")

    if os.path.exists("empty.db"):
        os.remove("empty.db")
