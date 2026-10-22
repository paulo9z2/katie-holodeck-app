# BRIEF LEVA 2 — Liquid Glass no app inteiro (debate ANTES de qualquer código)

> **FASE: DISCUSSÃO + PLANO. ZERO CÓDIGO.** O dono ordenou: debate com os cavaleiros → plano 100% consolidado no Obsidian → só então implementação. Você NÃO edita nenhum arquivo de código nesta fase. Sua entrega é um documento de opinião.

## CONTEXTO

- F1 (aro de cristal T2) **APROVADA pelo dono**: aro de vidro vetorial no MediaPanel, UndockBtn e cartão Settings — `Glass.cs` (aro 1px neutro 9,8% + faceta 45° 1,5px + specular 0x33 na luz −24°, tudo leitura viva, zero alocação por frame). Código atual = base aprovada.
- Nova leva: **estender a estética liquid glass pro "praticamente tudo"** + mudança na barra inferior. Fundo real do pedido: o dono sente que o app ainda não "vestiu" o material — só o aro pontuou.

## PEDIDOS EXPLÍCITOS DO DONO

**A) Barra inferior (bottom bar, MainWindow.xaml:592-605):**
- HOJE: `Ellipse` 7×7 com glow (linha 594 — **o "ponto vermelho" que segue a paleta da capa, MANTER**) + `HintText` (linha 598, texto "KATIE Holodeck · digite no chat à direita · música no chip abaixo do reator" — **TIRAR**).
- **ADD a versão do aplicativo** no lugar (o dono quer saber a versão de fácil). Obs.: csproj diz `Version 0.6.0` (assembly) mas a versão de RELEASE do app é v9.8.2 (convenção de marketing, hoje inexistente no código) — opinar sobre a fonte única da versão (constante central? assembly?).

**B) Liquid glass estendido — superfícies inventariadas (arquivo:linha):**
1. Ilha TELEMETRIA esquerda (MainWindow.xaml:165-172+; CPU/GPU/RAM/NET/ESTADO/MODE; atualiza a cada 2s)
2. Ilha CONVERSA direita (chat: `ChatScroll`/`ChatPanel` 547-551, `ChatInputBorder` 558+, `ChatExpandBtn` 540)
3. Bottom bar completa (592-644: status à esquerda + `BottomBar` botões agenda/e-mails/música no centro, 644)
4. **FullscreenMusic** (610+; Z50; modo música fullscreen v9.3; LRC karaokê embaixo; spec 18/10: conteúdo NÍTIDO sobre blur) — o dono apontou "a aba que abre quando ela é expandida" como alvo principal
5. MediaPanel inline + UndockBtn + cartão Settings (JÁ têm o aro F1 — harmonizar, não refazer)
6. Gauges/labels internos, views de agenda/e-mail, player window — opinar se vestem vidro ou não

## FUNDAMENTOS PESQUISADOS (use; não invente por cima)

**Apple reverteu o exagero (2025→2026):** contraste medido a 1,5:1 nas primeiras builds (WCAG pede 4,5:1 para texto). Betas 26.2/26.3 aumentaram opacidade; iOS 26.1 ganhou Clear vs Tinted; iOS 27 (WWDC 2026) REDUZIU transparência default, difunde fundos complexos com mais força, e adotou **edge escuro + specular claro** para separação (exatamente a linguagem do nosso aro F1). Lição: **vidro é chrome de container, nunca palco de leitura**.
**HIG da casa (ATA F1-F3, valem TODAS):** R7 leitura viva · R11 brackets · R12 silhueta única · R13/R14 nada de acento na face/edge · V1 vidro NUNCA atrás de roda/letras LRC/métricas/PlayerWindow · V3 sem glow no vidro · V4 sem véu iOS · máx 2 níveis de vidro simultâneos.
**WPF perf (MS Learn + benchmarks):** DropShadow/Blur em WPF é GPU (MilCore), mas re-rasteriza o offscreen buffer a cada mudança — em elemento que anima/atualiza vira custo por frame. Receita: efeito no **container estático** (+ `BitmapCache` se necessário), **ZERO efeito em DataTemplate per-item** (chat é lista virtualizada!), telemetria atualiza a cada 2s sem mudar tamanho do container, visual tree rasa.
**Referência Liquid Lyrics** (`../_refs/liquid-lyrics.js`, extensão Spicetify em JS/CSS): letras word-by-word sincronizadas, scroll suave tipo Lenis, card lateral glass que herda o tema, placeholders durante busca. O dono disse que "vai ajudar muito" no fullscreen — traduza os PRINCÍPIOS (progressão de leitura fluida, card que respira com o tema) pro WPF, sem copiar CSS às cegas.

## O QUE RESPONDER (documento de opinião, da SUA lente)

1. **Mapa de decisão por superfície**: onde o glass ENTRA, onde NÃO entra, e o porquê (HIG + legibilidade + perf). Seja específico com arquivo:linha.
2. **Como o T2 vector escala** pra ilhas grandes (telemetria/chat): aro + faceta + fill? Novo token? O que reusa do Glass.cs aprovado?
3. **Fullscreen música**: o que muda (container? letras? player card?) com referência Liquid Lyrics — o que traduz e o que fica de fora.
4. **Bottom bar**: dot + versão (fonte única, tipografia, posição).
5. **Fases** (mais fácil → mais difícil) com gates binários por fase.
6. **Riscos e o que você NÃO faria** (o dono pediu explicitamente pra não exagerar).

## REGRAS

- PT-BR. Busque na web (2025/2026) ANTES de opinar sobre tecnologia — cite a fonte no documento.
- **ZERO edições em código.** Você escreve APENAS `../planejamento-liquid-glass/OPINIAO_LEVA2_<SEU_NOME>.md`.
- Referências no `../_refs/`: `liquid-glass.md` (material oficial), `hig/` (9 páginas: color, typography, accessibility, text-views, text-fields, progress-indicators, windows, scroll-views, settings), `liquid-lyrics.js`, `LiquidGlassEffect.cs` (shader F0/F2), `code-audit.md`.
- Se o dono aprovou (F1), não refaça — estenda.
