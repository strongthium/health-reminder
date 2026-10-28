using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>A notice in the middle of the screen, in front: a coloured band with its title, a few lines, and buttons;
/// <see cref="Answer"/> is the button pressed (0 for the first, the main one), or -1 when it was closed.</summary>
public sealed class NoticeWindow : Window
{
    public int Answer { get; private set; } = -1;

    public NoticeWindow(string title, string heading, string text, Brush band, params string[] buttons)
    {
        Title = title;
        Icon = App.Icon;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(24, 0, 24, 20) };
        for (var i = buttons.Length - 1; i >= 0; i--)
        {
            var index = i;
            var button = new Button { Content = buttons[i], Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(8, 0, 0, 0) };
            if (i == 0) button.Style = (Style)FindResource("AccentButtonStyle");
            button.Click += (_, _) =>
            {
                Answer = index;
                Close();
            };
            buttonRow.Children.Insert(0, button);
        }
        var panel = new DockPanel();
        var top = new Border { Background = band, Padding = new Thickness(24, 18, 24, 18), Child = new TextBlock { Text = heading, Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap } };
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(buttonRow, Dock.Bottom);
        panel.Children.Add(top);
        panel.Children.Add(buttonRow);
        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 15, Margin = new Thickness(24, 16, 24, 16) });
        Content = panel;
    }
}
