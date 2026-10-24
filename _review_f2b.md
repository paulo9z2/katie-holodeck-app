# Revisão F2B — Codex

Data: 22/10/2026  
Commits revisados: `08c95e1` (AppInfo) e `873f0d0` (F2.2 telemetria + F2.3 chat)  
Diff: `git diff HEAD~2 HEAD`

## Veredito: APROVADO COM RESSALVAS

O fallback de versão e a troca de faces estão corretos. Há uma regressão de hit-test ao expandir o chat e uma ressalva visual: o aro neutro quase desaparece diante do TechFrame no screenshot. Recomendo corrigir a área de hit-test antes de considerar a interação de Chat expandido concluída; a presença do aro pode seguir para ajuste em F2.6.

## F2A — retorno ao parecer anterior

- **P1 corrigido — `Core/AppInfo.cs:24`.** `Version == null` agora retorna `string.Empty`; não há versão inventada no fallback. O comentário XML também explica esse comportamento.
- **P2 permanece como ressalva aceita.** As ocorrências históricas de `9.8.2` nos comentários citados pelo F2A continuam no repositório; este par de commits não as introduz nem muda a fonte da versão. O gate textual de “uma ocorrência” ainda não passa se contar comentários, mas isso não representa uma segunda fonte da versão exibida.

## Checklist F2B

1. **Host e paridade de layout — aprovado.** Em `MainWindow.xaml:171-188`, Telemetria usa `Padding=0`, `Background=x:Null`, `BorderThickness=0`, `Glass.Ring` e `TelemetryFace`. A margem 15 corresponde ao padding antigo 14 mais o pixel de borda removido. Em `MainWindow.xaml:544-565`, Chat usa margem `13,13,13,11`, igual ao padding antigo `12,12,12,10` mais a borda de 1 px. Os filhos de conteúdo e suas margens internas permanecem iguais; portanto, o conteúdo conserva a posição e o Path ganha a sangria.

2. **Troca da face e bug `Background=null` — aprovado.** `Glass.SetFace(ChatFace, "ThemeChatExpandedBrush")` ao expandir e `Glass.SetFace(ChatFace, "IslandGlass")` ao recolher (`MainWindow.xaml.cs:1776,1821`) são compatíveis com `Glass.ApplyFace` (`Glass.cs:372-395`). `Face` é uma propriedade anexada real; `ApplyFace` escolhe a chave da face e usa `SetResourceReference` no `Path.Fill`. As duas chaves existem em `App.xaml`. Não há mais restauração via `Border.Background=null`.

3. **`DimWhenGlass` — comportamento coerente com a implementação.** A propriedade anexada existe em `Glass.cs:441-462` e é usada nos dois `SurfaceTintBrush`. Com glass OFF, o overlay fica `Visible`; com glass ON, fica `Collapsed`. Portanto, o tint não recebe uma redução gradual de opacidade: ele sai, deixando a face `IslandGlass` por baixo e produzindo o resultado visual mais dim. Isso coincide com o comentário e o desenho atual de `ApplyDim`.

4. **`FloatingIslandStyle` — sem efeito colateral global.** Continua usado em Telemetria, Chat e no `PanelCard` de Settings (`SettingsPanel.xaml:246`). `Background`, `BorderThickness` e `Padding` são substituídos por valores locais apenas nas duas ilhas; `BorderBrush` local continua alimentando os adorners TechFrame/Chamfer embora o Border não desenhe a própria borda com thickness zero. O Style não foi alterado, e Settings mantém seus próprios overrides.

## Problemas e ressalvas

- **P2 — hit-test do Chat expandido — `MainWindow.xaml:544-565`, `MainWindow.xaml.cs:388`.** A face Path não participa do hit-test e o `Background="Transparent"` cobre só o Grid interno, que começa 13 DIP depois da borda esquerda. Essa faixa fica sem alvo no Chat expandido. Na largura mínima de 1080, ela se sobrepõe ao `ReactorHost`; como ele tem `Background="Transparent"` e `MouseLeftButtonDown` chama `OpenPlayerFromWheel()`, clicar nessa faixa pode abrir o player por baixo do chat. O Z-index alto do Chat não impede isso porque a faixa não tem visual hit-testável. Colocar um fundo transparente no Grid externo do host (ou outra superfície hit-testável de tamanho completo) evita o vazamento sem pintar a face.
- **Ressalva visual para F2.6 — aro — `App.xaml:101-102`, screenshot `obj/_gate/f2b_evid/on_compact.png`.** O aro neutro de 1 px fica muito sutil nas duas faces escuras e o TechFrame ciano domina a leitura. A separação de material existe, mas o aro não se apresenta como contorno visual claro. Recomendo avaliar um aumento pequeno e controlado de `GlassEdgeBrush`/`GlassFacetBrush`, comparando glass OFF/ON e chat compacto/expandido; não aumentaria o specular ciano como primeira medida.

Não executei build nem interação no app; a avaliação de runtime é estática e usa o screenshot fornecido.
