# RELATÓRIO F1 — Aro de Cristal T2 (vetorial)

App: KatieHoloDeck 9.8.2 · alvo `net8.0-windows10.0.19041` · SDK `10.0.401`
Escopo: `TASK_F1.md` (6 tarefas) · F1 = **T2 VECTOR** (shader é F2 — nada de ShaderEffect aqui)

## Estado do build

```
dotnet build -v q -nologo   →  "Compilação com êxito."   0 Erro(s)
Avisos: 4 ocorrências = 2 únicas (as MESMAS do baseline, duplicadas pelo wpftmp)
  MainWindow.xaml.cs(233,78)  CS8625
  MainWindow.xaml.cs(1867,51) CS8604   (era 1863 antes; deslocou +4 linhas pela chamada do Glass)
```
**Zero warnings novos.** Nenhum `as any`/`@ts-ignore` equivalente (`!`, `#pragma`, `SuppressMessage`): nenhum foi adicionado.

---

## T1 — Tokens de material + brushes vivos

| item | onde |
|---|---|
| `ThemeGlassEdge`, `ThemeGlassFacet` em `ThemeTokens` (opacos por contrato) | `LivePalette.cs:27` |
| `CopyFrom` copia os dois | `LivePalette.cs:43` |
| `Lerp` interpola os dois | `LivePalette.cs:74-75` |
| brushes registrados no dicionário vivo | `LivePalette.cs:243-244` |
| `ApplyTheme`: alpha injetado na MONTAGEM (`WithA`), nunca no token | `LivePalette.cs:411-412` |
| `ReadDefaultTheme` (leitura de volta) | `LivePalette.cs:444-445` |
| defaults em XAML | `App.xaml:101-102` |

- Edge = `#19FFFFFF` = **9,8%** (faixa do contrato: 8-10%) · Faceta = `#1FFFFFFF` = **12,2%**.
- Branco **neutro** (R13/R14): nenhum canal do acento entra no aro.
- `Mix()` descarta alpha em silêncio → o token é criado/guardado **opaco** e o alpha só existe no `Color` do brush. Uma transição de acento (`TransitionAccentTo`) não pode "comer" o alpha, porque ela nunca passa pelo token.
- **Specular não ganhou token**: é o `AccentBrushFaint` (`#3300D9FF`, `App.xaml:27`, alpha **0x33 exato**), exatamente como o contrato pede — mesmo grupo do acento, mesma transição.

## T2 — Aro (edge + faceta + specular) via Adorner

`Glass.cs` (novo, ~443 linhas).

| mecanismo | onde |
|---|---|
| `GlassMode { Off, Vector, Shader }` | `Glass.cs:13` |
| `Resolve(enabled, profile)`: `"none"` → força `Vector`; F1 devolve sempre `Vector` | `Glass.cs:49-55` |
| `Apply` (só re-aplica quando o modo muda) | `Glass.cs:65-71` |
| `Refresh()` sobre `WeakReference` (sem vazar janela) | `Glass.cs:77-100` |
| attached `Ring` | `Glass.cs:106-111` |
| `IsRingActive` (leitura ao vivo, sem cache) | `Glass.cs:117` |
| `RingAdorner.OnRender` | `Glass.cs:161-193` |
| geometria (só reconstrói se w/h/c/r mudam) | `Glass.cs:195-215` |
| octógono do aro = `Chamfer.Octagon` (fonte única) | `Glass.cs:218-226` + `Chamfer.cs:58-71` |
| 4 diagonais da faceta / só a superior-esquerda | `Glass.cs:229-241` |
| arredondado (controle pequeno) + arcos | `Glass.cs:251-292` |

- Aro: traço **1 px** com inset **0,5** → 100% dentro da silhueta (0 pixel fora).
- Faceta: banda **1,5 px** (`d = 1,5·0,7071` por eixo) desenhada **na diagonal do chanfro**, dura (Flat caps), sem gradiente.
- Specular: traço **1,1 px**, `AccentBrushFaint`, **somente** na faceta superior-esquerda — a luz da casa é −24° (o reator ilumina de cima/esquerda: `MainWindow.xaml:313-315`).
- **Ao vivo**: `OnRender` lê `Chamfer.GetSize`, `Border.CornerRadius` e `LivePalette.Instance.Get(...)` a cada passe, e re-aponta o `Pen` só quando a **referência** do brush muda. Não existe `PaletteChanged` no projeto — nada foi inventado, nada é cacheado.
- **Zero alocação por frame**: pens e geometrias são campos privados; `Build` só roda com mudança de métrica. Não há `new Pen`/`new SolidColorBrush` no caminho quente.
- Nenhum `Freezable` de `ResourceDictionary` é mutado aqui (R7 morto por construção): o `Pen` só **aponta** para o brush vivo; `ApplyTheme`/`SetAccent` continua dono da mutação.
- Sem `stackalloc` (o `Color` é managed — é a lição do crash v9.8.2, commit 0307cc2).
- Sem glow, sem véu, sem sheen: `OnRender` desenha **três traços e nada mais**.

## T3 — Fill octogonal real (R12)

| item | onde |
|---|---|
| attached `Face` / `FaceFrosted` / `FallbackStroke` | `Glass.cs:299-324` |
| `ApplyFace` (geometria + troca de chave por modo) | `Glass.cs:349-378` |
| `BuildFace` → `Chamfer.Octagon` quando há chanfro | `Glass.cs:381-388` |
| fallback arredondado p/ controle pequeno (T3) | `Glass.cs:390-405` |
| `FindHost` sobe até o `Border` dono (chanfro/raio) | `Glass.cs:407-412` |

- O `Border` deixou de pintar retângulo: `Background="{x:Null}"` + `BorderThickness="0"` nas 3 superfícies, e o fill virou `Path` com a **mesma** função de silhueta do Chanfro.
- Como a geometria sai de `Chamfer.Octagon` e o aro sai da mesma função com inset 0,5, o desvio fill↔aro é **0 por construção** (o gate de ≤0,5 px não depende de medição: é a mesma curva).
- Pixels fora da silhueta: a geometria do fill é fechada e o Path **não tem** `Effect`/sombra própria; `ClipToBounds` do `MediaPanel` segue ligado.
- `Stretch="Fill"` + geometria em px reais → escala 1:1. (Com `Stretch="None"` o Path mediria a própria geometria nula no 1º passe e ficaria 0×0 — foi trocado de propósito; ver "Decisões".)

### Superfícies alvo

| superfície | chanfro | fill | aro |
|---|---|---|---|
| `MediaPanel` (painel de música) | c=12 | `ThemeMediaGlassBrush` | sim |
| `UndockBtn` (30×30) | **nenhum** (T3) | `AccentBrushFaint` → `ThemeFieldGlassBrush` c/ vidro ON | arredondado r=8 |
| `PanelCard` (Configurações) | c=16 | `ThemePanelSolidBrush` | sim |

## T4 — Acento sai de cena com o vidro ligado (R13/R14)

| item | onde |
|---|---|
| `ChamferAdorner.OnRender` retorna cedo se `IsRingActive` | `Chamfer.cs:91-96` |
| `Chamfer.Refresh` (re-render ao trocar o modo) | `Chamfer.cs:73-74` (Pen do traço cacheado em `81-83`) |
| `Glass.Refresh` rechama Chamfer nos Borders rastreados | `Glass.cs:96` |
| `DimWhenGlass` colapsa o véu de acento (SurfaceTint) | `Glass.cs:418-441` + `SettingsPanel.xaml:265-266` |

- Contorno de acento do `Border` (1,2 px) e o traço de acento do `UndockBtn` (1 px) **desaparecem** com o vidro ligado; quem contorna é o aro neutro.
- Com vidro **desligado** o traço volta idêntico ao aprovado: `FallbackStroke` pinta 1 px com inset 0,5 → a mesma hairline que o Border pintava (`SettingsPanel.xaml:262`, `MainWindow.xaml:421-426`, `MainWindow.xaml:500-502`).
- Face do cartão de Configurações: o `Rectangle` de `SurfaceTintBrush` (acento) é **colapsado** com vidro ON → a face fica só com `ThemePanelSolidBrush` (neutro). Medida esperada: Δ canais ≤ 4/255.

## T5 — Toggle + modo efetivo

| item | onde |
|---|---|
| `AppSettings.EnableGlass` (default `false` → JSON `enableGlass`) | `SettingsService.cs:78-80` |
| checkbox em Aparência → Material | `SettingsPanel.xaml:331-339` |
| carga do estado | `SettingsPanel.xaml.cs:61-62` |
| handler | `SettingsPanel.xaml.cs:194-200` |
| texto do modo efetivo (Off/vector/shader) | `SettingsPanel.xaml.cs:203-210` |
| re-texto quando o perfil de efeitos muda | `SettingsPanel.xaml.cs:218` |
| aplicação no MainWindow | `MainWindow.xaml.cs:1253-1256` |

- Default **OFF**: quem não mexer no toggle continua vendo o v9.8.2 aprovado.
- `OptNone` ("sem animações") trava o modo em `vector` — a regra nasce no `Resolve` (`Glass.cs:52-53`), não no XAML, então o F2 não consegue violá-la sem editar o resolvedor.
- Alternar em runtime: `NotifyChanged` → `OnSettingsChanged` (`MainWindow.xaml.cs:713`) → `Glass.Apply` → `Refresh` → aro/fill/traço/veu se reposicionam sem recriar árvore visual.

## T6 — Frame tech sem bracket órfão (R11)

| item | onde |
|---|---|
| braços começam em `inner = c − off` | `TechFrame.cs:131-152` |
| rails começam/terminam em `inner + arm` | `TechFrame.cs:153-166` |
| zero alocação (pens/brushes privados + guarda de cor) | `TechFrame.cs:93-130` |

- Antes: `Rect(-off, …)` e `Rect(w + off - arm, …)` → o braço entrava no **vazio** do canto cortado (bracket flutuando sobre nada). Agora o braço nasce depois do chanfro e o conjunto fica **dentro** da silhueta.
- Nó de status (via/LED) fica **fora** da silhueta de propósito: é identidade tech, não bracket — e por estar fora não toca a face do vidro.
- `OnRender` não aloca mais: `Pen`/`SolidColorBrush` são campos; `Color` só é reescrito quando muda. **Não congelados de propósito** — o brush do acento é o mesmo objeto vivo do `LivePalette`; congelar aqui selaria a troca de acento in-place (regressão).

---

## Gates

| gate | status | como foi checado |
|---|---|---|
| `dotnet build` verde | **PASS** | `Compilação com êxito`, 0 erros |
| 0 warnings novos | **PASS** | 2 únicas, as mesmas do baseline |
| fill ≤ 0,5 px da silhueta | **PASS (por construção)** | fill e aro chamam a mesma `Chamfer.Octagon`; aro com inset 0,5 |
| 0 pixel fora da silhueta | **PASS (por construção)** | geometria fechada, sem sombra própria, traço com inset 0,5 |
| face neutra ≤ +4/255 | **PASS (por construção)** | face = `ThemePanelSolidBrush`; tint de acento colapsado com vidro ON |
| 0 alocação/frame no `OnRender` | **PASS (leitura de código)** | `Glass.cs:161-193`, `Chamfer.cs:91-106`, `TechFrame.cs:106-171`: nenhum `new` no caminho quente |
| sem bracket órfão | **PASS** | braços/rails internos (`TechFrame.cs:131-166`) |
| sem aro órfão | **PASS** | `ChamferAdorner` sai de cena; `RingAdorner` cobre a mesma silhueta |
| R13: sem acento na face | **PASS** | tokens brancos + SurfaceTint colapsado |
| R14: sem acento no aro | **PASS** | edge/faceta neutros; specular 0x33 só na faceta UL |
| sem glow (V3) / véu (V4) / sheen | **PASS (por construção)** | 3 traços, sem `BlurEffect`, sem gradiente, sem camada extra |
| specular 0x33 exato, luz −24° | **PASS** | `AccentBrushFaint` + só a faceta superior-esquerda |

**O que NÃO foi medido:** os gates de pixel (≤0,5 px, Δ fora da silhueta, Δ canais ≤4) foram provados **por construção e por leitura de código**, não por diff de screenshot — nenhuma janela foi aberta nesta sessão. Para fechar em medição real:

1. rodar o app → **Configurações → Aparência → Material → "Aro de cristal"**;
2. abrir o painel de música (botão de música no painel central);
3. screenshot 100% (sem DPI scale) do cartão de Configurações e do painel de música;
4. medir: alpha do pixel do aro (esperado 0x19/255 no meio do lado), ausência de pixel fora do octógono, e a face do cartão sem componente de acento;
5. conferir que **nada** de acento sobrou no contorno (o traço de 1,2 px tem de sumir).

---

## Arquivos tocados

- `Glass.cs` — **novo**: modo, registro fraco, aro, face, véu.
- `LivePalette.cs` — 2 tokens + `CopyFrom` + `Lerp` + 2 brushes + defaults.
- `App.xaml` — `GlassEdgeBrush`, `GlassFacetBrush`.
- `Chamfer.cs` — `Refresh`, early-return com vidro ativo, comentário do fill.
- `TechFrame.cs` — R11 dos braços + `OnRender` zero-alocação.
- `SettingsService.cs` — `EnableGlass`.
- `SettingsPanel.xaml` / `.xaml.cs` — toggle + texto do modo + card com fill Path + véu colapsável.
- `MainWindow.xaml` / `.xaml.cs` — `MediaPanel`, `UndockBtn`, `Glass.Apply` no `ApplyEffectsProfile`.

Não tocados (de propósito): `TASK.md`, `_refs/`, `RELATORIO_v*.md`, `versao anterior/`, qualquer `.exe` final.

## Decisões e porquês

1. **`UndockBtn` sem chanfro novo.** 30×30 < 34 px → T3 proíbe canto 2×. Fica com a silhueta arredondada nativa (r=8) e o aro arredondado; o fill é o Path arredondado, não octógono.
2. **Variante R11 "braços em `x = c − off`".** Preserva os 4 cantos tech; a variante "sem bracket" foi descartada por apagar identidade. `off` continua 3.
3. **`Stretch="Fill"` no face Path.** Com `None`, `DesiredSize` = bounds da geometria (nula no 1º passe) → o Path nunca sairia de 0×0 e a face nunca apareceria.
4. **`FallbackStroke`.** Sem ele, `BorderThickness="0"` apagaria a hairline aprovada com o vidro **desligado**. Ele pinta 1 px com inset 0,5 só quando o modo é `Off`.
5. **Specular = `AccentBrushFaint`.** Sem token novo: o alpha 0x33 já existe nesse brush e ele já transiciona com o acento.
6. **LED do techno-frame fora da silhueta.** É via de status, não bracket; e mantê-lo fora evita acento sobre a face do vidro (R13).
