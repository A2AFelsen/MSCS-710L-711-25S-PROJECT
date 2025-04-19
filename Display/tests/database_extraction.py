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
        

class ExtractTestCase(unittest.TestCase):
    """Testcase for Data Extraction"""

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

    def test_corruption(self):
        """"Testing corrupting a database then trying to read from it"""

        # Set the corrupted database.
        corrupt_db = "corrupt_test.db"

        # Copy the database for the test over, so we can corrupt it.
        shutil.copy(self.db_name, corrupt_db)

        # Connect to the new database and corrupt it.
        conn = sqlite3.connect(corrupt_db)
        database_setup.corrupt_db(conn)
        conn.commit()
        conn.close()

        # Connect again and try and read from the newly corrupted database.
        conn = sqlite3.connect(corrupt_db)
        cursor = conn.cursor()

        # We should get a DatabaseError when trying to read from the database.
        with self.assertRaises(sqlite3.DatabaseError):
            cursor.execute("SELECT * FROM component").fetchone()
        conn.close()

        # Cleanup the corrupted database.
        os.remove(corrupt_db)

    def test_new_database(self):
        """Testing removing entries from the database."""

        # Set up the 'new' database (empty)
        new_db = "new_test.db"

        # Copy the database over, so we can delete the entries.
        shutil.copy(self.db_name, new_db)

        # Connect to the new database.
        conn = sqlite3.connect(new_db)
        cursor = conn.cursor()

        # Find all the tables in the database (other than internal SQLite tables)
        tables = cursor.execute("SELECT name FROM sqlite_master WHERE type='table' "
                                "AND name NOT LIKE 'sqlite_%';").fetchall()

        # Go through each table found.
        # Before deletion there should be some data in the table.
        # After deletion there should be no data in the table.
        for (table,) in tables:
            self.assertGreater(len(cursor.execute(f"SELECT * FROM {table}").fetchall()), 0)
            cursor.execute(f"DELETE FROM {table}")
            self.assertEqual(len(cursor.execute(f"SELECT * FROM {table}").fetchall()), 0)

        conn.commit()
        conn.close()

        # Cleanup the new database.
        os.remove(new_db)

    def test_missing_database(self):
        """Tests what happens if no database is found."""

        # Set up the missing database.
        missing_db = "missing_test.db"

        # Doublecheck the database actually doesn't exist and delete it if it does.
        if os.path.exists(missing_db):
            os.remove(missing_db)

        # The database should not exist
        self.assertFalse(os.path.exists(missing_db))

        # Connecting to a database should automatically create a new one.
        conn = sqlite3.connect(missing_db)
        conn.close()

        # So now the new database should exist!
        self.assertTrue(os.path.exists(missing_db))

        # Cleaning up the 'missing' database
        os.remove(missing_db)


def load_tests(loader, tests, pattern):
    """Loads the tests into the test suite"""
    suite = unittest.TestSuite()

    # We are going to create to runs of the test suite.
    # normal.db in local mode and metrics.db.
    # normal.db is created as one of the test databases.
    for db in [["normal.db", True], [os.path.join(get_git_root(), "Display/tests/sample_dbs/metrics.db"), False]]:
        name = f"Test_{os.path.basename(db[0].split('.')[0])}"
        test_case = type(name, (ExtractTestCase,), {"db_name": db[0], "local": db[1]})
        tests = loader.loadTestsFromTestCase(test_case)
        suite.addTests(tests)
    return suite


if __name__ == '__main__':
    unittest.main()
