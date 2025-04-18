import unittest
import database_injection
import database_extraction

loader = unittest.TestLoader()
suite = unittest.TestSuite()

for module in [database_extraction, database_injection]:
    suite.addTests(loader.loadTestsFromModule(module))

unittest.TextTestRunner(verbosity=2).run(suite)
