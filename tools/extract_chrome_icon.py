#!/usr/bin/env python3
"""Extract Chrome icon from Google's servers or a local Chrome install."""
import os
import sys
import struct
import zipfile
import io

ICON_URLS = [
    "https://www.google.com/chrome/static/images/chrome-logo.svg",
    "https://ssl.gstatic.com/chrome/webstore/images/icon_256.png",
    "https://lh3.googleusercontent.com/95GnDOl1z7KtFg1gDhMJm-0rNyS5D2pWOmTjLRi0BCM0mzJcYq6vM1jYvHkq5Q",
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
            if r.status_code == 200 and len(r.content) > 1000:
                return r.content
        except:
            continue
    return None

def find_chrome_png():
    """Try to find Chrome icon from common install paths."""
    paths = [
        os.path.expandvars(r"%ProgramFiles%\Google\Chrome\Application\chrome.exe"),
        os.path.expandvars(r"%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe"),
        os.path.expandvars(r"%LocalAppData%\Google\Chrome\Application\chrome.exe"),
    ]
    for path in paths:
        if not os.path.exists(path):
            continue
        try:
            import win32api
            icon = win32api.ExtractIconEx(path, 0)
            if icon and icon[0]:
                import tempfile
                tmp = tempfile.NamedTemporaryFile(delete=False, suffix='.ico')
                with open(tmp.name, 'rb') as f:
                    return f.read()
        except:
            pass
    return None

def main():
    output = os.path.join(os.path.dirname(__file__), '..', 'resources', 'chrome.ico')
    os.makedirs(os.path.dirname(output), exist_ok=True)

    print("[*] Attempting to download Chrome icon...")
    png = download_icon()
    if png and b'PNG' in png[:4]:
        ico = create_ico_from_png(png)
        with open(output, 'wb') as f:
            f.write(ico)
        print(f"[+] Icon saved: {output} ({len(ico)} bytes)")
        return

    print("[!] Could not download Chrome icon. Creating placeholder...")
    # Generate minimal 16x16 black square as placeholder
    png_header = bytes([
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x91, 0x68,
        0x36, 0x00, 0x00, 0x00, 0x01, 0x73, 0x52, 0x47,
        0x42, 0x00, 0xAE, 0xCE, 0x1C, 0xE9, 0x00, 0x00,
        0x00, 0x04, 0x67, 0x41, 0x4D, 0x41, 0x00, 0x00,
        0xB1, 0x8F, 0x0B, 0xFC, 0x61, 0x05, 0x00, 0x00,
        0x00, 0x20, 0x63, 0x48, 0x52, 0x4D, 0x00, 0x00,
        0x7A, 0x26, 0x00, 0x00, 0x80, 0x84, 0x00, 0x00,
        0xFA, 0x00, 0x00, 0x00, 0x80, 0xE8, 0x00, 0x00,
        0x75, 0x30, 0x00, 0x00, 0xEA, 0x60, 0x00, 0x00,
        0x3A, 0x98, 0x00, 0x00, 0x17, 0x70, 0x9C, 0xBA,
        0x51, 0x3C, 0x00, 0x00, 0x00, 0x2A, 0x49, 0x44,
        0x41, 0x54, 0x38, 0x4F, 0x63, 0xF8, 0xCF, 0xC0,
        0xC0, 0xC0, 0xC8, 0xC0, 0xC0, 0xC0, 0xC4, 0xC0,
        0xC0, 0xC0, 0xC2, 0xC0, 0xC0, 0xC0, 0xCA, 0xC0,
        0xC0, 0xC0, 0xC6, 0xC0, 0xC0, 0xC0, 0xCE, 0xC0,
        0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0,
        0xC0, 0xC0, 0x30, 0x54, 0x01, 0x00, 0x00, 0x00,
        0x00, 0xFF, 0xFF, 0x03, 0x00, 0x10, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
    ])
    ico = create_ico_from_png(png_header)
    with open(output, 'wb') as f:
        f.write(ico)
    print(f"[-] Placeholder icon created: {output}")

if __name__ == '__main__':
    main()
