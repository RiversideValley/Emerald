# Account-provider build secrets

The packaged Emerald app receives its Microsoft and Ely.by OAuth configuration
through MSBuild properties. GitHub Actions passes those properties directly to
the existing Windows, macOS, and Linux publish commands, alongside the version
properties. The values are then embedded as assembly metadata by the project
files.

Add these four **repository Actions secrets** in GitHub under
**Settings → Secrets and variables → Actions**:

| Secret | Value |
| --- | --- |
| `EMERALD_MSFT_CLIENT_ID` | Microsoft/Xbox application client ID |
| `EMERALD_ELYBY_CLIENT_ID` | Ely.by OAuth application client ID |
| `EMERALD_ELYBY_CLIENT_SECRET` | Ely.by OAuth application client secret |
| `EMERALD_ELYBY_REDIRECT_URI` | The exact registered loopback redirect URI, including its path and trailing slash |

The workflow requires all four values for pushes to `main`/`release/**`, manual
`workflow_dispatch` runs, and pull requests whose branch belongs to
`RiversideValley/Emerald`. This includes repository branches used by Emerald's
owners and collaborators.

GitHub does not pass Actions secrets to public fork pull requests, regardless of
the author's repository role. Those builds receive empty account-property values
and are not release artifacts. Emerald treats the affected providers as
unavailable, so a secret-free artifact can still start and use configured
providers.

Never paste any of these values into tracked source. Local development can use
the ignored `Directory.Build.local.props` file described in
`Directory.Build.local.props.example`.
