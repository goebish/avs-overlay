# AVS Overlay

**AVS Overlay** is a lightweight fullscreen mirroring tool for Winamp’s legendary AVS visualizer.  

It's been created to replace the desktop overlay mode that's been broken since Windows Vista.

Perfect for retro-inspired audio-visual setups, custom stage visuals, home psychonaut labs, or just kicking it like it’s 2003.

---

## ✨ Features

- Fullscreen mirroring of the AVS window using native DWM thumbnails  
- Multi-monitor support
- Precise crop control to remove window borders
- Auto-reconnect if AVS is closed and reopened    
- Launch multiple instances on multiple monitors  

---

## 🔧 Command-Line Options

```
AVSOverlay.exe [--monitor N] [--crop L,T,R,B] [--help]
```

| Option        | Description                                  |
|---------------|----------------------------------------------|
| `--monitor N` | Select target monitor (e.g., 0, 1, 2...)     |
| `--crop`      | Crop margins: Left, Top, Right, Bottom       |
| `--help`      | Show usage information                       |

**Example:**

```
AVSOverlay.exe --monitor 0 --crop 11,20,8,14
```
(default settings if no arguments are provided)

---

## 💽 Requirements

- Windows 10 or 11 (DWM must be enabled)  
- Winamp (tested with classic and modern builds)  
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)  

---

## 🚀 Use Case

If you're:
- resurrecting your classic MP3 collection  
- running AVS visuals across projectors  
- building a personal retro-VJ rig  
- or just miss whipping that llama’s ass...

**This tool is for you.**

---

## 🧠 Behind the Scenes

AVS Overlay uses `DwmRegisterThumbnail` to capture and mirror AVS with zero-copy rendering.  
It polls windows titled `"AVS"`, filters only the one owned by `winamp.exe`, and mirrors it in a borderless fullscreen window on the monitor of your choice.

---

## 🛠 Build Instructions

```
git clone https://github.com/goebish/avs-overlay
cd avs-overlay
dotnet build -c Release
```

---

## 📜 License

MIT — free to use, remix, and deploy in your own audio-visual rituals.

---

## 💬 Credits

Created by pure passion and pixel madness.
