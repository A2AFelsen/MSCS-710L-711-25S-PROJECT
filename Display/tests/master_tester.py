import unittest
import database_injection
import database_extraction
import web_interface
import sys

# Initialize the test loader and test suite.
loader = unittest.TestLoader()
suite = unittest.TestSuite()

# Load all the tests from all the different test files into the test suite.
for module in [database_extraction, database_injection, web_interface]:
    suite.addTests(loader.loadTestsFromModule(module))

# Run the test suite at verbosity=2, so we can see each individual test run.
results = unittest.TextTestRunner(verbosity=2).run(suite)

# Check the results. If any test failed return a failed suite.
if not results.wasSuccessful():
    sys.exit(1)
