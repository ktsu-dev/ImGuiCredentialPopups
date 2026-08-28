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
	/// <returns>A CredentialWithUsernamePassword object containing the username and password.</returns>
	protected override Credential MakeCredential() =>
		new CredentialWithUsernamePassword()
		{
			Username = username.As<CredentialUsername>(),
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
	/// <returns>True if the user completed the input via shortcuts, otherwise false.</returns>
	protected override bool ShowEdit()
	{
		ImGui.InputText("Username", ref username, 100);
		ImGui.InputText("Password", ref password, 100, ImGuiInputTextFlags.Password);
		return false;
	}
}
