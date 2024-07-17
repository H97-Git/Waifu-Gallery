﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DynamicData;
using FluentAvalonia.UI.Controls;
using NaturalSort.Extension;
using ReactiveUI;
using WaifuGallery.Commands;
using WaifuGallery.Helpers;
using WaifuGallery.Models;

namespace WaifuGallery.ViewModels.FileManager;

public class FileManagerViewModel : ViewModelBase
{
    #region Private Fields

    private Brush? _fileManagerBackground;
    private CancellationTokenSource _searchCancellationTokenSource = new();
    private bool _isFileManagerExpanded = true;
    private bool _isFileManagerVisible = true;
    private bool _isPointerOver;
    private bool _isSearchFocused;
    private const int BatchSize = 5;
    private int _columnsCount;
    private int _selectedIndexInFileManager;
    private readonly FileManagerHistory _pathHistory = new();
    private string _currentPath = string.Empty;

    #endregion

    #region Private Methods

    private void SortFileInDir()
    {
        FilesInDir = new ObservableCollection<FileViewModel>(FilesInDir.OrderBy(f => f.FileName,
            StringComparison.OrdinalIgnoreCase.WithNaturalSort()));
    }

    private void RefreshFileManager(IFileCommand fileCommand)
    {
        switch (fileCommand)
        {
            case NewFolderCommand newFolderCommand:
                var newFolderInfo = new DirectoryInfo(newFolderCommand.Path);
                AddToCollectionAndSort(new FileViewModel(newFolderInfo));
                break;
            case DeleteCommand deleteCommand:
                var fvm = FilesInDir.FirstOrDefault(x => x.FullPath == deleteCommand.Path);
                if (fvm is not null)
                    FilesInDir.Remove(fvm);
                break;
            case PasteCommand pasteCommand:
                if (File.Exists(pasteCommand.Path))
                {
                    var fileInfo = new FileInfo(pasteCommand.Path);
                    AddToCollectionAndSort(new FileViewModel(fileInfo));
                    return;
                }

                if (Directory.Exists(pasteCommand.Path))
                {
                    var dirInfo = new DirectoryInfo(pasteCommand.Path);
                    AddToCollectionAndSort(new FileViewModel(dirInfo));
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fileCommand), fileCommand, null);
        }
    }

    private void GetFilesFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        //Remove last backslash if it exists
        if (path.Last() is '\\')
        {
            path = path[..^1];
        }

        _searchCancellationTokenSource.Cancel();
        _searchCancellationTokenSource = new CancellationTokenSource();

        var currentPathDirectoryInfo = new DirectoryInfo(path);
        if (!currentPathDirectoryInfo.Exists)
        {
            SendMessageToStatusBar(InfoBarSeverity.Warning, "The path does not exist");
            return;
        }

        Settings.Instance.FileManagerPreference.FileManagerLastPath = path;
        FilesInDir.Clear();
        SetDirsAndFiles(currentPathDirectoryInfo, _searchCancellationTokenSource.Token);
    }

    private async void SetDirsAndFiles(DirectoryInfo currentDir, CancellationToken token)
    {
        var dirs = currentDir.GetDirectories().Where(f => f.Name.First() is not '.' && f.Name.First() is not '$')
            .OrderBy(f => f.Name, StringComparison.OrdinalIgnoreCase.WithNaturalSort())
            .ToArray();
        var imagesFileInfo = currentDir.GetFiles()
            .Where(file => Helper.AllFileExtensions.Contains(file.Extension.ToLower()))
            .OrderBy(f => f.Name, StringComparison.OrdinalIgnoreCase.WithNaturalSort())
            .ToArray();
        if (dirs is not {Length: 0})
            await SetDirs(dirs, token);
        if (imagesFileInfo is not {Length: 0})
            await SetFiles(imagesFileInfo, token);
    }

    private async Task SetDirs(IEnumerable<DirectoryInfo> dirs, CancellationToken token)
    {
        var dirInfos = dirs.ToList();
        for (var i = 0; i < dirInfos.Count; i += BatchSize)
        {
            if (token.IsCancellationRequested)
            {
                FilesInDir.Clear();
                break;
            }

            var batch = dirInfos.Skip(i).Take(BatchSize);
            var batchList = batch.Select(dir => new FileViewModel(dir));
            await Dispatcher.UIThread.InvokeAsync(() => FilesInDir.AddRange(batchList));
            await Task.Delay(100, CancellationToken.None);
        }
    }

    private async Task SetFiles(IEnumerable<FileInfo> files, CancellationToken token)
    {
        var fileInfos = files.ToList();
        for (var i = 0; i < fileInfos.Count; i += BatchSize)
        {
            if (token.IsCancellationRequested)
            {
                FilesInDir.Clear();
                break;
            }

            var batch = fileInfos.Skip(i).Take(BatchSize);
            var batchList = batch.Select(file => new FileViewModel(file));
            await Dispatcher.UIThread.InvokeAsync(() => FilesInDir.AddRange(batchList));
            await Task.Delay(100, CancellationToken.None);
        }
    }

    private async void OpenPathInFileManager()
    {
        var storageProvider = App.GetTopLevel()?.StorageProvider;
        if (storageProvider is null) return;
        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
        {
            Title = "Open Folder",
            AllowMultiple = false,
        });

        if (result.Count is 0) return;
        var path = result[0].Path;
        ChangePath(path.LocalPath);
    }

    private void GoForwardHistory()
    {
        var path = _pathHistory.GoForward();
        UpdatePath(path);
    }

    private void GoBackwardHistory()
    {
        var path = _pathHistory.GoBack();
        UpdatePath(path);
    }

    private void UpdatePath(string path) => CurrentPath = path;

    private void AddToCollectionAndSort(FileViewModel fileViewModel)
    {
        var buffer = FilesInDir.ToList();
        buffer.Add(fileViewModel);
        buffer = buffer.OrderBy(x => x.FileName, StringComparison.OrdinalIgnoreCase.WithNaturalSort()).ToList();
        FilesInDir.Clear();
        FilesInDir.AddRange(buffer);
    }

    #endregion

    #region CTOR

    public FileManagerViewModel()
    {
        FileManagerBackground = new SolidColorBrush(Colors.Transparent);
        this.WhenAnyValue(x => x.CurrentPath)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Subscribe(GetFilesFromPath);
        this.WhenAnyValue(x => x.IsFileManagerExpanded)
            .Subscribe(_ =>
                FileManagerBackground = IsFileManagerExpanded ? new SolidColorBrush(Colors.Transparent) : null);
        this.WhenAnyValue(x => x.PreviewImageViewModel.IsPreviewImageVisible)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ScrollBarVisibility)));

        if (Settings.Instance.FileManagerPreference.ShouldSaveLastPathOnExit &&
            Settings.Instance.FileManagerPreference.FileManagerLastPath is not null)
        {
            var path = Settings.Instance.FileManagerPreference.FileManagerLastPath;
            if (Directory.Exists(path))
            {
                ChangePath(path);
            }
            else
            {
                SendMessageToStatusBar(InfoBarSeverity.Error, "The saved path does not exist anymore");
                ChangePath(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            }
        }
        else
        {
            ChangePath(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        }

        MessageBus.Current.Listen<ChangePathCommand>().Subscribe(x => ChangePath(x.Path));
        MessageBus.Current.Listen<StartPreviewCommand>().Subscribe(x => PreviewImageViewModel.ShowPreview(x.Path));
        MessageBus.Current.Listen<ToggleFileManagerCommand>().Subscribe(_ => ToggleFileManager());
        MessageBus.Current.Listen<ToggleFileManagerVisibilityCommand>().Subscribe(_ => ToggleFileManagerVisibility());
        MessageBus.Current.Listen<RefreshFileManagerCommand>().Subscribe(x => RefreshFileManager(x.FileCommand));
    }

    private void ToggleScrollbarVisibility(bool isPreviewImageVisible)
    {
        // Log.Debug($"""IsPreviewImageVisible: {isPreviewImageVisible}""");
        // Log.Debug($"""ScrollBarVisibility Before: {ScrollBarVisibility}""");
        // if (isPreviewImageVisible)
        //     ScrollBarVisibility = ScrollBarVisibility.Hidden;
        // else
        //     ScrollBarVisibility = ScrollBarVisibility.Visible;
        // Log.Debug($"""ScrollBarVisibility After: {ScrollBarVisibility}""");
    }

    #endregion

    #region Public Properties

    public PreviewImageViewModel PreviewImageViewModel { get; init; } = new();

    public FileViewModel SelectedFile => FilesInDir[SelectedIndex];

    public Brush? FileManagerBackground
    {
        get => _fileManagerBackground;
        set => this.RaiseAndSetIfChanged(ref _fileManagerBackground, value);
    }

    public ObservableCollection<FileViewModel> FilesInDir { get; set; } = [];

    public ScrollBarVisibility ScrollBarVisibility => PreviewImageViewModel.IsPreviewImageVisible
        ? ScrollBarVisibility.Disabled
        : ScrollBarVisibility.Auto;

    public bool IsPointerOver
    {
        get => _isPointerOver;
        set => this.RaiseAndSetIfChanged(ref _isPointerOver, value);
    }

    public bool IsFileManagerVisible
    {
        get => _isFileManagerVisible;
        set => this.RaiseAndSetIfChanged(ref _isFileManagerVisible, value);
    }

    public bool IsFileManagerExpanded
    {
        get => _isFileManagerExpanded;
        set => this.RaiseAndSetIfChanged(ref _isFileManagerExpanded, value);
    }

    public bool IsFileManagerExpandedAndVisible => IsFileManagerExpanded && IsFileManagerVisible;

    public bool IsSearchFocused
    {
        get => _isSearchFocused;
        set => this.RaiseAndSetIfChanged(ref _isSearchFocused, value);
    }

    public int ColumnsCount
    {
        get => _columnsCount;
        set => this.RaiseAndSetIfChanged(ref _columnsCount, value);
    }

    public int SelectedIndex
    {
        get => _selectedIndexInFileManager;
        set
        {
            if (value < 0)
                value = 0;

            if (value >= FilesInDir.Count)
                value = FilesInDir.Count - 1;

            this.RaiseAndSetIfChanged(ref _selectedIndexInFileManager, value);
        }
    }

    public string CurrentPath
    {
        get => _currentPath;
        set => this.RaiseAndSetIfChanged(ref _currentPath, value);
    }

    #endregion

    #region Public Methods

    public void GoToParentFolder()
    {
        var parent = Directory.GetParent(CurrentPath)?.FullName;
        if (parent is null) return;
        ChangePath(parent);
    }

    public void ChangePath(string path)
    {
        _pathHistory.AddPath(path);
        UpdatePath(path);
    }

    public void ToggleFileManager() => IsFileManagerExpanded = !IsFileManagerExpanded;

    public void ToggleFileManagerVisibility() => IsFileManagerVisible = !IsFileManagerVisible;

    public void GoUp()
    {
        var isFirstRow = SelectedIndex < ColumnsCount;
        if (isFirstRow) return;
        SelectedIndex -= ColumnsCount;
    }

    public void GoDown()
    {
        var isLastRow = SelectedIndex >= FilesInDir.Count - ColumnsCount;
        if (isLastRow)
        {
            SelectedIndex = FilesInDir.Count - 1;
        }
        else
        {
            SelectedIndex += ColumnsCount;
        }
    }

    public void OpenImageTabFromKeyboardEvent()
    {
        var imagesInPath = Helper.GetAllImagesInPath(SelectedFile);
        var command = SelectedFile.IsImage
            ? new OpenInNewTabCommand(SelectedIndex, imagesInPath)
            : new OpenInNewTabCommand(0, imagesInPath);

        MessageBus.Current.SendMessage(command);
    }

    #endregion

    #region Public Commands

    public ICommand GotoParentFolderCommand => ReactiveCommand.Create(GoToParentFolder);
    public ICommand GotoNextDirCommand => ReactiveCommand.Create(GoForwardHistory);
    public ICommand GotoPreviousDirCommand => ReactiveCommand.Create(GoBackwardHistory);
    public ICommand OpenFileCommand => ReactiveCommand.Create(OpenPathInFileManager);

    public ICommand Paste =>
        ReactiveCommand.Create(() => { MessageBus.Current.SendMessage(new PasteCommand(CurrentPath)); });

    public ICommand CreateNewFolder =>
        ReactiveCommand.Create(() => { MessageBus.Current.SendMessage(new NewFolderCommand(CurrentPath)); });

    public ICommand OpenInExplorer =>
        ReactiveCommand.Create(() => { MessageBus.Current.SendMessage(new OpenInFileExplorerCommand(CurrentPath)); });

    #endregion
}