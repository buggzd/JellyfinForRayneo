import importlib.util
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
from zipfile import ZipFile


spec = importlib.util.spec_from_file_location(
    "verify_web_assets", Path(__file__).resolve().parents[1] / "verify-web-assets.py")
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)


class WebAssetTests(unittest.TestCase):
    def setUp(self):
        self.temporary = TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.generated = self.root / "generated"
        self.apk = self.root / "app.apk"
        self.entries = {}
        for frontend in ("GlassesUI", "CompanionUI"):
            for name, content in {
                "index.html": b'<script type="module" src="./assets/main.js"></script>',
                "assets/main.js": b'import("./lazy.js")',
                "assets/lazy.js": b'export default "lazy"',
                "sounds/focus.wav": b"sample audio",
            }.items():
                path = self.generated / frontend / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(content)
                self.entries[f"assets/{frontend}/{name}"] = content

    def verify(self, apk_only=False):
        with ZipFile(self.apk, "w") as apk:
            for name, content in self.entries.items():
                apk.writestr(name, content)
        verifier.verify(self.apk, None if apk_only else self.generated)

    def test_matching_output_passes_with_android_excluded_finder_metadata(self):
        (self.generated / "GlassesUI/.DS_Store").write_bytes(b"Finder")
        self.verify()

    def test_missing_lazy_chunk_fails(self):
        del self.entries["assets/GlassesUI/assets/lazy.js"]
        with self.assertRaisesRegex(ValueError, "file set differs"):
            self.verify()

    def test_missing_public_sound_fails(self):
        del self.entries["assets/CompanionUI/sounds/focus.wav"]
        with self.assertRaisesRegex(ValueError, "file set differs"):
            self.verify()

    def test_stale_extra_bundle_fails(self):
        self.entries["assets/GlassesUI/assets/old.js"] = b"old"
        with self.assertRaisesRegex(ValueError, "file set differs"):
            self.verify()

    def test_changed_content_fails(self):
        self.entries["assets/GlassesUI/assets/main.js"] = b"old build"
        with self.assertRaisesRegex(ValueError, "content differs"):
            self.verify()

    def test_apk_only_accepts_another_build_without_local_output(self):
        self.entries["assets/GlassesUI/assets/main.js"] = b"another version"
        self.verify(apk_only=True)

    def test_apk_only_rejects_missing_entry_reference(self):
        del self.entries["assets/GlassesUI/assets/main.js"]
        with self.assertRaisesRegex(ValueError, "missing resource"):
            self.verify(apk_only=True)

    def test_apk_only_rejects_entry_reference_outside_frontend(self):
        self.entries["assets/GlassesUI/index.html"] = b'<script src="../other.js"></script>'
        with self.assertRaisesRegex(ValueError, "must be local"):
            self.verify(apk_only=True)


if __name__ == "__main__":
    unittest.main()
