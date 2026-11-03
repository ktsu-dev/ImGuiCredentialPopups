// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups.Test;

using System.Numerics;
using Hexa.NET.ImGui;
using ktsu.CredentialCache;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a token or username pasted together with surrounding whitespace, such as the line
/// ending copied from a terminal, comes back trimmed, while the password is left as pasted.
/// </summary>
/// <remarks>
/// These drive a headless ImGui context, as <see cref="CredentialEnterTests"/> does, and paste with
/// Ctrl+V so the text goes through the single-line field's own line-break handling, which turns a
/// pasted line break into a space.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class CredentialPasteTests
{
	private ImGuiContextPtr context;

	/// <summary>
	/// Creates a fresh headless ImGui context with a display and keyboard navigation.
	/// </summary>
	[TestInitialize]
	public void CreateContext()
	{
		context = ImGui.CreateContext();
		ImGui.SetCurrentContext(context);
		ImGuiIOPtr io = ImGui.GetIO();
		io.DisplaySize = new Vector2(800, 600);
		io.DeltaTime = 1f / 60f;
		io.BackendFlags |= ImGuiBackendFlags.RendererHasTextures;
		io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
		// ImGui defaults this on for Apple builds, where it moves the paste shortcut from Ctrl+V to
		// Cmd+V. Turning it off makes the Ctrl+V these tests send paste on every platform.
		io.ConfigMacOSXBehaviors = false;
		unsafe
		{
			// Keep the test from writing an imgui.ini into the working directory.
			io.IniFilename = null;
		}
	}

	/// <summary>
	/// Destroys the context created for the test.
	/// </summary>
	[TestCleanup]
	public void DestroyContext() => ImGui.DestroyContext(context);

	private static bool Frames(CredentialPopup popup, int count)
	{
		bool open = true;
		for (int i = 0; i < count; i++)
		{
			ImGui.NewFrame();
			open = popup.ShowIfOpen();
			ImGui.Render();
		}

		return open;
	}

	private static bool Press(CredentialPopup popup, ImGuiKey key)
	{
		ImGuiIOPtr io = ImGui.GetIO();
		io.AddKeyEvent(key, true);
		bool open = Frames(popup, 1);
		io.AddKeyEvent(key, false);
		return Frames(popup, 2) && open;
	}

	private static void Paste(CredentialPopup popup, string clipboard)
	{
		ImGui.SetClipboardText(clipboard);
		ImGuiIOPtr io = ImGui.GetIO();
		io.AddKeyEvent(ImGuiKey.ModCtrl, true);
		Assert.IsTrue(Frames(popup, 1), "Holding Ctrl must not close the popup.");
		io.AddKeyEvent(ImGuiKey.V, true);
		Assert.IsTrue(Frames(popup, 1), "Ctrl+V must not close the popup.");
		io.AddKeyEvent(ImGuiKey.V, false);
		io.AddKeyEvent(ImGuiKey.ModCtrl, false);
		Assert.IsTrue(Frames(popup, 2), "Releasing Ctrl+V must not close the popup.");
	}

	/// <summary>
	/// A token pasted with a line ending or surrounding spaces is returned without them.
	/// </summary>
	/// <param name="clipboard">The text on the clipboard.</param>
	[TestMethod]
	[DataRow("tok\n")]
	[DataRow("tok\r\n")]
	[DataRow(" tok ")]
	public void PastedTokenIsTrimmed(string clipboard)
	{
		// Arrange
		TokenPopup popup = new();
		Credential? confirmed = null;
		popup.Open("GitHub", "Personal access token", c => confirmed = c);
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");
		Paste(popup, clipboard);
		Assert.IsTrue(popup.token.Contains("tok", StringComparison.Ordinal), "Precondition: the paste reached the field.");
		Assert.AreNotEqual("tok", popup.token, "Precondition: the field holds the whitespace that was pasted.");

		// Act
		bool open = Press(popup, ImGuiKey.Enter);

		// Assert
		Assert.IsFalse(open, "Enter should close the popup.");
		Assert.IsNotNull(confirmed, "Enter should confirm.");
		Assert.AreEqual("tok", ((CredentialWithToken)confirmed).Token.WeakString);
	}

	/// <summary>
	/// A username pasted with a line ending or surrounding spaces is returned without them, and the
	/// password keeps the spaces it was typed with.
	/// </summary>
	/// <param name="clipboard">The text on the clipboard.</param>
	[TestMethod]
	[DataRow("user\n")]
	[DataRow("user\r\n")]
	[DataRow(" user ")]
	public void PastedUsernameIsTrimmedAndPasswordIsNot(string clipboard)
	{
		// Arrange
		UsernamePasswordPopup popup = new();
		Credential? confirmed = null;
		popup.Open("Azure DevOps", "Sign in", c => confirmed = c);
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");
		Paste(popup, clipboard);
		Assert.IsTrue(popup.username.Contains("user", StringComparison.Ordinal), "Precondition: the paste reached the field.");
		Assert.AreNotEqual("user", popup.username, "Precondition: the field holds the whitespace that was pasted.");
		Assert.IsTrue(Press(popup, ImGuiKey.Enter), "Enter in the username field must not close the popup.");
		Paste(popup, " pass ");

		// Act
		bool open = Press(popup, ImGuiKey.Enter);

		// Assert
		Assert.IsFalse(open, "Enter in the password field should close the popup.");
		Assert.IsNotNull(confirmed, "Enter in the password field should confirm.");
		CredentialWithUsernamePassword pair = (CredentialWithUsernamePassword)confirmed;
		Assert.AreEqual("user", pair.Username.WeakString);
		Assert.AreEqual(" pass ", pair.Password.WeakString, "A password may legitimately start or end with a space.");
	}
}
