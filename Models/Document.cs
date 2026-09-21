using System;

namespace Claudel.Models;

public class Document
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public string Author { get; set; } = "";

    public string Category { get; set; } = "";

    public string Tags { get; set; } = "";

    public int? Year { get; set; }

    public string S3Key { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}