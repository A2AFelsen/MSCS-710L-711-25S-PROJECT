import unittest
import database_injection
import database_extraction
import sys

loader = unittest.TestLoader()
suite = unittest.TestSuite()

for module in [database_extraction, database_injection]:
    suite.addTests(loader.loadTestsFromModule(module))

results = unittest.TextTestRunner(verbosity=2).run(suite)
if not results.wasSuccessful():
    sys.exit(1)
