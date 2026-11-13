// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups.Test;

using System.Numerics;
using Hexa.NET.ImGui;
using ktsu.CredentialCache;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a popup will not confirm a credential that cannot work: with a required field empty,
/// OK is disabled, Enter does nothing, and <c>onConfirm</c> is never called.
/// </summary>
/// <remarks>
/// These drive a headless ImGui context, as <see cref="CredentialEnterTests"/> does, and type
/// through ImGui's own input queue so the text reaches the fields the way a user's would.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class CredentialCanConfirmTests
{
	private ImGuiContextPtr context;

	/// <summary>
	/// Exposes <see cref="TokenPopup"/>'s <c>CanConfirm</c>.
	/// </summary>
	private sealed class ProbeTokenPopup : TokenPopup
	{
		internal bool ObservedCanConfirm => CanConfirm;
	}

	/// <summary>
	/// Exposes <see cref="UsernamePasswordPopup"/>'s <c>CanConfirm</c>.
	/// </summary>
	private sealed class ProbeUsernamePasswordPopup : UsernamePasswordPopup
	{
		internal bool ObservedCanConfirm => CanConfirm;
	}

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

	private static void Type(CredentialPopup popup, string text)
	{
		ImGuiIOPtr io = ImGui.GetIO();
		foreach (char c in text)
		{
			io.AddInputCharacter(c);
		}

		Assert.IsTrue(Frames(popup, 2), "Typing must not close the popup.");
	}

	private static bool Press(CredentialPopup popup, ImGuiKey key)
	{
		ImGuiIOPtr io = ImGui.GetIO();
		io.AddKeyEvent(key, true);
		bool open = Frames(popup, 1);
		io.AddKeyEvent(key, false);
		return Frames(popup, 2) && open;
	}

	private static bool ClickOk(CredentialPopup popup)
	{
		ImGuiIOPtr io = ImGui.GetIO();
		Vector2 okCenter = popup.OkButtonCenter;
		io.AddMousePosEvent(okCenter.X, okCenter.Y);
		Assert.IsTrue(Frames(popup, 1), "Moving the mouse must not close the popup.");
		io.AddMouseButtonEvent(0, true);
		bool open = Frames(popup, 1);
		io.AddMouseButtonEvent(0, false);
		return Frames(popup, 2) && open;
	}

	/// <summary>
	/// Clicking OK straight away on a token popup does nothing: the popup stays open and the
	/// callback is not called.
	/// </summary>
	[TestMethod]
	public void OkOnAnEmptyTokenDoesNotConfirm()
	{
		// Arrange
		TokenPopup popup = new();
		Credential? confirmed = null;
		popup.Open("GitHub", "Personal access token", c => confirmed = c);
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");

		// Act
		bool open = ClickOk(popup);

		// Assert
		Assert.IsTrue(open, "OK on an empty token must not close the popup.");
		Assert.IsNull(confirmed, "OK on an empty token must not call onConfirm.");
	}

	/// <summary>
	/// Enter in a token field holding only spaces does nothing, since the token would be trimmed to
	/// an empty one.
	/// </summary>
	[TestMethod]
	public void EnterOnABlankTokenDoesNotConfirm()
	{
		// Arrange
		TokenPopup popup = new();
		Credential? confirmed = null;
		popup.Open("GitHub", "Personal access token", c => confirmed = c);
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");
		Type(popup, "   ");
		Assert.AreEqual("   ", popup.token, "Precondition: the spaces reached the field.");

		// Act
		bool open = Press(popup, ImGuiKey.Enter);

		// Assert
		Assert.IsTrue(open, "Enter on a blank token must not close the popup.");
		Assert.IsNull(confirmed, "Enter on a blank token must not call onConfirm.");
	}

	/// <summary>
	/// Enter in the password field with the username left empty does nothing.
	/// </summary>
	[TestMethod]
	public void EnterWithAnEmptyUsernameDoesNotConfirm()
	{
		// Arrange -- skip the username and type only a password
		UsernamePasswordPopup popup = new();
		Credential? confirmed = null;
		popup.Open("Azure DevOps", "Sign in", c => confirmed = c);
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");
		Assert.IsTrue(Press(popup, ImGuiKey.Enter), "Precondition: Enter in Username moves on.");
		Type(popup, "hunter2");
		Assert.AreEqual("hunter2", popup.password, "Precondition: the password was typed into Password.");

		// Act
		bool open = Press(popup, ImGuiKey.Enter);

		// Assert
		Assert.IsTrue(open, "Enter with no username must not close the popup.");
		Assert.IsNull(confirmed, "Enter with no username must not call onConfirm.");
	}

	/// <summary>
	/// OK with a username but no password does nothing.
	/// </summary>
	[TestMethod]
	public void OkWithAnEmptyPasswordDoesNotConfirm()
	{
		// Arrange
		UsernamePasswordPopup popup = new();
		Credential? confirmed = null;
		popup.Open("Azure DevOps", "Sign in", c => confirmed = c);
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");
		Type(popup, "user");

		// Act
		bool open = ClickOk(popup);

		// Assert
		Assert.IsTrue(open, "OK with no password must not close the popup.");
		Assert.IsNull(confirmed, "OK with no password must not call onConfirm.");
	}

	/// <summary>
	/// <see cref="TokenPopup"/> can confirm only once its token has something besides whitespace.
	/// </summary>
	/// <param name="token">The token in the field.</param>
	/// <param name="expected">Whether it can be confirmed.</param>
	[TestMethod]
	[DataRow("", false)]
	[DataRow("  ", false)]
	[DataRow("ghp_secret", true)]
	[DataRow(" ghp_secret ", true)]
	public void TokenPopupCanConfirmOnlyWithAToken(string token, bool expected)
	{
		// Arrange
		ProbeTokenPopup popup = new()
		{
			token = token,
		};

		// Act
		bool canConfirm = popup.ObservedCanConfirm;

		// Assert
		Assert.AreEqual(expected, canConfirm);
	}

	/// <summary>
	/// <see cref="UsernamePasswordPopup"/> can confirm only with a username that isn't blank and a
	/// password that isn't empty. A password of spaces counts, since passwords are not trimmed.
	/// </summary>
	/// <param name="username">The username in the field.</param>
	/// <param name="password">The password in the field.</param>
	/// <param name="expected">Whether it can be confirmed.</param>
	[TestMethod]
	[DataRow("", "", false)]
	[DataRow("user", "", false)]
	[DataRow("", "hunter2", false)]
	[DataRow("  ", "hunter2", false)]
	[DataRow("user", "hunter2", true)]
	[DataRow("user", "  ", true)]
	public void UsernamePasswordPopupCanConfirmOnlyWithBothFields(string username, string password, bool expected)
	{
		// Arrange
		ProbeUsernamePasswordPopup popup = new()
		{
			username = username,
			password = password,
		};

		// Act
		bool canConfirm = popup.ObservedCanConfirm;

		// Assert
		Assert.AreEqual(expected, canConfirm);
	}
}
