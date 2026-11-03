namespace CarRental.Application.Abstractions.Terms;

/// <summary>PORT: where the current rental terms come from (config, CMS, database...).</summary>
public interface ITermsProvider
{
    TermsDocument Current { get; }
}

public sealed record TermsSection(string Heading, string Body);

public sealed record TermsDocument(string Version, string Title, DateOnly EffectiveFrom, IReadOnlyList<TermsSection> Sections);
