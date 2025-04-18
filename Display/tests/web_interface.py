import unittest
import sys
import os
import sqlite3

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from metrics_web_server import app


class FlaskAppTestCase(unittest.TestCase):
    debug = 0

    @classmethod
    def setUpClass(cls):
        cls.client = app.test_client()
        app.config["TESTING"] = True

    def test_index_route(self):
        response = self.client.get("/")
        self.assertEqual(response.status_code, 200)
        self.assertIn(b"<html", response.data.lower())

    def test_user_report_route(self):
        if self.debug == 6:
            with self.assertRaises(sqlite3.DatabaseError):
                self.client.post("/user_report", data={"debug": self.debug})
        else:
            response = self.client.post("/user_report", data={"debug": self.debug})
            self.assertEqual(response.status_code, 200)

    def test_proc_table_route(self):
        if self.debug == 6:
            with self.assertRaises(sqlite3.DatabaseError):
                self.client.post("/proc_table", data={"debug": self.debug})
        else:
            response = self.client.post("/proc_table", data={"debug": self.debug})
            self.assertEqual(response.status_code, 200)


def load_tests(loader, tests, pattern):
    suite = unittest.TestSuite()
    for i in range(0, 7):
        name = f"Test_Debug_level_{i}"
        test_case = type(name, (FlaskAppTestCase,), {"debug": i})
        tests = loader.loadTestsFromTestCase(test_case)
        suite.addTests(tests)
    return suite

if __name__ == "__main__":
    unittest.main()
