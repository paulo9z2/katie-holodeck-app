# Parecer de revisão — F2.0 + F2.1

Commit analisado: `7da8793` — `F2.0+F2.1`

## Veredito: REPROVADO

### Problemas

- **P1 — fonte única não é estrita — `Core/AppInfo.cs:20`.** O fallback `"v0.0.0"` é um literal de versão em C#. Se `AssemblyName.Version` vier nulo, a UI passa a exibir uma versão que não vem do `.csproj`. O gate proíbe qualquer versão manual em C#/XAML; substitua por um comportamento sem versão fixa.

- **P2 — gate de busca da versão falha — `Glass.cs:34`, `MainWindow.xaml.cs:841`, `MusicWheel.cs:87,158,220,354`, `SettingsPanel.xaml:332`, `SettingsService.cs:79`.** `git grep` ainda encontra `9.8.2` nesses comentários históricos, além do `.csproj`. Eles não são fontes da versão exibida, mas o gate literal do TASK_F2A (“única ocorrência [...] é o csproj”) não passa. As ocorrências citadas já existiam antes deste commit.

### Verificações aprovadas

- **Versão:** `<Version>9.8.2</Version>` está correta e não há bump acima dela. `AppInfo` consulta `Assembly.GetExecutingAssembly().GetName().Version`; Settings e barra consomem a mesma `VersionString`. `x:Static core:AppInfo.VersionString` compilou.
- **Status:** as quatro referências indicadas foram migradas para `StatusText` (`MainWindow.xaml.cs:731,1512,2160,2369`); o default está vazio e o texto tutorial foi removido.
- **Barra:** o trecho do Ellipse e seu DropShadowEffect não aparece alterado no diff. O `SharedSizeGroup` está aplicado às duas colunas laterais dentro do próprio `Grid.IsSharedSizeScope`, mantendo o espelho simétrico. As chaves `GlassEdgeBrush`, `ThemeLabelBrush`, `ThemeSecondaryBrush` e `ThemeSecondaryColor` existem. Não foi introduzido aro, blur ou Effect novo; a hairline usa o recurso neutro.
- **Legibilidade/HIG:** versão e status usam `ThemeLabelBrush`, sem cor de acento; a versão está em 11 px. O contraste é coerente com a recomendação sobre a shell escura; não fiz medição de pixels em execução. Minha opinião anterior sugeria Consolas para a versão, mas a Segoe UI Variable Text usada aqui é legível e também é permitida pelo TASK_F2A.

### Build e runtime

`dotnet build --no-restore` no destino padrão não conseguiu substituir `bin/.../KatieHoloDeck.exe`, pois o arquivo está aberto por outro processo. Não interagi com essa instância. Repeti a compilação com saída isolada em `%TEMP%\F2AReview`: **0 erros, 0 avisos**. Não executei o app nem validei visualmente as larguras/DPI.

Não encontrei referência dinâmica inexistente, `x:Static` incorreto, escopo de tamanhos compartilhados quebrado ou nome de status inconsistente. A reprovação decorre dos dois gates de fonte única acima.

