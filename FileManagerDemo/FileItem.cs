using System.ComponentModel;

public class FileItem : INotifyPropertyChanged
{
    public string Path { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string Icon { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;
}

