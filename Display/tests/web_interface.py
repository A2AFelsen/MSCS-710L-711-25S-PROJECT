import unittest
import sys
import os
import sqlite3

# Get the path of the directory above the test file and insert it into our path.
# This is where the web app lives, which we need to import to test.
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

# Import the app from the web app file.
from metrics_web_server import app


# Set up the test suite for the web interface.
class FlaskAppTestCase(unittest.TestCase):
    """Testcase for the Flask Application."""
    # Debug is default 0. Loosely, the higher the debug number the more dangerous the database.
    debug = 0

    @classmethod
    def setUpClass(cls):
        """Set up the app before running any tests"""
        cls.client = app.test_client()
        app.config["TESTING"] = True

    def test_index_route(self):
        """Test the index/home page"""
        # The response should be 200, and we should get back an html tag.
        response = self.client.get("/")
        self.assertEqual(response.status_code, 200)
        self.assertIn(b"<html", response.data.lower())

    def test_user_report_route(self):
        """Test the user_report/metrics page"""
        # debug:6 means testing the corrupted database. Should return an error.
        if self.debug == 6:
            with self.assertRaises(sqlite3.DatabaseError):
                self.client.post("/user_report", data={"debug": self.debug})
        else:
            # The response should be 200, and we should get back a html tag.
            response = self.client.post("/user_report", data={"debug": self.debug})
            self.assertEqual(response.status_code, 200)
            self.assertIn(b"<html", response.data.lower())

    def test_proc_table_route(self):
        """Test the process_table page"""
        # debug:6 means testing the corrupted database. Should return an error.
        if self.debug == 6:
            with self.assertRaises(sqlite3.DatabaseError):
                self.client.post("/proc_table", data={"debug": self.debug})
        else:
            # The response should be 200, and we should get back a html tag.
            response = self.client.post("/proc_table", data={"debug": self.debug})
            self.assertEqual(response.status_code, 200)
            self.assertIn(b"<html", response.data.lower())


def load_tests(loader, tests, pattern):
    """Loads the tests into the test suite"""
    suite = unittest.TestSuite()

    # Currently have 6 databases to test. Run each test once with each database
    for i in range(1, 7):
        name = f"Test_Debug_level_{i}"
        test_case = type(name, (FlaskAppTestCase,), {"debug": i})
        tests = loader.loadTestsFromTestCase(test_case)
        suite.addTests(tests)
    return suite


if __name__ == "__main__":
    unittest.main()
