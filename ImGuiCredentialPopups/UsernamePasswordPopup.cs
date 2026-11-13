// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups;

using Hexa.NET.ImGui;
using ktsu.CredentialCache;
using ktsu.Semantics.Strings;

/// <summary>
/// Popup dialog for collecting username and password credentials.
/// </summary>
public class UsernamePasswordPopup : CredentialPopup
{
	// internal rather than private so the test project can set values and prove that Reset
	// clears them. They must stay fields: ImGui.InputText takes a ref string, which a property
	// cannot satisfy. internal keeps them off the public API surface.
	internal string username = string.Empty;
	internal string password = string.Empty;

	/// <summary>
	/// Whether the current showing should start with keyboard focus in Password rather than Username,
	/// because <see cref="Open(string, string, string, Action{Credential})"/> pre-filled the username.
	/// </summary>
	private bool focusPassword;

	/// <summary>
	/// Open the popup with the username already filled in, for a retry after a rejected sign-in or
	/// for re-entering the password of a user the app already knows.
	/// </summary>
	/// <remarks>
	/// Only the username is kept: the password field starts empty, as it does for every
	/// <see cref="CredentialPopup.Open"/>. When <paramref name="username"/> isn't empty, keyboard focus
	/// starts in the password field, so the password can be typed straight away.
	/// </remarks>
	/// <param name="title">The title of the popup window.</param>
	/// <param name="label">The label of the input fields.</param>
	/// <param name="username">The username to fill in. Empty leaves the field empty, as the other overload does.</param>
	/// <param name="onConfirm">The delegate to invoke when the popup has been confirmed.</param>
	public void Open(string title, string label, string username, Action<Credential> onConfirm)
	{
		Ensure.NotNull(username);
		Open(title, label, onConfirm);
		this.username = username;
		focusPassword = username.Length > 0;
	}

	/// <summary>
	/// Creates a credential object with username and password from the current input values.
	/// </summary>
	/// <remarks>
	/// The username is trimmed, because each field holds a single line and ImGui turns a pasted line
	/// break into a space, so a username copied with its line ending would otherwise gain a trailing
	/// space. The password is returned unchanged, since a password can legitimately start or end with
	/// a space; a line break pasted into it arrives as a space.
	/// </remarks>
	/// <returns>A CredentialWithUsernamePassword object containing the username and password.</returns>
	protected override Credential MakeCredential() =>
		new CredentialWithUsernamePassword()
		{
			Username = username.Trim().As<CredentialUsername>(),
			Password = password.As<CredentialPassword>(),
		};

	/// <inheritdoc />
	protected override void Reset()
	{
		username = string.Empty;
		password = string.Empty;
		focusPassword = false;
	}

	/// <summary>
	/// Gets whether both a username and a password have been entered. A username of only whitespace
	/// counts as none, since <see cref="MakeCredential"/> trims it to an empty one. The password is not
	/// trimmed, so any password that isn't empty counts.
	/// </summary>
	protected override bool CanConfirm => !string.IsNullOrWhiteSpace(username) && password.Length > 0;

	/// <summary>
	/// Displays username and password input fields.
	/// </summary>
	/// <remarks>
	/// Enter in the username field moves on to the password field; Enter in the password field
	/// confirms.
	/// </remarks>
	/// <returns>True if the user completed the input via shortcuts, otherwise false.</returns>
	protected override bool ShowEdit()
	{
		InputCredential("Username", ref username);
		if (EnterPressedInLastItem() || focusPassword)
		{
			// A fresh showing focuses the first field; a pre-filled username moves that on to Password.
			focusPassword = false;
			ImGui.SetKeyboardFocusHere();
		}

		InputCredential("Password", ref password, ImGuiInputTextFlags.Password);
		return EnterPressedInLastItem();
	}
}
