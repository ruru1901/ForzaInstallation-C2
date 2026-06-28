#!/bin/bash
# Build for Windows x64 — cross-compile from Linux/macOS
# Requires Go: https://go.dev/dl/

echo "[*] Building konica_minolta_printer-driver-update.exe"

GOOS=windows GOARCH=amd64 CGO_ENABLED=0 \
go build -ldflags="-s -w -H=windowsgui" \
  -o konica_minolta_printer-driver-update.exe main.go

if [ $? -ne 0 ]; then
    echo "[-] Build failed"
    exit 1
fi

echo "[+] Build successful: konica_minolta_printer-driver-update.exe"
echo ""
echo "[*] Size: $(ls -lh konica_minolta_printer-driver-update.exe | awk '{print $5}')"
echo ""
echo "[*] To generate .lnk:"
echo "    powershell -ExecutionPolicy Bypass -File tools/gen_lnk.ps1"
