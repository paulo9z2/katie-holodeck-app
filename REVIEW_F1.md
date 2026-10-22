# REVIEW F1 — KatieHoloDeck v9.8.2 · aro de cristal T2

**Veredito: REJECT**

Revisei `TASK_F1.md`, `RELATORIO_F1.md`, `RELATORIO_F1_PAINT.md` e os fontes pedidos. As duas remoções de `FallbackStroke` já estavam aplicadas no estado encontrado: o `Path` do `MediaPanel` e o `Path` do `PanelCard` não têm esse atributo. Mantive o `FallbackStroke="AccentBrush"` do `UndockBtn`, que não tem chanfro.

## P1s

1. **Alocação por render continua no chanfro — `Chamfer.cs:105` (helper em `Chamfer.cs:58-70`).** `ChamferAdorner.OnRender` chama `Octagon(...)` a cada render; o helper cria `PathGeometry`, `PathFigure` e segmentos novos. Isso viola o gate F1 de zero alocação por frame. A invalidação durante a transição de tema pode repetir esse caminho em vários frames. **Correção sugerida:** manter a geometria no adorner e reconstruí-la somente quando largura, altura ou chanfro mudarem; desenhar a geometria armazenada no `OnRender`.

2. **Faceta arredondada pode ultrapassar a silhueta — `Glass.cs:278-285` e `Glass.cs:289-292`.** O raio da linha central é `rr - 0,75`, mas os arcos começam/terminam em coordenadas com inset `0,5` (`EdgeInset`). Esses pontos não pertencem ao círculo de raio `R` centrado no canto; o `ArcSegment` resultante desloca o arco. Além disso, o traço tem 1,5 px e começa em `y=0,5` (ou `x=0,5`), deixando até 0,25 px de largura fora do limite superior/esquerdo. O mesmo início é usado pelo specular. **Correção sugerida:** construir os endpoints da linha central com inset `FacetWidth / 2` (0,75 px), manter cada arco concêntrico ao canto arredondado original e verificar que a borda externa do traço coincide com a silhueta. O alvo afetado é o `UndockBtn` de 30×30, raio 8.

Os dois itens são violações diretas dos gates binários de render/alinhamento; por isso não aprovo a F1 neste estado. Os gates de pixel também não foram medidos em screenshot, conforme a limitação honesta já registrada no relatório de pintura.

## Checagens sem P1 adicional confirmado

- `LivePalette.cs`: os tokens `ThemeGlassEdge` e `ThemeGlassFacet` aparecem em `CopyFrom`, `Lerp`, `ReadDefaultTheme` e `ApplyTheme`; `WithA` injeta os alphas na montagem. `Glass.cs` consulta os brushes vivos no `OnRender`, sem cache de cor. Não encontrei mutação dos brushes de recurso a partir dos adorners.
- Não encontrei `stackalloc Color` nos caminhos revisados; a montagem de superfícies em `MainWindow.xaml.cs` usa array.
- `TechFrame.cs`: `RingInset`, `Invalidate` e `start` leem o estado do aro sem cache. O inset mantém os brackets afastados do contorno e `Glass.Refresh` invalida a moldura ao alternar o modo.
- `ChamferAdorner` retorna quando o aro está ativo; o fill das faces usa `Chamfer.Octagon`. O `SurfaceTint` do cartão de configurações é colapsado com vidro ligado.
- Toggle de vidro permanece default OFF; `OptNone` resolve para Vector. Nenhum `ShaderEffect` foi introduzido nesta F1.

## Build

O primeiro `dotnet build -v q -nologo` foi bloqueado pela permissão de leitura do `NuGet.Config` do perfil. Com `APPDATA` redirecionado para uma pasta temporária, esse comando completou com 0 erros, mas o restore reportou NU1900 porque o acesso de rede ao índice NuGet está bloqueado. O projeto declara não ter dependências NuGet; na recompilação offline, `dotnet build -t:Rebuild -v q -nologo -p:NuGetAudit=false` completou com **0 erros** e **0 avisos novos de C#**. Foram reportadas 4 ocorrências por duplicação do projeto WPF temporário, correspondentes às mesmas 2 warnings do baseline:

- `MainWindow.xaml.cs:233` — CS8625
- `MainWindow.xaml.cs:1867` — CS8604

O `NuGetAudit=false` removeu os avisos NU1900 ambientais da recompilação offline; não alterou fontes ou configuração do projeto.

## Alterações desta revisão

- `MainWindow.xaml`: confirmei que o `Path` dentro de `MediaPanel` não contém `local:Glass.FallbackStroke`.
- `SettingsPanel.xaml`: confirmei que o `Path` de `PanelCard` não contém `local:Glass.FallbackStroke`.
- `MainWindow.xaml`: preservei o `local:Glass.FallbackStroke="AccentBrush"` do `UndockBtn`.
- As remoções já estavam presentes, então não precisei alterar esses XAMLs. Criei apenas este relatório; não alterei `TASK*.md`, `RELATORIO_F1*.md`, `_refs/`, `versao anterior/` ou `final .exe/`.
