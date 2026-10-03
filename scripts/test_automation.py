"""Contract checks for CI coverage and release validation."""
import unittest
import xml.etree.ElementTree as ET

from coverage import rates
from release import inspect_manifest, version_from_tag


class AutomationTests(unittest.TestCase):
    def test_versions(self):
        self.assertEqual(("0.1.0", False), version_from_tag("v0.1.0", "0.1.0"))
        self.assertEqual(("0.2.0-rc.1", True), version_from_tag("v0.2.0-rc.1", "0.2.0-rc.1"))
        for tag in ("0.1.0", "v01.1.0", "v0.1.0-rc.01", "v0.1.0+build", "v0.1.0;echo bad"):
            with self.subTest(tag=tag), self.assertRaises(ValueError):
                version_from_tag(tag, "0.1.0")
        with self.assertRaises(ValueError):
            version_from_tag("v0.2.0", "0.1.0")

    def test_manifest_contract(self):
        good = '<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"><metadata><id>FormatParse</id><version>0.1.0</version><dependencies><group targetFramework="net8.0"/></dependencies></metadata></package>'
        inspect_manifest(good, "0.1.0")
        for bad in (
            good.replace("FormatParse", "Other"),
            good.replace("0.1.0", "0.2.0"),
            good.replace('<group targetFramework="net8.0"/>', '<dependency id="External" version="1.0"/>'),
            "<package/>",
        ):
            with self.subTest(manifest=bad), self.assertRaises(ValueError):
                inspect_manifest(bad, "0.1.0")

    def test_coverage_counts(self):
        self.assertEqual((0.9, 0.85), rates(ET.fromstring('<coverage lines-covered="90" lines-valid="100" branches-covered="85" branches-valid="100"/>')))
        self.assertEqual((1, 1), rates(ET.fromstring('<coverage lines-covered="0" lines-valid="0" branches-covered="0" branches-valid="0"/>')))
        with self.assertRaises(ValueError):
            rates(ET.fromstring('<coverage lines-covered="2" lines-valid="1" branches-covered="0" branches-valid="0"/>'))


if __name__ == "__main__":
    unittest.main()
