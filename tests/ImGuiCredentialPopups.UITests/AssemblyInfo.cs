// Copyright (c) 2023-2026 ktsu-dev contributors

// ImGui contexts are process-global and the harness refuses to start while another is live, so
// every test in this assembly must have the process to itself.
[assembly: Microsoft.VisualStudio.TestTools.UnitTesting.DoNotParallelize]
