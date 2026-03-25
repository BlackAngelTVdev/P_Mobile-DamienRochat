namespace ReadMe.Models;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Published { get; set; }
    public int NbPages { get; set; }
    public string Editor { get; set; } = string.Empty;
    public string Resume { get; set; } = string.Empty;
    public string Extrait { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public List<BookComment> Comments { get; set; } = [];
    public List<BookRate> Rates { get; set; } = [];
    public int UserId { get; set; }

    public string MetaLine => $"{Author} • {Category} • {Published}";
    public string ExtraLine => $"{NbPages} pages • {Editor}";
    public int CommentsCount => Comments.Count;
    public double AverageRate => Rates.Count == 0 ? 0 : Rates.Average(r => r.Value);
    public string ScoreLine => Rates.Count == 0
        ? "Aucune note"
        : $"Note: {AverageRate:0.0}/5 ({Rates.Count} avis)";
}

public class BookComment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class BookRate
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int Value { get; set; }
}