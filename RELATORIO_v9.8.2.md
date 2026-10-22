# RELATÓRIO v9.8.2 — Reparo do módulo música (KatieHoloDeck)

Data: 21/10/2026 · Base: `7d8500e` (v9.6) → HEAD `de7826f` (v9.8.1) · Sem commit, sem branch.

---

## 1. Resumo executivo

**Os 5 sintomas têm UMA causa raiz mestre + 2 gates errados herdados da v9.7.**

A causa mestre é uma exceção em runtime **dentro do caminho da capa-tema**:
`BuildCoverTheme` usava `stackalloc Color[9]`, mas `System.Windows.Media.Color` **não é um tipo
unmanaged** (carrega um `ColorContext` por referência). O CLR lança
`ArgumentException: Cannot use type 'System.Windows.Media.Color'` no primeiro update de mídia
que traz capa. Essa exceção sobe por `UpdateCoverAccent → OnMediaUpdate → Dispatcher.Invoke` e cai
no `catch` do `MediaTracker.PushAsync`, que publica um **`Idle()` com `HasSession=true`**
(`MediaTracker.cs:160`).

Consequência em cascata, a cada evento de mídia:

1. todo o código de música **depois** de `UpdateCoverAccent(u)` nunca executa
   (`_mediaPlaying`, `_spectrum.FeedPlayback`, `SetHasSession`, `SetPlaying`);
2. o `Idle` sobrescreve a UI com "Nada tocando" e chama `SetPlaying(false)` com sessão viva →
   **Ember (12%)** — o "círculo" acende sem música;
3. com música, todo update real morre no mesmo ponto → roda fica presa em Ember, parada e quase
   invisível, sem bands → sintomas 2/3/5;
4. a paleta nunca chega a `ApplyTheme` → capa-tema "morto" (sintoma 4).

Além da causa mestre, dois gates da v9.7 contrariam o ALVO v9.6 e foram corrigidos:

- `SetPlaying` descartava áudio WASAPI vivo quando não havia sessão SMTC (`playing && !_hasSession`);
- Ember acendia com **qualquer** sessão SMTC (browser parado, sessão fantasma) e também com
  "silêncio" durante playback.

---

## 2. Causa raiz por sintoma (arquivo:linha)

| # | Sintoma relatado | Causa raiz exata | Correção |
|---|---|---|---|
| 4 | App inteiro não muda a paleta com a capa | `MainWindow.xaml.cs:844` — `stackalloc Color[9]` em `BuildCoverTheme`; `Color` é managed → `ArgumentException` em **todo** update com capa. Chamada em `MainWindow.xaml.cs:978`; o `catch` de `MediaTracker.cs:160` converte o erro em `Idle(HasSession=true)`. | `ReadOnlySpan<Color> surfaces = new[] { ... }` (array simples; roda 1× por troca de faixa, não por frame). |
| 1 | Círculo ao redor do reator aparece SEM música | Duas camadas: (a) `Idle(HasSession=true)` do `MediaTracker` → `SetPlaying(false)`; (b) `MusicWheel.cs:178` aceitava **qualquer** `_hasSession` como Ember, inclusive sessão fantasma que nunca tocou. | Ember só com `_hasSession && _playedThisSession` (`_playedThisSession` só liga quando a roda fica Active; zera no Standby). Sessão fantasma → Standby invisível. |
| 2 | Com música não muda nada, fica parado e quase invisível | Update real morre na exceção acima → `SetPlaying(true)` nunca é chamado; além disso `MusicWheel.cs:357` transformava qualquer silêncio/stale em Ember (`_hasSession && silent`). | Exceção removida + `emberTarget = _ember ? 1 : 0` (brasa só do estado Ember explícito, ou seja, pausa real). |
| 3 | App não reage à música (sem efeitos) | `_spectrum.FeedPlayback`, `SetHasSession` e `SetPlaying` ficam depois de `UpdateCoverAccent`; com a exceção, nunca rodam. | Mesma correção da causa mestre; a cadeia volta a completar. |
| 5 | App não percebe se há música | O update real é descartado e substituído por `Idle` ("Nada tocando"); além do gate `if (playing && !_hasSession) return;` em `MusicWheel.cs:153` que ignorava áudio WASAPI sem sessão SMTC. | Gate removido: `audioLive = _spectrum.HasAudio \|\| _mediaPlaying` volta a ser o **único dono** da visibilidade (`MainWindow.xaml.cs:1670`). |

### Sobre o "rim do reator" (verificação pedida no item 2 do contrato)

O XAML do rim **não mudou** de v9.6 → HEAD: `ReactorRim` no `App.xaml` tem os mesmos stops
(`#E600D9FF / #2600D9FF / #0F00D9FF`) e o bloco dos anéis em `MainWindow.xaml` (`RingOuter`,
`RingMid`, `ReactorBeads`, `AccentArc`) está idêntico, com as mesmas opacidades. Portanto o
"círculo" que aparecia sem música era a **roda/Ember do `MusicWheel`**, não o rim — e foi
resolvido pelo gate de Ember, sem tocar em XAML.

---

## 3. Evidência experimental (bancada, sem tocar no app do dono)

Harness WPF separado (`%TEMP%\kprobe`, fora do repo) instanciou `MainWindow` real e injetou
`MediaUpdate` pelo método privado `OnMediaUpdate`, lendo o estado interno por reflexão:

```
A phantom-session: playing=False ember=False session=True played=False opacity=0,00
  themeAppliedBase=<null> · shell=#D00D2E43 #D8092030 #E0071723  (ciano padrão)
B playing:         playing=True  ember=False session=True played=True  opacity=1,00
  themeAppliedBase=#FFD5576D · paletteCurrent=#FFD5576D
  shell=#D04A1C24 #D8351319 #E0230B0F  (matiz da capa — mudou de fato)
  AccentBrush=#FFD5576D
C paused:          playing=False ember=True  session=True played=True  opacity=1,00
D lost-session:    playing=False ember=False session=False played=False opacity→0
```

Antes do patch, o passo B lançava:
`System.ArgumentException: Cannot use type 'System.Windows.Media.Color'` em
`BuildCoverTheme` (linha 841) → `TransitionAccentTo` (975) → `UpdateCoverAccent` (1217).
Depois do patch: zero exceções em A/B/C/D e a paleta muda para a cor da capa.

---

## 4. Diff aplicado (2 arquivos de fonte)

`KatieHoloDeck/MainWindow.xaml.cs` (+4/−1)
- `BuildCoverTheme`: `stackalloc Color[9]` → `new[] { ... }` com comentário do porquê.

`KatieHoloDeck/MusicWheel.cs` (+19/−5)
- campo `_playedThisSession` (sessão que já tocou de verdade);
- `SetPlaying`: removido o gate `playing && !_hasSession`; marca `_playedThisSession = true` no Active;
- Ember exige `_hasSession && _playedThisSession && _enabled`;
- `GoStandby`: zera `_playedThisSession`;
- `UpdateMusicState`: `emberTarget = _ember ? 1 : 0` (sem Ember por "silêncio"); removidos
  `silent` e a const `SignalFloor`, que ficaram sem uso.

Nada mais foi alterado. `PlayerWindow.*`, `Selo`, `App.xaml`, `MediaTracker.cs` e o mecanismo do
fix v9.8.1 do boot estão intocados. Nenhum brush congelado é mutado (helpers `Solid/Grad/PenBrush`
preservados). Sem `git commit`, sem branch, sem reescrita de arquivo inteiro.

---

## 5. Build

```
dotnet build KatieHoloDeck/KatieHoloDeck.csproj -c Release
Compilação com êxito.  0 Erro(s)
```

Avisos: 4 warnings de nulabilidade **pré-existentes** (CS8625/CS8604 em `MainWindow.xaml.cs:233`
e `:1863`), nenhum introduzido por este patch.

---

## 6. Como verificar ao vivo (roteiro do orquestrador)

1. Abrir o app **sem música** → roda/anel invisível (nada de brasa).
2. Abrir o navegador com player pausado (sessão SMTC fantasma) → continua invisível.
3. Dar play → anel aparece na hora, gira/pulsa com a energia; título/capa atualizam e **não**
   voltam para "Nada tocando".
4. Trocar de faixa → em ~600 ms a casca/textos mudam de matiz conforme a capa (ex.: capa
   avermelhada → shell vinho; antes ficava ciano fixo).
5. Pausar → brasa a ~12% respirando (5 s).
6. Fechar o player/sessão → anel apaga (Standby).
7. Conferir `%LOCALAPPDATA%\KatieHoloDeck\dispatcher.log` (deve seguir sem entradas novas) e
   `bin\Release\...\media_debug.jsonl` (updates reais continuam).
