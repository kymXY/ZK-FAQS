using System.Text.RegularExpressions;

namespace FaqCms.Services;

/// <summary>
/// A plain "Share" link from YouTube or SharePoint is not, by itself, usable inside an
/// &lt;iframe&gt; — YouTube's watch page and SharePoint's normal viewer both refuse to be
/// framed. Each has a separate "embed" URL format that IS allowed to be framed. This class
/// converts what someone naturally copy-pastes into whatever actually plays.
/// </summary>
public static class VideoEmbedHelper
{
    private static readonly Regex YouTubeId = new(
        @"(?:youtube\.com/(?:watch\?v=|shorts/|embed/)|youtu\.be/)([A-Za-z0-9_-]{6,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>True if this is one of our own uploaded files (served from /uploads/),
    /// which should be rendered with a native &lt;video&gt; tag rather than an iframe.</summary>
    public static bool IsUploadedFile(string? url) =>
        !string.IsNullOrWhiteSpace(url) && url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase);

    /// <summary>Best-effort conversion of an external video link into an embeddable URL.
    /// Returns the original URL unchanged if it isn't a recognized pattern (e.g. Vimeo/Loom
    /// links are already embed-friendly as pasted).</summary>
    public static string ToEmbedUrl(string url)
    {
        url = url.Trim();

        var youTube = YouTubeId.Match(url);
        if (youTube.Success)
        {
            return $"https://www.youtube.com/embed/{youTube.Groups[1].Value}";
        }

        // SharePoint/OneDrive "Share" links (the ones with ":v:/s/" or ":v:/g/" in the
        // path) open the normal viewer, which refuses to be framed — there's no reliable
        // way to rewrite one into an embeddable URL by editing the string, because
        // SharePoint's real embed URL needs the file's internal UniqueId, which isn't
        // present in a Share link. If someone pastes a Share link here it's passed
        // through unchanged (and typically won't play) — see the hint on the video field:
        // they need SharePoint's own "Embed" option (Share menu → Embed → copy the URL
        // out of the <iframe> snippet), not "Copy link".
        return url;
    }

    /// <summary>True if this looks like a SharePoint/OneDrive "Share" link rather than
    /// their "Embed" link — used to show a warning in the editor, since a Share link
    /// pasted here typically won't actually play.</summary>
    public static bool IsLikelySharePointShareLink(string url) =>
        url.Contains("sharepoint.com", StringComparison.OrdinalIgnoreCase) && url.Contains(":v:/", StringComparison.OrdinalIgnoreCase);
}
