#!/usr/bin/env python3
"""Extract application icon for Konica Minolta driver installer."""
import os
import sys
import struct

ICON_URLS = [
    "https://www.konicaminolta.com/common/images/favicon.ico",
    "https://www.konicaminolta.com/common/images/logo.svg",
]

def create_ico_from_png(png_data):
    """Create minimal .ico from a single PNG."""
    png_size = len(png_data)
    ico_header = struct.pack('<HHH', 0, 1, 1)
    ico_entry = struct.pack('<BBBBHHII', 0, 0, 0, 0, 1, 32, png_size, 22)
    return ico_header + ico_entry + png_data

def download_icon():
    import requests
    for url in ICON_URLS:
        try:
            r = requests.get(url, timeout=10)
            if r.status_code == 200 and len(r.content) > 100:
                return r.content
        except:
            continue
    return None

def main():
    output = os.path.join(os.path.dirname(__file__), '..', 'isodir', 'KM_Icon.ico')
    os.makedirs(os.path.dirname(output), exist_ok=True)

    print("[*] Attempting to download Konica Minolta logo...")
    data = download_icon()
    if data:
        ico = create_ico_from_png(data)
        with open(output, 'wb') as f:
            f.write(ico)
        print(f"[+] Icon saved: {output} ({len(ico)} bytes)")
        return

    print("[!] Could not download icon. Using placeholder.")

if __name__ == '__main__':
    main()
