// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups;

using System.Numerics;
using System.Text;
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
	/// <c>ImGui.InputText</c> rejects a paste that doesn't fit in the space left in its buffer
	/// <em>whole</em>: none of it is inserted, and the token and password fields are masked, so the user
	/// would see nothing wrong. <see cref="InputCredential"/> detects that, reports it under the field,
	/// and keeps Enter and OK from confirming until the field is edited. Most API keys and bearer tokens
	/// are a few hundred bytes, though a JWT carrying many claims can run to several kilobytes. The buffer
	/// size passed to <c>ImGui.InputText</c> counts the null terminator, so callers pass this plus one.
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

	/// <summary>
	/// Gets whether the last paste into one of the fields was too long to fit, so ImGui inserted none
	/// of it. While this is set, the popup shows why and Enter and OK do not confirm. Editing a field
	/// or reopening the popup clears it.
	/// </summary>
	internal bool PasteTooLong { get; private set; }

	/// <summary>
	/// Gets the centre of the OK button as of the last frame, so tests can click it.
	/// </summary>
	internal Vector2 OkButtonCenter { get; private set; }

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
		PasteTooLong = false;
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

		bool enterPressed = ShowEdit();
		if (PasteTooLong)
		{
			ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), $"The pasted text is too long (the limit is {MaxCredentialLength} bytes), so none of it was entered.");
		}

		if (enterPressed && !PasteTooLong)
		{
			Confirm();
		}

		ImGui.SameLine();
		ImGui.BeginDisabled(PasteTooLong);
		bool okClicked = ImGui.Button($"OK###{Title.ToSnakeCase()}_OK");
		OkButtonCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2f;
		ImGui.EndDisabled();
		if (okClicked && !PasteTooLong)
		{
			Confirm();
		}
	}

	/// <summary>
	/// Displays one credential input field, holding at most <see cref="MaxCredentialLength"/> UTF-8 bytes,
	/// and records a paste that was rejected for not fitting in it.
	/// </summary>
	/// <param name="label">The field's label.</param>
	/// <param name="value">The field's value.</param>
	/// <param name="flags">Flags for <c>ImGui.InputText</c>, such as <see cref="ImGuiInputTextFlags.Password"/>.</param>
	/// <returns>True if the value was edited this frame.</returns>
	protected bool InputCredential(string label, ref string value, ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
	{
		bool edited = ImGui.InputText(label, ref value, MaxCredentialLength + 1, flags);
		if (edited)
		{
			PasteTooLong = false;
		}
		else if (ImGui.IsItemActive() && PasteShortcutPressed() && !ClipboardFits(value))
		{
			// ImGui inserts a paste only if all of it fits, and this one didn't, so nothing changed.
			PasteTooLong = true;
		}

		return edited;
	}

	// The shortcuts ImGui's InputText pastes on. With ConfigMacOSXBehaviors, ImGui reports Cmd as Ctrl.
	private static bool PasteShortcutPressed()
	{
		ImGuiIOPtr io = ImGui.GetIO();
		return (io.KeyCtrl && !io.KeyShift && ImGui.IsKeyPressed(ImGuiKey.V))
			|| (io.KeyShift && !io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Insert));
	}

	// Whether the clipboard fits in the space left after the field's current text. A paste that
	// replaces a selection has more room than this, but such a paste that still didn't change the
	// field was rejected all the same.
	private static bool ClipboardFits(string value)
	{
		string? clipboard = ImGui.GetClipboardTextS();
		if (string.IsNullOrEmpty(clipboard))
		{
			return true;
		}

		int remaining = MaxCredentialLength - Encoding.UTF8.GetByteCount(value);
		return Encoding.UTF8.GetByteCount(clipboard) <= remaining;
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
