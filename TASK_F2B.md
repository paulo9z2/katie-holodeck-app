# TASK F2B — Fix P1 (review) + F2.2 Telemetria + F2.3 Chat compacta

> Executável pelo cavaleiro OpenCode. Releia sua `../planejamento-liquid-glass/OPINIAO_LEVA2_OPENCODE.md` (§5.2 padrão de host, F2.2/F2.3) e o review `../planejamento-liquid-glass/REVIEW_F2A_CODEX.md`. Build `0 erros/0 warnings`. **Dois commits separados** (fix primeiro, feature depois).

## Commit 1 — Fix P1 do review Codex (REPROVADO nesse ponto)

`Core/AppInfo.cs:20`: o fallback `"v0.0.0"` é literal de versão em C# (viola a fonte única). Trocar: se `Version == null` → `string.Empty` (UI mostra nada; versão que não vem do csproj não existe). Ajustar doc do XML comment. Gate: `grep -n "v0\|1\.0\.0\|9\.8\.2" Core/AppInfo.cs` → zero ocorrências de número.

## Commit 2 — F2.2: Ilha TELEMETRIA vira host glass (a prova do padrão)

**Hoje** (MainWindow.xaml:165-172): `Border x:Name="IslandTelemetry"` com `Style=FloatingIslandStyle` (Padding=14, Background=IslandGlass, hairline AccentEdgeBrush) + overrides locais `BorderBrush=PanelEdgeLiveBrush`, `local:Chamfer.Size="18"`, `local:TechFrame.Enabled="True"`; filho = `Grid` com `Rectangle SurfaceTintBrush` + `StackPanel` (labels/barras).

**Padrão de host** (sua especificação §5.2, aplicado aqui):
1. `Border`: `Padding="0"`, `Background="{x:Null}"`, `BorderThickness="0"` (remover hairline do Border — o aro Glass assume), manter `Chamfer.Size="18"` e `TechFrame.Enabled="True"`, `Glass.Ring="True"`
2. Primeiro filho: `Path` Glass.Face em sangria (aro 1px neutro + faceta + specular — o mesmo desenho F1), `x:Name="TelemetryFace"`
3. `Grid` interno com `Margin="14"` (o padding antigo vira margem) → `Rectangle SurfaceTintBrush` + conteúdo **intacto** (labels, ProgressBars, tudo)
4. Fill da face via `Glass.SetFace` — nunca `Border.Background`

**Gate F2.2**: build 0/0; toggle `enableGlass` OFF → ilha visualmente idêntica ao hoje (±1px); nenhum `Effect` novo; aro acompanha o chanfro 18 sem quebrar o TechFrame (adorners vivos); contraste dos labels não muda (conteúdo intocado).

## Commit 2 (continuação) — F2.3: Ilha CONVERSA compacta + mata o bug `Background=null`

**Hoje** (MainWindow.xaml:524+, code-behind 1758-1815): `Border x:Name="IslandChat"` mesmo style; no expand (`SetChatExpanded(true)`) faz `IslandChat.SetResourceReference(Border.BackgroundProperty, "ThemeChatExpandedBrush")`; no recolher (~1810) faz **`IslandChat.Background = null;` — BUG: valor local null PREVALECE sobre o setter do Style (IslandGlass @60% NÃO volta, o comentário "restaura o vidro" mente).**

**Mudanças**:
1. Aplicar o mesmo padrão de host da F2.2 (Path `x:Name="ChatFace"`)
2. Expand: fill do `ChatFace` troca pra `ThemeChatExpandedBrush` via `Glass.SetFace` (não via `Border.Background`) — a animação de 180ms e o resto do método fica intacto
3. Recolher: `Glass.SetFace(IslandChat, null)` restaura o fill default do glass (mata o `Background = null` e o bug) — o `FloatingIslandStyle` deixa de ser a fonte do fill (o Path é)
4. `ChatExpandBtn`, `ChatInputBorder`, `SendBtn`, bolhas, `ChatScroll`: **NÃO TOCAR**

**Gate F2.3**: build 0/0; 10× expand/recolher sem aro deslocado nem face presa (o Path é filho do Border — herda Column/ColumnSpan normalmente); modo compacta com aro = telemetria com aro (mesma língua); `grep -n "IslandChat.Background" MainWindow.xaml.cs` → zero.

## PROIBIÇÕES (valem os dois commits)

Sem `Effect`/blur novo · sem aro por item · sem tocar em chips/reator/LRC/PlayerWindow · tokens existentes apenas (IslandGlass, SurfaceTintBrush, PanelEdgeLiveBrush, ThemeChatExpandedBrush, GlassEdgeBrush) · build alternativo em pasta própria (`-p:OutDir=obj/_gate/f2b/`) — o .exe de produção tá RODANDO (PID do sir), NUNCA matar/copiar por cima do bin/Debug.

## Reporte

Diff resumido por commit, gates com resultado, hash de cada commit. PT-BR.
