# Disabled workflows

CI was moved back to [`.github/workflows/ci.yml`](../workflows/ci.yml) after fixing structural issues in the old 6-job pipeline (missing build artifacts for `--no-build` tests, silent `generate:api` skip, stub deploy job).

Do not copy `ci.yml` from this folder without reading the active workflow first.
