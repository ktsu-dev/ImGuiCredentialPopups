// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups.Test;

using System.Numerics;
using Hexa.NET.ImGui;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that dismissing a popup without confirming discards what was typed into it.
/// </summary>
/// <remarks>
/// Escape and the title-bar close button are handled inside <c>ImGuiPopups.Modal.ShowIfOpen</c>,
/// so unlike <see cref="CredentialResetTests"/> these need real frames. They drive a headless
/// ImGui context directly: no window, no renderer, just <c>NewFrame</c> and <c>Render</c>, which
/// is enough for ImGui's popup and input logic to run.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class CredentialDismissTests
{
	private ImGuiContextPtr context;

	/// <summary>
	/// Creates a fresh ImGui context with a display, and declares a renderer that builds font
	/// textures on demand so <c>NewFrame</c> does not require a prebuilt atlas.
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

	private static bool Frame(CredentialPopup popup)
	{
		ImGui.NewFrame();
		bool open = popup.ShowIfOpen();
		ImGui.Render();
		return open;
	}

	private static bool PressEscape(CredentialPopup popup)
	{
		ImGuiIOPtr io = ImGui.GetIO();
		io.AddKeyEvent(ImGuiKey.Escape, true);
		bool open = Frame(popup);
		io.AddKeyEvent(ImGuiKey.Escape, false);
		return open && Frame(popup);
	}

	/// <summary>
	/// Dismissing a username and password popup with Escape clears both fields, without waiting
	/// for a next <see cref="CredentialPopup.Open"/> that may never come.
	/// </summary>
	[TestMethod]
	public void DismissingAUsernamePasswordPopupWithEscapeClearsBothFields()
	{
		// Arrange
		UsernamePasswordPopup popup = new();
		popup.Open("Azure DevOps", "Sign in", _ => Assert.Fail("Dismissing must not confirm."));
		Assert.IsTrue(Frame(popup), "Precondition: the popup is showing.");
		popup.username = "user";
		popup.password = "hunter2";

		// Act
		bool open = PressEscape(popup);

		// Assert
		Assert.IsFalse(open, "Precondition: Escape dismissed the popup.");
		Assert.AreEqual(string.Empty, popup.username);
		Assert.AreEqual(string.Empty, popup.password);
	}

	/// <summary>
	/// The same for the token popup.
	/// </summary>
	[TestMethod]
	public void DismissingATokenPopupWithEscapeClearsTheToken()
	{
		// Arrange
		TokenPopup popup = new();
		popup.Open("GitHub", "Personal access token", _ => Assert.Fail("Dismissing must not confirm."));
		Assert.IsTrue(Frame(popup), "Precondition: the popup is showing.");
		popup.token = "ghp_secret";

		// Act
		bool open = PressEscape(popup);

		// Assert
		Assert.IsFalse(open, "Precondition: Escape dismissed the popup.");
		Assert.AreEqual(string.Empty, popup.token);
	}

	/// <summary>
	/// Showing a popup frame after frame must not clear what is being typed into it; only the
	/// transition to closed does.
	/// </summary>
	[TestMethod]
	public void KeepingAPopupOpenPreservesTheFields()
	{
		// Arrange
		TokenPopup popup = new();
		popup.Open("GitHub", "Personal access token", _ => { });
		Assert.IsTrue(Frame(popup), "Precondition: the popup is showing.");
		popup.token = "ghp_partial";

		// Act
		for (int i = 0; i < 3; i++)
		{
			Assert.IsTrue(Frame(popup));
		}

		// Assert
		Assert.AreEqual("ghp_partial", popup.token);
	}
}
