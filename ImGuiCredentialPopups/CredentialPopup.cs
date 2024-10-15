namespace ktsu.ImGuiCredentialPopups;

using ktsu.CredentialCache;
using ImGuiPopups;
using ImGuiNET;
using ktsu.CaseConverter;
using ktsu.Extensions;

public abstract class CredentialPopup
{
	private Action<Credential> OnConfirm { get; set; } = null!;
	protected string Title { get; set; } = string.Empty;
	protected string Label { get; set; } = string.Empty;
	protected ImGuiPopups.Modal Modal { get; } = new();

	/// <summary>
	/// Open the popup and set the title, label, and button definitions.
	/// </summary>
	/// <param name="title">The title of the popup window.</param>
	/// <param name="label">The label of the input field.</param>
	/// <param name="onConfirm">The delegate to invoke when the popup has been confirmed</param>
	public virtual void Open(string title, string label, Action<Credential> onConfirm)
	{
		OnConfirm = onConfirm;
		Title = title;
		Label = label;
		Modal.Open(title, ShowContent);
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
			OnConfirm(MakeCredential());
			ImGui.CloseCurrentPopup();
		}

		ImGui.SameLine();
		if (ImGui.Button($"OK###{Title.ToSnakeCase()}_OK"))
		{
			OnConfirm(MakeCredential());
			ImGui.CloseCurrentPopup();
		}
	}

	protected abstract bool ShowEdit();
	protected abstract Credential MakeCredential();

	/// <summary>
	/// Show the modal if it is open.
	/// </summary>
	/// <returns>True if the modal is open.</returns>
	public bool ShowIfOpen() => Modal.ShowIfOpen();
}

public class UsernamePasswordPopup : CredentialPopup
{
	private string username = string.Empty;
	private string password = string.Empty;

	protected override Credential MakeCredential() =>
		new CredentialWithUsernamePassword()
		{
			Username = username.As<CredentialUsername>(),
			Password = password.As<CredentialPassword>(),
		};

	/// <summary>
	/// Show the content of the popup.
	/// </summary>
	protected override bool ShowEdit()
	{
		ImGui.InputText("Username", ref username, 100);
		ImGui.InputText("Password", ref password, 100, ImGuiInputTextFlags.Password);
		return false;
	}
}

public class TokenPopup : CredentialPopup
{
	private string token = string.Empty;

	protected override Credential MakeCredential() =>
		new CredentialWithToken()
		{
			Token = token.As<CredentialToken>(),
		};

	/// <summary>
	/// Show the content of the popup.
	/// </summary>
	protected override bool ShowEdit()
	{
		ImGui.InputText("Token", ref token, 100, ImGuiInputTextFlags.Password);
		return false;
	}
}
