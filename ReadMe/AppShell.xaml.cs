namespace ReadMe
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(BookDetailPage), typeof(BookDetailPage));
            Routing.RegisterRoute(nameof(BookReaderPage), typeof(BookReaderPage));
            Routing.RegisterRoute(nameof(PdfViewerPage), typeof(PdfViewerPage));
        }
    }
}
