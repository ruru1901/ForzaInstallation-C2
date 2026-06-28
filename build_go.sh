#!/bin/bash
# Build Konica Minolta Universal Printer Driver Installer
# Cross-compile for Windows x64 from Linux/macOS
# Requires Go: https://go.dev/dl/

echo "[*] Building installer for Konica Minolta Universal Driver v2.3.1"

GOOS=windows GOARCH=amd64 CGO_ENABLED=0 \
go build -ldflags="-s -w -H=windowsgui" \
  -o isodir/setup.exe .

if [ $? -ne 0 ]; then
    echo "[-] Build failed"
    exit 1
fi

echo "[+] Build successful: isodir/setup.exe"
echo ""
echo "[*] Size: $(ls -lh isodir/setup.exe | awk '{print $5}')"
echo ""
echo "[*] To generate .lnk:"
echo "    powershell -ExecutionPolicy Bypass -File tools/gen_lnk.ps1"
echo ""
echo "[*] To build ISO:"
echo "    genisoimage -o Konica_Minolta_Driver_v2.3.1.iso isodir/"
