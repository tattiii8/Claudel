using System;
using System.Collections.Generic;

namespace Claudel.Models;

public class CrossrefWork
{
    public string DOI { get; set; } = "";

    public string Title { get; set; } = "";

    public List<string> Authors { get; set; } = new();

    public string AuthorDisplay =>
        string.Join(", ", Authors);

    public string JournalName { get; set; } = "";

    public DateTime? PublicationDate { get; set; }

    public string Volume { get; set; } = "";

    public string Issue { get; set; } = "";

    public string Pages { get; set; } = "";

    public string Publisher { get; set; } = "";

    public string DisplayPublicationDate =>
        PublicationDate?.ToString("yyyy-MM-dd") ?? "";

    public string DisplayBibliography
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(JournalName))
            {
                parts.Add(JournalName);
            }

            var volumeIssue = Volume;

            if (!string.IsNullOrWhiteSpace(Issue))
            {
                volumeIssue =
                    string.IsNullOrWhiteSpace(volumeIssue)
                        ? $"({Issue})"
                        : $"{volumeIssue}({Issue})";
            }

            if (!string.IsNullOrWhiteSpace(volumeIssue))
            {
                parts.Add(volumeIssue);
            }

            if (!string.IsNullOrWhiteSpace(Pages))
            {
                parts.Add(Pages);
            }

            return string.Join(", ", parts);
        }
    }
}