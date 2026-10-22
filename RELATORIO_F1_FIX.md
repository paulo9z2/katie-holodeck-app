# RELATÓRIO F1-FIX — fechamento dos 2 P1s do REVIEW_F1

**Escopo:** corrigir EXATAMENTE os dois P1s do `REVIEW_F1.md`, sem tocar no que já estava aprovado.
**Arquivos alterados:** `Chamfer.cs` (P1-1) e `Glass.cs` (P1-2). Nenhum outro arquivo foi editado.
**Build:** `dotnet build -v q -nologo` → **0 erros**, **4 avisos** (as 2 warnings de baseline, duplicadas pelas 2 passagens de compilação do WPF).

---

## P1-1 — alocação por render no `ChamferAdorner`

**Problema:** `ChamferAdorner.OnRender` chamava `Octagon(...)` a cada render; o helper aloca
`PathGeometry`, `PathFigure` e 8 `LineSegment` por frame.

**Correção:** a geometria passou a ser campo privado do adorner, reconstruída **somente** quando
largura, altura ou chanfro mudam (mesmo padrão de cache de `Glass.cs` `Build`).

### Antes — `Chamfer.cs:81-83` (campos)

```csharp
            private readonly Pen _stroke = new() { Thickness = 1.2 };
            private Brush? _strokeBrush;
            private double _strokeThickness = 1.2;
```

### Depois — `Chamfer.cs:81-87`

```csharp
            private readonly Pen _stroke = new() { Thickness = 1.2 };
            private Brush? _strokeBrush;
            private double _strokeThickness = 1.2;
            // F1-FIX (P1-1): a geometria vive no adorner e só é reconstruída quando
            // largura/altura/chanfro mudam — mesmo padrão de cache do Glass.cs Build.
            private Geometry? _geometry;
            private double _geoW = double.NaN, _geoH = double.NaN, _geoC = double.NaN;
```

### Antes — `Chamfer.cs:105` (render)

```csharp
                dc.DrawGeometry(null, _stroke, Octagon(el.ActualWidth, el.ActualHeight, c));
```

### Depois — `Chamfer.cs:110-117`

```csharp
                // Zero alocação por frame: só reconstrói quando o tamanho ou o chanfro muda.
                double w = el.ActualWidth, h = el.ActualHeight;
                if (_geometry == null || w != _geoW || h != _geoH || c != _geoC)
                {
                    _geometry = Octagon(w, h, c);
                    _geoW = w; _geoH = h; _geoC = c;
                }
                dc.DrawGeometry(null, _stroke, _geometry);
```

**Resultado:** caminho quente do `OnRender` não tem mais `new`. As invalidações da transição de
tema re-desenham a geometria armazenada; reconstrução só ocorre em `SizeChanged`/mudança de
`Chamfer.Size` (que já chamam `Chamfer.Apply → InvalidateVisual`). O `_stroke` e o brush vivo
continuam re-apontados por referência, como antes.

---

## P1-2 — faceta arredondada fora da silhueta (`BuildRounded`)

**Problema:** a linha de centro tinha raio `R = rr − 0,75`, mas os extremos dos arcos usavam
`EdgeInset = 0,5`. Esses pontos ficam a `rr − 0,5` do centro do canto, **fora** do círculo de
raio `R` → o `ArcSegment` era deslocado e a borda externa do traço de 1,5 px podia vazar até
0,25 px no `UndockBtn` 30×30 r=8.

**Correção:** extremos da linha de centro com inset `FacetWidth / 2 = 0,75`, mantendo cada arco
**concêntrico** ao canto original. O specular usa o mesmo centro/raio e o mesmo extremo.

### Antes — `Glass.cs:278-292`

```csharp
                double R = rr - FacetWidth * 0.5;   // raio da linha de centro da banda
                if (R <= 0.5) return;

                var facet = new PathGeometry();
                AddArc(facet, R, new Point(rr, EdgeInset), new Point(EdgeInset, rr), SweepDirection.Counterclockwise);                       // sup-esq
                AddArc(facet, R, new Point(w - rr, EdgeInset), new Point(w - EdgeInset, rr), SweepDirection.Clockwise);                        // sup-dir
                AddArc(facet, R, new Point(w - EdgeInset, h - rr), new Point(w - rr, h - EdgeInset), SweepDirection.Clockwise);                // inf-dir
                AddArc(facet, R, new Point(rr, h - EdgeInset), new Point(EdgeInset, h - rr), SweepDirection.Clockwise);                        // inf-esq
                _facetGeo = facet;

                // ---- specular: a fatia de cima do arco sup-esquerdo (luz −24°) ----
                double ang = (270.0 - SpecularSpan * 90.0) * Math.PI / 180.0;   // y para baixo
                var end = new Point(rr + R * Math.Cos(ang), rr + R * Math.Sin(ang));
                var spec = new PathGeometry();
                AddArc(spec, R, new Point(rr, EdgeInset), end, SweepDirection.Counterclockwise);
                _specGeo = spec;
```

### Depois — `Glass.cs:278-300`

```csharp
                double R = rr - FacetWidth * 0.5;   // raio da linha de centro da banda
                if (R <= 0.5) return;

                // F1-FIX (P1-2): a linha de centro é CONCÊNTRICA ao canto arredondado
                // (centros em (rr,rr), (w-rr,rr), (w-rr,h-rr), (rr,h-rr)) e os extremos
                // ficam a FacetWidth/2 (0,75) da borda — não mais em EdgeInset (0,5).
                // Assim R + FacetWidth/2 = rr: a borda externa do traço coincide com a
                // silhueta e não vaza nenhum pixel (antes vazava até 0,25 no canto).
                const double K = FacetWidth * 0.5;  // 0,75 — inset dos extremos da linha de centro
                var facet = new PathGeometry();
                AddArc(facet, R, new Point(rr, K), new Point(K, rr), SweepDirection.Counterclockwise);                       // sup-esq
                AddArc(facet, R, new Point(w - rr, K), new Point(w - K, rr), SweepDirection.Clockwise);                      // sup-dir
                AddArc(facet, R, new Point(w - K, h - rr), new Point(w - rr, h - K), SweepDirection.Clockwise);              // inf-dir
                AddArc(facet, R, new Point(rr, h - K), new Point(K, h - rr), SweepDirection.Clockwise);                      // inf-esq
                _facetGeo = facet;

                // ---- specular: a fatia de cima do arco sup-esquerdo (luz −24°) ----
                // Mesmo centro/raio do arco da faceta → flush e 100% dentro da silhueta.
                double ang = (270.0 - SpecularSpan * 90.0) * Math.PI / 180.0;   // y para baixo
                var end = new Point(rr + R * Math.Cos(ang), rr + R * Math.Sin(ang));
                var spec = new PathGeometry();
                AddArc(spec, R, new Point(rr, K), end, SweepDirection.Counterclockwise);
                _specGeo = spec;
```

### Prova geométrica (UndockBtn 30×30, r = 8)

| item | antes | depois |
|---|---|---|
| raio da linha de centro | `R = 8 − 0,75 = 7,25` | `R = 8 − 0,75 = 7,25` (inalterado) |
| extremo do arco sup-esq | `(8, 0,5)` / `(0,5, 8)` | `(8, 0,75)` / `(0,75, 8)` |
| distância dos extremos ao centro do canto | `rr − 0,5 = 7,5` (≠ R) | `rr − 0,75 = 7,25` (= R) |
| borda externa do traço (centro + 0,75) | 7,5 + 0,75 = `8,25` → **0,25 px fora** | 7,25 + 0,75 = `8,0` → **0 px fora** |

Os quatro arcos mantêm 90° e o centro correto de cada canto (`(rr,rr)`, `(w−rr,rr)`,
`(w−rr,h−rr)`, `(rr,h−rr)`); só os extremos e a concentricidade mudaram. O `aro` (1 px, inset 0,5)
continua intocado nos lados retos e casa com a faceta no início de cada canto sem fresta.
O specular é a mesma circunferência (mesmo `R`, mesmo centro) começando em `(rr, 0,75)` — flush
com a faceta e dentro da silhueta.

---

## Build (gate)

Comando exato: `dotnet build -v q -nologo`

```
MainWindow.xaml.cs(233,78): warning CS8625: Não é possível converter um literal nulo em um tipo de referência não anulável. [KatieHoloDeck_1t5wxecc_wpftmp.csproj]
MainWindow.xaml.cs(1867,51): warning CS8604: Possível argumento de referência nula para o parâmetro 'keepFrame' em 'void MainWindow.SetBackdropBlur(double to, FrameworkElement keepFrame = null)'. [KatieHoloDeck_1t5wxecc_wpftmp.csproj]
MainWindow.xaml.cs(233,78): warning CS8625: ... [KatieHoloDeck.csproj]
MainWindow.xaml.cs(1867,51): warning CS8604: ... [KatieHoloDeck.csproj]

Compilação com êxito.

    4 Aviso(s)
    0 Erro(s)
```

As 4 linhas são as **2 warnings de baseline** (`MainWindow.xaml.cs:233` CS8625 e
`MainWindow.xaml.cs:1867` CS8604) repetidas nas duas passagens de compilação do WPF temporário —
nenhum aviso novo em `Chamfer.cs` ou `Glass.cs`.

---

## O que NÃO foi tocado

- `REVIEW_F1.md`, `TASK.md`, `TASK_F1.md`, `RELATORIO_F1.md`, `RELATORIO_F1_PAINT.md`, `_refs_design/`
  (e demais `_refs/`, `versao anterior/`, `final .exe/`).
- `LivePalette.cs`, `SettingsService.cs`, `App.xaml`, `MainWindow.xaml`, `SettingsPanel.xaml`,
  `TechFrame.cs` — nenhuma edição nesta correção.
- Tokens (`ThemeGlassEdge` 0x19, `ThemeGlassFacet` 0x1F), alphas, `EdgeInset`, `FacetWidth`,
  `SpecularSpan` e os Pens permanecem exatamente como aprovados.
