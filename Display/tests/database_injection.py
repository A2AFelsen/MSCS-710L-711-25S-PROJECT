import sqlite3
import unittest
import database_setup
import os
import shutil


class BaseTestCases(unittest.TestCase):
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

    def test_component_integrity(self):
        table = "component"
        cursor = self.conn.cursor()
        values = cursor.execute(f"SELECT * FROM {table}").fetchone()

        with self.assertRaises(sqlite3.IntegrityError):
            cursor.execute(f"INSERT INTO {table} VALUES {values}")

    def test_component_statistic_integrity(self):
        table = "component_statistic"
        cursor = self.conn.cursor()
        values = cursor.execute(f"SELECT * FROM {table}").fetchone()

        with self.assertRaises(sqlite3.IntegrityError):
            cursor.execute(f"INSERT INTO {table} VALUES {values}")

    def test_process_integrity(self):
        table = "process"
        cursor = self.conn.cursor()
        values = cursor.execute(f"SELECT * FROM {table}").fetchone()

        with self.assertRaises(sqlite3.IntegrityError):
            cursor.execute(f"INSERT INTO {table} VALUES {values}")

    def test_component_null(self):
        table = "component"
        cursor = self.conn.cursor()
        with self.assertRaises(sqlite3.IntegrityError):
            cursor.execute(f"INSERT INTO {table} (serial_number, v_ram) VALUES (?, ?)", ("null_cpu", 0))

    def test_component_statistic_null(self):
        table = "component_statistic"
        cursor = self.conn.cursor()
        with self.assertRaises(sqlite3.IntegrityError):
            cursor.execute(f"INSERT INTO {table} (serial_number, timestamp) VALUES (?, ?)", ("null_cpu", "today"))

    def test_process_null(self):
        table = "process"
        cursor = self.conn.cursor()
        with self.assertRaises(sqlite3.IntegrityError):
            cursor.execute(f"INSERT INTO {table} (pid, timestamp) VALUES (?, ?)", (1, "today"))

    def test_component_emoji(self):
        table = "component"
        cursor = self.conn.cursor()

        init_len = len(cursor.execute(f"SELECT * FROM {table}").fetchall())

        new_entry = ("🔧", "💡", "✅", "😄", "🔍")
        cursor.execute(f"INSERT INTO {table} VALUES {new_entry}")
        new_len = len(cursor.execute(f"SELECT * FROM {table}").fetchall())

        self.assertEqual(init_len + 1, new_len)

    def test_component_statistic_emoji(self):
        table = "component_statistic"
        cursor = self.conn.cursor()

        init_len = len(cursor.execute(f"SELECT * FROM {table}").fetchall())

        new_entry = ("🔧", "💡", "✅", "😄", "🔍", "🔧", "💡", "✅", "😄", "🔍")
        cursor.execute(f"INSERT INTO {table} VALUES {new_entry}")
        new_len = len(cursor.execute(f"SELECT * FROM {table}").fetchall())

        self.assertEqual(init_len + 1, new_len)

    def test_process_emoji(self):
        table = "process"
        cursor = self.conn.cursor()

        init_len = len(cursor.execute(f"SELECT * FROM {table}").fetchall())

        new_entry = ("🔧", "💡", "✅", "😄", "🔍")
        cursor.execute(f"INSERT INTO {table} VALUES {new_entry}")
        new_len = len(cursor.execute(f"SELECT * FROM {table}").fetchall())

        self.assertEqual(init_len + 1, new_len)


def load_tests(loader, tests, pattern):
    suite = unittest.TestSuite()
    for db in [["normal.db", True], ["sample_dbs/metrics.db", False]]:
        name = f"Test_{os.path.basename(db[0].split('.')[0])}"
        test_case = type(name, (BaseTestCases,), {"db_name": db[0], "local": db[1]})
        tests = loader.loadTestsFromTestCase(test_case)
        suite.addTests(tests)
    return suite


if __name__ == '__main__':
    unittest.main()
