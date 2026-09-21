namespace Claudel.Models;

public class BookCoverCandidate
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public int? FirstPublishYear { get; set; }

    public string? Isbn13 { get; set; }
    public string? Isbn10 { get; set; }

    public string? CoverUrl { get; set; }

    public string? OpenLibraryKey { get; set; }

    public string DisplayYear =>
        FirstPublishYear?.ToString() ?? "";

    public string DisplayAuthor =>
        string.IsNullOrWhiteSpace(Author)
            ? ""
            : Author;
}