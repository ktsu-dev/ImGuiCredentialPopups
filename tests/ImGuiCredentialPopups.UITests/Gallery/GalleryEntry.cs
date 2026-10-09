// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups.UITests.Gallery;

using System.Text;

using ktsu.ImGui.App.Testing;

/// <summary>One picture in the popup gallery: which popup to open, how to fill it, and what to say about it.</summary>
/// <param name="Name">The caption, which also names the picture's file.</param>
/// <param name="Description">One or two sentences under the picture in the gallery's index.</param>
/// <param name="Create">Makes the popup to photograph, as a recording subclass of the one shipped.</param>
/// <param name="Title">The title the popup is opened with.</param>
/// <param name="Label">The label the popup is opened with.</param>
internal sealed record GalleryEntry(string Name, string Description, Func<IPhotographedPopup> Create, string Title, string Label)
{
	/// <summary>
	/// Gets what is typed into the popup once it has opened and settled, or null to photograph it as
	/// it opens. It types through ImGui's own input queue, the way a user would, and must not submit.
	/// </summary>
	public Action<ImGuiAppHarness>? Fill { get; init; }

	/// <summary>Gets the file name the picture is written under, without its extension.</summary>
	public string Slug => MakeSlug(Name);

	/// <inheritdoc/>
	public override string ToString() => Name;

	/// <summary>Turns a caption into a lower-case, hyphenated file name.</summary>
	internal static string MakeSlug(string text)
	{
		StringBuilder slug = new(text.Length);
		foreach (char character in text)
		{
			if (char.IsAsciiLetterOrDigit(character))
			{
				slug.Append(char.ToLowerInvariant(character));
			}
			else if (slug.Length > 0 && slug[^1] != '-')
			{
				slug.Append('-');
			}
		}

		return slug.ToString().TrimEnd('-');
	}
}
