# TASK F2.6 + F3.1 — Aro VISÍVEL (harmonização) + Chips no vidro

> Cavaleiro OpenCode. cwd = repo. Build 0/0 em `-p:OutDir=obj/_gate/f26/` (produção do dono RODANDO — nunca matar/copiar bin/Debug). Um commit por fase. Releia OPINIAO_LEVA2_OPENCODE.md e REVIEW_F2B_CODEX.md (§resalva visual — é o norte desta fase).

## F2.6 — Aro que se VÊ (feedback direto do dono: "não consigo ver o liquid glass")

Fatos: `App.xaml:101-102` — `GlassEdgeBrush` (aro 1px, alpha ~9,8%) e `GlassFacetBrush` (faceta 45°). Hoje o hairline ciano do TechFrame DOMINA e o aro some (parece "hairline estilizado", não vidro — visão + Codex + dono concordam). `GlassEdgeHighlightBrush` (specular) existe no grupo do acento.

**Mudanças (tokens, SEM tocar desenho do Path)**:
1. `GlassEdgeBrush`: alpha sobe de ~0x19 (9,8%) para **~0x42-0x4D (26-30%)** — aro neutro claro que lê como polimento de vidro no fundo escuro
2. `GlassFacetBrush`: alpha ~0x30 → **~0x52-0x5C** (faceta mais presente, mantém neutro — SEM acento ciano na face/edge)
3. Specular `GlassEdgeHighlightBrush` (0x33 hoje): **~0x55** — ponto de luz que a visão consegue APONTAR nos cantos superiores
4. **Proibido**: especular ciano mais forte (parede do Codex), aro >1,5px de espessura, glow/BlurEffect
5. **Gate de medida (obrigatório)**: screenshot do app (instância própria em OutDir, posicionada, SEM tocar a do dono) glass ON: amostra de pixel na linha do aro de IslandTelemetry — luminância do aro ≥2× o fundo imediato; e um print antes/depois lado a lado
6. Toggle OFF: retorno EXATO ao visual de hoje (±1px, o padrão de paridade da leva)

## F3.1 — Chips (agenda / e-mails / música) no vidro

Fatos: chips = `ChipPanel` com 3 botões (agenda/e-mails/música), estilo com storyboard de press/hover (`ChipButtonBaseStyle` — lição: NUNCA zerar RenderTransform de elemento com EventTrigger animando transform). Cada chip já tem cor de assinatura (azul/vermelho/verde).

**Mudanças**:
1. Cada chip vira host glass (padrão F2B: Border Padding=0/Background=x:Null/BorderThickness=0 + Glass.Ring + Path Face primeiro-filho + Grid interno com margem compensando + Background=Transparent no Grid externo pro hit-test)
2. Fill da face: `IslandGlass` (o vidro comum da casa) — a cor de assinatura do chip CONTINUA vinda do BorderBrush/foreground existentes, nunca na face
3. **NÃO tocar**: animações de press/hover, RenderTransform, glow interno, o seletor de painel, o reator
4. **Gate**: chips clicáveis (hit-test na sangria — o P2 da F2B), animação de press intacta (10 cliques UIA ou mouse-walk), OFF parity, build 0/0

## Reporte
Diff resumido por commit, gates com medida, hashes. PT-BR.
