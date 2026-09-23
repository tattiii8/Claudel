using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace Claudel.Models;

public class Document
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public List<Author> Authors { get; set; } = new();

    public string Category { get; set; } = "";

    public string DocumentType { get; set; } = "Book";

    public string DOI { get; set; } = "";

    public string JournalName { get; set; } = "";

    public string Volume { get; set; } = "";

    public string Issue { get; set; } = "";

    public string Pages { get; set; } = "";

    public List<Tag> Tags { get; set; } = new();

    public DateTime? PublicationDate { get; set; }

    // S3 is used only for cover images.
    public string CoverS3Key { get; set; } = "";

    public Bitmap? CoverImage { get; set; }

    // Redmine
    public int? RedmineIssueId { get; set; }

    public string RedmineIssueUrl { get; set; } = "";

    // Kavita
    public int? KavitaLibraryId { get; set; }

    public int? KavitaSeriesId { get; set; }

    public int? KavitaVolumeId { get; set; }

    public string KavitaUrl { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

