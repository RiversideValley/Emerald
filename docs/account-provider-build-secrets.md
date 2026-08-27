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

The workflow requires all four values for pushes to `main`/`release/**` and for
manual `workflow_dispatch` runs. Pull-request builds do not materialize these
secrets; this keeps credentials out of untrusted PR and fork execution. Those
build commands receive empty account-property values and are not release
artifacts.

Never paste any of these values into tracked source. Local development can use
the ignored `Directory.Build.local.props` file described in
`Directory.Build.local.props.example`.
