# WorktreeInitializer

[![Tests](https://github.com/AdamTovatt/worktree-initializer/actions/workflows/dotnet.yml/badge.svg)](https://github.com/AdamTovatt/worktree-initializer/actions/workflows/dotnet.yml)
[![NuGet Version](https://img.shields.io/nuget/v/WorktreeInitializer.svg)](https://www.nuget.org/packages/WorktreeInitializer)
[![NuGet Downloads](https://img.shields.io/nuget/dt/WorktreeInitializer.svg)](https://www.nuget.org/packages/WorktreeInitializer)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)

A tool for copying gitignored files from a source repository to a new worktree. Works as both a CLI tool and an MCP (Model Context Protocol) server for AI agents.

When you create a git worktree, gitignored files like `.env` files, `launchSettings.json` with secrets, `node_modules/`, build outputs, and other local configuration are not included. This tool copies all of them over in one command.

## Installation

```bash
dotnet tool install --global WorktreeInitializer
```

After installation, the `wi` command will be available globally.

To update to the latest version:

```bash
dotnet tool update --global WorktreeInitializer
```

To uninstall:

```bash
dotnet tool uninstall --global WorktreeInitializer
```

To register it as an MCP tool in Claude Code:

```bash
claude mcp add worktree-init -- wi --mcp
```

For Cursor or other MCP clients, add this to your MCP configuration:

```json
{
  "mcpServers": {
    "worktree-init": {
      "command": "wi",
      "args": ["--mcp"]
    }
  }
}
```

## Usage

```bash
wi init <source-path> <destination-path>    # Copy gitignored files from source to destination
wi help                                      # Show help information
```

### Examples

```bash
# Copy gitignored files from your main repo to a new worktree
wi init C:\code\myproject C:\code\myproject-worktree

# Works with paths containing spaces (just quote them)
wi init "/home/user/my project" "/home/user/my project-wt"

# Unix paths work the same way
wi init ~/code/myproject ~/code/myproject-worktree
```

## Behavior

### Discovers ignored files using git

Runs `git ls-files --others --ignored --exclude-standard` in the source directory. This handles all `.gitignore` complexity: nested `.gitignore` files, global gitignore, `.git/info/exclude`, negation patterns, and every other gitignore feature.

### Preserves directory structure

Files are copied to the same relative path in the destination. If `src/bin/Debug/app.dll` is ignored in the source, it will be copied to `src/bin/Debug/app.dll` in the destination.

### Creates directories as needed

If the destination directory structure doesn't exist yet, it is created automatically during the copy.

### Overwrites existing files

If a file already exists in the destination, it is overwritten. This makes it safe to re-run `wi init` if the source files have changed.

### Partial failures are reported

If some files fail to copy (e.g. locked by another process), the rest are still copied. The output lists which files failed and why.

### Requires git in PATH

Since the tool uses `git ls-files` to discover ignored files, git must be installed and available in PATH. If you're using git worktrees, you already have git installed.

## As MCP Server

```bash
wi --mcp
```

When running as an MCP server, the following tools are available:

- `wi_init(sourcePath, destinationPath)` - Copy all gitignored files from source to destination
- `wi_help()` - Get help

## Development

```bash
git clone <repository-url>
cd worktree-initializer
dotnet build WorktreeInitializer.slnx
dotnet test WorktreeInitializer.slnx
```

To run as MCP server during development:

```bash
dotnet run --project WorktreeInitializer.Cli/WorktreeInitializer.Cli.csproj -- --mcp
```

To package:

```bash
dotnet pack WorktreeInitializer.Cli/WorktreeInitializer.Cli.csproj --configuration Release
```

## License

MIT License
