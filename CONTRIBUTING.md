<!-- omit in toc -->
# Contributing to YouTube Music Desktop

First off, thanks for taking the time to contribute! ❤️

All types of contributions are encouraged and valued. See the [Table of Contents](#table-of-contents) for different ways to help and details about how this project handles them. Please make sure to read the relevant section before making your contribution. It will make it a lot easier for us maintainers and smooth out the experience for all involved. The community looks forward to your contributions. 🎉

> [!TIP]
> If you like the project, but just don't have time to contribute, that's fine. There are other easy ways to support the project and show your appreciation, which we would also be very happy about:
> - Star the project
> - Tweet about it
> - Refer this project in your project's readme
> - Mention the project at local meetups and tell your friends/colleagues

<!-- omit in toc -->
## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [I Have a Question](#i-have-a-question)
- [I Want To Contribute](#i-want-to-contribute)
- [Reporting Bugs](#reporting-bugs)
- [Suggesting Enhancements](#suggesting-enhancements)
- [Your First Code Contribution](#your-first-code-contribution)
- [Improving The Documentation](#improving-the-documentation)
- [Styleguides](#styleguides)
- [Commit Messages](#commit-messages)
- [Join The Project Team](#join-the-project-team)


## Code of Conduct

This project and everyone participating in it is governed by the
[YouTube Music Desktop Code of Conduct](https://github.com/XeroxDev/YouTubeMusicStreamer/blob/CODE_OF_CONDUCT.md).
By participating, you are expected to uphold this code. Please report unacceptable behavior
to @xeroxdev on Discord


## I Have a Question

Before you ask a question, it is best to search for existing [Issues](https://github.com/XeroxDev/YouTubeMusicStreamer/issues) that might help you. In case you have found a suitable issue and still need clarification, you can write your question in this issue. It is also advisable to search the internet for answers first.

If you then still feel the need to ask a question and need clarification, we recommend the following:

- Open an [Issue](https://github.com/XeroxDev/YouTubeMusicStreamer/issues/new).
- Provide as much context as you can about what you're running into.
- Provide project and platform versions (app version, windows version, etc), depending on what seems relevant.

We will then take care of the issue as soon as possible.

## I Want To Contribute

> ### Legal Notice <!-- omit in toc -->
> When contributing to this project, you must agree that you have authored 100% of the content, that you have the necessary rights to the content and that the content you contribute may be provided under the project licence.

### Reporting Bugs

<!-- omit in toc -->
#### Before Submitting a Bug Report

A good bug report shouldn't leave others needing to chase you up for more information. Therefore, we ask you to investigate carefully, collect information and describe the issue in detail in your report. Please complete the following steps in advance to help us fix any potential bug as fast as possible.

- Make sure that you are using the latest version.
- Determine if your bug is really a bug and not an error on your side e.g. using incompatible environment components/versions. If you are looking for support, you might want to check [this section](#i-have-a-question)).
- To see if other users have experienced (and potentially already solved) the same issue you are having, check if there is not already a bug report existing for your bug or error in the [bug tracker](https://github.com/XeroxDev/YouTubeMusicStreamer/issues?q=label%3A%22Type%3A%20Bug%22).
- Also make sure to search the internet to see if users outside the GitHub community have discussed the issue.
- Collect information about the bug:
- Stack trace (Traceback)
- OS, Platform and Version (Windows, Linux, macOS, x86, ARM)
- Version of the interpreter, compiler, SDK, runtime environment, package manager, depending on what seems relevant.
- Possibly your input and the output
- Can you reliably reproduce the issue? And can you also reproduce it with older versions?

<!-- omit in toc -->
#### How Do I Submit a Good Bug Report?

> You must never report security related issues, vulnerabilities or bugs including sensitive information to the issue tracker, or elsewhere in public. Instead sensitive bugs must be sent to @xeroxdev on Discord.

We use GitHub issues to track bugs and errors. If you run into an issue with the project:

- Open an [Issue](https://github.com/XeroxDev/YouTubeMusicStreamer/issues/new).
- Explain the behavior you would expect and the actual behavior.
- Please provide as much context as possible and describe the *reproduction steps* that someone else can follow to recreate the issue on their own. This usually includes your code. For good bug reports you should isolate the problem and create a reduced test case.
- Provide the information you collected in the previous section.

Once it's filed:

- The project team will label the issue accordingly.
- A team member will try to reproduce the issue with your provided steps. If there are no reproduction steps or no obvious way to reproduce the issue, the team will ask you for those steps and mark the issue as either `Status: Can't reproduce` or `Status: More details required`. Bugs with the `Status: Can't reproduce` or `Status: More details required` tag will not be addressed until they are reproduced.
- If the team is able to reproduce the issue, it will be marked `Status: Pending`, as well as possibly other tags (such as `Priority: x`), and the issue will be left to be [implemented by someone](#your-first-code-contribution).

### Suggesting Enhancements

This section guides you through submitting an enhancement suggestion for YouTube Music Desktop, **including completely new features and minor improvements to existing functionality**. Following these guidelines will help maintainers and the community to understand your suggestion and find related suggestions.

<!-- omit in toc -->
#### Before Submitting an Enhancement

- Make sure that you are using the latest version.
- Read the [documentation](https://help.xeroxdev.de/en/apps/youtube-music-streamer/) carefully and find out if the functionality is already covered, maybe by an individual configuration.
- Perform a [search](https://github.com/XeroxDev/YouTubeMusicStreamer/issues) to see if the enhancement has already been suggested. If it has, add a comment to the existing issue instead of opening a new one.
- Find out whether your idea fits with the scope and aims of the project. It's up to you to make a strong case to convince the project's developers of the merits of this feature. Keep in mind that we want features that will be useful to the majority of our users and not just a small subset.

<!-- omit in toc -->
#### How Do I Submit a Good Enhancement Suggestion?

Enhancement suggestions are tracked as [GitHub issues](https://github.com/XeroxDev/YouTubeMusicStreamer/issues).

- Use a **clear and descriptive title** for the issue to identify the suggestion.
- Provide a **step-by-step description of the suggested enhancement** in as many details as possible.
- **Describe the current behavior** and **explain which behavior you expected to see instead** and why. At this point you can also tell which alternatives do not work for you.
- You may want to **include screenshots or screen recordings** which help you demonstrate the steps or point out the part which the suggestion is related to. You can use [LICEcap](https://www.cockos.com/licecap/) to record GIFs on macOS and Windows, and the built-in [screen recorder in GNOME](https://help.gnome.org/users/gnome-help/stable/screen-shot-record.html.en) or [SimpleScreenRecorder](https://github.com/MaartenBaert/ssr) on Linux. <!-- this should only be included if the project has a GUI -->
- **Explain why this enhancement would be useful** to most YouTube Music Desktop users. You may also want to point out the other projects that solved it better and which could serve as inspiration.

### Your First Code Contribution
If you want to contribute code to YouTube Music Desktop, this section will guide you through the process of setting up your development environment and making your first contribution.

- Fork the repository on GitHub. To do this, click the "Fork" button in the top right corner of the repository page.
- Clone your forked repository to your local machine. You can do this by running the following command in your terminal:
  ```bash
  git clone https://github.com/<your-username>/YouTubeMusicStreamer.git
  ```
- Navigate to the cloned repository:
  ```bash
    cd YouTubeMusicStreamer
    ```
- Set up your development environment. You will need 
  - the latest version of dotnet (as of now .NET 9) SDK installed on your machine. You can download it from the [official .NET website](https://dotnet.microsoft.com/download).
  - everything required for MAUI development. You can find the instructions for setting up MAUI on the [.NET MAUI documentation](https://docs.microsoft.com/dotnet/maui/get-started/installation).
  - an IDE or text editor of your choice. [Rider](https://www.jetbrains.com/rider/), but you can use any IDE that supports C# and .NET development.
  - the [YouTube Music Desktop](https://github.com/ytmdesktop/ytmdesktop) app installed on your machine.
- Open the project in your IDE. If you are using Rider, you can open the solution file `YouTubeMusicStreamer.sln` in the root directory of the cloned repository.
- Copy the `example.env` file to `.env` and fill in the required values. The `.env` file contains the configuration for the app, such as Twitch API credentials, and other settings.
- Restore the project dependencies via the GUI of your IDE or by running the following command in your terminal:
  ```bash
  dotnet restore
  ```
- Build and run the project to make sure everything is set up correctly.
- Make your desired changes to the code. Either fix a bug, implement a new feature, or improve the documentation. Look into the [GitHub Issues](https://github.com/XeroxDev/YouTubeMusicStreamer/issues) to find issues you can work on. If you are not sure where to start, you can look for issues labeled with `Status: Pending`.
- Once you have made your changes, commit them to your local repository. We're using [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) for commit messages, so please follow the [commit message guidelines](#commit-messages).
- Push your changes to your forked repository on GitHub:
  ```bash
  git push origin <your-branch-name>
  ```
- Open a pull request on the original repository. You can do this by going to the "Pull requests" tab on the repository page and clicking the "New pull request" button. Make sure to select your forked repository and the branch you pushed your changes to.
- In the pull request description, explain what changes you made, why you made them, and any other relevant information. If your pull request is related to an existing issue, make sure to reference it in the description (e.g. "Fixes #123"), see [Linking a pull request to an issue](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/linking-a-pull-request-to-an-issue) for more information.
  - If you're changing visual elements, consider adding screenshots or screen recordings to help reviewers understand the changes.
- Wait for the project maintainers to review your pull request. They may ask for changes or provide feedback. Be responsive and make the necessary changes if requested.
- Once your pull request is approved, it will be merged into the main branch of the repository. Congratulations, you've made your first contribution to YouTube Music Desktop! 🎉

### Improving The Documentation
If you want to improve the documentation of YouTube Music Desktop, this section will guide you through the process of making your first contribution.

First you need to find the documentation you want to improve. Either the files in this project (e.g. README.md, CONTRIBUTING.md, etc.) or the [official help desk](https://github.com/XeroxDev/help.xeroxdev.de).
If you found the documentation you want to improve, you can follow the first few steps in the [Your First Code Contribution](#your-first-code-contribution) section to start making it locally.
The documentation is written in Markdown, and don't necessarily need compilation. This means you can edit the files directly in your IDE or text editor of choice, without the need to build the project.
For quick edits, you can also use GitHubs Online VSCode instance by hitting `.` (dot) in the repository.

Once you have made your changes, you can follow the steps in the [Your First Code Contribution](#your-first-code-contribution) section to commit and push your changes to your forked repository on GitHub and open a pull request on the original repository.

Internationalization is not yet supported, so please make sure everything is in English.

Usage of AI tools like ChatGPT, Copilot, etc. is allowed, but please make sure to review the changes made by the AI and ensure they are correct, fulfill the project's standards, are not plagiarized and are relevant to the documentation.
If you use AI tools, please mention it in the pull request description. 

## Styleguides
### Commit Messages
We follow the [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) specification for commit messages. 
This means that commit messages should be structured in a specific way to make it easier to understand the changes made in the commit.

This is very important because we use [release-please](https://github.com/googleapis/release-please) to automatically bump the version, generate release notes and changelogs based on the commit messages.

TL;DR of Conventional Commits:

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

#### Commit Types
The `<type>` is one of the following:

| Type         | Description                                                                                 |
|--------------|---------------------------------------------------------------------------------------------|
| **feat**     | A new feature                                                                               |
| **fix**      | A bug fix                                                                                   |
| **docs**     | Documentation only changes                                                                  |
| **style**    | Code style or formatting (white-space, formatting, missing semi-colons, etc.)               |
| **refactor** | A code change that neither fixes a bug nor adds a feature                                   |
| **perf**     | A code change that improves performance                                                     |
| **test**     | Adding missing or updating existing tests                                                   |
| **build**    | Changes that affect the build system or external dependencies (e.g. project files, scripts) |
| **ci**       | Changes to CI configuration and scripts                                                     |
| **chore**    | Other changes that don’t modify src or tests (e.g. tooling, housekeeping)                   |
| **revert**   | Reverts a previous commit                                                                   |

#### Commit Scopes

The `<scope>` is one of the following: (if you are not sure, you can leave it empty)

| Scope          | Description                                                                                                                                                                                                                                                                 |
|----------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **ui**         | All user-facing code & styling:<br/>- Blazor `.razor` pages & components<br/>- MAUI XAML views & layouts (`*.xaml` + code-behind)<br/>- CSS/SCSS under `Styles/…` (Bulma overrides, themes, utilities)<br/>- `wwwroot` HTML/CSS/JS assets                                   |
| **core**       | Core application logic & plumbing:<br/>- Domain models (`Models/…`), enums, interfaces, attributes<br/>- Extensions (`Extensions/…`) & utilities (`Utils/…`)<br/>- Startup & DI (`MauiProgram.cs`, `Program.cs`, `Services/App/*`)                                          |
| **connectors** | External-system connectors & adapters:<br/>- Twitch/YouTube services (`Services/Twitch/…`, `Services/YouTube/…`)<br/>- WebSocket clients & services (`WebSocketClients/`, `Services/WebSocket/…`)<br/>- Classes marked with `[WebSocketClient]`                             |
| **commands**   | Chat-command layer & orchestration:<br/>- `Commands/…` folder (`InfoCommand.cs`, `NextCommand.cs`, …)<br/>- Argument parsing & binding (`Services/Commands/ArgumentParser`, `Binding`)<br/>- Cooldown & execution (`Services/Commands/Cooldowns`)                           |
| **platform**   | Windows-specific glue & manifests:<br/>- `Platforms/Windows/*`, `Windows/OAuthWindow.cs`<br/>- Native interop (`NativeMethods.cs`)<br/>- App manifests (`app.manifest`, `Package.appxmanifest`)<br/>- Publish profiles (`Properties/PublishProfiles/*`)                     |
| **infra**      | Infrastructure & housekeeping:<br/>- CI & build (`.github/workflows/`, `scripts/…`)<br/>- Solution & project files (`.sln`, `.csproj`)<br/>- Config & env (`.env`, `example.env`, `release-please*.json`, `launchSettings.json`)<br/>- Dependency bumps (NuGet, Bulma, npm) |
| **tests**      | Automated tests & utilities (future):<br/>- Unit & integration tests under `YouTubeMusicStreamer.Tests/…`<br/>- Test fixtures, mocks, helpers                                                                                                                               |

#### Commit Message Examples

```
feat(ui): add dark-mode toggle to MainLayout
fix(core): handle null in AppSettings.SensitiveSettings
docs(infra): describe new commit types in CONTRIBUTING.md
style(ui): reformat Razor files with CLI formatter
refactor(commands): merge ArgumentParser and Binder services
perf(core): memoize AppSettings deserialization
test(tests): add unit tests for SettingsService
build(infra): bump .NET SDK to 7.0 in csproj files
ci(infra): extend GitHub Actions matrix for arm64
chore(infra): update .gitignore with log files
revert(ui): undo toggle-style change in MainLayout
```

But overall, should you forget something or do it wrong, don't worry too much about it. 
The maintainers will help you get it right, especially because we're, most of the time, squashing commits when merging pull requests.

## Join The Project Team
If you want to join the project team, you can do so by contributing to the project and showing your interest in the project.

The more you contribute, help, and show your interest in the project, the more likely you are to be invited to join the project team.

<!-- omit in toc -->
## Attribution
This guide is based on the [contributing.md](https://contributing.md/generator)!