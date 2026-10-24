# TASK_F2.7_F3.2 — PlayerWindow flutuante + Settings no liquid glass

Contexto: Leva 2 em andamento; F2.0-F2.6 + F3.1 commitadas (main em `00705b4`, incluindo o fix P1 do Codex: chips com `Background={x:Null}` — Border do host NUNCA carrega fill, o `Glass.Face` pinta fill nos dois modos). Padrão F2B é a lei: Path Face + `Glass.Ring` + Grid externo `Background="Transparent"` pro hit-test + margem interna compensando `BorderThickness`. Máx 2 níveis de vidro por composição. Um único fill por host.

**Prioridade: F2.7 primeiro (commit independente), depois F3.2 (commit independente).** Se o run apertar (60min), commite F2.7 com gates completos e reporte o estado exato da F3.2 para retomada. NUNCA commite fase com gate faltando.

## F2.7 — PlayerWindow flutuante no material (commit 1)

Arquivo: `PlayerWindow.xaml` (+ `PlayerWindow.xaml.cs` se necessário). Janela `WindowStyle=None` + `AllowsTransparency`, Border root `CornerRadius=16` com `Background=#E6101A30` (superfície DURA hoje) + `BorderBrush=#3300FF88`.

Fazer:
1. Border root vira host glass F2B: `Background={x:Null}`, `local:Glass.Ring="True"`, `local:Glass.Face` com `FaceFrosted=IslandGlass`, Path de face com raio puro (CornerRadius 16, `Chamfer.Size=0` — `ApplyFace` já suporta: lê `host.CornerRadius.TopLeft` como `r` e `Chamfer` como `c`; c=0 → canto redondo puro).
2. O fill OFF (vidro desligado) deve preservar o `#E6101A30` atual (face não-frosted = esse tom) — defina/uso token existente com esse valor, sem inventar cor nova.
3. Grid externo `Background="Transparent"` mantém hit-test; TitleBar continua dragável (MouseDown→DragMove não pode quebrar; o Grid transparente externo NÃO pode engolir o drag).
4. Botões prev/play/next: mantêm assinatura verde na borda/ícone (`BorderBrush`/foreground atuais); face pode receber `ThemeGlassBrush` translúcido SE o hit-test e os MouseLeftButtonDown seguem intactos. Se duvidar, face fica como está — botões pequenos de 22-32px com vidro triplo = ruído; a regra de 2 níveis manda: host vidro + botões vidro = 2 níveis, ok, mas priorize a legibilidade.
5. `ProgressTrack`/`ProgressFill`: track pode translucidir; fill é assinatura funcional, NUNCA muda.
6. Volume slider, cover art, textos: intocados (fora do contrato).

Gates F2.7 (todos obrigatórios, instância própria em `obj/_gate/f27/`, produção do dono RODANDO — nunca tocar `final .exe/`):
- [ ] Build 0 erros / 0 avisos
- [ ] OFF parity: screenshot com `enableGlass=false` vs build atual — diferença máxima ±1px por região (face fora do padding)
- [ ] ON: aro do mini-player com luminância ≥2× o fundo imediato (amostra de pixel documentada)
- [ ] Drag: 10× arrastar a janela sem travar, sem perder interação
- [ ] Play/prev/next clicáveis (command/spyhook real ou log), volume slider responde
- [ ] 10× abrir/fechar a janela sem crash/resíduo
- [ ] Zero `Effect` novo no XAML; blur continua vindo da infra Glass existente

## F3.2 — SettingsPanel no material (commit 2)

Arquivo: `SettingsPanel.xaml` (+ code-behind só se imprescindível). Painel 920×580 no `SettingsHost` do MainWindow, nav lateral de 8 RadioButtons, seções colapsáveis (`SecAparencia`…), switches animados (Ellipse `Ring` + Path `Tick`), swatches, HexBox, `GlassChk` (o próprio toggle do vidro), `RestoreDefaultsBtn`.

Fazer:
1. `PanelCard` (Border raiz do painel, linha ~245) vira host glass F2B: `Background={x:Null}`, `Glass.Ring=True`, Path Face `FaceFrosted=IslandGlass`, fill OFF preservando o tom atual do card.
2. Nav lateral (RailWidth container dos NavItemStyle): pode ganhar sub-nível de vidro (2º nível, o máximo permitido). Os RadioButtons de nav mantêm estados Checked/hover — storyboard do `NavItemStyle` intocado.
3. Cards/linhas de setting dentro das seções: SEM vidro (3º nível proibido) — permanecem como estão; no máximo o `GhostButtonStyle` segue como está.
4. Switches (Ring/Tick): animações intocadas; nada de vidro em controle de 30px.
5. `GlassChk` é o controlador do modo: ao flipar, o LivePalette re-renderiza a face do painel AO VIVO (Gate: flip dentro do painel aberto, sem fechar o SettingsHost).
6. NÃO mexer: lógica de persistência (SettingsService), HexBox, swatches, RestoreDefaults, acessibilidade (`AutomationProperties`) — visual host apenas.

Gates F3.2 (obrigatórios, instância própria `obj/_gate/f32/`):
- [ ] Build 0/0
- [ ] 8 abas navegam (1 clique cada, painel troca seção sem crash)
- [ ] GlassChk flip ao vivo: painel re-renderiza glass↔OFF sem fechar, sem aro deslocado
- [ ] Swatch de cor (Sw1→Sw3) muda assinatura sem quebrar a face
- [ ] OFF parity ±1px (screenshot enableGlass=false vs atual)
- [ ] Máx 2 níveis de vidro verificados na árvore (host + nav)
- [ ] RestoreDefaults e HexBox seguem funcionais (1 teste cada)
- [ ] Zero `Effect` novo

## Proibições globais (invioláveis)
- Sem `Effect` novo em lugar nenhum; sem DropShadow além das existentes
- Sem bump de versão (fica 9.8.2)
- Produção do dono RODANDO: build SEMPRE em `-p:OutDir=obj/_gate/fXX/`, nunca em `final .exe/`
- Animações/storyboards existentes: intocadas (NavItemStyle, switches, chips)
- Releia `OPINIAO_LEVA2_OPENCODE.md`, `REVIEW_F2B_CODEX.md` e o commit `00705b4` (o padrão Border={x:Null} é OBRIGATÓRIO em todo host novo)

## Reporte
Diff por fase + gates com números (pixels, contagens) + hashes dos commits. PT-BR.
