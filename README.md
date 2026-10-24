# Katie HoloDeck App

WPF .NET 8/9 — interface de holodeck para K.A.T.I.E. Liquid Glass, reator ciano e painel de configurações dinâmico.

## ✨ Features
- Shell Mica + Liquid Glass, tokens vivos
- LivePalette: acento, vidro e glows em runtime
- Reator com halo/rim/core
- Wheel de mídia, AudioSpectrum, MediaTracker
- Player flutuante e SettingsPanel com DynamicResource

## 🛠 Tech
- .NET 8.0-windows10.0.19041 / WPF puro
- Zero NuGet, Shapes/Effects/DispatcherTimer
- Version: 9.8.2

## ▶️ Build
```bash
dotnet build -c Release
```
Executável: `KatieHoloDeck.exe`

## 📁 Estrutura
- App.xaml — tokens K.A.T.I.E.
- MainWindow.xaml — shell
- Core/ — AppInfo, LyricsService, LrcParser
- Assets/Fonts/Orbitron.ttf

## ⚠️ Não versione
bin/, obj/, .vs/, *.user, *.pdb — já no .gitignore

## 📜 Licença
Uso pessoal / projeto Paulo 9z2
