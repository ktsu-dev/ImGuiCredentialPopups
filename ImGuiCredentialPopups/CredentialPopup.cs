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
	/// A confirmed credential, and the callback it is for, waiting to be handed over once
	/// <see cref="ShowIfOpen"/> has finished drawing the modal.
	/// </summary>
	private (Credential Credential, Action<Credential> Callback)? pendingConfirm;
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

		bool canConfirm = !PasteTooLong && CanConfirm;
		if (enterPressed && canConfirm)
		{
			Confirm();
		}

		ImGui.SameLine();
		ImGui.BeginDisabled(!canConfirm);
		bool okClicked = ImGui.Button($"OK###{Title.ToSnakeCase()}_OK");
		OkButtonCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2f;
		ImGui.EndDisabled();
		if (okClicked && canConfirm)
		{
			Confirm();
		}
	}

	/// <summary>
	/// Gets whether the current input values make a credential worth confirming.
	/// </summary>
	/// <remarks>
	/// While this is false the OK button is disabled and Enter does not confirm, so the
	/// <c>onConfirm</c> callback is never handed a credential that cannot work, such as a blank token.
	/// The base implementation always allows confirming; the built-in popups require their fields to
	/// be filled in. Override it in a custom popup to apply the same guard to its own fields.
	/// </remarks>
	protected virtual bool CanConfirm => true;

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
		// Decide whether a paste fits before InputText runs: ImGui deletes the selection before it
		// tries the insert, so afterwards the selection that made room for the paste is gone.
		string before = value;
		PasteAttempt? paste = PasteAttemptIn(ImGui.GetID(label));

		bool edited = ImGui.InputText(label, ref value, MaxCredentialLength + 1, flags);
		if (paste is { Fits: false } rejected)
		{
			// ImGui inserts a paste only if all of it fits, and this one didn't. With nothing selected
			// that leaves the field as it was, but a selection has already been deleted, so put it back.
			PasteTooLong = true;
			if (edited)
			{
				value = before;
				RestoreAfterRejectedPaste(rejected);
				edited = false;
			}
		}
		else if (edited)
		{
			PasteTooLong = false;
		}

		return edited;
	}

	/// <summary>
	/// A paste about to be made into the active field, and the selection it replaces.
	/// </summary>
	/// <param name="Fits">Whether ImGui will insert it: the clipboard fits in the field once the selection is deleted.</param>
	/// <param name="SelectionStart">Where the selection starts, as ImGui counts it.</param>
	/// <param name="SelectionEnd">Where the selection ends, as ImGui counts it.</param>
	private readonly record struct PasteAttempt(bool Fits, int SelectionStart, int SelectionEnd);

	// The paste the field with this ID is about to make this frame, or null if it isn't pasting.
	private static PasteAttempt? PasteAttemptIn(uint id)
	{
		ImGuiContextPtr context = ImGui.GetCurrentContext();
		if (context.ActiveId != id || context.InputTextState.ID != id || !PasteShortcutPressed())
		{
			return null;
		}

		string? clipboard = ImGui.GetClipboardTextS();
		if (string.IsNullOrEmpty(clipboard))
		{
			return null;
		}

		// ImGui keeps the field's text, and the selection's ends, in UTF-8 bytes.
		int start = ImGuiP.GetSelectionStart(ref context.InputTextState);
		int end = ImGuiP.GetSelectionEnd(ref context.InputTextState);
		int remaining = MaxCredentialLength - context.InputTextState.TextLen + Math.Abs(end - start);
		return new PasteAttempt(Encoding.UTF8.GetByteCount(clipboard) <= remaining, start, end);
	}

	// Has ImGui reload the field from the value it held before the paste, with the same selection,
	// on the next frame. Until then ImGui's own buffer still lacks the deleted selection.
	private static void RestoreAfterRejectedPaste(PasteAttempt paste)
	{
		ImGuiContextPtr context = ImGui.GetCurrentContext();
		context.InputTextState.WantReloadUserBuf = 1;
		context.InputTextState.ReloadSelectionStart = paste.SelectionStart;
		context.InputTextState.ReloadSelectionEnd = paste.SelectionEnd;
	}

	// The shortcuts ImGui's InputText pastes on. With ConfigMacOSXBehaviors, ImGui reports Cmd as Ctrl.
	private static bool PasteShortcutPressed()
	{
		ImGuiIOPtr io = ImGui.GetIO();
		return (io.KeyCtrl && !io.KeyShift && ImGui.IsKeyPressed(ImGuiKey.V))
			|| (io.KeyShift && !io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Insert));
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
	/// Builds the credential, clears the input fields and closes the popup, leaving the credential for
	/// <see cref="ShowIfOpen"/> to hand to the caller.
	/// </summary>
	/// <remarks>
	/// The order is deliberate. <see cref="Reset"/> runs before <c>OnConfirm</c> so the secret is
	/// already out of this instance by the time the callback -- which may do anything, including
	/// throw -- is invoked. The callback is not invoked here: this runs between the modal's
	/// <c>BeginPopupModal</c> and <c>EndPopup</c>, and a callback that threw would skip
	/// <c>EndPopup</c> and leave ImGui's window stack unbalanced, which ImGui aborts the process for
	/// on the next <c>Render</c> even if the host caught the exception.
	/// </remarks>
	private void Confirm()
	{
		Credential credential = MakeCredential();
		Reset();
		pendingConfirm = (credential, OnConfirm);
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
	///
	/// A confirmation's <c>onConfirm</c> callback runs here, after the modal has been drawn and
	/// ImGui's popup scope has ended, so an exception it throws reaches the caller with the popup
	/// closed and ImGui's state intact.
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

		if (pendingConfirm is (Credential credential, Action<Credential> callback))
		{
			// Clear it first: the callback may throw, or reopen the popup and be confirmed again.
			pendingConfirm = null;
			callback(credential);
		}

		return open;
	}
}
