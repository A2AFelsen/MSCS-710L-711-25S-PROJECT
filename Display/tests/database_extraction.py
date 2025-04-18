import sqlite3
import unittest
import database_setup
import os
import shutil
import subprocess


def get_git_root():
    try:
        root = subprocess.check_output(
            ['git', 'rev-parse', '--show-toplevel'], 
            stderr=subprocess.DEVNULL).decode('utf-8').strip()
        return root
    except subprocess.CalledProcessError:
        return None  
        

class BaseTestCase(unittest.TestCase):
    db_name = None  # to be set in subclasses

    @classmethod
    def setUpClass(cls) -> None:
        assert cls.db_name, "db_name must be set in the subclass"
        assert hasattr(cls, "local"), "local must be set in the subclass"
        if cls.local:
            database_setup.delete_all_databases()
            database_setup.create_all_databases()
        else:
            shutil.copy(cls.db_name, os.path.basename(cls.db_name))
            cls.db_name = os.path.basename(cls.db_name)
        cls.conn = sqlite3.connect(cls.db_name)

    @classmethod
    def tearDownClass(cls) -> None:
        cls.conn.close()
        if cls.local:
            database_setup.delete_all_databases()
        else:
            os.remove(cls.db_name)

    def test_corruption(self):
        corrupt_db = "corrupt_test.db"
        shutil.copy(self.db_name, corrupt_db)
        conn = sqlite3.connect(corrupt_db)
        database_setup.corrupt_db(conn)
        conn.commit()
        conn.close()

        conn = sqlite3.connect(corrupt_db)
        cursor = conn.cursor()
        with self.assertRaises(sqlite3.DatabaseError):
            cursor.execute("SELECT * FROM component").fetchone()
        conn.close()
        os.remove(corrupt_db)

    def test_new_database(self):
        new_db = "new_test.db"
        shutil.copy(self.db_name, new_db)
        conn = sqlite3.connect(new_db)
        cursor = conn.cursor()
        tables = cursor.execute("SELECT name FROM sqlite_master WHERE type='table' "
                                "AND name NOT LIKE 'sqlite_%';").fetchall()
        for (table,) in tables:
            self.assertGreater(len(cursor.execute(f"SELECT * FROM {table}").fetchall()), 0)
            cursor.execute(f"DELETE FROM {table}")
            self.assertEqual(len(cursor.execute(f"SELECT * FROM {table}").fetchall()), 0)

        conn.commit()
        conn.close()
        os.remove(new_db)

    def test_missing_database(self):
        missing_db = "missing_test.db"
        if os.path.exists(missing_db):
            os.remove(missing_db)
        self.assertFalse(os.path.exists(missing_db))
        conn = sqlite3.connect(missing_db)
        conn.close()
        self.assertTrue(os.path.exists(missing_db))
        os.remove(missing_db)


def load_tests(loader, tests, pattern):
    suite = unittest.TestSuite()
    for db in [["normal.db", True], [os.path.join(get_git_root(), "Display/tests/sample_dbs/metrics.db"), False]]:
        name = f"Test_{os.path.basename(db[0].split('.')[0])}"
        test_case = type(name, (BaseTestCase,), {"db_name": db[0], "local": db[1]})
        tests = loader.loadTestsFromTestCase(test_case)
        suite.addTests(tests)
    return suite


if __name__ == '__main__':
    unittest.main()
