// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups.Test;

using ktsu.CredentialCache;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests that entered credentials do not outlive the showing they were typed into.
/// </summary>
/// <remarks>
/// A host typically holds one popup instance for the life of the application -- the pattern
/// <c>ProjectDirector</c> uses for its own popups. Without a reset, a token or password entered
/// once would stay in that instance indefinitely, the next showing would pre-fill with it, and
/// cancelling would not discard it.
///
/// These tests reach the input fields directly (they are <c>internal</c>, exposed via
/// <c>InternalsVisibleTo</c>) because the only production writer is <c>ShowEdit</c>, which calls
/// ImGui and needs a live context. Setting the field is the closest a headless test can get to
/// simulating typing.
/// </remarks>
[TestClass]
public sealed class CredentialResetTests
{
	private sealed class ProbeTokenPopup : TokenPopup
	{
		internal Credential Build() => MakeCredential();
		internal void ForceReset() => Reset();
	}

	private sealed class ProbeUsernamePasswordPopup : UsernamePasswordPopup
	{
		internal Credential Build() => MakeCredential();
		internal void ForceReset() => Reset();
	}

	private static string TokenOf(Credential credential) =>
		((CredentialWithToken)credential).Token.WeakString;

	private static (string Username, string Password) PairOf(Credential credential)
	{
		CredentialWithUsernamePassword pair = (CredentialWithUsernamePassword)credential;
		return (pair.Username.WeakString, pair.Password.WeakString);
	}

	/// <summary>
	/// Opening a popup must discard whatever the previous showing left behind, so a password box
	/// never renders a row of dots the user did not type.
	/// </summary>
	[TestMethod]
	public void OpeningATokenPopupDiscardsThePreviousValue()
	{
		// Arrange -- as if the user had typed a token during an earlier showing
		ProbeTokenPopup popup = new() { token = "ghp_previous_secret" };
		Assert.AreEqual("ghp_previous_secret", TokenOf(popup.Build()), "Precondition: the value is present.");

		// Act
		popup.Open("GitHub", "Personal access token", _ => { });

		// Assert
		Assert.AreEqual(string.Empty, TokenOf(popup.Build()));
	}

	/// <summary>
	/// The same for both fields of the username and password popup.
	/// </summary>
	[TestMethod]
	public void OpeningAUsernamePasswordPopupDiscardsThePreviousValues()
	{
		// Arrange
		ProbeUsernamePasswordPopup popup = new() { username = "previous-user", password = "previous-password" };

		// Act
		popup.Open("Azure DevOps", "Sign in", _ => { });

		// Assert
		(string username, string password) = PairOf(popup.Build());
		Assert.AreEqual(string.Empty, username);
		Assert.AreEqual(string.Empty, password);
	}

	/// <summary>
	/// Reset must clear the token outright, not merely on the next open.
	/// </summary>
	[TestMethod]
	public void ResetClearsTheToken()
	{
		// Arrange
		ProbeTokenPopup popup = new() { token = "ghp_secret" };

		// Act
		popup.ForceReset();

		// Assert
		Assert.AreEqual(string.Empty, popup.token);
		Assert.AreEqual(string.Empty, TokenOf(popup.Build()));
	}

	/// <summary>
	/// Reset must clear both halves of a username and password pair, not just one.
	/// </summary>
	[TestMethod]
	public void ResetClearsBothTheUsernameAndThePassword()
	{
		// Arrange
		ProbeUsernamePasswordPopup popup = new() { username = "user", password = "password" };

		// Act
		popup.ForceReset();

		// Assert
		Assert.AreEqual(string.Empty, popup.username);
		Assert.AreEqual(string.Empty, popup.password);
	}

	/// <summary>
	/// Reset on an already-empty popup must be harmless, since Open calls it unconditionally.
	/// </summary>
	[TestMethod]
	public void ResettingAFreshPopupIsANoOp()
	{
		// Arrange
		ProbeTokenPopup popup = new();

		// Act
		popup.ForceReset();

		// Assert
		Assert.AreEqual(string.Empty, popup.token);
	}

	/// <summary>
	/// Reopening repeatedly must keep discarding, not just on the first reopen.
	/// </summary>
	[TestMethod]
	public void EachReopenDiscardsAgain()
	{
		// Arrange
		ProbeTokenPopup popup = new();

		// Act & Assert -- three cycles of "type something, reopen, expect blank"
		for (int cycle = 0; cycle < 3; cycle++)
		{
			popup.token = $"secret-{cycle}";
			popup.Open("GitHub", "Token", _ => { });
			Assert.AreEqual(string.Empty, TokenOf(popup.Build()), $"Cycle {cycle} should have been cleared.");
		}
	}

	/// <summary>
	/// Clearing one popup must not disturb another, since a host holds several.
	/// </summary>
	[TestMethod]
	public void ResettingOnePopupDoesNotClearAnother()
	{
		// Arrange
		ProbeTokenPopup first = new();
		ProbeTokenPopup second = new();
		first.token = "first-secret";
		second.token = "second-secret";

		// Act
		first.Open("First", "Token", _ => { });

		// Assert
		Assert.AreEqual(string.Empty, TokenOf(first.Build()));
		Assert.AreEqual("second-secret", TokenOf(second.Build()));
	}
}
