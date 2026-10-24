# Plano — Liquid Glass 100%

**Projeto:** KatieHoloDeck (WPF, **.NET 8**, `net8.0-windows10.0.19041` — confirmado em `KatieHoloDeck.csproj:5`; a rodada 1 dizia ".NET 9" por engano)
**Versão congelada:** 9.8.2 — **não subir versão nesta rodada**
**Data:** 23/10/2026 (rodada 2)
**Status:** rodada 2 — achados do `docs/REVIEW_plano_r1_CODEX.md` incorporados; **6 decisões do dono registradas como RESOLVIDAS** (a–f, incl. regra R15); escopo aprovado `F0.1 → F0.2 → F3.3`; aguardando "pode começar F0.1".
**Escopo deste documento:** plano de execução. **Nenhuma linha de código foi alterada nesta rodada.** Nada foi commitado (ordem do dono).

---

## 0. Como ler este documento

- Toda referência a arquivo usa `arquivo:linha` verificada na leitura do código atual.
- `ON` = superfície já recebe vidro. `PARCIAL` = host com vidro, internos opacos. `FORA` = não tem vidro e precisa de decisão/implementação. `EXCEÇÃO` = não deve receber vidro, com motivo escrito.
- **Uma linha do inventário ≠ necessariamente um elemento.** Cada linha é uma *superfície* (elemento único) **ou** uma família de controles do mesmo host (ex.: `05` = telemetria inteira, `09-11` = os três chips, `28/29` = controles do painel). A seção 5.1 explica a relação linha ↔ hosts de vidro (`Glass.Face`/`Glass.Ring`).
- Esforço: **Q** ≤ 0,5 dia · **M** 1–2 dias · **D** > 2 dias.
- Impacto: 1 (imperceptível) a 5 (define a percepção de "produto de vidro").
- Gates são numéricos e verificáveis à mão. Gate sem número não entra.
- **Todo gate tem procedimento escrito de medição na seção 10.1.** Gate que dependa de "olhar e ver" não é gate.
- **R15 (checkpoint por fase):** toda fase termina em **PARADA OBRIGATÓRIA** — o dev para, entrega a evidência e espera aprovação explícita do dono antes da fase seguinte. Ver seção 2.
- Nada aqui autoriza implementação. Fases só começam com "pode começar F<sub>x</sub>".

---

## 1. Resumo executivo

O app **já tem vidro vetorial**, e ele já passa nos gates de luminância do aro (`2,73×` e `2,68×` medidos, commit `34f7664`). O que falta para "100%" não é reescrever o material — é:

1. **Cobertura.** 10 de **37** superfícies recebem vidro; 3 recebem pela metade; **14** estão fora e 10 são exceções deliberadas. A percepção de "100%" vem de cobrir as 14 + fechar as 3 parciais, sem estourar a regra de 2 níveis. (A rodada 1 contava 35 porque faltavam duas barras de rolagem — ver P1-A na seção 5.)
2. **Controle.** O dono não consegue ajustar nada: existe **um único** `CheckBox` (`GlassChk`, `SettingsPanel.xaml:338`) e uma linha de texto informativo (`:342`). Não há slider, cor, intensidade nem preview. **Esta é a prioridade zero declarada pelo dono** e é a fase `F3.3`.
3. **Um bug real de pintura.** `MediaPanel` pode abrir **sem pintura nenhuma** no primeiro `Loaded`, porque `Glass.ApplyFace` assina apenas `p.Loaded`/`p.SizeChanged` do `Path` — nunca o tamanho do host — e um host com `Height=0` no `Loaded` faz `ApplyFace` sair em `Glass.cs:389` **antes de construir a geometria** (`Path.Data` fica **nulo**, não "0×0"), e um `Path` com `Data=null` arranja `Size.Empty`, nunca dispara `SizeChanged` e nunca re-tenta. É a fase `F0.1`, obrigatoriamente antes de qualquer fase visual.

**Escopo aprovado pelo dono nesta rodada:** `F0.1` → `F0.2` → `F3.3` e **parada** (checkpoint R15) para avaliação antes de qualquer `F4.x`. O resto do documento continua completo porque é o mapa da cobertura — não é autorização de execução.

Sequência completa (referência): `F0.1` → `F0.2` → `F3.3` → `F4.0` → `F4.1` → `F4.2` → `F4.3` → `F4.4` → `F4.5` → `F4.6`.
Total estimado: **~15,5 dias** de trabalho de um dev (só até `F3.3`: **~4 dias**).

Riscos maiores, resumidos: `LivePalette.ApplyTheme` reescreve os alphas do vidro a cada troca de capa (sliders do usuário seriam apagados); `Glass.Apply` faz *early-return* quando o modo não muda (`Glass.cs:73`), então mudança de token não repinta sozinha; **cinco tokens de vidro são consumidos em dois caminhos diferentes (face e `Background` direto)** — mexer neles no lugar quebra a paridade OFF, e por isso a rodada 2 cria uma camada de tokens só-vidro (seção 4.6); e levar vidro a mais superfícies multiplica adorners/`DropShadowEffect` (já são **35** no app) e pode engolir cliques, problema já documentado no F2.6.

---

## 2. Regras invioláveis (contrato desta rodada)

Estas regras vêm do F2.6/F3.1 e do `_review_f2c.md`. Violação = fase rejeitada, sem discussão.

| # | Regra |
|---|---|
| R1 | **Versão congelada em 9.8.2.** Nenhum bump até aprovação final de F4.6. |
| R2 | **Zero dependência nova.** Nenhum NuGet, nenhum DLL externo, nenhum instalador. (APIs do próprio Windows SDK do TFM — `UISettings`, `SystemParameters` — não contam: já estão no build.) |
| R3 | **Zero `Effect` novo.** Os **35** `DropShadowEffect` existentes (App.xaml 4, FluentControls 8, MainWindow 20, PlayerWindow 2, SettingsPanel 1 — contagem reconferida nesta rodada) são teto. O vidro é vetorial (`Path` + gradientes + strokes). |
| R4 | **Máximo 2 níveis de vidro por composição.** Host de vidro sobre host de vidro = proibido. |
| R5 | **Um único fill por host.** Nada de `Path` de face + `Border` com `Background` no mesmo elemento. |
| R6 | **Sem acento na face nem no aro.** R13/R14: acento é assinatura de *estado*, não de material. (Única exceção: opção explícita do dono, default OFF — ver `RingFollowsAccent`.) **Consequência para a moldura:** o aro do `AppShell` é neutro; o traço de acento que existia ali **sai**, não é mantido como "segundo traço". Nada de borda dupla. |
| R7 | **Padrão F2B obrigatório** para host **com face** de vidro: `Path` com `Glass.Face` + `Glass.Ring="True"` + `Grid` externo `Background="Transparent"` + margem interna compensando `BorderThickness`. |
| **R7-a** | **Host de aro (ring-only).** Um elemento pode receber `Glass.Ring="True"` **sem** `Path`/`Glass.Face` **somente** se já pintar o próprio fill por outro meio (exceção única desta rodada: `AppShell`, `MainWindow.xaml:70`, que pinta `ThemeShellBrush`). Nesse caso: (i) não entra na contagem de `Face` da seção 9; (ii) precisa zerar o traço de borda em ON via `Glass.HideBorderStroke` (senão há borda dupla — ver F4.1); (iii) `Glass.Ring` já é independente de `Glass.Face` no código (`ApplyRing` em `Glass.cs:138` não consulta `Face`). |
| R8 | **Toggle OFF = retorno EXATO ao visual de hoje (±1px).** Com `EnableGlass=false`, nenhum token novo pode tocar em brush, tamanho ou margem. **Inclui:** os tokens só-vidro são recalculados para o valor base e o caminho OFF lê exclusivamente `Glass.Face`/`FallbackStroke` (seção 4.6 e F3.3 / plumbing). |
| R9 | **Luminância do aro ≥ 2× o fundo imediato.** Medido conforme 10.1, não estimado. |
| R10 | **Hit-test preservado.** Chips e botões de vidro: 10/10 cliques (UIA ou mouse-walk). A sangria do `Grid` transparente não pode roubar clique de irmão. |
| R11 | **`TechFrame` e `Chamfer` não vão para superfície de 2º nível** (nav rail, popups, internos de painel). Só 1º nível. |
| R12 | **Doc consistente com código.** Se o comportamento muda, o comentário muda na mesma fase. |
| R13 | **Build 0 erro / 0 warning** ao fim de cada fase. |
| R14 | **Nada de refração real por HLSL.** Ver `F4.5` para o motivo técnico. |
| **R15** | **Checkpoint por fase (regra permanente de processo).** Nenhuma fase começa antes de a anterior ser **aprovada pelo dono**. Ao fim de cada fase o dev **PARA**: entrega (a) evidência dos gates, (b) o que mudou, (c) o que ficou fora, e espera "aprovado, pode seguir". Vale para F0.1, F0.2, F3.3, F4.0–F4.6, sem exceção e sem "já que estava ali". Fase não aprovada não é commitada em cima pela fase seguinte. |

---

## 3. Definição operacional de "Liquid Glass 100%"

Uma superfície está em 100% quando **as seis** condições valem:

1. **Material:** tem face vetorial, faceta interna, aro e especular coerentes com os tokens vivos (não com literais).
2. **Estados:** Normal / Hover / Pressed / FocusVisible / Disabled definidos e distintos, modulando **luminância do material** (não acento, salvo controles de assinatura).
3. **Contraste:** texto sobre o vidro ≥ 4,5:1 contra o pior fundo real (wallpaper claro **e** escuro) e ≥ 7:1 em conteúdo denso.
4. **Fallback:** funciona em **quatro** caminhos — `EnableGlass=OFF`, `EffectsProfile=none`, sessão remota / GPU Tier < 2, e **modo opaco** (alto contraste ou "reduzir transparência" do Windows). O modo opaco é **distinto** de OFF: OFF preserva os fills translúcidos do 9.8.2; opaco os leva a α = 255. Ver F4.3.
5. **Custo:** sem `Effect` novo; p95 de frame com vidro ≤ baseline + 1,2 ms.
6. **Controle:** o dono consegue ver e ajustar o que essa superfície usa em `Configurações → Liquid Glass`, com preview ao vivo.

O app está em 100% quando **as 37 superfícies** estiverem classificadas em `ON`/`EXCEÇÃO` (nenhuma `PARCIAL` ou `FORA` sem decisão escrita), e todos os gates da seção 10 passarem.

---

## 4. Arquitetura atual (verificada)

### 4.1 Como o vidro é aplicado

- `Glass.cs:54` — `Glass.Resolve` **sempre retorna `Vector`** (`:56` para `!enabled` → `Off`; `:58` e `:59` → `Vector`). O valor `Shader` do enum existe e não é usado em lugar nenhum. Consequência: não há caminho de shader hoje; qualquer "modo shader" é fachada e não deve ser exposto como opção real.
- `Glass.cs:374` — `ApplyFace` cria/atualiza a geometria (octógono com `Chamfer`) no `Path` e assina **somente** `p.Loaded` e `p.SizeChanged` (`:363-366`). **Não assina o host.** É a causa do bug do `MediaPanel`.
  - Dentro dele, `:384-388` já existe um resgate parcial (F2.7-fix): se o `Path` mede < 4 e o **host** mede ≥ 4, herda o tamanho do host naquele instante. O resgate falha exatamente no caso do `MediaPanel`, porque o host também está 0×0 nesse `Loaded`.
  - `:389` — `if (w < 4 || h < 4) return;` acontece **antes** de qualquer escrita em `p.Data`. Logo, no defeito, `Path.Data` é **`null`** (não uma geometria 0×0). Um `Shape` com `Data=null` e `Stretch=Fill` arranja `Size.Empty` → `ActualWidth` nunca sai de 0 → `SizeChanged` do `Path` nunca dispara → a face nunca pinta.
  - `:391-395` — seleção de fill: `glass ? GetFaceFrosted(p) : ""`, com fallback para `GetFace(p)`. **É aqui que o caminho OFF ignora tudo o que é vidro.** Ver 4.6 e P1-C em F3.3.
  - `:401-406` — **a guarda de idempotência já existe** (`st.Has || st.W != w || …`). A rodada 1 a listava como "mudança nova" da F0.1: não é. Só a subscription do host é nova.
- `Glass.cs:92-105` — `Refresh()` reaplica as faces/hosts inscritos e chama `InvalidateVisual()` nos anéis. É o **único** gancho de repintura ao vivo.
- `Glass.cs:70-76` — `Apply(enabled, profile)` **faz early-return se `next == _mode`**. Chamado em `MainWindow.xaml.cs:1271`. Mudar token exige `Refresh()` (ou mutação de brush), não `Apply()`.
- `Glass.cs:138-150` — `ApplyRing` independe de `Face`: pega o `AdornerLayer`, cria/reusa o `RingAdorner` e invalida. É o que permite o host de aro (R7-a).
- `Glass.cs:158-165` — `EdgeInset` (0,5) e `FacetWidth` (1,5) são **const privadas do `RingAdorner`**, e a espessura do aro é uma **terceira** constante: `_edge.Thickness = 1.0` (`:163`). Para virar slider, as três precisam ser promovidas (o plano da rodada 1 citava só duas).
- `Glass.cs:191-196` — brushes vivos do anel, re-resolvidos **por referência** a cada render; `CheckSum` decide se reaplica. É por isso que trocar o brush no dicionário (caso frozen) também propaga.
- `Glass.cs:454-477` — `DimWhenGlass`: hosts marcados recebem tinta mais escura quando o vidro está ativo (usado no telemetry island via `SurfaceTintBrush`). Padrão de attached property a copiar para `HideBorderStroke` (F4.1).
- Contagem de hosts (reconferida nesta rodada por `grep`): **11** elementos com `Face` (9 em `MainWindow.xaml`, 1 em `PlayerWindow.xaml`, 1 em `SettingsPanel.xaml`) e **11** com `Glass.Ring="True"` (9 + 1 + 1). Batem 1:1 — coerente. Ver seção 9 para os totais previstos.

### 4.2 De onde vêm as cores

- `LivePalette.cs:367` — `ApplyTheme` recalcula a paleta a cada troca de capa.
- `LivePalette.cs:421-422` — **reescreve os alphas do aro do vidro** (`0x4D`, `0x57`). Qualquer alpha custom do usuário seria apagado no próximo tick de tema.
- `LivePalette.cs:162` — `_glassHighlight` é `readonly` e inicializado com `0x55`, e **`ApplyTheme`/`SetAccent` nunca o tocam** (não aparece em nenhum dos dois métodos). **Correção da rodada 1 (P3 do Codex): `readonly` NÃO prova que o brush está frozen** — não há `Freeze()` em nenhum ponto da paleta (grep: os únicos `Freeze()` do projeto são em bitmaps/pincéis locais de `MainWindow.xaml.cs`, `MusicWheel.cs` e `PlayerWindow.xaml.cs`). O que existe é defesa reativa: os helpers `Solid`/`Grad` (`LivePalette.cs:268-283`) checam `IsFrozen` e, quando frozen, **substituem a entrada do dicionário**. Portanto o requisito não é "descongelar", é garantir que o consumidor re-resolva a referência (o `RingAdorner` já faz, `Glass.cs:191-196`; o `Path` usa `SetResourceReference`, `Glass.cs:407`, que também resolve de novo). `_glassHighlight` só vai precisar deixar de ser `readonly` se o slider de especular for implementado por mutação in-place.
- Alphas de face hoje: `IslandGlass` `0x66`/`0x4D` (40,0%/30,2%) — `App.xaml:38-41`, `LivePalette.cs:406-410`; `ThemeMediaGlassBrush` `0x99` (60,0%) — `App.xaml:88`, `LivePalette.cs:394`; `ThemeChipGlassBrush` `0x22` (13,3%) — `App.xaml:92`, `LivePalette.cs:398`.
- Alphas de aro hoje: `GlassEdgeBrush` `#4DFFFFFF` (30,2%) — `App.xaml:117`, `LivePalette.cs:421`; `GlassFacetBrush` `#57FFFFFF` (34,1%) — `App.xaml:118`, `LivePalette.cs:422`; `GlassEdgeHighlightBrush` `#55FFFFFF` (33,3%) — `App.xaml:119`, não mutado.
- **Tokens declarados e sem consumidor** (grep no projeto inteiro, excluindo `bin/obj`): `PanelGlassStrong` (`App.xaml:45`) e `ThemeChatExpandedBrush` (`App.xaml:93`). `LivePalette` os calcula (`:411-414`, `:399`) mas **nenhum XAML os referencia**. Classificação: `PanelGlassStrong` ganha consumidor em F4.2 (face do popup do ComboBox); `ThemeChatExpandedBrush` é removido em F4.6 (limpeza) se até lá não achar consumidor.

### 4.3 Configurações

- `SettingsService.cs:72` — `AppSettings`; `:80` — `EnableGlass` (default **true**, comentário antigo diz "Default OFF" — inconsistência de doc, **não corrigir sem pedido**).
- `:188` — `Save` atômico (tmp + move); `:202` — `Validate` (clamp).
- `MainWindow.xaml.cs:324` — caminho do `settings.json` fora do app dir (`BaseDirectory` + `settings.json`).
- `MainWindow.xaml.cs:690` — `OnSettingsChanged`; `:731` — `Save`; `:1256` — `ApplyEffectsProfile`; `:1271` — `Glass.Apply`.
- `SettingsPanel.xaml` — 8 itens de nav (`:279-286`), `NavAparencia` marcado por padrão; `GlassChk` em `:338`; `GlassModeInfo` em `:342`; radios de efeito `:353-355`; slider `:441`; combo `:452`; **`ScrollViewer` do conteúdo em `:299` com `ThinScrollBarStyle` em `:301`** (fonte da scrollbar que faltava no inventário).
- `SettingsPanel.xaml.cs` — `NotifyChanged` `:100`; `Glass_Toggled` `:194`; validação de HEX `TryParseAccent` em `:167` (`ApplyHexInput` `:164`), com o padrão exato a reusar no campo de cor do vidro: inválido → mensagem, **não** escreve no JSON; `Nav_Checked` `:124` (mapeia tag → `Visibility` de cada seção — a 9ª aba exige uma linha nova aqui).

### 4.4 Ambiente / fallback

- `DwmCompat.cs:169` — `ApplyBackdrop`; `:36` — gate de build **22621**.
- **Não existe detecção de sessão remota, de `RenderCapability.Tier`, de alto contraste nem de "reduzir transparência"** em nenhum arquivo. `grep` por `SystemParameters|IsRemoteSession|RenderCapability|HighContrast|UISettings` retorna só dois usos não relacionados (`MainWindow.xaml.cs:480` e `PlayerWindow.xaml.cs:41`, ambos `WorkArea`/borda de resize). Precisa ser criado em `F4.3` (opaco) e `F4.4` (remoto/tier).
- **As APIs reais** (correção do P1-D; ver F4.3 para o desenho): `SystemParameters.HighContrast` (WPF, alto contraste), `UISettings.AdvancedEffectsEnabled` (WinRT, **é** o "Transparency effects" do Windows; existe desde 10.0.15063 e o TFM 19041 já projeta `Windows.UI.ViewManagement`), `UISettings.AnimationsEnabled` (animações), e `SystemParameters.ClientAreaAnimation` (**animações da área cliente — NÃO é transparência**, era o erro da rodada 1).
- **Não existe instrumentação de frame/FPS.** A telemetria atual é CPU/GPU via `nvidia-smi`. Também não existe captura determinística: `grep` por `RenderTargetBitmap|CopyFromScreen|PrintWindow` retorna **zero**. O harness de F0.2 é código novo (DEBUG-only).

### 4.5 Onde o dono enxerga (e o problema)

`SettingsPanel` cobre o app (card de 920×580 com scrim `F5`). **Logo, "preview ao vivo" nas ilhas é impossível enquanto o painel está aberto.** O preview tem de ser **local**, dentro da seção — ver `F3.3 / PreviewTile`.

### 4.6 Camada de tokens — base vs. só-vidro (a resposta ao P1-B e ao P1-C)

**O problema, medido no código:** os tokens de face são consumidos por **dois caminhos diferentes**:

1. **Caminho vidro:** `Glass.ApplyFace` → `SetResourceReference(Shape.FillProperty, …)` (`Glass.cs:407`), escolhendo `FaceFrosted` quando ON (`:392`) ou `Face` quando OFF (`:393`).
2. **Caminho direto:** `Background="{DynamicResource <token>}"` em elementos que **não** são hosts de vidro e continuam visíveis com o vidro desligado.

Se um slider mutar esse token no lugar, o caminho 2 muda junto → **a paridade OFF (R8/G2) quebra sem ninguém tocar no vidro**. Além disso `MainWindow.xaml.cs:1794` chama `Glass.SetFace` (não `SetFaceFrosted`) para a face densa do chat, que por isso vale **nos dois modos**.

| Token | Declaração | Default | Consumidores reais (verificado) | Papel | Slider mexe? |
|---|---|---|---|---|---|
| `IslandGlass` | `App.xaml:38-41` | `#660E1B2E` → `#4D0E1B2E` | `MainWindow.xaml:183` (face telemetria, ON+OFF), `:578` (face chat compacto, ON+OFF), `:391/411/431` (chips, **só ON** via `FaceFrosted`), `SettingsPanel.xaml:267` (**só ON**), `PlayerWindow.xaml:23` (**só ON**). `FluentControls.xaml:690` (`FloatingIslandStyle`) é **inerte**: os 3 hosts que usam o estilo zeram `Background` (`MainWindow.xaml:171/566`, `SettingsPanel.xaml:246`) | face, **misto** | **não direto** — força atua na cópia só-vidro |
| `PanelGlassStrong` | `App.xaml:45` | `#F20E1B2E` → `#E60E1B2E` | **nenhum** | sem uso hoje → vira face do popup em F4.2 (fora do ledger) | não |
| `ThemeMediaGlassBrush` | `App.xaml:88` | `#990E1B2E` | `MainWindow.xaml:467` (MediaPanel), `:713` (strip do FS), `:767` (controles do FS) — face, **ON+OFF** | face | **não direto** — cópia só-vidro |
| `ThemeFieldGlassBrush` | `App.xaml:89` | `#170B1E38` | `MainWindow.xaml:543` (`UndockBtn`, `FaceFrosted`, **só ON**), `:617` (composer, `Background` direto), `SettingsPanel.xaml:186` (`FieldShellStyle`, `Background` direto) | **duplo** | **não** (multiplicar mudaria composer/campos no OFF) — cópia só-vidro serve o `UndockBtn` |
| `ThemeChipGlassBrush` | `App.xaml:92` | `#22000C14` | `MainWindow.xaml:391/411/431` (face, **só OFF**), `:631` (chip de envio, `Background` direto) | face OFF + fundo direto | **não** |
| `ThemeChatExpandedDenseBrush` | `App.xaml:99` | `#E60E1B2E` | `MainWindow.xaml.cs:1794` (`Glass.SetFace`, **ON+OFF**) | face de **leitura** (α 90%, ligada ao gate 7:1) | **não** |
| `ThemeChatExpandedBrush` | `App.xaml:93` | `#380E1B2E` | **nenhum** | sem uso → remover em F4.6 | não |
| `ThemePanelSolidBrush` | `App.xaml:103` | `#FF070C14` | `SettingsPanel.xaml:266` (face do card, **só OFF**), `MainWindow.xaml:743` (fundo do LRC) | face de leitura | **não** |
| `GlassEdgeBrush` | `App.xaml:117` | `#4DFFFFFF` (30,2%) | só `Glass.cs:191` | aro | **sim**, mutação in-place |
| `GlassFacetBrush` | `App.xaml:118` | `#57FFFFFF` (34,1%) | só `Glass.cs:193` | faceta | **sim**, mutação in-place |
| `GlassEdgeHighlightBrush` | `App.xaml:119` | `#55FFFFFF` (33,3%) | só `Glass.cs:195` | especular | **sim**, mutação in-place |
| `ChromeHairlineBrush` | `App.xaml:123` | `#19FFFFFF` | `MainWindow.xaml:659` | hairline funcional, não é material | **não** |

**Desenho adotado: camada derivada, três tokens novos, zero mutação dos base.**

`GlassMaterial` (classe nova, sem dependência) calcula, a partir do tema vivo **e** da configuração do usuário:

| Token novo | Base | Alpha default | Consumido por (`FaceFrosted`) |
|---|---|---|---|
| `GlassFaceIsland` | gradiente 2 stops com o RGB de `IslandGlass` | `0x66`/`0x4D` | `05`, `09`, `10`, `11`, `15`, `26`, `33` |
| `GlassFaceMedia` | RGB de `ThemeMediaGlassBrush` (`#0E1B2E`) | `0x99` | `12`, `22`, `24` |
| `GlassFaceField` | RGB de `ThemeFieldGlassBrush` (`#0B1E38`) | `0x17` | `14` (`UndockBtn`) |

Cinco propriedades garantidas **por construção**, não por teste:

1. **Default replica cada face.** O RGB vem do próprio token base e o alpha default é o alpha atual (byte a byte). Com força 100% o resultado é idêntico — gate G-F3.3-5 cobre a prova visual.
2. **OFF não lê nada disso.** `ApplyFace` com `_mode == Off` usa `GetFace` e ignora `FaceFrosted` (`Glass.cs:391-395`). Os três hosts que hoje pintam com `Face` em ambos os modos recebem `FaceFrosted` **novo** (aditivo), mantendo `Face` intacto para o OFF.
3. **Nenhum token de duplo uso é mutado** (`ThemeFieldGlassBrush`, `ThemeChipGlassBrush`, `ThemeChatExpandedDenseBrush`, `ThemePanelSolidBrush` ficam fora da matriz de sliders).
4. **`GlassTintColor` só toca a camada derivada.** A cor de tinta substitui o **RGB** de `GlassFaceIsland`/`GlassFaceMedia`; o default `#0E1B2E` é exatamente o RGB desses dois. Chip (`#000C14`) e field (`#0B1E38`) **não** seguem a tinta — é por isso que um único `GlassTintColor` não "pinta tudo": ele pinta o que é vidro. A paleta dinâmica por capa continua dona do RGB base; a tinta é um desvio opcional aplicado **depois** do tema (ordem obrigatória: tema → usuário → tinta).
5. **Modo opaco (F4.3) reescreve só a camada derivada + os fills de fallback**, e como tudo é recomputado a partir do tema, desligar o modo restaura o valor exato.

**Onde a composição acontece:** um único ponto — `GlassMaterial.Apply(ThemeTokens t, LiquidGlassSettings s, bool opaque)` — chamado (a) no fim de `LivePalette.ApplyTheme` (`LivePalette.cs:367`) e (b) quando o modo/usuário muda. Isso mata o risco R-A (slider apagado na troca de capa) por arquitetura: não existe segundo lugar escrevendo alpha.

---

## 5. Inventário das 37 superfícies

| # | Superfície (x:Name / local) | Técnica atual hoje | Classe | O que falta para 100% | Técnica recomendada | Esf. | Imp. |
|---|---|---|---|---|---|---|---|
| 01 | `AppShell` (`MainWindow.xaml:70`) | `Border` radial `ThemeShellBrush` + hairline acento 1,2 + `Chamfer 22` + `TechFrame`; o `ChamferAdorner` já desenha o octógono de acento (`Chamfer.cs:95-117`) | FORA | **DECIDIDO pelo dono (23/10):** hairline → aro de vidro; **não** vitrificar o casco | `Glass.Ring="True"` no `Border` (**host de aro**, R7-a — sem face, sem `Path`) + `Glass.HideBorderStroke` para zerar o traço em ON. OFF = pixel de hoje | M | 2 |
| 02 | Título (`:89`) | Sem face; acento + `TechFrame("KATIE//HOLODECK")` | FORA | **DECIDIDO pelo dono (23/10):** flutua | **Manter flutuando, sem face** — é o que faz o fundo "vazar". Exceção documentada | Q | 2 |
| 03 | Botões de título (`:120/132/143`) | Fill acento translúcido + hover | FORA | Alinhar hover/focus ao material | Manter acento (assinatura); hover = +6pp no aro, focus = anel 2px | Q | 2 |
| 04 | Ponto de status (settings) | `Ellipse` + glow | EXCEÇÃO | — | Assinatura de estado. Não é vidro | — | 1 |
| 05 | Telemetry island (`:171/183/190`) | `Path` + `IslandGlass` + `Ring` + `Chamfer 18` + `TechFrame` + `DimWhenGlass` | ON | Aro com espessura configurável; AA sobre wallpaper claro | Manter; tokenizar espessura; gate de contraste em 4 wallpapers | Q | 5 |
| 06 | Telemetry internos (barras, hairlines) | `SurfaceTintBrush` + progress | EXCEÇÃO | — | 3º nível proibido | — | 2 |
| 07 | `ReactorHost` (`:247`) | `Viewport3D` | EXCEÇÃO | — | Holograma 3D é a peça central, não superfície | — | 1 |
| 08 | Roda de música | Desenho custom | EXCEÇÃO | — | Idem | — | 1 |
| 09 | Chip agenda (`:384`) | `Path` + `ThemeChipGlassBrush`, `FaceFrosted=IslandGlass`, `Ring`, `Chamfer 8` | ON | Hover "lift" sem acento; specular visível em chamfer 8 | Elevar apenas luminância do aro no hover | M | 4 |
| 10 | Chip e-mails (`:404`) | idem | ON | idem | idem | M | 4 |
| 11 | Chip música (`:424`) | idem | ON | idem | idem | M | 4 |
| 12 | `MediaPanel` (`:450`) + face (`:467`) | `Path` + `ThemeMediaGlass` + `Ring` + `TechFrame`; host inicia `Height=0` | ON **com bug** | **Pintura na 1ª abertura** (F0.1) | Corrigir subscription no host (F0.1) | Q | 5 |
| 13 | Transport do MediaPanel (`:496-508`) | 3 `Border` flat | FORA | Nada | 3º nível: proibido. Exceção | Q | 2 |
| 14 | Ações do MediaPanel (`:514/538/551`) | FsBtn flat; `UndockBtn` vidro; Close flat vermelho | PARCIAL | Coerência visual | `UndockBtn` mantém (já 100%); Fs/Close ficam (assinatura funcional) | Q | 2 |
| 15 | Chat island (`:566/578`) | `Path` + `ChatFace` + `Ring` + `Chamfer 16` + `TechFrame` | ON | Contraste em conteúdo denso | Manter; gate 9,6:1 já atingido | Q | 5 |
| 16 | Chat expandido (`:599`) | Variante densa de `ChatFace` | ON | idem | idem | Q | 5 |
| 17 | Composer do chat (`:617/629`) | `ThemeFieldGlass` no input + chip de envio | PARCIAL | Foco showcase no vidro | Anel de foco 2px já existe → alinhar ao token novo | Q | 3 |
| 18 | Bolhas do chat | Sem superfície | EXCEÇÃO | — | Texto puro sobre a face; bolha = 3º nível | — | 1 |
| 19 | Scrollbar do chat (`MainWindow.xaml:606` `ChatScroll`) | `ThinScrollBarStyle` (`FluentControls.xaml:652`) → thumb com cor hardcoded `#A640546E` (`FluentControls.xaml:626`) | FORA | Não segue tema nem vidro | Novo token vivo `ScrollThumbBrush` (sem vidro) + estados hover/drag | Q | 3 |
| 20 | Barra inferior (`:659`) | `ChromeHairline` | EXCEÇÃO | — | F2.6 já testou "piso de vidro" e removeu: divisão de chrome precisa de leitura | — | 2 |
| 21 | Véu fullscreen (`:696`) | `ThemeFullscreenBrush` `CC` | EXCEÇÃO | — | É backdrop de leitura; vidro aqui destrói o lyrics | — | 3 |
| 22 | Strip now-playing FS (`:708/713`) | `Path` + vidro + `Ring` | ON | — | Já em 100% | Q | 4 |
| 23 | Face do lyrics (`:742`) | Opaca + máscara | EXCEÇÃO | — | ClearType: face translúcida degrada texto subpixel | — | 2 |
| 24 | Controles FS (`:762/767` strip, `:772-788` botões) | Strip ON; botões flat | PARCIAL | Botões coerentes | Botões: hover de luminância, sem face | Q | 3 |
| 25 | Scrim do Settings (`:795/796`) | `ScrimBrush` `F5` | EXCEÇÃO | — | Scrim não é material | — | 2 |
| 26 | Card do Settings (`:245`) | `Path` + `IslandGlass` + `Ring` | ON | — | Já em 100% | Q | 5 |
| 27 | Nav rail + itens (`:274/279-286`) | `PanelCard`/`ThemeFieldGlass`; item checked com fill | FORA | Sub-nível de vidro + estado | Face `Path` (2º nível, **sem** `TechFrame`), tint mais escuro; item ativo com especular interno | M | 4 |
| 28 | Controles do Settings | Acento em checked/hover | FORA | Estados de material | Hover/focus como em F4.0 | M | 3 |
| 29 | Slider / ComboBox (`:441/452`) | Flat | FORA | Nada (controles finos) | Manter flat; só estados | Q | 2 |
| 30 | Popup do ComboBox (`FluentControls.xaml:762-766`) | Sólido `#FA070C14` + `ScrollViewer` em `:765` **sem** `ScrollBar` style | FORA | Popup destoa do material; scrollbar interna é a **padrão do Windows** (clara) | `Popup AllowsTransparency=True` (já é, `:761`) + face vetorial 2º nível usando `PanelGlassStrong` (`App.xaml:45`, hoje sem consumidor) + estilizar a scrollbar interna com `ThinScrollBarStyle`. **Não entra no ledger do `Glass`** (desenho próprio) | M | 2 |
| 31 | `ToolTip` | Default do WPF (claro!) | FORA | Estilo não existe | Novo `Style` de `ToolTip` alinhado ao material (sólido escuro é aceitável) | Q | 3 |
| 32 | `MessageBox` | Nativo | EXCEÇÃO | — | Fora do nosso controle sem reimplementar diálogo | — | 1 |
| 33 | `PlayerWindow` host (`PlayerWindow.xaml:15/22`) | `Window AllowsTransparency` + `Path` + vidro + `Ring`; sem Mica | ON | Sombra/borda coerentes | Manter; acrescentar aro 1px ao redor do host | Q | 4 |
| 34 | Título do Player (`:33/38`) | Flat + close | FORA | Coerência | Só estados | Q | 2 |
| 35 | Controles do Player (`:64-84`) | Flat; slider **ciano** (deveria ser verde) | FORA | Assinatura de cor errada | Corrigir slider para verde + estados | Q | 2 |
| 36 | **Scrollbar das Configurações** (`SettingsPanel.xaml:299` `ScrollViewer`, style em `:301`) | mesmo `ThinScrollBarStyle` → thumb `#A640546E` (`FluentControls.xaml:626`) | FORA | Mesma dívida da 19: cor dura, ignora tema | Consumo do `ScrollThumbBrush` (mesmo token da 19) + estados | Q | 2 |
| 37 | **Scrollbar do popup do ComboBox** (`FluentControls.xaml:765` `ScrollViewer MaxHeight="180"`) | **sem** `ScrollBar` style → chrome clássico do Windows dentro de popup escuro | FORA | Contraste/quebra de linguagem visual evidente quando a lista rola | `Style TargetType="ScrollBar" BasedOn="{StaticResource ThinScrollBarStyle}"` no `ScrollViewer.Resources` do popup | Q | 2 |

### 5.1 Leitura do inventário

**Granularidade (correção do P1-A):** cada linha é uma superfície ou uma família do mesmo host. A relação com os hosts de vidro é: `05`=1 host/1 face · `09/10/11`=3 hosts/3 faces · `12`=1/1 · `15`=1/1 · `16`=mesma `ChatFace` de `15` (estado expandido, **não** é host extra) · `22`=1/1 · `26`=1/1 · `33`=1/1 · `14`=`UndockBtn` (1 face dentro do host de `12`). Somando: 1+3+1+1+1+1+1 = **9 em `MainWindow`** + 1 no `PlayerWindow` + 1 no `SettingsPanel` = **11 hosts com `Face`**, que é exatamente a contagem do código (seção 4.1). As linhas `36/37` **não** são hosts de vidro: são superfícies de token.

| Classe | Qtd | Superfícies |
|---|---|---|
| ON (já em 100%) | **10** | 05, 09, 10, 11, 12*, 15, 16, 22, 26, 33 |
| PARCIAL | **3** | 14, 17, 24 |
| FORA (precisa decisão/fazer) | **14** | 01, 02, 03, 13, 19, 27, 28, 29, 30, 31, 34, 35, 36, 37 |
| EXCEÇÃO deliberada | **10** | 04, 06, 07, 08, 18, 20, 21, 23, 25, 32 |
| **Total** | **37** | |

\* nº 12 está ON **tecnicamente**, mas com bug de pintura — só conta como 100% depois de F0.1.

**Trabalho real de vidro:** 14 `FORA` + 3 `PARCIAL` = **17 superfícies**. Dessas, 8 são "só estados/token" (Q, sem face nova): 03, 14, 28, 29, 34, 35, 36, 37. As que realmente mudam o material: 01, 19, 27, 30, 31 e os hovers de 09-11.

---

## 6. Fases

Cada fase é entregável sozinha, com gate próprio e **rollback específico** (não genérico — ver a linha `Rollback:` de cada fase). Nenhuma fase depende de `git revert` da anterior para funcionar.

**R15 — checkpoint obrigatório:** ao fim de **toda** fase o trabalho **PARA**. O dev entrega evidência dos gates + lista do que mudou + lista do que ficou fora, e **espera aprovação explícita do dono** antes de abrir a fase seguinte. Não existe "fase emendada". O bloco `Checkpoint:` no fim de cada fase define exatamente o que é entregue para aprovação.

---

### F0.1 — Corrigir a pintura do `MediaPanel` (bloqueante)

**Objetivo:** eliminar a possibilidade de um host de vidro abrir sem pintura. É a única fase que corrige um defect real.

**Causa raiz verificada (corrigida nesta rodada — P2 do Codex):** `Glass.ApplyFace` (`Glass.cs:374`) só conhece `p.Loaded` e `p.SizeChanged` (`:363-366`). O `MediaPanel` nasce com `Height=0` (`MainWindow.xaml:450`; `MainWindow.xaml.cs:290` define `MediaPanelHeight=124`, aplicado só na abertura, `:1726`/`:1734`). No primeiro `Loaded`:
1. `Path` mede 0 e o host também mede 0 → o resgate de `:385` **não** se aplica (exige host ≥ 4);
2. `:389` retorna **antes** de escrever a geometria → **`Path.Data` permanece `null`** (a rodada 1 dizia "Data 0×0": é impreciso — não há geometria nenhuma);
3. `Shape` com `Data=null` + `Stretch=Fill` arranja `Size.Empty` → `ActualWidth` nunca sai de 0;
4. o host cresce por animação, o `Path` continua 0×0, **`SizeChanged` do `Path` nunca dispara** → a face nunca pinta.

Consequência: **o que falta é uma fonte de evento do host**, não uma guarda nova (a guarda de idempotência já existe em `:401-406` e a rodada 1 a listava por engano como mudança).

**Arquivos:** `Glass.cs` (apenas `ApplyFace` e adjacências). `MainWindow.xaml.cs` **não** precisa mudar.

**Mudanças:**
1. Em `OnFaceChanged` (`:360-369`), além de `p.Loaded`/`p.SizeChanged`, assinar **`SizeChanged` do host** (`FindHost(p)`, `:443-448`). Guardar referência fraca (mesmo padrão de `_targets`, `:82`) e **desinscrever em `Unloaded`**.
2. Guarda de idempotência: **já existe** (`:401-406`). Não reimplementar; só confirmar que o handler novo passa por ela.
3. Se o host ainda estiver 0×0 no `Loaded`, agendar **uma** reaplicação em `DispatcherPriority.Loaded` (`BeginInvoke`, sem timer) **e** manter a subscription de `SizeChanged` como rede.
4. Não alterar `Resolve`, não tocar em `LivePalette`, não mudar constantes geométricas (`:158-165`).
5. **`TechFrame`/`Chamfer` estão OK — verificado, nada a fazer.** `Chamfer.cs:26-29` e `TechFrame.cs:33-37` assinam `SizeChanged`/`Loaded` **do próprio elemento** (o `Border`), que é justamente quem cresce. O defeito é exclusivo do `Path`, porque é a `Shape` que não muda de tamanho. (A rodada 1 prometia "corrigir no mesmo commit se tivessem a mesma guarda": não têm o problema.)

**Gates:**
- **G-F0.1-1 (visual):** 20 ciclos abrir/fechar do `MediaPanel` no primeiro minuto de vida do app → **20/20** com pintura visível. Procedimento: captura determinística por `RenderTargetBitmap` (10.1), amostra de 5×5 px no centro do painel, exigindo `A > 0` e Δ ≤ 1 contra a cor esperada.
- **G-F0.1-1b (estrutural — novo, P2):** após o **primeiro** `SizeChanged` do host, inspecionar `MediaPanel`'s `Path`: `Data != null`, `Data.Bounds.Width ≥ host.ActualWidth − 1`, `Data.Bounds.Height ≥ host.ActualHeight − 1`, `Figures.Count ≥ 1`. **Amostragem de alpha sozinha não prova isto** — um `Path` pode herdar pintura do host e continuar sem geometria.
- **G-F0.1-2:** os outros 10 hosts com `Face` continuam pintando: **11/11** (mesma verificação 1 + 1b).
- **G-F0.1-3:** `EnableGlass=false` → `MediaPanel` visual idêntico ao 9.8.2 (Δ ≤ 1 por canal em 12 pontos fixos, 10.1).
- **G-F0.1-4:** nenhum `DispatcherTimer`, nenhum polling, nenhum `Effect` novo; **zero** subscription viva após 20 ciclos (contador do `WeakReference` + `Unloaded` provado por log do probe).
- **G-F0.1-5:** build 0/0.

**Riscos:** reaplicação em cascata durante a animação de 280 ms de abertura (custo). Mitigação: guarda de idempotência (`:401-406`) + `BeginInvoke` único.
**Rollback (específico):** `git revert` do commit. É auto-contido: só `Glass.cs`; não cria token, não persiste nada em `settings.json`, não mexe em XAML. Nenhuma migração a desfazer. O bug volta a existir (estado atual), nada piora.
**Checkpoint (R15):** entregar as 20 capturas + o dump de `Data.Bounds` da G-F0.1-1b + build log. **Parar.**

---

### F0.2 — Baseline medido (antes de tocar em aparência)

**Objetivo:** ter números de partida para provar ganho/perda depois. Sem isso, "ficou mais bonito" é opinião.

**Entregáveis (nenhum código de produção):**
1. Tabela de baseline preenchida, 3 execuções por cenário, com o **procedimento da seção 10.1**: p95 de frame time, uso de GPU, custo de abertura do `MediaPanel`, custo de troca de aba do Settings.
2. Cenários: (a) vidro ON + capa escura; (b) vidro ON + capa clara; (c) vidro OFF; (d) fullscreen com lyrics; (e) chat expandido com 200 mensagens.
3. Procedimento escrito de captura (mesmo PC, mesma resolução 1280×800, mesmo wallpaper, `EffectsProfile` registrado, janela não maximizada, DPI 100%).
4. **Instrumentação obrigatória, não mais opcional** (P2 do Codex: um gate de performance não pode depender de medição "à mão"): `GlassProbe` compilado **apenas em `DEBUG`**, ativado por `KATIE_GLASS_PROBE=1`, escrevendo `glass_probe.jsonl` em `%TEMP%\katie\` no mesmo formato do `dwm_debug.jsonl` (`DwmCompat.cs:65` usa o `BaseDirectory`; o probe usa `%TEMP%` para não sujar a pasta do app). Registra: timestamp de `CompositionTarget.Rendering`, `GC.GetTotalMemory(false)`, `Process.PrivateMemorySize64`, `Glass.Mode`, `EffectsProfile`. **Sem alocação em regime** (buffer pré-alocado, escrita em lote a cada 1 s). Nunca em Release.

**Gates:**
- **G-F0.2-1:** 5 cenários × 3 execuções preenchidos, com data, hash do commit, wallpaper usado, e o `glass_probe.jsonl` arquivado junto.
- **G-F0.2-2:** `Release` build **não** contém o probe: verificar por `strings`/IL (procura de `glass_probe`) ou por ausência do `#if DEBUG` no assembly compilado.
- **G-F0.2-3:** o baseline declara explicitamente a **definição de frame** usada (intervalo entre ticks de `CompositionTarget.Rendering`, descartando os 2 s iniciais e o primeiro frame após cada resize) — sem isso o p95 não é comparável entre execuções.

**Riscos:** instrumentação vazar para Release (mitigação: `#if DEBUG` + G-F0.2-2); p95 medido em máquina diferente do dev (mitigação: a tabela registra CPU/GPU/refresh — baseline de outra máquina é inválido).
**Rollback (específico):** apagar `docs/baseline-glass.md` + o bloco `#if DEBUG` do probe. Não há estado persistido, nem token, nem mudança de produção. Nada a reverter no app.
**Checkpoint (R15):** entregar a tabela de baseline + os 15 `glass_probe.jsonl` + a definição de frame. **Parar.**

---

### F3.3 — "Liquid Glass" no painel de Configurações

**Objetivo:** dar ao dono controle real do material, com preview ao vivo, sem sair do painel. **Prioridade zero declarada.**

#### Decisão de navegação (tomada)
**9ª aba**, `x:Name="NavLiquid"` / `SecLiquid`, rótulo **"Liquid Glass"**, posicionada **imediatamente depois de "Aparência"**.
- `GlassChk` **sai** de Aparência (`SettingsPanel.xaml:338`) e vira o master switch da nova seção.
- Em Aparência fica **uma linha** de texto: "Vidro: controlado em *Liquid Glass*" (com o estado atual, ex. "ligado"), para quem procura ali.
- Alternativa descartada (registrada): bloco dentro de `SecAparencia`. Descartada porque Aparência já tem radios de efeito + slider + combo e o dono pediu explicitamente que "as configurações do liquid" estejam nas configurações — uma seção própria é o que ele espera encontrar.
- Consequência a validar: nav com 9 itens num rail de 582 linhas de XAML — verificar se cabe sem scroll em 580 px de altura. Se não couber, reduzir padding dos itens, **não** criar submenu.

#### Conteúdo da seção
Quatro sub-cards, nesta ordem:

**1) Preview (topo, sempre visível)**
- `PreviewTile` — controle leve novo (não registra em `Glass`, não entra no ledger): desenha **2 tiles de 220×110**, um sobre fundo escuro (`#0A0F18`) e um sobre claro (`#E8EDF4`), cada um com face + faceta + aro + especular usando **exatamente** os brushes vivos.
- Texto de amostra em cada tile ("Aa 100%") para o dono ver contraste.
- Atualiza a cada `ValueChanged`, sem fechar o painel. **Este é o único jeito de "preview ao vivo" existir**, porque o painel cobre o app.
- Gate: o `PreviewTile` e a ilha do telemetry devem reagir ao mesmo slider com o mesmo Δ (medido).

**2) Intensidade**
| Controle | Token (fonte) | Range | Step | Default (= hoje) |
|---|---|---|---|---|
| Aro | `GlassEdgeBrush` alpha (`App.xaml:117`) | 0–80% | 1% | **30,2%** (`0x4D`) |
| Faceta | `GlassFacetBrush` alpha (`App.xaml:118`) | 0–80% | 1% | **34,1%** (`0x57`) |
| Especular | `GlassEdgeHighlightBrush` alpha (`App.xaml:119`) | 0–80% | 1% | **33,3%** (`0x55`) |
| Força do vidro | multiplicador sobre a **camada derivada** (`GlassFaceIsland`/`GlassFaceMedia`/`GlassFaceField`, seção 4.6) | 50–150% | 5% | **100%** |

- Os três primeiros são seguros por construção: cada um tem **um único consumidor** (`RingAdorner`, `Glass.cs:191-195`), e com o vidro OFF o próprio `OnRender` sai em `:175` — mudar o alpha deles não pode tocar em nenhum pixel do caminho OFF.
- "Força do vidro" **não muta** `IslandGlass` nem `ThemeMediaGlassBrush`: multiplica a camada derivada (seção 4.6). Motivo: esses dois são lidos em ON **e** OFF (telemetria `MainWindow.xaml:183`, chat `:578`, MediaPanel `:467`, strips `:713/:767`) — mutação in-place quebraria R8/G2 sem tocar no vidro.
- O que **não** entra na força, com motivo escrito: `ThemeFieldGlassBrush` (duplo uso: `Background` do composer `:617` e do `FieldShellStyle` `SettingsPanel.xaml:186`), `ThemeChipGlassBrush` (face do OFF + `Background` do chip de envio `:631`), `ThemeChatExpandedDenseBrush` (face de leitura, gate 7:1), `ThemePanelSolidBrush` (face de leitura do LFR e do card no OFF).
- Em 100% os bytes precisam sair **idênticos**: `Math.Round(base * força/100)` + clamp `0…255`, nunca `+0,5` fixo. Arredondamento definido uma vez, no `GlassMaterial`, e usado por todos os tokens.
- Guardar **em byte 0–255**, não em float. Um float 0,30196 não reproduz `0x4D` com segurança em todos os paths; byte reproduz por construção. UI mostra %; storage guarda byte.

**3) Geometria** (dentro de um `Expander` "Avançado", fechado por padrão)
| Controle | O que é | Range | Step | Default |
|---|---|---|---|---|
| Espessura do aro | `Glass.Thickness` (const hoje) | 0,5–3,0 px | 0,1 | **1,0** |
| Largura da faceta | `Glass.FacetWidth` (const hoje) | 0–4,0 px | 0,1 | **1,5** |

- Requer **promover duas constantes a campos**. Não é cosmético: `EdgeInset` deriva de `Thickness`, então mudar espessura recalcula a geometria → precisa `Refresh()` e idempotência da F0.1 (senão rebuild a cada pixel de slider).

**4) Cor e extras** (dentro do mesmo "Avançado")
| Controle | O que é | Default |
|---|---|---|
| Cor da tinta | `GlassTintColor` — **novo**. Substitui o **RGB** de `GlassFaceIsland`/`GlassFaceMedia` depois do tema (seção 4.6, item 4). Não existe token único hoje: os bases são `#0E1B2E` (Island/Media/Panel/Chat), `#000C14` (Chip) e `#0B1E38` (Field) | `#0E1B2E` |
| Aro segue o acento | `RingFollowsAccent` (bool) — **viola R6 por escolha explícita do dono** | `false` |
| Ruído | novo, `DrawingBrush` 64×64 tile estático | `0%` (0–5%, step 1) |

- `GlassTintColor` pinta **só a camada de vidro**. Chip e Field mantêm o RGB próprio — é o que permite um único campo de cor e, ao mesmo tempo, default que reproduz todas as faces. Fórmula: `RGB_final = TintColor`, `A_final = Round(A_base × força)`.
- Campo de cor reutiliza o padrão de validação do `HexBox` (`SettingsPanel.xaml.cs:164-181`): inválido → mensagem, **mantém o último válido, não escreve lixo no JSON**.
- `RingFollowsAccent=true` mantém a **faceta neutra** e colore só o aro, com os mesmos alphas; gate de contraste continua valendo (aro colorido sobre face navy ≥ 2×). Ao desligar, o aro volta ao branco neutro da camada base no próximo `GlassMaterial.Apply`.

**5) Rodapé da seção**
- Botão **"Restaurar padrões do vidro"** — restaura **somente** as chaves de `LiquidGlass` (as 10 de configuração; `SchemaVersion` não é restaurável), nunca `EnableGlass`, nunca os efeitos, nunca a paleta.
- Linha de aviso quando `EnableGlass=false`: "Vidro desligado — os ajustes abaixo não têm efeito visual" e os sliders ficam `IsEnabled=false` (evita a frustração de mexer e nada acontecer).

#### Plumbing técnico (o que realmente dá trabalho)

1. **`GlassMaterial` — um único ponto de composição (novo).** `Apply(ThemeTokens t, LiquidGlassSettings s, bool opaque)` calcula, nesta ordem: **tema → usuário → tinta → opaco**, e escreve `GlassFaceIsland`, `GlassFaceMedia`, `GlassFaceField`, `GlassEdgeBrush`, `GlassFacetBrush`, `GlassEdgeHighlightBrush`. Chamado no fim de `LivePalette.ApplyTheme` (`LivePalette.cs:367`, que hoje termina em `:421-422` escrevendo `0x4D`/`0x57` na mão) e quando modo/usuário mudam. **Não existe um segundo lugar escrevendo alpha** — é isso que mata o risco R-A.
2. **Camada derivada (seção 4.6).** Três brushes novos criados em `LivePalette` (campo + `Dictionary[...]`, padrão de `:248-252`), apontados por `Glass.FaceFrosted` em 7 hosts: `MainWindow.xaml:183` (telemetria, hoje `Face` sem frosted), `:391/411/431` (chips, já têm `FaceFrosted` — só troca a chave), `:467` (MediaPanel, hoje `Face` sem frosted), `:578` (chat, hoje `Face` sem frosted), `:713` e `:767` (strips do FS), `SettingsPanel.xaml:267`, `PlayerWindow.xaml:23`, e `MainWindow.xaml:543` (`UndockBtn` → `GlassFaceField`). **Aditivo puro:** o `Face` de cada um continua exatamente o de hoje.
3. **Como o OFF ignora tudo (P1-C, resposta obrigatória).** O curto-circuito tem **cinco** pontos, todos verificados no código:
   - `Glass.Apply` → `Resolve(false, …)` devolve `Off` (`Glass.cs:56`), `_mode = Off` (`:74`) e `Refresh()` (`:75`).
   - `RingAdorner.OnRender` `:175` — `if (_mode == GlassMode.Off) return;` → **nenhum** aro/faceta/especular é desenhado; os alphas dos três brushes deixam de ter efeito.
   - `ApplyFace` `:391-395` — `glass == false` → `fillKey = GetFaceFrosted(p)` é descartado e o código usa **`GetFace(p)`**; `strokeKey = GetFallbackStroke(p)`. **`FaceFrosted` nunca é lido no OFF.**
   - `ApplyDim` `:475` — o véu de acento volta a `Visible`.
   - `IsRingActive` `:122` → `false` → `ChamferAdorner` (`Chamfer.cs:100`) volta a desenhar o traço de acento e `TechFrame` volta ao inset 0 (`TechFrame.cs:160`).
   - **E a rede de segurança:** ao entrar em `Off`, `Glass.Apply` chama `GlassMaterial.ResetToBase()`, que reescreve a camada derivada **e** os três brushes de aro com os valores base do tema (força 100%, alphas `0x4D/0x57/0x55`). Assim, mesmo que o usuário tenha customizado tudo e depois desligue, nenhum valor de usuário sobra em nenhum brush. Sem isso, um `Path` novo/criado depois herdaria lixo.
4. **`_glassHighlight`:** `readonly` **não** é prova de frozen (P3 do Codex; seção 4.2). A auditoria obrigatória antes de codar: `IsFrozen` em cada um dos 6 tokens vivos + `DynamicResource` vs. `StaticResource` em cada consumidor. Os helpers `Solid`/`Grad` (`LivePalette.cs:268-283`) já tratam frozen substituindo a entrada do dicionário; o `Path` resolve por `SetResourceReference` (`Glass.cs:407`) e o anel por re-resolução de referência (`:191-196`), então substituição propaga nos dois caminhos. **Se algum consumidor for `StaticResource`, o caminho é mutação in-place e o brush não pode ser substituído** — é o único caso em que a arquitetura muda.
5. **Repintura:** espessura/largura → `Glass.Refresh()` (`Glass.cs:92`). Cor/alpha → mutação de brush (barato). **Nunca** `Glass.Apply` para isso (early-return em `:73`).
6. **Throttle:** slider arrastado dispara dezenas de eventos. Aplicar em `CompositionTarget.Rendering` (um por frame), não por `ValueChanged`.

**Arquivos:** `SettingsPanel.xaml`, `SettingsPanel.xaml.cs`, `SettingsService.cs`, `Glass.cs`, `LivePalette.cs`, `MainWindow.xaml.cs` (`OnSettingsChanged`/`Save`), `App.xaml` (só se precisar desfrozenar brush), + `GlassPreviewTile.cs` e `GlassMaterial.cs` novos.

**Gates:**
- **G-F3.3-1:** 9 itens de nav navegam por clique; nenhum corte/scroll no rail em 580 px (medir `ActualHeight` da `StackPanel` de nav vs. 880 − 2×16 de margem).
- **G-F3.3-2:** cada um dos 7 sliders e do color picker produz mudança visível no `PreviewTile` (ΔL ≥ 10% no ponto do aro, método 10.1) **sem fechar o painel**.
- **G-F3.3-3:** os mesmos sliders mudam as ilhas reais ao fechar o painel, e os valores estão iguais aos do preview (mesma chave → mesmo brush).
- **G-F3.3-4:** persistência 5/5 — mover 5 controles, fechar app, reabrir, valores restaurados.
- **G-F3.3-5:** **paridade de default** — "Restaurar padrões" com vidro ON → Δ ≤ 1 unidade de canal nos 12 pontos fixos (10.1) contra o build 9.8.2, em **duas** capas (uma escura, uma clara).
- **G-F3.3-6:** **paridade OFF, recorte fixado (P1-C).** `EnableGlass=false` + qualquer combinação de sliders (testar 3 combinações: tudo no mínimo, tudo no máximo, meio) → comparação contra 9.8.2 com:
  - **recorte:** janela inteira 1280×800 com o painel de Configurações **fechado** (o painel aberto difere por construção: a 9ª aba é nova);
  - **tolerância:** Δ ≤ 1 por canal nos 12 pontos fixos **e** ≤ 0,05% dos pixels com Δ > 1 **e** nenhum ponto com Δ > 2;
  - **método:** `RenderTargetBitmap` (10.1), não screenshot de tela (DWM/wallpaper mudam o resultado).
- **G-F3.3-7:** trocar de capa 6× com valores custom → valores intactos (prova do risco nº 1). Verificar **nos brushes**, não só no JSON: ler `((SolidColorBrush)Application.Current.Resources["GlassEdgeBrush"]).Color.A` depois de cada troca.
- **G-F3.3-8:** `settings.json` de 9.8.2 (sem `LiquidGlass`) carrega com defaults, sem log de erro e sem reescrita destrutiva; round-trip preserva `MusicPrefs`/`ChatPrefs`/`Telemetry`/`Games`.
- **G-F3.3-9:** arrastar slider por 5 s: p95 ≤ baseline + 1,2 ms (10.1, mesmo cenário do F0.2); sem crescimento de memória (`PrivateMemorySize64` Δ < 5 MB no intervalo).
- **G-F3.3-10:** build 0/0; zero `Effect` novo (contagem 35).

**Riscos:** (a) `LivePalette` sobrescrevendo (mitigado por ponto único de composição, item 1); (b) brush frozen/`StaticResource` não propagando (item 4); (c) slider de geometria causando rebuild de 11 geometrias por frame (mitigado por `Refresh` só no commit + idempotência de `Glass.cs:401`); (d) nav com 9 itens estourando altura; (e) **novo** — a camada derivada divergir do base e a paridade de default falhar (mitigado por G-F3.3-5 e por derivar o RGB do próprio token).
**Rollback (específico, em ordem):** (1) `GlassMaterial.Apply` volta a não ser chamado e `LivePalette` volta a escrever `0x4D/0x57` direto (`:421-422`); (2) remover os 3 brushes derivados do `Dictionary` e os `FaceFrosted` novos do XAML (os hosts voltam a pintar com `Face`); (3) remover `SecLiquid`/`NavLiquid` e devolver o bloco `GlassChk` a Aparência (o XAML do checkbox não muda, só de lugar); (4) `LiquidGlass` no JSON pode **ficar** — `System.Text.Json` ignora propriedades desconhecidas e o build 9.8.2 lê o arquivo sem erro (provar com um arquivo de teste no gate de rollback). Nada de migração destrutiva.
**Checkpoint (R15):** entregar capturas do preview + os 4 gates de paridade (5, 6, 7, 8) + diff de `settings.json` + build log. **Parar** — este é o ponto de parada natural aprovado pelo dono.

---

### F4.0 — Estados de interação universais

**Objetivo:** uma regra única de estado, aplicada em todas as superfícies de vidro, para o app parar de ter hover "de cada um".

**Regra:** estados modulam **somente luminância do material**. Exceção: controles de assinatura (botões de título, close) mantêm acento.

| Estado | Efeito no material |
|---|---|
| Normal | tokens como estão |
| Hover | aro **+6 pp** de alpha; faceta +2 pp |
| Pressed | aro **+10 pp**; face **+4 pp**; deslocamento visual de 1 px (sem mudar layout) |
| FocusVisible | anel externo 2 px a **2×** a luminância do aro; só em teclado (`KeyboardNavigation`), nunca em clique de mouse |
| Disabled | todos os tokens a **40%**; sem aro de foco |

**Gates:**
- **G-F4.0-1:** 12 controles × 5 estados = 60 capturas; hover detectável (ΔL ≥ 3% no aro); pressed > hover em todos.
- **G-F4.0-2:** focus visível em `Tab` (≥ 12 paradas), anel ≥ 2× o fundo imediato.
- **G-F4.0-3:** nenhum estado adiciona `Effect` (contagem continua 35).
- **G-F4.0-4:** nenhum estado muda `ActualWidth/Height` (sem layout shift ao passar o mouse).
- **G-F4.0-5:** `EnableGlass=false` → estados continuam funcionando como hoje (sem regressão de feedback).

**Impacto:** 4 · **Risco:** estados que mudam alpha de brush compartilhado podem vazar para outros hosts (brush é singleton — hover em um chip mudaria todos). **Mitigação obrigatória:** estados por elemento exigem brush **por instância** ou `Opacity` do `Path` (preferir `Opacity`, que é local e não toca no brush).
**Rollback (específico):** os estados são um `Style`/attached behavior novo; remover as referências devolve os hovers atuais. Como a mitigação proíbe mutar brush compartilhado, não há estado residual em token — nada a resetar.
**Checkpoint (R15):** entregar as 60 capturas (12 controles × 5 estados) + as 12 paradas de `Tab` + build log. **Parar.**

---

### F4.1 — Chrome: moldura, título, botões, barra inferior

**Objetivo:** fechar `01`, `02`, `03` e confirmar `20` — conforme decisão do dono de 23/10.

- `01 AppShell` — **decisão (b): aro, não casco.** O `Border` recebe `Glass.Ring="True"` (host **de aro**, R7-a: sem `Face`, sem `Path`, sem face nova) e o traço de acento de 1,2 px (`BorderBrush="{DynamicResource AccentBrushGhost}"`) é zerado em ON por uma attached property nova **`Glass.HideBorderStroke`** (mesmo padrão de `DimWhenGlass`, `Glass.cs:454-477`: guarda valor original, restaura em OFF). O acento continua existindo como traço **só quando o vidro está OFF** e quando a janela está inativa — é o que evita borda dupla com o `DwmCompat`.
- `02 Título` — **decision (c): selado como exceção.** Flutua sobre o fundo; dar face a ele mata o "vazamento" que é o ponto do liquid glass. Sem face, sem `Ring`.
- `03 Botões de título` — mantêm acento; só herdam os estados de F4.0.
- `20 Barra inferior` — mantém a decisão do F2.6 (piso de vidro testado e removido).

**Consequência registrada em R6:** com o aro no `Border` externo, **nenhum acento** pode aparecer no `BorderBrush` do `AppShell` em ON. Os três botões de título continuam coloridos (são controles, não material).

**Gates:** **G2** (OFF parity completo: 12 pontos, ≤ 0,05% dos pixels com Δ > 1, nenhum > 2 — 10.1) · aro da moldura ≥ 2× o fundo imediato em 4 wallpapers · nenhum clique perdido (10/10) · `HideBorderStroke` restaura o traço exato em OFF (Δ ≤ 1 no `BorderBrush`) · 0/0.

**Impacto:** 2 · **Esforço:** 1,5 d · **Risco:** o aro encostar na borda do `DwmCompat` (mesma cor duas vezes = borda dupla). Verificar em maximize **e** em janela normal; se houver borda dupla em maximize, o aro desce 1 px só nesse estado (sem mexer no OFF).
**Rollback (específico):** remover `Glass.Ring` do `AppShell` e a attached `HideBorderStroke` → volta ao `BorderBrush` de acento de hoje. Nenhum token base foi tocado; `BorderBrush` é restaurado pela guarda da própria property.
**Checkpoint (R15):** entregar capturas normal/maximizada/ OFF + a medição de borda dupla + build log. **Parar.**

---

### F4.2 — Settings, Player e popups

**Objetivo:** cobrir `27`, `28`, `30`, `31`, `34`, `35` — o que o dono vê quando abre configurações ou o player.

- `27 Nav rail`: face `Path` de 2º nível (tint mais escuro que o card), **sem `TechFrame`** (R11). Item ativo recebe especular interno, não fill de acento. É o **+1 em `Face`** da seção 9 (11 → 12) e o **+1 em `Ring`** (11 → 12; o 13º é a moldura de F4.1).
- `28/29`: herdam F4.0; controles finos ficam flat.
- `30 ComboBox popup`: `AllowsTransparency=True` (já é, `:761`) + face vetorial 2º nível consumindo **`PanelGlassStrong`** (`App.xaml:45` — token hoje órfão, seção 4.6) + `Style TargetType="ScrollBar" BasedOn="{StaticResource ThinScrollBarStyle}"` no `ScrollViewer.Resources` do popup (`FluentControls.xaml:765`), resolvendo a **linha 37** do inventário. **Não** entra no ledger de `Glass` (desenho próprio, sem adorner).
- `31 ToolTip`: novo `Style` alinhado ao material (sólido escuro é aceitável e mais barato que vidro).
- `34/35 Player`: estados + **corrigir o slider ciano para verde** (assinatura de cor errada hoje).
- `36` (scrollbar do Settings): consome o `ScrollThumbBrush` criado na linha 19 — é a prova de que o token é único, não duplicado por painel.

**Gates:** **G2** (OFF parity completo — 10.1) · contraste ≥ 4,5:1 em todos os textos do popup e tooltip · 10/10 aberturas sem flicker (G15) · nav ativo distinguível (ΔL ≥ 8% vs. inativo) · scrollbar do popup **e** do Settings seguem o tema (2 capas, sem chrome claro) · 0/0.

**Impacto:** 4 · **Esforço:** 2,5 d · **Risco:** popup com `AllowsTransparency` perde sombra de sistema e ganha borda dura; e `Popup` + adorner é fonte clássica de artefato. Mitigação: popup **não** entra no ledger de `Glass` (desenho próprio).
**Rollback (específico):** remover a face do popup e o `Style` de `ScrollBar` do `ScrollViewer.Resources` → popup volta ao sólido `#FA070C14` e à scrollbar do sistema; remover `Glass.Ring`/face da nav rail → 27 volta a `FORA`. `PanelGlassStrong` volta a ficar órfão (sem efeito colateral). Nada persistido.
**Checkpoint (R15):** entregar as capturas do popup (2 capas, com e sem scroll), nav rail nos 5 estados, player, e a prova de que `PanelGlassStrong` ganhou consumidor. **Parar.**

---

### F4.3 — Acessibilidade

**Objetivo:** o vidro não pode ser bonito e ilegível.

#### Modo opaco = **terceiro modo**, não um "OFF disfarçado" (correção do P1-D)

Hoje o enum tem `Auto`, `On`, `Off`. **Não criar um quarto valor avulso**: `GlassMode` ganha `Opaque` (`GlassMode.Off` continua "sem material, fallback sólido neutro"; `Opaque` é "material presente, 100% opaco, com aro/faceta mantidos"). Motivo: se o opaco fosse só um alpha alto, (a) o aro continuaria translúcido e o texto seguiria com contraste variável, (b) `GlassMaterial.ResetToBase()` e o curto-circuito do OFF já existentes seriam reaproveitados errado — OFF **deve** voltar ao pixel de 9.8.2, `Opaque` **não** (é uma decisão de acessibilidade, não de desligar).

Mapeamento dos três modos de acessibilidade para o alvo:

| Gatilho | Detecção real | Efeito |
|---|---|---|
| "Efeitos de transparência" do Windows desligado | `UISettings.AdvancedEffectsEnabled == false` (`Windows.UI.ViewManagement`, desde 10.0.15063; o TFM 19041 projeta a namespace) | `GlassMode.Opaque`: faces a `0xFF`, aro/faceta/especular mantidos com os alphas do usuário |
| Alto contraste | `SystemParameters.HighContrast` + `SystemParameters.StaticPropertyChanged` (WPF) | `GlassMode.Opaque` **+** tint de alto contraste vindo do sistema (`SystemColors.WindowColor`/`WindowTextColor`), texto 100% |
| Animação reduzida (só informativo) | `UISettings.AnimationsEnabled` | **Não** altera material. `SystemParameters.ClientAreaAnimation` é **animação**, não transparência — a rodada 1 usava essa API por engano |

- `UISettings` é WinRT: instanciar **uma vez**, guardar, e assinar `AdvancedEffectsEnabledChanged`. Se a construção lançar (host sem WinRT/serviço), **degradar para "não detectado"**, nunca derrubar o app — `try/catch` com log único.
- Fallback sem WinRT: ler `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\EnableTransparency` (DWORD; 0 = desligado). Só usado se `UISettings` falhar; não assinar registro (custo/WMI desnecessário) — reavaliar no `Activated` da janela.
- **Prioridade:** `HighContrast` (mais forte) > `AdvancedEffectsEnabled` > escolha do usuário (`GlassMode` no painel) > `Auto`. Ou seja: com HC ligado, o usuário **não** consegue forçar vidro translúcido (é o ponto da feature); a UI mostra o motivo e o controle fica desabilitado, não escondido (R12).
- Reavaliar no `SystemEvents.UserPreferenceChanged`/`StaticPropertyChanged`, não por timer.

#### Resto da fase

- **Contraste:** medir **todos** os textos que hoje caem sobre vidro (lista fechada, 14 pares: título, 3 chips, face do chat, header do chat expandido, métricas de telemetria, 3 labels de campo no Settings, texto do card, tooltip, popup, player, LRC) contra wallpaper **claro** e **escuro** (4 wallpapers). Alvo ≥ 4,5:1; chat/leitura ≥ 7:1 (hoje 9,6:1 — não pode cair). Método de amostragem em 10.1 (luminância relativa WCAG, não "olhômetro").
- **Teclado:** toda superfície interativa alcançável por `Tab`, com anel de F4.0. Com `Opaque`, o anel continua visível (não depende de translucidez).
- **Escala:** 100/125/150/175% DPI — aro/faceta em px **não** escalam (senão viram 4,5 px em 150%), mas o mínimo de 1 px físico é preservado por `UseLayoutRounding`. Verificar que `RingInset` (`TechFrame.cs:160`) não arredonda para 0 em 125%.

**Gates:**
- **G-F4.3-1:** 4 wallpapers × 14 pares de texto, planilha com todos ≥ alvo (≥ 4,5:1; densos ≥ 7:1), medidos por WCAG (10.1).
- **G-F4.3-2:** `AdvancedEffectsEnabled=false` (forçado via registro + relançar) → **zero** pixel com alpha < 0xFF nas 11 faces; aro/faceta visíveis; app utilizável.
- **G-F4.3-3:** `HighContrast` ligado → zero superfície translúcida, texto com cor de sistema, controle de modo desabilitado com motivo visível.
- **G-F4.3-4:** desligar o gatilho → material volta **exatamente** ao estado anterior (Δ ≤ 1 por canal, 12 pontos) — o modo opaco não é one-way.
- **G-F4.3-5:** 4 DPIs sem corte, sem hairline desaparecendo, e `RingInset` > 0 em todos.
- **G-F4.3-6:** build 0/0; nenhum timer; `UISettings` instanciado 1× por processo (log).

**Impacto:** 5 (invisível quando certo, bloqueante quando errado) · **Esforço:** 2 d · **Risco:** API WinRT indisponível em alguns hosts → mitigado pelo fallback de registro e pelo "não detectado".
**Rollback (específico):** remover a detecção e o valor `Opaque` do enum (volta a `Auto/On/Off`); o schema mantém a chave `OpaqueWhen` (ignorada por build antigo). Nenhum token muda.
**Checkpoint (R15):** entregar a planilha de contraste + as 3 capturas de modo (normal / transparência off / HC) + o log de detecção. **Parar.**

---

### F4.4 — Performance e fallback

**Objetivo:** o app não pode ficar pior nem quebrar em máquina ruim.

- **Detecção de sessão remota:** `SystemParameters.IsRemoteSession` **não é lido hoje** — criar. Em RDP: `GlassMode.Opaque` (não OFF: em RDP o usuário ainda merece a UI completa, e RDP compõe alpha no cliente de forma barata), aviso em Configurações listando o motivo.
- **Tier de renderização:** `RenderCapability.Tier >> 16 < 2` → `GlassMode.Opaque` automático (sem Tier 2 não há aceleração garantida para gradientes/strokes; aro/faceta continuam desenháveis, o custo caro é a composição translúcida).
- **`EffectsProfile=none`:** vidro continua vetorial (não depende de efeito), mas perde ruído e qualquer blur de backdrop já existente. Testar os 3 perfis.
- **Orçamento:** p95 de frame ≤ baseline + 1,2 ms nos 5 cenários de F0.2 (procedimento 10.1: 60 s, 3 execuções, descarte dos 2 s iniciais); nenhum timer novo; nenhuma alocação por frame em regime.
- **Memória:** 30 min com troca de capa a cada 5 s → `PrivateMemorySize64` Δ < 15 MB e slope < 1 MB/min (10.1). O `Glass.Refresh` reaplica 11 geometrias; vazamento aqui é provável se as subscriptions de F0.1 não desinscreverem — por isso G-F0.1-4 conta subscription viva.

**Gates:**
- **G-F4.4-1:** `IsRemoteSession` forçado → `Opaque`, app utilizável, aviso visível e correto.
- **G-F4.4-2:** Tier forçado < 2 → idem.
- **G-F4.4-3:** 3 perfis sem crash e sem brush nulo.
- **G-F4.4-4:** 5 cenários dentro do orçamento (p95 ≤ baseline + 1,2 ms), com os 15 `glass_probe.jsonl`.
- **G-F4.4-5:** 30 min sem leak (Δ < 15 MB, slope < 1 MB/min) + contador de subscription estável.
- **G-F4.4-6:** desligar o gatilho restaura o material anterior (Δ ≤ 1 por canal).

**Impacto:** 4 · **Esforço:** 1,5 d · **Risco:** detecção errada desliga o vidro para quem tem máquina boa (falso positivo). Mitigação: override manual em Configurações + log na telemetria.
**Rollback (específico):** desligar as duas detecções por flag de compilação/const e deixar o override manual; o app volta a confiar só no `GlassMode` do usuário. `IsRemoteSession`/`Tier` são leituras puras — zero estado.
**Checkpoint (R15):** entregar as capturas de RDP/Tier forçados + os 15 probes + o dump de memória de 30 min. **Parar.**

---

### F4.5 — P&D: o que entra, o que sai

**Objetivo:** responder ao pedido de avaliar refração, tinta adaptativa e ruído — com veredito, não com "seria legal".

#### (a) Refração real (HLSL / `ShaderEffect`) — **DESCARTAR**
Motivo técnico, não estético: no WPF um `ShaderEffect` recebe **apenas a sua própria entrada**, não o que está atrás dele. Refração real exige ler o framebuffer:
- **Caminho 1 — `RenderTargetBitmap` do fundo:** snapshot → shader. Custo de cópia CPU 3–8 ms em 1280×800, conteúdo **atrasado** (refrata o frame anterior), e o `MediaPanel` animando produz smear visível.
- **Caminho 2 — `D3DImage` + interop D3D11:** funciona de verdade, mas exige dependência/interop e sai do contrato R2/R3, além de quebrar o caminho de software rendering.
- **Caminho 3 — `BitmapEffect`/legado:** morto e pior.

**Substituto vetorial (o que realmente entrega a sensação):** "dobra" de 2 paradas dentro da faceta — um gradiente curto com inversão de direção nas bordas internas simula a refração do aro sem custo e sem dependência. Já é 80% da leitura visual a 0% do custo.

#### (b) Tinta adaptativa pelo wallpaper — **ADIAR (com desenho pronto)**
- Conflita com o master de capa (a paleta já pinta o app inteiro) → dois sistemas disputando a mesma decisão de cor.
- Já existe adaptação real de backdrop via Mica (`DwmCompat`), que é melhor que tint calculado.
- **Desenho, se um dia entrar:** ler wallpaper via `HKCU\Control Panel\Desktop\WallPaper`, downsample 64×64, média ponderada, blend de **8%** na tinta, debounce em troca de wallpaper, e **só** quando o tema de capa estiver OFF.
- Fora desta rodada.

#### (c) Ruído sutil — **INCLUIR (opcional, default 0%)**
- `DrawingBrush` 64×64 estático, `TileMode=Tile`, alphas 0–5%, `SnapsToDevicePixels` + viewport absoluto (senão "ferve" ao escalar).
- Zero `Effect`, zero animação, custo ~0.
- Gate: ΔL ≤ 2% no aro e nenhum banding perceptível em 4 wallpapers; em 150% DPI o tile não pode virar moiré (se virar, o slider fica limitado a 0–2%).

#### (d) Aro seguindo o acento — **INCLUIR como opt-in, default OFF**
- Viola R6 por definição; só existe como escolha explícita do dono, com aviso na UI.
- A faceta permanece neutra (meia-medida deliberada: colorir tudo descaracteriza o material).
- Gate: contraste do aro colorido ≥ 2× a face navy; nenhum acento em estado Normal dos controles de assinatura.

**Esforço:** 1 d (implementar apenas c e d; a e b são veredito escrito).

**Gates:** (c) ΔL ≤ 2% no aro e zero banding em 4 wallpapers; em 150% DPI sem moiré, senão o slider é limitado a 0–2% · (d) aro colorido ≥ 2× a face navy; nenhum acento em estado Normal dos controles de assinatura; `RingFollowsAccent=false` reproduz o aro neutro (Δ ≤ 1) · (a)/(b) veredito escrito neste documento, sem código · 0/0 e contagem de `Effect` = 35 (o ruído é `DrawingBrush`, não `Effect`).
**Rollback (específico):** (c) remover o `DrawingBrush` do `Path` e o slider `Noise` (default 0 já não pinta nada); (d) remover o bool e a ramificação que colore o aro — o neutro é o caminho default, então reverter é apagar código. Nenhum token base mutado, nenhum estado persistido além de `Noise`/`RingFollowsAccent` (ignorados por build antigo).
**Checkpoint (R15):** entregar as capturas de ruído (0/2/5%) em 4 wallpapers × 2 DPIs e o par aro neutro/acento. **Parar.**

---

### F4.6 — Consolidação

- Recontagem do ledger: **37 superfícies**, nenhuma `PARCIAL`/`FORA` sem decisão escrita (as duas linhas novas são `36` e `37`, as scrollbars).
- Limpeza de tokens sem consumidor: remover `ThemeChatExpandedBrush` (`App.xaml:93`) se até aqui não tiver consumidor; `PanelGlassStrong` (`:45`) **fica** só se o popup de F4.2 o consumir — senão também sai. Cada remoção exige a linha correspondente em `LivePalette` (`:399`, `:411-414`) removida no mesmo commit, senão sobra cálculo morto.
- Atualizar `docs/` com o estado final (este documento ganha seção "Resultado").
- Suíte de gates completa, na ordem, em uma única sessão, com screenshots arquivados.
- Limpeza: nenhum `TODO`, nenhum brush órfão, nenhuma subscription sem `Unloaded`.
- Só aqui se discute bump de versão — **decisão do dono**, não do dev.

**Esforço:** 1 d.
**Rollback (específico):** a fase é doc + remoção de código morto; reverter é restaurar os dois tokens e as duas linhas de `LivePalette`. Sem impacto visual (são brush sem consumidor).
**Checkpoint (R15):** entregar a tabela final de 37 linhas + suíte de gates + lista de tokens removidos. **Parar.**

---

## 7. Ordem, dependências e esforço

```
F0.1 (0,5d) ──┐
F0.2 (0,5d) ──┴─> F3.3 (3d) ──> F4.0 (2d) ──> F4.1 (1,5d) ──> F4.2 (2,5d)
                                              └─> F4.3 (2d, pode rodar em paralelo com F4.1/F4.2)
                                                          └─> F4.4 (1,5d) ──> F4.5 (1d) ──> F4.6 (1d)
```

| Fase | Esforço | Depende de | Bloqueia | Checkpoint (R15) |
|---|---|---|---|---|
| F0.1 | 0,5 d | — | tudo (é bug de pintura) | 20 capturas + dump de `Data.Bounds` |
| F0.2 | 0,5 d | F0.1 | nada (medição) | baseline + 15 probes + definição de frame |
| F3.3 | 3 d | F0.1 (geometria viva), F0.2 | F4.0 | preview + 4 gates de paridade + diff de JSON |
| F4.0 | 2 d | F3.3 (tokens vivos) | F4.1, F4.2 | 3 estados × 11 hosts |
| F4.1 | 1,5 d | F4.0 | F4.6 | moldura/título/barra + paridade OFF |
| F4.2 | 2,5 d | F4.0 | F4.6 | popup/tooltip/player + 10 aberturas |
| F4.3 | 2 d | F4.0 | F4.6 | contraste + 3 modos |
| F4.4 | 1,5 d | F4.2 | F4.6 | RDP/Tier + 30 min de memória |
| F4.5 | 1 d | F3.3 | F4.6 | veredito escrito (a/b) + gates de (c/d) |
| F4.6 | 1 d | todas | — | tabela de 37 + suíte completa |
| **Total** | **~15,5 d** | | | |

**Escopo aprovado pelo dono (23/10):** `F0.1 → F0.2 → F3.3` **e parada**. São ~4 dias. Ali o dono tem controle total do material que existe hoje, o bug do `MediaPanel` está corrigido e existe baseline para justificar (ou não) os outros 11,5 dias. F4.0 em diante **não está autorizado** — cada checkpoint é uma decisão nova.

---

## 8. Persistência (schema)

Aditivo, dentro do `AppSettings` (`SettingsService.cs:72`). **Não duplicar `EnableGlass`** — o master continua sendo `EnableGlass:80`.

```json
"LiquidGlass": {
  "SchemaVersion": 1,
  "EdgeAlpha": 77,
  "FacetAlpha": 87,
  "SpecularAlpha": 85,
  "TintStrength": 100,
  "TintColor": "#0E1B2E",
  "EdgeWidth": 1.0,
  "FacetWidth": 1.5,
  "Noise": 0,
  "RingFollowsAccent": false,
  "OpaqueWhen": "auto"
}
```

Regras:
- **Alphas em byte (0–255)**, default exatamente `0x4D`/`0x57`/`0x55` → paridade de default por construção.
- `TintStrength` é `int` 50–150 (%), aplicado **sobre a camada derivada** (seção 4.6) — nunca sobre `IslandGlass`/`ThemeMediaGlassBrush`, que são lidos também no OFF.
- `OpaqueWhen`: `"auto"` (segue `UISettings.AdvancedEffectsEnabled` + HC + RDP + Tier), `"never"` (força translúcido, **ignorado** com HC ligado), `"always"` (força opaco). String e não bool porque "auto" é o estado real; um bool não expressa os três.
- `SchemaVersion` existe para permitir migração futura sem adivinhar. Hoje só lê 1.
- Chave ausente → default. Chave fora do range → `Validate` (`:202`) faz clamp, não descarta o arquivo. String desconhecida em `OpaqueWhen` → `"auto"`.
- `EdgeWidth`/`FacetWidth` em `double` com 1 casa; validar `> 0`.
- `Save` continua atômico (tmp + move, `:188`).
- Arquivo de 9.8.2 sem `LiquidGlass` deve carregar e **salvar depois** com o objeto completo, sem perder `MusicPrefs` nem qualquer outra chave (teste de round-trip no gate G-F3.3-8).
- **Compatibilidade de rollback:** um build 9.8.2 lendo este JSON ignora `LiquidGlass` inteiro (`System.Text.Json` com `UnmappedMemberHandling` padrão) — não lança, não zera nada. Provar com um arquivo de teste antes de fechar F3.3.

---

## 9. Contagem de material (para não estourar o orçamento)

| Métrica | Hoje | Teto na rodada |
|---|---|---|
| `DropShadowEffect` | 35 | **35** (zero novo — R3; contagem reconferida 23/10: 4 `App.xaml` + 8 `FluentControls` + 20 `MainWindow` + 2 `PlayerWindow` + 1 `SettingsPanel`) |
| Hosts com `Glass.Face` | **11** | **14** (máx. +3) |
| Hosts com `Glass.Ring="True"` | **11** | **14** (máx. +3) |
| `TechFrame` | 1º nível apenas | sem nova superfície de 2º nível |
| Timers novos | 0 | **0** |
| Dependências | 0 | **0** |

**Como os números vão a 12 e 13 (recontagem verificada em 23/10):**

| | Hoje | Previsto | +1 | +2 |
|---|---|---|---|---|
| `Face` | 11 (9 em `MainWindow`, 1 `SettingsPanel`, 1 `PlayerWindow`) | **12** | nav rail do Settings (item 27) | — |
| `Ring` | 11 | **13** | nav rail (item 27) | moldura do `AppShell` (item 01, **host de aro**, R7-a) |

- A moldura entra **só em `Ring`**: é host de aro sem face, sem `Path` (senão o "vitrificar o casco" proibido por R4 viraria realidade por acidente).
- O popup do ComboBox **não** entra: desenho próprio, sem adorner (seção 5, linha 30). As duas scrollbars (36/37) também não — são token, não material.
- `Face` 12 e `Ring` 13 são contagens **assimétricas de propósito**; qualquer fase que precise igualar as duas está criando um host que não foi aprovado.

Qualquer fase que precise furar esta tabela volta para aprovação.

---

## 10. Matriz de gates (consolidada)

| ID | Gate | Critério | Fase |
|---|---|---|---|
| G1 | Build | 0 erro / 0 warning | todas |
| G2 | OFF parity | `EnableGlass=false` → 12 pontos com Δ ≤ 1 **e** ≤ 0,05% dos pixels com Δ > 1, nenhum > 2 (janela inteira, painel **fechado**, `RenderTargetBitmap`) | todas |
| G3 | Luminância do aro | ≥ **2×** o fundo imediato (medido por 10.1, não por olho) | F3.3+ |
| G4 | Cliques | 10/10 (UIA ou mouse-walk) | F3.3+ |
| G5 | Níveis de vidro | ≤ 2 por composição | todas |
| G6 | `Effect` | contagem = 35 | todas |
| G7 | Default parity | "Restaurar padrões" → Δ ≤ 1 canal em 12 pontos, **2 capas** (escura + clara) | F3.3 |
| G8 | Persistência | 5/5 após restart | F3.3 |
| G9 | Tema × custom | 6 trocas de capa, valores intactos **nos brushes**, não só no JSON | F3.3 |
| G10 | Preview vivo | ΔL ≥ 10% no aro ao mover slider, painel aberto | F3.3 |
| G11 | Contraste | ≥ 4,5:1 geral, ≥ 7:1 denso, 4 wallpapers × **14 pares** | F4.3 |
| G12 | Frame | p95 ≤ baseline + **1,2 ms** (5 cenários × 3 execuções, 60 s) | F4.4 |
| G13 | Remoto / Tier | vai para **`Opaque`** (não OFF), app utilizável, aviso visível | F4.4 |
| G14 | Vazamento | 30 min, Δ memória < **15 MB** **e** slope < 1 MB/min | F4.4 |
| G15 | Hit-test do popup | 10/10 aberturas sem flicker | F4.2 |
| **G16** | **Geometria da face** | Após o 1º `SizeChanged` do host: `Path.Data != null`, `Bounds ≥ host − 1 px`, `Figures ≥ 1` — em 11/11 hosts | **F0.1** |
| **G17** | **Invariância ao wallpaper** | Trocar wallpaper com vidro ON e `OpaqueWhen=never` → face **não** muda de opacidade (só o que está atrás muda); já com `auto`/transparência off, face **não** translúcida | **F4.3** |

**Por que G16 existe:** captura de pixel no centro do `MediaPanel` pode passar com `Data=null` se o host tiver `Background` próprio herdado de outro caminho. A prova de que a face existe é a geometria, não a cor. (P2 do Codex.)

**Por que G17 existe:** é o único teste que separa "vidro de verdade" de "sólido com alpha chutado". Se a opacidade da face variar com o wallpaper, alguém colocou leitura de wallpaper/tinta adaptativa onde não devia (F4.5b está adiada por decisão).

### 10.1 Procedimentos de medição (sem isto, gate é opinião)

Todos os gates acima referenciam um destes cinco métodos. Nenhum usa screenshot de tela: DWM, escala do Windows e wallpaper contaminam o resultado.

1. **Captura determinística (`RenderTargetBitmap`)** — `PngBitmapEncoder` de `Window` a 96 DPI, `RenderOptions.BitmapScalingMode=NearestNeighbor`, janela em tamanho fixo **1280×800**, `WindowState=Normal`, `EffectsProfile` registrado no nome do arquivo. Comparação pixel-a-pixel em .NET (não em ferramenta de imagem). 12 pontos fixos pré-definidos em coordenadas do cliente (4 no aro, 4 no centro de faces, 4 no chrome). Usado por G2, G7, G11, G17.
2. **Frame time (p95)** — `glass_probe` (F0.2) amostrando `CompositionTarget.Rendering` por **60 s** por execução, **3 execuções**, descartando os **2 s iniciais** (warm-up/JIT) **e** o primeiro frame após qualquer resize. p95 = percentil 95 dos intervalos em ms. Baseline e medição usam o mesmo refresh (registrar Hz) e a mesma resolução. Usado por G12.
3. **Memória** — `PrivateMemorySize64` amostrado 1×/s por 30 min com troca de capa a cada 5 s; reportar Δ total e slope por regressão linear simples. Falha se Δ ≥ 15 MB **ou** slope ≥ 1 MB/min (o slope pega vazamento lento que 15 MB esconde). Usado por G14.
4. **Contraste (WCAG)** — luminância relativa `L = 0.2126R + 0.7152G + 0.0722B` com linearização `c ≤ 0.03928 ? c/12.92 : ((c+0.055)/1.055)^2.4`; razão `(L1+0.05)/(L2+0.05)`. Amostrar o **pior pixel** da caixa de texto (não a média) contra o fundo **composto** (face sobre wallpaper). Usado por G11 e G3.
5. **Cliques/hit-test** — UIA (`AutomationElement`) quando existir peer; senão mouse-walk em coordenadas conhecidas com verificação de estado depois do clique (não de "aconteceu algo na tela"). Usado por G4, G15.

**Regra de evidência:** cada gate entregue no checkpoint é um arquivo em `docs/evidence/<fase>/`, nomeado `<gate>-<cenario>-<timestamp>.png|jsonl|csv`. Sem arquivo, o gate não passou — "testei e funciona" não conta.

---

## 11. Riscos

### R-A (crítico) — `ApplyTheme` apaga a configuração do usuário
`LivePalette.cs:421-422` reescreve os alphas do aro a cada troca de capa. Se o estado do usuário não entrar **antes** dessa escrita, todo slider volta ao default sempre que a capa muda — e o dono vai achar que "não salva".
**Mitigação:** ponto único de composição (`GlassMaterial.Apply`, seção 4.6, item 1) chamado no fim de `ApplyTheme`; ordem tema → usuário → tinta. Não existe segundo lugar escrevendo alpha, então "esquecer de reaplicar" deixa de ser possível. Gate G9 prova nos brushes.

### R-B (crítico) — Brush frozen / `StaticResource` quebra a atualização ao vivo
**Correção da rodada 1 (P3):** `_glassHighlight` (`LivePalette.cs:162`) **não é frozen** — é `readonly` e nunca é tocado por `ApplyTheme`/`SetAccent` (seção 4.2). `readonly` é sobre a referência, não sobre o brush. O risco real é outro e continua válido: (a) se algum consumidor usar `StaticResource`, trocar a entrada do `Dictionary` não propaga; (b) se algum brush estiver congelado, mutar `.Color` lança `InvalidOperationException`. Os helpers `Solid`/`Grad` (`:268-283`) já contornam (b) substituindo a entrada; (a) **não** é contornável por substituição.
**Mitigação:** auditoria obrigatória antes de codar F3.3 — `IsFrozen` nos 6 tokens vivos + `DynamicResource` vs. `StaticResource` em **cada** consumidor da seção 4.6. O `Path` já usa `SetResourceReference` (`Glass.cs:407`) e o anel re-resolve (`:191-196`), então os dois caminhos de vidro propagam; o perigo está nos `Background` diretos. Gate G10 prova. **Nenhum código de F3.3 começa antes dessa auditoria estar escrita em `docs/evidence/f3.3/brush-audit.md`.**

### R-F (alto, novo — P1-C) — Token de vidro tem consumidor no caminho OFF
Cinco tokens de material são lidos **fora** do `Face`/`FaceFrosted`: `ThemeFieldGlassBrush` (`MainWindow.xaml:617` composer, `SettingsPanel.xaml:186` field), `ThemeChipGlassBrush` (`MainWindow.xaml:631` chip de envio), `ThemeChatExpandedDenseBrush` (`MainWindow.xaml.cs:1794`, ambos os modos), `ThemePanelSolidBrush` (`MainWindow.xaml:743` LRC), e `IslandGlass`/`ThemeMediaGlassBrush` como `Face` em hosts que pintam nos dois modos. Mutar qualquer um deles no lugar quebra a paridade OFF **sem tocar no vidro** — e o sintoma aparece só no gate G2, tarde.
**Mitigação:** camada derivada (seção 4.6) — só ela é mutada; lista de exclusão explícita na matriz de sliders; `ResetToBase()` ao entrar em OFF; G2 roda em **todas** as fases, não só F3.3.

### R-C (alto) — Custo visual ao cobrir mais superfícies
Já são 35 `Effect` e 11 hosts de vidro. Levar vidro a nav rail e moldura aumenta composição, adorners e risco de clique perdido (a sangria transparente do padrão F2B já mostrou isso no F2.6).
**Mitigação:** teto da seção 9 (assimétrico: Face 12, Ring 13), `TechFrame` proibido em 2º nível (R11), gate G4 em toda superfície nova, e F4.4 como porta antes de F4.6.

### R-D (médio) — `MediaPanel` "consertado" só na aparência
Se a F0.1 assinar o host mas não desinscrever, ou reaplicar sem guarda, o painel pinta — e vaza memória/CPU durante a animação de 280 ms. **Aguardar:** corrigir a geometria sem corrigir a subscription troca um bug visual por um vazamento silencioso.
**Mitigação:** guarda de idempotência (já existente, `:401-406`) + `Unloaded` + contador de subscription viva (G-F0.1-4) + gate G14.

### R-E (médio) — Expectativa de refração real
O dono pode esperar refração de verdade. Ela não é viável em WPF sem dependência (F4.5a). Se a expectativa não for alinhada **antes** de F4.5, a entrega parece incompleta mesmo estando correta.
**Mitigação:** este documento + o substituto vetorial de "dobra" na faceta.

### R-G (médio, novo — P1-D) — Detecção de acessibilidade que falha ou mente
`UISettings` é WinRT: pode lançar em host restrito, e "Transparency effects" desligado **não** significa "usuário quer opaco para sempre" (ele pode desligar para economizar e querer o app bonito). Falso positivo tira o material sem o dono entender.
**Mitigação:** degradar para "não detectado" em vez de assumir; prioridade explícita (HC > transparência > usuário > auto); override manual por app; motivo visível na UI; G17 separa "vidro real" de "sólido chutado".

**Rollback global:** `EnableGlass=false` sempre volta ao 9.8.2 (é o contrato R8). Cada fase é um commit próprio, revertível isolado, com a linha `Rollback:` específica. Nenhuma migração de JSON é destrutiva — build antigo ignora `LiquidGlass` inteiro. Nenhum token base é mutado, então reverter o código já devolve os alphas originais.

---

## 12. Decisões do dono — **RESOLVIDAS em 23/10**

| # | Pergunta | Decisão | Consequência registrada |
|---|---|---|---|
| a | Ordem: parar em `F3.3` ou ir até `F4.6`? | **Parar em F3.3.** Escopo aprovado: `F0.1 → F0.2 → F3.3`, ~4 dias. | F4.x não autorizado; cada checkpoint é decisão nova (§7). |
| b | Moldura (`01`): vitrificar o casco? | **Não.** Hairline → **aro de vidro**, casco intacto. | Host **de aro** (R7-a): `Ring` sem `Face`, sem `Path`. §9 vai a Ring 13 / Face 12. |
| c | Título (`02`): vidro ou flutuação? | **Flutua, sem face.** | Exceção documentada; é o que deixa o backdrop vazar. |
| d | Aro seguindo o acento | **Opt-in, default OFF.** | `RingFollowsAccent=false`; faceta permanece neutra; G3 continua valendo. |
| e | `MessageBox` (`32`) | **Nativo do Windows.** | Exceção deliberada; não reimplementar janela. |
| f | Processo | **Nova regra R15: checkpoint obrigatório ao fim de cada fase**, com parada e aprovação explícita. | Toda fase ganhou bloco `Checkpoint:`; nenhuma fase emenda na seguinte (§6). |

Nenhuma pergunta em aberto. Qualquer dúvida nova vira linha nesta tabela **antes** de virar código.

---

## 13. Critério de aceite final

A rodada está concluída quando:

- [ ] Os **17** gates da matriz (G1–G17) passam, com evidência arquivada em `docs/evidence/<fase>/` (arquivo nomeado, não relato verbal).
- [ ] Nenhuma das **37** superfícies é `PARCIAL` ou `FORA`; todas são `ON` ou `EXCEÇÃO` com motivo escrito.
- [ ] `EnableGlass=false` reproduz o 9.8.2 (G2: 12 pontos Δ ≤ 1, ≤ 0,05% dos pixels com Δ > 1, nenhum > 2).
- [ ] "Restaurar padrões do vidro" reproduz o 9.8.2 (G7: Δ ≤ 1 canal, 2 capas).
- [ ] Trocar de capa não altera nenhum valor do usuário (G9, verificado nos brushes).
- [ ] Build 0/0; contagem de `Effect` = 35; dependências = 0; timers novos = 0.
- [ ] `MediaPanel` pinta em 20/20 aberturas **e** tem geometria real (G16) em 11/11 hosts.
- [ ] Nenhum token base mutado: `IslandGlass`, `ThemeFieldGlassBrush`, `ThemeChipGlassBrush`, `ThemeChatExpandedDenseBrush`, `ThemePanelSolidBrush` com os valores de tema em qualquer estado.
- [ ] Versão: **decisão do dono** (congelada em 9.8.2 até então).

---

## Changelog

### Rodada 2 (23/10/2026) — resposta ao `REVIEW_plano_r1_CODEX.md`

**P1-A — inventário incompleto (scrollbars).** O plano listava 35 superfícies; faltavam duas. Verificado: `SettingsPanel.xaml:299` usa `ThinScrollBarStyle` (`:301`), e o `ScrollViewer` do popup do ComboBox (`FluentControls.xaml:765`, `MaxHeight=180`) **não tem** estilo de `ScrollBar` — logo herda o chrome clássico do Windows dentro de um popup escuro. Linhas **36** e **37** adicionadas; total **37**; `FORA` 12 → **14**. A cor do thumb é hardcoded em `FluentControls.xaml:626` (`#A640546E`, hover `:629`, drag `:632`) — não no `ScrollViewer`, como a rodada 1 dava a entender. Contagens de §5.1, §9, §13 atualizadas; §9 ganhou a tabela de como Face vai a 12 e Ring a 13 (assimétrico de propósito).

**P1-B — mapa token → controle → linha faltando.** Nova **seção 4.6** com a matriz completa (12 tokens × declaração × default × consumidores reais × papel × "slider mexe?"). Achados: `PanelGlassStrong` (`App.xaml:45`) e `ThemeChatExpandedBrush` (`:93`) **não têm consumidor** (só `LivePalette` os calcula) → `ThemeChatExpandedBrush` sai em F4.6, `PanelGlassStrong` vira face do popup em F4.2. `FluentControls.xaml:690` (`FloatingIslandStyle`) é **inerte**: os 3 hosts que usam o estilo zeram `Background`. `_glassHighlight` nunca é tocado por `ApplyTheme`/`SetAccent`.

**P1-C — paridade OFF não se sustenta mutando token de face.** Cinco tokens de material têm consumidor **fora** do vidro (`ThemeFieldGlassBrush` em `MainWindow.xaml:617` e `SettingsPanel.xaml:186`; `ThemeChipGlassBrush` em `:631`; `ThemeChatExpandedDenseBrush` via `Glass.SetFace` em `MainWindow.xaml.cs:1794`, nos dois modos; `ThemePanelSolidBrush` em `:743`; e `IslandGlass`/`ThemeMediaGlassBrush` como `Face` em hosts que pintam nos dois modos). Desenho trocado: **camada derivada** (`GlassFaceIsland`, `GlassFaceMedia`, `GlassFaceField`) + `GlassMaterial.ResetToBase()`; tokens base **nunca** mutados. Novo risco **R-F**. G2 recortado: janela inteira, painel fechado, 12 pontos, ≤ 0,05% dos pixels com Δ > 1, nenhum > 2.

**P1-D — acessibilidade com as APIs erradas.** `SystemParameters.ClientAreaAnimation` é **animação**, não transparência. Trocado por `UISettings.AdvancedEffectsEnabled` (+ evento, desde 10.0.15063; TFM 19041 já projeta), `UISettings.AnimationsEnabled`, `SystemParameters.HighContrast` + `StaticPropertyChanged`, e fallback de registro `EnableTransparency`. **Modo opaco virou terceiro modo** (`GlassMode.Opaque`), distinto de OFF; F4.3 e F4.4 reescritas (RDP/Tier vão para `Opaque`, não OFF); nova chave `OpaqueWhen`; novo risco **R-G**; novo gate **G17** (invariância ao wallpaper).

**P2 — diagnósticos e gates imprecisos.**
- `MediaPanel`: o defeito é **`Path.Data` permanecer `null`** (`Glass.cs:389` retorna antes de escrever), não "Data 0×0"; e o `SizeChanged` do `Path` nunca dispara porque a `Shape` não cresce. Causa raiz: falta subscription no **host**. Novo gate **G16** (geometria, não pixel).
- `TechFrame` e `Chamfer`: **já** assinam `SizeChanged`/`Loaded` do próprio elemento (`Chamfer.cs:26-29`, `TechFrame.cs:33-37`) — a promessa de "corrigir no mesmo commit se tiverem a mesma guarda" foi removida; eles não têm o problema.
- Guarda de idempotência **já existe** (`Glass.cs:401-406`); a rodada 1 a listava como mudança nova.
- Baseline: instrumentação deixou de ser "opcional se manual for inconclusivo" — agora é obrigatória, com definição explícita de frame (p95, 60 s, 3 execuções, descarte de 2 s) e prova de ausência em Release.
- Nova **§10.1** com os 5 procedimentos de medição (`RenderTargetBitmap`, p95, memória com slope, WCAG, UIA) e a regra de evidência por arquivo. Gates G2/G7/G11/G13/G14 reescritos em cima deles; 15 → **17 gates**.

**P3 — "readonly ≠ frozen".** Rodada 1 afirmava que `_glassHighlight` estava frozen. Não está: é `readonly`, e o projeto não tem `Freeze()` na paleta; os helpers `Solid`/`Grad` (`:268-283`) tratam frozen de forma reativa substituindo a entrada do dicionário. Requisito reescrito: garantir que o consumidor re-resolva a referência (o que `Glass.cs:191-196` e `:407` já fazem) e auditar `IsFrozen` + `DynamicResource` antes de codar — com o artefato `docs/evidence/f3.3/brush-audit.md` como pré-condição.

**Decisões do dono (a–f) registradas como RESOLVIDAS em 23/10** (§12), incluindo a nova regra de processo **R15** (checkpoint obrigatório por fase) e o escopo aprovado `F0.1 → F0.2 → F3.3` com parada. Cada fase ganhou `Rollback:` **específico** e `Checkpoint:`.

**Correções factuais avulsas:** TFM é `net8.0-windows10.0.19041` (`KatieHoloDeck.csproj:5`), não .NET 9; telemetria no `MainWindow.xaml:183` (a rodada 1 dizia `:184`); `RingAdorner` tem **três** medidas (0,5 / 1,5 / 1,0) em `:158-165`, não duas; `DwmCompat` grava `dwm_debug.jsonl` em `BaseDirectory` (`:65`) e o gate de backdrop é 22621 (`:36`); `SettingsService.cs:80` tem `EnableGlass=true` com doc-comment dizendo "Default OFF" (registrado, não corrigido).

**Não alterado:** nenhum arquivo de código, nenhum commit, versão 9.8.2.

---

### Rodada 1 (22/10/2026) — plano inicial *(histórico; números e diagnósticos superados pela rodada 2)*

**Adicionado**
- Inventário de superfícies com `arquivo:linha`: 10 `ON`, 3 `PARCIAL`, 12 `FORA`, 10 `EXCEÇÃO` (35 no total — **recontado para 37** na rodada 2).
- Gerado `docs/liquid-glass-plan.md` com 10 fases (F0.1, F0.2, F3.3, F4.0–F4.6).
- Definida a 9ª aba "Liquid Glass", com `GlassChk` migrado de Aparência e `PreviewTile` local obrigatório (o painel cobre o app, logo preview nas ilhas é impossível).
- Mapa token → controle com 7 sliders + cor + 2 toggles, ranges e defaults que reproduzem o 9.8.2 (alphas em byte).
- Schema `LiquidGlass` aditivo, sem duplicar `EnableGlass`.

**Corrigido (diagnóstico, não código)**
- Causa raiz da falha de pintura do `MediaPanel`: `Glass.ApplyFace` (`Glass.cs:374`) assina `Path.Loaded`/`Path.SizeChanged`, nunca o host. *(rodada 2: detalhado como `Data=null`; `TechFrame`/`Chamfer` não têm o problema.)*
- `Glass.Resolve` (`Glass.cs:54`) sempre retorna `Vector` — modo `Shader` é código morto.
- `LivePalette.ApplyTheme` (`:421-422`) sobrescreve os alphas do aro. *(rodada 2: `_glassHighlight` **não** é frozen — ver P3.)*

**Avaliado**
- Refração HLSL: **descartada** (WPF `ShaderEffect` não lê o fundo; snapshot custa 3–8 ms e atrasa o conteúdo; interop D3D fura o contrato de zero dependência). Substituto vetorial de "dobra" na faceta.
- Tinta adaptativa por wallpaper: **adiada** (conflita com o master de capa; Mica já adapta o backdrop).
- Ruído: **incluído** como opcional default 0%, `DrawingBrush` estático, sem `Effect`.
- Aro seguindo acento: **incluído como opt-in**, default OFF, com a faceta permanecendo neutra.

**Não alterado**
- Nenhum arquivo de código. Nenhum commit. Versão permanece 9.8.2.
