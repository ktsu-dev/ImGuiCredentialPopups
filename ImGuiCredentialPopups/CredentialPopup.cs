// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups;

using Hexa.NET.ImGui;

using ktsu.ImGui.Popups;

using ktsu.CaseConverter;
using ktsu.CredentialCache;

/// <summary>
/// Abstract base class for credential input popups with ImGui.
/// Provides common functionality for displaying modal dialogs to collect credentials.
/// </summary>
public abstract class CredentialPopup
{
	private Action<Credential> OnConfirm { get; set; } = null!;
	/// <summary>
	/// Gets or sets the title of the popup window.
	/// </summary>
	protected string Title { get; set; } = string.Empty;
	/// <summary>
	/// Gets or sets the label for the input field.
	/// </summary>
	protected string Label { get; set; } = string.Empty;
	/// <summary>
	/// Gets the modal popup instance used to display the credential dialog.
	/// </summary>
	protected ImGuiPopups.Modal Modal { get; } = new();

	/// <summary>
	/// Open the popup and set the title, label, and button definitions.
	/// </summary>
	/// <param name="title">The title of the popup window.</param>
	/// <param name="label">The label of the input field.</param>
	/// <param name="onConfirm">The delegate to invoke when the popup has been confirmed</param>
	public virtual void Open(string title, string label, Action<Credential> onConfirm)
	{
		Reset();
		OnConfirm = onConfirm;
		Title = title;
		Label = label;
		Modal.Open(title, ShowContent);
	}

	/// <summary>
	/// Clears any values entered into the popup's input fields.
	/// </summary>
	/// <remarks>
	/// Called when the popup is opened, and again immediately after the credential has been built
	/// on confirmation. A host typically holds one popup instance for the life of the application,
	/// so without this a secret would stay in the instance indefinitely and the next showing would
	/// pre-fill with it.
	///
	/// This shortens the window the secret is held; it does not erase it. The fields are
	/// <see langword="string"/>, which is immutable, so reassignment leaves the previous value on
	/// the managed heap until it is collected. Erasing it properly would mean holding the input in
	/// a <see langword="char"/> buffer that can be zeroed in place, which collides with
	/// <c>ImGui.InputText</c>'s <c>ref string</c> signature -- tracked separately.
	/// </remarks>
	protected virtual void Reset()
	{
	}

	/// <summary>
	/// Show the content of the popup.
	/// </summary>
	private void ShowContent()
	{
		ImGui.TextUnformatted(Label);
		ImGui.NewLine();

		if (!Modal.WasOpen && !ImGui.IsItemFocused())
		{
			ImGui.SetKeyboardFocusHere();
		}

		if (ShowEdit())
		{
			Confirm();
		}

		ImGui.SameLine();
		if (ImGui.Button($"OK###{Title.ToSnakeCase()}_OK"))
		{
			Confirm();
		}
	}

	/// <summary>
	/// Builds the credential, clears the input fields, then hands the credential to the caller.
	/// </summary>
	/// <remarks>
	/// The order is deliberate. <see cref="Reset"/> runs before <c>OnConfirm</c> so the secret is
	/// already out of this instance by the time the callback -- which may do anything, including
	/// throw -- is invoked.
	/// </remarks>
	private void Confirm()
	{
		Credential credential = MakeCredential();
		Reset();
		OnConfirm(credential);
		ImGui.CloseCurrentPopup();
	}

	/// <summary>
	/// Shows the credential input fields.
	/// </summary>
	/// <returns>True if the edit process was completed and the popup should close, otherwise false.</returns>
	protected abstract bool ShowEdit();

	/// <summary>
	/// Creates a credential object from the current input values.
	/// </summary>
	/// <returns>A credential object containing the user input.</returns>
	protected abstract Credential MakeCredential();

	/// <summary>
	/// Show the modal if it is open.
	/// </summary>
	/// <returns>True if the modal is open.</returns>
	public bool ShowIfOpen() => Modal.ShowIfOpen();
}
