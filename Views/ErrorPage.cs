using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace KrakenMobile.Views;

public class ErrorPage : ContentPage
{
    public ErrorPage(string title, string message)
    {
        BackgroundColor = Colors.Black;

        var titleLabel = new Label
        {
            Text = "Kraken failed to start",
            TextColor = Colors.Red,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        };

        var subLabel = new Label
        {
            Text = title,
            TextColor = Colors.Orange,
            FontSize = 16,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var body = new Label
        {
            Text = message ?? "(no details)",
            TextColor = Colors.White,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0)
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Children = { titleLabel, subLabel, body }
            }
        };
    }
}
