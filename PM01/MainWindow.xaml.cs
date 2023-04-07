using MS.WindowsAPICodePack.Internal;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Xml.Serialization;

namespace PM01
{
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();

            var vm = new MainViewModel(FindName);

            Loaded += (_, _) =>
            {
                vm.OnLoaded();
            };

            DataContext = vm;
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private Func<string, object?> FindName;

        public MainViewModel(Func<string, object?> findFunc)
        {
            FindName = findFunc;

            AddRuleCommand = new RelayCommand(AddRule);
            RemoveRuleCommand = new RelayCommand(RemoveSelectedRule);
            RemoveSelectedRuleCommand = new RelayCommand(RemoveSelectedRule);
            SaveRulesCommand = new RelayCommand(SaveRules);

            Rules = new ObservableCollection<ObservationRule>();

            Rules.CollectionChanged += (sender, args) =>
            {
                OnPropertyChanged(nameof(HasRules));
                if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                    SelectedRule = Rules.Last();
            };

        }

        public void OnLoaded()
        {
            if (File.Exists(RULES_FILE_NAME))
                LoadRules();

            var listBoxElement = FindName("logBox");
            if (listBoxElement is ListBox)
            {
                var listBox = (ListBox) listBoxElement;
                if (!listBox.Items.IsEmpty)
                    listBox.ScrollIntoView(listBox.Items[^1]);
            }
        }

        public ObservableCollection<ObservationRule> Rules { get; }

        private ObservationRule _selectedRule;

        public ObservationRule SelectedRule
        {
            get { return _selectedRule; }
            set
            {
                _selectedRule = value;
                OnPropertyChanged(nameof(SelectedRule));
                OnPropertyChanged(nameof(HasSelection));

                var listBoxElement = FindName("logBox");
                if (listBoxElement is ListBox)
                {
                    var listBox = (ListBox)listBoxElement;

                    if (!listBox.Items.IsEmpty)
                        listBox.ScrollIntoView(listBox.Items[^1]);

                    _selectedRule.EventLogs.CollectionChanged += (_, _) =>
                    {
                        if (SelectedRule != value)
                            return;

                        var listBox = (ListBox)listBoxElement;
                        listBox.ScrollIntoView(listBox.Items[^1]);
                    };
                }
            }
        }

        public bool HasSelection { get => _selectedRule != null; }

        public bool HasRules { get => Rules.Count > 0; }

        public ICommand AddRuleCommand { get; }
        public ICommand RemoveRuleCommand { get; }
        public ICommand RemoveSelectedRuleCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand SaveRulesCommand { get; }

        private const string RULES_FILE_NAME = "rules.json";

        private void LoadRules()
        {
            if (File.Exists(RULES_FILE_NAME))
            {
                JsonSerializer serializer = new();

                using (StreamReader sr = new(RULES_FILE_NAME))
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    var rules = serializer.Deserialize<IEnumerable<ObservationRule>>(reader);
                    if (rules != null) foreach (var rule in rules)
                       Rules.Add(rule);
                }
            }
        }

        private void SaveRules()
        {
            JsonSerializer serializer = new();
            serializer.Formatting = Formatting.Indented;

            using (StreamWriter sw = new(RULES_FILE_NAME))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, Rules);
            }
        }

        private void AddRule()
        {
            var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.Title = "Select a folder to watch";
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (dialog.ShowDialog() == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
            {
                ObservationRule rule = new(dialog.FileName);
                Rules.Add(rule);
            }
        }
        private void RemoveSelectedRule()
        {
            if (SelectedRule != null)
            {
                Rules.Remove(SelectedRule);
                SelectedRule = null;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }

    public class RelayCommand : ICommand
    {
        private Action action;
        private Func<bool> canExecute;

        public RelayCommand(Action action, Func<bool> canExecute = null)
        {
            this.action = action;
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return canExecute == null || canExecute();
        }

        public void Execute(object parameter)
        {
            action();
        }
    }

}