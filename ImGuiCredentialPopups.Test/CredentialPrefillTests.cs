// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups.Test;

using System.Numerics;
using Hexa.NET.ImGui;
using ktsu.CredentialCache;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for opening a <see cref="UsernamePasswordPopup"/> with the username already filled in: the
/// retry after a rejected sign-in keeps the username, focus starts in Password, and the password is
/// never carried over.
/// </summary>
/// <remarks>
/// These drive a headless ImGui context, as <see cref="CredentialEnterTests"/> does, and type
/// through ImGui's own input queue, so whatever is typed lands in whichever field has keyboard focus.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class CredentialPrefillTests
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
	/// Opening with a username fills it in and puts focus in Password, so typing the password and
	/// pressing Enter confirms both.
	/// </summary>
	[TestMethod]
	public void OpeningWithAUsernameFillsItInAndFocusesThePassword()
	{
		// Arrange
		UsernamePasswordPopup popup = new();
		Credential? confirmed = null;
		popup.Open("Sign in", "Enter the password for alice", "alice", c => confirmed = c);
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");

		// Act
		Type(popup, "hunter2");
		bool open = Press(popup, ImGuiKey.Enter);

		// Assert
		Assert.AreEqual(string.Empty, popup.username, "Confirming must reset the fields.");
		Assert.IsFalse(open, "Enter in Password should confirm and close the popup.");
		Assert.IsNotNull(confirmed, "Enter in Password should confirm.");
		CredentialWithUsernamePassword pair = (CredentialWithUsernamePassword)confirmed;
		Assert.AreEqual("alice", pair.Username.WeakString, "The pre-filled username should be confirmed unchanged.");
		Assert.AreEqual("hunter2", pair.Password.WeakString, "What was typed should go to Password.");
	}

	/// <summary>
	/// The retry pattern: a callback rejects the password and reopens with the same username. The
	/// retry keeps the username, starts with an empty password, and focuses Password.
	/// </summary>
	[TestMethod]
	public void RetryFromInsideOnConfirmKeepsTheUsernameAndClearsThePassword()
	{
		// Arrange -- the first sign-in is rejected and reopens with the username it was given
		UsernamePasswordPopup popup = new();
		List<CredentialWithUsernamePassword> received = [];
		void OnConfirm(Credential c)
		{
			CredentialWithUsernamePassword pair = (CredentialWithUsernamePassword)c;
			received.Add(pair);
			if (received.Count == 1)
			{
				popup.Open("Sign in", "Wrong password, try again", pair.Username.WeakString, OnConfirm);
			}
		}

		popup.Open("Sign in", "Enter credentials", OnConfirm);
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");
		Type(popup, "alice");
		Assert.IsTrue(Press(popup, ImGuiKey.Enter), "Precondition: Enter in Username moves on.");
		Type(popup, "bad");
		Press(popup, ImGuiKey.Enter);
		Assert.HasCount(1, received, "Precondition: the first sign-in was confirmed.");
		Assert.IsTrue(Frames(popup, 2), "The retry should be showing.");

		// Assert -- the retry's starting state
		Assert.AreEqual("alice", popup.username, "The retry should keep the username.");
		Assert.AreEqual(string.Empty, popup.password, "The retry must not keep the password.");

		// Act
		Type(popup, "good");
		bool open = Press(popup, ImGuiKey.Enter);

		// Assert
		Assert.IsFalse(open, "Confirming the retry should close the popup.");
		Assert.HasCount(2, received);
		Assert.AreEqual("alice", received[1].Username.WeakString);
		Assert.AreEqual("good", received[1].Password.WeakString, "The retry's typing should go to Password.");
	}

	/// <summary>
	/// Opening with an empty username behaves like the overload without one: both fields start empty
	/// and focus starts in Username.
	/// </summary>
	[TestMethod]
	public void OpeningWithAnEmptyUsernameFocusesTheUsername()
	{
		// Arrange
		UsernamePasswordPopup popup = new();
		popup.Open("Sign in", "Enter credentials", string.Empty, _ => { });
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");

		// Act
		Type(popup, "bob");

		// Assert
		Assert.AreEqual("bob", popup.username, "With no username given, typing should go to Username.");
		Assert.AreEqual(string.Empty, popup.password);
	}

	/// <summary>
	/// A pre-filled username does not survive into a later plain <see cref="CredentialPopup.Open"/>.
	/// </summary>
	[TestMethod]
	public void APlainOpenAfterAPrefilledOneStartsEmptyInUsername()
	{
		// Arrange -- a pre-filled showing is dismissed with Escape
		UsernamePasswordPopup popup = new();
		popup.Open("Sign in", "Enter the password for alice", "alice", _ => { });
		Assert.IsTrue(Frames(popup, 2), "Precondition: the popup is showing.");
		Assert.IsFalse(Press(popup, ImGuiKey.Escape), "Precondition: Escape dismisses the popup.");

		// Act
		popup.Open("Sign in", "Enter credentials", _ => { });
		Assert.IsTrue(Frames(popup, 2), "The popup should show again.");
		Type(popup, "bob");

		// Assert
		Assert.AreEqual("bob", popup.username, "A plain Open should start empty, with focus in Username.");
		Assert.AreEqual(string.Empty, popup.password);
	}
}
