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

    public List<Tag> Tags { get; set; } = new();

    public DateTime? PublicationDate { get; set; }

    public string S3Bucket { get; set; } = "";

    public string S3Key { get; set; } = "";

    public string CoverS3Key { get; set; } = "";

    public Bitmap? CoverImage { get; set; }

    public int? RedmineIssueId { get; set; }

    public string RedmineIssueUrl { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}