using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Atmosphere;

public partial class App : Application
{
    // All application styling is declared in C#; no runtime or compiled XAML is required.
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        var s1 = new Style(x => x.OfType<Window>());
        s1.Setters.Add(new Setter(Window.FontFamilyProperty, new FontFamily("Segoe UI,Inter,Arial")));
        s1.Setters.Add(new Setter(Window.ForegroundProperty, Brush.Parse("#E8EEFA")));
        Styles.Add(s1);

        var s2 = new Style(x => x.OfType<Button>());
        s2.Setters.Add(new Setter(Button.CornerRadiusProperty, new CornerRadius(10)));
        s2.Setters.Add(new Setter(Button.MinHeightProperty, 39d));
        s2.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(16, 10)));
        s2.Setters.Add(new Setter(Button.BackgroundProperty, Brush.Parse("#253149")));
        s2.Setters.Add(new Setter(Button.ForegroundProperty, Brush.Parse("#E6EDF9")));
        s2.Setters.Add(new Setter(Button.BorderBrushProperty, Brush.Parse("#38445E")));
        s2.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(1)));
        s2.Setters.Add(new Setter(Button.FontWeightProperty, FontWeight.SemiBold));
        Styles.Add(s2);

        var s3 = new Style(x => x.OfType<Button>().Class(":pointerover"));
        s3.Setters.Add(new Setter(Button.BackgroundProperty, Brush.Parse("#35425D")));
        Styles.Add(s3);

        var s4 = new Style(x => x.OfType<Button>().Class("primary"));
        s4.Setters.Add(new Setter(Button.BackgroundProperty, Brush.Parse("#6B68EA")));
        s4.Setters.Add(new Setter(Button.BorderBrushProperty, Brush.Parse("#8382FA")));
        s4.Setters.Add(new Setter(Button.ForegroundProperty, Brush.Parse("White")));
        Styles.Add(s4);

        var s5 = new Style(x => x.OfType<Button>().Class("primary").Class(":pointerover"));
        s5.Setters.Add(new Setter(Button.BackgroundProperty, Brush.Parse("#8180F5")));
        Styles.Add(s5);

        var s6 = new Style(x => x.OfType<Button>().Class("danger"));
        s6.Setters.Add(new Setter(Button.BackgroundProperty, Brush.Parse("#3D2535")));
        s6.Setters.Add(new Setter(Button.BorderBrushProperty, Brush.Parse("#895064")));
        s6.Setters.Add(new Setter(Button.ForegroundProperty, Brush.Parse("#FFCAD4")));
        Styles.Add(s6);

        var s7 = new Style(x => x.OfType<Button>().Class("danger").Class(":pointerover"));
        s7.Setters.Add(new Setter(Button.BackgroundProperty, Brush.Parse("#593247")));
        Styles.Add(s7);

        var s8 = new Style(x => x.OfType<Button>().Class("subtle"));
        s8.Setters.Add(new Setter(Button.BackgroundProperty, Brush.Parse("#192337")));
        s8.Setters.Add(new Setter(Button.BorderBrushProperty, Brush.Parse("#303B50")));
        Styles.Add(s8);

        var s9 = new Style(x => x.OfType<TextBox>());
        s9.Setters.Add(new Setter(TextBox.BackgroundProperty, Brush.Parse("#121B2E")));
        s9.Setters.Add(new Setter(TextBox.ForegroundProperty, Brush.Parse("#F4F6FF")));
        s9.Setters.Add(new Setter(TextBox.BorderBrushProperty, Brush.Parse("#35425A")));
        s9.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(1)));
        s9.Setters.Add(new Setter(TextBox.CornerRadiusProperty, new CornerRadius(10)));
        s9.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(13, 10)));
        s9.Setters.Add(new Setter(TextBox.MinHeightProperty, 42d));
        Styles.Add(s9);

        var s10 = new Style(x => x.OfType<TextBox>().Class(":focus"));
        s10.Setters.Add(new Setter(TextBox.BorderBrushProperty, Brush.Parse("#8888FD")));
        Styles.Add(s10);

        var s11 = new Style(x => x.OfType<ListBox>());
        s11.Setters.Add(new Setter(ListBox.BackgroundProperty, Brush.Parse("#10192A")));
        s11.Setters.Add(new Setter(ListBox.BorderBrushProperty, Brush.Parse("#26334A")));
        s11.Setters.Add(new Setter(ListBox.BorderThicknessProperty, new Thickness(1)));
        s11.Setters.Add(new Setter(ListBox.CornerRadiusProperty, new CornerRadius(10)));
        s11.Setters.Add(new Setter(ListBox.PaddingProperty, new Thickness(6)));
        Styles.Add(s11);

        var s12 = new Style(x => x.OfType<CheckBox>());
        s12.Setters.Add(new Setter(CheckBox.ForegroundProperty, Brush.Parse("#D9E2F3")));
        Styles.Add(s12);

        var s13 = new Style(x => x.OfType<Border>().Class("panel"));
        s13.Setters.Add(new Setter(Border.BackgroundProperty, Brush.Parse("#192338")));
        s13.Setters.Add(new Setter(Border.BorderBrushProperty, Brush.Parse("#2A3650")));
        s13.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1)));
        s13.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(16)));
        Styles.Add(s13);

        var s14 = new Style(x => x.OfType<Border>().Class("soft-panel"));
        s14.Setters.Add(new Setter(Border.BackgroundProperty, Brush.Parse("#131D30")));
        s14.Setters.Add(new Setter(Border.BorderBrushProperty, Brush.Parse("#27334B")));
        s14.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1)));
        s14.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(13)));
        Styles.Add(s14);

        var s15 = new Style(x => x.OfType<TextBlock>().Class("page-title"));
        s15.Setters.Add(new Setter(TextBlock.FontSizeProperty, 29d));
        s15.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeight.Bold));
        s15.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush.Parse("#F4F6FF")));
        Styles.Add(s15);

        var s16 = new Style(x => x.OfType<TextBlock>().Class("eyebrow"));
        s16.Setters.Add(new Setter(TextBlock.FontSizeProperty, 11d));
        s16.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeight.Bold));
        s16.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush.Parse("#9AA9C9")));
        Styles.Add(s16);

        var s17 = new Style(x => x.OfType<TextBlock>().Class("section-title"));
        s17.Setters.Add(new Setter(TextBlock.FontSizeProperty, 16d));
        s17.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeight.SemiBold));
        s17.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush.Parse("#F0F4FD")));
        Styles.Add(s17);

        var s18 = new Style(x => x.OfType<TextBlock>().Class("muted"));
        s18.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush.Parse("#ADBBD2")));
        s18.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        Styles.Add(s18);

        var s19 = new Style(x => x.OfType<TabItem>());
        s19.Setters.Add(new Setter(TabItem.WidthProperty, 230d));
        s19.Setters.Add(new Setter(TabItem.HeightProperty, 54d));
        s19.Setters.Add(new Setter(TabItem.PaddingProperty, new Thickness(15, 12)));
        s19.Setters.Add(new Setter(TabItem.MarginProperty, new Thickness(8, 3)));
        s19.Setters.Add(new Setter(TabItem.FontSizeProperty, 15d));
        s19.Setters.Add(new Setter(TabItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
        s19.Setters.Add(new Setter(TabItem.ForegroundProperty, Brush.Parse("#A9B9D0")));
        s19.Setters.Add(new Setter(TabItem.BackgroundProperty, Brush.Parse("Transparent")));
        Styles.Add(s19);

        var s20 = new Style(x => x.OfType<TabItem>().Class(":selected"));
        s20.Setters.Add(new Setter(TabItem.ForegroundProperty, Brush.Parse("#FFFFFF")));
        s20.Setters.Add(new Setter(TabItem.BackgroundProperty, Brush.Parse("#263653")));
        Styles.Add(s20);

        var s21 = new Style(x => x.OfType<TabItem>().Class(":pointerover"));
        s21.Setters.Add(new Setter(TabItem.ForegroundProperty, Brush.Parse("#FFFFFF")));
        Styles.Add(s21);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
