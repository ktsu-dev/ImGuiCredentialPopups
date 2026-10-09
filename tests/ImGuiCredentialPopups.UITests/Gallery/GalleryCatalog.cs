// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups.UITests.Gallery;

using Hexa.NET.ImGui;

/// <summary>Every picture in the popup gallery, in the order the index shows them.</summary>
/// <remarks>
/// Every value typed here is made up. A new popup earns a picture by adding an entry here; nothing
/// else needs to change, because the runner, the index and the workflow all read this list.
/// </remarks>
internal static class GalleryCatalog
{
	/// <summary>Gets the entries.</summary>
	internal static IReadOnlyList<GalleryEntry> Entries { get; } =
	[
		new(
			"Username and password",
			"`UsernamePasswordPopup` as it opens: the caller's title and label, a username field that already has the keyboard, a masked password field and an OK button.",
			() => new PhotographedUsernamePasswordPopup(),
			"Sign in to GitHub",
			"Enter the account to sign in with."),
		new(
			"Username and password, filled in",
			"The same popup with a username typed, Enter pressed to move on to the password, and a password typed. The password is masked as it is typed; Enter in it, or OK, hands back a `CredentialWithUsernamePassword`.",
			() => new PhotographedUsernamePasswordPopup(),
			"Sign in to GitHub",
			"Enter the account to sign in with.")
		{
			Fill = harness =>
			{
				harness.Keyboard.Type("octocat");
				harness.Keyboard.Press(ImGuiKey.Enter);
				harness.Keyboard.Type("correct-horse-battery");
			},
		},
		new(
			"Token",
			"`TokenPopup` as it opens, for a personal access token, an API key or any other single secret.",
			() => new PhotographedTokenPopup(),
			"Personal Access Token",
			"Paste a token with the repo scope."),
		new(
			"Token, filled in",
			"A pasted token is masked like a password. Enter, or OK, hands back a `CredentialWithToken`.",
			() => new PhotographedTokenPopup(),
			"Personal Access Token",
			"Paste a token with the repo scope.")
		{
			Fill = harness => harness.Keyboard.Type("ghp_0123456789abcdefghijklmnopqrstuvwxyz"),
		},
		new(
			"A custom popup",
			"The `ApiKeyPopup` from the README, derived from `CredentialPopup` in a dozen lines: the base class supplies the modal, the title, the label, the keyboard focus and the OK button, and the derived class only draws its field and builds its credential.",
			() => new PhotographedApiKeyPopup(),
			"Weather Service",
			"Enter the API key from your account page.")
		{
			Fill = harness => harness.Keyboard.Type("wx-4f1d2c9a7b"),
		},
	];
}
