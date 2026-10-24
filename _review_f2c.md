# Revisão F2C — Codex

Data: 22/10/2026  
Commits: `fe7a200` (hit-test), `b67dba8` (F2.4 face densa), `fc33680` (F2.5 fullscreen)  
Diff revisado: `git diff 873f0d0..fc33680 --`

## Veredito: APROVADO COM RESSALVAS

Não encontrei bloqueio funcional estático nos três commits. O P2 de hit-test do F2B foi corrigido e o fluxo de saída por faixa vazia percorre o mesmo encerramento do fullscreen. Permanecem duas verificações visuais que o código não prova: a medição exata de 9,6:1 e o modo de rasterização ClearType das linhas esmaecidas.

## Retorno aos apontamentos anteriores

- **F2A P1 — fechado.** O fallback da versão foi corrigido em `Core/AppInfo.cs:24` no histórico anterior, conforme registrado no review F2B; F2C não o reabre.
- **F2A P2 — ainda presente como gate textual herdado.** A busca literal por `9.8.2` continua encontrando comentários/documentação, por exemplo `Glass.cs:34`, `MainWindow.xaml.cs:856`, `MusicWheel.cs:87,158,220,354`, `SettingsPanel.xaml:332` e `SettingsService.cs:79`. São referências históricas, não outra fonte da versão exibida, e F2C não as introduz.
- **F2B P2 — hit-test fechado.** Os Grids externos dos hosts agora têm `Background="Transparent"` em `MainWindow.xaml:180` e `MainWindow.xaml:557`. Como o fundo hit-testável cobre o Grid completo, a sangria deixa de atravessar para o `ReactorHost`.
- A ressalva visual anterior sobre a sutileza do aro não foi reavaliada em execução; os tokens do aro não mudaram nestes commits.

## Checklist F2C

1. **F2.4 — face densa: código aprovado; medição exata não comprovada.** O token `ThemeChatExpandedDenseBrush` existe em `App.xaml:99` com alpha `0xE6`. `LivePalette` registra e atualiza a mesma chave em `LivePalette.cs:241,397`; `SetChatExpanded(true)` usa essa chave em `MainWindow.xaml.cs:1794`. **P2 — medição não auditável:** não encontrei captura ou log de amostragem F2C; os artefatos locais disponíveis são da F2B. Portanto, confirmo token, alpha e ligação, mas não atesto independentemente o valor medido de **9,6:1** descrito em `App.xaml:95,96`.

2. **Hit-test dos hosts: aprovado.** `Background="Transparent"` está no Grid externo, antes do Path não hit-testável, tanto em Telemetria quanto em Chat (`MainWindow.xaml:180,557`). Isso cobre a área de sangria sem adicionar pintura.

3. **F2.5 — escala e layout: aprovados.** `FullscreenScale` não aparece em `MainWindow.xaml` nem em `MainWindow.xaml.cs`; a entrada/saída ficou só com fade. No now-playing, `Margin 0,13,0,0` + `Padding 16,13,16,8` preserva o início do conteúdo em 26 DIP e a altura de 120 DIP (`MainWindow.xaml:688,689`). No strip, `Padding 14,6,14,10` + `Margin 0,0,0,6` preserva o conteúdo a 6 DIP e a altura de 48 DIP (`MainWindow.xaml:742,743`). `ThemeMediaGlassBrush` existe em `App.xaml:88` e é atualizado pela paleta viva em `LivePalette.cs:235,390`.

4. **LRC — máscara no lugar certo; ClearType requer confirmação visual.** A face opaca usa `ThemePanelSolidBrush` e seu `OpacityMask` está no `Border` de fundo (`MainWindow.xaml:722,725`), fora do `ScrollViewer` e dos `TextBlock`; o brush sólido está em `App.xaml:100`. Cada linha nasce com `ScaleTransform(1,1)` e origem central em `MainWindow.xaml.cs:2003,2004`, permitindo que `SetLineEmphasis` aplique escala sem mudar métricas (`MainWindow.xaml.cs:2072,2084,2089`). **P2 — verificação visual:** linhas não ativas recebem `Opacity=0.92` em `MainWindow.xaml.cs:2082`; embora a máscara não cubra o texto, essa composição pode tirar ClearType dessas linhas. Não há captura F2C para confirmar a rasterização final.

5. **Pausa do reator e auto-exit: aprovados estaticamente.** `SetReactorPaused` apenas compartilha os mesmos gates de repouso já usados por `IdleMotion` no loop de render (`MainWindow.xaml.cs:498,513,764,767`); não adiciona compositor nem efeito. A saída limpa o TechFrame, recolhe adorners da subárvore, para `_lyricsTimer` e descongela o reator (`MainWindow.xaml.cs:1927,1930,1933,1937,1938`). O caso `fsEmpty` chama `ExitFullscreenMusic()` enquanto `_fsMusicOpen` ainda está ativo (`MainWindow.xaml.cs:1676`), então não cai no retorno antecipado. O timer também verifica `_fsMusicOpen` antes de sincronizar linhas (`MainWindow.xaml.cs:2032,2034`). Não encontrei caminho estático que deixe adorner órfão ou timer de sincronização rodando com o painel fechado.

6. **Recursos dinâmicos/runtime: sem chave ausente identificada.** As novas chaves `ThemeChatExpandedDenseBrush`, `ThemeMediaGlassBrush`, `ThemePanelSolidBrush` e `ThemeLabelBrush` existem em `App.xaml` e são registradas/atualizadas por `LivePalette`. O preenchimento dos Path usa `SetResourceReference` em `Glass.ApplyFace`, compatível com a paleta mutável. O diff também troca `FsPos`/`FsDur` para `ThemeLabelBrush` (`MainWindow.xaml:751,764`). Não identifiquei risco estático de `DynamicResource` ausente.

## Escopo da verificação

Revisão estática do diff e dos fluxos relacionados. Não rodei build nem abri o app; por isso, contraste medido, ClearType, paridade visual real e repetição de abrir/fechar ficam sem validação em execução.

As cópias dos reviews anteriores estavam no workspace como `_review_f2a.md` e `_review_f2b.md`; os arquivos com nomes `REVIEW_F2A_CODEX.md` e `REVIEW_F2B_CODEX.md` não estavam presentes na pasta `../planejamento-liquid-glass/`.
