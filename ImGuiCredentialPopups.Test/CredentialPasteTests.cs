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

	/// <summary>
	/// A token one byte over <see cref="CredentialPopup.MaxCredentialLength"/> doesn't fit the field,
	/// so ImGui inserts none of it. The popup must say so and refuse to confirm, rather than let
	/// Enter or OK submit the empty field.
	/// </summary>
	/// <param name="pressOk">Whether to confirm with the OK button rather than Enter.</param>
	[TestMethod]
	[DataRow(false)]
	[DataRow(true)]
	public void AnOverLimitTokenPasteIsReportedAndNotConfirmed(bool pressOk)
	{
		// Arrange
		TokenPopup popup = new();
		Credential? confirmed = null;
		popup.Open("GitHub", "Personal access token", c => confirmed = c);
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");
		Paste(popup, new string('a', CredentialPopup.MaxCredentialLength + 1));
		Assert.AreEqual(string.Empty, popup.token, "Precondition: ImGui rejected the whole paste.");

		// Act
		bool open = pressOk ? ClickOk(popup) : Press(popup, ImGuiKey.Enter);

		// Assert
		Assert.IsTrue(popup.PasteTooLong, "The rejected paste should be reported.");
		Assert.IsTrue(open, "The popup should stay open.");
		Assert.IsNull(confirmed, "An empty token must not be confirmed.");
	}

	/// <summary>
	/// A paste that doesn't fit in the space left after text already in the field is rejected too.
	/// The field keeps only what was there, so confirming would submit a partial password.
	/// </summary>
	[TestMethod]
	public void AnOverLimitPasswordPasteAfterExistingTextIsReportedAndNotConfirmed()
	{
		// Arrange
		UsernamePasswordPopup popup = new();
		Credential? confirmed = null;
		popup.Open("Azure DevOps", "Sign in", c => confirmed = c);
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");
		Paste(popup, "user");
		Assert.IsTrue(Press(popup, ImGuiKey.Enter), "Enter in the username field must not close the popup.");
		Paste(popup, "abc");
		Paste(popup, new string('p', CredentialPopup.MaxCredentialLength - 2));
		Assert.AreEqual("abc", popup.password, "Precondition: ImGui rejected the whole paste.");

		// Act
		bool open = Press(popup, ImGuiKey.Enter);

		// Assert
		Assert.IsTrue(popup.PasteTooLong, "The rejected paste should be reported.");
		Assert.IsTrue(open, "The popup should stay open.");
		Assert.IsNull(confirmed, "A partial password must not be confirmed.");
	}

	/// <summary>
	/// Once the user edits the field after a rejected paste, the message goes away and the popup
	/// confirms what the field holds.
	/// </summary>
	[TestMethod]
	public void EditingAfterAnOverLimitPasteClearsTheReport()
	{
		// Arrange
		TokenPopup popup = new();
		Credential? confirmed = null;
		popup.Open("GitHub", "Personal access token", c => confirmed = c);
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");
		Paste(popup, new string('a', CredentialPopup.MaxCredentialLength + 1));
		Assert.IsTrue(popup.PasteTooLong, "Precondition: the rejected paste is reported.");

		// Act
		Paste(popup, "tok");

		// Assert
		Assert.IsFalse(popup.PasteTooLong, "A paste that fits should clear the report.");
		Assert.IsFalse(Press(popup, ImGuiKey.Enter), "Enter should close the popup.");
		Assert.IsNotNull(confirmed, "Enter should confirm.");
		Assert.AreEqual("tok", ((CredentialWithToken)confirmed).Token.WeakString);
	}

	/// <summary>
	/// A paste of exactly <see cref="CredentialPopup.MaxCredentialLength"/> bytes still fits, is not
	/// reported, and confirms with Enter or OK. The OK case also shows that <see cref="ClickOk"/>
	/// reaches the button, which the over-limit test relies on.
	/// </summary>
	/// <param name="pressOk">Whether to confirm with the OK button rather than Enter.</param>
	[TestMethod]
	[DataRow(false)]
	[DataRow(true)]
	public void APasteAtTheLimitIsNotReported(bool pressOk)
	{
		// Arrange
		TokenPopup popup = new();
		Credential? confirmed = null;
		string secret = new('a', CredentialPopup.MaxCredentialLength);
		popup.Open("GitHub", "Personal access token", c => confirmed = c);
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");

		// Act
		Paste(popup, secret);

		// Assert
		Assert.IsFalse(popup.PasteTooLong, "A paste that fits should not be reported.");
		Assert.IsFalse(pressOk ? ClickOk(popup) : Press(popup, ImGuiKey.Enter), "Confirming should close the popup.");
		Assert.IsNotNull(confirmed, "The popup should confirm.");
		Assert.AreEqual(secret, ((CredentialWithToken)confirmed).Token.WeakString);
	}

	/// <summary>
	/// Clicks the OK button by moving the mouse onto it and pressing the left button.
	/// </summary>
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
}
