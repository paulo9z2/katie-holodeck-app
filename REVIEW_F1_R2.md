# F1 — RE-REVIEW round 2

**Veredito final: APPROVE**

Escopo revisado no código atual: `Chamfer.cs` e `Glass.cs`. Os dois P1 do round 1 estão corrigidos. Não encontrei regressão introduzida pelos fixes em cache válido, brushes compartilhados, mudança de tema ou tamanho zero.

## P1-1 — cache de `ChamferAdorner`

**Resolvido.** `ChamferAdorner` mantém `_geometry`, `_geoW`, `_geoH` e `_geoC` (`Chamfer.cs:84-87`). Em `OnRender`, a geometria só é reconstruída se ainda não existir ou se largura, altura ou chanfro diferirem da chave armazenada (`Chamfer.cs:110-117`). O adorner é invalidado em carregamento e em `SizeChanged`; `Apply` também o invalida quando as medidas/chanfro são válidos (`Chamfer.cs:34-35, 47-55`).

Tamanho zero ou menor que `2 * c` retorna antes da criação e do desenho da geometria (`Chamfer.cs:101-102`), portanto não passa uma geometria nula ou degenerada ao `DrawGeometry`. O brush e a espessura do `Pen` privado são atualizados separadamente do cache geométrico (`Chamfer.cs:81-83, 103-108`); o adorner só lê/aponta para o brush, sem mutar o `Freezable` compartilhado.

## P1-2 — arcos arredondados da faceta

**Resolvido.** `BuildRounded` limita `rr` ao raio e às dimensões disponíveis, e define o raio da linha central como `R = rr - FacetWidth/2` (`Glass.cs:265-280`). Com `FacetWidth = 1,5` (`Glass.cs:155-163`), `K = 0,75` (`Glass.cs:286`); cada extremo dos quatro arcos está a `0,75` da borda e no círculo do canto, com centros em `(rr,rr)`, `(w-rr,rr)`, `(w-rr,h-rr)` e `(rr,h-rr)` (`Glass.cs:281-292`). Assim, o limite externo do stroke é `R + 0,75 = rr`, sem ultrapassar a silhueta.

O specular começa no mesmo extremo, termina por coordenadas calculadas com o mesmo centro e `R`, e usa esse raio em `AddArc` (`Glass.cs:294-300`). Os `Pen`s da faceta e do specular têm espessura 1,5 px e caps flat (`Glass.cs:157, 162-163`).

## Regressões verificadas

- `RingAdorner` recompõe as geometrias apenas quando W/H/C/R mudam (`Glass.cs:184-185`). A mudança de tema atualiza os brushes vivos por referência a cada render (`Glass.cs:187-196`), sem colocar cor na chave geométrica nem mutar o brush compartilhado. `ChamferAdorner` também usa um `Pen` privado e só aponta para o brush (`Chamfer.cs:81-83, 103-108`).
- `RingAdorner` retorna para dimensões menores que 10 antes de desenhar (`Glass.cs:175-178`). `BuildRounded` zera as geometrias antes de retornar para raio inválido ou raio de faceta degenerado (`Glass.cs:265-280`), impedindo que referências antigas sejam desenhadas depois de uma reconstrução.
- **Ressalva fora dos P1 corrigidos:** `Chamfer.Apply` retorna antes de localizar/invalidate o adorner se `Chamfer.Size` for zero ou incompatível com as dimensões (`Chamfer.cs:37-48`). As ocorrências atuais de `Chamfer.Size` que encontrei estão declaradas no XAML; não encontrei alteração dinâmica correspondente no código. Se o valor passar a ser desligado dinamicamente sem resize ou outra invalidação, a pintura retida pode não ser removida imediatamente. Não atribuo esse caminho ao cache novo nem o uso para reabrir os dois P1.

## Build

O comando exato `dotnet build -v q -nologo` inicialmente foi bloqueado pela permissão de leitura do `NuGet.Config` do perfil Windows. Repeti o mesmo comando com `APPDATA` redirecionado para pasta temporária: build com êxito, **0 erros**, com dois avisos ambientais `NU1900` por indisponibilidade de `https://api.nuget.org/v3/index.json`.

Para forçar compilação limpa sem a auditoria NuGet dependente de rede, rodei `dotnet build -t:Rebuild -v q -nologo -p:NuGetAudit=false`: **0 erros** e **4 avisos**, somente os dois avisos C# de baseline duplicados entre o projeto WPF temporário e o projeto principal:

- `MainWindow.xaml.cs:233` — CS8625
- `MainWindow.xaml.cs:1867` — CS8604

Nenhum arquivo de código foi editado; este relatório é o único arquivo de saída escrito manualmente.
