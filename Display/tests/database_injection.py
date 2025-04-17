import sqlite3
import unittest
import database_setup


class MyTestCase(unittest.TestCase):

    @classmethod
    def setUpClass(cls) -> None:
        database_setup.delete_all_databases()
        database_setup.create_all_databases()
        cls.normal_conn = sqlite3.connect("normal.db")

    @classmethod
    def tearDownClass(cls) -> None:
        cls.normal_conn.close()
        database_setup.delete_all_databases()

    def test_component_integrity(self):
        table = "component"
        cursor = self.normal_conn.cursor()
        values = cursor.execute(f"SELECT * FROM {table}").fetchone()

        with self.assertRaises(sqlite3.IntegrityError):
            database_setup.inject_values(self.normal_conn, table, values)

    def test_component_statistic_integrity(self):
        table = "component_statistic"
        cursor = self.normal_conn.cursor()
        values = cursor.execute(f"SELECT * FROM {table}").fetchone()

        with self.assertRaises(sqlite3.IntegrityError):
            database_setup.inject_values(self.normal_conn, table, values)

    def test_process_integrity(self):
        table = "process"
        cursor = self.normal_conn.cursor()
        values = cursor.execute(f"SELECT * FROM {table}").fetchone()

        with self.assertRaises(sqlite3.IntegrityError):
            database_setup.inject_values(self.normal_conn, table, values)


if __name__ == '__main__':
    unittest.main()
