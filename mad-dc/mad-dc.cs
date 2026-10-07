using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MadDC
{
    public class GameInfo
    {
        public string SteamId { get; set; } = "";
        public string InstallDir { get; set; } = "";
        public string Exe { get; set; } = "";
        public string ExeRelativePath { get; set; } = "";
        public string Name { get; set; } = "";
        public string Source { get; set; } = ""; // "steamdb", "acf", "steamdb+acf"
    }

    public class AppConfig
    {
        public string SteamPath { get; set; } = @"C:\Program Files (x86)\Steam\steamapps\common";
        public string SteamAppsPath { get; set; } = @"C:\Program Files (x86)\Steam\steamapps";
    }

    public class MainWindow : Window
    {
        private static readonly string ConfigFile = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".mad-dc-config.json");

        private static readonly string AppDataDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "mad-dc");

        private static readonly string StubExePath = System.IO.Path.Combine(AppDataDir, "mad-dc-stub.exe");

        private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
        private AppConfig config;
        private GameInfo? currentGame;
        private bool settingsOpen = false;

        private TextBox steamPathBox = null!;
        private TextBox steamAppsBox = null!;
        private Border settingsPanel = null!;
        private TextBox searchBox = null!;
        private CheckBox steamGameCheckBox = null!;
        private CheckBox nonSteamGameCheckBox = null!;
        private Border gameCard = null!;
        private TextBlock steamIdText = null!;
        private TextBlock installDirText = null!;
        private TextBlock exeNameText = null!;
        private TextBlock exePathText = null!;
        private TextBlock nameText = null!;
        private TextBlock sourceText = null!;
        private Button launchBtn = null!;
        private TextBlock statusText = null!;

        public MainWindow()
        {
            Title = "mad-dc";
            Width = 680;
            Height = 800;
            MinWidth = 600;
            MinHeight = 720;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(0x0b, 0x0d, 0x10));

            http.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");
            http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");

            config = LoadConfig();
            BuildUI();

            _ = Task.Run(() => EnsureStubExe());
        }


        private void EnsureStubExe()
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);
                if (File.Exists(StubExePath) && new FileInfo(StubExePath).Length > 0)
                    return;

                var asm = typeof(MainWindow).Assembly;
                using var stream = asm.GetManifestResourceStream("mad-dc-stub.exe");
                if (stream == null) throw new InvalidOperationException("stub resource not found");

                var tmp = StubExePath + ".tmp";
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    stream.CopyTo(fs);

                if (File.Exists(StubExePath)) File.Delete(StubExePath);
                File.Move(tmp, StubExePath);
            }
            catch
            {
                try { if (File.Exists(StubExePath + ".tmp")) File.Delete(StubExePath + ".tmp"); } catch { }
            }
        }


        private void BuildUI()
        {
            var root = new Grid { Margin = new Thickness(26), Opacity = 0 };
            for (int i = 0; i < 6; i++)
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var logo = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(14),
                Background = new LinearGradientBrush(
                    Color.FromRgb(0x4c, 0x7d, 0xff),
                    Color.FromRgb(0x78, 0x50, 0xe8),
                    new Point(0, 0), new Point(1, 1))
            };
            logo.Child = new TextBlock
            {
                Text = "M",
                FontSize = 20,
                FontWeight = FontWeights.ExtraBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            brand.Children.Add(logo);

            var brandText = new StackPanel { Margin = new Thickness(13, 0, 0, 0) };
            brandText.Children.Add(new TextBlock
            {
                Text = "mad-dc",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            brandText.Children.Add(new TextBlock
            {
                Text = "GAME PRESENCE LAUNCHER",
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6f, 0x7d, 0x94)),
                Margin = new Thickness(1, 1, 0, 0)
            });
            brand.Children.Add(brandText);
            Grid.SetColumn(brand, 0);
            header.Children.Add(brand);

            var settingsBtn = MakeIconButton("⚙");
            settingsBtn.ToolTip = "Einstellungen";
            settingsBtn.Click += (_, _) => ToggleSettings();
            Grid.SetColumn(settingsBtn, 1);
            header.Children.Add(settingsBtn);

            Grid.SetRow(header, 0);
            root.Children.Add(header);

            settingsPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x13, 0x18, 0x22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x2e, 0x3e)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Margin = new Thickness(0, 0, 0, 18),
                Padding = new Thickness(18),
                Visibility = Visibility.Collapsed,
                Opacity = 0
            };

            var settingsStack = new StackPanel();
            settingsStack.Children.Add(new TextBlock
            {
                Text = "STEAM EINSTELLUNGEN",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x78, 0x8a, 0xa5)),
                Margin = new Thickness(0, 0, 0, 12)
            });

            var row1 = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            steamPathBox = MakePathBox(config.SteamPath);
            Grid.SetColumn(steamPathBox, 0);
            row1.Children.Add(steamPathBox);
            var b1 = MakePillButton("auswählen", 0x1a, 0x22, 0x31);
            b1.Width = 105; b1.Height = 42; b1.Margin = new Thickness(9, 0, 0, 0);
            b1.Click += (_, _) => BrowseInto(steamPathBox);
            Grid.SetColumn(b1, 1);
            row1.Children.Add(b1);
            settingsStack.Children.Add(row1);

            var row2 = new Grid();
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            steamAppsBox = MakePathBox(config.SteamAppsPath);
            Grid.SetColumn(steamAppsBox, 0);
            row2.Children.Add(steamAppsBox);
            var b2 = MakePillButton("auswählen", 0x1a, 0x22, 0x31);
            b2.Width = 105; b2.Height = 42; b2.Margin = new Thickness(9, 0, 0, 0);
            b2.Click += (_, _) => BrowseInto(steamAppsBox);
            Grid.SetColumn(b2, 1);
            row2.Children.Add(b2);
            settingsStack.Children.Add(row2);

            var saveRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 13, 0, 0)
            };
            var saveBtn = MakePillButton("einstellungen speichern", 0x3d, 0x69, 0xe8);
            saveBtn.Height = 40;
            saveBtn.Click += (_, _) => SaveSettings();
            saveRow.Children.Add(saveBtn);
            settingsStack.Children.Add(saveRow);

            settingsPanel.Child = settingsStack;
            Grid.SetRow(settingsPanel, 1);
            root.Children.Add(settingsPanel);

            var modeCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x11, 0x16, 0x1e)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x24, 0x2d, 0x3c)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var modeStack = new StackPanel();
            modeStack.Children.Add(new TextBlock
            {
                Text = "PLATTFORM",
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x70, 0x7f, 0x98)),
                Margin = new Thickness(3, 0, 0, 9)
            });

            var platformRow = new StackPanel { Orientation = Orientation.Horizontal };
            steamGameCheckBox = MakePlatformCheckBox("Steam Game", true);
            nonSteamGameCheckBox = MakePlatformCheckBox("Nicht Steam Game", false);

            steamGameCheckBox.Checked += (_, _) =>
            {
                if (nonSteamGameCheckBox.IsChecked == true) nonSteamGameCheckBox.IsChecked = false;
                SetStatus("steam-modus · steam app info wird verwendet");
            };
            steamGameCheckBox.Unchecked += (_, _) =>
            {
                if (nonSteamGameCheckBox.IsChecked != true) SetStatus("bitte Steam Game oder Nicht Steam Game auswählen", true);
            };
            nonSteamGameCheckBox.Checked += (_, _) =>
            {
                if (steamGameCheckBox.IsChecked == true) steamGameCheckBox.IsChecked = false;
                SetStatus("nicht-steam-modus · Discord detectable games");
            };
            nonSteamGameCheckBox.Unchecked += (_, _) =>
            {
                if (steamGameCheckBox.IsChecked != true) SetStatus("bitte Steam Game oder Nicht Steam Game auswählen", true);
            };
            platformRow.Children.Add(steamGameCheckBox);
            platformRow.Children.Add(nonSteamGameCheckBox);
            modeStack.Children.Add(platformRow);
            modeCard.Child = modeStack;
            Grid.SetRow(modeCard, 2);
            root.Children.Add(modeCard);

            var searchCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x13, 0x18, 0x22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x2f, 0x40)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            searchBox = MakeSearchBox();
            searchBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) SearchGame(); };
            Grid.SetColumn(searchBox, 0);
            searchGrid.Children.Add(searchBox);

            var searchBtn = MakePillButton("suchen", 0x3d, 0x69, 0xe8);
            searchBtn.Width = 112;
            searchBtn.Height = 48;
            searchBtn.Margin = new Thickness(10, 0, 0, 0);
            searchBtn.Click += (_, _) => SearchGame();
            Grid.SetColumn(searchBtn, 1);
            searchGrid.Children.Add(searchBtn);
            searchCard.Child = searchGrid;
            Grid.SetRow(searchCard, 3);
            root.Children.Add(searchCard);

            gameCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x13, 0x18, 0x22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x34, 0x47)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(22),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 14),
                Visibility = Visibility.Collapsed,
                Opacity = 0
            };

            var cardStack = new StackPanel();
            var resultHeader = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            resultHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            resultHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            resultHeader.Children.Add(new TextBlock
            {
                Text = "SPIEL GEFUNDEN",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x74, 0x86, 0xa3))
            });
            var ready = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x16, 0x32, 0x29)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(9, 5, 9, 5)
            };
            ready.Child = new TextBlock
            {
                Text = "READY",
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6f, 0xd8, 0xa3))
            };
            Grid.SetColumn(ready, 1);
            resultHeader.Children.Add(ready);
            cardStack.Children.Add(resultHeader);

            steamIdText = AddCardRow(cardStack, "STEAM ID", true);
            nameText = AddCardRow(cardStack, "SPIELNAME", false);
            installDirText = AddCardRow(cardStack, "INSTALLATION", false);
            exeNameText = AddCardRow(cardStack, "EXECUTABLE", false);
            exePathText = AddCardRow(cardStack, "EXE PFAD", false);
            sourceText = AddCardRow(cardStack, "QUELLE", false);
            gameCard.Child = cardStack;
            Grid.SetRow(gameCard, 4);
            root.Children.Add(gameCard);

            launchBtn = MakePillButton("▶   SPIEL STARTEN", 0x3d, 0x69, 0xe8);
            launchBtn.Height = 58;
            launchBtn.FontSize = 14;
            launchBtn.FontWeight = FontWeights.Bold;
            launchBtn.IsEnabled = false;
            launchBtn.Margin = new Thickness(0, 0, 0, 4);
            launchBtn.Click += (_, _) => OnLaunch();
            Grid.SetRow(launchBtn, 5);
            root.Children.Add(launchBtn);

            statusText = new TextBlock
            {
                Text = "bereit · spielname eingeben",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x70, 0x7f, 0x98)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 9, 0, 0)
            };
            Grid.SetRow(statusText, 5);
            statusText.RenderTransform = new TranslateTransform(0, 32);
            root.Children.Add(statusText);

            Content = root;

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            root.BeginAnimation(OpacityProperty, fade);
        }

        private TextBox MakeSearchBox()
        {
            var box = new TextBox
            {
                FontSize = 14,
                Padding = new Thickness(16, 12, 16, 12),
                Background = new SolidColorBrush(Color.FromRgb(0x0d, 0x12, 0x19)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2b, 0x36, 0x48)),
                BorderThickness = new Thickness(1),
                CaretBrush = new SolidColorBrush(Color.FromRgb(0x8a, 0xb4, 0xff)),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            box.Template = RoundedTextBoxTemplate(14);
            return box;
        }

        private CheckBox MakePlatformCheckBox(string text, bool isChecked)
        {
            var cb = new CheckBox
            {
                Content = text,
                IsChecked = isChecked,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xe5, 0xeb, 0xf5)),
                Background = new SolidColorBrush(Color.FromRgb(0x18, 0x1f, 0x2a)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2d, 0x39, 0x4d)),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 9, 0),
                Cursor = Cursors.Hand,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            var template = new ControlTemplate(typeof(CheckBox));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(13));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.PaddingProperty, new Thickness(14, 9, 14, 9));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });

            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.AppendChild(cp);
            template.VisualTree = border;

            var checkedTrigger = new Trigger { Property = CheckBox.IsCheckedProperty, Value = true };
            checkedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x3d, 0x69, 0xe8))));
            checkedTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x5e, 0x86, 0xff))));
            template.Triggers.Add(checkedTrigger);
            cb.Template = template;
            return cb;
        }

        private TextBox MakePathBox(string initial)
        {
            var box = new TextBox
            {
                Text = initial,
                FontSize = 12,
                Padding = new Thickness(12, 10, 12, 10),
                Background = new SolidColorBrush(Color.FromRgb(0x0d, 0x12, 0x19)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xee, 0xf2, 0xf8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2a, 0x34, 0x46)),
                BorderThickness = new Thickness(1),
                CaretBrush = new SolidColorBrush(Color.FromRgb(0x8a, 0xb4, 0xff)),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            box.Template = RoundedTextBoxTemplate(12);
            return box;
        }

        private ControlTemplate RoundedTextBoxTemplate(double radius)
        {
            var template = new ControlTemplate(typeof(TextBox));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetValue(Border.PaddingProperty, new Thickness(1));

            var content = new FrameworkElementFactory(typeof(ScrollViewer));
            content.Name = "PART_ContentHost";
            border.AppendChild(content);
            template.VisualTree = border;
            return template;
        }

        private TextBlock AddCardRow(StackPanel parent, string label, bool highlight)
        {
            var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6e, 0x7d, 0x95)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbl, 0);
            row.Children.Add(lbl);

            var val = new TextBlock
            {
                Text = "–",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = highlight
                    ? new SolidColorBrush(Color.FromRgb(0x8a, 0xb4, 0xff))
                    : new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf2)),
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12, 0, 0, 0)
            };
            Grid.SetColumn(val, 1);
            row.Children.Add(val);
            parent.Children.Add(row);
            return val;
        }

        private Button MakePillButton(string text, byte r, byte g, byte b)
        {
            var btn = new Button
            {
                Content = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(r, g, b)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(18, 10, 18, 10),
                Cursor = Cursors.Hand,
                Height = 44
            };

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(13));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });

            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            template.VisualTree = border;
            btn.Template = template;

            btn.MouseEnter += (_, _) => btn.BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, 0.86, TimeSpan.FromMilliseconds(120)));
            btn.MouseLeave += (_, _) => btn.BeginAnimation(OpacityProperty, new DoubleAnimation(0.86, 1.0, TimeSpan.FromMilliseconds(120)));
            return btn;
        }

        private Button MakeIconButton(string icon)
        {
            var btn = MakePillButton(icon, 0x17, 0x1e, 0x29);
            btn.Width = 46;
            btn.Height = 46;
            btn.Padding = new Thickness(0);
            btn.FontSize = 17;
            btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2b, 0x35, 0x47));
            btn.BorderThickness = new Thickness(1);
            return btn;
        }

        private void BrowseInto(TextBox target)
        {
            var picked = TryModernFolderPicker(target.Text) ?? TryWinFormsFolderPicker(target.Text);
            if (!string.IsNullOrEmpty(picked))
                target.Text = picked;
            else
                SetStatus("ordnerauswahl abgebrochen");
        }

        private string? TryModernFolderPicker(string initial)
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "ordner auswählen",
                    InitialDirectory = SafeInitialDir(initial)
                };
                if (dlg.ShowDialog(this) == true && !string.IsNullOrEmpty(dlg.FolderName))
                    return dlg.FolderName;
            }
            catch { }
            return null;
        }

        private string? TryWinFormsFolderPicker(string initial)
        {
            try
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "ordner auswählen",
                    ShowNewFolderButton = true,
                    UseDescriptionForTitle = true
                };
                var init = SafeInitialDir(initial);
                if (!string.IsNullOrEmpty(init)) dlg.SelectedPath = init;

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedPath))
                    return dlg.SelectedPath;
            }
            catch { }
            return null;
        }

        private string? SafeInitialDir(string input)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input)) return null;
                var trimmed = input.Trim().TrimEnd('\\', '/');
                if (Directory.Exists(trimmed)) return trimmed;
                var dir = trimmed;
                for (int i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
                {
                    var parent = System.IO.Path.GetDirectoryName(dir);
                    if (string.IsNullOrEmpty(parent)) break;
                    if (Directory.Exists(parent)) return parent;
                    dir = parent;
                }
            }
            catch { }
            return null;
        }

        private void ToggleSettings()
        {
            settingsOpen = !settingsOpen;
            if (settingsOpen)
            {
                settingsPanel.Visibility = Visibility.Visible;
                settingsPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            }
            else
            {
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                fade.Completed += (_, _) => settingsPanel.Visibility = Visibility.Collapsed;
                settingsPanel.BeginAnimation(OpacityProperty, fade);
            }
        }

        private void SaveSettings()
        {
            config.SteamPath = steamPathBox.Text.Trim();
            config.SteamAppsPath = steamAppsBox.Text.Trim();
            try
            {
                File.WriteAllText(ConfigFile,
                    JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
            SetStatus("einstellungen gespeichert");
        }

        private void SetStatus(string text, bool error = false)
        {
            statusText.Text = text;
            statusText.Foreground = new SolidColorBrush(error
                ? Color.FromRgb(0xff, 0x7b, 0x7b)
                : Color.FromRgb(0x7c, 0x8a, 0xa0));
        }


        private async void SearchGame()
        {
            var q = searchBox.Text.Trim();
            if (string.IsNullOrEmpty(q))
            {
                SetStatus("bitte einen spielnamen eingeben", true);
                return;
            }

            if (steamGameCheckBox.IsChecked != true && nonSteamGameCheckBox.IsChecked != true)
            {
                SetStatus("bitte Steam Game oder Nicht Steam Game auswählen", true);
                return;
            }

            SetStatus(steamGameCheckBox.IsChecked == true
                ? "suche steam spiel ..."
                : "suche nicht-steam spiel auf Discord ...");
            gameCard.Visibility = Visibility.Collapsed;
            launchBtn.IsEnabled = false;
            currentGame = null;

            var info = await ResolveGameAsync(q);

            if (info == null)
            {
                SetStatus(steamGameCheckBox.IsChecked == true
                    ? "steam spiel nicht gefunden"
                    : "nicht-steam spiel nicht gefunden oder keine executable vorhanden", true);
                return;
            }

            currentGame = info;
            steamIdText.Text = info.SteamId;
            nameText.Text = info.Name;
            installDirText.Text = info.InstallDir;
            exeNameText.Text = info.Exe;
            exePathText.Text = info.ExeRelativePath;
            sourceText.Text = info.Source;

            gameCard.Visibility = Visibility.Visible;
            gameCard.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400)));

            launchBtn.IsEnabled = true;
            SetStatus("spiel bereit · bereit zum starten");
        }

        private async Task<GameInfo?> ResolveGameAsync(string query)
        {
            if (steamGameCheckBox.IsChecked == true)
                return await ResolveSteamGameAsync(query);

            return await ResolveNonSteamGameAsync(query);
        }

        private async Task<GameInfo?> ResolveSteamGameAsync(string query)
        {
            var hit = await SteamDbAutocompleteAsync(query);
            if (hit == null) return null;

            string appid = hit.Value.AppId;
            string steamName = hit.Value.Name;

            var apiInfo = await SteamCmdApiFetchAsync(appid);
            var steamDbLaunch = apiInfo == null ? await SteamDbFetchLaunchAsync(appid) : null;
            var acf = FindLocalAcf(appid);

            string installDir = apiInfo?.InstallDir
                ?? steamDbLaunch?.InstallDir
                ?? acf?.InstallDir
                ?? steamName;

            string exeRel = apiInfo?.ExeRelativePath
                ?? steamDbLaunch?.ExeRelativePath
                ?? "";

            if (string.IsNullOrWhiteSpace(exeRel))
                exeRel = Regex.Replace(steamName, @"[^A-Za-z0-9]", "") + ".exe";

            string exeName = System.IO.Path.GetFileName(exeRel.Replace('/', '\\'));

            string source = apiInfo != null ? "steamcmd"
                : steamDbLaunch != null ? "steamdb"
                : acf != null ? "acf" : "steam";

            return new GameInfo
            {
                SteamId = appid,
                Name = steamName.ToUpperInvariant(),
                InstallDir = installDir,
                Exe = exeName,
                ExeRelativePath = exeRel,
                Source = source
            };
        }

        private class SteamApiInfo
        {
            public string Name { get; set; } = "";
            public string InstallDir { get; set; } = "";
            public string ExeRelativePath { get; set; } = "";
        }

        private async Task<SteamApiInfo?> SteamCmdApiFetchAsync(string appid)
        {
            try
            {
                var urls = new[]
                {
                    "https://api.steamcmd.net/v1/info/" + Uri.EscapeDataString(appid),
                    "https://api.steamcmd.net/v1/info/?appid=" + Uri.EscapeDataString(appid)
                };

                foreach (var url in urls)
                {
                    using var resp = await http.GetAsync(url);
                    if (!resp.IsSuccessStatusCode) continue;

                    var body = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("data", out var data)) continue;

                    JsonElement app = default;
                    if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty(appid, out var named))
                        app = named;
                    else if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
                        app = data[0];

                    if (app.ValueKind != JsonValueKind.Object) continue;

                    var info = new SteamApiInfo();
                    if (app.TryGetProperty("common", out var common) && common.ValueKind == JsonValueKind.Object)
                    {
                        if (common.TryGetProperty("name", out var n)) info.Name = n.GetString() ?? "";
                        if (common.TryGetProperty("installdir", out var d)) info.InstallDir = d.GetString() ?? "";
                    }

                    if (app.TryGetProperty("config", out var configNode) && configNode.ValueKind == JsonValueKind.Object &&
                        configNode.TryGetProperty("launch", out var launch) && launch.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var option in launch.EnumerateObject())
                        {
                            if (option.Value.ValueKind != JsonValueKind.Object) continue;
                            if (!option.Value.TryGetProperty("executable", out var exe)) continue;
                            var candidate = exe.GetString() ?? "";
                            if (string.IsNullOrWhiteSpace(candidate)) continue;
                            info.ExeRelativePath = candidate.Replace('/', '\\').TrimStart('\\');
                            break;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(info.InstallDir) || !string.IsNullOrWhiteSpace(info.ExeRelativePath))
                        return info;
                }
            }
            catch { }
            return null;
        }

        private async Task<GameInfo?> ResolveNonSteamGameAsync(string query)
        {
            try
            {
                var urls = new[]
                {
                    "https://discord.com/api/applications/detectable",
                    "https://discord.com/api/v9/applications/detectable",
                    "https://discord.com/api/v8/applications/detectable"
                };

                JsonElement games = default;
                foreach (var url in urls)
                {
                    try
                    {
                        using var resp = await http.GetAsync(url);
                        if (!resp.IsSuccessStatusCode) continue;
                        var body = await resp.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(body);
                        games = doc.RootElement.Clone();
                        if (games.ValueKind == JsonValueKind.Array) break;
                    }
                    catch { }
                }

                if (games.ValueKind != JsonValueKind.Array) return null;

                var normalizedQuery = query.Trim();
                JsonElement? best = null;
                int bestScore = -1;

                foreach (var game in games.EnumerateArray())
                {
                    if (game.ValueKind != JsonValueKind.Object) continue;
                    string name = game.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    int score = 0;
                    if (name.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase)) score = 100;
                    else if (name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)) score = 80;
                    else if (normalizedQuery.Contains(name, StringComparison.OrdinalIgnoreCase)) score = 70;

                    if (score == 0) continue;
                    if (score > bestScore)
                    {
                        best = game;
                        bestScore = score;
                    }
                }

                if (best == null) return null;
                var selected = best.Value;

                string id = "";
                foreach (var key in new[] { "id", "application_id", "app_id" })
                {
                    if (selected.TryGetProperty(key, out var idNode))
                    {
                        id = idNode.ToString();
                        if (!string.IsNullOrWhiteSpace(id)) break;
                    }
                }
                if (string.IsNullOrWhiteSpace(id)) return null;

                string nameValue = selected.TryGetProperty("name", out var nameNode)
                    ? (nameNode.GetString() ?? normalizedQuery) : normalizedQuery;

                string exe = "";
                string exePath = "";
                if (selected.TryGetProperty("executables", out var executables) && executables.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in executables.EnumerateArray())
                    {
                        if (entry.ValueKind != JsonValueKind.Object) continue;
                        var os = entry.TryGetProperty("os", out var osNode) ? (osNode.GetString() ?? "") : "";
                        if (!string.IsNullOrWhiteSpace(os) && !os.Contains("win", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!entry.TryGetProperty("name", out var exeNode)) continue;
                        var candidate = exeNode.GetString() ?? "";
                        if (string.IsNullOrWhiteSpace(candidate)) continue;

                        var normalizedCandidate = candidate.Replace('/', '\\').TrimStart('\\');
                        var candidateName = System.IO.Path.GetFileName(normalizedCandidate);
                        if (!candidateName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                        if (normalizedCandidate.Contains(".app\\", StringComparison.OrdinalIgnoreCase) ||
                            normalizedCandidate.Contains(".app/", StringComparison.OrdinalIgnoreCase)) continue;

                        exePath = normalizedCandidate;
                        exe = candidateName;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(exe))
                    return null;

                return new GameInfo
                {
                    SteamId = id,
                    Name = nameValue,
                    InstallDir = "games\\" + id,
                    Exe = exe,
                    ExeRelativePath = exePath,
                    Source = "discord detectable"
                };
            }
            catch
            {
                return null;
            }
        }

        private async Task<(string AppId, string Name)?> SteamDbAutocompleteAsync(string query)
        {
            try
            {
                var url = "https://steamdb.info/api/Autocomplete/?q=" + Uri.EscapeDataString(query);
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("X-Requested-With", "XMLHttpRequest");
                req.Headers.Add("Referer", "https://steamdb.info/");

                var resp = await http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;

                var body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);

                if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
                if (doc.RootElement.GetArrayLength() == 0) return null;

                var first = doc.RootElement[0];
                string? appid = null;
                string? name = null;

                if (first.TryGetProperty("appid", out var a)) appid = a.ToString();
                if (first.TryGetProperty("name", out var n)) name = n.GetString();

                if (string.IsNullOrEmpty(appid)) return null;
                if (string.IsNullOrEmpty(name)) name = query;

                return (appid, name);
            }
            catch
            {
                return null;
            }
        }


        private class LaunchInfo
        {
            public string ExeRelativePath { get; set; } = "";
            public string InstallDir { get; set; } = "";
        }

        private async Task<LaunchInfo?> SteamDbFetchLaunchAsync(string appid)
        {
            try
            {
                var url = "https://steamdb.info/app/" + appid + "/config/";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Referer", "https://steamdb.info/");

                using var resp = await http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;

                var html = await resp.Content.ReadAsStringAsync();
                var info = new LaunchInfo();

                string Decode(string value) => WebUtility.HtmlDecode(value)
                    .Replace("&nbsp;", " ")
                    .Trim();

                string NormalizePath(string value) => Decode(value)
                    .Replace('/', '\\')
                    .Trim()
                    .Trim('`', '"', '\\');

                var dirPatterns = new[]
                {
                    @"(?:<th|<td)[^>]*>\s*installdir\s*</(?:th|td)>\s*(?:<td|<th)[^>]*>\s*(?:<code[^>]*>)?([^<]+?)(?:</code>)?\s*</(?:td|th)>",
                    @"installdir\s*</(?:th|td)>\s*<(?:td|th)[^>]*>\s*(?:<code[^>]*>)?([^<]+?)(?:</code>)?\s*</(?:td|th)>"
                };

                foreach (var pattern in dirPatterns)
                {
                    var m = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (m.Success)
                    {
                        info.InstallDir = Decode(m.Groups[1].Value);
                        break;
                    }
                }

                var exePatterns = new[]
                {
                    @"Executable\s*</(?:th|td)>\s*<(?:td|th)[^>]*>\s*(?:<code[^>]*>)?([^<]+?\.exe)(?:</code>)?\s*</(?:td|th)>",
                    @"Executable[^\r\n]{0,100}?`([^`]+\.exe)`",
                    @"Executable[^\r\n]{0,100}?([A-Za-z0-9_ .\\/\-]+\.exe)"
                };

                foreach (var pattern in exePatterns)
                {
                    var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    foreach (Match m in matches)
                    {
                        var candidate = NormalizePath(m.Groups[1].Value);
                        if (string.IsNullOrWhiteSpace(candidate)) continue;
                        if (candidate.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;

                        info.ExeRelativePath = candidate;
                        break;
                    }
                    if (!string.IsNullOrEmpty(info.ExeRelativePath)) break;
                }

                if (string.IsNullOrEmpty(info.InstallDir) || string.IsNullOrEmpty(info.ExeRelativePath))
                {
                    var text = Regex.Replace(html, @"<script\b[^>]*>.*?</script>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    text = Regex.Replace(text, @"<style\b[^>]*>.*?</style>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    text = Regex.Replace(text, @"<[^>]+>", " ");
                    text = WebUtility.HtmlDecode(text);
                    text = Regex.Replace(text, @"[ \t]+", " ");

                    if (string.IsNullOrEmpty(info.InstallDir))
                    {
                        var m = Regex.Match(text, @"\binstalldir\s+([A-Za-z0-9._()'&+\- ]+?)(?=\s+(?:name_localized|Launch Options|Supported Languages|$))", RegexOptions.IgnoreCase);
                        if (m.Success) info.InstallDir = m.Groups[1].Value.Trim();
                    }

                    if (string.IsNullOrEmpty(info.ExeRelativePath))
                    {
                        var m = Regex.Match(text, @"Executable\s+[`'""]?([A-Za-z0-9_ .\\/\-]+\.exe)", RegexOptions.IgnoreCase);
                        if (m.Success) info.ExeRelativePath = NormalizePath(m.Groups[1].Value);
                    }
                }

                if (string.IsNullOrEmpty(info.InstallDir) && string.IsNullOrEmpty(info.ExeRelativePath))
                    return null;

                return info;
            }
            catch
            {
                return null;
            }
        }


        private class AcfInfo
        {
            public string InstallDir { get; set; } = "";
        }

        private AcfInfo? FindLocalAcf(string appid)
        {
            try
            {
                var roots = CollectSteamAppsRoots();
                var acfName = "appmanifest_" + appid + ".acf";

                foreach (var root in roots)
                {
                    if (!Directory.Exists(root)) continue;
                    var path = System.IO.Path.Combine(root, acfName);
                    if (!File.Exists(path)) continue;

                    var content = File.ReadAllText(path);
                    var m = Regex.Match(content,
                        @"""installdir""\s+""([^""]+)""",
                        RegexOptions.IgnoreCase);

                    if (m.Success)
                    {
                        return new AcfInfo { InstallDir = m.Groups[1].Value.Trim() };
                    }
                }
            }
            catch { }
            return null;
        }

        private List<string> CollectSteamAppsRoots()
        {
            var list = new List<string>();

            void TryAdd(string p)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(p)) return;
                    var t = p.Trim().TrimEnd('\\', '/');
                    if (Directory.Exists(t) && !list.Contains(t, StringComparer.OrdinalIgnoreCase))
                        list.Add(t);
                }
                catch { }
            }

            TryAdd(config.SteamAppsPath);

            try
            {
                if (!string.IsNullOrWhiteSpace(config.SteamPath))
                {
                    var common = config.SteamPath.TrimEnd('\\', '/');
                    var parent = System.IO.Path.GetDirectoryName(common);
                    if (!string.IsNullOrEmpty(parent)) TryAdd(parent);
                }
            }
            catch { }

            try
            {
                foreach (var root in list.ToArray())
                {
                    var vdf = System.IO.Path.Combine(root, "libraryfolders.vdf");
                    if (!File.Exists(vdf)) continue;

                    var lines = File.ReadAllLines(vdf);
                    foreach (var line in lines)
                    {
                        var m = Regex.Match(line, @"""path""\s+""([^""]+)""", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            var lib = m.Groups[1].Value.Replace(@"\\", @"\").Trim();
                            var apps = System.IO.Path.Combine(lib, "steamapps");
                            TryAdd(apps);
                        }
                    }
                }
            }
            catch { }

            return list;
        }


        private async void OnLaunch()
        {
            if (currentGame == null)
            {
                SetStatus("kein spiel ausgewählt", true);
                return;
            }

            var steamBase = steamPathBox.Text.Trim();
            if (string.IsNullOrEmpty(steamBase))
            {
                SetStatus("bitte steam installationspfad angeben", true);
                if (!settingsOpen) ToggleSettings();
                return;
            }

            SetStatus("starte spiel ...");

            try
            {
                if (!File.Exists(StubExePath))
                    await Task.Run(() => EnsureStubExe());

                if (nonSteamGameCheckBox.IsChecked == true)
                    await Task.Run(() => LaunchNonSteamGame(currentGame));
                else
                    await Task.Run(() => LaunchGame(steamBase, currentGame));

                SetStatus("spiel gestartet · viel spaß");
            }
            catch (Exception ex)
            {
                SetStatus("fehler: " + ex.Message, true);
            }
        }

        private void LaunchNonSteamGame(GameInfo info)
        {
            var gamesRoot = System.IO.Path.Combine(AppDataDir, "games");
            var gameFolder = System.IO.Path.Combine(gamesRoot, info.SteamId);
            Directory.CreateDirectory(gameFolder);

            var exeRelative = (info.ExeRelativePath ?? string.Empty)
                .Replace('/', '\\')
                .TrimStart('\\');
            if (string.IsNullOrWhiteSpace(exeRelative)) exeRelative = info.Exe;

            var exePath = System.IO.Path.Combine(gameFolder, exeRelative);
            var exeDirectory = System.IO.Path.GetDirectoryName(exePath) ?? gameFolder;
            Directory.CreateDirectory(exeDirectory);

            if (File.Exists(exePath)) File.Delete(exePath);
            if (!File.Exists(StubExePath)) throw new FileNotFoundException("stub exe nicht gefunden", StubExePath);
            File.Copy(StubExePath, exePath, true);

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = exeDirectory,
                UseShellExecute = true
            });
        }

        private void LaunchGame(string steamBase, GameInfo info)
        {
            var basePath = steamBase.TrimEnd('\\', '/');
            var gameFolder = System.IO.Path.Combine(basePath, info.InstallDir);
            Directory.CreateDirectory(gameFolder);

            var acfPath = System.IO.Path.Combine(gameFolder, $"appmanifest_{info.SteamId}.acf");
            var acfContent =
                "\"AppState\"\n" +
                "{\n" +
                $"    \"appid\"    \"{info.SteamId}\"\n" +
                $"    \"name\"     \"{info.Name}\"\n" +
                $"    \"installdir\"       \"{info.InstallDir}\"\n" +
                "}\n";
            File.WriteAllText(acfPath, acfContent, new UTF8Encoding(false));

            var exeRelative = (info.ExeRelativePath ?? string.Empty)
                .Replace('/', '\\')
                .TrimStart('\\');
            if (string.IsNullOrWhiteSpace(exeRelative))
                exeRelative = string.IsNullOrWhiteSpace(info.Exe) ? "game.exe" : info.Exe;

            var exePath = System.IO.Path.Combine(gameFolder, exeRelative);
            var exeDirectory = System.IO.Path.GetDirectoryName(exePath) ?? gameFolder;
            Directory.CreateDirectory(exeDirectory);

            if (File.Exists(StubExePath))
            {
                try { if (File.Exists(exePath)) File.Delete(exePath); } catch { }

                File.Copy(StubExePath, exePath, true);

                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = exeDirectory,
                    UseShellExecute = true
                });
                return;
            }

            var batPath = exePath + ".bat";
            var batContent =
                "@echo off\n" +
                $"title {info.Name}\n" +
                "mode con: cols=80 lines=12\n" +
                "cls\n" +
                "color 0B\n" +
                "echo.\n" +
                "echo    ================================================\n" +
                "echo.\n" +
                "echo                m a d - d c\n" +
                "echo.\n" +
                "echo        this is part of the mad-dc application\n" +
                "echo.\n" +
                "echo    ================================================\n" +
                "echo.\n" +
                "echo    [druecke eine taste zum beenden]\n" +
                "pause > nul\n";
            File.WriteAllText(batPath, batContent, new UTF8Encoding(false));

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start \"{info.Name}\" cmd.exe /k \"{batPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }

        private AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    var json = File.ReadAllText(ConfigFile);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch { }
            return new AppConfig();
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            var application = new Application();
            application.Run(new MainWindow());
        }
    }
}
