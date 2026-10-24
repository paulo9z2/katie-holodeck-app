meFullscreenOpaqueBrush", WithA(t.FullscreenOpaque, 0xF5));
 405: 
 406:             Grad(ref _islandGlass, "IslandGlass", g =>
 407:             {
 408:                 g.GradientStops[0].Color = WithA(t.Island0, 0x66);
 409:                 g.GradientStops[1].Color = WithA(t.Island1, 0x4D);
 410:             });
 411:             Grad(ref _panelGlassStrong, "PanelGlassStrong", g =>
 412:             {
 413:                 g.GradientStops[0].Color = WithA(t.PanelStrong0, 0xF2);
 414:                 g.GradientStops[1].Color = WithA(t.PanelStrong1, 0xE6);
 415:             });
 416:             Solid(ref _scrim, "ScrimBrush", WithA(t.Scrim, 0xF5));
 417: 
 418:             // F1/F2.6 â€” aro de cristal: tokens opacos + alpha na montagem (Mix descarta alpha).
 419:             // F2.6: 0x19â†’0x4D (30,2%) e 0x1Fâ†’0x57 (34,1%) â€” o aro 10% nÃ£o sobrevivia ao AA
 420:             // de 1 px e o dono nÃ£o o via (medido 1,57Ã— o fundo; gate exige 2Ã—).
 421:             Solid(ref _glassEdge, "GlassEdgeBrush", WithA(t.ThemeGlassEdge, 0x4D));   // 30,2%
 422:             Solid(ref _glassFacet, "GlassFacetBrush", WithA(t.ThemeGlassFacet, 0x57)); // 34,1% â€” faceta 45Â°
 423: 
 424:             Application.Current.Resources["ThemeSecondaryColor"] = Opaque(t.Secondary);
 425:         }
 426: 
 427:         private static ThemeTokens ReadDefaultTheme(ResourceDictionary res) => new()
 428:         {
 429:             Secondary = Rgb(ColorOf(res, "ThemeSecondaryColor", Color.FromRgb(0x00, 0xFF, 0x88))),
 430:             TextStrong = Rgb(Solid(res, "ThemeTextStrongBrush", Color.FromRgb(0xF3, 0xF4, 0xF4))),
 431:             Text = Rgb(Solid(res, "ThemeTextBrush", Color.FromRgb(0xB3, 0xBB, 0xBD))),
 432:             Label = Rgb(Solid(res, "ThemeLabelBrush", Color.FromRgb(0x8C, 0x99, 0x9B))),
 433:             TextDim = Rgb(Solid(res, "ThemeTextDimBrush", Color.FromRgb(0x55, 0x60, 0x62))),
 434:             GridLine = Rgb(Pen(res, "ThemeGridBrush", Color.FromRgb(0x0F, 0x38, 0x4B))),
 435:             ScanLine = Rgb(Pen(res, "ThemeScanBrush", Color.FromRgb(0x0E, 0x32, 0x47))),
 436:             Shell0 = Rgb(Stop(res, "ThemeShellBrush", 0, Color.FromRgb(0x0D, 0x2E, 0x43))),
 437:             Shell1 = Rgb(Stop(res, "ThemeShellBrush", 1, Color.FromRgb(0x09, 0x20, 0x30))),
 438:             Shell2 = Rgb(Stop(res, "ThemeShellBrush", 2, Color.FromRgb(0x07, 0x17, 0x23))),
 439:             VignetteEdge = Rgb(Stop(res, "ThemeVignetteBrush", 1, Color.FromRgb(0x02, 0x15, 0x1E))),
 440:             MediaGlass = Rgb(Solid(res, "ThemeMediaGlassBrush", Color.FromRgb(0x0E, 0x1B, 0x2E))),
 441:             FieldGlass = Rgb(Solid(res, "ThemeFieldGlassBrush", Color.FromRgb(0x0B, 0x1E, 0x38))),
 442:             BarTrack = Rgb(Solid(res, "ThemeBarTrackBrush", Color.FromRgb(0x00, 0xFF, 0x88))),
 443:             CoverPlaceholder = Rgb(Solid(res, "ThemeCoverPlaceholderBrush", Color.FromRgb(0x1E, 0x4E, 0x8C))),
 444:             ChipGlass = Rgb(Solid(res, "ThemeChipGlassBrush", Color.FromRgb(0x00, 0x0C, 0x14))),
 445:             ChatExpanded = Rgb(Solid(res, "ThemeChatExpandedBrush", Color.FromRgb(0x0E, 0x1B, 0x2E))),
 446:             PanelSolid = Rgb(Solid(res, "ThemePanelSolidBrush", Color.FromRgb(0x07, 0x0C, 0x14))),
 447:             Island0 = Rgb(Stop(res, "IslandGlass", 0, Color.FromRgb(0x0E, 0x1B, 0x2E))),
 448:             Island1 = Rgb(Stop(res, "IslandGlass", 1, Color.FromRgb(0x0E, 0x1B, 0x2E))),
 449:             PanelStrong0 = Rgb(Stop(res, "PanelGlassStrong", 0, Color.FromRgb(0x0E, 0x1B, 0x2E))),
 450:             PanelStrong1 = Rgb(Stop(res, "PanelGlassStrong", 1, Color.FromRgb(0x0E, 0x1B, 0x2E))),
 451:             Scrim = Rgb(Solid(res, "ScrimBrush", Color.FromRgb(0x06, 0x0A, 0x12))),
 452:             Fullscreen = Rgb(Solid(res, "ThemeFullscreenBrush", Color.FromRgb(0x0A, 0x13, 0x22))),
 453:             FullscreenOpaque = Rgb(Solid(res, "ThemeFullscreenOpaqueBrush", Color.FromRgb(0x0A, 0x13, 0x22))),
 454:             ThemeGlassEdge = Rgb(Solid(res, "GlassEdgeBrush", Colors.White)),
 455:             ThemeGlassFacet = Rgb(Solid(res, "GlassFacetBrush", Colors.White))
 456:         };
 457: 
 458:         private static Color ColorOf(ResourceDictionary res, string key, Color fallback) =>
 459:             res[key] is Color c ? c : fallback;
 460: 
--- Glass.cs
 150:         }
 151: 
 152:         private sealed class RingAdorner : Adorner
 153:         {
 154:             // Pens PRIVADOS (nÃ£o vÃ£o pra dicionÃ¡rio nenhum): podem ser reusados e reapontados
 155:             // a cada render sem alocar. NÃ£o sÃ£o congelados de propÃ³sito â€” o brush vivo do
 156:             // acento Ã© compartilhado e o LivePalette continua dono da mutaÃ§Ã£o.
 157:             // ===== MEDIDAS DO MATERIAL (T2) â€” o sir fixou: aro 1 px Â· faceta 1,5 px =====
 158:             private const double EdgeInset = 0.5;    // aro de 1 px 100% DENTRO da silhueta
 159:             private const double FacetWidth = 1.5;    // "faceta 45Â° dura 1-3px"
 160:             private const double SpecularSpan = 0.62; // fatia da faceta UL que pega a luz da casa (âˆ’24Â°)
 161:             private const double Sqrt2 = 0.7071067811865476;
 162: 
 163:             private readonly Pen _edge = new() { Thickness = 1.0, StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
 164:             private readonly Pen _facet = new() { Thickness = FacetWidth, StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
 165:             private readonly Pen _spec = new() { Thickness = FacetWidth, StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
 166: 
 167:             private Geometry? _edgeGeo, _facetGeo, _specGeo;
 168:             private double _w = double.NaN, _h = double.NaN, _c = double.NaN, _r = double.NaN;
 169: 
 170:             public RingAdorner(FrameworkElement el) : base(el) { IsHitTestVisible = false; }
 310:             }
 311: 
 312:             private static void AddArc(PathGeometry g, double r, Point from, Point to, SweepDirection sweep)
 313:             {
 314:                 var f = new PathFigure { StartPoint = from, IsClosed = false, IsFilled = false };
 315:                 f.Segments.Add(new ArcSegment(to, new Size(r, r), 0, false, sweep, true));
 316:                 g.Figures.Add(f);
 317:             }
 318:         }
 319: 
 320:         // =====================================================================
 321:         // FACE â€” fill octogonal real (Path), no lugar do retÃ¢ngulo do Border
 322:         // =====================================================================
 323: 
 324:         public static readonly DependencyProperty FaceProperty =
 325:             DependencyProperty.RegisterAttached("Face", typeof(string), typeof(Glass),
 326:                 new FrameworkPropertyMetadata("", OnFaceChanged));
 327: 
 328:         /// <summary>Chave de recurso do fill (ex.: "ThemeMediaGlassBrush"). Sempre aplicada.</summary>
 329:         public static string GetFace(DependencyObject o) => (string?)o.GetValue(FaceProperty) ?? "";
 330:         public static void SetFace(DependencyObject o, string v) => o.SetValue(FaceProperty, v);
 331: 
 332:         public static readonly DependencyProperty FaceFrostedProperty =
 333:             DependencyProperty.RegisterAttached("FaceFrosted", typeof(string), typeof(Glass),
 334:                 new FrameworkPropertyMetadata("", OnFaceChanged));
 335: 
 336:         /// <summary>Chave alternativa quando o vidro estÃ¡ LIGADO (face neutra; R13).</summary>
 337:         public static string GetFaceFrosted(DependencyObject o) => (string?)o.GetValue(FaceFrostedProperty) ?? "";
 338:         public static void SetFaceFrosted(DependencyObject o, string v) => o.SetValue(FaceFrostedProperty, v);
 339: 
 340:         public static readonly DependencyProperty FallbackStrokeProperty =
 341:             DependencyProperty.RegisterAttached("FallbackStroke", typeof(string), typeof(Glass),
 342:                 new FrameworkPropertyMetadata("", OnFaceChanged));
 343: 
 344:         /// <summary>
 345:         /// Chave de traÃ§o usada SÃ“ com o vidro desligado â€” Ã© o contorno atual de controles
 346:         /// pequenos (UndockBtn, 30 px, sem Chamfer) que nÃ£o tÃªm aro para substituÃ­-lo.
 347:         /// </summary>
 348:         public static string GetFallbackStroke(DependencyObject o) => (string?)o.GetValue(FallbackStrokeProperty) ?? "";
 349:         public static void SetFallbackStroke(DependencyObject o, string v) => o.SetValue(FallbackStrokeProperty, v);
 350: 
 351:         private sealed class FaceState
 352:         {
 353:             public double W, H, C, R, Inset;
 354:             public string FillKey = "", StrokeKey = "";
 355:             public bool Has;
 356:         }
 357: 
 358:         private static readonly ConditionalWeakTable<Path, FaceState> _faces = new();
 359: 
 360:         private static void OnFaceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
 361:         {
 362:             if (o is not Path p) return;
 363:             p.Loaded -= FaceLoaded;
 364:             p.Loaded += FaceLoaded;
 365:             p.SizeChanged -= FaceSizeChanged;
 366:             p.SizeChanged += FaceSizeChanged;
 367:             Track(p);
 368:             ApplyFace(p);
 369:         }
 370: 
 371:         private static void FaceLoaded(object sender, RoutedEventArgs e) => ApplyFace(sender as Path);
 372:         private static void FaceSizeChanged(object sender, SizeChangedEventArgs e) => ApplyFace(sender as Path);
 373: 
 374:         private static void ApplyFace(Path? p)
 375:         {
 376:             if (p == null) return;
 377:             double w = p.ActualWidth, h = p.ActualHeight;
 378:             // F2.7-fix (bootstrap da face): Path em sangria, Stretch=Fill e Data=null
 379:             // mede 0x0 e o Shape arranja Size.Empty (nÃ£o hÃ¡ geometria esticÃ¡vel), entÃ£o
 380:             // ActualWidth nunca saÃ­a de zero e esta guarda nunca deixava construir a
 381:             // geometria â€” a face jamais pintava em NENHUM host. O Path cobre exatamente
 382:             // a Ã¡rea do host, entÃ£o herda o tamanho dele no primeiro passe; assim que
 383:             // Data existe, o prÃ³ximo SizeChanged jÃ¡ reaplica com o tamanho real.
 384:             var hostEl = FindHost(p) as FrameworkElement;
 385:             if ((w < 4 || h < 4) && hostEl is { ActualWidth: >= 4, ActualHeight: >= 4 })
 386:             {
 387:                 w = hostEl.ActualWidth; h = hostEl.ActualHeight;
 388:             }
 389:             if (w < 4 || h < 4) return;
 390: 
 391:             bool glass = _mode != GlassMode.Off;
 392:             string fillKey = glass ? GetFaceFrosted(p) : "";
 393:             if (fillKey.Length == 0) fillKey = GetFace(p);
 394:             if (fillKey.Length == 0) return;
 395:             string strokeKey = glass ? "" : GetFallbackStroke(p);
 396: 
 397:             double c = 0, r = 0;
 398:             if (FindHost(p) is Border host) { c = Chamfer.GetSize(host); r = host.CornerRadius.TopLeft; }
 399:             double inset = strokeKey.Length > 0 ? 0.5 : 0;
 400: 
 401:             var st = _faces.GetOrCreateValue(p);
 402:             if (!st.Has || st.W != w || st.H != h || st.C != c || st.R != r || st.Inset != inset)
 403:             {
 404:                 p.Data = BuildFace(w, h, c, r, inset);
 405:                 st.W = w; st.H = h; st.C = c; st.R = r; st.Inset = inset; st.Has = true;
 406:             }
 407:             if (st.FillKey != fillKey) { p.SetResourceReference(Shape.FillProperty, fillKey); st.FillKey = fillKey; }
 408:             if (st.StrokeKey != strokeKey)
 409:             {
 410:                 if (strokeKey.Length > 0) { p.StrokeThickness = 1; p.SetResourceReference(Shape.StrokeProperty, strokeKey); }
 411:                 else p.ClearValue(Shape.StrokeProperty);
 412:                 st.StrokeKey = strokeKey;
 413:             }
 414:         }
 415: 
 416:         /// <summary>OctÃ³gono do Chamfer quando a superfÃ­cie tem chanfro; senÃ£o o arredondado do Border.</summary>
 417:         private static Geometry BuildFace(double w, double h, double c, double r, double inset)
 418:         {
 419:             if (c > 0 && w - 2 * inset > 2 * c && h - 2 * inset > 2 * c)
 420:             {
 421:                 var g = Chamfer.Octagon(w - 2 * inset, h - 2 * inset, c);
 422:                 if (inset != 0) g.Transform = new TranslateTransform(inset, inset);
 423:                 return g;
 424:             }
 425: 
 426:             // Controle pequeno (T3/chanfro proibido): mantÃ©m a silhueta arredondada nativa.
 427:             double x0 = inset, y0 = inset, ww = w - 2 * inset, hh = h - 2 * inset;
 428:             double rr = Math.Max(0, Math.Min(r, Math.Min(ww, hh) * 0.5));
 429:             var f = new PathFigure { StartPoint = new Point(x0 + rr, y0), IsClosed = true, IsFilled = true };
 430:             f.Segments.Add(new LineSegment(new Point(x0 + ww - rr, y0), true));
 431:             f.Segments.Add(new ArcSegment(new Point(x0 + ww, y0 + rr), new Size(rr, rr), 0, false, SweepDirection.Clockwise, true));
 432:             f.Segments.Add(new LineSegment(new Point(x0 + ww, y0 + hh - rr), true));
 433:             f.Segments.Add(new ArcSegment(new Point(x0 + ww - rr, y0 + hh), new Size(rr, rr), 0, false, SweepDirection.Clockwise, true));
 434:             f.Segments.Add(new LineSegment(new Point(x0 + rr, y0 + hh), true));
 435:             f.Segments.Add(new ArcSegment(new Point(x0, y0 + hh - rr), new Size(rr, rr), 0, false, SweepDirection.Clockwise, true));
 436:             f.Segments.Add(new LineSegment(new Point(x0, y0 + rr), true));
 437:             f.Segments.Add(new ArcSegment(new Point(x0 + rr, y0), new Size(rr, rr), 0, false, SweepDirection.Clockwise, true));
 438:             var gg = new PathGeometry();
 439:             gg.Figures.Add(f);
 440:             return gg;
 441:         }
 442: 
 443:         private static Border? FindHost(DependencyObject? node)
 444:         {
 445:             while (node != null && node is not Border)

web search: 
web search: site:learn.microsoft.com WPF SystemParameters ClientAreaAnimation property ...
2026-10-23T03:53:57.735903Z ERROR rmcp::transport::worker: worker quit with fatal: Transport channel closed, when Client(HttpRequest(HttpRequest("error decoding response body")))
2026-10-23T03:53:58.268491Z ERROR rmcp::transport::worker: worker quit with fatal: Transport channel closed, when Client(HttpRequest(HttpRequest("error decoding response body")))
2026-10-23T03:53:59.619519Z ERROR rmcp::transport::worker: worker quit with fatal: Transport channel closed, when Client(HttpRequest(HttpRequest("error decoding response body")))
codex
# Veredito: **REJEITADO — rodada 1**

A sequência de trabalho faz sentido, e a correção do `MediaPanel` aponta para o evento certo. Mas há lacunas que impedem aprovar o plano sob o contrato atual, principalmente na paridade OFF, no mapa de tokens e no fallback de acessibilidade.

## Achados

1. **P1 — §5, linhas 19 e 30; §5.1: inventário incompleto de barras de rolagem.** A tabela inclui a scrollbar do chat, mas não as barras visíveis do `ScrollViewer` de Configurações (`SettingsPanel.xaml:299`) nem a do `ScrollViewer` dentro do popup do ComboBox (`FluentControls.xaml:760–765`). O `MessageBox` nativo está coberto na linha 32; não encontrei outro diálogo próprio. A categoria de `ToolTip` parece cobrir os tooltips existentes. A contagem de 35 precisa explicar se cada linha representa uma superfície ou um agrupamento de controles.

2. **P1 — §F3.3, “Intensidade” e “Cor e extras”; §8: mapa de recursos incompleto e defaults sem fórmula.** O multiplicador de força lista `IslandGlass`, `ThemeMediaGlassBrush` e `ThemeChipGlassBrush`, mas omite `ThemeFieldGlassBrush`, usado como `FaceFrosted` no botão de desacoplar. `ThemeChatExpandedDenseBrush` também é uma face real, e `PanelGlassStrong` está declarado como vidro em `App.xaml`, mas não é classificado como ajustável, excluído ou recurso sem uso. Além disso, `GlassTintColor=#0E1B2E` não corresponde às cores base de todos os fills — por exemplo, chip e field — nem explica como preserva a paleta dinâmica por capa. O plano reconhece `GlassTintColor` como novo; falta definir sua fórmula e provar que o default reproduz cada face atual.

3. **P1 — §F3.3, plumbing e G-F3.3-5/6: a paridade OFF não está garantida pelo desenho.** O plano propõe alterar brushes de face compartilhados. Hoje, quando o vidro está OFF, `Glass.ApplyFace` usa `Face`; quando está ON, usa `FaceFrosted` se houver. Assim, um brush alterado por um slider pode continuar afetando o fallback OFF. Não está descrito como separar valores efetivos de vidro dos fills de fallback, nem como restaurar os valores originais ao desligar. Também falta definir se o hash compara só as superfícies do app ou inclui a tela de Configurações, que mudou. Ver [Glass.cs](/C:/Users/9z2.pj/Downloads/katie%20df%20aplicativo/codigo%20fonte/Glass.cs:391).

4. **P1 — §F4.3: detecção de “Reduzir transparência” incorreta e fallback opaco não demonstrado.** `SystemParameters.ClientAreaAnimation` representa a preferência por animações da área cliente, não a configuração de transparência; a API documentada para consultar efeitos de transparência do Windows é `UISettings.AdvancedEffectsEnabled`. [Microsoft Learn: `ClientAreaAnimation`](https://learn.microsoft.com/en-us/dotnet/api/system.windows.systemparameters.clientareaanimation?view=windowsdesktop-10.0), [Microsoft Learn: `AdvancedEffectsEnabled`](https://learn.microsoft.com/en-us/uwp/api/windows.ui.viewmanagement.uisettings.advancedeffectsenabled?view=winrt-28000). Também não basta chamar `Glass.Apply(..., false)` para garantir “zero superfície translúcida”: o caminho OFF ainda usa fills como `IslandGlass` e `ThemeMediaGlassBrush`, que têm alpha parcial. Alto contraste e redução de transparência precisam de um caminho opaco explícito, separado do OFF visual atual.

5. **P2 — §F0.1: a correção proposta é adequada, mas o diagnóstico e o gate devem ser ajustados.** Assinar `SizeChanged` do host e tentar novamente após `Loaded` deve cobrir o caso em que o `Path` começa sem tamanho útil. Porém, o código atual retorna antes de construir a geometria quando largura ou altura ficam abaixo de 4; nesse caso, `Path.Data` permanece nulo, não é uma geometria 0×0. A guarda de idempotência descrita como mudança nova já existe em [Glass.cs](/C:/Users/9z2.pj/Downloads/katie%20df%20aplicativo%20codigo%20fonte/Glass.cs:401). Faça o gate verificar `Data` não nulo e geometria dimensionada após o host crescer; a amostragem de alpha da captura, isoladamente, não prova isso.

6. **P2 — §§F0.2, F3.3-9, F4.4 e F4.5: números existem, mas parte da medição não é reproduzível.** O plano pede p95 de frame “à mão”, sem definir ferramenta, duração da amostra, definição de frame ou método para medir o acréscimo de 1,2 ms. A instrumentação que poderia ajudar é opcional, então o gate de performance depende de uma medição não especificada. O descarte de HLSL é justificável pelo contrato R3; já a estimativa de 3–8 ms e a frase “80% da leitura visual a 0% do custo” não têm medição ou referência no plano. O ruído também precisa de gate de custo, não apenas de aparência.

7. **P2 — §§6, F4.0–F4.6 e §11: rollback genérico, sem plano por fase como prometido.** O §11 estabelece commits isolados e `git revert`, mas as fases F4.0–F4.6 não detalham como reverter mudanças de estado, recursos ou preferências persistidas. Isso importa especialmente em F3.3, cujo próprio texto reconhece que o plumbing de `LivePalette` não é aditivo. A declaração do §6 de que cada fase tem rollback próprio vai além do que as fases descrevem.

8. **P2 — §§2/R6–R7, 5/linha 1, F4.1 e §§4.5/9: conflito com o padrão F2B e com a contagem de hosts.** A moldura do `AppShell` recebe aro sem face, embora R7 exija `Path` com `Glass.Face` e `Glass.Ring` para hosts de vidro. F4.1 também propõe manter um segundo traço de acento junto ao aro; esclareça como isso respeita R6. Por fim, §9 conta popup e moldura entre os três novos hosts, mas F4.2 diz que o popup não entra no ledger do `Glass`; o total previsto de `Face` e `Ring` precisa fechar com essa decisão.

9. **P3 — §4.2 e plumbing de F3.3: “frozen” não está comprovado.** O código mostra `_glassHighlight` como `readonly`, mas isso, por si só, não prova que o brush está congelado. Não há uma chamada `Freeze()` nesse trecho de [LivePalette.cs](/C:/Users/9z2.pj/Downloads/katie%20df%20aplicativo%20codigo%20fonte/LivePalette.cs:162). O risco de mutação deve depender da verificação de `IsFrozen` prevista no próprio plano, não da conclusão de que o brush já é frozen.

## Até cinco edições para a rodada 2

1. **Fechar a matriz de tokens de F3.3:** listar todos os fills e strokes declarados/usados, incluindo `ThemeFieldGlassBrush` e `ThemeChatExpandedDenseBrush`; marcar `PanelGlassStrong` como ajustável, excluído ou sem uso. Definir o que `GlassTintColor` faz e demonstrar paridade de default em cada superfície e tema.
2. **Especificar a separação OFF/ON:** descrever como mudanças de vidro ficam isoladas do fallback atual, incluindo ao desligar o vidro após personalizar. Fixar o recorte e a tolerância do gate de paridade.
3. **Corrigir o plano de acessibilidade:** escolher a detecção real de transparência do Windows e definir fills opacos para alto contraste e redução de transparência, sem confundi-los com o modo OFF atual.
4. **Completar o inventário e resolver F2B:** adicionar as scrollbars de Configurações e do popup; reconciliar o aro sem face do `AppShell`, o traço de acento e as contagens de `Face`/`Ring`.
5. **Tornar os gates operacionais por fase:** especificar como medir p95, luminância, captura e memória; registrar risco e rollback por fase. Em F0.1, conferir `Path.Data` e geometria após `SizeChanged` do host, além da captura visual.
tokens used
98.158
# Veredito: **REJEITADO — rodada 1**

A sequência de trabalho faz sentido, e a correção do `MediaPanel` aponta para o evento certo. Mas há lacunas que impedem aprovar o plano sob o contrato atual, principalmente na paridade OFF, no mapa de tokens e no fallback de acessibilidade.

## Achados

1. **P1 — §5, linhas 19 e 30; §5.1: inventário incompleto de barras de rolagem.** A tabela inclui a scrollbar do chat, mas não as barras visíveis do `ScrollViewer` de Configurações (`SettingsPanel.xaml:299`) nem a do `ScrollViewer` dentro do popup do ComboBox (`FluentControls.xaml:760–765`). O `MessageBox` nativo está coberto na linha 32; não encontrei outro diálogo próprio. A categoria de `ToolTip` parece cobrir os tooltips existentes. A contagem de 35 precisa explicar se cada linha representa uma superfície ou um agrupamento de controles.

2. **P1 — §F3.3, “Intensidade” e “Cor e extras”; §8: mapa de recursos incompleto e defaults sem fórmula.** O multiplicador de força lista `IslandGlass`, `ThemeMediaGlassBrush` e `ThemeChipGlassBrush`, mas omite `ThemeFieldGlassBrush`, usado como `FaceFrosted` no botão de desacoplar. `ThemeChatExpandedDenseBrush` também é uma face real, e `PanelGlassStrong` está declarado como vidro em `App.xaml`, mas não é classificado como ajustável, excluído ou recurso sem uso. Além disso, `GlassTintColor=#0E1B2E` não corresponde às cores base de todos os fills — por exemplo, chip e field — nem explica como preserva a paleta dinâmica por capa. O plano reconhece `GlassTintColor` como novo; falta definir sua fórmula e provar que o default reproduz cada face atual.

3. **P1 — §F3.3, plumbing e G-F3.3-5/6: a paridade OFF não está garantida pelo desenho.** O plano propõe alterar brushes de face compartilhados. Hoje, quando o vidro está OFF, `Glass.ApplyFace` usa `Face`; quando está ON, usa `FaceFrosted` se houver. Assim, um brush alterado por um slider pode continuar afetando o fallback OFF. Não está descrito como separar valores efetivos de vidro dos fills de fallback, nem como restaurar os valores originais ao desligar. Também falta definir se o hash compara só as superfícies do app ou inclui a tela de Configurações, que mudou. Ver [Glass.cs](/C:/Users/9z2.pj/Downloads/katie%20df%20aplicativo/codigo%20fonte/Glass.cs:391).

4. **P1 — §F4.3: detecção de “Reduzir transparência” incorreta e fallback opaco não demonstrado.** `SystemParameters.ClientAreaAnimation` representa a preferência por animações da área cliente, não a configuração de transparência; a API documentada para consultar efeitos de transparência do Windows é `UISettings.AdvancedEffectsEnabled`. [Microsoft Learn: `ClientAreaAnimation`](https://learn.microsoft.com/en-us/dotnet/api/system.windows.systemparameters.clientareaanimation?view=windowsdesktop-10.0), [Microsoft Learn: `AdvancedEffectsEnabled`](https://learn.microsoft.com/en-us/uwp/api/windows.ui.viewmanagement.uisettings.advancedeffectsenabled?view=winrt-28000). Também não basta chamar `Glass.Apply(..., false)` para garantir “zero superfície translúcida”: o caminho OFF ainda usa fills como `IslandGlass` e `ThemeMediaGlassBrush`, que têm alpha parcial. Alto contraste e redução de transparência precisam de um caminho opaco explícito, separado do OFF visual atual.

5. **P2 — §F0.1: a correção proposta é adequada, mas o diagnóstico e o gate devem ser ajustados.** Assinar `SizeChanged` do host e tentar novamente após `Loaded` deve cobrir o caso em que o `Path` começa sem tamanho útil. Porém, o código atual retorna antes de construir a geometria quando largura ou altura ficam abaixo de 4; nesse caso, `Path.Data` permanece nulo, não é uma geometria 0×0. A guarda de idempotência descrita como mudança nova já existe em [Glass.cs](/C:/Users/9z2.pj/Downloads/katie%20df%20aplicativo%20codigo%20fonte/Glass.cs:401). Faça o gate verificar `Data` não nulo e geometria dimensionada após o host crescer; a amostragem de alpha da captura, isoladamente, não prova isso.

6. **P2 — §§F0.2, F3.3-9, F4.4 e F4.5: números existem, mas parte da medição não é reproduzível.** O plano pede p95 de frame “à mão”, sem definir ferramenta, duração da amostra, definição de frame ou método para medir o acréscimo de 1,2 ms. A instrumentação que poderia ajudar é opcional, então o gate de performance depende de uma medição não especificada. O descarte de HLSL é justificável pelo contrato R3; já a estimativa de 3–8 ms e a frase “80% da leitura visual a 0% do custo” não têm medição ou referência no plano. O ruído também precisa de gate de custo, não apenas de aparência.

7. **P2 — §§6, F4.0–F4.6 e §11: rollback genérico, sem plano por fase como prometido.** O §11 estabelece commits isolados e `git revert`, mas as fases F4.0–F4.6 não detalham como reverter mudanças de estado, recursos ou preferências persistidas. Isso importa especialmente em F3.3, cujo próprio texto reconhece que o plumbing de `LivePalette` não é aditivo. A declaração do §6 de que cada fase tem rollback próprio vai além do que as fases descrevem.

8. **P2 — §§2/R6–R7, 5/linha 1, F4.1 e §§4.5/9: conflito com o padrão F2B e com a contagem de hosts.** A moldura do `AppShell` recebe aro sem face, embora R7 exija `Path` com `Glass.Face` e `Glass.Ring` para hosts de vidro. F4.1 também propõe manter um segundo traço de acento junto ao aro; esclareça como isso respeita R6. Por fim, §9 conta popup e moldura entre os três novos hosts, mas F4.2 diz que o popup não entra no ledger do `Glass`; o total previsto de `Face` e `Ring` precisa fechar com essa decisão.

9. **P3 — §4.2 e plumbing de F3.3: “frozen” não está comprovado.** O código mostra `_glassHighlight` como `readonly`, mas isso, por si só, não prova que o brush está congelado. Não há uma chamada `Freeze()` nesse trecho de [LivePalette.cs](/C:/Users/9z2.pj/Downloads/katie%20df%20aplicativo%20codigo%20fonte/LivePalette.cs:162). O risco de mutação deve depender da verificação de `IsFrozen` prevista no próprio plano, não da conclusão de que o brush já é frozen.

## Até cinco edições para a rodada 2

1. **Fechar a matriz de tokens de F3.3:** listar todos os fills e strokes declarados/usados, incluindo `ThemeFieldGlassBrush` e `ThemeChatExpandedDenseBrush`; marcar `PanelGlassStrong` como ajustável, excluído ou sem uso. Definir o que `GlassTintColor` faz e demonstrar paridade de default em cada superfície e tema.
2. **Especificar a separação OFF/ON:** descrever como mudanças de vidro ficam isoladas do fallback atual, incluindo ao desligar o vidro após personalizar. Fixar o recorte e a tolerância do gate de paridade.
3. **Corrigir o plano de acessibilidade:** escolher a detecção real de transparência do Windows e definir fills opacos para alto contraste e redução de transparência, sem confundi-los com o modo OFF atual.
4. **Completar o inventário e resolver F2B:** adicionar as scrollbars de Configurações e do popup; reconciliar o aro sem face do `AppShell`, o traço de acento e as contagens de `Face`/`Ring`.
5. **Tornar os gates operacionais por fase:** especificar como medir p95, luminância, captura e memória; registrar risco e rollback por fase. Em F0.1, conferir `Path.Data` e geometria após `SizeChanged` do host, além da captura visual.
