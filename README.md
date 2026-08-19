# ktsu.ImGuiCredentialPopups

> Ready-made Dear ImGui modal dialogs for collecting credentials.

[![License](https://img.shields.io/github/license/ktsu-dev/ImGuiCredentialPopups.svg?label=License&logo=nuget)](LICENSE.md)
[![NuGet Version](https://img.shields.io/nuget/v/ktsu.ImGuiCredentialPopups?label=Stable&logo=nuget)](https://nuget.org/packages/ktsu.ImGuiCredentialPopups)
[![NuGet Version](https://img.shields.io/nuget/vpre/ktsu.ImGuiCredentialPopups?label=Latest&logo=nuget)](https://nuget.org/packages/ktsu.ImGuiCredentialPopups)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ktsu.ImGuiCredentialPopups?label=Downloads&logo=nuget)](https://nuget.org/packages/ktsu.ImGuiCredentialPopups)
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/ktsu-dev/ImGuiCredentialPopups?label=Commits&logo=github)](https://github.com/ktsu-dev/ImGuiCredentialPopups/commits/main)
[![GitHub contributors](https://img.shields.io/github/contributors/ktsu-dev/ImGuiCredentialPopups?label=Contributors&logo=github)](https://github.com/ktsu-dev/ImGuiCredentialPopups/graphs/contributors)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/ktsu-dev/ImGuiCredentialPopups/dotnet.yml?label=Build&logo=github)](https://github.com/ktsu-dev/ImGuiCredentialPopups/actions)

## Introduction

`ktsu.ImGuiCredentialPopups` provides drop-in modal dialogs for asking a user for credentials
inside a Dear ImGui application. Rather than hand-rolling an input modal, masked text field, and
confirm/cancel button pair for every application that needs to authenticate, you open a popup and
receive a `Credential` back through a callback.

The credential types come from [`ktsu.CredentialCache`](https://github.com/ktsu-dev/CredentialCache),
so what the popup hands you is ready to persist in the host's native keyring or pass straight to an
API client.

## Features

- **Username/Password Popup**: `UsernamePasswordPopup` collects a username and a masked password
  and returns a `CredentialWithUsernamePassword`.
- **Token Popup**: `TokenPopup` collects a single masked token value and returns a
  `CredentialWithToken`.
- **Shared Base Class**: `CredentialPopup` handles the modal lifecycle, title and label
  configuration, and confirmation callback, so adding a new credential shape means overriding two
  methods.
- **Masked Input**: Secret fields use `ImGuiInputTextFlags.Password` so values are never rendered
  in plain text.
- **Automatic Focus**: The first input field receives keyboard focus when the modal opens, so the
  user can start typing immediately.
- **CredentialCache Integration**: Results are `ktsu.CredentialCache` credential types, ready to
  store through an `ICredentialStore`.

## Installation

### Package Manager Console

```powershell
Install-Package ktsu.ImGuiCredentialPopups
```

### .NET CLI

```bash
dotnet add package ktsu.ImGuiCredentialPopups
```

### Package Reference

```xml
<PackageReference Include="ktsu.ImGuiCredentialPopups" Version="x.y.z" />
```

## Usage Examples

### Basic Example

Create the popup once and keep it alive for the lifetime of the window, then call `ShowIfOpen()`
every frame from your render loop.

```csharp
using ktsu.CredentialCache;
using ktsu.ImGuiCredentialPopups;

private readonly UsernamePasswordPopup loginPopup = new();

private void OnRender()
{
    if (ImGui.Button("Sign in"))
    {
        loginPopup.Open("Sign in to GitHub", "Credentials", credential =>
        {
            CredentialWithUsernamePassword login = (CredentialWithUsernamePassword)credential;
            Authenticate(login.Username, login.Password);
        });
    }

    // Must be called every frame so the modal can draw itself.
    loginPopup.ShowIfOpen();
}
```

### Collecting a Token

```csharp
using ktsu.CredentialCache;
using ktsu.ImGuiCredentialPopups;

private readonly TokenPopup tokenPopup = new();

private void OnRender()
{
    if (ImGui.Button("Set access token"))
    {
        tokenPopup.Open("Personal Access Token", "Token", credential =>
        {
            CredentialWithToken token = (CredentialWithToken)credential;
            cache.Store("github", token);
        });
    }

    tokenPopup.ShowIfOpen();
}
```

### Adding a Custom Credential Popup

Derive from `CredentialPopup` and supply the input UI and the credential it produces.

```csharp
using ktsu.ImGuiCredentialPopups;

public class ApiKeyPopup : CredentialPopup
{
    private string apiKey = string.Empty;

    protected override bool ShowEdit()
    {
        ImGui.InputText("API Key", ref apiKey, 200, ImGuiInputTextFlags.Password);
        return false;
    }

    protected override Credential MakeCredential() =>
        new CredentialWithToken { Token = apiKey.As<CredentialToken>() };
}
```

## API Reference

### `CredentialPopup`

Abstract base class for credential input popups.

#### Properties

| Name | Type | Description |
|------|------|-------------|
| `Title` | `string` | Title of the popup window. |
| `Label` | `string` | Label shown above the input fields. |
| `Modal` | `ImGuiPopups.Modal` | The underlying modal used to render the dialog. |

#### Methods

| Name | Return Type | Description |
|------|-------------|-------------|
| `Open(string title, string label, Action<Credential> onConfirm)` | `void` | Opens the popup and registers the callback invoked on confirmation. |
| `ShowIfOpen()` | `bool` | Renders the popup if it is open and returns whether it is open. Call once per frame. |
| `ShowEdit()` | `bool` | *(protected, abstract)* Draws the input fields. Returns `true` if the user completed input via a keyboard shortcut. |
| `MakeCredential()` | `Credential` | *(protected, abstract)* Builds the credential from the current input values. |

### `UsernamePasswordPopup`

`CredentialPopup` that collects a username and masked password, producing a
`CredentialWithUsernamePassword`.

### `TokenPopup`

`CredentialPopup` that collects a single masked token, producing a `CredentialWithToken`.

## Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

## License

This project is licensed under the MIT License. See the [LICENSE.md](LICENSE.md) file for details.
