using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace KatieHoloDeck
{
    public enum GameActivityState
    {
        /// <summary>Nenhum perfil habilitado casou com os processos abertos.</summary>
        None,
        /// <summary>Processo do jogo aberto (pode estar em segundo plano).</summary>
        Running,
        /// <summary>Processo do jogo aberto E com a janela em primeiro plano.</summary>
        Foreground,
        /// <summary>Não foi possível ler os processos (permissão/erro).</summary>
        Unavailable
    }

    public sealed record GameActivity(GameActivityState State, string? GameName, string? ProfileId, DateTime? SinceUtc);

    /// <summary>
    /// Detecção LOCAL e opcional de jogos — sem admin, sem hooks, sem ler memória,
    /// nada sai da máquina. Casa o NOME do processo (exato, case-insensitive, sem
    /// ".exe") com os perfis configurados. "Jogando agora" só quando o PID do
    /// processo em primeiro plano também casa — nunca afirma foco sem evidência.
    /// A varredura roda em background (Task), com cancelamento e intervalo próprio.
    /// </summary>
    public sealed class GameDetectionService : IDisposable
    {
        private readonly Func<IReadOnlyList<GameProfile>> _profiles;
        private CancellationTokenSource? _cts;
        private string? _currentProfileId;
        private DateTime _sinceUtc;

        /// <summary>Disparado a cada varredura (thread de background — o consumidor marshala).</summary>
        public event Action<GameActivity>? Updated;

        public GameDetectionService(Func<IReadOnlyList<GameProfile>> profiles) => _profiles = profiles;

        public bool IsRunning => _cts != null;

        public void Start(TimeSpan interval)
        {
            Stop();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    var act = ScanOnce(_profiles());
                    if (!ct.IsCancellationRequested) Updated?.Invoke(act);
                    try { await Task.Delay(interval, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
            }, ct);
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            _cts?.Dispose();
            _cts = null;
            _currentProfileId = null;
        }

        /// <summary>Varredura única (síncrona) — usada no botão "testar agora".</summary>
        public GameActivity ScanOnce(IReadOnlyList<GameProfile> profiles)
        {
            try
            {
                var lookup = new Dictionary<string, GameProfile>(StringComparer.OrdinalIgnoreCase);
                foreach (var g in profiles)
                {
                    if (!g.Enabled) continue;
                    foreach (var p in g.Processes)
                        if (!lookup.ContainsKey(p)) lookup[p] = g;
                }
                if (lookup.Count == 0)
                {
                    _currentProfileId = null;
                    return new GameActivity(GameActivityState.None, null, null, null);
                }

                Process[] procs;
                try { procs = Process.GetProcesses(); }
                catch { return new GameActivity(GameActivityState.Unavailable, null, null, null); }

                var matches = new List<(GameProfile Game, int Pid)>();
                foreach (var p in procs)
                {
                    try
                    {
                        // Só o NOME do processo: não exige elevação nem acessa memória.
                        var norm = SettingsService.NormalizeProcess(p.ProcessName);
                        if (norm.Length > 0 && lookup.TryGetValue(norm, out var g))
                            matches.Add((g, p.Id));
                    }
                    catch { /* processo protegido/morreu no meio: ignora */ }
                    finally { p.Dispose(); }
                }

                if (matches.Count == 0)
                {
                    _currentProfileId = null;
                    return new GameActivity(GameActivityState.None, null, null, null);
                }

                // Foco: janela em primeiro plano → PID; senão, prioridade + nome (determinístico).
                int fgPid = GetForegroundPid();
                var fg = fgPid != 0 ? matches.FirstOrDefault(m => m.Pid == fgPid) : default;
                var chosen = fg.Game != null
                    ? fg
                    : matches
                        .OrderBy(m => m.Game.Priority)
                        .ThenBy(m => m.Game.Name, StringComparer.OrdinalIgnoreCase)
                        .First();

                if (_currentProfileId != chosen.Game.Id)
                {
                    _currentProfileId = chosen.Game.Id;
                    _sinceUtc = DateTime.UtcNow;
                }

                return new GameActivity(
                    fg.Game != null ? GameActivityState.Foreground : GameActivityState.Running,
                    chosen.Game.Name,
                    chosen.Game.Id,
                    _sinceUtc);
            }
            catch
            {
                return new GameActivity(GameActivityState.Unavailable, null, null, null);
            }
        }

        private static int GetForegroundPid()
        {
            try
            {
                IntPtr h = GetForegroundWindow();
                if (h == IntPtr.Zero) return 0;
                _ = GetWindowThreadProcessId(h, out uint pid);
                return (int)pid;
            }
            catch { return 0; }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public void Dispose() => Stop();
    }
}
