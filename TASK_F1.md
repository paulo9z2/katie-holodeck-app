# TASK_F1 — ARO DE CRISTAL T2 (Liquid Glass F1 no app)

> Contrato cirúrgico da Fase 1 do plano Liquid Glass F1-F3 (consolidado pelos 3 cavaleiros, aprovado pelo sir 22/10). Você é o cavaleiro OpenCode/DeepSeek V4.1 — implemente DIRETO, sem subagentes.

## CONTEXTO

- App **KatieHoloDeck v9.8.2 APROVADA** (esta cópia de trabalho; o sir fez backup — você pode editar livremente DENTRO do escopo).
- Efeito liquid glass provado no spike F0 (`../katie teste f0/LiquidGlassSpike`, APROVADO) — sua referência de shader é `_refs/LiquidGlassEffect.cs`, mas **F1 é T2 VECTOR — NÃO monte ShaderEffect agora** (isso é F2).
- Lente de design: `_refs/liquid-glass.md` (skill Apple Design, material Liquid Glass oficial + checklist) e `_refs/code-audit.md`. Traduza os princípios pra WPF — não copie SwiftUI às cegas.
- **Relé:** você monta a fundação F1 inteira. Depois de você, Freebuff refina o visual e Codex revisa o diff. Não edite o `TASK.md` antigo da raiz (é do v9.8.2) — o SEU contrato é este arquivo.

## FATOS-PROVADOS (confie; não rederive — já auditados em batalha)

1. `Chamfer.Octagon(double w, double h, double c)` **existe** — `Chamfer.cs:57`. O molde Adorner já desenha octógono com Pen em `Chamfer.cs:89` (padrão de leitura viva no OnRender).
2. **`PaletteChanged` NÃO existe** no LivePalette — resolvido por construção: **leitura ao vivo no OnRender**, zero cache (R7 morto por construção).
3. `ThemeTokens` vive em `LivePalette.cs:12`. **Tokens novos PRECISAM de `CopyFrom` + `Lerp`** — fora do Lerp = estalo visível no crossfade de capa 600ms. `Mix()` **descarta alpha em silêncio** → token nasce opaco, alpha entra via `WithA()` na montagem.
4. Luz da casa = **−24°**, elipse especular do reator em `MainWindow.xaml:313-315` (30×20, #30FFFFFF, glow #B3FFFFFF).
5. Superfícies-alvo: painel inline do MediaPanel (aro + fill denso mantido), `UndockBtn` `MainWindow.xaml:482` (30×30), cartão de Settings (`SettingsPanel.xaml:289` SecAparencia mora aí).
6. **WPF desta casa (v9.8.x pagou caro):** Freezable NUNCA é mutado após instalação em ResourceDictionary em uso — construir/mutar FORA e instalar POR ÚLTIMO (commit de7826f); **zero `stackalloc` com `Color` managed** (crash silencioso v9.8.2, commit 0307cc2); zero alocação por frame em caminho de render.
7. CornerRadius=14 do MediaPanel: alinhar ao idioma octogonal (1 linha visível, decisão do sir).
8. NÃO é repo git → **não commit**, não rode `git init`. Entrega = arquivos editados + RELATORIO_F1.md.

## ALVO — "aro de cristal" (definição de vidro da casa, T2)

Aro = **edge 1px branca neutra 8-10%** + **faceta 45° dura 1-3px** + **specular 0x33 SÓ na faceta superior-esquerda** (luz −24°). Face neutra (nunca sobe luminância local > 4/255). **Sem sheen** — o conceito morre, nasce `ThemeGlassFacet`.

## TAREFAS

1. **Tokens** em `ThemeTokens`: `ThemeGlassEdge` (branca neutra, alpha 8-10%), `ThemeGlassFacet` (faceta), specular **0x33 exato** (mesmo grupo do acento, = `AccentBrushFaint`) — todos com `CopyFrom`+`Lerp`, opacos, alpha via `WithA()`.
2. **Fill octogonal**: fill das 3 superfícies vira `Path`/`PathGeometry` via `Chamfer.Octagon()` (R12: rounded-rect mente sob traço octogonal; bônus: sombra herda a silhueta).
3. **Aro** nas 3 superfícies: painel inline (aro + fill denso mantido) · UndockBtn (hairline+faceta, **sem chanfro novo**, T3 proibido em 30px) · Settings (aro no cartão, **fill sólido mantido**).
4. **R11**: brackets da TechFrame flutuam no vazio do chanfro → braços começam em `x = c − off` OU variante sem bracket (escolha e documente).
5. **Toggle** `enableGlass` (default **OFF**) na SecAparencia; modo efetivo Off/Vector/Shader; `OptNone` força Vector.
6. **Proibições de cor**: nada de acento na face ou edge do vidro (R13/R14), sem glow no vidro (V3), sem véu iOS (V4).

## GATES F1 (binários — passe todos, com evidência)

- Pixels fora da silhueta: Δ=0 bit a bit
- Luminância da face: ≤ +4/255
- Fill ≤ 0,5px da silhueta octogonal
- Nenhum bracket órfão flutuando
- Troca de tema/acento sem estalo (crossfade 600ms limpo)
- Custo zero no render loop (zero alocação/frame em OnRender)
- `dotnet build` 0 erros, 0 warnings novos

## ENTREGÁVEIS

- Código editado nos arquivos do app (TODAS as edições citadas arquivo:linha no relatório)
- `RELATORIO_F1.md` na raiz: o que fez arquivo-por-arquivo, decisões tomadas (ex.: variante R11), self-check dos gates, e o que deixou pro Freebuff refinar/Codex revisar

## REGRAS GERAIS

- PT-BR. Editar > recriar. Escopo estrito: só arquivos do app + `RELATORIO_F1.md` + `TASK_F1.md` (se precisar anotar).
- NÃO edite: `TASK.md`, `_refs/*`, `RELATORIO_v9.8.2.md`, `RELATORIO_v951.md`, `versao anterior/`, `final .exe/`, `../onboarding-frota/*`.
- Erro no caminho? Documente no relatório com arquivo:linha e siga — não pare no primeiro obstáculo.
