using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace WorldSeed.App;

/// <summary>Small, reusable glossary help beside a workspace heading.</summary>
public sealed class HelpIcon : Button
{
    private string _helpText = "";
    public string HelpText
    {
        get => _helpText;
        set
        {
            _helpText = value;
            ToolTip.SetTip(this, new TextBlock { Text = value, MaxWidth = 340, TextWrapping = TextWrapping.Wrap });
            AutomationProperties.SetHelpText(this, value);
        }
    }

    public HelpIcon()
    {
        Content = "ⓘ";
        Padding = new Thickness(5, 1);
        Margin = new Thickness(6, 0, 0, 0);
        Background = Brushes.Transparent;
        AutomationProperties.SetName(this, "Help");
    }
}
