// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups.UITests.Gallery;

using ktsu.ImGui.App;
using ktsu.ImGui.App.Testing;

/// <summary>
/// Photographs the credential popups for <c>docs/gallery</c>: one picture per
/// <see cref="GalleryCatalog"/> entry, cropped to the popup, and an index that captions them.
/// </summary>
/// <remarks>
/// <para>
/// These run as ordinary tests, so every pull request proves each picture can still be staged.
/// Pictures are written to a temporary directory unless <c>IMGUICREDENTIALPOPUPS_GALLERY_OUT</c>
/// names one, which is how the gallery workflow, and anyone regenerating by hand, sends them to
/// <c>docs/gallery</c>.
/// </para>
/// <para>
/// Each entry starts its own headless application with nothing in it but the popup, so a picture
/// never depends on the one taken before it. Every value typed is made up.
/// </para>
/// </remarks>
[TestClass]
public sealed class PopupGallery
{
	/// <summary>The environment variable naming the directory the gallery is written to.</summary>
	internal const string OutputVariable = "IMGUICREDENTIALPOPUPS_GALLERY_OUT";

	/// <summary>How far past the popup's own edges a picture extends, so it shows the dimmed backdrop.</summary>
	internal const int Margin = 24;

	/// <summary>The display the popups are drawn on. Each is centred on it and then cropped to.</summary>
	internal static readonly (int Width, int Height) Display = (800, 480);

	private static readonly Lazy<string> TemporaryOutput = new(() =>
		Path.Combine(Path.GetTempPath(), $"imguicredentialpopups-gallery-{Guid.NewGuid():N}"));

	private ImGuiAppHarness? harness;

	/// <summary>Gets every entry's name, one test case each.</summary>
	public static IEnumerable<object[]> EntryNames => GalleryCatalog.Entries.Select(entry => new object[] { entry.Name });

	/// <summary>Gets or sets the test context, which MSTest supplies.</summary>
	public TestContext TestContext { get; set; } = null!;

	/// <summary>Gets the directory pictures are written to.</summary>
	internal static string OutputDirectory =>
		Environment.GetEnvironmentVariable(OutputVariable) is string output && output.Length > 0
			? Path.GetFullPath(output)
			: TemporaryOutput.Value;

	/// <summary>Removes the temporary directory, when pictures went there rather than to a named one.</summary>
	[ClassCleanup]
	public static void DeleteTemporaryOutput()
	{
		if (TemporaryOutput.IsValueCreated && Directory.Exists(TemporaryOutput.Value))
		{
			Directory.Delete(TemporaryOutput.Value, recursive: true);
		}
	}

	/// <summary>Stops the harness an entry started, since only one may be live at a time.</summary>
	[TestCleanup]
	public void StopHarness()
	{
		harness?.Dispose();
		harness = null;
	}

	[TestMethod]
	[DynamicData(nameof(EntryNames))]
	public void Photograph(string name)
	{
		GalleryEntry entry = GalleryCatalog.Entries.Single(candidate => candidate.Name == name);
		IPhotographedPopup photographed = entry.Create();
		bool confirmed = false;

		harness = ImGuiAppHarness.Start(
			new ImGuiAppConfig
			{
				Title = "Popup gallery",
				SaveIniSettings = false,
				OnRender = _ => photographed.Popup.ShowIfOpen(),
			},
			new HarnessOptions { Width = Display.Width, Height = Display.Height });

		Assert.IsTrue(GalleryFonts.Load(), "ImGuiApp's own font could not be found, so the pictures would not look like an application.");
		harness.Mouse.MoveTo(-100f, -100f);
		harness.Step(2);

		photographed.Popup.Open(entry.Title, entry.Label, _ => confirmed = true);
		harness.Step(3);

		if (entry.Fill is not null)
		{
			entry.Fill(harness);
			harness.Step(3);
		}

		Assert.IsFalse(confirmed, "Staging a picture submitted the popup, so it photographed whatever came after it.");
		Rectangle? window = photographed.Window;
		Assert.IsNotNull(window, $"'{entry.Name}' never drew its popup.");

		Rectangle region = new(
			window.Value.MinX - Margin,
			window.Value.MinY - Margin,
			window.Value.MaxX + Margin,
			window.Value.MaxY + Margin);
		Bitmap32 picture = Crop(harness.Target, region);

		Directory.CreateDirectory(OutputDirectory);
		string path = Path.Combine(OutputDirectory, entry.Slug + ".png");
		picture.SavePng(path);
		TestContext.WriteLine($"Wrote {path} ({picture.Width}x{picture.Height}).");
	}

	[TestMethod]
	public void WriteTheIndex()
	{
		string[] slugs = [.. GalleryCatalog.Entries.Select(entry => entry.Slug)];
		Assert.HasCount(slugs.Length, slugs.Distinct(StringComparer.Ordinal), "Two gallery entries would write the same file.");

		Directory.CreateDirectory(OutputDirectory);
		File.WriteAllText(Path.Combine(OutputDirectory, "README.md"), GalleryIndex.Render(GalleryCatalog.Entries));
	}

	/// <summary>Copies a rectangle out of a frame, clamped to its edges.</summary>
	internal static Bitmap32 Crop(Bitmap32 source, Rectangle region)
	{
		int minX = Math.Clamp(region.MinX, 0, source.Width);
		int minY = Math.Clamp(region.MinY, 0, source.Height);
		int maxX = Math.Clamp(region.MaxX, minX, source.Width);
		int maxY = Math.Clamp(region.MaxY, minY, source.Height);
		Assert.IsTrue(maxX > minX && maxY > minY, $"The crop {region} leaves nothing of a {source.Width}x{source.Height} frame.");

		Bitmap32 cropped = new(maxX - minX, maxY - minY);
		int rowBytes = cropped.Width * 4;
		for (int y = minY; y < maxY; y++)
		{
			source.Pixels.Slice(((y * source.Width) + minX) * 4, rowBytes)
				.CopyTo(cropped.Pixels.Slice((y - minY) * rowBytes, rowBytes));
		}

		return cropped;
	}
}
