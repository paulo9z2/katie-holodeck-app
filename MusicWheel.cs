using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace KatieHoloDeck
{
    /// <summary>
    /// FASE 3 (v9.5.0) — roda audio-reativa desenhada em um único FrameworkElement.
    /// 64 segmentos radiais + 2 anéis girantes (CW/CCW) derivados do corsair_o_tron: o giro é
    /// dirigido pela energia (ápice com a música, repouso no silêncio) — não é mais loop fixo de 10 s.
    /// Gating: SMTC decide a visibilidade; WASAPI decide a forma. Idle com roda oculta = zero timer.
    /// v9.7 (RING SLEEP): três estados — Active (v9.6) → Ember (brasa na pausa/silêncio) → Standby
    /// (sem sessão de mídia: roda invisível, como já é). A brasa é um lerp em C# no tick (nada de
    /// storyboard em pincel de paleta) e NUNCA aloca por frame.
    /// </summary>
    public sealed class MusicWheel : FrameworkElement
    {
        public event Action? Clicked;

        private const int BandCount = 64;

        private readonly float[] _bands = new float[BandCount];
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        private readonly RotateTransform _ringCw = new();
        private readonly RotateTransform _ringCc = new();
        private readonly Pen?[] _pens = new Pen?[10];

        // v9.6 P2: cache do caminho quente do render (zero alocação por frame)
        private readonly Pen?[] _ringPens = new Pen?[16]; // canetas dos anéis por faixa de alpha (passo 16)
        private Color _ringPensAccent = Color.FromArgb(0, 0, 0, 0); // acento com que _ringPens foi criado (sentinela)
        private StreamGeometry? _arcOuterGeo;  // 3 arcos externos (ângulos base 0/120/240)
        private StreamGeometry? _arcInnerGeo;  // 2 arcos internos (ângulos base 0/180)
        private double _arcCx, _arcCy, _arcROuter, _arcRInner; // tamanho para o qual as geometrias foram geradas

        // ===== v9.6: reatividade musical (pulso de beat, giro por energia, brilho por energia) =====
        /// <summary>transform do pulso de beat (1.00 -> 1.06) — RenderTransform, zero re-layout.</summary>
        private readonly ScaleTransform _pulseScale = new(1, 1);
        /// <summary>halo VIVO próprio (gradiente radial nosso) — alpha lerpado pela energia; a paleta fica intocada.</summary>
        private readonly RadialGradientBrush _glowBrush;
        private readonly Stopwatch _clock = new();
        private const int LowBandCount = 10;          // 30–88 Hz: primeiras bands FFT (grave/kick)
        private const double PulseMax = 0.06;         // escala do pulso: 1.00 -> 1.06
        private const double SpinMaxDegPerSec = 110; // giro da roda no ápice de energia
        private const double RingMaxDegPerSec = 40;  // giro dos anéis decorativos no ápice de energia

        // ===== v9.6: limiares do DETECTOR DE BEAT (calibram o que conta como kick; a resposta visual usa PulseMax/SpinMax) =====
        private const double BeatDataTimeout = 0.25;  // s sem pacote novo → energia/beat tratados como zero
        private const double BeatLowFloor = 0.07;     // piso absoluto da energia grave para aceitar um beat
        private const double BeatRearmRatio = 1.10;   // grave < 1.10× a média → detector rearmado
        private const double BeatTriggerRatio = 1.25; // grave > 1.25× a média → dispara o beat
        private const double BeatCooldown = 0.14;     // s mínimos entre dois beats
        private const double BeatSustainRearm = 0.60; // s de grave sustentado que forçam o rearm
        private const double BeatAvgFloor = 0.02;     // piso do denominador ao normalizar o excesso
        private const double BeatExcessSpan = 0.60;   // excesso (grave/média − 1) que satura a força em 1.0
        private const double BeatPulseKick = 32.0;    // impulso na mola por beat (pico ≈ 1.0 → escala 1.06)

        private double _energyRaw;       // energia bruta (TotalPeak do AudioSpectrum)
        private double _energy;           // energia suavizada (attack rápido / release lento)
        private double _lowAvg;           // média móvel das graves = threshold dinâmico do beat
        private double _pulse, _pulseVel; // pulso de beat (mola: sobe rápido, desce com leve overshoot)
        private double _spin, _spinRad;   // giro da roda (graus) — proporcional à energia
        private double _ringBoost = 1.0;  // hover acelera os anéis
        private byte _glowA0, _glowA1;    // cache do alpha do halo (não reescreve o pincel todo frame)
        private Color _glowAccent;        // acento com que o halo foi desenhado (atualiza só a parada de alpha 0)
        private double _lastTickTime;     // instante do último tick no relógio monotônico (segundos)
        private double _lastDataTime = double.NegativeInfinity; // relógio monotônico do último pacote de bands
        private double _lastBeatTime = double.NegativeInfinity; // relógio monotônico do último beat disparado
        private bool _beatArmed = true;   // o grave voltou pro chão? (evita 2-3 beats no mesmo kick)

        private Color _accent = Color.FromRgb(0x00, 0xE5, 0xFF);
        private bool _enabled = true;
        private bool _playing;
        private bool _hover;
        private bool _ringsVisible = true;
        private bool _pensDirty = true;
        private double _penThickness = 15.0;

        // ===== v9.7 RING SLEEP: Active → Ember (brasa na pausa) → Standby (sem sessão) =====
        /// <summary>estado EMBER: existe sessão de mídia, mas não há áudio (pausa/silêncio).</summary>
        private bool _ember;
        /// <summary>existe sessão de mídia (transportado SEPARADO do AUMID: sessão válida pode ter AUMID "").</summary>
        private bool _hasSession;
        /// <summary>v9.8.2: a sessão atual JÁ tocou de verdade nesta execução. Brasa/Ember só existe
        /// depois de uma pausa real — sessão fantasma (browser/jogo parado, nunca tocou) fica invisível.</summary>
        private bool _playedThisSession;
        /// <summary>0..1 — quanto da BRASA está aceso (lerp em C# no tick; nada de storyboard).</summary>
        private double _emberLevel;
        /// <summary>fase da respiração lenta da brasa (rad).</summary>
        private double _emberPhase;
        private const double EmberInTau = 0.35;       // s — a brasa acende devagar (brisa, não apaga)
        private const double EmberOutTau = 0.10;      // s — acorda em ~150-300 ms (spec do sir)
        private const double EmberBreathAmp = 0.02;   // 2% da escala (spec: < 5%)
        private const double EmberBreathPeriod = 5.0; // s — respiração LENTA (o anel hibernando)
        private const double EmberGlowFloor = 0.12;   // 12% do brilho normal (spec: 10-15%)

        public MusicWheel()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            Focusable = false;
            Opacity = 0;
            Visibility = Visibility.Visible;
            IsHitTestVisible = false;
            Cursor = Cursors.Arrow;

            // pulso de beat: escala do elemento INTEIRO (RenderTransform = zero re-layout por frame)
            RenderTransform = _pulseScale;
            RenderTransformOrigin = new Point(0.5, 0.5);

            // halo vivo próprio (pincel NOSSO — a paleta congelada nunca é mutada)
            _glowBrush = new RadialGradientBrush { Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5 };
            _glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0x00, 0xE5, 0xFF), 0));
            _glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0x00, 0xE5, 0xFF), 0.55));
            _glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0x00, 0xE5, 0xFF), 1));

            _clock.Start();
            _timer.Tick += (_, _) =>
            {
                // v9.7: a brasa também é "viva" (o anel hibernando) — no standby o timer nem roda.
                if (!_enabled) return;
                if (!_playing && !_ember) return;
                UpdateMusicState();
                InvalidateVisual();
            };
            SetRingBoost(false);
        }

        public void StopTimer() => _timer.Stop(); // Codex P2 v9.5.0: PlayerWindow modeless pode segurar o app após fechar a principal

        public void SetEnabled(bool enabled)
        {
            if (_enabled == enabled) return;
            _enabled = enabled;

            if (!enabled)
            {
                SetPlaying(false);
                Visibility = Visibility.Collapsed;
                IsHitTestVisible = false;
            }
            else
            {
                Visibility = Visibility.Visible;
            }

            InvalidateVisual();
        }

        public void SetPlaying(bool playing)
        {
            if (!_enabled && playing) return;

            // ===== ACTIVE: tem áudio (comportamento v9.6) =====
            // v9.8.2 (ALVO): a visibilidade volta a ter UM dono — audioLive (spectrum.HasAudio ||
            // IsPlaying) em MainWindow. O gate "playing && !_hasSession" da v9.7 matava a roda
            // quando o WASAPI tinha áudio mas o SMTC não expunha sessão (sintomas 2/3/5).
            if (playing)
            {
                _playedThisSession = true;
                if (_playing && !_ember)
                {
                    if (!_timer.IsEnabled) _timer.Start();
                    return;
                }

                _playing = true;
                _ember = false; // v9.7: voltou a ter áudio → acorda da brasa (150-300 ms)
                ShowWheel();
                return;
            }

            // ===== EMBER: só na PAUSA REAL (sessão viva + já tocou) → a roda NÃO apaga =====
            // Sessão fantasma que nunca tocou cai no STANDBY (ALVO: invisível sem música).
            if (_hasSession && _playedThisSession && _enabled)
            {
                if (!_ember)
                {
                    bool wasActive = _playing;
                    _playing = false;
                    _ember = true;
                    // nasce JÁ como brasa (appearing from standby) ou esfria devagar (pausa)
                    if (!wasActive) _emberLevel = 1.0;
                    ShowWheel();
                }
                else if (!_timer.IsEnabled) _timer.Start();
                InvalidateVisual();
                return;
            }

            // ===== STANDBY: sem áudio e sem sessão → a roda some (comportamento v9.6, zero timer) =====
            GoStandby();

            InvalidateVisual();
        }

        /// <summary>Roda visível (Active ou Ember): opacidade 1, hit-test e timer ligado.</summary>
        private void ShowWheel()
        {
            Visibility = Visibility.Visible;
            IsHitTestVisible = true;
            Cursor = Cursors.Hand;
            _lastTickTime = _clock.Elapsed.TotalSeconds; // relógio monotônico não reinicia (frescor sobrevive ao stop)
            _timer.Start();
            SetRingBoost(_hover);
            AnimateOpacity(1);
            InvalidateVisual();
        }

        /// <summary>v9.7 P1: STANDBY — sem sessão de mídia a roda não existe: Active e Ember caem juntos,
        /// o timer para e o estado zera. Idempotente (só age se a roda estiver viva).</summary>
        private void GoStandby()
        {
            bool wasVisible = _playing || _ember;
            _playing = false;
            _ember = false;
            _playedThisSession = false; // v9.8.2: perdida a sessão, a próxima só acende se tocar de verdade
            if (!wasVisible) return;

            _hover = false;
            IsHitTestVisible = false;
            Cursor = Cursors.Arrow;
            _timer.Stop();
            AnimateOpacity(0);
            SetRingBoost(false);

            ResetMusicState();
        }

        /// <summary>v9.7: a sessão de mídia chega em canal PRÓPRIO (independente do AUMID do selo origem).
        /// Sessão válida com AUMID vazio continua indo para a brasa; perder a sessão = STANDBY imediato
        /// (mesmo que o áudio WASAPI ainda esteja residualmente vivo).</summary>
        public void SetHasSession(bool has)
        {
            if (has == _hasSession) return;
            _hasSession = has;

            // a sessão de mídia morreu: Active e Ember não podem sobreviver sem dono → STANDBY
            if (!has)
            {
                GoStandby();
                InvalidateVisual();
            }
        }

        /// <summary>bands FFT (64) + energia total do spectrum (0..1) — a energia alimenta giro e brilho.</summary>
        public void SetBands(float[] bands, float totalPeak = 0f)
        {
            int n = Math.Min(BandCount, bands.Length);
            Array.Copy(bands, _bands, n);
            if (n < BandCount) Array.Clear(_bands, n, BandCount - n);
            _energyRaw = Math.Clamp(totalPeak, 0f, 1f);
            _lastDataTime = _clock.Elapsed.TotalSeconds; // timestamp monotônico: avança mesmo com o timer parado
        }

        public void SetAccent(Color color)
        {
            _accent = Color.FromRgb(color.R, color.G, color.B);
            _pensDirty = true;
            _glowA0 = 0; _glowA1 = 0; // força o refresh do halo vivo com a nova cor
            if (_playing || _ember) InvalidateVisual();
        }

        public void SetRingsVisible(bool visible)
        {
            if (_ringsVisible == visible) return;
            _ringsVisible = visible;

            // v9.6: o giro dos anéis passou a ser dirigido pela ENERGIA (UpdateMusicState) —
            // parado no silêncio, vivo com a música. Nada de rotação fixa de enfeite.
            if (visible && (_playing || _ember)) SetRingBoost(_hover);

            if (_playing || _ember) InvalidateVisual();
        }

        private void AnimateOpacity(double to)
        {
            var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new CubicEase { EasingMode = to > 0 ? EasingMode.EaseOut : EasingMode.EaseIn }
            };

            if (to <= 0)
            {
                anim.Completed += (_, _) =>
                {
                    if (_playing) return;
                    ResetMusicState();
                };
            }

            BeginAnimation(OpacityProperty, anim);
        }

        /// <summary>v9.6: o giro dos anéis decorativos é dirigido pela ENERGIA (UpdateMusicState).
        /// O hover só multiplica a velocidade — não existe mais rotação fixa "de enfeite".</summary>
        private void SetRingBoost(bool fast) => _ringBoost = fast ? 2.0 : 1.0;

        // ================= v9.6 — REATIVIDADE MUSICAL (roda viva com a música) =================

        /// <summary>Perfil de efeitos (1.0 completo · 0.5 reduzido · 0 parado) — MainWindow.ApplyEffectsProfile.</summary>
        public double MotionScale { get; set; } = 1.0;

        /// <summary>Tick da roda: suaviza a energia, detecta o beat (mola) e gira o anel pela energia.</summary>
        private void UpdateMusicState()
        {
            // relógio monotônico: continua avançando mesmo com o timer parado (frescor correto no stop/start)
            double now = _clock.Elapsed.TotalSeconds;
            double dt = now - _lastTickTime;
            _lastTickTime = now;
            if (dt <= 0.0005) dt = 0.016;
            if (dt > 0.120) dt = 0.120;

            // dado velho (o áudio parou) = energia zero — a roda volta ao repouso sozinha
            bool stale = (now - _lastDataTime) > BeatDataTimeout;
            double energyTarget = stale ? 0.0 : _energyRaw;
            double lowTarget = stale ? 0.0 : LowBandEnergy();

            // ENERGIA: attack rápido (55 ms) / release lento (420 ms) — sem flicker nervoso
            _energy += (energyTarget - _energy) * Rate(dt, energyTarget > _energy ? 0.055 : 0.420);

            // THRESHOLD DINÂMICO do beat: média móvel (τ 1.2 s) das graves
            _lowAvg += (lowTarget - _lowAvg) * Rate(dt, 1.2);

            // BEAT: grave acima da média (e acima do piso) → impulso na mola.
            // RE-ARM: só dispara de novo quando o grave volta pro chão (ou 0.6 s de grave sustentado).
            // Sem isso, a cauda suavizada (200 ms) do MESMO kick disparava 2-3 beats em sequência.
            if (lowTarget < _lowAvg * BeatRearmRatio || (now - _lastBeatTime) > BeatSustainRearm) _beatArmed = true;
            if (_beatArmed && lowTarget > BeatLowFloor && lowTarget > _lowAvg * BeatTriggerRatio
                && (now - _lastBeatTime) > BeatCooldown)
            {
                _lastBeatTime = now;
                _beatArmed = false;
                double excess = lowTarget / Math.Max(_lowAvg, BeatAvgFloor) - 1.0;
                double strength = Math.Clamp(excess / BeatExcessSpan, 0.0, 1.0);
                _pulseVel += BeatPulseKick * (0.45 + 0.55 * strength); // calibrado: pico do pulso ~1.0 => escala ~1.06
            }

            // MOLA do pulso: sobe rápido, desce com leve overshoot (nunca linear seco)
            const double k = 120.0, c = 16.0;
            _pulseVel += (-k * _pulse - c * _pulseVel) * dt;
            _pulse += _pulseVel * dt;
            if (_pulse < 0) { _pulse = 0; _pulseVel = 0; }
            if (_pulse > 1.15) { _pulse = 1.15; if (_pulseVel > 0) _pulseVel = 0; }

            double motion = MotionScale;

            // ===== v9.7 RING SLEEP: a brasa entra devagar e ACORDA rápido (150-300 ms) =====
            // lerp em C# no tick (nada de storyboard, nada de pincel de paleta, zero alocação)
            // brasa = sessão de mídia viva mas sem áudio (pausa OU silêncio)
            // v9.8.2 (ALVO): brasa SÓ do estado Ember explícito (pausa real, com _playedThisSession).
            // "silêncio" com IsPlaying=true é trecho silencioso — a roda segue visível e apagada,
            // como na v9.6; não vira brasa nem acende sozinha por sessão fantasma.
            double emberTarget = _ember ? 1.0 : 0.0;
            _emberLevel += (emberTarget - _emberLevel) * Rate(dt, emberTarget > _emberLevel ? EmberInTau : EmberOutTau);
            if (_emberLevel <= 0.0005) _emberLevel = 0; else if (_emberLevel >= 0.9995) _emberLevel = 1;

            // respiração da brasa: LENTA (5 s) e MUITO sutil (2% < 5% da spec) — transform-only
            if (_emberLevel > 0.001)
            {
                _emberPhase += dt * (2.0 * Math.PI / EmberBreathPeriod);
                if (_emberPhase > 2.0 * Math.PI) _emberPhase -= 2.0 * Math.PI;
            }
            double breath = 0.5 + 0.5 * Math.Sin(_emberPhase);

            _pulseScale.ScaleX = _pulseScale.ScaleY = 1.0 + PulseMax * _pulse * motion
                                 + EmberBreathAmp * _emberLevel * breath * motion;

            // GIRO: velocidade proporcional à energia (silêncio = giro mínimo)
            _spin = (_spin + SpinMaxDegPerSec * _energy * motion * dt) % 360.0;
            _spinRad = _spin * Math.PI / 180.0;

            // ANÉIS DECORATIVOS: giram com a energia também (silêncio = quase parados)
            double ringSpeed = (2.0 + RingMaxDegPerSec * _energy) * _ringBoost * motion;
            _ringCw.Angle = (_ringCw.Angle + ringSpeed * dt) % 360.0;
            _ringCc.Angle = (_ringCc.Angle - ringSpeed * 0.72 * dt + 360.0) % 360.0;

            UpdateGlowBrush();
        }

        /// <summary>Energia das primeiras bands FFT (grave) — base do detector de beat.</summary>
        private double LowBandEnergy()
        {
            int n = Math.Min(LowBandCount, BandCount);
            double sum = 0;
            for (int i = 0; i < n; i++) sum += _bands[i];
            return sum / n;
        }

        /// <summary>Brilho por energia: alpha do halo vivo lerpado (pincel NOSSO — a paleta fica intocada).
        /// v9.7: na brasa o halo cai para ~12% do normal (spec: 10-15%).</summary>
        private void UpdateGlowBrush()
        {
            double em = _emberLevel;
            byte a0 = (byte)Math.Round(Math.Clamp((0.05 + 0.55 * _energy) * (1.0 - em) + 0.60 * EmberGlowFloor * em, 0, 1) * 255);
            byte a1 = (byte)Math.Round(Math.Clamp((0.015 + 0.20 * _energy) * (1.0 - em) + 0.215 * EmberGlowFloor * em, 0, 1) * 255);

            // acento mudou: atualiza SÓ a parada de alpha zero (as outras duas são tratadas por alpha abaixo)
            if (_accent != _glowAccent)
            {
                _glowAccent = _accent;
                _glowBrush.GradientStops[2].Color = Color.FromArgb(0, _accent.R, _accent.G, _accent.B);
                _glowA0 = 0;
                _glowA1 = 0;
            }

            // cada parada é comparada/atualizada de forma independente — só reescreve a que mudou
            if (a0 != _glowA0)
            {
                _glowA0 = a0;
                _glowBrush.GradientStops[0].Color = Color.FromArgb(a0, _accent.R, _accent.G, _accent.B);
            }

            if (a1 != _glowA1)
            {
                _glowA1 = a1;
                _glowBrush.GradientStops[1].Color = Color.FromArgb(a1, _accent.R, _accent.G, _accent.B);
            }
        }

        /// <summary>Repouso: sem música a roda fica estática (nunca idle-motion próprio).</summary>
        private void ResetMusicState()
        {
            _pulse = 0;
            _pulseVel = 0;
            _pulseScale.ScaleX = _pulseScale.ScaleY = 1.0;
            _energy = 0;
            _energyRaw = 0;
            _lowAvg = 0;
            _spin = 0;
            _spinRad = 0;
            _beatArmed = true;

            // v9.7: standby limpa a brasa (a roda some de vez)
            _emberLevel = 0;
            _emberPhase = 0;

            // P2 frescor stop/start: sem bandas residuais e sem dado "recente" até chegar um pacote NOVO
            Array.Clear(_bands, 0, BandCount);
            _lastDataTime = double.NegativeInfinity;
            _lastBeatTime = double.NegativeInfinity;
        }

        private static double Rate(double dt, double tau) => 1.0 - Math.Exp(-dt / Math.Max(tau, 0.001));

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (!_enabled || Opacity <= 0.01) return;

            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            double cx = w / 2, cy = h / 2;
            double maxR = Math.Min(w, h) / 2;
            double segInner = maxR - 66;
            double segOuter = maxR - 22;
            if (segInner < 40 || segOuter <= segInner) return;

            // v9.7: a BRASA segura o brilho em ~12% do normal (spec: 10-15%)
            double em = _emberLevel;

            // halo de música (MusicGlowBrush da paleta — a cor do pincel congelado NUNCA é animada;
            // a energia só modula a OPACIDADE do desenho via PushOpacity)
            if (TryFindResource("MusicGlowBrush") is Brush glow)
            {
                dc.PushOpacity((0.30 + 0.70 * _energy) * (1.0 - em) + EmberGlowFloor * em);
                dc.DrawEllipse(glow, null, new Point(cx, cy), maxR - 8, maxR - 8);
                dc.Pop();
            }

            // halo extra VIVO (pincel NOSSO, alpha lerpado pela energia — brilho sobe com a música)
            dc.DrawEllipse(_glowBrush, null, new Point(cx, cy), maxR - 14, maxR - 14);

            // ===== 64 segmentos radiais (giro pela energia) =====
            double thickness = Math.Max(4.0, 2 * Math.PI * segInner / BandCount * 0.72);
            EnsurePens(thickness);

            double step = Math.PI * 2 / BandCount;
            double span = step * 0.84; // gap fino (~6 px visual no corsair)

            // a roda inteira gira com a energia: música parada = roda parada
            // (v9.7: na brasa os segmentos ficam esmaecidos e mais curtos)
            dc.PushOpacity((0.60 + 0.40 * _energy) * (1.0 - em) + 0.15 * em);
            for (int i = 0; i < BandCount; i++)
            {
                float v = Math.Clamp(_bands[i], 0f, 1f);
                int level = (int)Math.Round(v * (_pens.Length - 1));
                var pen = _pens[level];
                if (pen is null) continue;

                double angle = -Math.PI / 2 + i * step + (step - span) / 2 + _spinRad;
                double len = 6 + v * (segOuter - segInner - 6) * (1.0 - 0.5 * em);
                var p0 = PointAt(cx, cy, segInner, angle);
                var p1 = PointAt(cx, cy, segInner + len, angle);
                dc.DrawLine(pen, p0, p1);
            }
            dc.Pop();

            // ===== 2 anéis decorativos girando (CW/CCW) =====
            if (_ringsVisible)
            {
                // P2b: todas as canetas (faixas de alpha) e geometrias já existem; aqui só SELECIONA a faixa
                // v9.7: na brasa os anéis decorativos também caem para ~12% do normal
                Pen ringPen = EnsureRingPen(QuantizeAlpha((0x30 + 0x66 * _energy) * (1.0 - em) + 0x96 * EmberGlowFloor * em));

                double rOuter = maxR - 3;
                double rInner = maxR - 12;
                EnsureArcGeometries(cx, cy, rOuter, rInner);

                // a geometria externa já contém os 3 arcos (0/120/240): girar o conjunto = girar cada arco
                _ringCw.CenterX = cx;
                _ringCw.CenterY = cy;
                dc.PushTransform(_ringCw);
                dc.DrawGeometry(null, ringPen, _arcOuterGeo);
                dc.Pop();

                // a geometria interna já contém os 2 arcos (0/180)
                _ringCc.CenterX = cx;
                _ringCc.CenterY = cy;
                dc.PushTransform(_ringCc);
                dc.DrawGeometry(null, ringPen, _arcInnerGeo);
                dc.Pop();
            }
        }

        private void EnsurePens(double thickness)
        {
            if (!_pensDirty && Math.Abs(thickness - _penThickness) < 0.5) return;

            _penThickness = thickness;
            _pensDirty = false;

            for (int i = 0; i < _pens.Length; i++)
            {
                double t = i / (double)(_pens.Length - 1);
                double alpha = (0.28 + 0.72 * t) * (_hover ? 1.15 : 1.0);
                byte a = (byte)Math.Clamp(alpha * 255.0, 0.0, 255.0);

                var brush = new SolidColorBrush(Color.FromArgb(a, _accent.R, _accent.G, _accent.B));
                brush.Freeze();

                var pen = new Pen(brush, thickness)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                pen.Freeze();
                _pens[i] = pen;
            }
        }

        /// <summary>Faixa de alpha (passo 16) — índice do array de canetas dos anéis.</summary>
        private static byte QuantizeAlpha(double alpha)
            => (byte)Math.Clamp((int)Math.Round(alpha / 16.0) * 16, 0, 255);

        /// <summary>Caneta dos anéis: as 16 faixas de alpha (passo 16) são criadas DE UMA VEZ quando o
        /// acento muda; no caminho quente isto é só um indexador — zero alocação por frame.</summary>
        private Pen EnsureRingPen(byte alpha)
        {
            if (_ringPens[0] is null || _accent != _ringPensAccent)
            {
                _ringPensAccent = _accent;
                for (int i = 0; i < _ringPens.Length; i++)
                {
                    var brush = new SolidColorBrush(Color.FromArgb((byte)(i * 16), _accent.R, _accent.G, _accent.B));
                    brush.Freeze();

                    var pen = new Pen(brush, 1.25)
                    {
                        StartLineCap = PenLineCap.Round,
                        EndLineCap = PenLineCap.Round
                    };
                    pen.Freeze();
                    _ringPens[i] = pen;
                }
            }

            return _ringPens[alpha >> 4]!;
        }

        /// <summary>Geometrias dos anéis (coordenadas absolutas): reconstruídas só quando o tamanho muda.</summary>
        private void EnsureArcGeometries(double cx, double cy, double rOuter, double rInner)
        {
            if (_arcOuterGeo is not null && cx == _arcCx && cy == _arcCy
                && rOuter == _arcROuter && rInner == _arcRInner) return;

            _arcCx = cx;
            _arcCy = cy;
            _arcROuter = rOuter;
            _arcRInner = rInner;
            _arcOuterGeo = BuildArcRing(cx, cy, rOuter, 3, 88);
            _arcInnerGeo = BuildArcRing(cx, cy, rInner, 2, 132);
        }

        /// <summary>Um StreamGeometry com todos os arcos do anel em ângulos base (giro fica no transform).</summary>
        private static StreamGeometry BuildArcRing(double cx, double cy, double radius, int count, double sweepDeg)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                double sweep = sweepDeg * Math.PI / 180.0;
                for (int i = 0; i < count; i++)
                {
                    double start = i * (Math.PI * 2 / count);
                    var p0 = PointAt(cx, cy, radius, start);
                    var p1 = PointAt(cx, cy, radius, start + sweep);
                    ctx.BeginFigure(p0, false, false);
                    ctx.ArcTo(p1, new Size(radius, radius), 0, sweepDeg > 180,
                        SweepDirection.Clockwise, true, false);
                }
            }
            geo.Freeze();
            return geo;
        }

        private static Point PointAt(double cx, double cy, double radius, double angle)
            => new(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));

        protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
        {
            // v9.7: a brasa continua clicável (abre o player como a roda ativa)
            if (!_enabled || (!_playing && !_ember) || Opacity < 0.05) return null;

            double maxR = Math.Min(ActualWidth, ActualHeight) / 2;
            double inner = maxR - 76;
            double dx = hitTestParameters.HitPoint.X - ActualWidth / 2;
            double dy = hitTestParameters.HitPoint.Y - ActualHeight / 2;
            double r = Math.Sqrt(dx * dx + dy * dy);

            return r >= inner && r <= maxR
                ? new PointHitTestResult(this, hitTestParameters.HitPoint)
                : null;
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            _pensDirty = true;
            SetRingBoost(true);
            if (_playing) InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            _pensDirty = true;
            SetRingBoost(false);
            if (_playing) InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!_enabled || (!_playing && !_ember)) return;
            Clicked?.Invoke();
            e.Handled = true;
        }
    }
}
