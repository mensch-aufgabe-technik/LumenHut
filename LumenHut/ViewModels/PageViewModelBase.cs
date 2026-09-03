using System;
using System.ComponentModel;
using LumenHut.Services;

namespace LumenHut.ViewModels;

/// <summary>
/// Base for the three page view models. Status text is held as a render function rather than a
/// finished string so that switching the UI language also re-renders the message that is
/// currently on screen.
/// </summary>
public abstract class PageViewModelBase : ViewModelBase, IDisposable
{
    private Func<Strings, string> _status;

    protected PageViewModelBase()
    {
        _status = _ => string.Empty;
        Strings.Instance.PropertyChanged += OnStringsChanged;
    }

    public string StatusMessage => _status(S);

    protected void SetStatus(Func<Strings, string> render)
    {
        _status = render;
        OnPropertyChanged(nameof(StatusMessage));
    }

    /// <summary>Status text with no localized counterpart (e.g. an exception message passed through).</summary>
    protected void SetStatusRaw(string message) => SetStatus(_ => message);

    private void OnStringsChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(StatusMessage));
        OnLanguageChanged();
    }

    /// <summary>
    /// Called after the UI language changed. Pages override this to re-render text they built
    /// themselves — formatted numbers and dates in particular, which are produced once at
    /// projection time rather than bound to <see cref="Strings"/>.
    /// </summary>
    protected virtual void OnLanguageChanged()
    {
    }

    public virtual void Dispose() => Strings.Instance.PropertyChanged -= OnStringsChanged;
}
