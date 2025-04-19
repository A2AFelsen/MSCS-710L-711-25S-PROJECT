import sqlite3
import unittest
import database_setup
import os
import shutil
import subprocess


def get_git_root():
    """Finds the git root"""
    try:
        # Try to find the root of the git repo.
        root = subprocess.check_output(
            ['git', 'rev-parse', '--show-toplevel'],
            stderr=subprocess.DEVNULL).decode('utf-8').strip()
        return root

    # If the subprocess had an error we are not in a git repo.
    except subprocess.CalledProcessError:
        return None  


class InjectTestCase(unittest.TestCase):
    """Testcase for Data Injection"""

    # Initialize db to be used in the testcase.
    db_name = None

    @classmethod
    def setUpClass(cls) -> None:
        """Set up the run before all testcases"""

        # If db_name not set raise an error.
        assert cls.db_name
        # local can be 0 or false so we just check it exists.
        assert hasattr(cls, "local")

        # If we are running in local mode then we want to first delete any lingering test databases.
        # Then create a fresh set of test databases.
        if cls.local:
            database_setup.delete_all_databases()
            database_setup.create_all_databases()

        # If not in local mode then copy the database over first and update the db_name to the copy.
        else:
            shutil.copy(cls.db_name, os.path.basename(cls.db_name))
            cls.db_name = os.path.basename(cls.db_name)

        # Connect to the database
        cls.conn = sqlite3.connect(cls.db_name)

    @classmethod
    def tearDownClass(cls) -> None:
        """Tearing down the run after all test cases"""

        # Close the connection to the database.
        cls.conn.close()

        # If in local mode then delete all the test databases.
        if cls.local:
            database_setup.delete_all_databases()
        # Otherwise delete the copy of the database we used.
        else:
            os.remove(cls.db_name)

    def test_component_integrity(self):
        """Testing Primary Key duplicate for component table"""

        # Connect to the component table and fetch any entry.
        table = "component"
        cursor = self.conn.cursor()
        values = cursor.execute(f"SELECT * FROM {table}").fetchone()

        # When we try to insert the same entry we should get an IntegrityError
        with self.assertRaises(sqlite3.IntegrityError):
            cursor.execute(f"INSERT INTO {table} VALUES {values}")

    def test_component_statistic_integrity(self):
        """Testing Primary Key duplicate for component_statistic table"""

        # Connect to the component_statistic table and fetch any entry
        table = "component_statistic"
        cursor = self.conn.cursor()
        values = cursor.execute(f"SELECT * FROM {table}").fetchone()

        # When we try to insert the same entry we should get an IntegrityError
        with self.assertRaises(sqlite3.IntegrityError):
            cursor.execute(f"INSERT INTO {table} VALUES {values}")

    def test_process_integrity(self):
        """Testing Primary Key duplicate for process table"""

        # Connect to the process table and fetch any entry
        table = "process"
        cursor = self.conn.cursor()
        values = cursor.execute(f"SELECT * FROM {table}").fetchone()

        # When we try to insert the same entry we should get an IntegrityError
        with self.assertRaises(sqlite3.IntegrityError):
            cursor.execute(f"INSERT INTO {table} VALUES {values}")

    def test_component_null(self):
        """Testing adding Null values into component table"""

        table = "component"
        cursor = self.conn.cursor()

        # Not adding values into columns that can't be Null should raise an IntegrityError
        with self.assertRaises(sqlite3.IntegrityError):
            cursor.execute(f"INSERT INTO {table} (serial_number, v_ram) VALUES (?, ?)", ("null_cpu", 0))

    def test_component_statistic_null(self):
        """Testing adding Null values into component_statistic table"""

        table = "component_statistic"
        cursor = self.conn.cursor()

        # Not adding values into columns that can't be Null should raise an IntegrityError
        with self.assertRaises(sqlite3.IntegrityError):
            cursor.execute(f"INSERT INTO {table} (serial_number, timestamp) VALUES (?, ?)", ("null_cpu", "today"))

    def test_process_null(self):
        """Testing adding Null values into process table"""

        table = "process"
        cursor = self.conn.cursor()

        # Not adding values into columns that can't be Null should raise an IntegrityError
        with self.assertRaises(sqlite3.IntegrityError):
            cursor.execute(f"INSERT INTO {table} (pid, timestamp) VALUES (?, ?)", (1, "today"))

    def test_component_emoji(self):
        """Testing adding Emojis values into component table"""

        table = "component"
        cursor = self.conn.cursor()

        # Getting the number of entries before we add emojis.
        init_len = len(cursor.execute(f"SELECT * FROM {table}").fetchall())

        # Insert a set of emojis into the table and get the new length.
        new_entry = ("🔧", "💡", "✅", "😄", "🔍")
        cursor.execute(f"INSERT INTO {table} VALUES {new_entry}")
        new_len = len(cursor.execute(f"SELECT * FROM {table}").fetchall())

        # The new length should be 1 larger with the new entry.
        self.assertEqual(init_len + 1, new_len)

    def test_component_statistic_emoji(self):
        """Testing adding Emojis values into component_statistic table"""
        table = "component_statistic"
        cursor = self.conn.cursor()

        # Getting the number of entries before we add emojis.
        init_len = len(cursor.execute(f"SELECT * FROM {table}").fetchall())

        # Insert a set of emojis into the table and get the new length.
        new_entry = ("🔧", "💡", "✅", "😄", "🔍", "🔧", "💡", "✅", "😄", "🔍")
        cursor.execute(f"INSERT INTO {table} VALUES {new_entry}")
        new_len = len(cursor.execute(f"SELECT * FROM {table}").fetchall())

        # The new length should be 1 larger with the new entry.
        self.assertEqual(init_len + 1, new_len)

    def test_process_emoji(self):
        """Testing adding Emojis values into process table"""
        table = "process"
        cursor = self.conn.cursor()

        # Getting the number of entries before we add emojis.
        init_len = len(cursor.execute(f"SELECT * FROM {table}").fetchall())

        # Insert a set of emojis into the table and get the new length.
        new_entry = ("🔧", "💡", "✅", "😄", "🔍")
        cursor.execute(f"INSERT INTO {table} VALUES {new_entry}")
        new_len = len(cursor.execute(f"SELECT * FROM {table}").fetchall())

        # The new length should be 1 larger with the new entry.
        self.assertEqual(init_len + 1, new_len)


def load_tests(loader, tests, pattern):
    """Loads the tests into the test suite"""
    suite = unittest.TestSuite()

    # We are going to create to runs of the test suite.
    # normal.db in local mode and metrics.db.
    # normal.db is created as one of the test databases.
    for db in [["normal.db", True], [os.path.join(get_git_root(), "Display/tests/sample_dbs/metrics.db"), False]]:
        name = f"Test_{os.path.basename(db[0].split('.')[0])}"
        test_case = type(name, (InjectTestCase,), {"db_name": db[0], "local": db[1]})
        tests = loader.loadTestsFromTestCase(test_case)
        suite.addTests(tests)
    return suite



if __name__ == '__main__':
    unittest.main()
