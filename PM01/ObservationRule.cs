using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Windows.Shapes;

namespace PM01
{
    // Класс, представляющий правило наблюдения
    [Serializable]
    public class ObservationRule
    {
        private readonly FileSystemWatcher watcher = new();
        public string Path { get; set; } = string.Empty;

        public bool ObserveChanges { get; set; } = true;

        public bool ObserveCreation { get; set; } = true;

        public bool ObserveRename { get; set; } = true;

        public bool ObserveDeletion { get; set; } = true;

        public bool IsEnabled { get { return watcher.EnableRaisingEvents; } set { watcher.EnableRaisingEvents = value; } }

        //Коллекция, представляющая журнал событий
        public ObservableCollection<LogEntry> EventLog { get; private set; } = new ObservableCollection<LogEntry>();

        public ObservationRule(string path)
        {
            Path = path;

            //Назначение обработчиков событий
            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
            watcher.Changed += OnChanged;

            watcher.Path = Path;
            watcher.IncludeSubdirectories = true;

            watcher.EnableRaisingEvents = IsEnabled;
        }

        private void OnCreated(object source, FileSystemEventArgs e)
        {
            if (!ObserveCreation)
                return;

            App.Current.Dispatcher.Invoke(delegate
            {
                EventLog.Add(new LogEntry($"Файл создан: {e.FullPath}"));
            });
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            if (!ObserveChanges)
                return;

            App.Current.Dispatcher.Invoke(delegate
            {
                EventLog.Add(new LogEntry($"Файл изменён: {e.FullPath}"));
            });
        }

        private void OnRenamed(object source, RenamedEventArgs e)
        {
            if (!ObserveRename) 
                return;

            App.Current.Dispatcher.Invoke(delegate
            {
                EventLog.Add(new LogEntry($"Файл переименован: {e.OldFullPath} -> {e.FullPath}"));
            });
        }

        private void OnDeleted(object source, FileSystemEventArgs e)
        {
            if (!ObserveDeletion)
                return;

            App.Current.Dispatcher.Invoke(delegate
            {
                EventLog.Add(new LogEntry($"Файл удалён: {e.FullPath}"));
            });
        }

        public void StopWatching()
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        public override string ToString()
        {
            return Path;
        }
    }

    //Класс, представляющий запись в журнале событий
    [Serializable]
    public class LogEntry
    {
        public DateTime Timestamp { get; }
        public string Message { get; } = string.Empty;

        public LogEntry(string message) : this(DateTime.Now, message)
        {

        }

        //Явно указываем конструктор, который будет использоваться при десериализации
        [JsonConstructor]
        public LogEntry(DateTime timestamp, string message)
        {
            Timestamp = timestamp;
            Message = message;
        }

        public override string ToString()
        {
            return $"{Timestamp}: {Message}";
        }
    }
}
