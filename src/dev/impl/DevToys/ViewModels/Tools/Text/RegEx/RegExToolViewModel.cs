﻿#nullable enable

using System;
using System.Collections.Generic;
using System.Composition;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevToys.Api.Core;
using DevToys.Api.Core.Settings;
using DevToys.Api.Tools;
using DevToys.Core.Threading;
using DevToys.Shared.Core.Threading;
using DevToys.UI.Controls;
using DevToys.Views.Tools.RegEx;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace DevToys.ViewModels.Tools.RegEx
{
    [Export(typeof(RegExToolViewModel))]
    public sealed class RegExToolViewModel : ObservableRecipient, IToolViewModel
    {
        private static readonly SettingDefinition<bool> IgnoreCaseSetting
            = new(
                name: $"{nameof(RegExToolViewModel)}.{nameof(IgnoreCaseSetting)}",
                isRoaming: true,
                defaultValue: false);

        private static readonly SettingDefinition<bool> IgnoreWhitespaceSetting
            = new(
                name: $"{nameof(RegExToolViewModel)}.{nameof(IgnoreWhitespaceSetting)}",
                isRoaming: true,
                defaultValue: false);

        private static readonly SettingDefinition<bool> CultureInvariantSetting
            = new(
                name: $"{nameof(RegExToolViewModel)}.{nameof(CultureInvariantSetting)}",
                isRoaming: true,
                defaultValue: false);

        private static readonly SettingDefinition<bool> SinglelineSetting
            = new(
                name: $"{nameof(RegExToolViewModel)}.{nameof(SinglelineSetting)}",
                isRoaming: true,
                defaultValue: false);

        private static readonly SettingDefinition<bool> MultilineSetting
            = new(
                name: $"{nameof(RegExToolViewModel)}.{nameof(MultilineSetting)}",
                isRoaming: true,
                defaultValue: false);

        private static readonly SettingDefinition<bool> RightToLeftSetting
            = new(
                name: $"{nameof(RegExToolViewModel)}.{nameof(RightToLeftSetting)}",
                isRoaming: true,
                defaultValue: false);

        private static readonly SettingDefinition<bool> EcmaScriptSetting
            = new(
                name: $"{nameof(RegExToolViewModel)}.{nameof(EcmaScriptSetting)}",
                isRoaming: true,
                defaultValue: false);

        private readonly IMarketingService _marketingService;
        private readonly Queue<(string pattern, string text)> _regExMatchingQueue = new();

        private bool _toolSuccessfullyWorked;
        private bool _calculationInProgress;
        private string? _regularExpression;
        private string? _text;

        public Type View { get; } = typeof(RegExToolPage);

        internal RegExStrings Strings => LanguageManager.Instance.RegEx;

        internal ISettingsProvider SettingsProvider { get; }

        internal bool IgnoreCase
        {
            get => SettingsProvider.GetSetting(IgnoreCaseSetting);
            set
            {
                if (SettingsProvider.GetSetting(IgnoreCaseSetting) != value)
                {
                    SettingsProvider.SetSetting(IgnoreCaseSetting, value);
                    OnPropertyChanged();
                    QueueRegExMatch();
                }
            }
        }

        internal bool IgnoreWhitespace
        {
            get => SettingsProvider.GetSetting(IgnoreWhitespaceSetting);
            set
            {
                if (SettingsProvider.GetSetting(IgnoreWhitespaceSetting) != value)
                {
                    SettingsProvider.SetSetting(IgnoreWhitespaceSetting, value);
                    OnPropertyChanged();
                    QueueRegExMatch();
                }
            }
        }

        internal bool CultureInvariant
        {
            get => SettingsProvider.GetSetting(CultureInvariantSetting);
            set
            {
                if (SettingsProvider.GetSetting(CultureInvariantSetting) != value)
                {
                    SettingsProvider.SetSetting(CultureInvariantSetting, value);
                    OnPropertyChanged();
                    QueueRegExMatch();
                }
            }
        }

        internal bool Singleline
        {
            get => SettingsProvider.GetSetting(SinglelineSetting);
            set
            {
                if (SettingsProvider.GetSetting(SinglelineSetting) != value)
                {
                    SettingsProvider.SetSetting(SinglelineSetting, value);
                    OnPropertyChanged();
                    QueueRegExMatch();
                }
            }
        }

        internal bool Multiline
        {
            get => SettingsProvider.GetSetting(MultilineSetting);
            set
            {
                if (SettingsProvider.GetSetting(MultilineSetting) != value)
                {
                    SettingsProvider.SetSetting(MultilineSetting, value);
                    OnPropertyChanged();
                    QueueRegExMatch();
                }
            }
        }

        internal bool RightToLeft
        {
            get => SettingsProvider.GetSetting(RightToLeftSetting);
            set
            {
                if (SettingsProvider.GetSetting(RightToLeftSetting) != value)
                {
                    SettingsProvider.SetSetting(RightToLeftSetting, value);
                    OnPropertyChanged();
                    QueueRegExMatch();
                }
            }
        }

        internal bool EcmaScript
        {
            get => SettingsProvider.GetSetting(EcmaScriptSetting);
            set
            {
                if (SettingsProvider.GetSetting(EcmaScriptSetting) != value)
                {
                    SettingsProvider.SetSetting(EcmaScriptSetting, value);
                    OnPropertyChanged();
                    QueueRegExMatch();
                }
            }
        }

        internal string? RegularExpression
        {
            get => _regularExpression;
            set
            {
                SetProperty(ref _regularExpression, value);
                QueueRegExMatch();
            }
        }

        internal string? Text
        {
            get => _text;
            set
            {
                SetProperty(ref _text, value);
                QueueRegExMatch();
            }
        }

        internal ICustomTextBox? MatchTextBox { private get; set; }

        [ImportingConstructor]
        public RegExToolViewModel(ISettingsProvider settingsProvider, IMarketingService marketingService)
        {
            SettingsProvider = settingsProvider;
            _marketingService = marketingService;
        }

        private void QueueRegExMatch()
        {
            _regExMatchingQueue.Enqueue(new(RegularExpression ?? string.Empty, Text ?? string.Empty));
            TreatQueueAsync().Forget();
        }

        private async Task TreatQueueAsync()
        {
            if (_calculationInProgress)
            {
                return;
            }

            _calculationInProgress = true;

            Color highlighterForegroundColor;
            Color highlighterBackgroundColor;
            await ThreadHelper.RunOnUIThreadAsync(() =>
            {
                ElementTheme currentTheme = ((Frame)Window.Current.Content).ActualTheme;
                string? highlighterBackgroundResourceName = currentTheme == ElementTheme.Light ? "SystemAccentColorLight2" : "SystemAccentColorDark1";
                highlighterForegroundColor = currentTheme == ElementTheme.Light ? Colors.Black : Colors.White;
                highlighterBackgroundColor = (Color)Application.Current.Resources[highlighterBackgroundResourceName];
            });

            await TaskScheduler.Default;

            while (_regExMatchingQueue.TryDequeue(out (string pattern, string text) data))
            {
                var spans = new List<HighlightSpan>();

                try
                {
                    string? pattern = data.pattern.Trim('/');

                    var regex = new Regex(data.pattern, GetOptions());
                    MatchCollection matches = regex.Matches(data.text);

                    foreach (Match match in matches)
                    {
                        int lineCount = CountLines(data.text, match.Index);
                        spans.Add(
                            new HighlightSpan()
                            {
                                StartIndex = match.Index - lineCount,
                                Length = match.Length,
                                BackgroundColor = highlighterBackgroundColor,
                                ForegroundColor = highlighterForegroundColor
                            });
                    }
                }
                catch
                {
                    // TODO: indicate the user that the regex is wrong.
                }

                ThreadHelper.RunOnUIThreadAsync(
                    ThreadPriority.Low,
                    () =>
                    {
                        MatchTextBox?.SetHighlights(spans);

                        if (spans.Count > 0 && !_toolSuccessfullyWorked)
                        {
                            _toolSuccessfullyWorked = true;
                            _marketingService.NotifyToolSuccessfullyWorked();
                        }
                    }).ForgetSafely();
            }

            _calculationInProgress = false;
        }

        private RegexOptions GetOptions()
        {
            RegexOptions options = RegexOptions.None;
            if (EcmaScript)
            {
                options |= RegexOptions.ECMAScript;
            }
            if (CultureInvariant)
            {
                options |= RegexOptions.CultureInvariant;
            }
            if (IgnoreCase)
            {
                options |= RegexOptions.IgnoreCase;
            }
            if (IgnoreWhitespace && !EcmaScript)
            {
                options |= RegexOptions.IgnorePatternWhitespace;
            }
            if (Singleline && !EcmaScript)
            {
                options |= RegexOptions.Singleline;
            }
            if (Multiline)
            {
                options |= RegexOptions.Multiline;
            }
            if (RightToLeft && !EcmaScript)
            {
                options |= RegexOptions.RightToLeft;
            }

            return options;
        }

        private int CountLines(string input, int maxLength)
        {
            int lines = 0;
            int i = 0;
            while (i > -1 && i < maxLength)
            {
                i = input.IndexOf("\r\n", startIndex: i);
                if (i > -1 && i < maxLength)
                {
                    lines++;
                    i++;
                }
            }

            return lines;
        }
    }
}
