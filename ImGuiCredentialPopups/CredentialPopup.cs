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
	/// <summary>
	/// The longest credential, in UTF-8 bytes, that an input field accepts.
	/// </summary>
	/// <remarks>
	/// <c>ImGui.InputText</c> silently drops anything past its buffer size, and the token and password
	/// fields are masked, so a user cannot see a pasted secret being cut short. Real API keys and bearer
	/// tokens run from about a hundred bytes to several hundred, so this leaves generous headroom. The
	/// buffer size passed to <c>ImGui.InputText</c> counts the null terminator, so callers pass this plus one.
	/// </remarks>
	internal const int MaxCredentialLength = 4096;

	/// <summary>
	/// Whether the modal was showing as of the previous <see cref="ShowIfOpen"/> call.
	/// </summary>
	private bool wasShowing;

	/// <summary>
	/// Whether <see cref="Open"/> has been called since <see cref="ShowContent"/> last ran, so the next
	/// frame has to start a fresh showing even if the modal never closed in between.
	/// </summary>
	private bool freshShowing;

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
		freshShowing = true;
		OnConfirm = onConfirm;
		Title = title;
		Label = label;
		Modal.Open(title, ShowContent);
	}

	/// <summary>
	/// Clears any values entered into the popup's input fields.
	/// </summary>
	/// <remarks>
	/// Called when the popup is opened, immediately after the credential has been built on
	/// confirmation, and when the popup is dismissed without confirming. A host typically holds one popup instance for the life of the application,
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

		// Every Open is a fresh showing, including one made while the popup is already up -- a second
		// request, or an onConfirm that rejects the value and asks again. Modal.WasOpen stays true
		// across those, so it cannot be what decides this.
		if (freshShowing)
		{
			freshShowing = false;
			DiscardActiveEdit();
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
	/// Drops ImGui's own copy of whatever is being typed, so it cannot be written back over a field
	/// that <see cref="Reset"/> has just cleared.
	/// </summary>
	/// <remarks>
	/// While an <c>InputText</c> is active ImGui edits its own buffer and copies it into the
	/// <c>ref string</c> each frame. Deactivating it is not enough on its own: ImGui remembers the
	/// deactivated widget's text and writes that back on the next frame too. Both have to go, or the
	/// previous request's secret returns and is handed to the new callback.
	/// </remarks>
	private static void DiscardActiveEdit()
	{
		ImGuiP.ClearActiveID();
		ImGui.GetCurrentContext().InputTextDeactivatedState.ID = 0;
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
	/// Whether the item just drawn was the focused one when Enter or keypad Enter was pressed.
	/// </summary>
	/// <remarks>
	/// Call it straight after an <c>ImGui.InputText</c>. It deliberately does not rely on
	/// <see cref="ImGuiInputTextFlags.EnterReturnsTrue"/>: with that flag, Hexa's <c>ref string</c>
	/// overload writes the edited text back only on the frame Enter is pressed, so typing and then
	/// clicking OK would submit an empty value.
	/// </remarks>
	/// <returns>True if Enter was pressed in the item just drawn.</returns>
	protected static bool EnterPressedInLastItem() =>
		ImGui.IsItemFocused() && (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter));

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
	/// <remarks>
	/// The modal can also be dismissed without confirming -- by Escape or the title-bar close
	/// button -- inside <see cref="ImGuiPopups.Modal.ShowIfOpen"/>, where no code of ours runs. So
	/// this watches for the open-to-closed transition and resets then, ensuring a cancelled entry is
	/// discarded just as a confirmed one is.
	/// </remarks>
	/// <returns>True if the modal is open.</returns>
	public bool ShowIfOpen()
	{
		bool open = Modal.ShowIfOpen();
		if (wasShowing && !open)
		{
			Reset();
		}

		wasShowing = open;
		return open;
	}
}
