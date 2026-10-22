---
title: KatieHoloDeck - Referencias Visuais e Styles (18-10-2026)
aliases: [Referencias Visuais KatieHoloDeck, Styles sci-fi KATIE]
tags: [holodeck, design, referencia, pesquisa]
created: 2026-10-18
updated: 2026-10-18
status: "pesquisa concluida — insumo para o refinamento visual da v8"
up: "[[KatieHoloDeck (MOC)]]"
---

# KatieHoloDeck — Referências Visuais e Styles (18-10-2026)

> [!abstract]
> Pesquisa web (18/10/2026, prioridade 2025-26) para elevar o visual do KatieHoloDeck mantendo o DNA JARVIS/Stark Industries: fundo dark navy com grid, acento ciano, Orbitron. O app já acerta o essencial (navy+grid+ciano, Mica real na v7, cascata de chips); o que falta é **disciplina de design de produto** — hierarquia por luminância, cor com papel funcional, motion que explica estado. Entrega: 10 referências com URL e por-quê, micro-trends 2025-26 portáveis para WPF, paleta/tipografia e 8 upgrades acionáveis.

## 1. Referências visuais (top 10)

| Ref | URL | Por quê |
|---|---|---|
| **FUI Sci-Fi HUD (Curio)** | https://designbycurio.com/learn/fui-sci-fi-hud | Dicionário do gênero: campo void-black, wireframe fino brilhante, ring gauges, radar grids, scan motion. Regra de ouro: o contraste deve parecer **emissivo, não impresso** — a UI gera a própria luz |
| **Star Citizen (Curio)** | https://designbycurio.com/learn/star-citizen-roberts | O parente mais próximo do app: ciano = informação, azul-painel = estrutura, preto = ambiente, **laranja exclusivo de alerta**. "O drama vem da contenção, não do espetáculo" |
| **Cyberpunk 2077 Screen UI (Curio)** | https://designbycurio.com/learn/cyberpunk-2077-screen | Receitas de micro-efeitos: scanlines a 3-6%, **L-brackets** nos cantos (borda parcial em vez de retângulo), chamfer 45°, **glow-como-elevação** (brilho substitui sombra no dark), glitch só no hover/transição |
| **Cyberpunk Night City Yellow (Curio)** | https://designbycurio.com/learn/cyberpunk-2077-night-city-yellow | Aberração cromática magenta+ciano como sinal **semântico** (intensidade sobe em estado de dano/perigo), não decoração. Todo pixel tem classificação funcional |
| **jenman — réplica JARVIS (GitHub)** | https://github.com/Davood121/jenman | Inventário roubável de widgets + hexes exatos do HUD Stark: ciano elétrico `#00CFFF`/`#00FFF7`, scanline CRT, grid holográfico, corner brackets, vinheta radial, arc reactor com anéis concêntricos, radar sweep, reticle |
| **Jayse Hansen — HUD Bible** | http://inventinginteractive.com/2012/07/08/interview-jayse-hansen/ | O designer original do HUD do Stark: "HUD Bible" documenta a **função de cada widget**; densidade alta com leitura em milissegundos via pattern-recognition; elementos animam em camadas de profundidade 3D |
| **scifiinterfaces — Iron Man 3** | https://scifiinterfaces.com/category/marvel-cinematic-universe/iron-man/iron-man-3-2013/ | Hierarquia viva: gauges vivem pequenos na periferia e **escalam para o centro** quando ficam relevantes (JARVIS "escala o sinal") — base comportamental dos chips/painéis |
| **Territory Studio (Q&A)** | https://scifiinterfaces.com/2020/06/23/scifi-interfaces-qa-with-territory-studio/ | O anti-kitsch de Blade Runner 2049/The Martian: credibilidade via pesquisa real (aviação, NASA), paleta simples, FUI que "comunica o plot em um relance" |
| **Lusion (Awwwards SOTY 2023)** | https://www.awwwards.com/lusion/ | Benchmark de motion premiado (12× Site of the Day): premium = **lerped, lento, atmosférico — nunca snappy**; o timing ensina mais que o efeito em si |
| **daisyUI — Futuristic/Speculative UI 2026** | https://trends.daisyui.com/futuristic-speculative-ui/ | Taxonomia de mercado 2026: FUI/HUD (telemetria, reticles, scan, readouts translúcidos) e Holographic/Iridescent (spectral shifts) — nomeia o estilo que o app já faz |

> [!warning]
> **Tropos de FUI ≠ UI de produto** (vocab.design/fictional-user-interface): a *composição* transfere (hierarquia sob pouca luz, paleta luminosa restrita, densidade legível), os *tropos* não — números piscando ilegíveis, anéis decorativos competindo com controles reais, loops eternos de telemetria. Atmosfera vai para hero/loading/empty states; **o painel que a pessoa opera fica quieto**.

## 2. Micro-trends 2025-26 aplicáveis ao WPF

- **Glow-como-elevação** (Cyberpunk 2077): no dark, elevação é brilho externo, não sombra → `DropShadowEffect ShadowDepth="0" BlurRadius="8-20" Color="#00CFFF"`, animando `Opacity`/`BlurRadius` para pulsar. Efeito aplicado na Window é clipado: usar `Border` interno com margem (padrão glow VS2012, alski.net).
- **Scanline/CRT overlay** (jenman/Cyberpunk): 1 retângulo full-window acima de tudo — `TileBrush` de linha 1-2px a 3-6% de opacidade + `TranslateY` animado + vinheta radial. Receita já validada no reator: TileBrush scanline animado + flicker senoidal.
- **Bordas parciais L-bracket + chamfer 45°** (Cyberpunk): 4 `Path`s de canto no `ControlTemplate` em vez de `Border` retangular; cantos chanfrados via `Polygon`. O upgrade "look HUD" mais barato em perf.
- **Física de mola nos controles** (motion 2026): ease linear/ease-out parece mecânico → `ElasticEase`/`BackEase` no pressed (~120ms, scale 0.97→1.0) e na abertura de painéis; a cascata de chips (v7 ✓) já segue o staggered reveal premiado.
- **Shimmer skeleton** (premium 2026): loading com `LinearGradientBrush` animado varrendo um `OpacityMask` (gradiente a 200% da largura, loop 1.5s — receita DinoChan/wpf_design_and_animation_lab) para o chat aguardando KATIE e o boot da telemetria.
- **Motion reativo a conteúdo** (fronteira 2026): animação reage ao dado — glow/velocidade dos anéis do reator modulados por CPU/GPU reais; "thinking" do chat = pulso rítmico no avatar. O HUD parece vivo sem decoração vazia.
- **Hierarquia por luminância** (FUI): elemento ativo brilha mais, inativo esmaece (opacity ~0.6) — hierarquia não vem só de tamanho/peso de fonte.
- **Motion com propósito** (Tubik 2026): animação explica estado/estrutura, não enfeita; animações que ensinam <300ms; micro-delay proposital ("Processing…") passa confiabilidade — usar no envio de comando da KATIE.
- **Glass 2.0 estratégico** (Liquid Glass/Mica 2026): blur/vidro SÓ em camadas secundárias (player flutuante, popups, painel lateral). Mica/Acrylic real já funciona na v7 — o refinamento 2026 é *onde* usar, não se usar.
- **Idle que respira** (designmd HUD principles): "A dormant HUD still breathes" — rotação sutil, scan, pulsos de nó em amplitude baixa; nada estático, nada gritando.

## 3. Paleta e tipografia (dark navy + ciano)

| Papel | Valor sugerido | Nota |
|---|---|---|
| Fundo base | `#0A0E1A`–`#0D1220` | Navy quase-preto (hsl ~225 20% 7%); NUNCA `#000` puro (halation) |
| Elevação de superfície | `#111827` → `#161F33` | +5-8% de lightness por nível; superfície mais alta = mais clara |
| Ciano primário | `#00CFFF` (var. `#00FFF7`) | Info/glow — hexes do HUD Stark (jenman) |
| Ciano secundário | `#7FDFFF` | Light-cyan para texto fino e detalhe (Star Citizen) |
| Texto | branco a 87% · secundário 60% | Padrão Material; branco puro causa halation no dark |
| Grid de fundo | ciano/branco a 5-10% | Linhas 1px sobre o navy |
| Alerta | `#FF7A1A` · crítico `#F44747` | **Laranja exclusivo de alerta** (disciplina Star Citizen) |

Contraste: corpo ≥4.5:1, componentes ≥3:1 (WCAG 2.2); acentos no dark pedem dessaturação leve com lightness maior (equivalente ao peso 300-400 do brand); testar com brilho de tela em 50-70% — não em 100%.

**Tipografia em 3 níveis (máx. 3 fontes):**
- **Orbitron = display apenas** — títulos, números grandes, labels de seção; all-caps + letter-spacing generoso; emborracha em texto pequeno.
- **Dados/telemetria = monospace** — referência JetBrains Mono (Cyberpunk); nativo no Windows: **Cascadia Mono** ou Consolas para readouts, timestamps, hex.
- **Corpo = humanista tech** — Exo 2 / Rajdhani / Space Grotesk (Google) ou **Segoe UI Variable** (nativo, zero custo).
- Linhagem: Eurostile Bold Extended é "a fonte do futuro" desde 2001 — Orbitron/Electrolize descendem dela.
- "Typography as data" (designmd): uppercase, monospaced ou ambos; labels curtos — frase longa = saiu do gênero.

## 4. Upgrades acionáveis para a v8

1. **Enquadramento HUD** — trocar bordas retangulares por L-brackets (4 `Path`s de canto) + chamfer 45° nos painéis principais; efeito imediato, custo de perf ~zero.
2. **Camada scanline + vinheta** — 1 overlay full-window (TileBrush 3-6% + vinheta radial) por cima de tudo: o "selo" holográfico do jenman/Cyberpunk.
3. **Glow como elevação** — DropShadowEffect ciano `ShadowDepth=0` nos elementos vivos (anéis do reator, chip ativo, borda do chat); elementos inativos a 60% de opacity.
4. **Estados táteis com mola** — pressed = scale 0.97 + BackEase (~120ms); hover = ramp de BlurRadius no glow (não troca de cor). Respeitar a lição v7: nunca zerar o RenderTransform que o EventTrigger anima.
5. **Telemetria reativa** — CPU/GPU reais modulam glow e velocidade dos anéis; threshold >85% escala o widget para o centro e pinta laranja (escalação de sinal do scifiinterfaces).
6. **Shimmer no loading** — skeletons com varredura de gradient (loop 1.5s) no boot do chat/telemetria; estado "thinking" da KATIE com pulso rítmico em vez de spinner.
7. **Tipografia 3 níveis** — Orbitron (display) · Cascadia Mono (readouts) · Segoe UI Variable (corpo); remover Orbitron de qualquer texto pequeno.
8. **Paleta tokenizada** — 4 superfícies navy (+5-8% cada) + ciano em 2 pesos + laranja exclusivo de alerta. Se tudo brilha, o design perde a calma de instrumento (Star Citizen).

**Ver também:** [[KatieHoloDeck - v7 Repaginada e Crash RenderTransform]] · [[Referencias de Bordas - As 4 imagens do sir]] · [[KATIE-Playground - Lab de Iteracao Rapida]]
