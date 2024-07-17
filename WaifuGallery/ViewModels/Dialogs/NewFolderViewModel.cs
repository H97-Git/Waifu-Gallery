﻿using ReactiveUI;

namespace WaifuGallery.ViewModels.Dialogs;

public class UserInputDialog: ViewModelBase
{
    
    private string? _userInput;
    public string? UserInput
    {
        get => _userInput;
        set => this.RaiseAndSetIfChanged(ref _userInput, value);
    }
}