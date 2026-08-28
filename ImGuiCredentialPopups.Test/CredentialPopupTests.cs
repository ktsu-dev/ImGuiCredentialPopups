// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups.Test;

using ktsu.CredentialCache;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for the parts of the credential popups that do not need a live ImGui context.
/// </summary>
/// <remarks>
/// Everything these popups draw runs inside <c>CredentialPopup.ShowContent</c>, which calls ImGui
/// directly and so needs a real context, a window and a display. What can be reached without one
/// is the state management around that drawing: what <see cref="CredentialPopup.Open"/> records,
/// and what <c>MakeCredential</c> produces from the current input values. That is where a mistake
/// would leak one popup's credential into the next, so it is worth pinning even though it is not
/// the whole class.
///
/// The ImGui-dependent half -- that the OK button invokes the confirm callback exactly once, that
/// cancelling invokes it not at all, and that a submitted popup returns what was typed into it --
/// is not covered here. Reaching it needs the headless harness in <c>ktsu.ImGui.App.Testing</c>,
/// which this library does not reference.
/// </remarks>
[TestClass]
public sealed class CredentialPopupTests
{
	/// <summary>
	/// Exposes <see cref="CredentialPopup"/>'s protected surface so tests can observe it.
	/// </summary>
	private sealed class ProbeTokenPopup : TokenPopup
	{
		internal string ObservedTitle => Title;
		internal string ObservedLabel => Label;
		internal Credential Build() => MakeCredential();
	}

	/// <summary>
	/// Exposes <see cref="UsernamePasswordPopup"/>'s protected surface.
	/// </summary>
	private sealed class ProbeUsernamePasswordPopup : UsernamePasswordPopup
	{
		internal string ObservedTitle => Title;
		internal string ObservedLabel => Label;
		internal Credential Build() => MakeCredential();
	}

	/// <summary>
	/// Opening a popup must record the title and label it was given, since both are drawn later
	/// and the title also forms the modal's ImGui id.
	/// </summary>
	[TestMethod]
	public void OpeningATokenPopupRecordsTheTitleAndLabel()
	{
		// Arrange
		ProbeTokenPopup popup = new();

		// Act
		popup.Open("GitHub", "Personal access token", _ => { });

		// Assert
		Assert.AreEqual("GitHub", popup.ObservedTitle);
		Assert.AreEqual("Personal access token", popup.ObservedLabel);
	}

	/// <summary>
	/// The same for the username and password popup, which shares the base implementation.
	/// </summary>
	[TestMethod]
	public void OpeningAUsernamePasswordPopupRecordsTheTitleAndLabel()
	{
		// Arrange
		ProbeUsernamePasswordPopup popup = new();

		// Act
		popup.Open("Azure DevOps", "Sign in", _ => { });

		// Assert
		Assert.AreEqual("Azure DevOps", popup.ObservedTitle);
		Assert.AreEqual("Sign in", popup.ObservedLabel);
	}

	/// <summary>
	/// Reopening must replace the previous title and label rather than keeping the first ones.
	/// </summary>
	[TestMethod]
	public void ReopeningReplacesTheTitleAndLabel()
	{
		// Arrange
		ProbeTokenPopup popup = new();
		popup.Open("First", "First label", _ => { });

		// Act
		popup.Open("Second", "Second label", _ => { });

		// Assert
		Assert.AreEqual("Second", popup.ObservedTitle);
		Assert.AreEqual("Second label", popup.ObservedLabel);
	}

	/// <summary>
	/// A token popup must produce a token credential, not some other shape -- the caller branches
	/// on the concrete type.
	/// </summary>
	[TestMethod]
	public void ATokenPopupProducesATokenCredential()
	{
		// Arrange
		ProbeTokenPopup popup = new();

		// Act
		Credential credential = popup.Build();

		// Assert
		Assert.IsInstanceOfType<CredentialWithToken>(credential);
	}

	/// <summary>
	/// A username and password popup must produce a username and password credential.
	/// </summary>
	[TestMethod]
	public void AUsernamePasswordPopupProducesAUsernamePasswordCredential()
	{
		// Arrange
		ProbeUsernamePasswordPopup popup = new();

		// Act
		Credential credential = popup.Build();

		// Assert
		Assert.IsInstanceOfType<CredentialWithUsernamePassword>(credential);
	}

	/// <summary>
	/// A popup that has never been typed into must produce empty values rather than nulls, since
	/// the semantic string types are constructed from whatever the field currently holds.
	/// </summary>
	[TestMethod]
	public void AFreshTokenPopupProducesAnEmptyToken()
	{
		// Arrange
		ProbeTokenPopup popup = new();

		// Act
		CredentialWithToken credential = (CredentialWithToken)popup.Build();

		// Assert
		Assert.IsNotNull(credential.Token);
		Assert.AreEqual(string.Empty, credential.Token.WeakString);
	}

	/// <summary>
	/// The same for both fields of a fresh username and password popup.
	/// </summary>
	[TestMethod]
	public void AFreshUsernamePasswordPopupProducesEmptyValues()
	{
		// Arrange
		ProbeUsernamePasswordPopup popup = new();

		// Act
		CredentialWithUsernamePassword credential = (CredentialWithUsernamePassword)popup.Build();

		// Assert
		Assert.IsNotNull(credential.Username);
		Assert.IsNotNull(credential.Password);
		Assert.AreEqual(string.Empty, credential.Username.WeakString);
		Assert.AreEqual(string.Empty, credential.Password.WeakString);
	}

	/// <summary>
	/// Each construction must produce a fresh credential instance rather than handing out a cached
	/// one, or a caller mutating what it received would reach back into the popup.
	/// </summary>
	[TestMethod]
	public void EachBuildProducesADistinctCredentialInstance()
	{
		// Arrange
		ProbeTokenPopup popup = new();

		// Act
		Credential first = popup.Build();
		Credential second = popup.Build();

		// Assert
		Assert.AreNotSame(first, second);
	}

	/// <summary>
	/// Two popup instances must not share input state, since a host typically holds one per
	/// credential it collects.
	/// </summary>
	[TestMethod]
	public void SeparateInstancesDoNotShareState()
	{
		// Arrange
		ProbeTokenPopup first = new();
		ProbeTokenPopup second = new();

		// Act
		first.Open("First", "First label", _ => { });

		// Assert
		Assert.AreEqual(string.Empty, second.ObservedTitle);
		Assert.AreEqual(string.Empty, second.ObservedLabel);
	}
}
