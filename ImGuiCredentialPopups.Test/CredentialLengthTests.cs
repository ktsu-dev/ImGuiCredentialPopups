// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups.Test;

using System.Numerics;
using Hexa.NET.ImGui;
using ktsu.CredentialCache;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that a long credential typed or pasted into a popup comes back whole.
/// </summary>
/// <remarks>
/// <c>ImGui.InputText</c> drops whatever does not fit its buffer, and the token and password fields
/// are masked, so a truncated secret is invisible to the user. These tests type into the fields
/// through ImGui's own input queue on a headless context, as <see cref="CredentialDismissTests"/>
/// does, because the truncation happens inside ImGui and a test that sets the field directly would
/// not see it.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class CredentialLengthTests
{
	private ImGuiContextPtr context;

	private sealed class ProbeTokenPopup : TokenPopup
	{
		internal Credential Build() => MakeCredential();
	}

	private sealed class ProbeUsernamePasswordPopup : UsernamePasswordPopup
	{
		internal Credential Build() => MakeCredential();
	}

	/// <summary>
	/// Creates a fresh headless ImGui context with a display.
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

	private static void Frames(CredentialPopup popup, int count)
	{
		for (int i = 0; i < count; i++)
		{
			ImGui.NewFrame();
			Assert.IsTrue(popup.ShowIfOpen(), "Precondition: the popup is showing.");
			ImGui.Render();
		}
	}

	private static void Type(CredentialPopup popup, string text)
	{
		ImGuiIOPtr io = ImGui.GetIO();
		foreach (char c in text)
		{
			io.AddInputCharacter(c);
		}

		Frames(popup, 3);
	}

	private static void PressTab(CredentialPopup popup)
	{
		ImGuiIOPtr io = ImGui.GetIO();
		io.AddKeyEvent(ImGuiKey.Tab, true);
		Frames(popup, 1);
		io.AddKeyEvent(ImGuiKey.Tab, false);
		Frames(popup, 2);
	}

	private static string LongSecret(int length) =>
		string.Concat(Enumerable.Range(0, length).Select(i => (char)('a' + (i % 26))));

	/// <summary>
	/// A token longer than the old 99-byte limit, such as a typical API key or bearer token, is
	/// returned unchanged.
	/// </summary>
	/// <param name="length">The token length.</param>
	[TestMethod]
	[DataRow(150)]
	[DataRow(1000)]
	public void ALongTokenComesBackWhole(int length)
	{
		// Arrange
		ProbeTokenPopup popup = new();
		popup.Open("GitHub", "Personal access token", _ => { });
		Frames(popup, 2);
		string secret = LongSecret(length);

		// Act
		Type(popup, secret);

		// Assert
		Assert.AreEqual(secret, ((CredentialWithToken)popup.Build()).Token.WeakString);
	}

	/// <summary>
	/// The same for both fields of the username and password popup.
	/// </summary>
	/// <param name="length">The username and password length.</param>
	[TestMethod]
	[DataRow(150)]
	[DataRow(1000)]
	public void ALongUsernameAndPasswordComeBackWhole(int length)
	{
		// Arrange
		ProbeUsernamePasswordPopup popup = new();
		popup.Open("Azure DevOps", "Sign in", _ => { });
		Frames(popup, 2);
		string username = LongSecret(length);
		string password = new([.. LongSecret(length).Reverse()]);

		// Act
		Type(popup, username);
		PressTab(popup);
		Type(popup, password);

		// Assert
		CredentialWithUsernamePassword pair = (CredentialWithUsernamePassword)popup.Build();
		Assert.AreEqual(username, pair.Username.WeakString);
		Assert.AreEqual(password, pair.Password.WeakString);
	}
}
