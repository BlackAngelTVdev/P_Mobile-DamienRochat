using ReadMe.Models;

namespace ReadMe;

[QueryProperty(nameof(Book), "Book")]
public partial class BookReaderPage : ContentPage
{
    private Book _book;
    public Book Book
    {
        get => _book;
        set
        {
            _book = value;
            OnPropertyChanged();
        }
    }

    public BookReaderPage()
    {
        InitializeComponent();
        BindingContext = this;
    }
}
