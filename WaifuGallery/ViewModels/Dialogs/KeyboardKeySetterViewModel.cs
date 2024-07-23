﻿using System;
using Avalonia.Input;
using ReactiveUI;
using Serilog;

namespace WaifuGallery.ViewModels.Dialogs;

public class KeyboardKeySetterViewModel : ViewModelBase
{
    #region Private Fields

    private KeyGesture? _keyGesture;
    private string _keyName = string.Empty;

    #endregion

    #region CTOR

    public KeyboardKeySetterViewModel()
    {
        this.WhenAnyValue(x => x.KeyGesture).Subscribe(x =>
        {
            Log.Debug("x: {S}", x?.ToString());
            Log.Debug("KeyGesture: {S}", KeyGesture?.ToString());
            KeyName = KeyGesture?.ToString() ?? "";
        });
    }

    #endregion

    #region Public Properties

    public KeyGesture? KeyGesture
    {
        get => _keyGesture;
        set => this.RaiseAndSetIfChanged(ref _keyGesture, value);
    }

    public string KeyName
    {
        get => _keyName;
        set => this.RaiseAndSetIfChanged(ref _keyName, value);
    }

    #endregion
}