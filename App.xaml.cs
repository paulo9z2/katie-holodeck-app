using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace KatieHoloDeck
{
    public partial class App : Application
    {
        public App()
        {
            // v8 (hang da UI): desliga o stack de stylus/touch do WPF.
            // Classe conhecida de travamentos do WPF .NET Core (UI ociosa + input morto +
            // janela "não responde") — dotnet/wpf#5642, #9752, #1037 — com workaround
            // OFICIAL "Switch.System.Windows.Input.Stylus.DisableStylusAndTouchSupport".
            // O app é mouse/teclado (desktop); o switch precisa valer ANTES do primeiro input,
            // por isso vive no ctor do App (roda antes de qualquer janela/InputManager).
            // BISECT: AppContext.SetSwitch("Switch.System.Windows.Input.Stylus.DisableStylusAndTouchSupport", true);

            // v9.5.1 (blindagem): storyboard animando Freezable congelado (paleta da capa
            // substituindo Background de botão de título) lançava InvalidOperationException
            // não tratada e MATAVA o app no primeiro hover — 10 dumps WER em 20/10.
            DispatcherUnhandledException += OnDispatcherUnhandled;
        }

        private void OnDispatcherUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Boot (sem MainWindow): deixa morrer — crash de inicialização precisa ser
            // visível e debugável. Em runtime: loga e segue; trigger de hover falho
            // não derruba mais o app inteiro.
            if (MainWindow == null) return;
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "KatieHoloDeck");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "dispatcher.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception.GetType().Name}: {e.Exception.Message}{Environment.NewLine}" +
                    (e.Exception.InnerException != null ? $"INNER: {e.Exception.InnerException}{Environment.NewLine}" : string.Empty) +
                    e.Exception.StackTrace + Environment.NewLine + "---" + Environment.NewLine);
            }
            catch { /* log nunca pode derrubar o app */ }
            e.Handled = true;
        }
    }
}
