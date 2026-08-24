# WorktreeInitializer

[![Tests](https://github.com/AdamTovatt/worktree-initializer/actions/workflows/dotnet.yml/badge.svg)](https://github.com/AdamTovatt/worktree-initializer/actions/workflows/dotnet.yml)
[![NuGet Version](https://img.shields.io/nuget/v/WorktreeInitializer.svg)](https://www.nuget.org/packages/WorktreeInitializer)
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
wi init [--ignore <path>]... [--include <path>]...
wi init <source-path> <destination-path> [--ignore <path>]... [--include <path>]...
wi help
```

### Examples

```bash
# Auto-detect: run inside a worktree to automatically find the source repo
cd ~/code/myproject-worktree
wi init

# Explicit paths: specify source and destination directly
wi init C:\code\myproject C:\code\myproject-worktree

# Exclude specific directories from copying
wi init ./myproject ./myproject-worktree --ignore node_modules --ignore .venv

# Re-include a path that would otherwise be ignored (include wins over ignore)
wi init ./myproject ./myproject-worktree --ignore node_modules --include node_modules

# Auto-detect with flags
wi init --ignore node_modules --ignore .venv

# Works with paths containing spaces (just quote them)
wi init "/home/user/my project" "/home/user/my project-wt"

# Unix paths work the same way
wi init ~/code/myproject ~/code/myproject-worktree
```

### Auto-detect mode

When you run `wi init` with no paths inside a git worktree, it automatically detects the main repository as the source and uses the current directory as the destination. This is the simplest way to use the tool — just `cd` into your worktree and run `wi init`.

### WorktreeConfig.json

You can place a `WorktreeConfig.json` file in the source repo root to define default ignores and
commands to run once the copy has finished:

```json
{
  "ignores": ["node_modules", ".venv", "dist"],
  "postInitialize": ["npm install"]
}
```

- The file is optional — if missing, no default ignores are applied and no commands are run
- If present but malformed, an error is reported
- CLI `--ignore` flags are merged with config file ignores (union of both)
- `--include` overrides both CLI `--ignore` and config file ignores (include always wins)

### postInitialize

Some things cannot be copied into place and have to be rebuilt in the worktree: a Python virtualenv
records its own absolute path, and a package manager may need to reconcile a dependency tree against
the new location. `postInitialize` is a list of shell commands for that.

- They run in the **destination** worktree, in the order listed, after copying has finished
- They run even when there was nothing to copy
- Each one goes through the platform shell (`/bin/sh -c` on unix, `cmd.exe /c` on Windows), so pipes,
  redirection and `&&` work
- The first command to exit non-zero stops the rest and fails the init, reporting the exit code and
  the command's output. Later commands are assumed to depend on the earlier ones having succeeded
- A command that runs longer than 30 minutes is killed and the init fails

Because these come from a file in the source repository, they run with whatever privileges `wi` was
started with. Treat the config file as you would any other executable content in the repo.

## Behavior

### Discovers ignored files using git

Runs `git ls-files --others --ignored --exclude-standard` in the source directory. This handles all `.gitignore` complexity: nested `.gitignore` files, global gitignore, `.git/info/exclude`, negation patterns, and every other gitignore feature.

### Preserves directory structure

Files are copied to the same relative path in the destination. If `src/bin/Debug/app.dll` is ignored in the source, it will be copied to `src/bin/Debug/app.dll` in the destination.

### Preserves symbolic links and permissions

A symbolic link is recreated as a link to the same raw target, rather than having the linked content
copied. That matters twice over: a relative link keeps resolving inside the destination tree instead
of pointing back at the source repository, and a link to a *directory* can be copied at all —
dereferencing one means opening a directory as a file, which fails outright.

A regular file keeps its unix permission bits, so an executable arrives executable. Copying the bytes
alone is what turns a package manager's `node_modules/.bin` shims into non-executable files and makes
the worktree fail to start with `Permission denied`.

On Windows, where creating a link can be denied outright, a link to a file falls back to copying its
content; a link to a directory is reported as a failure.

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

- `wi_init(sourcePath?, destinationPath?, ignorePaths?, includePaths?)` - Copy all gitignored files from source to destination, then run any `postInitialize` commands the source repo's `WorktreeConfig.json` declares (paths auto-detected when omitted and server is running inside a worktree)
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
