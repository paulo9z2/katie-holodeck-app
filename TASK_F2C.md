# TASK F2C — F2.4 Face densa do chat expandido + F2.5 Fullscreen música

> Executável pelo cavaleiro OpenCode. Releia `../planejamento-liquid-glass/OPINIAO_LEVA2_OPENCODE.md` (§F2.4/F2.5 — sua especificação completa) e os reviews `REVIEW_F2A_CODEX.md`/`REVIEW_F2B_CODEX.md`. Build `0/0` em pasta própria (`-p:OutDir=obj/_gate/f2c/`) — o .exe de produção do dono RODA (não matar, não escrever em bin/Debug). Um commit por fase (2 commits).

## F2.4 — Face densa do chat expandido (corrige legibilidade REAL)

**Fato**: `ThemeChatExpandedBrush` = `#380E1B2E` (alpha 0x38 = 22% — reprovado no seu próprio parecer: face por trás de letras com blur 9 atrás). O estado expandido cobre o reator; a face é o único escudo das mensagens.

**Mudança**:
1. NOVO token `ThemeChatExpandedDenseBrush` em `App.xaml`: cor base `#0E1B2E` com alpha medido pra contraste composto ≥4,5:1 p/ `ThemeTextStrongBrush` sobre o pior caso (blur 9 + reator @0,15 + grade do fundo). Partir de ~0xE6 (90%) e medir de verdade (captura + amostra de pixel como você fez na F2B); ajustar até passar. **Zero véu iOS** (V4) — face densa única, o blur ambiente continua vazando pelas margens.
2. `SetChatExpanded(true)`: `Glass.SetFace(ChatFace, "ThemeChatExpandedDenseBrush")` (troca a chave da F2B).
3. O véu local (`_chatVeilBlur` no CenterColumn) e o resto do método: **intactos**.
4. **Gate**: contraste composto medido ≥4,5:1 no pior caso (screenshot da instância própria); 10× expand/recolher sem face presa; build 0/0.

## F2.5 — Fullscreen música (a fase difícil; spec v9.3 intacta: conteúdo NÍTIDO sobre blur)

**Fatos**: `FullscreenMusic` = Grid Z50 (MainWindow.xaml:668+, `Collapsed`/`Opacity=0`); now-playing card (`FsCover` 86×86 R10, `FsTitle` 21px, `FsArtist` 13px); `FsLyricsScroll`/`FsLyricsPanel` (698+); strip de controles (`FsPos`/`FsDur` 10px Consolas `ThemeTextDimBrush`, `FsPlayIcon`); `OpenFullscreenMusic`/`ExitFullscreenMusic` no code-behind (1743+); blur via `SetFsBackdropBlur(true)`.

**Mudanças**:
1. **Escala 2% da entrada: REMOVER** (decisão consolidada) — fica só o fade atual. `grep` do ScaleTransform do FsRoot sai.
2. **Cartão now-playing** (`FsCover`+título/artista) e **strip de controles** (linha do play/timecodes): viram hosts glass (padrão F2B: Path Face + Glass.Ring). Faces densas com tema — `Glass.SetFace` por paleta se o modo live já troca (reusar o pipeline LivePalette, nunca mutar brush frozen — lição v9.8.x).
3. **Letras LRC: face de leitura OPACA** — `FsLyricsPanel` ganha ATRÁS de si um retângulo/face densa (gradiente vertical suave nas bordas é permitido, centro opaco). Letras ficam NÍTIDAS (V1: vidro NUNCA atrás de LRC — aqui a face serve ÀS letras, não é decoração). Bandas de fade topo/base via `OpacityMask` em vermelho? NÃO — usar LinearGradientBrush de opacidade; **validar ClearType** (se fringar, mask sai — gate).
4. **Ênfase da linha ativa por ScaleTransform** (fim do FontSize se existir) — 1,06× com blur bitmap mínimo, vizinhas 0,92 de opacidade (progressão Liquid Lyrics traduzida: linha atual dominante, vizinhas legíveis — SEM word-by-word: LRC é line-level).
5. **Reator congelado sob blur**: `OpenFullscreenMusic` congela `IdleMotion` (reusa o mecanismo existente do blur de Settings — achar como Settings congelou e aplicar igual); `ExitFullscreenMusic` descongela. Zero trabalho de compositor desperdiçado sob o Z50.
6. **Timecodes** `FsPos`/`FsDur`: 10px `ThemeTextDimBrush` → token 4,5:1 (achado do Codex no F2A — texto pequeno pede 4,5:1, não 3:1). Usar `ThemeLabelBrush` (o mesmo da versão da barra, 5,56:1 medido) ou medir e criar `ThemeTextSmallBrush`.
7. **Ciclo de vida**: `SetSubtreeAdornersVisible` aplicado ao abrir/fechar (chanfros/adorners da subárvore coberta colapsam como o chat expandido já faz); ESC fecha (já existe 1421); música esvazia → fecha (já existe 1661) — conferir que os novos hosts sobrevivem a esses fluxos.

**Gate F2.5**: 10× abrir/fechar zero resíduo (adorners voltam, faces íntegras); `enableGlass` OFF → fullscreen idêntico ao hoje em conteúdo (±1px silhueta); p95 ≤16,7ms no scroll de letras (screenshot de frame, sem profiler — razoar por zero efeito novo por item); contraste das letras sobre a face ≥4,5:1; CoverAll ON/OFF (capa larga) intacto.

## PROIBIÇÕES

Sem blur/Effect novo nos cartões (o blur é o backdrop que JÁ existe) · sem aro por letra · sem vidro atrás de LRC/título/artista (face densa de leitura É o contrário disso — ok) · sem word-by-word (sem dado é teatro) · sem mexer em MediaPanel/PlayerWindow (já têm F1/são outro modo) · tokens existentes + os 2 novos justificados por medida · NUNCA matar/copiar por cima do bin/Debug do processo do dono.

## Reporte

Diff resumido por commit, gates com resultado e método de medida, hashes. PT-BR.
