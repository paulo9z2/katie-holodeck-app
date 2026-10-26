# TASK CAVALEIROS - Katie HoloDeck

## 1. Selo de Origem - Refinar
Problema: badge aparece longe do título, não inline, e no navegador aparece ícone do navegador em vez do app real.
Ação:
- Mover selo para dentro do mesmo TextBlock/Grid do título, inline no fim do texto.
- No PlayerWindow.xaml e MainWindow.xaml, garantir que o ícone seja do processo de mídia real, não do host do navegador.
- Se AUMID == navegador, tentar resolver via MediaSession.UserInfo / AppName do SMTC, fallback para ícone genérico de web.
- Não quebrar clique SetForegroundWindow.

## 2. L-brackets parciais nos cantos
Objetivo: adicionar brackets L parciais em cantos das ilhas, estilo Cyberpunk.
Ação:
- Criar ControlTemplate decorador reutilizável com Path de L-bracket nos 4 cantos.
- Usar cor AccentEdgeBrush, espessura 1px, opacidade 70%.
- Aplicar em MainWindow ilhas: MediaPanel, ChatPanel, SettingsPanel.
- Sem alocação por frame, usar DrawingBrush estático.

## 3. Capa como Tema Global - Toggle
Objetivo: capa da música vira tema do app inteiro + toggle nas configurações.
Ação:
- Estender LivePalette para aplicar cores dominantes em reator, anel, glow e fundo.
- Adicionar toggle em SettingsPanel: "Tema da capa".
- Crossfade 300ms ao trocar faixa.
- Não afetar performance: usar brush cache.

Regras:
- Nunca abrir janela/GUI que atrapalhe jogo do usuário.
- Commits separados por feature.
- Auditar diff antes de commit.
