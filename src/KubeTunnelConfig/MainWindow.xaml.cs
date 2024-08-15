using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Shared;
using Path = System.IO.Path;

namespace KubeTunnelConfig
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public partial class MainWindow : INotifyPropertyChanged
    {
        private string? _searchText;
        public string? SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                FilterServices();
            }
        }

        private string? _currentProfile;
        public string CurrentProfile
        {
            get => _currentProfile ?? string.Empty;
            set
            {
                _currentProfile = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ServiceInfo> AllServices = new();
        public ObservableCollection<PortForwardConfig> ConfiguredServices { get; set; } = new();

        private ServiceInfo? _selectedService;
        public ServiceInfo? SelectedService
        {
            get => _selectedService;
            set
            {
                _selectedService = value;
                OnPropertyChanged();
                // Any additional code you want to run when the selection changes can go here.
            }
        }

        public ICollectionView FilteredServices { get; set; }
        public List<string> ProfileList { get; set; } = new();

        public MainWindow()
        {
            DataContext = this;

            var config = LoadConfig();
            CurrentProfile = config.CurrentProfile;
            var portForwardConfigs = LoadPortForwardConfiguration(CurrentProfile);
            LoadServices();
            foreach (var portForwardConfig in portForwardConfigs ?? Enumerable.Empty<PortForwardConfig>())
            {
                ConfiguredServices.Add(portForwardConfig);
            }

            FilteredServices = CollectionViewSource.GetDefaultView(AllServices);
            InitializeComponent();
            FilterServices();
            LoadProfileItems();
            TxtSearch.Focus();
        }

        private async void LoadServices()
        {
            // Your logic to fetch the list of services.
            // Example: 
            // ConfiguredServices.Add(new PortForwardConfig { Service = "service1", LocalPort = "8080", RemotePort = "80" });
            var startInfo = new ProcessStartInfo
            {
                FileName = "kubectl",
                Arguments = "get services --all-namespaces",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);

            if (process == null)
            {
                return;
            }

            bool firstLine = true;

            while (!process.StandardOutput.EndOfStream)
            {
                string? line = await process.StandardOutput.ReadLineAsync();

                if (firstLine)
                {
                    firstLine = false;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var split = line.Split(' ', 7, options: StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                try
                {
                    var port = int.Parse(split[5].Split('/').First());
                    AllServices.Add(new ServiceInfo()
                    {
                        Service = split[1],
                        LocalPort = split[5],
                        Namespace = split[0],
                        ParsedPort = port
                    });
                }
                catch
                {
                    // ignored
                }
            }
        }

        private void AddToConfig_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedService == null)
            {
                return;
            }

            var canParsePort = int.TryParse(TxtLocalPort.Text, out var port);

            if (!canParsePort)
            {
                MessageBox.Show("Invalid port.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (ConfiguredServices.Any(x => x.Service == SelectedService?.Service))
            {
                MessageBox.Show("Cannot add an already existing service.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (ConfiguredServices.Any(x => x.LocalPort == port))
            {
                MessageBox.Show("Port conflict.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var serviceConfig = new PortForwardConfig
            {
                Service = SelectedService.Service,  // Add a logic or UI element to get the service name.
                LocalPort = port,
                RemotePort = SelectedService.ParsedPort,
                Namespace = SelectedService.Namespace
            };
            ConfiguredServices.Add(serviceConfig);
            FilterServices();
            TxtSearch.SelectAll();
            TxtSearch.Focus();
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            SaveConfig();
            SaveCurrentPortForwardToProfile();
        }

        private void SaveConfig()
        {
            var folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KubeTunnelConfig",
                "config.json");
            var json = JsonSerializer.Serialize(new Config { CurrentProfile = CurrentProfile });
            File.WriteAllText(folderPath, json);
        }

        private void SaveCurrentPortForwardToProfile()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KubeTunnelConfig", "Profiles");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var configPath = Path.Combine(folder, $"{CurrentProfile}.json");
            var json = JsonSerializer.Serialize(ConfiguredServices.ToList());
            File.WriteAllText(configPath, json);
        }

        private Config LoadConfig()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KubeTunnelConfig");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var profilePath = Path.Combine(folder, "config.json");

            var profilesDirectory = Path.Combine(folder, "Profiles");

            if (!Directory.Exists(profilesDirectory))
                Directory.CreateDirectory(profilesDirectory);

            var profiles = Directory.GetFiles(profilesDirectory);

            if (!profiles.Any())
            {
                ProfileList.Add(Config.DefaultConfigName);
            }
            else
            {
                foreach (var profile in profiles)
                {
                    ProfileList.Add(Path.GetFileNameWithoutExtension(profile));
                }
            }

            if (!File.Exists(profilePath))
            {
                var newProfileConfig = new Config();
                var json = JsonSerializer.Serialize(newProfileConfig);
                File.WriteAllText(profilePath, json);
                return new Config();
            }

            var profileContent = File.ReadAllText(profilePath);
            var config = JsonSerializer.Deserialize<Config>(profileContent) ?? new Config();
            return config;
        }

        static PortForwardConfig[]? LoadPortForwardConfiguration(string name)
        {
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KubeTunnelConfig", "Profiles");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var configPath = Path.Combine(folder, $"{name}.json");

                var configContent = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<PortForwardConfig[]>(configContent);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void FilterServices()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                FilteredServices.Filter =
                    (obj) => !ConfiguredServices.Any(c => c.Namespace == ((ServiceInfo)obj).Namespace && c.Service == ((ServiceInfo)obj).Service);
            else
                FilteredServices.Filter = (obj) => ((ServiceInfo)obj).Service.Contains(SearchText, StringComparison.OrdinalIgnoreCase) && !ConfiguredServices.Any(c => c.Namespace == ((ServiceInfo)obj).Namespace && c.Service == ((ServiceInfo)obj).Service);
            FilteredServices.Refresh();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).CommandParameter is PortForwardConfig selectedService)
            {
                ConfiguredServices.Remove(selectedService);
                FilterServices();
            }
        }

        private void UIElement_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (!string.IsNullOrWhiteSpace(TxtLocalPort.Text) && int.TryParse(TxtLocalPort.Text, out var parsedPort))
                {
                    TxtLocalPort.Text = (++parsedPort).ToString();
                }
                TxtLocalPort.Focus();
                TxtLocalPort.SelectAll();
                e.Handled = true;
            }
        }

        private void TxtLocalPort_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TxtLocalPort.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                AddToConfig_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void TxtSearch_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                try
                {
                    UpdateLayout();
                    ListViewAll.SelectedIndex = 0;
                    ListViewItem? item = ListViewAll.ItemContainerGenerator.ContainerFromIndex(0) as ListViewItem;
                    item?.Focus();
                }
                catch (Exception)
                {
                    // ignored
                }
                e.Handled = true;
            }
        }

        private void MenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var kubeTunnel = Process.GetProcessesByName("KubeTunnel");

            foreach (var kubeProcess in kubeTunnel)
            {
                kubeProcess.Kill(true);
                e.Handled = true;
                return;
            }

            var processes = Process.GetProcessesByName("kubectl");

            foreach (var process in processes)
            {
                process.Kill();
            }
            e.Handled = true;
        }

        private void LoadProfileItems()
        {
            ProfileMenuItem.Items.Clear();
            foreach (var profile in ProfileList)
            {
                var menuitem = new MenuItem
                {
                    Header = profile,
                };
                menuitem.Click += (_, _) =>
                {
                    SaveCurrentPortForwardToProfile();
                    ChangeProfile(profile);
                };
                ProfileMenuItem.Items.Add(menuitem);
            }
            UpdateCheckedProfileMenuItems();
        }

        private void ChangeProfile(string profile)
        {
            CurrentProfile = profile;
            UpdateCheckedProfileMenuItems();
            ConfiguredServices.Clear();

            var config = LoadPortForwardConfiguration(profile);

            foreach (var forwardConfig in config ?? Enumerable.Empty<PortForwardConfig>())
            {
                ConfiguredServices.Add(forwardConfig);
            }
        }

        private void UpdateCheckedProfileMenuItems()
        {
            foreach (var item in ProfileMenuItem.Items)
            {
                if (item is not MenuItem menuItem) 
                    continue;

                menuItem.IsChecked = (string)menuItem.Header == CurrentProfile;
            }
        }

        private void CreateProfile_OnClick(object sender, RoutedEventArgs e)
        {
            CreateProfile();
        }

        private void CreateProfile()
        {
            var textInputDialog = new TextInputDialog("Create profile")
            {
                Owner = this
            };
            textInputDialog.ShowDialog();

            if (textInputDialog.DialogResult == true)
            {
                SaveCurrentPortForwardToProfile();
                ProfileList.Add(textInputDialog.InputText);
                CurrentProfile = textInputDialog.InputText;
                ChangeProfile(textInputDialog.InputText);
                LoadProfileItems();
            }
        }

        private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SaveCurrentPortForwardToProfile();
                SaveConfig();
                MessageBox.Show($"Saved to: {CurrentProfile}", "Config saved", MessageBoxButton.OK, MessageBoxImage.Information);
                e.Handled = true;
            }
            else if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
            {
                CreateProfile();
                e.Handled = true;
            }
        }
    }
}
