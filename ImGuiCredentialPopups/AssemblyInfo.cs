// Copyright (c) 2023-2026 ktsu-dev contributors

// Required by KTSU0002 once a test project exists, independently of whether the tests actually
// reach any internal member -- CredentialPopupTests currently uses only the public and protected
// surface, via a derived probe class.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ktsu.ImGuiCredentialPopups.Test")]
