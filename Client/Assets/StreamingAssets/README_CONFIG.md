# 🌐 Client Configuration Guide

This folder contains `app_config.json`, which controls where the Unity client connects.

## 🛠 Option 1: JSON Config File
1. Open `app_config.json` in any text editor.
2. Update the `"host"` field (e.g., change `"127.0.0.1"` to `"192.168.1.50"` or a domain name).
3. Update the `"port"` if your Nakama server is running on a non-standard port.
4. Save the file and restart the game. **No recompilation or Unity build is required.**

## 💻 Option 2: Command Line Arguments (Highest Priority)
You can override settings via command line arguments when launching the game. This is useful for CI/CD or temporary testing.
CLI arguments override the `app_config.json` file.

**Usage:**
```bash
# Windows
.\YourGame.exe -host 192.168.1.5 -port 7350

# MacOS
./YourGame.app/Contents/MacOS/YourGame -host 192.168.1.5
```

**Available Arguments:**
- `-host <ip_or_domain>`
- `-port <number>`
- `-scheme <http|https>`
- `-serverKey <key>`

## 📂 Location in Builds
When you build the game, the config file will be located at:
- **Windows:** `[GameName]_Data/StreamingAssets/app_config.json`
- **Android:** (Inside the APK, usually read-only unless using specific overrides)
- **iOS:** (Inside the app bundle)

## ⚠️ Important Note
If this file is deleted, the game will automatically fallback to:
- **Host:** 127.0.0.1
- **Port:** 7350
- **Scheme:** http
- **Key:** defaultkey