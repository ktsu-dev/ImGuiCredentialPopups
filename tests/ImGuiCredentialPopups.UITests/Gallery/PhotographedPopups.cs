// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.ImGuiCredentialPopups.UITests.Gallery;

using Hexa.NET.ImGui;

using ktsu.CredentialCache;
using ktsu.ImGui.App.Testing;
using ktsu.Semantics.Strings;

/// <summary>A credential popup that remembers where its modal window was drawn, so the gallery can crop to it.</summary>
internal interface IPhotographedPopup
{
	/// <summary>Gets the popup being photographed.</summary>
	public CredentialPopup Popup { get; }

	/// <summary>Gets the modal window's rectangle on the most recent frame it drew, or null if it has not drawn.</summary>
	public Rectangle? Window { get; }
}

/// <summary>Measures the window the popup's fields are being drawn into.</summary>
internal static class PopupWindow
{
	/// <summary>Reads the current window's rectangle. Call from inside the popup's own drawing.</summary>
	internal static Rectangle Measure()
	{
		System.Numerics.Vector2 position = ImGui.GetWindowPos();
		System.Numerics.Vector2 size = ImGui.GetWindowSize();
		return new Rectangle(
			(int)MathF.Floor(position.X),
			(int)MathF.Floor(position.Y),
			(int)MathF.Ceiling(position.X + size.X),
			(int)MathF.Ceiling(position.Y + size.Y));
	}
}

/// <summary>The shipped <see cref="UsernamePasswordPopup"/>, drawn unchanged, recording its window.</summary>
internal sealed class PhotographedUsernamePasswordPopup : UsernamePasswordPopup, IPhotographedPopup
{
	/// <inheritdoc/>
	public CredentialPopup Popup => this;

	/// <inheritdoc/>
	public Rectangle? Window { get; private set; }

	/// <inheritdoc/>
	protected override bool ShowEdit()
	{
		bool completed = base.ShowEdit();
		Window = PopupWindow.Measure();
		return completed;
	}
}

/// <summary>The shipped <see cref="TokenPopup"/>, drawn unchanged, recording its window.</summary>
internal sealed class PhotographedTokenPopup : TokenPopup, IPhotographedPopup
{
	/// <inheritdoc/>
	public CredentialPopup Popup => this;

	/// <inheritdoc/>
	public Rectangle? Window { get; private set; }

	/// <inheritdoc/>
	protected override bool ShowEdit()
	{
		bool completed = base.ShowEdit();
		Window = PopupWindow.Measure();
		return completed;
	}
}

/// <summary>
/// The custom popup from the README's "Adding a Custom Credential Popup" example, written as it is
/// there, so the gallery shows what a few lines of derived class buy.
/// </summary>
internal class ApiKeyPopup : CredentialPopup
{
	private string apiKey = string.Empty;

	/// <inheritdoc/>
	protected override bool ShowEdit()
	{
		ImGui.InputText("API Key", ref apiKey, 200, ImGuiInputTextFlags.Password);
		return false;
	}

	/// <inheritdoc/>
	protected override Credential MakeCredential() =>
		new CredentialWithToken { Token = apiKey.As<CredentialToken>() };
}

/// <summary>The README's <see cref="ApiKeyPopup"/>, drawn unchanged, recording its window.</summary>
internal sealed class PhotographedApiKeyPopup : ApiKeyPopup, IPhotographedPopup
{
	/// <inheritdoc/>
	public CredentialPopup Popup => this;

	/// <inheritdoc/>
	public Rectangle? Window { get; private set; }

	/// <inheritdoc/>
	protected override bool ShowEdit()
	{
		bool completed = base.ShowEdit();
		Window = PopupWindow.Measure();
		return completed;
	}
}
