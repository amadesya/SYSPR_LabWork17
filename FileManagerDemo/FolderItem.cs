using System.Collections.ObjectModel;
using System.ComponentModel;

namespace FileManager
{
    public class FolderItem : INotifyPropertyChanged
    {
        public string Path { get; set; }
        public string Name { get; set; }

        public ObservableCollection<FolderItem> SubFolders { get; set; } = new ObservableCollection<FolderItem>();
        public ObservableCollection<FileItem> Files { get; set; } = new ObservableCollection<FileItem>();

        public bool IsExpanded { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
