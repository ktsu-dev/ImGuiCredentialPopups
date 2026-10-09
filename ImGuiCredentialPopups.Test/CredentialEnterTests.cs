// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups.Test;

using System.Numerics;
using Hexa.NET.ImGui;
using ktsu.CredentialCache;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that Enter in the last field confirms a popup, and that the OK button still submits what
/// was typed without Enter.
/// </summary>
/// <remarks>
/// These drive a headless ImGui context, as <see cref="CredentialDismissTests"/> does, and type
/// through ImGui's own input queue so the text reaches the fields the way a user's would.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class CredentialEnterTests
{
	private ImGuiContextPtr context;

	/// <summary>
	/// Creates a fresh headless ImGui context with a display and keyboard navigation, so the OK
	/// button can be reached and pressed without a mouse.
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

	/// <summary>
	/// Enter, or keypad Enter, in the token field confirms with the typed token and closes the popup.
	/// </summary>
	/// <param name="key">The Enter key pressed.</param>
	[TestMethod]
	[DataRow(ImGuiKey.Enter)]
	[DataRow(ImGuiKey.KeypadEnter)]
	public void EnterInTheTokenFieldConfirms(ImGuiKey key)
	{
		// Arrange
		TokenPopup popup = new();
		Credential? confirmed = null;
		popup.Open("GitHub", "Personal access token", c => confirmed = c);
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");
		Type(popup, "ghp_secret");

		// Act
		bool open = Press(popup, key);

		// Assert
		Assert.IsFalse(open, "Enter should close the popup.");
		Assert.IsNotNull(confirmed, "Enter should confirm.");
		Assert.AreEqual("ghp_secret", ((CredentialWithToken)confirmed).Token.WeakString);
		Assert.AreEqual(string.Empty, popup.token, "Confirming must reset the field, as OK does.");
	}

	/// <summary>
	/// Enter in the username field moves on to the password field; Enter there confirms both.
	/// </summary>
	[TestMethod]
	public void EnterInTheUsernameFieldMovesOnAndEnterInThePasswordFieldConfirms()
	{
		// Arrange
		UsernamePasswordPopup popup = new();
		Credential? confirmed = null;
		popup.Open("Azure DevOps", "Sign in", c => confirmed = c);
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");
		Type(popup, "user");

		// Act
		Assert.IsTrue(Press(popup, ImGuiKey.Enter), "Enter in the username field must not close the popup.");
		Assert.IsNull(confirmed, "Enter in the username field must not confirm.");
		Type(popup, "hunter2");
		bool open = Press(popup, ImGuiKey.Enter);

		// Assert
		Assert.IsFalse(open, "Enter in the password field should close the popup.");
		Assert.IsNotNull(confirmed, "Enter in the password field should confirm.");
		CredentialWithUsernamePassword pair = (CredentialWithUsernamePassword)confirmed;
		Assert.AreEqual("user", pair.Username.WeakString);
		Assert.AreEqual("hunter2", pair.Password.WeakString);
	}

	/// <summary>
	/// Pressing OK without ever pressing Enter still submits what was typed. This guards against
	/// moving to <see cref="ImGuiInputTextFlags.EnterReturnsTrue"/>, which would submit an empty value.
	/// </summary>
	[TestMethod]
	public void PressingOkWithoutEnterSubmitsWhatWasTyped()
	{
		// Arrange
		TokenPopup popup = new();
		Credential? confirmed = null;
		popup.Open("GitHub", "Personal access token", c => confirmed = c);
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");
		Type(popup, "ghp_secret");

		// Act -- Tab to the OK button and activate it with Space, so Enter is never pressed
		Assert.IsTrue(Press(popup, ImGuiKey.Tab), "Tab must not close the popup.");
		Assert.IsNull(confirmed, "Precondition: nothing has confirmed yet.");
		bool open = Press(popup, ImGuiKey.Space);

		// Assert
		Assert.IsFalse(open, "OK should close the popup.");
		Assert.IsNotNull(confirmed, "OK should confirm.");
		Assert.AreEqual("ghp_secret", ((CredentialWithToken)confirmed).Token.WeakString);
	}
}
