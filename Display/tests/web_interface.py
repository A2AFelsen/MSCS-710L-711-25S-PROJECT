import unittest
import sys
import os

# Add parent directory to sys.path so we can import app.py
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from metrics_web_server import app # import the Flask app from app.py


class FlaskAppTestCase(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        cls.client = app.test_client()
        app.config["TESTING"] = True

    def test_index_route(self):
        response = self.client.get("/")
        self.assertEqual(response.status_code, 200)
        self.assertIn(b"<html", response.data.lower())

    def test_user_report_route(self):
        response = self.client.post("/user_report")
        self.assertEqual(response.status_code, 200)

    def test_proc_table_route(self):
        response = self.client.post("/proc_table")
        self.assertEqual(response.status_code, 200)


if __name__ == "__main__":
    unittest.main()
