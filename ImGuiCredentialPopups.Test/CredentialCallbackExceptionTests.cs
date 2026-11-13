// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups.Test;

using System.Numerics;
using Hexa.NET.ImGui;
using ktsu.CredentialCache;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that an <c>onConfirm</c> callback that throws leaves ImGui in a state the host can carry on
/// from: the exception reaches the caller of <see cref="CredentialPopup.ShowIfOpen"/>, the popup is
/// closed, and the frame can still be rendered.
/// </summary>
/// <remarks>
/// A callback that throws while the modal's <c>BeginPopupModal</c> scope is open skips its
/// <c>EndPopup</c>. With ImGui's default settings the next <c>Render</c> then aborts the process on a
/// "Missing End()" assertion, which no test could report. These tests turn that assertion off so
/// ImGui recovers instead, and assert on what a broken scope leaves behind: the popup still open.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class CredentialCallbackExceptionTests
{
	private ImGuiContextPtr context;

	/// <summary>
	/// Creates a fresh headless ImGui context with a display and keyboard navigation, set to recover
	/// from an unbalanced window stack rather than abort.
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
		io.ConfigErrorRecoveryEnableAssert = false;
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

	// Runs frames the way a host that guards its popup would: a throwing ShowIfOpen is caught and the
	// frame is still rendered.
	private static bool Frames(CredentialPopup popup, int count, List<Exception> caught)
	{
		bool open = true;
		for (int i = 0; i < count; i++)
		{
			ImGui.NewFrame();
			try
			{
				open = popup.ShowIfOpen();
			}
			catch (InvalidOperationException ex)
			{
				caught.Add(ex);
			}

			ImGui.Render();
		}

		return open;
	}

	/// <summary>
	/// Enter in the token field with a callback that throws: the host sees the exception, and the
	/// popup is closed rather than left open with its field already cleared.
	/// </summary>
	[TestMethod]
	public void AThrowingCallbackReachesTheHostAndClosesThePopup()
	{
		// Arrange
		TokenPopup popup = new();
		List<Exception> caught = [];
		int calls = 0;
		popup.Open("GitHub", "Personal access token", _ =>
		{
			calls++;
			throw new InvalidOperationException("Could not store the credential.");
		});
		Assert.IsTrue(Frames(popup, 2, caught), "Precondition: the popup is showing.");
		ImGuiIOPtr io = ImGui.GetIO();
		foreach (char c in "ghp_secret")
		{
			io.AddInputCharacter(c);
		}

		Assert.IsTrue(Frames(popup, 2, caught), "Precondition: typing must not close the popup.");

		// Act
		io.AddKeyEvent(ImGuiKey.Enter, true);
		Frames(popup, 1, caught);
		io.AddKeyEvent(ImGuiKey.Enter, false);
		bool open = Frames(popup, 3, caught);

		// Assert
		Assert.AreEqual(1, calls, "The callback should be called once.");
		Assert.HasCount(1, caught, "The callback's exception should reach the caller of ShowIfOpen.");
		Assert.IsFalse(open, "The popup should be closed after a confirmation, even one whose callback threw.");
		Assert.IsFalse(ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId), "No popup should be left open in ImGui.");
		Assert.AreEqual(string.Empty, popup.token, "The field must still be cleared.");
	}

	/// <summary>
	/// After a callback has thrown, the same popup can be opened again and confirmed normally.
	/// </summary>
	[TestMethod]
	public void ThePopupCanBeReusedAfterACallbackThrew()
	{
		// Arrange -- the first confirmation throws
		TokenPopup popup = new();
		List<Exception> caught = [];
		popup.Open("GitHub", "Personal access token", _ => throw new InvalidOperationException("Could not store the credential."));
		Assert.IsTrue(Frames(popup, 2, caught), "Precondition: the popup is showing.");
		ImGuiIOPtr io = ImGui.GetIO();
		io.AddInputCharacter('a');
		Frames(popup, 2, caught);
		io.AddKeyEvent(ImGuiKey.Enter, true);
		Frames(popup, 1, caught);
		io.AddKeyEvent(ImGuiKey.Enter, false);
		Frames(popup, 2, caught);
		Assert.HasCount(1, caught, "Precondition: the first callback threw.");

		// Act
		Credential? confirmed = null;
		popup.Open("GitHub", "Try again", c => confirmed = c);
		Assert.IsTrue(Frames(popup, 2, caught), "The popup should show again.");
		foreach (char c in "ghp_good")
		{
			io.AddInputCharacter(c);
		}

		Frames(popup, 2, caught);
		io.AddKeyEvent(ImGuiKey.Enter, true);
		Frames(popup, 1, caught);
		io.AddKeyEvent(ImGuiKey.Enter, false);
		bool open = Frames(popup, 2, caught);

		// Assert
		Assert.IsFalse(open, "Confirming the second request should close the popup.");
		Assert.IsNotNull(confirmed, "The second callback should be called.");
		Assert.AreEqual("ghp_good", ((CredentialWithToken)confirmed).Token.WeakString);
		Assert.HasCount(1, caught, "Only the first callback threw.");
	}
}
