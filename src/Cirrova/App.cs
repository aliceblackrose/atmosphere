using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Cirrova;

public partial class App : Application
{
    // All application styling is declared in C#; no runtime or compiled XAML is required.
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        var s1 = new Style(x => x.OfType<Window>());
        s1.Setters.Add(new Setter(Window.FontFamilyProperty, new FontFamily("Segoe UI,Inter,Arial")));
        s1.Setters.Add(new Setter(Window.ForegroundProperty, Brush.Parse("#EAF3FA")));
        Styles.Add(s1);

        var s2 = new Style(x => x.OfType<Button>());
        s2.Setters.Add(new Setter(Button.CornerRadiusProperty, new CornerRadius(12)));
        s2.Setters.Add(new Setter(Button.MinHeightProperty, 39d));
        s2.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(17, 10)));
        s2.Setters.Add(new Setter(Button.BackgroundProperty, Brush.Parse("#1D2A3B")));
        s2.Setters.Add(new Setter(Button.ForegroundProperty, Brush.Parse("#E6F0F8")));
        s2.Setters.Add(new Setter(Button.BorderBrushProperty, Brush.Parse("#2C4053")));
        s2.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(1)));
        s2.Setters.Add(new Setter(Button.FontWeightProperty, FontWeight.SemiBold));
        Styles.Add(s2);

        var s3 = new Style(x => x.OfType<Button>().Class(":pointerover"));
        s3.Setters.Add(new Setter(Button.BackgroundProperty, Brush.Parse("#293C50")));
        Styles.Add(s3);

        var s4 = new Style(x => x.OfType<Button>().Class("primary"));
        s4.Setters.Add(new Setter(Button.BackgroundProperty, Brush.Parse("#65DCC5")));
        s4.Setters.Add(new Setter(Button.BorderBrushProperty, Brush.Parse("#9AEDD9")));
        s4.Setters.Add(new Setter(Button.ForegroundProperty, Brush.Parse("#0B2A2C")));
        Styles.Add(s4);

        var s5 = new Style(x => x.OfType<Button>().Class("primary").Class(":pointerover"));
        s5.Setters.Add(new Setter(Button.BackgroundProperty, Brush.Parse("#86EAD4")));
        Styles.Add(s5);

        var s6 = new Style(x => x.OfType<Button>().Class("danger"));
        s6.Setters.Add(new Setter(Button.BackgroundProperty, Brush.Parse("#3B2531")));
        s6.Setters.Add(new Setter(Button.BorderBrushProperty, Brush.Parse("#845069")));
        s6.Setters.Add(new Setter(Button.ForegroundProperty, Brush.Parse("#FFD5DE")));
        Styles.Add(s6);

        var s7 = new Style(x => x.OfType<Button>().Class("danger").Class(":pointerover"));
        s7.Setters.Add(new Setter(Button.BackgroundProperty, Brush.Parse("#543044")));
        Styles.Add(s7);

        var s8 = new Style(x => x.OfType<Button>().Class("subtle"));
        s8.Setters.Add(new Setter(Button.BackgroundProperty, Brush.Parse("#152130")));
        s8.Setters.Add(new Setter(Button.BorderBrushProperty, Brush.Parse("#2A3B4D")));
        Styles.Add(s8);

        var s9 = new Style(x => x.OfType<TextBox>());
        s9.Setters.Add(new Setter(TextBox.BackgroundProperty, Brush.Parse("#111C29")));
        s9.Setters.Add(new Setter(TextBox.ForegroundProperty, Brush.Parse("#F2F8FD")));
        s9.Setters.Add(new Setter(TextBox.BorderBrushProperty, Brush.Parse("#32465B")));
        s9.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(1)));
        s9.Setters.Add(new Setter(TextBox.CornerRadiusProperty, new CornerRadius(12)));
        s9.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(13, 10)));
        s9.Setters.Add(new Setter(TextBox.MinHeightProperty, 42d));
        Styles.Add(s9);

        var s10 = new Style(x => x.OfType<TextBox>().Class(":focus"));
        s10.Setters.Add(new Setter(TextBox.BorderBrushProperty, Brush.Parse("#65DCC5")));
        Styles.Add(s10);

        var s11 = new Style(x => x.OfType<ListBox>());
        s11.Setters.Add(new Setter(ListBox.BackgroundProperty, Brush.Parse("#101A26")));
        s11.Setters.Add(new Setter(ListBox.BorderBrushProperty, Brush.Parse("#27394A")));
        s11.Setters.Add(new Setter(ListBox.BorderThicknessProperty, new Thickness(1)));
        s11.Setters.Add(new Setter(ListBox.CornerRadiusProperty, new CornerRadius(10)));
        s11.Setters.Add(new Setter(ListBox.PaddingProperty, new Thickness(6)));
        Styles.Add(s11);

        var s12 = new Style(x => x.OfType<CheckBox>());
        s12.Setters.Add(new Setter(CheckBox.ForegroundProperty, Brush.Parse("#DAE8F2")));
        Styles.Add(s12);

        var s13 = new Style(x => x.OfType<Border>().Class("panel"));
        s13.Setters.Add(new Setter(Border.BackgroundProperty, Brush.Parse("#162231")));
        s13.Setters.Add(new Setter(Border.BorderBrushProperty, Brush.Parse("#2A3B4D")));
        s13.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1)));
        s13.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(18)));
        Styles.Add(s13);

        var s14 = new Style(x => x.OfType<Border>().Class("soft-panel"));
        s14.Setters.Add(new Setter(Border.BackgroundProperty, Brush.Parse("#111B29")));
        s14.Setters.Add(new Setter(Border.BorderBrushProperty, Brush.Parse("#253747")));
        s14.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1)));
        s14.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(14)));
        Styles.Add(s14);

        var s15 = new Style(x => x.OfType<TextBlock>().Class("page-title"));
        s15.Setters.Add(new Setter(TextBlock.FontSizeProperty, 29d));
        s15.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeight.Bold));
        s15.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush.Parse("#F2F8FD")));
        Styles.Add(s15);

        var s16 = new Style(x => x.OfType<TextBlock>().Class("eyebrow"));
        s16.Setters.Add(new Setter(TextBlock.FontSizeProperty, 11d));
        s16.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeight.Bold));
        s16.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush.Parse("#7EDFCB")));
        Styles.Add(s16);

        var s17 = new Style(x => x.OfType<TextBlock>().Class("section-title"));
        s17.Setters.Add(new Setter(TextBlock.FontSizeProperty, 16d));
        s17.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeight.SemiBold));
        s17.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush.Parse("#EDF6FA")));
        Styles.Add(s17);

        var s18 = new Style(x => x.OfType<TextBlock>().Class("muted"));
        s18.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush.Parse("#9DB0C3")));
        s18.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        Styles.Add(s18);

        var s19 = new Style(x => x.OfType<TabItem>());
        s19.Setters.Add(new Setter(TabItem.WidthProperty, 216d));
        s19.Setters.Add(new Setter(TabItem.HeightProperty, 50d));
        s19.Setters.Add(new Setter(TabItem.PaddingProperty, new Thickness(16, 12)));
        s19.Setters.Add(new Setter(TabItem.MarginProperty, new Thickness(9, 4)));
        s19.Setters.Add(new Setter(TabItem.FontSizeProperty, 14d));
        s19.Setters.Add(new Setter(TabItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
        s19.Setters.Add(new Setter(TabItem.ForegroundProperty, Brush.Parse("#9BAEC1")));
        s19.Setters.Add(new Setter(TabItem.BackgroundProperty, Brush.Parse("Transparent")));
        Styles.Add(s19);

        var s20 = new Style(x => x.OfType<TabItem>().Class(":selected"));
        s20.Setters.Add(new Setter(TabItem.ForegroundProperty, Brush.Parse("#FFFFFF")));
        s20.Setters.Add(new Setter(TabItem.BackgroundProperty, Brush.Parse("#23394B")));
        s20.Setters.Add(new Setter(TabItem.BorderBrushProperty, Brush.Parse("#65DCC5")));
        s20.Setters.Add(new Setter(TabItem.BorderThicknessProperty, new Thickness(3, 0, 0, 0)));
        Styles.Add(s20);

        var s21 = new Style(x => x.OfType<TabItem>().Class(":pointerover"));
        s21.Setters.Add(new Setter(TabItem.ForegroundProperty, Brush.Parse("#FFFFFF")));
        Styles.Add(s21);

        // Selection, search filters, and lists use the same visual language as the navigation.
        var listItem = new Style(x => x.OfType<ListBoxItem>());
        listItem.Setters.Add(new Setter(ListBoxItem.MinHeightProperty, 38d));
        listItem.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(12, 9)));
        listItem.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(2, 2)));
        listItem.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Brush.Parse("#D5E3EF")));
        Styles.Add(listItem);

        var listItemHover = new Style(x => x.OfType<ListBoxItem>().Class(":pointerover"));
        listItemHover.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brush.Parse("#223548")));
        Styles.Add(listItemHover);

        var listItemSelected = new Style(x => x.OfType<ListBoxItem>().Class(":selected"));
        listItemSelected.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brush.Parse("#284C55")));
        listItemSelected.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Brush.Parse("#F1FFFC")));
        Styles.Add(listItemSelected);

        var combo = new Style(x => x.OfType<ComboBox>());
        combo.Setters.Add(new Setter(ComboBox.BackgroundProperty, Brush.Parse("#111C29")));
        combo.Setters.Add(new Setter(ComboBox.ForegroundProperty, Brush.Parse("#EAF3FA")));
        combo.Setters.Add(new Setter(ComboBox.BorderBrushProperty, Brush.Parse("#32465B")));
        combo.Setters.Add(new Setter(ComboBox.BorderThicknessProperty, new Thickness(1)));
        combo.Setters.Add(new Setter(ComboBox.PaddingProperty, new Thickness(12, 9)));
        combo.Setters.Add(new Setter(ComboBox.MinHeightProperty, 42d));
        Styles.Add(combo);

        var progress = new Style(x => x.OfType<ProgressBar>());
        progress.Setters.Add(new Setter(ProgressBar.ForegroundProperty, Brush.Parse("#65DCC5")));
        progress.Setters.Add(new Setter(ProgressBar.BackgroundProperty, Brush.Parse("#253748")));
        Styles.Add(progress);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
