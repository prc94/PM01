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

            //Добавляем обработчик события завершения инициаилзации
            Loaded += (_, _) =>
            {
                vm.OnInitialized();
            };

            DataContext = vm;
        }
    }

    //Здесь реализована основная логика приложения
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly Func<string, object?> FindName;

        public MainViewModel(Func<string, object?> findNameFunc)
        {
            //Передаём функцию FindName из класса главного окна, чтобы иметь возможность получать доступ к компонентам интерфейса
            //из нашего контекста данных
            FindName = findNameFunc;

            AddRuleCommand = new RelayCommand(AddRule);
            RemoveRuleCommand = new RelayCommand(RemoveSelectedRule);
            RemoveSelectedRuleCommand = new RelayCommand(RemoveSelectedRule);
            SaveRulesCommand = new RelayCommand(SaveRules);

            Rules = new ObservableCollection<ObservationRule>();

            Rules.CollectionChanged += (_, args) =>
            {
                OnPropertyChanged(nameof(HasRules));

                if (args.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                    return;

                SelectedRule = Rules.Last();

                if (args.NewItems != null)
                    foreach (var item in args.NewItems)
                    {
                        var listBoxElement = FindName("logBox");
                        if (listBoxElement is ListBox listBox && item is ObservationRule rule)
                        {
                            if (!listBox.Items.IsEmpty)
                                listBox.ScrollIntoView(listBox.Items[^1]);

                            //Добавляем обработчик для прокрутки списка событий по мере появления новых
                            rule.EventLog.CollectionChanged += (_, _) =>
                            {
                                //Прокручиваем элемент списка событий только если текущее правило является выбранным
                                //и список событий не пуст
                                if (SelectedRule != rule || listBox.Items.IsEmpty)
                                    return;

                                listBox.ScrollIntoView(listBox.Items[^1]);
                            };
                        }
                    }
            };
        }

        //Действия, которые должны быть выполнены после полной инициализация приложения
        public void OnInitialized()
        {
            //Загружаем правила, при наличии сохранения
            if (File.Exists(RULES_FILE_NAME))
                LoadRules();

            //Если в журнале событий есть элементы (они могут появится после загрузки правил), прокручиваем его до конца
            var listBoxElement = FindName("logBox");
            if (listBoxElement is ListBox)
            {
                var listBox = (ListBox)listBoxElement;
                if (!listBox.Items.IsEmpty)
                    listBox.ScrollIntoView(listBox.Items[^1]);
            }
        }

        public ObservableCollection<ObservationRule> Rules { get; }

        private ObservationRule? _selectedRule;
        public ObservationRule? SelectedRule
        {
            get { return _selectedRule; }
            set
            {
                _selectedRule = value;
                OnPropertyChanged(nameof(SelectedRule));
                OnPropertyChanged(nameof(HasSelection));

                if (_selectedRule == null)
                    return;

                //При изменении выбранного правила необходимо заново прокрутить список событий вниз
                var listBoxElement = FindName("logBox");
                if (listBoxElement is ListBox listBox)
                    if (!listBox.Items.IsEmpty)
                        listBox.ScrollIntoView(listBox.Items[^1]);
            }
        }

        //Указывает есть ли у нас выбранное правило в настоящий момент
        public bool HasSelection { get => _selectedRule != null; }

        //Укзывает есть ли правила в настоящий момент
        public bool HasRules { get => Rules.Count > 0; }

        public ICommand AddRuleCommand { get; }
        public ICommand RemoveRuleCommand { get; }
        public ICommand RemoveSelectedRuleCommand { get; }
        public ICommand SaveRulesCommand { get; }

        //Имя файла для сохранения данных приложения
        private const string RULES_FILE_NAME = "rules.json";

        //Метод десериализации правил из файла json
        private void LoadRules()
        {
            if (File.Exists(RULES_FILE_NAME))
            {
                JsonSerializer serializer = new();

                using (StreamReader sr = new(RULES_FILE_NAME))
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    var rules = serializer.Deserialize<IEnumerable<ObservationRule>>(reader);
                    if (rules != null) 
                        foreach (var rule in rules)
                            Rules.Add(rule);
                }
            }
        }

        //Метод сериализации правил в файл json
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
            var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Выбор папки для слежения",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dialog.ShowDialog() == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
                Rules.Add(new(dialog.FileName));
        }

        private void RemoveSelectedRule()
        {
            if (SelectedRule != null)
            {
                Rules.Remove(SelectedRule);
                SelectedRule = null;
            }
        }

        //Обработчик события изменения для отслеживаемых свойств
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }

    //Типовая реализация интерфейса ICommand
    public class RelayCommand : ICommand
    {
        private Action action;
        private Func<bool> canExecute;

        public RelayCommand(Action action, Func<bool> canExecute = null)
        {
            this.action = action;
            this.canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return canExecute == null || canExecute();
        }

        public void Execute(object? parameter)
        {
            action();
        }
    }
}