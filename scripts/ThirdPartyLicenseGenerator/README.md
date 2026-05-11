Repo-local third-party license generator.

Purpose:
- replace `nuget-license` for this app's packaging/compliance workflow
- read the resolved NuGet dependency graph
- prefer local package license files
- fall back to canonical offline texts and strict text-only fetches when needed
