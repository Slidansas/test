using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using AROKIS.Backend.Services;
using Photino.Blazor.AROKIS.AppConfig;
using AppConfigModel = Photino.Blazor.AROKIS.AppConfig.AppConfig;

namespace Photino.Blazor.AROKIS;

public partial class Window1 : Window
{
    private ArokisInterop     _interop;
    private ControllerService _controllerService;

    // Ключ реестра Windows, хранящий настройку темы приложений
    private const string ThemeRegKey   = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ThemeRegValue = "AppsUseLightTheme";

    [STAThread]
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var app = new Application();
        app.Run(new Window1());
    }

    public Window1()
    {
        InitializeComponent();

        var (settings, config) = ConfigManager.LoadConfig();

        var data   = new DataStorageService();
        var stream = new StreamUpdateService(data);
        var shape  = new ShapeGenerator(data);
        var api    = new ArokisApi(shape, stream, data, settings);

        _controllerService = new ControllerService(stream);
        _interop           = new ArokisInterop(api, settings, _controllerService, config);

        Loaded += async (s, e) =>
        {
            await WebView.EnsureCoreWebView2Async();
            WebView.CoreWebView2.AddHostObjectToScript("arokis", _interop);

            var path = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
            WebView.CoreWebView2.Navigate(new Uri(path).AbsoluteUri);

            // Применить тему Windows после каждой навигации (смена страниц).
            // fromSystem=false — не сбрасываем ручной выбор пользователя.
            WebView.CoreWebView2.NavigationCompleted += (_, _) => ApplyTheme(fromSystem: false);

            // Слушать изменения темы Windows во время работы приложения
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            Closed += (_, _) => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

            await _interop.AutoConnectAsync();
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Определение текущей темы Windows
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Читает реестр Windows.
    /// AppsUseLightTheme == 0  →  тёмная тема.
    /// AppsUseLightTheme == 1  →  светлая тема (по умолчанию).
    /// </summary>
    private static bool IsWindowsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ThemeRegKey);
            return key?.GetValue(ThemeRegValue) is int v && v == 0;
        }
        catch { return false; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Применение темы в WebView2
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Применяет тему в WebView2.
    /// fromSystem=true — только при реальной смене системной темы Windows
    ///   (сбрасывает ручной выбор пользователя).
    /// fromSystem=false — при навигации между страницами
    ///   (уважает ручной выбор: если пользователь выбрал тему вручную — не трогаем).
    /// </summary>
    private void ApplyTheme(bool fromSystem = false)
    {
        if (WebView?.CoreWebView2 == null) return;

        string theme = IsWindowsDark() ? "dark" : "light";
        string flag  = fromSystem ? "true" : "false";
        // При навигации: если в localStorage есть ручной выбор — JS его сохранит.
        // fromSystem=true только при реальном изменении темы Windows.
        string js = $"window.__arokisSetTheme && window.__arokisSetTheme('{theme}', {flag});";
        _ = WebView.CoreWebView2.ExecuteScriptAsync(js);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Обработчик системного события смены темы
    // ─────────────────────────────────────────────────────────────────────────

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // Категория General включает изменения темы оформления Windows
        if (e.Category == UserPreferenceCategory.General)
        {
            // Событие может прийти из фонового потока — диспетчеризуем в UI
            Dispatcher.BeginInvoke(() => ApplyTheme(fromSystem: true));
        }
    }
}
