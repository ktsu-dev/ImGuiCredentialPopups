// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups;

using Hexa.NET.ImGui;
using ktsu.CredentialCache;
using ktsu.Semantics.Strings;

/// <summary>
/// Popup dialog for collecting token-based credentials.
/// </summary>
public class TokenPopup : CredentialPopup
{
	// internal rather than private so the test project can set a value and prove that Reset
	// clears it. It must stay a field: ImGui.InputText takes a ref string, which a property
	// cannot satisfy. internal keeps it off the public API surface.
	internal string token = string.Empty;

	/// <summary>
	/// Creates a credential object with token from the current input value.
	/// </summary>
	/// <remarks>
	/// The token is trimmed. The field holds a single line, so ImGui turns a pasted line break into a
	/// space, and a token copied together with its line ending would otherwise come back with a
	/// trailing space the masked field hides. A token never legitimately starts or ends with whitespace.
	/// </remarks>
	/// <returns>A CredentialWithToken object containing the token.</returns>
	protected override Credential MakeCredential() =>
		new CredentialWithToken()
		{
			Token = token.Trim().As<CredentialToken>(),
		};

	/// <inheritdoc />
	protected override void Reset() => token = string.Empty;

	/// <summary>
	/// Displays a token input field.
	/// </summary>
	/// <returns>True if the user completed the input via shortcuts, otherwise false.</returns>
	protected override bool ShowEdit()
	{
		ImGui.InputText("Token", ref token, MaxCredentialLength + 1, ImGuiInputTextFlags.Password);
		return EnterPressedInLastItem();
	}
}
