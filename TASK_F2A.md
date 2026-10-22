# TASK F2A — Versão fonte única + Bottom bar (Leva 2, fases 1-2)

> Executável pelo cavaleiro OpenCode. Você já opinou nesta leva: releia `../planejamento-liquid-glass/OPINIAO_LEVA2_OPENCODE.md` (seções F2.0/F2.1) — este task segue SUAS especificações. Build `0 erros / 0 warnings` obrigatório. Commit git no final.

## F2.0 — Versão fonte única

1. `KatieHoloDeck.csproj`: trocar `<Version>0.6.0</Version>` → `<Version>9.8.2</Version>`. **SEM bump além disso** (o display é v9.8.2, a versão de release de marketing vigente — regra da casa: nada de version bump a menos que o dono peça).
2. Criar `Core/AppInfo.cs`: classe estática `AppInfo` com `VersionString` → `"v" + AssemblyVersion(major.minor.build)` lido do assembly, cacheado em static readonly. Nada de constante manual de versão em C#.
3. Onde o app mostra versão hoje (ex.: cartão Settings): trocar para usar `AppInfo.VersionString`.
4. **Gate F2.0**: `grep -rn "9\.8\.2\|0\.6\.0"` na fonte → a ÚNICA ocorrência de versão é o csproj; `dotnet build` 0/0; versão mostrada no app = "v9.8.2" = tab Version do .exe.

## F2.1 — Bottom bar (MainWindow.xaml, barra de status esquerda: linhas ~592-605)

1. **TIRAR o texto de tutorial** do `HintText` (a legenda "KATIE Holodeck · digite no chat à direita · música no chip abaixo do reator").
2. **NÃO APAGAR o canal funcional**: `HintText` é escrito por 5 pontos do code-behind (linhas ~731, 1512, 2160, 2369 — status de agenda, e-mail, música). Renomear o elemento para `x:Name="StatusText"`, `Text=""` (default vazio), atualizar as 5 referências. Texto de tutorial NÃO volta.
3. **Layout 3 slots** na barra: [esquerda: dot + versão] [centro: StatusText] [direita: espaço simétrico vazio]. O dot (`Ellipse` 7×7 com glow, linha ~594) fica EXATAMENTE como está — é o acento da barra. A versão entra ao lado do dot: `TextBlock` com `{x:Static core:AppInfo.VersionString}` (ou binding equivalente), fonte 11-12px (Segoe UI Variable Text se disponível), `ThemeTextDim` ou token com contraste composto ≥4,5:1 sobre o fundo da barra. **Versão SEM cor de acento** (o dot já é o acento — R13/R14).
4. **Hairline neutra**: a linha de separação do topo da barra passa a usar `GlassEdgeBrush` (neutra), não aro/bracket — a barra é chrome de faixa, não pílula.
5. **PROIBIDO**: aro/bracket na barra, blur/Effect, tocar no Ellipse/glow, apagar o StatusText, cor de acento na versão, hint de tutorial, aro por botão.
6. **Gate F2.1**: build 0/0; versão visível na barra (contraste ok); os 3 fluxos funcionais que escrevem StatusText continuam mudando o texto (agenda/e-mail/música — teste lendo o code-behind, sem rodar o app); dot pixel-idêntico ao anterior.

## Verificação final (obrigatória antes do commit)

- `dotnet build` → 0 erros / 0 warnings
- `git add -A && git commit -m "F2.0+F2.1: versao fonte unica (csproj/AppInfo) + bottom bar 3 slots (dot+versao | status | vazio), hairline neutra, StatusText sem tutorial"`
- Reportar: diff resumido, linhas tocadas, gates F2.0/F2.1 com resultado, hash do commit.
