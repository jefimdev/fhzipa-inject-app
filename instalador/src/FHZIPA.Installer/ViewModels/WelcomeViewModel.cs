using FHZIPA.Installer.Localization;
using FHZIPA.Installer.Models;

namespace FHZIPA.Installer.ViewModels;

/// <summary>
/// A tela de boas-vindas.
/// </summary>
public sealed class WelcomeViewModel : StepViewModelBase
{
    public WelcomeViewModel(MainViewModel main) : base(main)
    {
    }

    public override bool CanContinue => true;

    public override bool CanGoBack => false;

    public string Title => Strings.Get("Welcome_Title");

    public string Subtitle => Strings.Get("Welcome_Subtitle");

    public string InstallText => Strings.Get("Btn_Install");

    public string InfoTitle => Strings.Get("Welcome_Info_Title");

    public string InfoBody => Strings.Get("Welcome_Info_Body");

    public string WarningTitle => Strings.Get("Welcome_Warning_Title");

    public string WarningBody => Strings.Get("Welcome_Warning_Body");

    public string Feature1Title => Strings.Get("Welcome_Feature1_Title");

    public string Feature1Body => Strings.Get("Welcome_Feature1_Body");

    public string Feature2Title => Strings.Get("Welcome_Feature2_Title");

    public string Feature2Body => Strings.Get("Welcome_Feature2_Body");

    public string Feature3Title => Strings.Get("Welcome_Feature3_Title");

    public string Feature3Body => Strings.Get("Welcome_Feature3_Body");

    public override Task ContinueAsync()
    {
        Main.SetState(InstallerState.DirectorySelection);
        Main.Show(Main.Directory, Strings.Get("Btn_Install"));
        return Task.CompletedTask;
    }

    public override Task BackAsync() => Task.CompletedTask;
}
