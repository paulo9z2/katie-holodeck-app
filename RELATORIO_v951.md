# RELATÓRIO v9.5.1 — Roda ouve o áudio real + toggle do reator

Executor: OpenCode/DeepSeek V4.1. Nenhum commit, nenhum arquivo fora dos permitidos, app não executado.
Todos os builds: `dotnet build --nologo` no cwd, 0 Erro(s) ao fim de CADA fase (avisos pré-existentes mantidos).

---

## FASE 1 — AudioSpectrum: sinal "tem som agora"

**Arquivo alterado:** `AudioSpectrum.cs`

- `AudioSpectrum.cs:97-98` — adicionada propriedade pública:
  ```csharp
  public bool HasAudio => _realAudio && _totalPeak > 0.004f;
  ```
- Pipeline real/synthetic e `PublishBands` intocados.

**Build (fase 1):**
```
Compilação com êxito.
    4 Aviso(s)
    0 Erro(s)
```
Avisos: CS8625 em MainWindow.xaml.cs(214,78) e CS8604 em MainWindow.xaml.cs(1606,51) — pré-existentes.

---

## FASE 2 — MainWindow: roda acorda pelo ÁUDIO

**Arquivo alterado:** `MainWindow.xaml.cs`

- `MainWindow.xaml.cs:706-707` (`ApplyMusicPrefs`, antigo ~700):
  ```csharp
  bool audioLive = _spectrum.HasAudio || _mediaPlaying;
  MusicWheelView.SetPlaying(audioLive && s.Music.Wheel);
  ```
- `MainWindow.xaml.cs:1450-1451` (`OnMediaUpdate`, antigo ~1432):
  ```csharp
  bool audioLive = _spectrum.HasAudio || _mediaPlaying;
  MusicWheelView.SetPlaying(audioLive && _settings.Music.Wheel);
  ```
- `_mediaPlaying` continua vindo do SMTC (backup quando o WASAPI falha). Nenhum outro gate de mídia tocado.

**Build (fase 2):**
```
Compilação com êxito.
    4 Aviso(s)
    0 Erro(s)
```
Avisos: CS8625 em MainWindow.xaml.cs(214,78) e CS8604 em MainWindow.xaml.cs(1608,51) — pré-existentes.

---

## FASE 3 — Toggle "Movimento do reator" (default FALSE)

**Arquivos alterados:** `SettingsService.cs`, `MainWindow.xaml.cs`

`SettingsService.cs`
- `SettingsService.cs:63-69` — nova classe `ReactorSettings` com `IdleMotion { get; set; } = false`.
- `SettingsService.cs:81` — `public ReactorSettings Reactor { get; set; } = new();` em `AppSettings` (mesmo padrão das seções Music/Chat/General).
- `SettingsService.cs:221` — `s.Reactor ??= new ReactorSettings();` no `Validate` (settings editado à mão não derruba).

`MainWindow.xaml.cs`
- `MainWindow.xaml.cs:468-472` — `foreach` dos rotors envolvido por `if (_settings.Reactor.IdleMotion)`; OFF = anéis param no ângulo atual.
- `MainWindow.xaml.cs:483-485` — `coreAlive` agora é `_settings.Reactor.IdleMotion && (_mode == "thinking" || _mode == "speaking" || _talkGlow > 0.01 || _dotHover || _fsMusicOpen)`. Meridianos/sweep permanecem dentro do `if (coreAlive)`.
- `MainWindow.xaml.cs:714-723` — novo `ApplyReactorPrefs(AppSettings s)`: com IdleMotion OFF zera `_breath` e `_corePulsePhase` (repouso imediato).
- `MainWindow.xaml.cs:314` — `ApplyReactorPrefs(_settings)` no boot (ctor, logo após `ApplyMusicPrefs`).
- `MainWindow.xaml.cs:682` — `ApplyReactorPrefs(s)` no `OnSettingsChanged` (aplicação imediata ao vivo).
- `_motionScale` / `MotionEnabled`, tilt/zoom/parallax e timers existentes intocados.

**Build (fase 3):**
```
Compilação com êxito.
    4 Aviso(s)
    0 Erro(s)
```
Avisos: CS8625 em MainWindow.xaml.cs(214,78) e CS8604 em MainWindow.xaml.cs(1625,51) — pré-existentes.

---

## FASE 4 — SettingsPanel: toggle em "Efeitos e desempenho"

**Arquivos alterados:** `SettingsPanel.xaml`, `SettingsPanel.xaml.cs`

`SettingsPanel.xaml`
- `SettingsPanel.xaml:330-336` — dentro de `SecEfeitos` (seção "Efeitos e desempenho"):
  ```xml
  <CheckBox x:Name="ReactorMotionChk" Style="{StaticResource FluentCheckBoxStyle}"
            AutomationProperties.Name="Movimento do reator"
            Content="Movimento do reator (animação autônoma do núcleo e dos anéis)"
            Checked="ReactorMotion_Toggled" Unchecked="ReactorMotion_Toggled"/>
  <TextBlock Text="desligado: anéis param e o núcleo não pulsa sozinho — tilt, zoom e resposta a input continuam."
             Style="{StaticResource InfoTextStyle}"/>
  ```

`SettingsPanel.xaml.cs`
- `SettingsPanel.xaml.cs:60` — `ReactorMotionChk.IsChecked = settings.Reactor.IdleMotion;` no carregamento da UI (mesmo bloco dos toggles de Música).
- `SettingsPanel.xaml.cs:379-385` — wiring idêntico aos toggles de Música:
  ```csharp
  private void ReactorMotion_Toggled(object sender, RoutedEventArgs e)
  {
      if (_loadingUi || _s == null) return;
      _s.Reactor.IdleMotion = ReactorMotionChk.IsChecked == true;
      NotifyChanged();
  }
  ```
  `NotifyChanged()` salva via `SettingsService` e dispara `OnSettingsChanged` → `ApplyReactorPrefs` (imediato).

**Build (fase 4):**
```
Compilação com êxito.
    4 Aviso(s)
    0 Erro(s)
```
Avisos: CS8625 em MainWindow.xaml.cs(214,78) e CS8604 em MainWindow.xaml.cs(1625,51) — pré-existentes.

---

## GATE FINAL

- `dotnet build`: **0 Erro(s)** (4 avisos pré-existentes de nullable).
- Arquivos tocados: `AudioSpectrum.cs`, `MainWindow.xaml.cs`, `SettingsService.cs`, `SettingsPanel.xaml`, `SettingsPanel.xaml.cs`, `TASK.md` (pré-existente) e este relatório.
- Sem commit. Sem execução do app. Nenhum `Freezable` mutado, nenhum `RenderTransform` de estilo animado zerado, `DispatcherTimer`s existentes inalterados.
