#!/usr/bin/env python3
"""Check APK frontend entry references and, by default, exact build output parity."""

import argparse
from html.parser import HTMLParser
from pathlib import Path, PurePosixPath
from urllib.parse import unquote, urlsplit
from zipfile import BadZipFile, ZipFile


class EntryReferences(HTMLParser):
    def __init__(self):
        super().__init__()
        self.references = []

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "script" and attrs.get("src"):
            self.references.append(attrs["src"])
        if tag == "link" and attrs.get("href"):
            self.references.append(attrs["href"])


def verify(apk_path, generated_root=None):
    with ZipFile(apk_path) as apk:
        entries = [entry.filename for entry in apk.infolist() if not entry.is_dir()]
        for frontend in ("GlassesUI", "CompanionUI"):
            prefix = f"assets/{frontend}/"
            names = [name[len(prefix):] for name in entries if name.startswith(prefix)]
            if len(names) != len(set(names)):
                raise ValueError(f"{frontend}: duplicate APK assets")
            if "index.html" not in names:
                raise ValueError(f"{frontend}: missing index.html")
            parser = EntryReferences()
            parser.feed(apk.read(prefix + "index.html").decode("utf-8"))
            if not parser.references:
                raise ValueError(f"{frontend}: entry has no resources")
            for reference in parser.references:
                url = urlsplit(reference)
                path = unquote(url.path)
                if (url.scheme or url.netloc or path.startswith("/")
                        or "\\" in path or ".." in PurePosixPath(path).parts):
                    raise ValueError(f"{frontend}: entry resource must be local")
                if str(PurePosixPath(path)) not in names:
                    raise ValueError(f"{frontend}: entry references a missing resource")
            if generated_root is not None:
                root = generated_root / frontend
                # Vite copies Finder metadata from public/; Android excludes it.
                expected = {path.relative_to(root).as_posix(): path
                            for path in root.rglob("*")
                            if path.is_file() and path.name != ".DS_Store"}
                if set(names) != set(expected):
                    raise ValueError(f"{frontend}: APK file set differs from generated output")
                for name, path in expected.items():
                    if apk.read(prefix + name) != path.read_bytes():
                        raise ValueError(f"{frontend}: APK content differs from generated output")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    parser.add_argument("--apk-only", action="store_true",
                        help="Check an existing APK without comparing local build output")
    args = parser.parse_args()
    generated_root = Path(__file__).resolve().parents[1] / "AndroidApp/app/build/generated/webAssets"
    try:
        verify(args.apk, None if args.apk_only else generated_root)
    except (ValueError, OSError, BadZipFile, KeyError) as error:
        parser.exit(1, f"Web asset verification failed: {error}\n")
    print("APK frontend assets verified" + ("" if args.apk_only else " against generated output"))


if __name__ == "__main__":
    main()
