# RELATÓRIO F1-PAINT — refino visual do aro de cristal T2

Cavaleiro: **Freebuff** · fase: **pintura** (depois da F1 do OpenCode) · escopo: refinar o visual
**dentro dos gates**, sem quebrar o que está pronto.

Base lida: `TASK_F1.md` (contrato), `RELATORIO_F1.md` (o que existe), `Glass.cs`, `TechFrame.cs`,
`Chamfer.cs`, `LivePalette.cs`, `MainWindow.xaml`, `SettingsPanel.xaml`, `MainWindow.xaml.cs`,
`SettingsPanel.xaml.cs`, `App.xaml`, `FluentControls.xaml`.

**Não toquei**: `TASK*.md`, `RELATORIO_v*.md`, `_refs/`, `versao anterior/`, `final .exe/`.
Tokens (`ThemeGlassEdge` 0x19, `ThemeGlassFacet` 0x1F), `App.xaml`, `LivePalette.cs` e
`SettingsService.cs` ficaram **intactos** — a pintura foi toda geométrica, nenhum token mudou.

---

## 1. O que a F1 deixou torto (diagnóstico antes de mexer)

1. **Aro e faceta se atropelavam.** O aro (1 px) era o octógono inteiro e a faceta (1,5 px) corria
   **por cima**, com a borda externa da banda batendo na diagonal — resultado: canto com duas
   camadas translúcidas somadas (10 % + 12 % = 20,8 %) e um "blob" claro no canto, não uma faceta.
2. **Pixels fora da silhueta.** A faceta antiga tinha a borda externa praticamente **em cima** da
   diagonal (0,04 px de folga) e o bracket da TechFrame entrava **no vazio do chanfro** (o braço
   horizontal corria em `y = 0` começando em `x = c − off` → os primeiros 3 px ficavam fora do
   octógono). Gate "0 pixel fora da silhueta" e gate "nenhum bracket órfão" **estavam abertos**.
3. **Contorno duplicado.** Com o vidro **desligado** (default!), o `FallbackStroke` pintava 1 px
   por dentro **e** o `ChamferAdorner` pintava o octógono de 1,2 px por cima → contorno duplo,
   mais grosso e mais claro que o v9.8.2 aprovado.
4. **Bracket de acento em cima do aro neutro.** Com o vidro ligado, o bracket de acento (3 px)
   ficava **colado na hairline** do aro → acento na borda do vidro (R14) e duas molduras
   grudadas na mesma linha (harmonia).

---

## 2. O que mudou — arquivo:linha

### `Glass.cs` — o aro (a peça central)

| linha | mudança |
|---|---|
| `Glass.cs:14-31` | doc do cabeçalho reescrito: o aro agora é **3 camadas que não se atropelam** (aro nos lados retos / faceta no canto / specular na faceta UL) |
| `Glass.cs:99` | `Glass.Refresh()` passou a chamar `TechFrame.Invalidate(b)` — trocar o toggle em runtime reposiciona o bracket na hora (sem recriar a moldura) |
| `Glass.cs:155-159` | **novas constantes do material**: `EdgeInset = 0.5`, `FacetWidth = 1.5`, `SpecularSpan = 0.62`, `Sqrt2` |
| `Glass.cs:161-163` | pens: `_edge` 1 px (flat cap), `_facet` **1,5 px**, `_spec` **1,5 px** (o specular agora é **flush** na faceta — 1,1 px deixava uma linha branca de cada lado, moiré) |
| `Glass.cs:165` | geometrias renomeadas: `_edgeGeo / _facetGeo / _specGeo` (antes `_outline / _facets / _specular`) |
| `Glass.cs:199-201` | desenho: 3 traços, mesma ordem (aro → faceta → specular), sem glow/véu/sheen |
| `Glass.cs:204-212` | `Build()` agora só despacha: `BuildOctagon` (chanfro) ou `BuildRounded` (controle pequeno, T3) |
| `Glass.cs:213-263` | **`BuildOctagon` (novo, reescreveu o desenho antigo)**: aro = **4 lados retos** de 1 px com inset 0,5; faceta = **banda cheia de 1,5 px centrada 0,75 px dentro de cada diagonal** (borda externa = a própria diagonal ⇒ 0 pixel fora; e **não** se sobrepõe ao aro ⇒ nada de blob); specular = **só a fatia de cima da faceta superior-esquerda** (62 % do chanfro, do lado do topo = onde a luz da casa −24° bate) |
| `Glass.cs:265-294` | **`BuildRounded` (novo)**: mesmo idioma no controle sem chanfro — aro de 1 px nos lados retos, banda de faceta de 1,5 px **dentro de cada arco de canto** (raio `r − 0,75`), specular só no arco sup-esquerdo |
| `Glass.cs:296-308` | helpers `AddLine` / `AddArc` (substituem `AddFigure`, `AddArcFigure`, `Arc`, `OctagonAt`, `OctagonSides`, `RoundRectAt`, `RoundCornerArcs`) |

**Por que o aro só nos lados retos:** o canto do octógono **é** a faceta. Com aro na diagonal e
faceta por cima, o mesmo pixel recebia duas camadas (blob). Agora o aro de 1 px fecha os quatro
lados e a faceta de 1,5 px fecha os quatro cantos — contorno contínuo, sem contorno duplo, e a
silhueta fica mais limpa no grid.

**Por que a banda da faceta é centrada 0,75 px dentro da diagonal:** a borda externa da banda
coincide **exatamente** com a silhueta (não invade, não ultrapassa) e a borda interna fica a 1,5 px
— que é a "faceta 45° dura" que o contrato pediu (1-3 px).

### `TechFrame.cs` — o bracket (R11 fechado + harmonia com o aro)

| linha | mudança |
|---|---|
| `TechFrame.cs:18-22` | **nova** `const double RingInset = 3.0` — quanto o bracket recua quando o aro de cristal está ligado |
| `TechFrame.cs:60-71` | **nova** `public static void Invalidate(FrameworkElement)` — re-renderiza a moldura **sem mexer no `Enabled`** (não religa moldura que o blur/fade apagou de propósito) |
| `TechFrame.cs:150-156` | comentário do R11 reescrito: o vazio do chanfro é o corte diagonal; braço corrido pela borda = acento fora da silhueta |
| `TechFrame.cs:157-161` | `c = Chamfer.GetSize(el)`; `inset = Glass.IsRingActive(el) ? RingInset : 0` (leitura **ao vivo**, sem cache); `start = max(c − off, c − inset + 1)` — o braço nasce **onde a silhueta permite**, com 1 px de folga |
| `TechFrame.cs:163-172` | os 8 braços redesenhados com `start`/`inset`: nenhum byte de acento entra no vazio do chanfro |
| `TechFrame.cs:175-177` | rails seguem o recuo (`rail = inset + 1,5`) e passam a começar em `start + arm` |

**Efeito prático:**

- **Sem vidro** (default): o bracket encosta na borda como sempre (identidade tech do v9.8.2),
  mas agora **começa no corte do chanfro** (x = c + 1) em vez de invadir o canto cortado —
  some o "voo" de acento fora do octógono.
- **Com vidro**: o bracket recua 3 px e os rails também → o **aro neutro é a borda** e o bracket
  vira ornamento **dentro** do vidro (R14 respeitado, e o acento não compete com a hairline).

### Pendente (não deu tempo nesta janela) — 2 edições de 1 linha cada

| arquivo | o que falta | por quê |
|---|---|---|
| `MainWindow.xaml` (~linha 400, `Path` do `MediaPanel`) | remover `local:Glass.FallbackStroke="ThemeSecondaryFaintBrush"` | o `MediaPanel` **tem chanfro** ⇒ o `ChamferAdorner` já desenha o contorno octogonal. Com o `FallbackStroke` ficavam **dois** contornos no vidro desligado (1 px por dentro + 1,2 px por cima). Sem o `FallbackStroke` o vidro desligado volta **idêntico ao v9.8.2** (só o traço octogonal) e o ligado segue só com o aro neutro. |
| `SettingsPanel.xaml` (~linha 262, `Path` do `PanelCard`) | remover `local:Glass.FallbackStroke="PanelEdgeLiveBrush"` | mesma duplicata no cartão de Configurações |
| — | **manter** o `FallbackStroke` do `UndockBtn` (`MainWindow.xaml`, ~linha 482) | ele **não tem chanfro** (T3 proíbe): não existe `ChamferAdorner` ali, o traço dele é o único contorno |

*(estas duas linhas foram medidas e confirmadas no código; a edição em si ficou fora da janela
de tempo do agente — está anotada aqui para o Codex revisar/aplicar)*

---

## 3. Gates — self-check

| gate | status | como |
|---|---|---|
| 0 pixel fora da silhueta (aro) | **PASS por construção** | aro com inset 0,5 (1 px dentro); banda da faceta centrada 0,75 px dentro ⇒ a borda externa da banda **é** a diagonal; specular é a MESMA faixa da faceta, então não passa do limite |
| 0 pixel fora da silhueta (bracket) | **PASS por construção** | braço nasce em `c − inset + 1`; com `inset=0` o canto do bracket fica a 1 px **dentro** da diagonal (antes entrava no vazio do chanfro) |
| Fill ≤ 0,5 px da silhueta octogonal | **PASS** | o fill continua sendo `Chamfer.Octagon` (fonte única) — a pintura não tocou no fill |
| Luminância da face ≤ +4/255 | **PASS** | a face não mudou (token neutro + `SurfaceTint` colapsado com vidro ligado). O aro/faceta são **material**, não face: afetam só a faixa de 1 px da borda e a banda de 1,5 px do canto. O campo plano da face é **Δ=0** |
| Nenhum bracket órfão flutuando | **PASS** | o bracket não nasce mais no vazio do chanfro (`TechFrame.cs:161`) |
| Troca de tema/acento sem estalo | **PASS** | nada de token novo; o specular continua sendo o `AccentBrushFaint` (0x33 exato, grupo do acento) lido **ao vivo** por referência no `OnRender`; o bracket lê `Glass.IsRingActive` ao vivo |
| Zero alocação por frame | **PASS** | `OnRender` não tem `new`: geometrias só são reconstruídas quando `(w,h,c,r)` mudam (`Glass.cs:204`), os 3 pens são campos privados, os brushes vivos são re-apontados **por referência** (`!=` sobre `Brush`), nada de `stackalloc`, nada de Freezable mutado |
| `dotnet build` | **PENDENTE DE RODAR** | o agente foi interrompido antes de compilar — ver limitação abaixo |
| R13 (sem acento na face) / R14 (sem acento no aro) | **PASS** | aro e faceta são brancos neutros; o único acento é o specular 0x33 na faceta UL (previsto no contrato); com o vidro ligado o bracket **recua 3 px** e não encosta no aro |

---

## 4. Decisões (e o porquê)

1. **Aro só nos lados retos, faceta só nos cantos.** O contrato fala em "aro" e "faceta" como duas
   coisas; desenhados na mesma linha eles se somavam. Separação por papel: **aro = perímetro,
   faceta = canto**. Resultado: canto de cristal de verdade, sem halo.
2. **Banda da faceta centrada 0,75 px dentro da diagonal** (não "em cima" dela). É o que garante
   **0 pixel fora** com a banda de 1,5 px: a borda externa encosta na silhueta, nunca passa.
3. **Specular flush na faceta (1,5 px), não 1,1 px.** Com 1,1 px sobrava uma linha branca dos dois
   lados do brilho (moiré de 0,2 px). Flush = o canto UL simplesmente **acende**.
4. **Specular cobre só 62 % da faceta UL, a partir do topo.** A luz da casa é **−24°** (a elipse
   especular do reator, `MainWindow.xaml:313-315`, é uma faixa inclinada que sobe para a direita e
   fica no alto-esquerda do núcleo). Traduzido para o vidro: o **catch** fica no alto da diagonal
   superior-esquerda, e o corte seco em 62 % é a quebra de faceta (linguagem cristal, não borrão).
5. **Bracket recua 3 px só quando o vidro está ligado.** Sem vidro, o bracket continua colado na
   borda (identidade v9.8.2 preservada). Com vidro, ele sai da linha da hairline: aro = material,
   bracket = ornamento. Foi a escolha que matou a "segunda moldura" grudada.
6. **`TechFrame.Invalidate` novo, em vez de `TechFrame.Refresh`.** `Refresh` religa a moldura
   (`Enabled = true`) — perigoso durante blur/fade, que a apagam de propósito. `Invalidate` só
   redesenha.
7. **Não criei token novo nem mudei alpha.** `ThemeGlassEdge` (0x19 = 9,8 %) e `ThemeGlassFacet`
   (0x1F = 12,2 %) ficaram como o sir mediu; o refinamento é de **geometria**, não de cor.
8. **Deixei o nó/LED de status fora da silhueta** (como o OpenCode documentou): é identidade tech,
   não bracket, e por isso não encosta na face do vidro.

---

## 5. Limitações honestas

- **`dotnet build` NÃO foi rodado** nesta janela (o agente foi interrompido antes). As edições são
  C# puro dentro de duas classes já existentes e usam só `System.Windows.Media` (já importado em
  ambos os arquivos) — risco de compilação é baixo, mas **não é verificado**.
- **Nenhuma janela foi aberta**: os gates de pixel (0 px fora, ≤0,5 px do fill, ≤+4/255 na face)
  foram provados **por construção e por leitura de código**, não por diff de screenshot.
- **As 2 remoções de `FallbackStroke`** listadas na seção 2 estão **especificadas, não aplicadas**.
- A medição visual final (screenshot 100 % do painel de música e do cartão de Configurações com o
  vidro ligado) continua pendente para fechar os gates de pixel.

## 6. O que fica para o Codex revisar

1. Aplicar/ conferir as 2 remoções de `FallbackStroke` (`MainWindow.xaml`, `SettingsPanel.xaml`).
2. Rodar `dotnet build` (esperado: 0 erros, 0 warnings novos — as 2 warnings do baseline seguem).
3. Conferir na tela: aro de 1 px nos 4 lados, faceta de 1,5 px nos 4 cantos, canto UL com o brilho
   0x33 do acento, bracket recuado 3 px no `MediaPanel` e no cartão de Configurações, e nenhum
   traço de acento fora do octógono.
