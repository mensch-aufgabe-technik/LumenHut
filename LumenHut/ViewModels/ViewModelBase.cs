using CommunityToolkit.Mvvm.ComponentModel;
using LumenHut.Services;

namespace LumenHut.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    /// <summary>Localized UI text; bound from XAML as <c>{Binding S.Xyz}</c>.</summary>
    public Strings S => Strings.Instance;
}
