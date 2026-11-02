using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Strongthium.HealthReminder;

/// <summary>
/// Printing something: what goes on it to pick on the left, and the pages as they'll print beside it, made again as
/// it's changed. Printing makes them again on the printer's own paper, each with its title and page number at the foot.
/// </summary>
public sealed partial class PrintWindow : Window
{
    private readonly string title;
    private readonly Func<Size, FlowDocument> build;

    public PrintWindow(string title, FrameworkElement options, Func<Size, FlowDocument> build)
    {
        InitializeComponent();
        Icon = App.Icon;
        this.title = title;
        this.build = build;
        Title = $"Print: {title}";
        HeadingText.Text = title;
        OptionsHost.Content = options;
        Rebuild();
    }

    /// <summary>The pages shown again, as they are now picked; what went wrong instead, if they can't be made, so that
    /// the reminders go on.</summary>
    public void Rebuild()
    {
        try
        {
            Viewer.Document = build(Paper);
            PrintButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            Viewer.Document = new FlowDocument(new Paragraph(new Run($"The pages couldn't be made: {ex.Message}")));
            PrintButton.IsEnabled = false;
        }
    }

    /// <summary>The default printer's paper, or Letter when there's no printer.</summary>
    private static Size Paper => paper ??= DefaultPaper();
    private static Size? paper;

    private static Size DefaultPaper()
    {
        try
        {
            var dialog = new PrintDialog();
            if (dialog.PrintableAreaWidth > 200 && dialog.PrintableAreaHeight > 200) return new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
        }
        catch (Exception)
        {
            // No printer, or it can't be reached: Letter.
        }
        return new Size(8.5 * 96, 11 * 96);
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;
        try
        {
            var document = build(new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight));
            dialog.PrintDocument(new Footed(((IDocumentPaginatorSource)document).DocumentPaginator, title), $"Health Reminder: {title}");
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"It couldn't be printed: {ex.Message}", "Health Reminder");
        }
    }

    /// <summary>The pages of a document, each with its title and number at the foot.</summary>
    private sealed class Footed : DocumentPaginator
    {
        private readonly DocumentPaginator pages;
        private readonly string title;

        public Footed(DocumentPaginator pages, string title)
        {
            this.pages = pages;
            this.title = title;
            pages.ComputePageCount();
        }

        public override DocumentPage GetPage(int pageNumber)
        {
            var page = pages.GetPage(pageNumber);
            var visual = new ContainerVisual();
            visual.Children.Add(page.Visual);
            var foot = new DrawingVisual();
            using (var context = foot.RenderOpen())
            {
                var text = new FormattedText($"{title} · {DateTime.Now.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)} · page {pageNumber + 1} of {pages.PageCount}",
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Printing.Gray, 1);
                context.DrawText(text, new Point(Printing.Margin, page.Size.Height - Printing.Margin / 2 - text.Height));
            }
            visual.Children.Add(foot);
            return new DocumentPage(visual, page.Size, page.BleedBox, page.ContentBox);
        }

        public override bool IsPageCountValid => pages.IsPageCountValid;
        public override int PageCount => pages.PageCount;

        public override Size PageSize
        {
            get => pages.PageSize;
            set => pages.PageSize = value;
        }

        public override IDocumentPaginatorSource Source => pages.Source;
    }
}
