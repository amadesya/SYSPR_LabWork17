using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace FileManager
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<FolderItem> rootFolders = new ObservableCollection<FolderItem>();

        public MainWindow()
        {
            InitializeComponent();

            PathTextBox.Text = @"C:\";

            LoadDrives();

            FoldersTreeView.ItemsSource = rootFolders;

            // Подгрузка дочерних папок при раскрытии
            FoldersTreeView.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(TreeViewItem_Expanded));
        }

        private void LoadDrives()
        {
            rootFolders.Clear();
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var folder = new FolderItem
                {
                    Path = drive.RootDirectory.FullName,
                    Name = drive.Name.TrimEnd('\\'),
                };

                // Добавим заглушку для lazy loading
                folder.SubFolders.Add(null);

                rootFolders.Add(folder);
            }
        }

        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem item && item.DataContext is FolderItem folder)
            {
                if (folder.SubFolders.Count == 1 && folder.SubFolders[0] == null)
                {
                    folder.SubFolders.Clear();
                    try
                    {
                        var dirs = Directory.GetDirectories(folder.Path);
                        foreach (var dir in dirs)
                        {
                            var dirInfo = new DirectoryInfo(dir);
                            var subFolder = new FolderItem
                            {
                                Path = dir,
                                Name = dirInfo.Name,
                            };
                            // Добавляем заглушку для lazy loading
                            if (Directory.GetDirectories(dir).Any())
                                subFolder.SubFolders.Add(null);

                            folder.SubFolders.Add(subFolder);
                        }
                    }
                    catch { /* Игнорируем ошибки доступа */ }
                }
            }
        }

        private void FoldersTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (FoldersTreeView.SelectedItem is FolderItem folder)
            {
                PathTextBox.Text = folder.Path;
                LoadFiles(folder.Path);
            }
        }

        private void LoadFiles(string path)
        {
            var files = new ObservableCollection<FileItem>();
            try
            {
                var fileInfos = new DirectoryInfo(path).GetFiles();
                foreach (var fi in fileInfos)
                {
                    files.Add(new FileItem
                    {
                        Path = fi.FullName,
                        Name = fi.Name,
                        Type = fi.Extension.ToLower(),
                        Icon = GetIconByExtension(fi.Extension.ToLower())
                    });
                }
            }
            catch { /* Игнорируем ошибки доступа */ }

            FilesListView.ItemsSource = files;
        }

        private FolderItem FindFolderItemRecursive(ObservableCollection<FolderItem> folders, string path)
        {
            foreach (var folder in folders)
            {
                if (string.Equals(folder.Path.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    return folder;

                // Если подкаталоги не загружены, загрузим их
                if (folder.SubFolders.Count == 1 && folder.SubFolders[0] == null)
                {
                    try
                    {
                        folder.SubFolders.Clear();
                        var dirs = Directory.GetDirectories(folder.Path);
                        foreach (var dir in dirs)
                        {
                            var dirInfo = new DirectoryInfo(dir);
                            var subFolder = new FolderItem
                            {
                                Path = dir,
                                Name = dirInfo.Name,
                            };
                            if (Directory.GetDirectories(dir).Any())
                                subFolder.SubFolders.Add(null);

                            folder.SubFolders.Add(subFolder);
                        }
                    }
                    catch { /* Игнорируем ошибки доступа */ }
                }

                var found = FindFolderItemRecursive(folder.SubFolders, path);
                if (found != null)
                    return found;
            }
            return null;
        }


        private string GetIconByExtension(string ext)
        {
            switch (ext)
            {
                case ".txt":
                    return "Icons/file_txt.png";
                case ".pdf":
                    return "Icons/file_pdf.png";
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                    return "Icons/file_image.png";
                default:
                    return "Icons/file_default.png";
            }
        }

        private async void GoButton_Click(object sender, RoutedEventArgs e)
        {
            string path = PathTextBox.Text?.Trim();

            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Введите путь.");
                return;
            }

            if (!Directory.Exists(path))
            {
                MessageBox.Show("Папка не найдена.");
                return;
            }

            try
            {
                // Не вызываем LoadDrives() — дерево уже загружено

                // Ищем элемент, подгружая подкаталоги рекурсивно
                var folderItem = FindFolderItemRecursive(rootFolders, path);
                if (folderItem == null)
                {
                    MessageBox.Show("Папка не найдена в дереве.");
                    return;
                }

                // Раскрываем путь к папке в дереве
                await ExpandToFolderAsync(folderItem);

                // Выделяем элемент в TreeView
                SelectTreeViewItem(FoldersTreeView, folderItem);

                LoadFiles(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private async Task ExpandToFolderAsync(FolderItem folder)
        {
            // Чтобы раскрыть путь — нужно знать родителей
            // Предположим, у FolderItem есть свойство Parent
            // Если нет — можно реализовать поиск пути по Path

            var pathParts = folder.Path.TrimEnd('\\').Split('\\');
            ObservableCollection<FolderItem> currentLevel = rootFolders;
            FolderItem currentFolder = null;
            string accumulatedPath = "";

            for (int i = 0; i < pathParts.Length; i++)
            {
                var part = pathParts[i];
                accumulatedPath = i == 0 ? part + "\\" : System.IO.Path.Combine(accumulatedPath, part);

                currentFolder = currentLevel.FirstOrDefault(f =>
                    string.Equals(f.Name, part, StringComparison.InvariantCultureIgnoreCase));

                if (currentFolder == null)
                    break;

                // Получаем TreeViewItem и раскрываем
                var tvi = GetTreeViewItem(FoldersTreeView, currentFolder);
                if (tvi == null)
                {
                    // Если визуальный элемент не создан, попробуем раскрыть родителя и дождаться создания
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    tvi = GetTreeViewItem(FoldersTreeView, currentFolder);
                }

                if (tvi != null && !tvi.IsExpanded)
                    tvi.IsExpanded = true;

                // Ждём, чтобы подгрузились подкаталоги
                await Task.Delay(100);

                currentLevel = currentFolder.SubFolders;
            }
        }


        private void PathTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                GoButton_Click(null, null);
        }

        private void SelectTreeViewItem(TreeView treeView, object itemToSelect)
        {
            var tvi = GetTreeViewItem(treeView, itemToSelect);
            if (tvi != null)
            {
                tvi.IsSelected = true;
                tvi.BringIntoView();
            }
        }

        private TreeViewItem GetTreeViewItem(ItemsControl container, object item)
        {
            if (container == null) return null;

            for (int i = 0; i < container.Items.Count; i++)
            {
                var currentItem = container.Items[i];
                var tvi = container.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;

                if (tvi == null)
                {
                    // Элемент ещё не создан, попробуем развернуть родителя для генерации
                    if (container is TreeViewItem parentTvi)
                    {
                        parentTvi.IsExpanded = true;
                        tvi = container.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                    }
                }

                if (currentItem == item)
                    return tvi;

                // Рекурсивно ищем в дочерних элементах
                var childTvi = GetTreeViewItem(tvi, item);
                if (childTvi != null)
                    return childTvi;
            }
            return null;
        }


        private FolderItem FindFolderItem(ObservableCollection<FolderItem> folders, string path)
        {
            foreach (var folder in folders)
            {
                if (string.Equals(folder.Path.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    return folder;

                if (folder.SubFolders != null && folder.SubFolders.Count > 0)
                {
                    var found = FindFolderItem(folder.SubFolders, path);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }

        private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedFiles = FilesListView.SelectedItems.Cast<FileItem>().Select(f => f.Path).ToList();
            if (selectedFiles.Count > 0)
            {
                StringCollection paths = new StringCollection();
                paths.AddRange(selectedFiles.ToArray());
                Clipboard.SetFileDropList(paths);
            }
        }

        private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                string destFolder = PathTextBox.Text;

                foreach (string file in files)
                {
                    try
                    {
                        string destFile = System.IO.Path.Combine(destFolder, System.IO.Path.GetFileName(file));
                        if (File.Exists(destFile))
                        {
                            // Можно добавить диалог перезаписи, но для простоты пропускаем
                            continue;
                        }
                        File.Copy(file, destFile);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка копирования файла {file}: {ex.Message}");
                    }
                }

                LoadFiles(destFolder);
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
