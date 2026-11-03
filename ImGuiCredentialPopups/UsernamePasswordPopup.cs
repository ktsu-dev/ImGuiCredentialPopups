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
	}

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
		ImGui.InputText("Username", ref username, MaxCredentialLength + 1);
		if (EnterPressedInLastItem())
		{
			ImGui.SetKeyboardFocusHere();
		}

		ImGui.InputText("Password", ref password, MaxCredentialLength + 1, ImGuiInputTextFlags.Password);
		return EnterPressedInLastItem();
	}
}
