# Glifos — Segoe MDL2 Assets · KatieHoloDeck

> **Fonte:** tabela oficial da Microsoft **"Segoe MDL2 Assets icons"** —
> `https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-ui-symbol-font`
> |atenção| a URL antiga `.../style/segoe-ui-icon-font` (citada em prompts) **dá 404** —
> a página viva hoje é a `segoe-ui-symbol-font`.
>
> **Verificação dupla (11/10/2026):**
> 1. cada codepoint foi conferido na tabela oficial (parser da página inteira, 1386 glifos);
> 2. cada codepoint foi **renderizado na máquina** (Win11 26H2 build 26300, `segmdl2.ttf`,
>    Segoe MDL2 Assets presente) e comparado por hash de pixels — **zero tofu** em todos
>    os 32 codepoints listados abaixo.
>
> **Regra de XAML:** glifo de fonte SEMPRE como **entidade numérica**:
> `Text="&#xE768;"` — **nunca** `\uE768` (o parser de XAML do WPF não interpreta e
> renderiza o texto literal).
>
> **Fonte de UI:** usar o estilo `GlyphTextStyle` (`FluentControls.xaml`): Segoe MDL2
> Assets · 12 px · Foreground herdado. `Segoe Fluent Icons` **não está instalada** —
> tudo abaixo é MDL2 clássico e funciona nela.

---

## 1. Tabela principal — o que o app usa

| codepoint | entidade XAML pronta | nome oficial | uso no app | confirma MDL2? (sim/não) |
|---|---|---|---|---|
| U+E787 | `&#xE787;` | Calendar | chip **agenda** | sim |
| U+E715 | `&#xE715;` | Mail | chip **e-mails** | sim |
| U+EC4F | `&#xEC4F;` | MusicNote | chip **música** | sim |
| U+E768 | `&#xE768;` | Play | player: play | sim |
| U+E769 | `&#xE769;` | Pause | player: pause | sim |
| U+E892 | `&#xE892;` | Previous | faixa anterior | sim |
| U+E893 | `&#xE893;` | Next | próxima faixa | sim |
| U+E767 | `&#xE767;` | Volume | volume (player inline) | sim |
| U+E74F | `&#xE74F;` | Mute | mudo | sim |
| U+E921 | `&#xE921;` | ChromeMinimize | title bar: **minimizar** | sim |
| U+E922 | `&#xE922;` | ChromeMaximize | title bar: **maximizar** | sim |
| U+E923 | `&#xE923;` | ChromeRestore | title bar: **restaurar** (maximizado) | sim |
| U+E8BB | `&#xE8BB;` | ChromeClose | title bar: **fechar** | sim |
| U+E740 | `&#xE740;` | FullScreen | **tela cheia** | sim |
| U+E724 | `&#xE724;` | Send | botão **enviar** (seta) | sim |
| U+E8A7 | `&#xE8A7;` | OpenInNewWindow | **desancorar** o player flutuante | sim |
| U+E70E | `&#xE70E;` | ChevronUp | chevron cima (expansores) | sim |
| U+E70D | `&#xE70D;` | ChevronDown | chevron baixo (expansores) | sim |
| U+EA3B | `&#xEA3B;` | CircleFill | **ponto de status** (bolinha cheia) | sim |

**Nenhum glifo pedido ficou "não"** — todos os itens do briefing existem em MDL2 (nada
de Fluent-Icons-only). Onde havia dúvida ou alternativa melhor, está na seção 2 / mapa
da seção 3.

---

## 2. Alternativas e extras (também MDL2, também verificados)

| codepoint | entidade XAML pronta | nome oficial | para que serve | confirma MDL2? |
|---|---|---|---|---|
| U+E992 | `&#xE992;` | Volume0 | mudo "sem riscado" (alternativa a Mute) | sim |
| U+E993 | `&#xE993;` | Volume1 | volume baixo — pode variar o ícone c/ o nível | sim |
| U+E994 | `&#xE994;` | Volume2 | volume médio | sim |
| U+E995 | `&#xE995;` | Volume3 | volume alto | sim |
| U+EBC5 | `&#xEBC5;` | VolumeBars | "tocando agora" com barras | sim |
| U+E725 | `&#xE725;` | SendFill | enviar preenchido (estado hover/ativo) | sim |
| U+E92D | `&#xE92D;` | ChromeFullScreen | tela cheia "estilo chrome" (alt. a E740) | sim |
| U+E73C | `&#xE73C;` | BackToWindow | **sair** da tela cheia | sim |
| U+EA3A | `&#xEA3A;` | CircleRing | ponto de status **anel** (vazio) | sim |
| U+EA81 | `&#xEA81;` | StatusCircle | ponto de status com relevo interno | sim |
| U+E711 | `&#xE711;` | Cancel | fecha/limpa genérico (alt. a ChromeClose) | sim |
| U+E8BF | `&#xE8BF;` | CalendarDay | agenda "com dia" (p/ badge de data) | sim |
| U+E713 | `&#xE713;` | Settings | engrenagem (config futura) | sim |

---

## 3. Mapa "glifo atual do app → substituição"

| hoje no app | vira | observação |
|---|---|---|
| 📅 (emoji) | `&#xE787;` | chip agenda |
| ✉ (emoji) | `&#xE715;` | chip e-mails |
| ♪ (unicode) | `&#xEC4F;` | chip música |
| ⏮ ⏸ ⏭ (media chars) | `&#xE892;` `&#xE769;` `&#xE768;` | player inline + flutuante (play/pause alternam por código, como hoje) |
| 🔊 (emoji) | `&#xE767;` | volume do player (mudo: `&#xE74F;`) |
| — (em dash) | `&#xE921;` | minimizar |
| ⤢ | `&#xE922;` / `&#xE923;` | maximizar ↔ restaurar (troca por código no MaxBtn_Click, como hoje) |
| ✕ | `&#xE8BB;` | fechar |
| ⤒ | `&#xE8A7;` | desancorar player |

---

## 4. Como usar no XAML

```xml
<!-- chip: glifo + label -->
<StackPanel Orientation="Horizontal">
    <TextBlock Style="{StaticResource GlyphTextStyle}"
               Text="&#xE787;"
               Foreground="#FF00D9FF"
               VerticalAlignment="Center"
               Margin="0,0,7,0"/>
    <TextBlock Text="agenda" FontFamily="Consolas" FontSize="12" Foreground="White"/>
</StackPanel>
```

- `Foreground` **não** vem no estilo (herda do pai) — em fundo escuro, sete explícito
  (`#FF00D9FF`, `#FFFF3B3B`, `#FF00FF88`, `#FF9FB0C8`…).
- Tamanhos: 12 px é o default do estilo; para o player pequeno, `FontSize="9..10"` local.
- Centralização vertical: `VerticalAlignment="Center"` — glifos MDL2 têm baseline alto.

---

## 5. Como conferir um codepoint novo (reproduzível)

1. Página oficial (a viva): `learn.microsoft.com/en-us/windows/apps/design/style/segoe-ui-symbol-font`
   → adicione um `MAIÚSCULAS-4-hex` e busque o nome oficial;
2. Render local (PowerShell, sem libs externas): desenha o codepoint com a fonte
   `Segoe MDL2 Assets` num bitmap e compara com um codepoint sabidamente vazio —
   hash igual = glifo ausente (tofu), hash diferente = glifo presente.
   O script usado nesta verificação (32 codepoints, todos OK) ficou no projeto de
   teste em `%TEMP%\opencode\wpfcheck\` (bancada — fora do repo).

> |nota| MDL2 é uma **fonte privada**: codepoint fora da tabela oficial pode
> simplesmente não existir no `segmdl2.ttf` desta máquina. Sempre verifique antes de
> commitar um glifo novo — o WPF mostra um retângulo ("tofu") silenciosamente.
