# TASK — Integração Liquid Glass Oyenza (branch liquid-glass-oyenza)

**Origem:** port do pipeline liquid glass do Poni/Oyenza — nota do cofre
"KatieHoloDeck - Integracao Liquid Glass Oyenza (26-10-2026)". Pedido do sir:
nova UI WinUI3+liquid no código fonte da pasta de entrega.

## Veredito de pesquisa (26/10, KATIE)

**WinUI 3 real segue DESCARTADO.** WASDK 2.0 (2026) trouxe `SystemBackdropElement`
(janela transparente + material in-app), MAS:

- #11134 (out/2026, aberta): islands/interop seguem com background opaco enforced;
  só pedem *output transparency* — não resolve holo overlay.
- #10746: per-pixel click-through sem API nativa até hoje.
- Windows App SDK = NuGet + runtime — quebra o zero-NuGet/offline do app (csproj v9.8.2).

Rota mantida (aprovada 11/10): **WPF + estética WinUI 3 via DWM (DwmCompat já
aplica Mica/Acrylic no boot, linha 476) + liquid glass.** "WinUI3+liquid" aqui
significa o visual, não o framework.

## Estado (26/10 ~19:50)

- main: T3.3 CPU glass commitado (099ed08) — LUT Snell, bilinear 3-canais,
  remount-on-resize, failsafe 650ms. Entregue em final .exe 25/10 18:25.
- branch liquid-glass-oyenza: `Effects/Oyenza/GlassMath.cs` **portado pra stdlib**
  (correções: `MathF.Lerp` não existe em .NET 8 → helper Lerp; `float2` é HLSL →
  Vector2; 5 using Vortice não usados nos corpos → removidos). `glass.hlsl`
  copiado como referência do shader GPU.
- Spike standalone: `Downloads/katie-holodeck-liquid-glass/src/` (csproj com
  Vortice; **nunca buildado** — sem SDK .NET 8 até hoje, instalando agora).

## Próximos passos (ordem do sir, nota 26/10)

1. Instalar SDK .NET 8 (em andamento) e buildar spike standalone → validar
   swap chain flip-model + janela com glass (GetSwapChain/RenderFrame).
2. lightMix dinâmico + fallback CPU quando shader falha.
3. Decidir ROTA DE ENTRADA no app (debate — fase grande, relé):
   - **A. Vortice.Windows no app** — port direto, mas quebra zero-NuGet/offline.
   - **B. P/Invoke puro (D3D11/D2D/DXGI/DComp)** — fiel à doutrina, muito interop.
   - **C. CPU T3.3 como default + GPU opt-in** — ship hoje, GPU depois.
   Recomendação inicial: validar no spike (A, é lab), decidir B vs C pro app
   com opinião dos cavaleiros.
4. Hospedar no KatieHoloDeck via HwndHost/D3DImage no MediaGlassHost; fallback
   = LiquidGlassCpu (T3.3) já em produção.
5. Parâmetros por página (GlassParams.Thickness por altura da island).

## Regras

- Build 0/0 antes de considerar fase done; main sempre verde.
- Sem launch de app com jogo aberto (regra do sir).
- Commit por responsabilidade; sem push sem build verde.
