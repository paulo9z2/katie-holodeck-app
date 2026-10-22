# CONTRATO v9.8.2 — REPARO DO MÓDULO MÚSICA (KatieHoloDeck)

Você é o cirurgião. Implemente DIRETO você mesmo, sem subagentes/orquestração/delegações internas.
NÃO faça commit — o orquestrador (Katie) audita o diff no disco e commita.
Repo: este diretório (master). Build de verificação: `dotnet build KatieHoloDeck/KatieHoloDeck.csproj -c Release` (0 erros).

## CONTEXTO — o que o dono reportou ao testar o app AO VIVO (21/10, v9.8.1 recém-aberta):
1. O círculo ao redor do reator aparece MESMO SEM música (antes: só aparecia com música tocando).
2. COM música tocando: ele não muda nada — fica parado e quase invisível.
3. O app não reage a nada (sem efeitos de interação conforme a música).
4. O app inteiro não muda a paleta de cores conforme a capa da música (capa-tema morto).
5. "O app não consegue nem perceber se tem música tocando."

## FATOS JÁ PROVADOS pela caça (Katie, 21/10 — confie, não rederive):
- MediaTracker/SMTC está VIVO: `bin/Release/net8.0-windows10.0.19041/media_debug.jsonl` mostra updates reais de hoje (Summertime Sadness|Lana Del Rey, Smooth Operator|Sade, Moonlight|Kali Uchis). O `OnMediaUpdate` RODA.
- settings.json: `wheel=true, rings=true, coverAccent=true, coverAll=true` — tudo ligado.
- AudioSpectrum.cs INTACTO de v9.6→HEAD (loopback WASAPI que FUNCIONAVA na v9.6 não mudou).
- v9.6 (commit 7d8500e) é a última versão que o dono viu funcionar 100%.
- v9.7 (77241a4 capa-tema + 99e2a72 Ring Sleep) CRASHAVA NO BOOT (LivePalette mutava brush congelado) — **nunca rodou em vida**. O fix v9.8.1 (de7826f) botou o app de pé, mas o comportamento de música nunca foi testado até agora.
- MusicWheel.cs +182: estados NOVOS Active→Ember(12% brilho, respiração 5s, GoStandby) — suspeito de renderizar "quase invisível e parado".
- MainWindow.xaml.cs +519: cadeia capa-tema `UpdateCoverAccent → TransitionAccentTo → PaletteTick (_paletteTimer) → LivePalette.ApplyTheme/SetAccent`, com `_themeFrom/_themeTo/_themeScratch/LerpColor`.
- LivePalette.cs (436 linhas) é NOVO (v9.8.1): paleta por SUBSTITUIÇÃO quando brush frozen ("construir FORA, instalar POR ÚLTIMO" — helpers Solid/Grad/PenBrush; `Dictionary[key] = novo` re-resolve via DynamicResource) e mutação in-place quando vivo.
- dispatcher.log: boot limpo, zero exceção.
- Sintoma 1+2 têm cara de estado EMBER preso (sessão SMTC fantasma de browser/jogo segura `HasSession=true` e a roda nunca vai a Standby; com música real o gate não acende Active).
- LivePalette.SetAccent aplica alphas fortes no ReactorRim NO BOOT (0xE6/0x26/0x0F) — no v9.6 o rim só acendia com música.

## ALVO — comportamento v9.6 (o dono pediu "volta como era antes"):
- SEM música de verdade: círculo/rim/anéis ao redor do reator INVISÍVEIS (não existe brasa/Ember visível sem ter tocado).
- COM música: anéis aparecem, giram, beat pulse e brilho por energia (bands do AudioSpectrum WASAPI — o mesmo que funcionava na v9.6).
- Paleta do app inteira muda conforme a capa da música (capa-tema) com crossfade — feature v9.7 MANTIDA, mas FUNCIONANDO.
- Selo de Origem (AUMID no título inline/fullscreen, clique traz o app de origem) — MANTER (aprovado), não mexa.
- Ember (Ring Sleep): pode existir SÓ na pausa real (sessão viva + estava tocando + IsPlaying=false agora). Sessão fantasma que nunca tocou = INVISÍVEL. Em dúvida: prioridade absoluta é o gate v9.6 (visível ⇔ `spectrum.HasAudio || IsPlaying`).

## TAREFAS (diff `7d8500e..HEAD` é seu mapa; edite, NÃO recrie arquivos):
1. Diagnóstico primeiro: para CADA sintoma (1-5), encontre a causa raiz exata com arquivo:linha comparando v9.6↔HEAD. Anote no relatório.
2. Fix do gate de visibilidade da roda/rim/anéis (sintomas 1+2): voltar ao critério v9.6 `audioLive = _spectrum.HasAudio || _mediaPlaying` como ÚNICO dono da visibilidade; Standby total quando false; Ember restrito à pausa real como no ALVO. Verifique também quem controla a VISIBILIDADE XAML do rim (MainWindow.xaml) — v9.6 escondia sem música.
3. Fix da reação (sintomas 2+3): garantir que bands/publish da v9.6 chegam na roda (OnRender idem v9.6), incluindo SetPlaying/SetHasSession com os gates novos SEM prender Active/Ember.
4. Fix do capa-tema (sintomas 4): rastreie `UpdateCoverAccent→TransitionAccentTo→PaletteTick→ApplyTheme` ponto a ponto e conserte onde para de propagar (hipóteses: crossfade nunca dispara; ApplyTheme substitui brushes mas consumidores não re-resolvem; _paletteTimer parado; ThemeTokens.CopyFrom/Lerp com estado errado). O tema default ciano + acento por settings continuam funcionando (já funcionam).
5. REGRA INVIOLÁVEL do projeto: NUNCA mutar brush congelado — WPF sela Freezables no ato da inserção em ResourceDictionary em uso (v9.4.0/9.8.1). Toda troca: construir/mutar FORA e instalar POR ÚLTIMO.
6. REGRA DE PERF v9.8: zero alocação por frame no render loop; brushes/gradientes reusáveis (exceto burst raro de troca de paleta).
7. Proibido: reescrever arquivos inteiros, mexer em PlayerWindow/Selo, tocar no mecanismo do fix v9.8.1 do boot, `git commit`, criar branches.

## ENTREGÁVEIS
- Diff mínimo nos arquivos necessários (MusicWheel.cs, MainWindow.xaml/.cs, LivePalette.cs — onde a causa raiz morar).
- `RELATORIO_v9.8.2.md` NA RAIZ do repo com, para cada sintoma: causa raiz (arquivo:linha), o que mudou, como o orquestrador pode verificar (comando/observação).
- Build 0 erros.
